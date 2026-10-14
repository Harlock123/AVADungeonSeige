using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using DungeonSiege.Core.Engine;

namespace DungeonSiege.App.Converters;

public static class Converters
{
    /// <summary>Resource key (e.g. "IconSword") -> <see cref="Geometry"/> from Themes/Icons.axaml.</summary>
    public static readonly IValueConverter IconGeometry = new FuncValueConverter<string?, Geometry?>(key =>
        key is not null && Application.Current?.TryGetResource(key, null, out var value) == true
            ? value as Geometry
            : null);

    /// <summary>Colours the action log by message category.</summary>
    public static readonly IValueConverter MessageBrush = new FuncValueConverter<MessageKind, IBrush>(kind => kind switch
    {
        MessageKind.Story => new SolidColorBrush(Color.Parse("#E8DAB2")),
        MessageKind.Combat => new SolidColorBrush(Color.Parse("#D9906E")),
        MessageKind.Skill => new SolidColorBrush(Color.Parse("#9FD88A")),
        MessageKind.Loot => new SolidColorBrush(Color.Parse("#E0C060")),
        MessageKind.Quest => new SolidColorBrush(Color.Parse("#F0D27A")),
        _ => new SolidColorBrush(Color.Parse("#8E8574")),
    });

    /// <summary>Hex colour -> lit leather/heraldic portrait gradient.</summary>
    public static readonly IValueConverter PortraitBrush = new FuncValueConverter<string?, IBrush>(hex =>
    {
        var c = Color.TryParse(hex, out var parsed) ? parsed : Colors.SaddleBrown;
        return new RadialGradientBrush
        {
            GradientOrigin = new RelativePoint(0.5, 0.3, RelativeUnit.Relative),
            Center = new RelativePoint(0.5, 0.3, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Shade(c, 1.5), 0),
                new GradientStop(c, 0.45),
                new GradientStop(Shade(c, 0.25), 1),
            },
        };
    });

    public static Color Shade(Color c, double factor) => Color.FromArgb(
        c.A,
        (byte)Math.Clamp(c.R * factor, 0, 255),
        (byte)Math.Clamp(c.G * factor, 0, 255),
        (byte)Math.Clamp(c.B * factor, 0, 255));
}
