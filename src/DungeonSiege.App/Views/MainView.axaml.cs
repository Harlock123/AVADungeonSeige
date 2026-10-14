using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Threading;
using DungeonSiege.App.ViewModels;

namespace DungeonSiege.App.Views;

public partial class MainView : UserControl
{
    private MainViewModel? _viewModel;

    public MainView() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.FrameAdvanced -= OnFrame;
            _viewModel.Log.CollectionChanged -= OnLogChanged;
        }

        _viewModel = DataContext as MainViewModel;
        if (_viewModel is not null)
        {
            _viewModel.FrameAdvanced += OnFrame;
            _viewModel.Log.CollectionChanged += OnLogChanged;
        }
    }

    /// <summary>The custom-drawn views repaint every frame; everything else is data-bound.</summary>
    private void OnFrame(object? sender, EventArgs e)
    {
        World.InvalidateVisual();
        Minimap.InvalidateVisual();
    }

    private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
            Dispatcher.UIThread.Post(() => LogScroller.ScrollToEnd(), DispatcherPriority.Background);
    }
}
