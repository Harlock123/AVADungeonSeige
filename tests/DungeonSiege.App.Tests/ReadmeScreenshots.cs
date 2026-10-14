using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using DungeonSiege.Core.Characters;
using DungeonSiege.Core.Content;
using DungeonSiege.Core.World;

namespace DungeonSiege.App.Tests;

/// <summary>
/// The README's screenshots, rendered headlessly from the real window (no desktop is captured).
/// Nothing is written unless AVADS_README_SHOTS names a folder; <c>tools/screenshots.sh</c> sets it
/// to <c>screenshots/</c> and runs these. A fixed seed and frame-by-frame play keep the pictures the
/// same from run to run, so only a real change to the game's look shows up in git.
/// </summary>
public class ReadmeScreenshots
{
    private static readonly string? Folder =
        Environment.GetEnvironmentVariable("AVADS_README_SHOTS") is { Length: > 0 } dir ? dir : null;

    private static void Shoot(TestKit kit, string name)
    {
        kit.Pump();
        var frame = kit.Window.CaptureRenderedFrame() ?? throw new InvalidOperationException("no frame");
        if (Folder is null) return;
        Directory.CreateDirectory(Folder);
        frame.Save(Path.Combine(Folder, name + ".png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    /// <summary>Late-chapter party: everyone has some practice in the skills they use.</summary>
    private static void Seasoned(TestKit kit)
    {
        var p = kit.Session.Party.Members;
        p[0].AwardSkillExperience(SkillType.Melee, 900);
        p[0].AwardSkillExperience(SkillType.Ranged, 150);
        p[0].AwardSkillExperience(SkillType.CombatMagic, 90);
        foreach (var m in p.Skip(1)) m.AwardSkillExperience(m.ActiveSkill, 600);
    }

    private static Enemy Nearest(TestKit kit, string templateId) => kit.Session.World.Enemies
        .Where(e => e.IsAlive && e.Template.Id == templateId)
        .MinBy(e => Vector2.Distance(e.Position, kit.Session.Party.Leader.Position))!;

    [AvaloniaFact]
    public void FarmsteadDialogue()
    {
        var kit = TestKit.Open();
        kit.Session.Interact(new Vector2(17, 32));
        kit.PlayUntil(() => kit.Session.Dialogue.IsActive, 20);
        kit.Vm.ChooseDialogue(0); // "Who did this?"
        Shoot(kit, "farmstead-dialogue");
    }

    [AvaloniaFact]
    public void FarmsteadBattle()
    {
        var kit = TestKit.Open().HearHollin();
        var leader = kit.Session.Party.Leader;
        kit.Session.Interact(Nearest(kit, "krug_scout").Position);
        kit.PlayUntil(() => leader.Target is { } t && leader.EdgeDistanceTo(t) < 2, 30);
        kit.Play(1.6);
        Shoot(kit, "farmstead-battle");
    }

    [AvaloniaFact]
    public void RecruitMerik()
    {
        var kit = TestKit.Open().HearHollin();
        kit.Progress(Core.Content.ObjectiveType.Kill, "krug_scout", 3);
        kit.Teleport(78, 27).Play(0.5);
        kit.Session.Interact(new Vector2(84, 24));
        kit.PlayUntil(() => kit.Session.Dialogue.IsActive, 10);
        Shoot(kit, "recruit-merik");
    }

    [AvaloniaFact]
    public void CanyonSkirmish()
    {
        var kit = TestKit.Open().HearHollin();
        kit.Progress(ObjectiveType.Kill, "krug_scout", 3);
        kit.Recruit("merik");
        kit.Recruit("ilsa");
        Seasoned(kit);
        kit.Teleport(168, 30).Play(0.3);
        kit.Session.Interact(Nearest(kit, "krug_grunt").Position);
        kit.PlayUntil(() => kit.Session.World.Enemies.Count(e => e.State == EnemyState.Chasing) >= 2, 20);
        kit.Play(2.4);
        Shoot(kit, "canyon-skirmish");
    }

    [AvaloniaFact]
    public void BraakFight()
    {
        var kit = TestKit.Open().HearHollin();
        kit.Progress(ObjectiveType.Kill, "krug_scout", 3);
        kit.Progress(ObjectiveType.Reach, "krug_canyons");
        kit.Recruit("merik");
        kit.Recruit("ilsa");
        Seasoned(kit);
        kit.Teleport(274, 33).Play(0.3);
        kit.Session.Interact(Nearest(kit, "braak").Position);
        kit.PlayUntil(() => Nearest(kit, "braak").State == EnemyState.Chasing, 20);
        kit.Play(2.2);
        Shoot(kit, "braak-fight");
    }

    [AvaloniaFact]
    public void CharacterSheet()
    {
        var kit = TestKit.Open().HearHollin();
        kit.Recruit("merik");
        Seasoned(kit);
        kit.Teleport(96, 34).Play(4.5); // let the region title card fade
        kit.Vm.ToggleCharacterSheetCommand.Execute(null);
        Shoot(kit, "character-sheet");
    }

    [AvaloniaFact]
    public void PausedOrders()
    {
        var kit = TestKit.Open().HearHollin();
        kit.Progress(ObjectiveType.Kill, "krug_scout", 3);
        kit.Recruit("merik");
        kit.Teleport(100, 34).Play(4.5); // the field scouts close in as the title card fades
        kit.Vm.TogglePauseCommand.Execute(null);
        kit.Session.SetBehavior(kit.Session.Party.Members[1], Core.Parties.CombatBehavior.Wait);
        kit.Session.Interact(new Vector2(108, 39));
        kit.Play(0.1); // paused: no game time passes, but the frame redraws with the queued order
        Shoot(kit, "paused-orders");
    }

    [AvaloniaFact]
    public void GatesOfGlacern()
    {
        var kit = TestKit.Open().HearHollin();
        kit.Progress(ObjectiveType.Kill, "krug_scout", 3);
        kit.Progress(ObjectiveType.Reach, "krug_canyons");
        kit.Progress(ObjectiveType.Kill, "braak");
        kit.Recruit("merik");
        kit.Recruit("ilsa");
        Seasoned(kit);
        kit.Teleport(408, 32).Play(0.2);
        kit.Session.Interact(new Vector2(431, 31));
        kit.Play(2.4);
        Shoot(kit, "gates-of-glacern");
    }

    [AvaloniaFact]
    public void QuestComplete()
    {
        var kit = TestKit.Open().HearHollin();
        kit.Progress(ObjectiveType.Kill, "krug_scout", 3);
        kit.Progress(ObjectiveType.Reach, "krug_canyons");
        kit.Progress(ObjectiveType.Kill, "braak");
        kit.Recruit("merik");
        Seasoned(kit);
        kit.Teleport(436, 33).Play(0.5);
        kit.Session.Interact(new Vector2(443, 31));
        kit.PlayUntil(() => kit.Session.Dialogue.IsActive, 10);
        while (kit.Session.Dialogue.IsActive) kit.Vm.ChooseDialogue(0);
        kit.Play(1);
        Shoot(kit, "quest-complete");
    }
}
