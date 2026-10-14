using System.Numerics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using DungeonSiege.Core.Characters;
using DungeonSiege.Core.Quests;

namespace DungeonSiege.App.Tests;

public class HudUiTests
{
    [AvaloniaFact]
    public void PortraitsFollowThePartyRoster()
    {
        var kit = TestKit.Open();
        Assert.Single(kit.Vm.Party);
        kit.Recruit("merik");
        kit.Play(0.1);
        Assert.Equal(2, kit.Vm.Party.Count);
        Assert.True(kit.Vm.Party[0].IsSelected);
    }

    [AvaloniaFact]
    public void SlotSwitchesTheLeadersWeapon()
    {
        var kit = TestKit.Open();
        kit.Vm.Slots[1].ActivateCommand.Execute(null);
        kit.Play(0.1);
        Assert.Equal(SkillType.Ranged, kit.Session.Party.Leader.ActiveSkill);
        Assert.True(kit.Vm.Slots[1].IsActive);
        Assert.False(kit.Vm.Slots[0].IsActive);
    }

    [AvaloniaFact]
    public void PauseFreezesTheWorldAndShowsThePlate()
    {
        var kit = TestKit.Open();
        kit.Vm.TogglePauseCommand.Execute(null);
        var time = kit.Session.Time;
        kit.Play(1);
        Assert.True(kit.Vm.IsPaused);
        Assert.Equal(time, kit.Session.Time);
    }

    [AvaloniaFact]
    public void HollinsDialogueStartsTheQuestAndTracker()
    {
        var kit = TestKit.Open();
        kit.Session.Interact(new Vector2(17, 32));
        kit.PlayUntil(() => kit.Vm.IsDialogueOpen, 20);
        Assert.Equal("Old Hollin", kit.Vm.DialogueSpeaker);
        while (kit.Vm.IsDialogueOpen) kit.Vm.ChooseDialogue(0);
        kit.Play(0.1);
        Assert.Equal(QuestStatus.Active, kit.Session.Quests.Get("travel_to_glacern")!.Status);
        Assert.Equal("Travel to Glacern", kit.Vm.QuestTitle);
    }

    [AvaloniaFact]
    public void WindowRendersAFrame()
    {
        var kit = TestKit.Open();
        kit.Play(0.5);
        var frame = kit.Window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Assert.Equal(1440, frame.PixelSize.Width);
    }
}
