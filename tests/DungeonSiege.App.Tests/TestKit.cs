using System.Numerics;
using Avalonia.Headless;
using Avalonia.Threading;
using DungeonSiege.App.ViewModels;
using DungeonSiege.App.Views;
using DungeonSiege.Core.Characters;
using DungeonSiege.Core.Content;
using DungeonSiege.Core.Engine;
using DungeonSiege.Core.Quests;

namespace DungeonSiege.App.Tests;

/// <summary>Opens the real window on a fixed-seed session and plays it frame by frame.</summary>
internal sealed class TestKit
{
    public const double Frame = 1 / 30.0;
    private static readonly ContentDatabase Content = ContentDatabase.LoadEmbedded();

    private TestKit(MainWindow window, MainViewModel vm)
    {
        Window = window;
        Vm = vm;
    }

    public MainWindow Window { get; }
    public MainViewModel Vm { get; }
    public GameSession Session => Vm.Session;

    public static TestKit Open(int seed = 2002, int width = 1440, int height = 900)
    {
        var vm = new MainViewModel(new GameSession(Content, seed));
        var window = new MainWindow { DataContext = vm, Width = width, Height = height };
        window.Show();
        var kit = new TestKit(window, vm);
        kit.Pump();
        return kit;
    }

    /// <summary>Plays <paramref name="seconds"/> of game time at 30 frames a second.</summary>
    public TestKit Play(double seconds)
    {
        for (var t = 0.0; t < seconds; t += Frame) Vm.Tick(TimeSpan.FromSeconds(Frame));
        Pump();
        return this;
    }

    /// <summary>Plays until <paramref name="done"/> holds (or the time limit runs out).</summary>
    public TestKit PlayUntil(Func<bool> done, double limitSeconds = 60)
    {
        for (var t = 0.0; t < limitSeconds && !done(); t += Frame) Vm.Tick(TimeSpan.FromSeconds(Frame));
        Pump();
        return this;
    }

    public void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Walks up to Old Hollin and hears him out, which starts "Travel to Glacern".</summary>
    public TestKit HearHollin()
    {
        Session.Interact(new Vector2(17, 32));
        PlayUntil(() => Session.Dialogue.IsActive, 20);
        while (Session.Dialogue.IsActive) Vm.ChooseDialogue(0);
        Pump();
        return this;
    }

    public Character Recruit(string templateId)
    {
        var member = Session.CreateCharacter(templateId);
        member.Position = Session.Party.Leader.Position + new Vector2(-2, 1.5f * Session.Party.Members.Count);
        Session.Party.TryRecruit(member);
        return member;
    }

    /// <summary>Moves the whole party, keeping formation, as if they had walked there.</summary>
    public TestKit Teleport(float x, float y)
    {
        var leader = Session.Party.Leader;
        var offset = new Vector2(x, y) - leader.Position;
        foreach (var m in Session.Party.Members)
        {
            m.Position += offset;
            m.Order = null;
        }
        Session.World.Update(leader.Position);
        return this;
    }

    /// <summary>Marks quest objectives done, for pictures set later in the chapter.</summary>
    public TestKit Progress(ObjectiveType type, string target, int amount = 1)
    {
        Session.Quests.Notify(type, target, amount);
        return this;
    }
}
