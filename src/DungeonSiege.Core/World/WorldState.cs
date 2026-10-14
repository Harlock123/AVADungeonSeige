namespace DungeonSiege.Core.World;

/// <summary>Persistent world facts that survive regions streaming in and out (i.e. the save game).</summary>
public sealed class WorldState
{
    public HashSet<string> ClearedSpawns { get; } = [];
    public HashSet<string> FiredTriggers { get; } = [];
    public HashSet<string> Flags { get; } = [];
    public HashSet<string> VisitedRegions { get; } = [];

    /// <summary>NPCs that left the map (e.g. recruited into the party).</summary>
    public HashSet<string> DepartedNpcs { get; } = [];
}
