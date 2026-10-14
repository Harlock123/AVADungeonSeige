using System.Numerics;
using DungeonSiege.Core.Characters;
using DungeonSiege.Core.Content;
using DungeonSiege.Core.Dialogue;
using DungeonSiege.Core.Parties;
using DungeonSiege.Core.Quests;
using DungeonSiege.Core.World;

namespace DungeonSiege.Core.Engine;

/// <summary>
/// Owns one play-through: the party, the streamed world, quests, dialogue and the
/// pausable fixed-timestep simulation. Has no knowledge of any UI framework – a host
/// calls <see cref="Advance"/> once per rendered frame and reads state back out.
/// </summary>
public sealed partial class GameSession
{
    /// <summary>Simulation runs at a fixed 30 Hz regardless of render rate.</summary>
    public const double FixedStep = 1.0 / 30.0;

    /// <summary>Clamp for hitches (debugger, window drag) to avoid a spiral of death.</summary>
    private const double MaxFrameTime = 0.25;

    private readonly Random _rng;
    private readonly List<CombatEffect> _effects = [];
    private readonly List<FloatingText> _floatingTexts = [];
    private double _accumulator;

    public GameSession(ContentDatabase content, int seed = 2002)
    {
        Content = content;
        _rng = new Random(seed);
        State = new WorldState();
        World = new WorldStreamer(content, State);
        World.RegionLoaded += _ => RegionsStreamedIn++;
        World.RegionUnloaded += _ => RegionsStreamedOut++;
        Quests = new QuestLog(content);
        Dialogue = new DialogueRunner();
        Party = new Party();
    }

    public ContentDatabase Content { get; }
    public WorldState State { get; }
    public WorldStreamer World { get; }
    public QuestLog Quests { get; }
    public DialogueRunner Dialogue { get; }
    public Party Party { get; }

    /// <summary>Simulated seconds elapsed (does not advance while paused or in dialogue).</summary>
    public double Time { get; private set; }
    public bool IsPaused { get; private set; }
    public bool IsRunning => !IsPaused && !Dialogue.IsActive;
    public float TimeScale { get; set; } = 1f;

    public RegionDefinition? CurrentRegion { get; private set; }
    public IReadOnlyList<CombatEffect> Effects => _effects;
    public IReadOnlyList<FloatingText> FloatingTexts => _floatingTexts;
    public int RegionsStreamedIn { get; private set; }
    public int RegionsStreamedOut { get; private set; }

    public event Action<GameMessage>? MessageLogged;
    public event Action<RegionDefinition>? RegionEntered;
    public event Action<bool>? PauseChanged;

    // ----- Lifecycle -----------------------------------------------------------------

    public void Start()
    {
        var chapter = Content.Chapter;
        var hero = CreateCharacter(chapter.Protagonist);
        hero.Position = chapter.StartPosition.ToVector();
        Party.TryRecruit(hero);
        foreach (var (item, count) in chapter.StartingItems) Party.Inventory.Add(item, count);
        Party.Gold = chapter.StartingGold;

        Log(MessageKind.Story, chapter.Title);
        foreach (var line in chapter.Intro) Log(MessageKind.Story, line);

        World.Update(hero.Position);
        UpdateCurrentRegion();
    }

    public Character CreateCharacter(string templateId) => new(Content.Characters[templateId], Content.Items);

    /// <summary>Feed real elapsed time; runs as many fixed steps as are due.</summary>
    public void Advance(TimeSpan realElapsed)
    {
        if (!IsRunning)
        {
            _accumulator = 0;
            return;
        }

        _accumulator += Math.Min(realElapsed.TotalSeconds, MaxFrameTime) * TimeScale;
        while (_accumulator >= FixedStep && IsRunning)
        {
            Step((float)FixedStep);
            _accumulator -= FixedStep;
        }
    }

    // ----- Player commands (all legal while paused: they queue and execute on resume) ----

    public void TogglePause() => SetPaused(!IsPaused);

    public void SetPaused(bool paused)
    {
        if (IsPaused == paused) return;
        IsPaused = paused;
        Log(MessageKind.System, paused ? "Paused. Orders given now are carried out on resume." : "Resumed.");
        PauseChanged?.Invoke(paused);
    }

    public void Select(Character member) => Party.Select(member);

    /// <summary>Context click in the world: attack an enemy, talk to an NPC, or move.</summary>
    public void Interact(Vector2 point)
    {
        var leader = Party.Leader;
        if (!leader.IsAlive) return;

        switch (HitTest(point))
        {
            case Enemy enemy:
                leader.Order = new AttackOrder(enemy);
                leader.Target = enemy;
                break;
            case Npc npc:
                leader.Order = new TalkOrder(npc);
                break;
            default:
                if (!World.IsWalkable(point)) return;
                leader.Order = new MoveOrder(point);
                leader.Target = null;
                break;
        }
    }

    /// <summary>What is under a world point – for click handling and cursor feedback.</summary>
    public object? HitTest(Vector2 point)
    {
        var enemy = World.Enemies
            .Where(e => e.IsAlive && Vector2.Distance(e.Position, point) <= e.Radius + 0.6f)
            .MinBy(e => Vector2.DistanceSquared(e.Position, point));
        if (enemy is not null) return enemy;
        return World.Npcs.FirstOrDefault(n => Vector2.Distance(n.Position, point) <= n.Radius + 0.8f);
    }

