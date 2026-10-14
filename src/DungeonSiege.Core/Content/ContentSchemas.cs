using System.Numerics;
using DungeonSiege.Core.Characters;
using DungeonSiege.Core.Parties;
using DungeonSiege.Core.World;

namespace DungeonSiege.Core.Content;

// ---------------------------------------------------------------------------
// Data contracts for chapter content. Everything the designer touches lives in
// Content/Data/*.json and deserialises into these immutable records.
// ---------------------------------------------------------------------------

public sealed record PointDef(float X, float Y)
{
    public Vector2 ToVector() => new(X, Y);
}

public sealed record RectDef(float X, float Y, float Width, float Height)
{
    public float Right => X + Width;
    public float Bottom => Y + Height;
    public Vector2 Center => new(X + Width / 2, Y + Height / 2);

    public bool Contains(Vector2 p) => p.X >= X && p.X <= Right && p.Y >= Y && p.Y <= Bottom;

    /// <summary>Distance from a point to the rectangle (0 when inside).</summary>
    public float DistanceTo(Vector2 p)
    {
        var dx = MathF.Max(MathF.Max(X - p.X, 0), p.X - Right);
        var dy = MathF.Max(MathF.Max(Y - p.Y, 0), p.Y - Bottom);
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}

// ----- Items ---------------------------------------------------------------

public enum ItemKind { Weapon, Spell, Potion }

public enum SpellEffect { Damage, Heal }

public sealed record ItemDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public ItemKind Kind { get; init; }

    /// <summary>The skill trained by using this item (classless progression).</summary>
    public SkillType Skill { get; init; }

    public SpellEffect Effect { get; init; } = SpellEffect.Damage;
    public float MinPower { get; init; }
    public float MaxPower { get; init; }
    public float Range { get; init; } = 1.6f;
    public float Cooldown { get; init; } = 1.2f;
    public float ManaCost { get; init; }
    public float RestoreHealth { get; init; }
    public float RestoreMana { get; init; }
    public string Icon { get; init; } = "IconSword";
    public string? Description { get; init; }
}

// ----- Characters & enemies ---------------------------------------------------

public sealed record CharacterTemplate
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Title { get; init; } = "";
    public string PortraitColor { get; init; } = "#6b4a2b";
    public float Strength { get; init; } = Character.BaseAttribute;
    public float Dexterity { get; init; } = Character.BaseAttribute;
    public float Intelligence { get; init; } = Character.BaseAttribute;
    public Dictionary<SkillType, int> Skills { get; init; } = [];

    /// <summary>One optional item per skill slot: Melee, Ranged, CombatMagic, NatureMagic.</summary>
    public Dictionary<SkillType, string> Loadout { get; init; } = [];
    public SkillType ActiveSkill { get; init; } = SkillType.Melee;
    public CombatBehavior Behavior { get; init; } = CombatBehavior.Aggressive;
}

public enum EnemyRank { Minion, Elite, Boss }

public sealed record EnemyTemplate
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public string[] Tags { get; init; } = [];
    public EnemyRank Rank { get; init; } = EnemyRank.Minion;
    public float MaxHealth { get; init; } = 20;
    public float Armor { get; init; }
    public float MinDamage { get; init; } = 1;
    public float MaxDamage { get; init; } = 3;
    public float AttackRange { get; init; } = 1.5f;
    public float AttackCooldown { get; init; } = 1.5f;
    public float MoveSpeed { get; init; } = 3.5f;
    public float AggroRadius { get; init; } = 10;
    public float LeashRadius { get; init; } = 25;
    public float Experience { get; init; } = 10;
    public int GoldMin { get; init; }
    public int GoldMax { get; init; }
    public float PotionDropChance { get; init; }
    public float Radius { get; init; } = 0.6f;
    public string Color { get; init; } = "#7a5c3a";
    public bool Ranged { get; init; }
}

// ----- World -------------------------------------------------------------------

public sealed record PropPlacement(PropKind Kind, float X, float Y, float W = 1, float H = 1);

public sealed record PropScatter(PropKind Kind, int Count, float MinSize = 0.8f, float MaxSize = 1.4f);

public sealed record SpawnDefinition
{
    public required string Id { get; init; }
    public required string Enemy { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public int Count { get; init; } = 1;
    public float Spread { get; init; } = 3;
}

public sealed record NpcDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public string? Dialogue { get; init; }
    public string Color { get; init; } = "#c8b27a";

    /// <summary>If set, the dialogue starts on its own when the leader comes this close.</summary>
    public float? AutoTalkRadius { get; init; }
    public bool Wounded { get; init; }
}

public enum TriggerType { Enter }

public sealed record RegionTrigger
{
    public required string Id { get; init; }
    public TriggerType On { get; init; } = TriggerType.Enter;
    public string? Message { get; init; }
    public string? Dialogue { get; init; }
}

public sealed record RegionDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Subtitle { get; init; } = "";
    public required RectDef Bounds { get; init; }
    public string GroundColor { get; init; } = "#4a5a2a";
    public string AccentColor { get; init; } = "#3a4a22";
    public int Seed { get; init; }
    public List<PropPlacement> Props { get; init; } = [];
    public List<PropScatter> Scatter { get; init; } = [];
    public List<SpawnDefinition> Spawns { get; init; } = [];
    public List<NpcDefinition> Npcs { get; init; } = [];
    public List<RegionTrigger> Triggers { get; init; } = [];
}

// ----- Dialogue ------------------------------------------------------------------

public sealed record DialogueChoice
{
    public required string Text { get; init; }

    /// <summary>Next node id; null ends the conversation.</summary>
    public string? Next { get; init; }

    /// <summary>Script actions, e.g. <c>quest.start:travel_to_glacern</c>, <c>recruit:merik</c>, <c>give:potion_health:2</c>.</summary>
    public List<string> Actions { get; init; } = [];
}

public sealed record DialogueNode
{
    public required string Id { get; init; }
    public required string Speaker { get; init; }
    public required string Text { get; init; }
    public List<DialogueChoice> Choices { get; init; } = [];
}

public sealed record DialogueDefinition
{
    public required string Id { get; init; }
    public required string Start { get; init; }
    public List<DialogueNode> Nodes { get; init; } = [];

    public DialogueNode? Node(string id) => Nodes.Find(n => n.Id == id);
}

// ----- Quests ----------------------------------------------------------------------

public enum ObjectiveType { Talk, Kill, Reach, Flag }

public sealed record ObjectiveDefinition
{
    public required string Id { get; init; }
    public required string Description { get; init; }
    public ObjectiveType Type { get; init; }

    /// <summary>NPC id, enemy id or tag, region id, or flag name depending on <see cref="Type"/>.</summary>
    public required string Target { get; init; }
    public int Count { get; init; } = 1;
    public bool Optional { get; init; }
}

public sealed record QuestDefinition
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string Summary { get; init; } = "";
    public List<ObjectiveDefinition> Objectives { get; init; } = [];
    public string CompletionText { get; init; } = "";
    public int RewardGold { get; init; }
}

// ----- Chapter -----------------------------------------------------------------------

public sealed record ChapterDefinition
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public List<string> Intro { get; init; } = [];
    public required string Protagonist { get; init; }
    public required PointDef StartPosition { get; init; }
    public Dictionary<string, int> StartingItems { get; init; } = [];
    public int StartingGold { get; init; }
}
