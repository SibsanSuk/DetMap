using DetMap.Building;
using DetMap.Core;
using DetMap.Layers;

namespace DetMap.Tests.Building;

public class BuildingPlacerTests
{
    private static (DetGrid grid, DetValueLayer<int> bldg, DetBitLayer walkable) MakeGrid(int w = 16, int h = 16)
    {
        var grid = new DetGrid(w, h);
        var bldg = grid.CreateValueLayer("building", DetType.Int);
        var walkable = grid.CreateBitLayer("walkable");
        walkable.SetAll(true);
        return (grid, bldg, walkable);
    }

    [Fact]
    public void CanPlace_EmptyCell_ReturnsTrue()
    {
        var (grid, bldg, walkable) = MakeGrid();
        var def = new BuildingDefinition("house", 2, 2, 1);
        Assert.True(BuildingPlacer.CanPlace(grid, 0, 0, def, bldg, walkable));
    }

    [Fact]
    public void CanPlace_OccupiedCell_ReturnsFalse()
    {
        var (grid, bldg, walkable) = MakeGrid();
        var def = new BuildingDefinition("house", 2, 2, 1);
        BuildingPlacer.Place(grid, 0, 0, def, bldg, walkable);
        Assert.False(BuildingPlacer.CanPlace(grid, 0, 0, def, bldg, walkable));
    }

    [Fact]
    public void CanPlace_OutOfBounds_ReturnsFalse()
    {
        var (grid, bldg, walkable) = MakeGrid(4, 4);
        var def = new BuildingDefinition("house", 3, 3, 1);
        Assert.False(BuildingPlacer.CanPlace(grid, 3, 3, def, bldg, walkable));
    }

    [Fact]
    public void Place_StampsBuildingLayer()
    {
        var (grid, bldg, walkable) = MakeGrid();
        var def = new BuildingDefinition("temple", 2, 2, 5);
        BuildingPlacer.Place(grid, 1, 1, def, bldg, walkable);
        Assert.Equal(5, bldg.Get(1, 1));
        Assert.Equal(5, bldg.Get(2, 1));
        Assert.Equal(5, bldg.Get(1, 2));
        Assert.Equal(5, bldg.Get(2, 2));
    }

    [Fact]
    public void Place_BlocksWalkable()
    {
        var (grid, bldg, walkable) = MakeGrid();
        var def = new BuildingDefinition("wall", 1, 1, 2);
        BuildingPlacer.Place(grid, 3, 3, def, bldg, walkable);
        Assert.False(walkable.Get(3, 3));
    }

    [Fact]
    public void Remove_ClearsBuilding_RestoresWalkable()
    {
        var (grid, bldg, walkable) = MakeGrid();
        var def = new BuildingDefinition("house", 2, 2, 1);
        BuildingPlacer.Place(grid, 0, 0, def, bldg, walkable);
        BuildingPlacer.Remove(grid, 0, 0, def, bldg, walkable);
        Assert.Equal(0, bldg.Get(0, 0));
        Assert.True(walkable.Get(0, 0));
    }

    [Fact]
    public void Place_WithMask_OnlySolidCellsAreBlocked()
    {
        var (grid, bldg, walkable) = MakeGrid();
        // 2x2 with top-right cell hollow
        var mask = new bool[] { true, false, true, true };
        var def = new BuildingDefinition("lhouse", 2, 2, 3, mask);
        BuildingPlacer.Place(grid, 0, 0, def, bldg, walkable);
        Assert.Equal(3, bldg.Get(0, 0)); // solid
        Assert.Equal(0, bldg.Get(1, 0)); // hollow
        Assert.Equal(3, bldg.Get(0, 1)); // solid
        Assert.Equal(3, bldg.Get(1, 1)); // solid
    }

    [Fact]
    public void CanPlace_ExtraCheck_Predicate_Blocks()
    {
        var (grid, bldg, walkable) = MakeGrid();
        var def = new BuildingDefinition("house", 1, 1, 1);
        // Extra rule: cannot place on y=0 row
        bool result = BuildingPlacer.CanPlace(grid, 5, 0, def, bldg, walkable,
            extraCheck: (_, _, y) => y != 0);
        Assert.False(result);
    }

    [Fact]
    public void CanPlace_ExtraCheck_Predicate_Allows()
    {
        var (grid, bldg, walkable) = MakeGrid();
        var def = new BuildingDefinition("house", 1, 1, 1);
        bool result = BuildingPlacer.CanPlace(grid, 5, 5, def, bldg, walkable,
            extraCheck: (_, _, y) => y != 0);
        Assert.True(result);
    }

    [Fact]
    public void BuildingDefinition_CreateLShapeMask_CorrectCells()
    {
        // 4x4 L-shape: top-right 2x2 quadrant is hollow
        var mask = BuildingDefinition.CreateLShapeMask(4, 4);
        Assert.True(mask[0 * 4 + 0]);  // bottom-left — solid
        Assert.True(mask[0 * 4 + 1]);  // solid
        Assert.False(mask[0 * 4 + 2]); // top-right quadrant — hollow
        Assert.False(mask[0 * 4 + 3]); // hollow
    }

    [Fact]
    public void Place_AdjacentBuildings_NoCellConflict()
    {
        var (grid, bldg, walkable) = MakeGrid();
        var def = new BuildingDefinition("house", 2, 2, 1);
        BuildingPlacer.Place(grid, 0, 0, def, bldg, walkable);
        BuildingPlacer.Place(grid, 2, 0, def, bldg, walkable);
        Assert.Equal(1, bldg.Get(1, 0));
        Assert.Equal(1, bldg.Get(2, 0));
    }
}
