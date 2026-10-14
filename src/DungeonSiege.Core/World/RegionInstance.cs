using System.Numerics;
using DungeonSiege.Core.Content;

namespace DungeonSiege.Core.World;

/// <summary>The live, resident form of a region: spawned enemies, NPCs and props.</summary>
public sealed class RegionInstance
{
    internal RegionInstance(RegionDefinition definition, ContentDatabase content, WorldState state)
    {
        Definition = definition;
        var rng = new Random(definition.Seed);
        var b = definition.Bounds;

        var props = definition.Props
            .Select(p => new Prop(p.Kind, new Vector2(p.X, p.Y), new Vector2(p.W, p.H), (float)rng.NextDouble()))
            .ToList();
        foreach (var scatter in definition.Scatter)
        {
            for (var i = 0; i < scatter.Count; i++)
            {
                var size = scatter.MinSize + (float)rng.NextDouble() * (scatter.MaxSize - scatter.MinSize);
                var pos = new Vector2(b.X + 1 + (float)rng.NextDouble() * (b.Width - 2), b.Y + 1 + (float)rng.NextDouble() * (b.Height - 2));
                props.Add(new Prop(scatter.Kind, pos, new Vector2(size, size), (float)rng.NextDouble()));
            }
        }
        Props = props;

        foreach (var spawn in definition.Spawns)
        {
            var template = content.Enemies[spawn.Enemy];
            for (var i = 0; i < spawn.Count; i++)
            {
                var key = $"{definition.Id}/{spawn.Id}#{i}";
                if (state.ClearedSpawns.Contains(key)) continue;
                var angle = rng.NextDouble() * Math.Tau;
                var dist = spawn.Count == 1 ? 0 : spawn.Spread * (0.4 + 0.6 * rng.NextDouble());
                var home = new Vector2(spawn.X + (float)(Math.Cos(angle) * dist), spawn.Y + (float)(Math.Sin(angle) * dist));
                Enemies.Add(new Enemy(template, key, home));
            }
        }

        Npcs = definition.Npcs
            .Where(n => !state.DepartedNpcs.Contains(n.Id))
            .Select(n => new Npc(n))
            .ToList();
    }

    public RegionDefinition Definition { get; }
    public string Id => Definition.Id;
    public IReadOnlyList<Prop> Props { get; }
    public List<Enemy> Enemies { get; } = [];
    public List<Npc> Npcs { get; }
}
