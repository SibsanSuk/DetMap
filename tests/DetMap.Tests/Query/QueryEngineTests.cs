using DetMap.Core;
using DetMap.Layers;
using DetMap.Query;

namespace DetMap.Tests.Query;

public class QueryEngineTests
{
    private static DetGrid MakeGrid(int w = 16, int h = 16) => new(w, h);

    [Fact]
    public void RectQuery_AllMatch_ReturnsAllCells()
    {
        var grid = MakeGrid(4, 4);
        var results = new CellHit[16];
        int count = QueryEngine.RectQuery(grid, 0, 0, 3, 3, (_, _, _) => true, results);
        Assert.Equal(16, count);
    }

    [Fact]
    public void RectQuery_NoneMatch_ReturnsZero()
    {
        var grid = MakeGrid(4, 4);
        var results = new CellHit[16];
        int count = QueryEngine.RectQuery(grid, 0, 0, 3, 3, (_, _, _) => false, results);
        Assert.Equal(0, count);
    }

    [Fact]
    public void RectQuery_WithPredicate_FiltersCorrectly()
    {
        var grid = MakeGrid(8, 8);
        var layer = grid.CreateValueLayer("val", DetType.Int);
        layer.Set(2, 2, 1);
        layer.Set(3, 3, 1);

        var results = new CellHit[64];
        int count = QueryEngine.RectQuery(grid, 0, 0, 7, 7,
            (g, x, y) => g.GetValueLayer<int>("val").Get(x, y) == 1,
            results);
        Assert.Equal(2, count);
    }

    [Fact]
    public void RadiusQuery_CircleShape_ExcludesCorners()
    {
        var grid = MakeGrid(10, 10);
        var results = new CellHit[100];
        // radius=1 from (5,5): should include (5,5) and 4 cardinal neighbors, not diagonals (1^2+1^2=2 > 1)
        int count = QueryEngine.RadiusQuery(grid, 5, 5, 1, (_, _, _) => true, results);
        Assert.Equal(5, count); // center + N, E, S, W
    }

    [Fact]
    public void FloodFill_ConnectedRegion_ReturnsAllCells()
    {
        var grid = MakeGrid(5, 5);
        var results = new CellHit[25];
        int count = QueryEngine.FloodFill(grid, 0, 0, (_, _, _) => true, results);
        Assert.Equal(25, count);
    }

    [Fact]
    public void FloodFill_Blocked_StopsAtBoundary()
    {
        var grid = MakeGrid(5, 5);
        var walkable = grid.CreateBooleanLayer("walkable");
        walkable.SetAll(true);
        // Block column x=2
        for (int y = 0; y < 5; y++) walkable.Set(2, y, false);

        var results = new CellHit[25];
        int count = QueryEngine.FloodFill(grid, 0, 2,
            (g, x, y) => g.GetBooleanLayer("walkable").Get(x, y),
            results);
        Assert.Equal(10, count); // only left side: x=0,1 * y=0..4
    }

    [Fact]
    public void FloodFill_StartOnBlockedCell_ReturnsZero()
    {
        var grid = MakeGrid(5, 5);
        var results = new CellHit[25];
        int count = QueryEngine.FloodFill(grid, 2, 2, (_, _, _) => false, results);
        Assert.Equal(0, count);
    }

    [Fact]
    public void RadiusQuery_RadiusZero_ReturnsOnlyCenter()
    {
        var grid = MakeGrid(10, 10);
        var results = new CellHit[25];
        int count = QueryEngine.RadiusQuery(grid, 5, 5, 0, (_, _, _) => true, results);
        Assert.Equal(1, count);
        Assert.Equal(5, results[0].X);
        Assert.Equal(5, results[0].Y);
    }

    [Fact]
    public void RectQuery_ResultBuffer_RespectsMaxSize()
    {
        var grid = MakeGrid(10, 10);
        var results = new CellHit[5]; // buffer smaller than matching cells
        int count = QueryEngine.RectQuery(grid, 0, 0, 9, 9, (_, _, _) => true, results);
        Assert.Equal(5, count); // capped at buffer size
    }
}
