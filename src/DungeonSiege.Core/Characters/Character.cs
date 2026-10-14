using DungeonSiege.Core.Content;
using DungeonSiege.Core.Parties;

namespace DungeonSiege.Core.Characters;

/// <summary>
/// A party member. There are no classes: Strength, Dexterity and Intelligence grow
/// purely as a side effect of skill levels, and skills grow purely through use.
/// </summary>
public sealed class Character : Actor
{
    public const float BaseAttribute = 10f;

    private readonly Dictionary<SkillType, Skill> _skills;
    private readonly Dictionary<SkillType, ItemDefinition> _loadout;

    public Character(CharacterTemplate template, IReadOnlyDictionary<string, ItemDefinition> items)
    {
        TemplateId = template.Id;
        Name = template.Name;
        Title = template.Title;
        PortraitColor = template.PortraitColor;
        Strength = template.Strength;
        Dexterity = template.Dexterity;
        Intelligence = template.Intelligence;
        _skills = Enum.GetValues<SkillType>().ToDictionary(
            t => t, t => new Skill(t, template.Skills.GetValueOrDefault(t)));
        _loadout = template.Loadout.ToDictionary(kv => kv.Key, kv => items[kv.Value]);
        ActiveSkill = _loadout.ContainsKey(template.ActiveSkill) ? template.ActiveSkill : _loadout.Keys.FirstOrDefault();
        Behavior = template.Behavior;
        Health = MaxHealth;
        Mana = MaxMana;
    }

    public string TemplateId { get; }
    public string Title { get; }
    public string PortraitColor { get; }
    public override Faction Faction => Faction.Party;
    public override float MoveSpeed => 5.2f + Dexterity * 0.01f;

    // ----- Attributes ----------------------------------------------------------

    public float Strength { get; private set; }
    public float Dexterity { get; private set; }
    public float Intelligence { get; private set; }

    public override float MaxHealth => 20f + Strength * 3f + Dexterity * 1f + Intelligence * 0.5f;
    public float MaxMana => 5f + Intelligence * 2.5f + Dexterity * 0.25f;
    public float Mana { get; private set; }

    /// <summary>"Uber level": the sum of all skill levels, like the original game.</summary>
    public int Level => _skills.Values.Sum(s => s.Level);

    public IReadOnlyDictionary<SkillType, Skill> Skills => _skills;
    public Skill Skill(SkillType type) => _skills[type];

    // ----- Equipment / behaviour ----------------------------------------------

    public IReadOnlyDictionary<SkillType, ItemDefinition> Loadout => _loadout;
    public SkillType ActiveSkill { get; private set; }
    public ItemDefinition? ActiveItem => _loadout.GetValueOrDefault(ActiveSkill);

    public CombatBehavior Behavior { get; set; }
    public Order? Order { get; set; }

    /// <summary>Seconds spent unconscious; companions recover once the fighting stops.</summary>
    public float DownedTime { get; internal set; }

    public bool TrySetActiveSkill(SkillType skill)
    {
        if (!_loadout.ContainsKey(skill)) return false;
        ActiveSkill = skill;
        return true;
    }

    /// <summary>Damage (or healing) multiplier from the attribute and skill that drive an item.</summary>
    public float PowerMultiplier(SkillType skill)
    {
        var attribute = skill switch
        {
            SkillType.Melee => Strength,
            SkillType.Ranged => Dexterity,
            _ => Intelligence,
        };
        return 1f + (attribute - BaseAttribute) * 0.04f + _skills[skill].Level * 0.03f;
    }

    public float RollPower(ItemDefinition item, Random rng)
    {
        var raw = item.MinPower + (float)rng.NextDouble() * (item.MaxPower - item.MinPower);
        return raw * PowerMultiplier(item.Skill);
    }

    public bool TrySpendMana(float amount)
    {
        if (Mana < amount) return false;
        Mana -= amount;
        return true;
    }

    public void RestoreMana(float amount) => Mana = MathF.Min(MaxMana, Mana + amount);

    /// <summary>
    /// Trains a skill. Every level gained bumps attributes per <see cref="AttributeGrowth"/>,
    /// and current health/mana rise with their maximums so a level-up feels rewarding mid-fight.
    /// </summary>
    public IReadOnlyList<SkillLevelUp> AwardSkillExperience(SkillType skill, float experience)
    {
        var levels = _skills[skill].AddExperience(experience);
        if (levels == 0) return [];

        var results = new List<SkillLevelUp>(levels);
        var gain = AttributeGrowth.For(skill);
        for (var i = 0; i < levels; i++)
        {
            var oldMaxHealth = MaxHealth;
            var oldMaxMana = MaxMana;
            Strength += gain.Strength;
            Dexterity += gain.Dexterity;
            Intelligence += gain.Intelligence;
            if (IsAlive) Health += MaxHealth - oldMaxHealth;
            Mana += MaxMana - oldMaxMana;
            results.Add(new SkillLevelUp(this, skill, _skills[skill].Level - levels + i + 1, gain));
        }
        return results;
    }

    internal void Revive(float fraction)
    {
        Health = MathF.Max(1, MaxHealth * fraction);
        DownedTime = 0;
        Order = null;
        Target = null;
    }

    internal void Regenerate(float dt, bool inCombat)
    {
        if (!IsAlive) return;
        var hpRate = inCombat ? 0.15f : 1.2f + Strength * 0.05f;
        Heal(hpRate * dt);
        RestoreMana((0.4f + Intelligence * 0.04f) * (inCombat ? 1f : 2.5f) * dt);
    }
}
