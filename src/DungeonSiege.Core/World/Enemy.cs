using System.Numerics;
using DungeonSiege.Core.Characters;
using DungeonSiege.Core.Content;

namespace DungeonSiege.Core.World;

public enum EnemyState { Idle, Chasing, Returning }

public sealed class Enemy : Actor
{
    public Enemy(EnemyTemplate template, string spawnKey, Vector2 home)
    {
        Template = template;
        Name = template.Name;
        SpawnKey = spawnKey;
        Home = home;
        Position = home;
        Radius = template.Radius;
        Health = template.MaxHealth;
    }

    public EnemyTemplate Template { get; }

    /// <summary>Stable id used to remember this spawn is dead across region unload/reload.</summary>
    public string SpawnKey { get; }
    public Vector2 Home { get; }
    public EnemyState State { get; set; }
    public Vector2? WanderTarget { get; set; }
    public float WanderTimer { get; set; }
    public double DiedAt { get; set; }

    public override Faction Faction => Faction.Krug;
    public override float MaxHealth => Template.MaxHealth;
    public override float Armor => Template.Armor;
    public override float MoveSpeed => Template.MoveSpeed;

    internal void ResetHealth() => Health = MaxHealth;
}
