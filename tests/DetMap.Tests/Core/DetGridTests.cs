using DetMap.Core;
using DetMap.Layers;

namespace DetMap.Tests.Core;

public class DetGridTests
{
    [Fact]
    public void InBounds_InsideGrid_ReturnsTrue()
    {
        var grid = new DetGrid(10, 10);
        Assert.True(grid.InBounds(0, 0));
        Assert.True(grid.InBounds(9, 9));
        Assert.True(grid.InBounds(5, 5));
    }

    [Fact]
    public void InBounds_AtBoundary_ReturnsFalse()
    {
        var grid = new DetGrid(10, 10);
        Assert.False(grid.InBounds(10, 0));
        Assert.False(grid.InBounds(0, 10));
        Assert.False(grid.InBounds(10, 10));
    }

    [Fact]
    public void InBounds_Negative_ReturnsFalse()
    {
        var grid = new DetGrid(10, 10);
        Assert.False(grid.InBounds(-1, 0));
        Assert.False(grid.InBounds(0, -1));
    }

    [Fact]
    public void CreateLayer_RetrievedByName()
    {
        var grid = new DetGrid(8, 8);
        var layer = grid.CreateValueLayer("building", DetType.Int);
        var retrieved = grid.GetValueLayer<int>("building");
        Assert.Same(layer, retrieved);
    }

    [Fact]
    public void CreateBitLayer_RetrievedByName()
    {
        var grid = new DetGrid(8, 8);
        var layer = grid.CreateBitLayer("walkable");
        var retrieved = grid.GetBitLayer("walkable");
        Assert.Same(layer, retrieved);
    }

    [Fact]
    public void CreateEntityLayer_RetrievedByName()
    {
        var grid = new DetGrid(8, 8);
        var layer = grid.CreateEntityLayer("units");
        var retrieved = grid.GetEntityLayer("units");
        Assert.Same(layer, retrieved);
    }

    [Fact]
    public void CreateTagLayer_RetrievedByName()
    {
        var grid = new DetGrid(8, 8);
        var layer = grid.CreateTagLayer("services");
        var retrieved = grid.GetTagLayer("services");
        Assert.Same(layer, retrieved);
    }

    [Fact]
    public void CreateFlowLayer_RetrievedByName()
    {
        var grid = new DetGrid(8, 8);
        var layer = grid.CreateFlowLayer("flow");
        var retrieved = grid.GetFlowLayer("flow");
        Assert.Same(layer, retrieved);
    }

    [Fact]
    public void AllLayers_ContainsAllCreated()
    {
        var grid = new DetGrid(8, 8);
        grid.CreateValueLayer("building", DetType.Int);
        grid.CreateBitLayer("walkable");
        grid.CreateEntityLayer("units");
        Assert.Equal(3, grid.AllLayers.Count);
    }

    [Fact]
    public void Width_Height_MatchConstructor()
    {
        var grid = new DetGrid(32, 64);
        Assert.Equal(32, grid.Width);
        Assert.Equal(64, grid.Height);
    }
}
