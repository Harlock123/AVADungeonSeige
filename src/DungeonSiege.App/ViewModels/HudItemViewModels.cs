using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DungeonSiege.Core.Characters;
using DungeonSiege.Core.Engine;
using DungeonSiege.Core.Parties;

namespace DungeonSiege.App.ViewModels;

/// <summary>One of the four weapon/spell slots on the belt (Melee, Ranged, Combat, Nature).</summary>
public sealed partial class SlotViewModel(SkillType skill, string hotkey, Action<SlotViewModel> activate) : ObservableObject
{
    public SkillType Skill { get; } = skill;
    public string Hotkey { get; } = hotkey;
    public IRelayCommand ActivateCommand => _activate ??= new RelayCommand(() => activate(this));
    private IRelayCommand? _activate;

    [ObservableProperty] private string _iconKey = "IconEmpty";
    [ObservableProperty] private string _tooltip = "";
    [ObservableProperty] private string _levelText = "";
    [ObservableProperty] private bool _isAvailable;
    [ObservableProperty] private bool _isActive;

    public void Refresh(Character member)
    {
        var item = member.Loadout.GetValueOrDefault(Skill);
        IsAvailable = item is not null && member.IsAlive;
        IsActive = item is not null && member.ActiveSkill == Skill;
        IconKey = item?.Icon ?? "IconEmpty";
        LevelText = member.Skill(Skill).Level.ToString();
        Tooltip = item is null
            ? $"{GameSession.SkillName(Skill)}: nothing equipped"
            : $"{item.Name} [{Hotkey}]\n{GameSession.SkillName(Skill)} skill {member.Skill(Skill).Level}"
              + (item.ManaCost > 0 ? $"\nMana cost: {item.ManaCost:0}" : "")
              + (item.Description is { } d ? $"\n{d}" : "");
    }
}

public sealed partial class InventorySlotViewModel(string itemId, string name, string iconKey, string tint, string hotkey, Action<InventorySlotViewModel> use)
    : ObservableObject
{
    public string ItemId { get; } = itemId;
    public string IconKey { get; } = iconKey;
    public bool IsHealth { get; } = tint == "health";
    public bool IsMana { get; } = tint == "mana";
    public string Hotkey { get; } = hotkey;
    public string Tooltip { get; } = $"{name} [{hotkey}]";
    public IRelayCommand Command => _command ??= new RelayCommand(() => use(this));
    private IRelayCommand? _command;

    [ObservableProperty] private int _count;
}

public sealed partial class BehaviorOptionViewModel(CombatBehavior behavior, string label, string tooltip, Action<BehaviorOptionViewModel> choose)
    : ObservableObject
{
    public CombatBehavior Behavior { get; } = behavior;
    public string Label { get; } = label;
    public string Tooltip { get; } = tooltip;
    public IRelayCommand Command => _command ??= new RelayCommand(() => choose(this));
    private IRelayCommand? _command;

    [ObservableProperty] private bool _isActive;
}

public sealed class DialogueChoiceViewModel(int index, string text, Action<int> choose)
{
    public string Label { get; } = $"{index + 1}.  {text}";
    public IRelayCommand Command { get; } = new RelayCommand(() => choose(index));
}

public sealed class LogEntryViewModel(GameMessage message)
{
    public string Text { get; } = message.Text;
    public MessageKind Kind { get; } = message.Kind;
}
