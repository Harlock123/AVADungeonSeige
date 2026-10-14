using System.Numerics;
using DungeonSiege.Core.Content;

namespace DungeonSiege.Core.World;

/// <summary>A non-combatant the party can talk to.</summary>
public sealed class Npc(NpcDefinition definition)
{
    public NpcDefinition Definition { get; } = definition;
    public string Id => Definition.Id;
    public string Name => Definition.Name;
    public Vector2 Position { get; } = new(definition.X, definition.Y);
    public float Radius => 0.55f;
}
