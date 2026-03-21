using DetMath;
using DetMap.Layers;
using DetMap.Pathfinding;

namespace DetMap.Tests.Pathfinding;

public class DetPathfinderTests
{
    private static DetBitLayer AllWalkable(int w, int h)
    {
        var layer = new DetBitLayer("walkable", w, h);
        layer.SetAll(true);
        return layer;
    }

    [Fact]
    public void FindPath_StraightLine_ReturnsValidPath()
    {
        var walkable = AllWalkable(10, 10);
        var pf = new DetPathfinder(10, 10);
        var path = pf.FindPath(0, 0, 5, 0, walkable);
        Assert.True(path.IsValid);
        Assert.Equal(6, path.Length); // 0..5 inclusive
    }

    [Fact]
    public void FindPath_StartEqualsGoal_ReturnsOneStep()
    {
        var walkable = AllWalkable(10, 10);
        var pf = new DetPathfinder(10, 10);
        var path = pf.FindPath(3, 3, 3, 3, walkable);
        Assert.True(path.IsValid);
        Assert.Equal(1, path.Length);
    }

    [Fact]
    public void FindPath_BlockedPath_ReturnsInvalid()
    {
        var walkable = AllWalkable(5, 5);
        // Wall from x=2, y=0 to y=4 (full column blocked)
        for (int y = 0; y < 5; y++) walkable.Set(2, y, false);

        var pf = new DetPathfinder(5, 5);
        var path = pf.FindPath(0, 2, 4, 2, walkable);
        Assert.False(path.IsValid);
    }

    [Fact]
    public void FindPath_Deterministic_SameResultTwice()
    {
        var walkable = AllWalkable(20, 20);
        var pf = new DetPathfinder(20, 20);

        var p1 = pf.FindPath(0, 0, 10, 10, walkable);
        var p2 = pf.FindPath(0, 0, 10, 10, walkable);

        Assert.Equal(p1.Length, p2.Length);
        for (int i = 0; i < p1.Length; i++)
            Assert.Equal(p1.Steps![i], p2.Steps![i]);
    }

    [Fact]
    public void FindPath_AroundObstacle_FindsAlternativeRoute()
    {
        var walkable = AllWalkable(10, 10);
        // Horizontal wall at y=5, x=1..8
        for (int x = 1; x <= 8; x++) walkable.Set(x, 5, false);

        var pf = new DetPathfinder(10, 10);
        var path = pf.FindPath(4, 3, 4, 7, walkable);
        Assert.True(path.IsValid);
    }

    [Fact]
    public void DetPath_Advance_MovesCurrentStep()
    {
        var walkable = AllWalkable(10, 10);
        var pf = new DetPathfinder(10, 10);
        var path = pf.FindPath(0, 0, 3, 0, walkable);

        Assert.Equal(0, path.CurrentStep);
        path.Advance();
        Assert.Equal(1, path.CurrentStep);
    }

    [Fact]
    public void DetPath_Current_ReturnsCorrectCell()
    {
        var walkable = AllWalkable(10, 10);
        var pf = new DetPathfinder(10, 10);
        var path = pf.FindPath(0, 0, 0, 0, walkable);
        var (x, y) = path.Current(10);
        Assert.Equal(0, x);
        Assert.Equal(0, y);
    }

    [Fact]
    public void FindPath_OutOfBounds_Start_ReturnsInvalid()
    {
        var walkable = AllWalkable(10, 10);
        var pf = new DetPathfinder(10, 10);
        var path = pf.FindPath(-1, 0, 5, 5, walkable);
        Assert.False(path.IsValid);
    }

    [Fact]
    public void FindPath_OutOfBounds_Goal_ReturnsInvalid()
    {
        var walkable = AllWalkable(10, 10);
        var pf = new DetPathfinder(10, 10);
        var path = pf.FindPath(0, 0, 20, 20, walkable);
        Assert.False(path.IsValid);
    }

    [Fact]
    public void FindPath_MaxSearchNodes_LimitsSearch()
    {
        var walkable = AllWalkable(20, 20);
        var pf = new DetPathfinder(20, 20);
        // maxSearchNodes=1 cannot reach far goal
        var path = pf.FindPath(0, 0, 15, 15, walkable, maxSearchNodes: 1);
        Assert.False(path.IsValid);
    }

    [Fact]
    public void FindPath_UnitCount_AvoidsCongestion()
    {
        var walkable = AllWalkable(10, 10);
        var unitCount = new DetValueLayer<byte>("units", 10, 10);
        // Mark x=5 column as heavily congested
        for (int y = 0; y < 10; y++) unitCount.Set(5, y, 50);

        var pf = new DetPathfinder(10, 10);
        var direct = pf.FindPath(0, 5, 9, 5, walkable);
        var aware = pf.FindPath(0, 5, 9, 5, walkable, unitCount);

        // Both paths are valid, but congestion-aware path avoids x=5
        Assert.True(direct.IsValid);
        Assert.True(aware.IsValid);
    }

    [Fact]
    public void FindPath_PathStartsAtStart_EndsAtGoal()
    {
        var walkable = AllWalkable(10, 10);
        var pf = new DetPathfinder(10, 10);
        var path = pf.FindPath(2, 3, 7, 6, walkable);

        Assert.True(path.IsValid);

        var (sx, sy) = path.Current(10); // first step = start
        Assert.Equal(2, sx);
        Assert.Equal(3, sy);

        // Walk to end
        while (!path.IsComplete) path.Advance();
        var (gx, gy) = path.Current(10);
        Assert.Equal(7, gx);
        Assert.Equal(6, gy);
    }
}
