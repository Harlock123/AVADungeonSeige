using System.Numerics;

namespace DungeonSiege.Core.World;

public enum PropKind
{
    // Flat (drawn under actors)
    Road, CropRow, Puddle, Scorch, Bones,
    // Standing
    Tree, Pine, DeadTree, Rock, Boulder, Bush, Fence, House, BurningHouse, Barn, Well,
    Fire, Tent, Totem, Banner, Wall, Gate, Torch, Shrine,
}

/// <summary>Static decoration instantiated when a region streams in.</summary>
public readonly record struct Prop(PropKind Kind, Vector2 Position, Vector2 Size, float Variant)
{
    public bool IsFlat => Kind <= PropKind.Bones;
}
