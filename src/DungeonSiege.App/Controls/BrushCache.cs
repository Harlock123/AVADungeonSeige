using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace DungeonSiege.App.Controls;

/// <summary>Immutable brushes/pens keyed by colour – the renderer runs every frame, so don't allocate.</summary>
internal static class BrushCache
{
    private static readonly Dictionary<(string, byte), IImmutableSolidColorBrush> Brushes = [];
    private static readonly Dictionary<(string, byte, double), ImmutablePen> Pens = [];

    public static IImmutableSolidColorBrush Brush(string hex, byte alpha = 255)
    {
        if (Brushes.TryGetValue((hex, alpha), out var brush)) return brush;
        var c = Color.Parse(hex);
        brush = new ImmutableSolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
        Brushes[(hex, alpha)] = brush;
        return brush;
    }

    public static ImmutablePen Pen(string hex, double thickness, byte alpha = 255)
    {
        if (Pens.TryGetValue((hex, alpha, thickness), out var pen)) return pen;
        pen = new ImmutablePen(Brush(hex, alpha), thickness, lineCap: PenLineCap.Round);
        Pens[(hex, alpha, thickness)] = pen;
        return pen;
    }

    public static IBrush Fade(string hex, float alpha01) => Brush(hex, (byte)(Math.Clamp(alpha01, 0, 1) * 255));
}
