using System.Globalization;
using System.Numerics;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using DungeonSiege.Core.Characters;
using DungeonSiege.Core.Engine;
using DungeonSiege.Core.Parties;
using DungeonSiege.Core.World;
using static DungeonSiege.App.Controls.BrushCache;

namespace DungeonSiege.App.Controls;

/// <summary>
/// Immediate-mode renderer for the streamed world. It only ever draws the regions the
/// engine currently has resident, so what you see *is* the streaming bubble.
/// Pure view: reads <see cref="GameSession"/> state, and reports clicks through
/// <see cref="InteractCommand"/> as world coordinates.
/// </summary>
public sealed class WorldView : Control
{
    public static readonly StyledProperty<GameSession?> SessionProperty =
        AvaloniaProperty.Register<WorldView, GameSession?>(nameof(Session));

    public static readonly StyledProperty<ICommand?> InteractCommandProperty =
        AvaloniaProperty.Register<WorldView, ICommand?>(nameof(InteractCommand));

    /// <summary>Pixels per world unit (mouse wheel zooms).</summary>
    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<WorldView, double>(nameof(Zoom), 24);

    private static readonly Typeface LabelFace = new("Palatino Linotype, Georgia, Noto Serif, serif", FontStyle.Normal, FontWeight.SemiBold);
    private static readonly Typeface NumberFace = new("Georgia, Noto Serif, serif", FontStyle.Normal, FontWeight.Bold);

