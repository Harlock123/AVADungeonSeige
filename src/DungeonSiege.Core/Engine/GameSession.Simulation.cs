using System.Numerics;
using DungeonSiege.Core.Characters;
using DungeonSiege.Core.Content;
using DungeonSiege.Core.Parties;
using DungeonSiege.Core.Quests;
using DungeonSiege.Core.World;

namespace DungeonSiege.Core.Engine;

// One fixed simulation step: streaming, party AI, enemy AI, combat and progression.
public sealed partial class GameSession
{
    private const float FollowerLeash = 24f;
    private const float CorpseLifetime = 30f;

    internal void Step(float dt)
    {
        Time += dt;

        if (!Party.Leader.IsAlive && Party.Members.FirstOrDefault(m => m.IsAlive) is { } survivor)
            Party.Select(survivor);

        World.Update(Party.Leader.Position);
        UpdateCurrentRegion();
        CheckAutoTalk();
        if (Dialogue.IsActive) return;

        var enemies = World.Enemies.ToList();
        foreach (var member in Party.Members) UpdateMember(member, enemies, dt);
        foreach (var enemy in enemies) UpdateEnemy(enemy, dt);
        SeparateActors(enemies);

        foreach (var region in World.Resident)
            region.Enemies.RemoveAll(e => !e.IsAlive && Time - e.DiedAt > CorpseLifetime);
        _effects.RemoveAll(e => e.Age(Time) >= 1);
        _floatingTexts.RemoveAll(t => t.Age(Time) >= 1);

        if (Party.IsWiped) HandlePartyWipe();
    }

    // ----- World / narrative --------------------------------------------------------

    private void UpdateCurrentRegion()
    {
        var region = World.RegionAt(Party.Leader.Position);
        if (region is null || region == CurrentRegion) return;

        CurrentRegion = region;
        State.VisitedRegions.Add(region.Id);
        RegionEntered?.Invoke(region);
        Log(MessageKind.System, $"Entering {region.Name}.");
        ApplyQuestUpdates(Quests.Notify(ObjectiveType.Reach, region.Id));

        foreach (var trigger in region.Triggers.Where(t => t.On == TriggerType.Enter))
        {
            if (!State.FiredTriggers.Add(trigger.Id)) continue;
            if (trigger.Message is not null) Log(MessageKind.Story, trigger.Message);
            if (trigger.Dialogue is not null && !Dialogue.IsActive) StartDialogue(trigger.Dialogue, null);
        }
    }

    private void CheckAutoTalk()
    {
        if (Dialogue.IsActive) return;
        var leader = Party.Leader;
        foreach (var npc in World.Npcs)
        {
            if (npc.Definition is not { AutoTalkRadius: { } radius, Dialogue: { } dialogue }) continue;
            if (State.Flags.Contains($"talked:{npc.Id}")) continue;
            if (Vector2.Distance(leader.Position, npc.Position) > radius) continue;
            StartDialogue(dialogue, npc.Id);
            return;
        }
    }

    private void HandlePartyWipe()
    {
        Log(MessageKind.Story, "Darkness takes your party... You awaken at the farmstead, battered but alive.");
        var start = Content.Chapter.StartPosition.ToVector();
        for (var i = 0; i < Party.Members.Count; i++)
        {
            var m = Party.Members[i];
            m.Revive(0.5f);
            m.Position = start + new Vector2(i * 1.5f, 1.5f);
        }
        foreach (var e in World.Enemies.Where(e => e.IsAlive))
        {
            e.Target = null;
            e.State = EnemyState.Returning;
        }
    }

    // ----- Party AI --------------------------------------------------------------------

