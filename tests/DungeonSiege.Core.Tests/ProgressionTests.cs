using DungeonSiege.Core.Characters;
using DungeonSiege.Core.Content;

namespace DungeonSiege.Core.Tests;

public class ProgressionTests
{
    private static readonly ContentDatabase Content = ContentDatabase.LoadEmbedded();

    private static Character NewFarmer() => new(Content.Characters["farmer"], Content.Items);

    [Theory]
    [InlineData(SkillType.Melee)]
    [InlineData(SkillType.Ranged)]
    [InlineData(SkillType.CombatMagic)]
    [InlineData(SkillType.NatureMagic)]
    public void UsingASkillRaisesItsPrimaryAttributeMost(SkillType skill)
    {
        var c = NewFarmer();
        var (str, dex, intel) = (c.Strength, c.Dexterity, c.Intelligence);

        var ups = c.AwardSkillExperience(skill, 1_000);

        Assert.NotEmpty(ups);
        Assert.Equal(ups.Count, c.Skill(skill).Level);
        var gains = new Dictionary<string, float>
        {
            ["str"] = c.Strength - str, ["dex"] = c.Dexterity - dex, ["int"] = c.Intelligence - intel,
        };
        var expected = skill switch
        {
            SkillType.Melee => "str",
            SkillType.Ranged => "dex",
            _ => "int",
        };
        Assert.Equal(expected, gains.MaxBy(g => g.Value).Key);
    }

    [Fact]
    public void OtherSkillsAreUntouched()
    {
        var c = NewFarmer();
        c.AwardSkillExperience(SkillType.Ranged, 500);
        Assert.Equal(0, c.Skill(SkillType.Melee).Level);
        Assert.Equal(0, c.Skill(SkillType.NatureMagic).Level);
    }

    [Fact]
    public void StrengthGainsRaiseMaxAndCurrentHealth()
    {
        var c = NewFarmer();
        var before = c.MaxHealth;
        c.AwardSkillExperience(SkillType.Melee, 1_000);
        Assert.True(c.MaxHealth > before);
        Assert.Equal(c.MaxHealth, c.Health, 3);
    }

    [Fact]
    public void LevelIsSumOfSkills()
    {
        var c = NewFarmer();
        c.AwardSkillExperience(SkillType.Melee, 200);
        c.AwardSkillExperience(SkillType.CombatMagic, 200);
        Assert.Equal(c.Skills.Values.Sum(s => s.Level), c.Level);
    }
}