    private readonly Dictionary<string, (Vector2 Pos, float Radius, bool Light)[]> _groundPatches = [];
    private readonly IBrush _vignette = new RadialGradientBrush
    {
        GradientStops =
        {
            new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.55),
            new GradientStop(Color.FromArgb(170, 0, 0, 0), 1.0),
        },
    };

    private Vector2 _camera;
    private bool _cameraInitialised;
    private double _cameraTime;
    private Vector2? _lastClick;
    private double _lastClickTime;
    private Point? _pointer;
    private StandardCursorType _cursorType = StandardCursorType.Arrow;

    static WorldView()
    {
        AffectsRender<WorldView>(SessionProperty, ZoomProperty);
        ClipToBoundsProperty.OverrideDefaultValue<WorldView>(true);
        FocusableProperty.OverrideDefaultValue<WorldView>(true);
    }

    public GameSession? Session
    {
        get => GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    public ICommand? InteractCommand
    {
        get => GetValue(InteractCommandProperty);
        set => SetValue(InteractCommandProperty, value);
    }

    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    // ----- Coordinates ------------------------------------------------------------

    private float Z => (float)Zoom;

    private Point ToScreen(Vector2 w) => new(
        (w.X - _camera.X) * Z + Bounds.Width / 2,
        (w.Y - _camera.Y) * Z + Bounds.Height / 2);

    private Vector2 ToWorld(Point p) => new(
        (float)((p.X - Bounds.Width / 2) / Z) + _camera.X,
        (float)((p.Y - Bounds.Height / 2) / Z) + _camera.Y);

    private Rect ToScreen(Vector2 center, Vector2 size)
    {
        var tl = ToScreen(center - size / 2);
        return new Rect(tl.X, tl.Y, size.X * Z, size.Y * Z);
    }

    // ----- Input ----------------------------------------------------------------------

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var world = ToWorld(e.GetPosition(this));
        if (InteractCommand?.CanExecute(world) == true)
        {
            InteractCommand.Execute(world);
            _lastClick = world;
            _lastClickTime = Session?.Time ?? 0;
        }
        Focus();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _pointer = e.GetPosition(this);
        var hit = Session?.HitTest(ToWorld(_pointer.Value));
        var cursor = hit switch
        {
            Enemy => StandardCursorType.Cross,
            Npc => StandardCursorType.Hand,
            _ => StandardCursorType.Arrow,
        };
        if (cursor == _cursorType) return;
        _cursorType = cursor;
        Cursor = new Cursor(cursor);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pointer = null;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Zoom = Math.Clamp(Zoom * (e.Delta.Y > 0 ? 1.1 : 1 / 1.1), 8, 36);
        e.Handled = true;
    }

    // ----- Rendering --------------------------------------------------------------------

    public override void Render(DrawingContext ctx)
    {
        var bounds = new Rect(Bounds.Size);
        ctx.DrawRectangle(Brush("#0b0907"), null, bounds);

        var s = Session;
        if (s is null) return;

        // Camera eases toward the leader by *simulated* time, so it is frame-rate independent,
        // holds still while paused, and lands exactly on target after a long jump.
        var target = s.Party.Leader.Position;
        if (!_cameraInitialised || Vector2.Distance(_camera, target) > 40)
        {
            _camera = target;
            _cameraInitialised = true;
        }
        var follow = 1 - Math.Exp(-(s.Time - _cameraTime) * 8);
        _camera = Vector2.Lerp(_camera, target, (float)Math.Clamp(follow, 0, 1));
        _cameraTime = s.Time;

        var time = s.Time;
        DrawGround(ctx, s);
        foreach (var region in s.World.Resident)
            foreach (var prop in region.Props.Where(p => p.IsFlat))
                DrawFlatProp(ctx, prop, time);

        DrawOrders(ctx, s);

        // Standing props and actors are depth-sorted on Y for a faux-3D overlap.
        var drawables = new List<(float Y, Action Draw)>();
        foreach (var region in s.World.Resident)
            foreach (var prop in region.Props.Where(p => !p.IsFlat))
                drawables.Add((prop.Position.Y + prop.Size.Y * 0.3f, () => DrawProp(ctx, prop, time)));
        foreach (var enemy in s.World.Enemies)
            drawables.Add((enemy.IsAlive ? enemy.Position.Y : float.MinValue, () => DrawEnemy(ctx, enemy, s)));
        foreach (var npc in s.World.Npcs)
            drawables.Add((npc.Position.Y, () => DrawNpc(ctx, npc, s)));
        foreach (var member in s.Party.Members)
            drawables.Add((member.Position.Y, () => DrawMember(ctx, member, s)));
        foreach (var (_, draw) in drawables.OrderBy(d => d.Y)) draw();

        foreach (var effect in s.Effects) DrawEffect(ctx, effect, time);
        foreach (var text in s.FloatingTexts) DrawFloatingText(ctx, text, time);
        DrawHoverLabel(ctx, s);

        ctx.DrawRectangle(_vignette, null, bounds);
    }

    private void DrawGround(DrawingContext ctx, GameSession s)
    {
        // Cliff/rock rim first; neighbouring ground then covers shared seams.
        foreach (var region in s.World.Resident)
        {
            var b = region.Definition.Bounds;
            var r = ToScreen(new Vector2(b.X + b.Width / 2, b.Y + b.Height / 2), new Vector2(b.Width + 3, b.Height + 3));
            ctx.DrawRectangle(Brush("#2a221a"), null, r, 6 * Z / 16, 6 * Z / 16);
        }
        foreach (var region in s.World.Resident)
        {
            var def = region.Definition;
            var b = def.Bounds;
            ctx.DrawRectangle(Brush(def.GroundColor), null, ToScreen(b.Center, new Vector2(b.Width, b.Height)));

            if (!_groundPatches.TryGetValue(def.Id, out var patches))
            {
                var rng = new Random(def.Seed * 31);
                patches = Enumerable.Range(0, (int)(b.Width * b.Height / 10)).Select(_ => (
                    new Vector2(b.X + (float)rng.NextDouble() * b.Width, b.Y + (float)rng.NextDouble() * b.Height),
                    0.6f + (float)rng.NextDouble() * 2.2f,
                    rng.NextDouble() > 0.5)).ToArray();
                _groundPatches[def.Id] = patches;
            }
            foreach (var (pos, radius, light) in patches)
            {
                if (!b.Contains(pos)) continue;
                var c = ToScreen(pos);
                if (!IsOnScreen(c, radius * Z)) continue;
                ctx.DrawEllipse(light ? Brush(def.AccentColor, 90) : Brush("#000000", 28), null, c, radius * Z, radius * Z * 0.7);
            }
        }
    }

    private bool IsOnScreen(Point p, double margin) =>
        p.X > -margin && p.Y > -margin && p.X < Bounds.Width + margin && p.Y < Bounds.Height + margin;

    private void DrawFlatProp(DrawingContext ctx, Prop prop, double time)
    {
        var rect = ToScreen(prop.Position, prop.Size);
        if (!rect.Intersects(new Rect(Bounds.Size).Inflate(40))) return;
        switch (prop.Kind)
        {
            case PropKind.Road:
                ctx.DrawRectangle(Brush("#6e5c40", 200), null, rect, Z * 0.8, Z * 0.8);
                ctx.DrawRectangle(Brush("#8a7652", 70), null, rect.Deflate(new Thickness(0, rect.Height * 0.3)));
                break;
            case PropKind.CropRow:
                ctx.DrawRectangle(Brush("#3f2e1a"), null, rect);
                for (var x = rect.Left + Z * 0.4; x < rect.Right; x += Z * 0.9)
                    ctx.DrawEllipse(Brush("#9a9a3a"), null, new Point(x, rect.Center.Y), Z * 0.32, Z * 0.32);
                break;
            case PropKind.Puddle:
                ctx.DrawEllipse(Brush("#2c3c4a", 200), Pen("#6a8090", 1, 120), rect.Center, rect.Width / 2, rect.Height / 2);
                break;
            case PropKind.Scorch:
                ctx.DrawEllipse(Brush("#0a0806", 120), null, rect.Center, rect.Width / 2, rect.Height / 2);
                break;
            case PropKind.Bones:
                var c = rect.Center;
                ctx.DrawLine(Pen("#d8d0b8", Z * 0.15), c + new Point(-Z * 0.6, -Z * 0.2), c + new Point(Z * 0.6, Z * 0.2));
                ctx.DrawLine(Pen("#d8d0b8", Z * 0.12), c + new Point(-Z * 0.3, Z * 0.4), c + new Point(Z * 0.4, -Z * 0.3));
                ctx.DrawEllipse(Brush("#e8e0c8"), null, c + new Point(Z * 0.7, -Z * 0.1), Z * 0.25, Z * 0.22);
                break;
        }
    }

    private void DrawProp(DrawingContext ctx, Prop prop, double time)
    {
        var c = ToScreen(prop.Position);
        var sx = prop.Size.X * Z;
        var sy = prop.Size.Y * Z;
        if (!IsOnScreen(c, Math.Max(sx, sy) * 1.5)) return;
        var shadow = Brush("#000000", 70);
        var flicker = (float)(0.75 + 0.25 * Math.Sin(time * 11 + prop.Variant * 20) * Math.Sin(time * 7.3 + prop.Variant * 5));

        switch (prop.Kind)
        {
            case PropKind.Tree:
                ctx.DrawEllipse(shadow, null, c + new Point(sx * 0.35, sx * 0.3), sx * 0.95, sx * 0.7);
                ctx.DrawEllipse(Brush("#4a3220"), null, c, sx * 0.2, sx * 0.2);
                var greens = prop.Variant > 0.5 ? ("#25401a", "#335a22", "#4a7230") : ("#2c3f16", "#3f5a1e", "#5a7428");
                ctx.DrawEllipse(Brush(greens.Item1), null, c, sx * 0.9, sx * 0.85);
                ctx.DrawEllipse(Brush(greens.Item2), null, c + new Point(-sx * 0.15, -sx * 0.15), sx * 0.65, sx * 0.6);
                ctx.DrawEllipse(Brush(greens.Item3, 200), null, c + new Point(-sx * 0.28, -sx * 0.3), sx * 0.3, sx * 0.28);
                break;
            case PropKind.Pine:
                ctx.DrawEllipse(shadow, null, c + new Point(sx * 0.35, sx * 0.3), sx * 0.8, sx * 0.6);
                DrawStar(ctx, c, sx * 0.85, sx * 0.45, 8, Brush("#1c3328"));
                DrawStar(ctx, c, sx * 0.6, sx * 0.3, 8, Brush("#2a4a38"));
                ctx.DrawEllipse(Brush("#e8f0f0", 150), null, c + new Point(-sx * 0.15, -sx * 0.2), sx * 0.18, sx * 0.14);
                break;
            case PropKind.DeadTree:
                ctx.DrawEllipse(shadow, null, c + new Point(sx * 0.2, sx * 0.2), sx * 0.5, sx * 0.35);
                for (var i = 0; i < 5; i++)
                {
                    var a = prop.Variant * 6 + i * 1.3;
                    ctx.DrawLine(Pen("#3a2c20", Math.Max(1, sx * 0.1)), c, c + new Point(Math.Cos(a) * sx * 0.8, Math.Sin(a) * sx * 0.8));
                }
                ctx.DrawEllipse(Brush("#3a2c20"), null, c, sx * 0.18, sx * 0.18);
                break;
            case PropKind.Rock:
            case PropKind.Boulder:
                var big = prop.Kind == PropKind.Boulder;
                ctx.DrawEllipse(shadow, null, c + new Point(sx * 0.25, sx * 0.2), sx * 0.6, sx * 0.45);
                ctx.DrawEllipse(Brush(big ? "#4a4038" : "#5e574e"), Pen("#211c18", 1), c, sx * 0.55, sx * 0.42);
                ctx.DrawEllipse(Brush(big ? "#6a5e50" : "#7e766a", 180), null, c + new Point(-sx * 0.15, -sx * 0.12), sx * 0.28, sx * 0.18);
                break;
            case PropKind.Bush:
                ctx.DrawEllipse(shadow, null, c + new Point(sx * 0.2, sx * 0.2), sx * 0.6, sx * 0.45);
                ctx.DrawEllipse(Brush("#2e4a1c"), null, c, sx * 0.55, sx * 0.45);
                ctx.DrawEllipse(Brush("#466a28", 200), null, c + new Point(-sx * 0.15, -sx * 0.12), sx * 0.28, sx * 0.22);
                break;
            case PropKind.Fence:
            {
                var r = ToScreen(prop.Position, prop.Size);
                ctx.DrawRectangle(Brush("#5a4028"), null, r);
                for (var x = r.Left; x <= r.Right; x += Z * 2)
                    ctx.DrawRectangle(Brush("#3e2a18"), null, new Rect(x - Z * 0.15, r.Top - Z * 0.15, Z * 0.3, r.Height + Z * 0.3));
                break;
            }
            case PropKind.House:
            case PropKind.BurningHouse:
            case PropKind.Barn:
                DrawBuilding(ctx, prop, time, flicker);
                break;
            case PropKind.Well:
                ctx.DrawEllipse(shadow, null, c + new Point(sx * 0.3, sx * 0.3), sx * 0.6, sx * 0.5);
                ctx.DrawEllipse(Brush("#6e665a"), Pen("#2e2a24", 2), c, sx * 0.55, sx * 0.55);
                ctx.DrawEllipse(Brush("#10161c"), null, c, sx * 0.32, sx * 0.32);
                break;
            case PropKind.Fire:
            case PropKind.Torch:
                var size = prop.Kind == PropKind.Torch ? Z * 0.5 : sx * 0.6;
                if (prop.Kind == PropKind.Torch) ctx.DrawEllipse(Brush("#2a1e14"), null, c, Z * 0.2, Z * 0.2);
                ctx.DrawEllipse(Brush("#ff8a20", 40), null, c, size * 3.2 * flicker, size * 3.2 * flicker);
                ctx.DrawEllipse(Brush("#e2541a", 200), null, c, size * flicker, size * flicker);
                ctx.DrawEllipse(Brush("#ffd060", 230), null, c + new Point(0, -size * 0.2), size * 0.5 * flicker, size * 0.55 * flicker);
                break;
            case PropKind.Tent:
            {
                var r = ToScreen(prop.Position, prop.Size);
                ctx.DrawEllipse(shadow, null, r.Center + new Point(Z * 0.6, Z * 0.5), r.Width * 0.55, r.Height * 0.55);
                DrawPolygon(ctx, Brush("#7a5a36"), Pen("#2a1c10", 1.5), r.TopLeft.WithY(r.Center.Y), r.Center.WithY(r.Top), r.TopRight.WithY(r.Center.Y), r.Center.WithY(r.Bottom));
                ctx.DrawLine(Pen("#3a2a18", 2), r.Center.WithY(r.Top), r.Center.WithY(r.Bottom));
                break;
            }
            case PropKind.Totem:
                ctx.DrawEllipse(shadow, null, c + new Point(Z * 0.3, Z * 0.3), Z * 0.5, Z * 0.35);
                ctx.DrawEllipse(Brush("#3a2a1c"), null, c, Z * 0.35, Z * 0.35);
                ctx.DrawEllipse(Brush("#e0d8c0"), Pen("#000000", 1), c + new Point(0, -Z * 0.1), Z * 0.28, Z * 0.24);
                ctx.DrawLine(Pen("#a02018", Z * 0.12), c + new Point(Z * 0.2, 0), c + new Point(Z * 0.7, Z * 0.4));
                break;
            case PropKind.Banner:
                ctx.DrawLine(Pen("#2a1e14", Z * 0.15), c, c + new Point(0, -Z * 2.5));
                DrawPolygon(ctx, Brush(prop.Variant > 0.5 ? "#8a1a14" : "#1a2a6a"), null,
                    c + new Point(0, -Z * 2.5), c + new Point(Z * 1.1, -Z * 2.3), c + new Point(Z * 0.9, -Z * 1.6), c + new Point(0, -Z * 1.5));
                break;
            case PropKind.Wall:
            case PropKind.Gate:
            {
                var r = ToScreen(prop.Position, prop.Size);
                ctx.DrawRectangle(Brush("#000000", 90), null, r.Translate(new Avalonia.Vector(Z * 0.6, Z * 0.6)));
                if (prop.Kind == PropKind.Gate)
                {
                    ctx.DrawRectangle(Brush("#3a2818"), Pen("#14100c", 2), r);
                    for (var y = r.Top + r.Height / 4; y < r.Bottom; y += r.Height / 4)
                        ctx.DrawLine(Pen("#5a5a5a", Z * 0.15), new Point(r.Left, y), new Point(r.Right, y));
                }
                else
                {
                    ctx.DrawRectangle(Brush("#6a6862"), Pen("#2a2826", 2), r);
                    for (var y = r.Top; y < r.Bottom; y += Z * 1.5)
                        ctx.DrawRectangle(Brush("#86847c"), Pen("#2a2826", 1), new Rect(r.Left - Z * 0.3, y, Z * 0.8, Z * 0.8));
                }
                break;
            }
            case PropKind.Shrine:
                var pulse = 0.6 + 0.4 * Math.Sin(time * 2);
                ctx.DrawEllipse(Brush("#5aa0ff", (byte)(50 * pulse)), null, c, sx * 1.4, sx * 1.4);
                ctx.DrawEllipse(Brush("#8a8a84"), Pen("#2a2a28", 2), c, sx * 0.5, sx * 0.5);
                ctx.DrawEllipse(Brush("#9ad0ff", (byte)(200 * pulse)), null, c, sx * 0.22, sx * 0.22);
                break;
        }
    }

    private void DrawBuilding(DrawingContext ctx, Prop prop, double time, float flicker)
    {
        var r = ToScreen(prop.Position, prop.Size);
        var burning = prop.Kind == PropKind.BurningHouse;
        var (wall, roof, ridge) = prop.Kind switch
        {
            PropKind.Barn => ("#5a2a1a", "#7a3422", "#4a1a10"),
            PropKind.BurningHouse => ("#3a2a1c", "#241810", "#120c08"),
            _ => ("#6b4a2b", "#6a5a3a", "#3e3020"),
        };
        ctx.DrawRectangle(Brush("#000000", 100), null, r.Translate(new Avalonia.Vector(Z * 0.8, Z * 0.8)));
        ctx.DrawRectangle(Brush(wall), Pen("#140e08", 2), r);
        // Pitched roof seen from above: two slopes either side of a ridge.
        var inset = r.Deflate(Z * 0.25);
        ctx.DrawRectangle(Brush(roof), null, inset.WithHeight(inset.Height / 2));
        ctx.DrawRectangle(Brush(roof, 200), null, new Rect(inset.Left, inset.Center.Y, inset.Width, inset.Height / 2));
        ctx.DrawLine(Pen(ridge, Z * 0.3), new Point(inset.Left, inset.Center.Y), new Point(inset.Right, inset.Center.Y));
        for (var x = inset.Left + Z; x < inset.Right; x += Z)
            ctx.DrawLine(Pen(ridge, 1, 120), new Point(x, inset.Top), new Point(x, inset.Bottom));

        if (!burning) return;
        for (var i = 0; i < 4; i++)
        {
            var p = new Point(r.Left + r.Width * (0.2 + 0.2 * i), r.Top + r.Height * (0.3 + 0.15 * (i % 2)));
            var f = flicker * (0.8 + 0.2 * Math.Sin(time * 9 + i));
            ctx.DrawEllipse(Brush("#ff7020", 50), null, p, Z * 2.6 * f, Z * 2.6 * f);
            ctx.DrawEllipse(Brush("#e2541a", 210), null, p, Z * 0.9 * f, Z * 0.9 * f);
            ctx.DrawEllipse(Brush("#ffd060", 220), null, p, Z * 0.45 * f, Z * 0.45 * f);
        }
        // Smoke drifting east.
        for (var i = 0; i < 5; i++)
        {
            var t = (time * 0.25 + i * 0.2) % 1.0;
            var p = new Point(r.Center.X + t * Z * 9, r.Top - t * Z * 4);
            ctx.DrawEllipse(Brush("#2a2622", (byte)(110 * (1 - t))), null, p, Z * (1 + t * 2.5), Z * (0.8 + t * 2));
        }
    }

    private void DrawOrders(DrawingContext ctx, GameSession s)
    {
        var leader = s.Party.Leader;
        if (leader.Order is MoveOrder move)
        {
            var p = ToScreen(move.Destination);
            var k = Z * 0.4;
            ctx.DrawLine(Pen("#f0d27a", 2, 200), p + new Point(-k, -k), p + new Point(k, k));
            ctx.DrawLine(Pen("#f0d27a", 2, 200), p + new Point(-k, k), p + new Point(k, -k));
        }
        if (_lastClick is { } click && s.Time - _lastClickTime < 0.5)
        {
            var age = (float)((s.Time - _lastClickTime) / 0.5);
            ctx.DrawEllipse(null, Pen("#f0d27a", 2, (byte)(255 * (1 - age))), ToScreen(click), Z * (0.3 + age), Z * (0.2 + age * 0.7));
        }
        if (leader.Target is { IsAlive: true } target)
        {
            var p = ToScreen(target.Position);
            ctx.DrawEllipse(null, Pen("#e03030", 2, 220), p + new Point(0, target.Radius * Z * 0.5), target.Radius * Z * 1.4, target.Radius * Z * 0.8);
        }
    }

    private void DrawMember(DrawingContext ctx, Character m, GameSession s)
    {
        var p = ToScreen(m.Position);
        var r = m.Radius * Z;
        if (!m.IsAlive)
        {
            ctx.DrawEllipse(Brush("#5a0000", 140), null, p, r * 1.5, r * 0.8);
            ctx.DrawEllipse(Brush("#5a5550"), Pen("#000000", 1), p, r * 1.2, r * 0.55);
            return;
        }

        ctx.DrawEllipse(Brush("#000000", 90), null, p + new Point(r * 0.3, r * 0.5), r * 1.1, r * 0.7);
        if (m == s.Party.Leader)
        {
            var pulse = 0.7 + 0.3 * Math.Sin(s.Time * 4);
            ctx.DrawEllipse(null, Pen("#f0d27a", 2, (byte)(255 * pulse)), p + new Point(0, r * 0.5), r * 1.7, r * 1.0);
        }
        else
        {
            ctx.DrawEllipse(null, Pen("#4fae4a", 1.5, 200), p + new Point(0, r * 0.5), r * 1.5, r * 0.85);
        }

        // Weapon cue in the facing direction.
        var f = m.Facing == Vector2.Zero ? Vector2.UnitY : Vector2.Normalize(m.Facing);
        var tip = p + new Point(f.X * r * 1.9, f.Y * r * 1.9);
        var weaponColor = m.ActiveSkill switch
        {
            SkillType.Ranged => "#8a6a3a",
            SkillType.CombatMagic => "#7aa0ff",
            SkillType.NatureMagic => "#7ad07a",
            _ => "#c8c8c8",
        };
        ctx.DrawLine(Pen(weaponColor, Math.Max(2, Z * 0.18)), p, tip);

        ctx.DrawEllipse(Brush(m.PortraitColor), Pen("#120c06", 1.5), p, r, r);
        ctx.DrawEllipse(Brush("#d8b890"), null, p + new Point(f.X * r * 0.3, f.Y * r * 0.3 - r * 0.1), r * 0.45, r * 0.45);
    }

    private void DrawEnemy(DrawingContext ctx, Enemy e, GameSession s)
    {
        var p = ToScreen(e.Position);
        var r = e.Radius * Z;
        if (!IsOnScreen(p, r * 3)) return;

        if (!e.IsAlive)
        {
            var fade = (byte)Math.Clamp(255 * (1 - (s.Time - e.DiedAt) / 30), 40, 255);
            ctx.DrawEllipse(Brush("#3a0a06", (byte)(fade / 2)), null, p, r * 1.4, r * 0.8);
            ctx.DrawEllipse(Brush(e.Template.Color, fade), null, p, r * 1.1, r * 0.5);
            return;
        }

        ctx.DrawEllipse(Brush("#000000", 100), null, p + new Point(r * 0.3, r * 0.5), r * 1.1, r * 0.7);
        var f = e.Facing == Vector2.Zero ? Vector2.UnitY : Vector2.Normalize(e.Facing);
        var hitFlash = s.Time - e.LastHitTime < 0.12;

        if (e.Template.Rank == Core.Content.EnemyRank.Boss)
        {
            // Braak: hulking body with pale tusks.
            ctx.DrawEllipse(Brush("#2a1a10"), null, p, r * 1.08, r * 1.08);
            var side = new Point(-f.Y, f.X);
            foreach (var sign in new[] { -1, 1 })
            {
                var root = p + new Point(f.X * r * 0.7 + side.X * sign * r * 0.4, f.Y * r * 0.7 + side.Y * sign * r * 0.4);
                ctx.DrawLine(Pen("#e8dcc0", Z * 0.22), root, root + new Point(f.X * r * 0.7, f.Y * r * 0.7));
            }
        }
        ctx.DrawEllipse(hitFlash ? Brush("#ffffff") : Brush(e.Template.Color), Pen("#150c04", 1.5), p, r, r);
        // Krug ears / brow toward facing.
        ctx.DrawEllipse(Brush("#c83a20"), null, p + new Point(f.X * r * 0.45 - f.Y * r * 0.25, f.Y * r * 0.45 + f.X * r * 0.25), r * 0.13, r * 0.13);
        ctx.DrawEllipse(Brush("#c83a20"), null, p + new Point(f.X * r * 0.45 + f.Y * r * 0.25, f.Y * r * 0.45 - f.X * r * 0.25), r * 0.13, r * 0.13);

        var damaged = e.Health < e.MaxHealth || e.Template.Rank == Core.Content.EnemyRank.Boss;
        if (damaged || e.State == EnemyState.Chasing)
        {
            var w = Math.Max(Z * 1.4, r * 2.2);
            var bar = new Rect(p.X - w / 2, p.Y - r - Z * 0.55, w, Math.Max(3, Z * 0.2));
            ctx.DrawRectangle(Brush("#000000", 200), null, bar.Inflate(1));
            ctx.DrawRectangle(Brush("#c0141e"), null, bar.WithWidth(bar.Width * e.Health / e.MaxHealth));
        }
    }

    private void DrawNpc(DrawingContext ctx, Npc npc, GameSession s)
    {
        var p = ToScreen(npc.Position);
        var r = npc.Radius * Z;
        if (npc.Definition.Wounded)
        {
            ctx.DrawEllipse(Brush("#6a0a08", 170), null, p + new Point(r * 0.4, r * 0.3), r * 1.8, r * 1.0);
            ctx.DrawEllipse(Brush(npc.Definition.Color), Pen("#120c06", 1.5), p, r * 1.3, r * 0.65);
            ctx.DrawEllipse(Brush("#d8b890"), null, p + new Point(-r * 1.0, 0), r * 0.42, r * 0.42);
        }
        else
        {
            ctx.DrawEllipse(Brush("#000000", 90), null, p + new Point(r * 0.3, r * 0.5), r * 1.1, r * 0.7);
            ctx.DrawEllipse(Brush(npc.Definition.Color), Pen("#120c06", 1.5), p, r, r);
            ctx.DrawEllipse(Brush("#d8b890"), null, p + new Point(0, r * 0.15), r * 0.45, r * 0.45);
        }

        if (!s.State.Flags.Contains($"talked:{npc.Id}"))
        {
            var bob = Math.Sin(s.Time * 3) * Z * 0.15;
            DrawText(ctx, "!", p + new Point(0, -r - Z * 1.1 + bob), NumberFace, Math.Max(14, Z * 1.1), Brush("#f0d27a"), outline: true);
        }
        DrawText(ctx, npc.Name, p + new Point(0, r + Z * 0.7), LabelFace, Math.Max(10, Z * 0.7), Brush("#e3d5b0", 230), outline: true);
    }

    private void DrawEffect(DrawingContext ctx, CombatEffect fx, double time)
    {
        var age = fx.Age(time);
        var from = ToScreen(fx.From);
        var to = ToScreen(fx.To);
        var alpha = 1 - age;
        switch (fx.Kind)
        {
            case EffectKind.Slash:
            case EffectKind.Claw:
            {
                var color = fx.Kind == EffectKind.Slash ? "#ffffff" : "#ff4a30";
                var dir = to - from;
                var len = Math.Max(1, Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y));
                var n = new Point(-dir.Y / len, dir.X / len);
                for (var i = -1; i <= 1; i++)
                {
                    var o = to + n * (i * Z * 0.25);
                    ctx.DrawLine(Pen(color, 2, (byte)(230 * alpha)), o + new Point(-n.Y * Z * 0.6, n.X * Z * 0.6) + n * Z * 0.5,
                        o - new Point(-n.Y * Z * 0.6, n.X * Z * 0.6) - n * Z * 0.5);
                }
                break;
            }
            case EffectKind.Arrow:
            case EffectKind.Spear:
            {
                var pos = from + (to - from) * Math.Min(1, age * 1.6);
                var dir = to - from;
                var len = Math.Max(1, Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y));
                var tail = pos - dir / len * Z * (fx.Kind == EffectKind.Spear ? 1.4 : 0.9);
                if (age < 0.62)
                    ctx.DrawLine(Pen(fx.Kind == EffectKind.Arrow ? "#e8d8b0" : "#8a6a3a", fx.Kind == EffectKind.Spear ? 3 : 2), tail, pos);
                break;
            }
            case EffectKind.Zap:
            {
                var rng = new Random((int)(fx.CreatedAt * 1000));
                var points = new List<Point> { from };
                for (var i = 1; i < 7; i++)
                {
                    var t = i / 7.0;
                    points.Add(from + (to - from) * t + new Point((rng.NextDouble() - 0.5) * Z, (rng.NextDouble() - 0.5) * Z));
                }
                points.Add(to);
                for (var i = 1; i < points.Count; i++)
                {
                    ctx.DrawLine(Pen("#4a7cf0", 6, (byte)(90 * alpha)), points[i - 1], points[i]);
                    ctx.DrawLine(Pen("#e0ecff", 2, (byte)(255 * alpha)), points[i - 1], points[i]);
                }
                ctx.DrawEllipse(Brush("#9ab8ff", (byte)(120 * alpha)), null, to, Z * 0.8, Z * 0.8);
                break;
            }
            case EffectKind.Heal:
                ctx.DrawLine(Pen("#7ad07a", 3, (byte)(120 * alpha)), from, to);
                for (var i = 0; i < 6; i++)
                {
                    var a = i * Math.PI / 3 + age * 2;
                    var pt = to + new Point(Math.Cos(a) * Z * 0.8, Math.Sin(a) * Z * 0.5 - age * Z * 1.5);
                    ctx.DrawEllipse(Brush("#a8ff9a", (byte)(220 * alpha)), null, pt, Z * 0.15, Z * 0.15);
                }
                break;
            case EffectKind.LevelUp:
                ctx.DrawEllipse(null, Pen("#f0d27a", 3, (byte)(255 * alpha)), from, Z * (0.5 + age * 2.5), Z * (0.3 + age * 1.6));
                break;
        }
    }

    private void DrawFloatingText(DrawingContext ctx, FloatingText text, double time)
    {
        var age = text.Age(time);
        var p = ToScreen(text.Position) + new Point(0, -Z * (0.9 + age * 1.6));
        var color = text.Kind switch
        {
            FloatingTextKind.PartyDamage => "#ff5040",
            FloatingTextKind.Heal => "#8aff7a",
            FloatingTextKind.LevelUp => "#f0d27a",
            FloatingTextKind.Gold => "#ffd84a",
            _ => "#fff2d8",
        };
        var size = text.Kind == FloatingTextKind.LevelUp ? 15 : 13;
        DrawText(ctx, text.Text, p, NumberFace, size, Brush(color, (byte)(255 * Math.Clamp(1.4 - age, 0, 1))), outline: true);
    }

    private void DrawHoverLabel(DrawingContext ctx, GameSession s)
    {
        if (_pointer is not { } pointer || s.HitTest(ToWorld(pointer)) is not Enemy enemy) return;
        var p = ToScreen(enemy.Position) + new Point(0, -enemy.Radius * Z - Z * 1.3);
        DrawText(ctx, $"{enemy.Name}  {enemy.Health:0}/{enemy.MaxHealth:0}", p, LabelFace, 13, Brush("#ffb090"), outline: true);
    }

    // ----- Primitives ------------------------------------------------------------------

    private static void DrawText(DrawingContext ctx, string text, Point center, Typeface face, double size, IBrush brush, bool outline)
    {
        var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, size, brush);
        var origin = new Point(center.X - ft.Width / 2, center.Y - ft.Height / 2);
        if (outline)
        {
            var shadow = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, size, Brush("#000000", 200));
            ctx.DrawText(shadow, origin + new Point(1, 1));
            ctx.DrawText(shadow, origin + new Point(-1, 1));
        }
        ctx.DrawText(ft, origin);
    }

    private static void DrawPolygon(DrawingContext ctx, IBrush? fill, IPen? pen, params Point[] points)
    {
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(points[0], fill is not null);
            foreach (var p in points.Skip(1)) g.LineTo(p);
            g.EndFigure(true);
        }
        ctx.DrawGeometry(fill, pen, geometry);
    }

    private static void DrawStar(DrawingContext ctx, Point c, double outer, double inner, int spikes, IBrush fill)
    {
        var pts = new Point[spikes * 2];
        for (var i = 0; i < pts.Length; i++)
        {
            var a = i * Math.PI / spikes;
            var rad = i % 2 == 0 ? outer : inner;
            pts[i] = c + new Point(Math.Cos(a) * rad, Math.Sin(a) * rad);
        }
        DrawPolygon(ctx, fill, null, pts);
    }
}