    private void UpdateMember(Character m, List<Enemy> enemies, float dt)
    {
        if (!m.IsAlive)
        {
            m.DownedTime += dt;
            var threatened = enemies.Any(e => e is { IsAlive: true, State: EnemyState.Chasing }
                                              && Vector2.Distance(e.Position, m.Position) < 15);
            if (!threatened && m.DownedTime > 6)
            {
                m.Revive(0.25f);
                Log(MessageKind.Combat, $"{m.Name} staggers back to their feet.");
            }
            return;
        }

        var inCombat = Time - m.LastHitTime < 4 || m.Target is { IsAlive: true };
        m.Regenerate(dt, inCombat);
        m.AttackCooldown = MathF.Max(0, m.AttackCooldown - dt);

        // 1. Explicit orders always win.
        switch (m.Order)
        {
            case AttackOrder { Target: var target } when target.IsAlive:
                m.Target = target;
                Engage(m, target, dt, mayChase: true);
                return;
            case AttackOrder:
                m.Order = null;
                m.Target = null;
                break;
            case MoveOrder move:
                if (MoveTowards(m, move.Destination, m.MoveSpeed, dt, 0.25f)) m.Order = null;
                return;
            case TalkOrder talk:
                if (MoveTowards(m, talk.Npc.Position, m.MoveSpeed, dt, talk.Npc.Radius + m.Radius + 1.2f))
                {
                    m.Order = null;
                    if (talk.Npc.Definition.Dialogue is { } dialogue) StartDialogue(dialogue, talk.Npc.Id);
                }
                return;
        }

        // 2. Autonomous combat according to the member's behaviour.
        var item = m.ActiveItem;
        if (item is { Effect: SpellEffect.Heal })
        {
            if (TryHealAlly(m, item, dt)) return;
        }
        else if (AcquireTarget(m, enemies, item) is { } target)
        {
            m.Target = target;
            if (Engage(m, target, dt, mayChase: m.Behavior != CombatBehavior.Wait)) return;
        }
        else
        {
            m.Target = null;
        }

        // 3. Followers keep formation on the leader.
        if (m == Party.Leader || m.Behavior == CombatBehavior.Wait) return;
        var slot = Party.FormationSlot(m);
        var distance = Vector2.Distance(m.Position, slot);
        if (distance > 0.8f)
            MoveTowards(m, slot, m.MoveSpeed * (distance > 8 ? 1.6f : 1f), dt, 0.3f);
    }

    private Enemy? AcquireTarget(Character m, List<Enemy> enemies, ItemDefinition? item)
    {
        var range = item?.Range ?? 1.5f;
        var leaderPos = Party.Leader.Position;

        bool StillValid(Enemy e) => m.Behavior == CombatBehavior.Wait
            ? m.EdgeDistanceTo(e) <= range + 0.2f
            : Vector2.Distance(e.Position, leaderPos) <= FollowerLeash;

        if (m.Target is Enemy { IsAlive: true } current && StillValid(current)) return current;

        var candidates = enemies.Where(e => e.IsAlive);
        candidates = m.Behavior switch
        {
            CombatBehavior.Aggressive => candidates.Where(e =>
                Vector2.Distance(e.Position, m.Position) <= MathF.Max(12, range + 2)
                && Vector2.Distance(e.Position, leaderPos) <= FollowerLeash - 2),
            CombatBehavior.Defend => candidates.Where(e =>
                (e.Target is Character && Vector2.Distance(e.Position, leaderPos) <= 12)
                || m.EdgeDistanceTo(e) <= range + 0.5f),
            _ => candidates.Where(e => m.EdgeDistanceTo(e) <= range + 0.2f),
        };
        return candidates.MinBy(e => Vector2.DistanceSquared(e.Position, m.Position));
    }

    /// <returns>True if the character spent this step fighting (moving into range counts).</returns>
    private bool Engage(Character m, Actor target, float dt, bool mayChase)
    {
        if (m.ActiveItem is not { } item) return false;

        if (m.EdgeDistanceTo(target) > item.Range)
        {
            if (!mayChase) return false;
            MoveTowards(m, target.Position, m.MoveSpeed, dt, item.Range * 0.9f + m.Radius + target.Radius);
            return true;
        }

        m.Facing = Vector2.Normalize(target.Position - m.Position + new Vector2(1e-4f));
        if (m.AttackCooldown > 0) return true;

        if (item.ManaCost > 0 && !m.TrySpendMana(item.ManaCost))
        {
            var fallback = m.Loadout.ContainsKey(SkillType.Melee) ? SkillType.Melee : SkillType.Ranged;
            if (m.TrySetActiveSkill(fallback))
                Log(MessageKind.Combat, $"{m.Name} is out of mana and draws {m.ActiveItem!.Name}.");
            return true;
        }

        m.AttackCooldown = item.Cooldown;
        var dealt = target.TakeDamage(m.RollPower(item, _rng), m, Time);
        AddEffect(EffectFor(item.Skill), m.Position, target.Position, item.Skill == SkillType.Melee ? 0.2f : 0.35f);
        AddText($"{dealt:0}", target.Position, FloatingTextKind.Damage);

        // Usage-based progression: damage dealt trains whatever skill the item uses.
        Train(m, item.Skill, dealt * 0.5f);
        if (!target.IsAlive && target is Enemy enemy) OnEnemyKilled(enemy, m, item.Skill);
        return true;
    }

