using DetMap.Core;
using DetMap.Layers;
using DetMap.Spatial;

namespace DetMap.Tests.Spatial;

public class SpatialPlacerTests
{
    private static (DetGrid grid, DetValueLayer<int> placements, DetBitLayer walkable) MakeGrid(int w = 16, int h = 16)
    {
        var grid = new DetGrid(w, h);
        var placements = grid.CreateValueLayer("placements", DetType.Int);
        var walkable = grid.CreateBitLayer("walkable");
        walkable.SetAll(true);
        return (grid, placements, walkable);
    }

    [Fact]
    public void CanPlace_EmptyCell_ReturnsTrue()
    {
        var (grid, placements, walkable) = MakeGrid();
        var def = new SpatialDefinition("house", 2, 2, 1);
        Assert.True(SpatialPlacer.CanPlace(grid, 0, 0, def, placements, walkable));
    }

    [Fact]
    public void CanPlace_OccupiedCell_ReturnsFalse()
    {
        var (grid, placements, walkable) = MakeGrid();
        var def = new SpatialDefinition("house", 2, 2, 1);
        SpatialPlacer.Place(grid, 0, 0, def, placements, walkable);
        Assert.False(SpatialPlacer.CanPlace(grid, 0, 0, def, placements, walkable));
    }

    [Fact]
    public void CanPlace_NonWalkableCell_ReturnsFalse()
    {
        var (grid, placements, walkable) = MakeGrid();
        var def = new SpatialDefinition("house", 1, 1, 1);
        walkable.Set(4, 4, false);

        Assert.False(SpatialPlacer.CanPlace(grid, 4, 4, def, placements, walkable));
    }

    [Fact]
    public void CanPlace_OutOfBounds_ReturnsFalse()
    {
        var (grid, placements, walkable) = MakeGrid(4, 4);
        var def = new SpatialDefinition("house", 3, 3, 1);
        Assert.False(SpatialPlacer.CanPlace(grid, 3, 3, def, placements, walkable));
    }

    [Fact]
    public void Place_StampsPlacementLayer()
    {
        var (grid, placements, walkable) = MakeGrid();
        var def = new SpatialDefinition("temple", 2, 2, 5);
        SpatialPlacer.Place(grid, 1, 1, def, placements, walkable);
        Assert.Equal(5, placements.Get(1, 1));
        Assert.Equal(5, placements.Get(2, 1));
        Assert.Equal(5, placements.Get(1, 2));
        Assert.Equal(5, placements.Get(2, 2));
    }

    [Fact]
    public void Place_BlocksWalkable()
    {
        var (grid, placements, walkable) = MakeGrid();
        var def = new SpatialDefinition("wall", 1, 1, 2);
        SpatialPlacer.Place(grid, 3, 3, def, placements, walkable);
        Assert.False(walkable.Get(3, 3));
    }

    [Fact]
    public void Remove_ClearsBuilding_RestoresWalkable()
    {
        var (grid, placements, walkable) = MakeGrid();
        var def = new SpatialDefinition("house", 2, 2, 1);
        SpatialPlacer.Place(grid, 0, 0, def, placements, walkable);
        SpatialPlacer.Remove(grid, 0, 0, def, placements, walkable);
        Assert.Equal(0, placements.Get(0, 0));
        Assert.True(walkable.Get(0, 0));
    }

    [Fact]
    public void Place_WithMask_OnlySolidCellsAreBlocked()
    {
        var (grid, placements, walkable) = MakeGrid();
        // 2x2 with top-right cell hollow
        var mask = new bool[] { true, false, true, true };
        var def = new SpatialDefinition("lhouse", 2, 2, 3, mask);
        SpatialPlacer.Place(grid, 0, 0, def, placements, walkable);
        Assert.Equal(3, placements.Get(0, 0)); // solid
        Assert.Equal(0, placements.Get(1, 0)); // hollow
        Assert.Equal(3, placements.Get(0, 1)); // solid
        Assert.Equal(3, placements.Get(1, 1)); // solid
    }

    [Fact]
    public void CanPlace_ExtraCheck_Predicate_Blocks()
    {
        var (grid, placements, walkable) = MakeGrid();
        var def = new SpatialDefinition("house", 1, 1, 1);
        // Extra rule: cannot place on y=0 row
        bool result = SpatialPlacer.CanPlace(grid, 5, 0, def, placements, walkable,
            extraCheck: (_, _, y) => y != 0);
        Assert.False(result);
    }

    [Fact]
    public void CanPlace_ExtraCheck_Predicate_Allows()
    {
        var (grid, placements, walkable) = MakeGrid();
        var def = new SpatialDefinition("house", 1, 1, 1);
        bool result = SpatialPlacer.CanPlace(grid, 5, 5, def, placements, walkable,
            extraCheck: (_, _, y) => y != 0);
        Assert.True(result);
    }

    [Fact]
    public void SpatialDefinition_CreateLShapeMask_CorrectCells()
    {
        // 4x4 L-shape: top-right 2x2 quadrant is hollow
        var mask = SpatialDefinition.CreateLShapeMask(4, 4);
        Assert.True(mask[0 * 4 + 0]);  // bottom-left — solid
        Assert.True(mask[0 * 4 + 1]);  // solid
        Assert.False(mask[0 * 4 + 2]); // top-right quadrant — hollow
        Assert.False(mask[0 * 4 + 3]); // hollow
    }

    [Fact]
    public void Place_AdjacentPlacements_NoCellConflict()
    {
        var (grid, placements, walkable) = MakeGrid();
        var def = new SpatialDefinition("house", 2, 2, 1);
        SpatialPlacer.Place(grid, 0, 0, def, placements, walkable);
        SpatialPlacer.Place(grid, 2, 0, def, placements, walkable);
        Assert.Equal(1, placements.Get(1, 0));
        Assert.Equal(1, placements.Get(2, 0));
    }
}
