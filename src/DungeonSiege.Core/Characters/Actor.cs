using System.Numerics;

namespace DungeonSiege.Core.Characters;

public enum Faction { Party, Krug }

/// <summary>Anything that can move, fight, and be hurt.</summary>
public abstract class Actor
{
    private static int s_nextId;

    public int Id { get; } = Interlocked.Increment(ref s_nextId);
    public string Name { get; protected init; } = "";
    public abstract Faction Faction { get; }

    public Vector2 Position { get; set; }
    public Vector2 Facing { get; set; } = Vector2.UnitY;
    public float Radius { get; protected init; } = 0.55f;
    public abstract float MoveSpeed { get; }

    public abstract float MaxHealth { get; }
    public float Health { get; protected set; }
    public virtual float Armor => 0;
    public bool IsAlive => Health > 0;

    /// <summary>Seconds until the next attack is ready.</summary>
    public float AttackCooldown { get; set; }
    public Actor? Target { get; set; }
    public Actor? LastAttacker { get; private set; }
    public double LastHitTime { get; private set; } = double.NegativeInfinity;

    /// <returns>Damage actually applied.</returns>
    public float TakeDamage(float amount, Actor source, double time)
    {
        if (!IsAlive) return 0;
        var dealt = MathF.Min(Health, MathF.Max(1, amount - Armor));
        Health -= dealt;
        LastAttacker = source;
        LastHitTime = time;
        return dealt;
    }

    /// <returns>Health actually restored.</returns>
    public float Heal(float amount)
    {
        if (!IsAlive || amount <= 0) return 0;
        var healed = MathF.Min(amount, MaxHealth - Health);
        Health += healed;
        return healed;
    }

    public float DistanceTo(Actor other) => Vector2.Distance(Position, other.Position);
    public float EdgeDistanceTo(Actor other) => DistanceTo(other) - Radius - other.Radius;
}
