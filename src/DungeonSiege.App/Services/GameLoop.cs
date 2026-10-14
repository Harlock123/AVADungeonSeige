using System.Diagnostics;
using Avalonia.Threading;

namespace DungeonSiege.App.Services;

/// <summary>
/// Drives the engine from the UI thread at ~60 Hz. The engine itself integrates in fixed
/// 30 Hz steps, so render jitter never changes gameplay.
/// </summary>
public sealed class GameLoop : IDisposable
{
    private readonly Stopwatch _clock = new();
    private readonly DispatcherTimer _timer;
    private TimeSpan _last;

    public GameLoop(Action<TimeSpan> onFrame)
    {
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(1000.0 / 60), DispatcherPriority.Render, (_, _) =>
        {
            var now = _clock.Elapsed;
            var elapsed = now - _last;
            _last = now;
            onFrame(elapsed);
        });
    }

    public void Start()
    {
        _clock.Restart();
        _last = TimeSpan.Zero;
        _timer.Start();
    }

    public void Dispose() => _timer.Stop();
}
