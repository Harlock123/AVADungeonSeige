namespace DungeonSiege.Core.Characters;

/// <summary>A single trainable skill. Experience is earned only by using it.</summary>
public sealed class Skill(SkillType type, int level = 0)
{
    public SkillType Type { get; } = type;
    public int Level { get; private set; } = level;

    /// <summary>Experience accumulated towards the next level.</summary>
    public float Experience { get; private set; }

    public float ExperienceToNext => SkillCurve.Required(Level);
    public float Progress => Experience / ExperienceToNext;

    /// <returns>Number of levels gained.</returns>
    internal int AddExperience(float amount)
    {
        if (amount <= 0) return 0;
        Experience += amount;
        var gained = 0;
        while (Experience >= ExperienceToNext)
        {
            Experience -= ExperienceToNext;
            Level++;
            gained++;
        }
        return gained;
    }
}

public static class SkillCurve
{
    /// <summary>Experience needed to go from <paramref name="level"/> to level + 1.</summary>
    public static float Required(int level) => 25f * MathF.Pow(level + 1, 1.45f);
}

/// <summary>
/// How each skill level feeds the primary attributes – the heart of Dungeon Siege's
/// classless system: you become a fighter by fighting, a mage by casting.
/// </summary>
public static class AttributeGrowth
{
    public readonly record struct Gain(float Strength, float Dexterity, float Intelligence);

    public static Gain For(SkillType skill) => skill switch
    {
        SkillType.Melee => new(1.00f, 0.35f, 0.00f),
        SkillType.Ranged => new(0.35f, 1.00f, 0.00f),
        SkillType.CombatMagic => new(0.00f, 0.15f, 1.00f),
        SkillType.NatureMagic => new(0.15f, 0.00f, 1.00f),
        _ => throw new ArgumentOutOfRangeException(nameof(skill)),
    };
}

public readonly record struct SkillLevelUp(
    Character Character, SkillType Skill, int NewLevel, AttributeGrowth.Gain Gain);
