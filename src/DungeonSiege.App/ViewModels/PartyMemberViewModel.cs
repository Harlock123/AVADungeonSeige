using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DungeonSiege.Core.Characters;
using DungeonSiege.Core.Engine;

namespace DungeonSiege.App.ViewModels;

/// <summary>Portrait + character-sheet projection of a <see cref="Character"/>.</summary>
public sealed partial class PartyMemberViewModel : ObservableObject
{
    public PartyMemberViewModel(Character character, Action<PartyMemberViewModel> select)
    {
        Character = character;
        SelectCommand = new RelayCommand(() => select(this));
        Skills = new(Enum.GetValues<SkillType>().Select(s => new SkillViewModel(s)));
    }

    public Character Character { get; }
    public IRelayCommand SelectCommand { get; }
    public ObservableCollection<SkillViewModel> Skills { get; }

    public string Name => Character.Name;
    public string Title => Character.Title;
    public string Initial => Character.Name.TrimStart().Split(' ')[^1][..1];
    public string PortraitColor => Character.PortraitColor;

    [ObservableProperty] private double _health;
    [ObservableProperty] private double _maxHealth;
    [ObservableProperty] private double _mana;
    [ObservableProperty] private double _maxMana;
    [ObservableProperty] private string _healthText = "";
    [ObservableProperty] private string _manaText = "";
    [ObservableProperty] private int _level;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isDown;
    [ObservableProperty] private string _strength = "";
    [ObservableProperty] private string _dexterity = "";
    [ObservableProperty] private string _intelligence = "";
    [ObservableProperty] private string _behavior = "";
    [ObservableProperty] private string _activeWeapon = "";

    public void Refresh(bool selected)
    {
        var c = Character;
        Health = c.Health;
        MaxHealth = c.MaxHealth;
        Mana = c.Mana;
        MaxMana = c.MaxMana;
        HealthText = $"{c.Health:0} / {c.MaxHealth:0}";
        ManaText = $"{c.Mana:0} / {c.MaxMana:0}";
        Level = c.Level;
        IsSelected = selected;
        IsDown = !c.IsAlive;
        Strength = $"{c.Strength:0.0}";
        Dexterity = $"{c.Dexterity:0.0}";
        Intelligence = $"{c.Intelligence:0.0}";
        Behavior = c.Behavior.ToString();
        ActiveWeapon = c.ActiveItem?.Name ?? "Bare hands";
        foreach (var skill in Skills) skill.Refresh(c.Skill(skill.Type));
    }
}

public sealed partial class SkillViewModel(SkillType type) : ObservableObject
{
    public SkillType Type { get; } = type;
    public string Name { get; } = GameSession.SkillName(type);

    [ObservableProperty] private int _level;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _experienceText = "";

    public void Refresh(Skill skill)
    {
        Level = skill.Level;
        Progress = skill.Progress * 100;
        ExperienceText = $"{skill.Experience:0} / {skill.ExperienceToNext:0} xp";
    }
}
