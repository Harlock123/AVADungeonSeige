using System.Numerics;
using DungeonSiege.Core.Content;

namespace DungeonSiege.Core.World;

/// <summary>
/// Dungeon Siege's signature "no loading screens": the world is one continuous space and
/// regions stream in/out around the party. A region becomes resident well before it can
/// be seen (<see cref="LoadRadius"/>) and is evicted with hysteresis (<see cref="UnloadRadius"/>)
/// so walking back and forth across a seam never thrashes.
/// </summary>
public sealed class WorldStreamer(ContentDatabase content, WorldState state)
{
    public float LoadRadius { get; init; } = 45f;
    public float UnloadRadius { get; init; } = 70f;

    private readonly Dictionary<string, RegionInstance> _resident = [];

    public IReadOnlyCollection<RegionInstance> Resident => _resident.Values;
    public IEnumerable<Enemy> Enemies => _resident.Values.SelectMany(r => r.Enemies);
    public IEnumerable<Npc> Npcs => _resident.Values.SelectMany(r => r.Npcs);
    public int TotalRegions => content.Regions.Count;

    public event Action<RegionInstance>? RegionLoaded;
    public event Action<RegionInstance>? RegionUnloaded;

    public void Update(Vector2 focus)
    {
        foreach (var region in content.Regions)
        {
            var distance = region.Bounds.DistanceTo(focus);
            var resident = _resident.TryGetValue(region.Id, out var instance);

            if (!resident && distance <= LoadRadius)
            {
                instance = new RegionInstance(region, content, state);
                _resident[region.Id] = instance;
                RegionLoaded?.Invoke(instance);
            }
            else if (resident && distance > UnloadRadius)
            {
                _resident.Remove(region.Id);
                RegionUnloaded?.Invoke(instance!);
            }
        }
    }

    public RegionDefinition? RegionAt(Vector2 p) =>
        content.Regions.FirstOrDefault(r => r.Bounds.Contains(p));

    public bool IsWalkable(Vector2 p) => RegionAt(p) is not null;

    public void RemoveNpc(Npc npc)
    {
        foreach (var r in _resident.Values) r.Npcs.Remove(npc);
    }
}
