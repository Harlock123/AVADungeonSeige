using System.Numerics;
using DungeonSiege.Core.Characters;
using DungeonSiege.Core.Items;

namespace DungeonSiege.Core.Parties;

/// <summary>DS-style fight orders for companions.</summary>
public enum CombatBehavior
{
    /// <summary>Engage any Krug in sight.</summary>
    Aggressive,

    /// <summary>Only fight things threatening the party; stay near the leader.</summary>
    Defend,

    /// <summary>Hold position; only strike what comes into weapon range.</summary>
    Wait,
}

/// <summary>An explicit player command. Can be issued while paused and executes on resume.</summary>
public abstract record Order;
public sealed record MoveOrder(Vector2 Destination) : Order;
public sealed record AttackOrder(Actor Target) : Order;
public sealed record TalkOrder(World.Npc Npc) : Order;

public sealed class Party
{
    public const int MaxMembers = 8;

    private readonly List<Character> _members = [];

    public IReadOnlyList<Character> Members => _members;
    public Inventory Inventory { get; } = new();
    public int Gold { get; set; }

    /// <summary>The selected member; the camera and followers track this character.</summary>
    public Character Leader { get; private set; } = null!;

    public bool IsWiped => _members.Count > 0 && _members.TrueForAll(m => !m.IsAlive);

    public bool TryRecruit(Character character)
    {
        if (_members.Count >= MaxMembers || _members.Contains(character)) return false;
        _members.Add(character);
        Leader ??= character;
        return true;
    }

    public void Select(Character character)
    {
        if (_members.Contains(character)) Leader = character;
    }

    /// <summary>Wedge formation behind the leader, relative to the direction they face.</summary>
    public Vector2 FormationSlot(Character member)
    {
        var followers = _members.Where(m => m != Leader).ToList();
        var index = followers.IndexOf(member);
        if (index < 0) return Leader.Position;

        var facing = Leader.Facing == Vector2.Zero ? Vector2.UnitY : Vector2.Normalize(Leader.Facing);
        var right = new Vector2(-facing.Y, facing.X);
        var row = index / 2 + 1;
        var side = index % 2 == 0 ? -1 : 1;
        return Leader.Position - facing * (2.2f * row) + right * (side * 1.8f * row);
    }
}
