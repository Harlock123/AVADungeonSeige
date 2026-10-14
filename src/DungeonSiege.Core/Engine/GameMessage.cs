using System.Numerics;

namespace DungeonSiege.Core.Engine;

public enum MessageKind { Story, Combat, Skill, Loot, Quest, System }

public readonly record struct GameMessage(MessageKind Kind, string Text, double Time);

public enum EffectKind { Slash, Arrow, Spear, Zap, Heal, Claw, LevelUp }

/// <summary>Short-lived visual event the renderer can draw (strike line, projectile, spell).</summary>
public sealed record CombatEffect(EffectKind Kind, Vector2 From, Vector2 To, double CreatedAt, float Duration)
{
    public float Age(double now) => (float)((now - CreatedAt) / Duration);
}

public enum FloatingTextKind { Damage, PartyDamage, Heal, LevelUp, Gold }

public sealed record FloatingText(string Text, Vector2 Position, FloatingTextKind Kind, double CreatedAt, float Duration = 1.1f)
{
    public float Age(double now) => (float)((now - CreatedAt) / Duration);
}
