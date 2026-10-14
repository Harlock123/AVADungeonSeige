using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DungeonSiege.App.Services;
using DungeonSiege.App.ViewModels;

namespace DungeonSiege.App.Views;

public partial class MainWindow : Window
{
    private GameLoop? _loop;

    /// <summary>
    /// False in UI tests: they drive <see cref="MainViewModel.Tick"/> themselves so every frame,
    /// and so every README screenshot, is deterministic.
    /// </summary>
    public static bool RunGameLoop { get; set; } = true;

    public MainWindow()
    {
        InitializeComponent();
        // Tunnel so Space pauses even when a HUD button has keyboard focus.
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (RunGameLoop && ViewModel is { } vm)
        {
            _loop = new GameLoop(vm.Tick);
            _loop.Start();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _loop?.Dispose();
        base.OnClosed(e);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not { } vm) return;

        if (vm.IsDialogueOpen)
        {
            if (e.Key is >= Key.D1 and <= Key.D9) vm.ChooseDialogue(e.Key - Key.D1);
            e.Handled = e.Key is >= Key.D1 and <= Key.D9 or Key.Space;
            return;
        }

        switch (e.Key)
        {
            case Key.Space or Key.P: vm.TogglePauseCommand.Execute(null); break;
            case Key.C: vm.ToggleCharacterSheetCommand.Execute(null); break;
            case Key.Escape: vm.CloseOverlays(); break;
            case >= Key.D1 and <= Key.D4: vm.ActivateSlotAt(e.Key - Key.D1); break;
            case >= Key.F1 and <= Key.F8: vm.SelectMemberAt(e.Key - Key.F1); break;
            case Key.H: vm.UseItem("potion_health"); break;
            case Key.M: vm.UseItem("potion_mana"); break;
            default: return;
        }
        e.Handled = true;
    }
}
