using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DungeonSiege.Core.Engine;
using static DungeonSiege.App.Controls.BrushCache;

namespace DungeonSiege.App.Controls;

/// <summary>Circular, north-up minimap centred on the party leader.</summary>
public sealed class MinimapView : Control
{
    public static readonly StyledProperty<GameSession?> SessionProperty =
        AvaloniaProperty.Register<MinimapView, GameSession?>(nameof(Session));

    /// <summary>World units visible across the minimap's diameter.</summary>
    public static readonly StyledProperty<double> RangeProperty =
        AvaloniaProperty.Register<MinimapView, double>(nameof(Range), 110);

    static MinimapView() => AffectsRender<MinimapView>(SessionProperty, RangeProperty);

    public GameSession? Session
    {
        get => GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    public double Range
    {
        get => GetValue(RangeProperty);
        set => SetValue(RangeProperty, value);
    }

    public override void Render(DrawingContext ctx)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        var rect = new Rect((Bounds.Width - size) / 2, (Bounds.Height - size) / 2, size, size);
        ctx.DrawEllipse(Brush("#0c0a08"), null, rect.Center, size / 2, size / 2);
        if (Session is not { } s) return;

        var scale = size / Range;
        var center = s.Party.Leader.Position;
        Point Map(Vector2 w) => new(rect.Center.X + (w.X - center.X) * scale, rect.Center.Y + (w.Y - center.Y) * scale);

        using (ctx.PushClip(new RoundedRect(rect, size / 2)))
        {
            // Explored regions are drawn in full; resident-but-unvisited ones are hinted.
            foreach (var region in s.Content.Regions)
            {
                var visited = s.State.VisitedRegions.Contains(region.Id);
                var resident = s.World.Resident.Any(r => r.Id == region.Id);
                if (!visited && !resident) continue;
                var b = region.Bounds;
                var tl = Map(new Vector2(b.X, b.Y));
                var r = new Rect(tl.X, tl.Y, b.Width * scale, b.Height * scale);
                ctx.DrawRectangle(Brush(region.GroundColor, visited ? (byte)220 : (byte)80), null, r);
            }

            foreach (var region in s.World.Resident)
                foreach (var prop in region.Props)
                    if (prop.Kind is Core.World.PropKind.Road)
                    {
                        var tl = Map(prop.Position - prop.Size / 2);
                        ctx.DrawRectangle(Brush("#a08a60", 160), null, new Rect(tl.X, tl.Y, prop.Size.X * scale, prop.Size.Y * scale));
                    }

            foreach (var npc in s.World.Npcs)
                ctx.DrawEllipse(Brush("#f0d27a"), Pen("#000000", 1), Map(npc.Position), 3, 3);
            foreach (var enemy in s.World.Enemies.Where(e => e.IsAlive))
                ctx.DrawEllipse(Brush("#e03020"), null, Map(enemy.Position), enemy.Template.Rank == Core.Content.EnemyRank.Boss ? 4 : 2.2, enemy.Template.Rank == Core.Content.EnemyRank.Boss ? 4 : 2.2);
            foreach (var member in s.Party.Members)
                ctx.DrawEllipse(Brush(member == s.Party.Leader ? "#f0d27a" : "#5ad05a"), Pen("#000000", 1), Map(member.Position), 3.2, 3.2);
        }
    }
}
