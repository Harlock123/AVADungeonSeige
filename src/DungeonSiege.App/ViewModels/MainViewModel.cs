using System.Collections.ObjectModel;
using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DungeonSiege.Core.Characters;
using DungeonSiege.Core.Content;
using DungeonSiege.Core.Engine;
using DungeonSiege.Core.Parties;
using DungeonSiege.Core.Quests;

namespace DungeonSiege.App.ViewModels;

/// <summary>
/// Bridges the engine to the HUD. The engine is polled once per frame (<see cref="Tick"/>);
/// observable properties only raise change notifications when values actually differ.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private const int MaxLogEntries = 200;
    private static readonly TimeSpan BannerDuration = TimeSpan.FromSeconds(4);

    private readonly Dictionary<Character, PartyMemberViewModel> _members = [];
    private TimeSpan _bannerRemaining;

    public MainViewModel() : this(new GameSession(ContentDatabase.LoadEmbedded())) { }

    public MainViewModel(GameSession session)
    {
        Session = session;
        Slots =
        [
            new(SkillType.Melee, "1", ActivateSlot),
            new(SkillType.Ranged, "2", ActivateSlot),
            new(SkillType.CombatMagic, "3", ActivateSlot),
            new(SkillType.NatureMagic, "4", ActivateSlot),
        ];
        Inventory =
        [
            new("potion_health", session.Content.Items["potion_health"].Name, "IconFlask", "health", "H", UseInventory),
            new("potion_mana", session.Content.Items["potion_mana"].Name, "IconFlask", "mana", "M", UseInventory),
        ];
        BehaviorOptions =
        [
            new(CombatBehavior.Aggressive, "Aggressive", "Engage any enemy in sight", SetBehavior),
            new(CombatBehavior.Defend, "Defend", "Only fight what threatens the party", SetBehavior),
            new(CombatBehavior.Wait, "Wait", "Hold position; strike only what comes in range", SetBehavior),
        ];

        session.MessageLogged += OnMessage;
        session.PauseChanged += paused => IsPaused = paused;
        session.Dialogue.Changed += SyncDialogue;
        session.RegionEntered += region =>
        {
            RegionName = region.Name;
            RegionSubtitle = region.Subtitle;
            ShowRegionBanner = true;
            _bannerRemaining = BannerDuration;
        };

        session.Start();
        Refresh();
    }

    public GameSession Session { get; }

    public ObservableCollection<PartyMemberViewModel> Party { get; } = [];
    public ObservableCollection<SlotViewModel> Slots { get; }
    public ObservableCollection<InventorySlotViewModel> Inventory { get; }
    public ObservableCollection<BehaviorOptionViewModel> BehaviorOptions { get; }
    public ObservableCollection<LogEntryViewModel> Log { get; } = [];
    public ObservableCollection<DialogueChoiceViewModel> DialogueChoices { get; } = [];

    /// <summary>Raised after every simulated frame so custom-drawn views can repaint.</summary>
    public event EventHandler? FrameAdvanced;

    [ObservableProperty] private PartyMemberViewModel? _selectedMember;
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private bool _isCharacterSheetOpen;
    [ObservableProperty] private string _regionName = "";
    [ObservableProperty] private string _regionSubtitle = "";
    [ObservableProperty] private bool _showRegionBanner;
    [ObservableProperty] private bool _hasQuest;
    [ObservableProperty] private string _questTitle = "";
    [ObservableProperty] private string _questObjective = "";
    [ObservableProperty] private string _goldText = "";
    [ObservableProperty] private string _streamingStatus = "";
    [ObservableProperty] private bool _isDialogueOpen;
    [ObservableProperty] private string _dialogueSpeaker = "";
    [ObservableProperty] private string _dialogueText = "";

    // ----- Frame -------------------------------------------------------------------

    public void Tick(TimeSpan elapsed)
    {
        Session.Advance(elapsed);

        if (ShowRegionBanner && (_bannerRemaining -= elapsed) <= TimeSpan.Zero)
            ShowRegionBanner = false;

        Refresh();
        FrameAdvanced?.Invoke(this, EventArgs.Empty);
    }

    private void Refresh()
    {
        var party = Session.Party;
        foreach (var member in party.Members)
        {
            if (_members.ContainsKey(member)) continue;
            var vm = new PartyMemberViewModel(member, m => SelectMember(m));
            _members[member] = vm;
            Party.Add(vm);
        }
        foreach (var vm in Party) vm.Refresh(vm.Character == party.Leader);

        SelectedMember = _members[party.Leader];
        foreach (var slot in Slots) slot.Refresh(party.Leader);
        foreach (var item in Inventory) item.Count = party.Inventory.Count(item.ItemId);
        foreach (var option in BehaviorOptions) option.IsActive = party.Leader.Behavior == option.Behavior;

        GoldText = $"{party.Gold} gold";
        StreamingStatus = $"{Session.World.Resident.Count}/{Session.World.TotalRegions} regions resident";
        if (Session.CurrentRegion is { } region) RegionName = region.Name;

        var quest = Session.Quests.Active.FirstOrDefault()
                    ?? Session.Quests.All.LastOrDefault(q => q.Status == QuestStatus.Completed);
        HasQuest = quest is not null;
        if (quest is not null)
        {
            QuestTitle = quest.Definition.Title;
            QuestObjective = quest.CurrentObjective is { } o
                ? o.Count > 1 ? $"{o.Description} ({quest.Progress(o)}/{o.Count})" : o.Description
                : "Completed";
        }
    }

    // ----- Commands -------------------------------------------------------------------

    [RelayCommand]
    private void TogglePause() => Session.TogglePause();

    [RelayCommand]
    private void ToggleCharacterSheet() => IsCharacterSheetOpen = !IsCharacterSheetOpen;

    [RelayCommand]
    private void InteractAt(Vector2 worldPoint)
    {
        if (!IsDialogueOpen) Session.Interact(worldPoint);
    }

    public void SelectMember(PartyMemberViewModel member) => Session.Select(member.Character);

    public void SelectMemberAt(int index)
    {
        if (index < Party.Count) SelectMember(Party[index]);
    }

    private void SetBehavior(BehaviorOptionViewModel option) =>
        Session.SetBehavior(Session.Party.Leader, option.Behavior);

    private void ActivateSlot(SlotViewModel slot) => Session.SetActiveSkill(Session.Party.Leader, slot.Skill);

    public void ActivateSlotAt(int index)
    {
        if (index < Slots.Count) ActivateSlot(Slots[index]);
    }

    private void UseInventory(InventorySlotViewModel item) => Session.UseItem(Session.Party.Leader, item.ItemId);

    public void UseItem(string itemId) => Session.UseItem(Session.Party.Leader, itemId);

    public void ChooseDialogue(int index)
    {
        if (index < DialogueChoices.Count) Session.ChooseDialogue(index);
    }

    public void CloseOverlays() => IsCharacterSheetOpen = false;

    // ----- Engine events ------------------------------------------------------------------

    private void OnMessage(GameMessage message)
    {
        Log.Add(new LogEntryViewModel(message));
        while (Log.Count > MaxLogEntries) Log.RemoveAt(0);
    }

    private void SyncDialogue()
    {
        DialogueChoices.Clear();
        var node = Session.Dialogue.Node;
        IsDialogueOpen = node is not null;
        if (node is null) return;

        DialogueSpeaker = node.Speaker;
        DialogueText = node.Text;
        if (node.Choices.Count == 0)
            DialogueChoices.Add(new DialogueChoiceViewModel(0, "Farewell.", Session.ChooseDialogue));
        else
            for (var i = 0; i < node.Choices.Count; i++)
                DialogueChoices.Add(new DialogueChoiceViewModel(i, node.Choices[i].Text, Session.ChooseDialogue));
    }
}