    private bool TryHealAlly(Character healer, ItemDefinition spell, float dt)
    {
        var patient = Party.Members
            .Where(a => a.IsAlive && a.Health < a.MaxHealth * 0.7f
                        && Vector2.Distance(a.Position, Party.Leader.Position) < FollowerLeash)
            .MinBy(a => a.Health / a.MaxHealth);
        if (patient is null) return false;

        if (healer.EdgeDistanceTo(patient) > spell.Range && healer != patient)
        {
            if (healer.Behavior == CombatBehavior.Wait) return false;
            MoveTowards(healer, patient.Position, healer.MoveSpeed, dt, spell.Range * 0.9f);
            return true;
        }
        if (healer.AttackCooldown > 0 || !healer.TrySpendMana(spell.ManaCost)) return false;

        healer.AttackCooldown = spell.Cooldown;
        var healed = patient.Heal(healer.RollPower(spell, _rng));
        AddEffect(EffectKind.Heal, healer.Position, patient.Position, 0.6f);
        AddText($"+{healed:0}", patient.Position, FloatingTextKind.Heal);
        Train(healer, spell.Skill, healed * 0.8f);
        return true;
    }

    private void OnEnemyKilled(Enemy enemy, Character killer, SkillType skill)
    {
        enemy.DiedAt = Time;
        State.ClearedSpawns.Add(enemy.SpawnKey);
        Log(MessageKind.Combat, $"{killer.Name} slays the {enemy.Name}.");
        Train(killer, skill, enemy.Template.Experience);

        var gold = _rng.Next(enemy.Template.GoldMin, enemy.Template.GoldMax + 1);
        if (gold > 0)
        {
            Party.Gold += gold;
            AddText($"+{gold} gold", enemy.Position + new Vector2(0, -0.8f), FloatingTextKind.Gold);
        }
        if (_rng.NextDouble() < enemy.Template.PotionDropChance)
        {
            Party.Inventory.Add("potion_health");
            Log(MessageKind.Loot, $"The {enemy.Name} dropped a {Content.Items["potion_health"].Name}.");
        }

        ApplyQuestUpdates(Quests.Notify(ObjectiveType.Kill, [enemy.Template.Id, .. enemy.Template.Tags]));
    }

    private void Train(Character member, SkillType skill, float experience)
    {
        foreach (var up in member.AwardSkillExperience(skill, experience))
        {
            var g = up.Gain;
            var gains = string.Join(", ", new[]
            {
                g.Strength > 0 ? $"+{g.Strength:0.##} Str" : null,
                g.Dexterity > 0 ? $"+{g.Dexterity:0.##} Dex" : null,
                g.Intelligence > 0 ? $"+{g.Intelligence:0.##} Int" : null,
            }.OfType<string>());
            Log(MessageKind.Skill, $"{member.Name}'s {SkillName(skill)} rises to {up.NewLevel}! ({gains})");
            AddText($"{SkillName(skill)} {up.NewLevel}!", member.Position + new Vector2(0, -1.2f), FloatingTextKind.LevelUp);
            AddEffect(EffectKind.LevelUp, member.Position, member.Position, 1.2f);
        }
    }

    public static string SkillName(SkillType skill) => skill switch
    {
        SkillType.CombatMagic => "Combat Magic",
        SkillType.NatureMagic => "Nature Magic",
        _ => skill.ToString(),
    };

    private static EffectKind EffectFor(SkillType skill) => skill switch
    {
        SkillType.Melee => EffectKind.Slash,
        SkillType.Ranged => EffectKind.Arrow,
        SkillType.CombatMagic => EffectKind.Zap,
        _ => EffectKind.Heal,
    };

    // ----- Enemy AI ----------------------------------------------------------------------

