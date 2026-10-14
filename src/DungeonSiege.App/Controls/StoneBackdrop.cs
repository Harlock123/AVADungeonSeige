using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using static DungeonSiege.App.Controls.BrushCache;

namespace DungeonSiege.App.Controls;

/// <summary>
/// Procedural dressed-stone masonry used behind the HUD – no bitmap assets needed.
/// Deterministic, so it looks the same every launch.
/// </summary>
public sealed class StoneBackdrop : Control
{
    private static readonly string[] Shades = ["#2e2924", "#332d27", "#29241f", "#37312a", "#2b2621", "#312b25"];

    public override void Render(DrawingContext ctx)
    {
        var bounds = new Rect(Bounds.Size);
        ctx.DrawRectangle(Brush("#14110e"), null, bounds);

        var rng = new Random(1066);
        const double rowHeight = 22;
        for (double y = 0, row = 0; y < bounds.Height; y += rowHeight, row++)
        {
            var x = row % 2 == 0 ? -rng.Next(10, 40) : -rng.Next(30, 70);
            while (x < bounds.Width)
            {
                var w = rng.Next(46, 96);
                var block = new Rect(x + 1.5, y + 1.5, w - 3, rowHeight - 3);
                ctx.DrawRectangle(Brush(Shades[rng.Next(Shades.Length)]), null, block, 2, 2);
                // Chisel highlight along the top, shadow along the bottom.
                ctx.DrawLine(Pen("#6a5e50", 1, 70), block.TopLeft + new Point(2, 0.5), block.TopRight + new Point(-2, 0.5));
                ctx.DrawLine(Pen("#000000", 1, 120), block.BottomLeft + new Point(2, -0.5), block.BottomRight + new Point(-2, -0.5));
                // A few pits and cracks for weathering.
                if (rng.NextDouble() < 0.35)
                    ctx.DrawEllipse(Brush("#000000", 50), null,
                        new Point(block.X + rng.NextDouble() * block.Width, block.Y + rng.NextDouble() * block.Height), 1.5, 1.2);
                x += w;
            }
        }
        // Darken toward the bottom like a recessed panel.
        ctx.DrawRectangle(new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.FromArgb(0, 0, 0, 0), 0), new GradientStop(Color.FromArgb(110, 0, 0, 0), 1) },
        }, null, bounds);
    }
}
