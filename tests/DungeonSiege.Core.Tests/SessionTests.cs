using System.Numerics;
using DungeonSiege.Core.Content;
using DungeonSiege.Core.Engine;
using DungeonSiege.Core.Parties;
using DungeonSiege.Core.Quests;

namespace DungeonSiege.Core.Tests;

public class SessionTests
{
    private static GameSession NewSession()
    {
        var session = new GameSession(ContentDatabase.LoadEmbedded());
        session.Start();
        return session;
    }

    /// <summary>Plays through Old Hollin's conversation by always picking the first choice.</summary>
    private static void FinishDialogue(GameSession s)
    {
        for (var guard = 0; s.Dialogue.IsActive && guard < 20; guard++) s.ChooseDialogue(0);
    }

    [Fact]
    public void PausedSessionDoesNotAdvanceButKeepsOrders()
    {
        var s = NewSession();
        s.SetPaused(true);
        var start = s.Party.Leader.Position;

        s.Interact(start + new Vector2(0, 5));
        s.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(0, s.Time);
        Assert.Equal(start, s.Party.Leader.Position);
        Assert.IsType<MoveOrder>(s.Party.Leader.Order);

        s.SetPaused(false);
        s.Advance(TimeSpan.FromSeconds(0.2));
        Assert.True(s.Time > 0);
        Assert.NotEqual(start, s.Party.Leader.Position);
    }

    [Fact]
    public void FixedStepIsIndependentOfFrameRate()
    {
        var a = NewSession();
        var b = NewSession();
        for (var i = 0; i < 60; i++) a.Advance(TimeSpan.FromSeconds(1 / 60.0));
        for (var i = 0; i < 20; i++) b.Advance(TimeSpan.FromSeconds(1 / 20.0));
        // Both hosts simulate the same number of 30 Hz steps (± one carried in the accumulator).
        Assert.InRange(Math.Abs(a.Time - b.Time), 0, GameSession.FixedStep + 1e-6);
    }

    [Fact]
    public void ApproachingHollinStartsTravelToGlacern()
    {
        var s = NewSession();
        for (var i = 0; i < 300 && !s.Dialogue.IsActive; i++)
        {
            s.Interact(new Vector2(17, 33));
            s.Advance(TimeSpan.FromSeconds(0.1));
        }
        Assert.True(s.Dialogue.IsActive);
        Assert.Equal("Old Hollin", s.Dialogue.Node!.Speaker);

        FinishDialogue(s);

        var quest = s.Quests.Get("travel_to_glacern");
        Assert.NotNull(quest);
        Assert.Equal(QuestStatus.Active, quest.Status);
        Assert.Equal("clear_farm", quest.CurrentObjective!.Id);
        Assert.Equal(5, s.Party.Inventory.Count("potion_health"));
    }

    [Fact]
    public void WorldStreamsRegionsAroundTheLeader()
    {
        var s = NewSession();
        var resident = s.World.Resident.Select(r => r.Id).ToHashSet();
        Assert.Contains("farmstead", resident);
        Assert.DoesNotContain("glacern_gate", resident);

        // Teleport far east: west is evicted, east streams in — no loading screen involved.
        s.Party.Leader.Position = new Vector2(430, 31);
        s.World.Update(s.Party.Leader.Position);
        resident = s.World.Resident.Select(r => r.Id).ToHashSet();
        Assert.Contains("glacern_gate", resident);
        Assert.DoesNotContain("farmstead", resident);
    }

    [Fact]
    public void KilledSpawnsStayDeadAfterRegionReload()
    {
        var s = NewSession();
        var scout = s.World.Enemies.First(e => e.Template.Id == "krug_scout");
        scout.TakeDamage(1_000, s.Party.Leader, 0);
        s.State.ClearedSpawns.Add(scout.SpawnKey);
        var key = scout.SpawnKey;

        s.World.Update(new Vector2(430, 31));
        s.World.Update(new Vector2(10, 30));

        Assert.DoesNotContain(s.World.Enemies, e => e.SpawnKey == key);
    }

    [Fact]
    public void WaitBehaviourHoldsPosition()
    {
        var s = NewSession();
        FinishDialogue(s);
        var follower = s.CreateCharacter("merik");
        follower.Position = s.Party.Leader.Position + new Vector2(-3, 0);
        s.Party.TryRecruit(follower);
        s.SetBehavior(follower, CombatBehavior.Wait);
        var hold = follower.Position;

        s.Party.Leader.Order = new MoveOrder(s.Party.Leader.Position + new Vector2(0, 10));
        for (var i = 0; i < 30; i++) s.Advance(TimeSpan.FromSeconds(0.1));

        Assert.Equal(hold, follower.Position);
    }
}

public class OrderCompletionTests
{
    [Fact]
    public void TalkOrderReachesTheNpcAndOpensDialogue()
    {
        var s = new GameSession(ContentDatabase.LoadEmbedded());
        s.Start();
        var leader = s.Party.Leader;
        leader.Position = new Vector2(436, 33);
        s.World.Update(leader.Position);

        s.Interact(new Vector2(443, 31)); // the Captain of the Guard
        for (var i = 0; i < 300 && !s.Dialogue.IsActive; i++) s.Advance(TimeSpan.FromSeconds(1 / 30.0));

        Assert.True(s.Dialogue.IsActive);
        Assert.Null(leader.Order);
    }

    [Fact]
    public void MoveOrderCompletes()
    {
        var s = new GameSession(ContentDatabase.LoadEmbedded());
        s.Start();
        s.Interact(s.Party.Leader.Position + new Vector2(0, 6));
        for (var i = 0; i < 120 && s.Party.Leader.Order is not null; i++) s.Advance(TimeSpan.FromSeconds(1 / 30.0));
        Assert.Null(s.Party.Leader.Order);
    }
}