    public void SetBehavior(Character member, CombatBehavior behavior)
    {
        member.Behavior = behavior;
        Log(MessageKind.System, behavior switch
        {
            CombatBehavior.Aggressive => $"{member.Name} will engage enemies on sight.",
            CombatBehavior.Defend => $"{member.Name} will defend the party.",
            _ => $"{member.Name} will hold position.",
        });
    }

    public void SetActiveSkill(Character member, SkillType skill)
    {
        if (member.ActiveSkill != skill && member.TrySetActiveSkill(skill))
            Log(MessageKind.System, $"{member.Name} readies {member.ActiveItem!.Name}.");
    }

    public bool UseItem(Character member, string itemId)
    {
        var item = Content.Items[itemId];
        if (item.Kind != ItemKind.Potion || !member.IsAlive) return false;
        if (!Party.Inventory.TryConsume(itemId))
        {
            Log(MessageKind.System, $"You have no {item.Name} left.");
            return false;
        }

        var healed = member.Heal(item.RestoreHealth);
        member.RestoreMana(item.RestoreMana);
        if (healed > 0) AddText($"+{healed:0}", member.Position, FloatingTextKind.Heal);
        Log(MessageKind.Loot, $"{member.Name} drinks a {item.Name}.");
        return true;
    }

    public void ChooseDialogue(int index)
    {
        var npcId = Dialogue.NpcId;
        var (actions, ended) = Dialogue.Choose(index);
        foreach (var action in actions) RunAction(action, npcId);

        if (ended && npcId is not null)
        {
            State.Flags.Add($"talked:{npcId}");
            ApplyQuestUpdates(Quests.Notify(ObjectiveType.Talk, npcId));
        }
    }

    // ----- Scripting -------------------------------------------------------------------

    private void StartDialogue(string dialogueId, string? npcId)
    {
        foreach (var m in Party.Members) m.Order = null;
        Dialogue.Begin(Content.Dialogues[dialogueId], npcId);
    }

    /// <summary>Executes a content script action of the form <c>verb:arg[:arg]</c>.</summary>
    private void RunAction(string action, string? npcId)
    {
        var parts = action.Split(':', 3);
        switch (parts[0])
        {
            case "quest.start":
                if (Quests.Start(parts[1]) is { } started) ApplyQuestUpdates([started]);
                break;
            case "recruit":
                Recruit(parts[1], npcId);
                break;
            case "give":
                var count = parts.Length > 2 ? int.Parse(parts[2]) : 1;
                Party.Inventory.Add(parts[1], count);
                Log(MessageKind.Loot, $"Received {count} × {Content.Items[parts[1]].Name}.");
                break;
            case "gold":
                Party.Gold += int.Parse(parts[1]);
                Log(MessageKind.Loot, $"Received {parts[1]} gold.");
                break;
            case "flag":
                State.Flags.Add(parts[1]);
                ApplyQuestUpdates(Quests.Notify(ObjectiveType.Flag, parts[1]));
                break;
            case "heal":
                foreach (var m in Party.Members)
                {
                    if (m.IsAlive) m.Heal(m.MaxHealth);
                    else m.Revive(1f);
                    m.RestoreMana(m.MaxMana);
                }
                Log(MessageKind.Story, "Your party's wounds are tended.");
                break;
            case "story":
                Log(MessageKind.Story, action["story:".Length..]);
                break;
            default:
                Log(MessageKind.System, $"[content] Unknown action '{action}'.");
                break;
        }
    }

    private void Recruit(string templateId, string? npcId)
    {
        if (Party.Members.Count >= Party.MaxMembers)
        {
            Log(MessageKind.System, "Your party is full.");
            return;
        }

        var character = CreateCharacter(templateId);
        var npc = npcId is null ? null : World.Npcs.FirstOrDefault(n => n.Id == npcId);
        character.Position = npc?.Position ?? Party.Leader.Position;
        Party.TryRecruit(character);
        if (npc is not null)
        {
            World.RemoveNpc(npc);
            State.DepartedNpcs.Add(npc.Id);
        }
        Log(MessageKind.Story, $"{character.Name} joins your party.");
    }

    private void ApplyQuestUpdates(IEnumerable<QuestUpdate> updates)
    {
        foreach (var u in updates)
        {
            var q = u.Quest.Definition;
            switch (u.Kind)
            {
                case QuestUpdateKind.Started:
                    Log(MessageKind.Quest, $"New quest: {q.Title}. {q.Summary}");
                    break;
                case QuestUpdateKind.ObjectiveProgress:
                    Log(MessageKind.Quest, $"{u.Objective!.Description} ({u.Quest.Progress(u.Objective)}/{u.Objective.Count})");
                    break;
                case QuestUpdateKind.ObjectiveComplete:
                    Log(MessageKind.Quest, $"Objective complete: {u.Objective!.Description}");
                    break;
                case QuestUpdateKind.Completed:
                    Log(MessageKind.Quest, $"Quest complete: {q.Title}. {q.CompletionText}");
                    if (q.RewardGold > 0)
                    {
                        Party.Gold += q.RewardGold;
                        Log(MessageKind.Loot, $"Reward: {q.RewardGold} gold.");
                    }
                    break;
            }
        }
    }

    internal void Log(MessageKind kind, string text) =>
        MessageLogged?.Invoke(new GameMessage(kind, text, Time));

    private void AddEffect(EffectKind kind, Vector2 from, Vector2 to, float duration) =>
        _effects.Add(new CombatEffect(kind, from, to, Time, duration));

    private void AddText(string text, Vector2 position, FloatingTextKind kind) =>
        _floatingTexts.Add(new FloatingText(text, position, kind, Time));
}