    private void UpdateEnemy(Enemy e, float dt)
    {
        if (!e.IsAlive) return;
        e.AttackCooldown = MathF.Max(0, e.AttackCooldown - dt);

        if (e.State == EnemyState.Returning)
        {
            if (MoveTowards(e, e.Home, e.MoveSpeed * 1.2f, dt, 0.3f))
            {
                e.State = EnemyState.Idle;
                e.ResetHealth();
            }
            return;
        }

        if (e.Target is not Character { IsAlive: true }) e.Target = null;
        if (e.Target is null)
        {
            var provoker = e.LastAttacker is Character { IsAlive: true } c && Time - e.LastHitTime < 5 ? c : null;
            e.Target = provoker ?? Party.Members
                .Where(m => m.IsAlive && e.DistanceTo(m) <= e.Template.AggroRadius)
                .MinBy(m => e.DistanceTo(m));
            if (e.Target is not null) AlertPack(e);
        }

        if (e.Target is { } target)
        {
            if (Vector2.Distance(e.Position, e.Home) > e.Template.LeashRadius)
            {
                e.State = EnemyState.Returning;
                e.Target = null;
                return;
            }

            e.State = EnemyState.Chasing;
            if (e.EdgeDistanceTo(target) > e.Template.AttackRange)
            {
                MoveTowards(e, target.Position, e.MoveSpeed, dt, e.Template.AttackRange * 0.9f + e.Radius + target.Radius);
                return;
            }

            e.Facing = Vector2.Normalize(target.Position - e.Position + new Vector2(1e-4f));
            if (e.AttackCooldown > 0) return;

            e.AttackCooldown = e.Template.AttackCooldown * (0.85f + 0.3f * (float)_rng.NextDouble());
            var damage = e.Template.MinDamage + (float)_rng.NextDouble() * (e.Template.MaxDamage - e.Template.MinDamage);
            var dealt = target.TakeDamage(damage, e, Time);
            AddEffect(e.Template.Ranged ? EffectKind.Spear : EffectKind.Claw, e.Position, target.Position, e.Template.Ranged ? 0.4f : 0.2f);
            AddText($"{dealt:0}", target.Position, FloatingTextKind.PartyDamage);

            if (!target.IsAlive)
            {
                Log(MessageKind.Combat, $"{target.Name} has been knocked unconscious by a {e.Name}!");
                if (target is Character fallen) fallen.Order = null;
                e.Target = null;
            }
            return;
        }

        // Idle: mill about the camp.
        e.State = EnemyState.Idle;
        e.WanderTimer -= dt;
        if (e.WanderTimer <= 0)
        {
            e.WanderTimer = 3 + (float)_rng.NextDouble() * 4;
            var angle = _rng.NextDouble() * Math.Tau;
            e.WanderTarget = e.Home + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * (float)(_rng.NextDouble() * 3);
        }
        if (e.WanderTarget is { } wander && MoveTowards(e, wander, e.MoveSpeed * 0.35f, dt, 0.2f))
            e.WanderTarget = null;
    }

    /// <summary>Krug hunt in packs: one sighting rouses its spawn-mates.</summary>
    private void AlertPack(Enemy e)
    {
        var group = e.SpawnKey[..e.SpawnKey.IndexOf('#')];
        foreach (var other in World.Enemies)
        {
            if (other == e || !other.IsAlive || other.Target is not null) continue;
            if (!other.SpawnKey.StartsWith(group, StringComparison.Ordinal)) continue;
            if (Vector2.Distance(other.Position, e.Position) > 10) continue;
            other.Target = e.Target;
            other.State = EnemyState.Chasing;
        }
    }

    // ----- Movement ----------------------------------------------------------------------

    /// <returns>True once within <paramref name="stopDistance"/> (or hopelessly blocked).</returns>
    private bool MoveTowards(Actor actor, Vector2 destination, float speed, float dt, float stopDistance)
    {
        // A little slack on arrival: easing in on the stop distance would otherwise leave the
        // actor a rounding error short forever, and the order would never complete.
        const float ArrivalSlack = 0.05f;
        var delta = destination - actor.Position;
        var distance = delta.Length();
        if (distance <= stopDistance + ArrivalSlack) return true;

        var dir = delta / distance;
        actor.Facing = dir;
        var step = MathF.Min(speed * dt, distance - stopDistance);
        var next = actor.Position + dir * step;

        if (World.IsWalkable(next)) actor.Position = next;
        else if (World.IsWalkable(actor.Position + new Vector2(dir.X * step, 0))) actor.Position += new Vector2(dir.X * step, 0);
        else if (World.IsWalkable(actor.Position + new Vector2(0, dir.Y * step))) actor.Position += new Vector2(0, dir.Y * step);
        else return true;
        return false;
    }

    /// <summary>Cheap circle separation so crowds don't stack on a single point.</summary>
    private void SeparateActors(List<Enemy> enemies)
    {
        var actors = new List<Actor>(Party.Members.Count + enemies.Count);
        actors.AddRange(Party.Members.Where(m => m.IsAlive));
        actors.AddRange(enemies.Where(e => e.IsAlive));

        for (var i = 0; i < actors.Count; i++)
        {
            for (var j = i + 1; j < actors.Count; j++)
            {
                var a = actors[i];
                var b = actors[j];
                var delta = b.Position - a.Position;
                var distance = delta.Length();
                var min = a.Radius + b.Radius;
                if (distance >= min || distance < 1e-4f) continue;

                var push = delta / distance * ((min - distance) * 0.5f);
                if (World.IsWalkable(a.Position - push)) a.Position -= push;
                if (World.IsWalkable(b.Position + push)) b.Position += push;
            }
        }
    }
}
