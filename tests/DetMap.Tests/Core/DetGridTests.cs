using DetMap.Core;
using DetMap.Layers;
using DetMap.Schema;

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
        var layer = grid.CreateValueLayer("placements", DetType.Int);
        var retrieved = grid.GetValueLayer<int>("placements");
        Assert.Same(layer, retrieved);
    }

    [Fact]
    public void TypedLayerFactories_RetrieveTypedLayersByName()
    {
        var grid = new DetGrid(8, 8);

        var byteLayer = grid.CreateByteLayer("flags");
        var intLayer = grid.CreateIntLayer("placements");
        var fix64Layer = grid.CreateFix64Layer("height");

        Assert.Same(byteLayer, grid.GetByteLayer("flags"));
        Assert.Same(intLayer, grid.GetIntLayer("placements"));
        Assert.Same(fix64Layer, grid.GetFix64Layer("height"));
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
    public void CreateCellIndex_RetrievedByName()
    {
        var grid = new DetGrid(8, 8);
        var layer = grid.CreateCellIndex("units");
        var retrieved = grid.GetCellIndex("units");
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
        grid.CreateValueLayer("placements", DetType.Int);
        grid.CreateBitLayer("walkable");
        grid.CreateCellIndex("units");
        Assert.Equal(3, grid.AllLayers.Count);
    }

    [Fact]
    public void Width_Height_MatchConstructor()
    {
        var grid = new DetGrid(32, 64);
        Assert.Equal(32, grid.Width);
        Assert.Equal(64, grid.Height);
    }

    [Fact]
    public void GetLayerSchemas_PreservesCreationOrderAndKinds()
    {
        var grid = new DetGrid(8, 8);
        grid.CreateValueLayer("height", DetType.Fix64);
        grid.CreateBitLayer("walkable");
        grid.CreateCellIndex("units");

        IReadOnlyList<DetLayerSchema> schemas = grid.GetLayerSchemas();

        Assert.Equal(3, schemas.Count);
        Assert.Equal("height", schemas[0].Name);
        Assert.Equal(DetLayerKind.ValueFix64, schemas[0].Kind);
        Assert.Equal("walkable", schemas[1].Name);
        Assert.Equal(DetLayerKind.Bit, schemas[1].Kind);
        Assert.Equal("units", schemas[2].Name);
        Assert.Equal(DetLayerKind.CellIndex, schemas[2].Kind);
    }
}
