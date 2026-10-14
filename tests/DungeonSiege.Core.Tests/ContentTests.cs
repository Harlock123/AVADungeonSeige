using DungeonSiege.Core.Content;

namespace DungeonSiege.Core.Tests;

public class ContentTests
{
    [Fact]
    public void EmbeddedChapterOneLoadsAndValidates()
    {
        var content = ContentDatabase.LoadEmbedded();

        Assert.Equal("farmer", content.Chapter.Protagonist);
        Assert.Contains("krug_scout", content.Enemies.Keys);
        Assert.Contains("krug_grunt", content.Enemies.Keys);
        Assert.Contains("braak", content.Enemies.Keys);
        Assert.Contains(content.Regions, r => r.Id == "farmstead");
        Assert.Contains(content.Regions, r => r.Id == "krug_canyons");
        Assert.Contains(content.Regions, r => r.Id == "glacern_gate");
        Assert.True(content.Quests.ContainsKey("travel_to_glacern"));
    }

    [Fact]
    public void RegionsFormOneContinuousWalkableWorld()
    {
        var content = ContentDatabase.LoadEmbedded();
        var ordered = content.Regions.OrderBy(r => r.Bounds.X).ToList();
        for (var i = 1; i < ordered.Count; i++)
        {
            var a = ordered[i - 1].Bounds;
            var b = ordered[i].Bounds;
            Assert.Equal(a.Right, b.X);
            Assert.True(a.Y < b.Bottom && b.Y < a.Bottom, $"{ordered[i - 1].Id} and {ordered[i].Id} must share an edge");
        }
    }
}
