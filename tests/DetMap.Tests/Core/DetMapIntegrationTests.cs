using DetMath;
using DetMap.Building;
using DetMap.Core;
using DetMap.Layers;
using DetMap.Pathfinding;
using DetMap.Tables;

namespace DetMap.Tests.Core;

/// <summary>End-to-end tests matching the usage example in the design document.</summary>
public class DetMapIntegrationTests
{
    [Fact]
    public void FullScenario_SpawnMovePathfind_Works()
    {
        var map = new DetMap.Core.DetMap(64, 64);

        var building = map.Grid.CreateValueLayer("building", DetType.Int);
        var walkable = map.Grid.CreateBitLayer("walkable");
        walkable.SetAll(true);

        var units = map.Grid.CreateEntityLayer("units");
        var chars = map.CreateTable("characters");
        var nameCol = chars.CreateStringColumn("name");
        var jobCol = chars.CreateColumn("job", DetType.Byte);
        var paths = map.CreatePathStore("unitPaths");

        map.SetGlobal("treasury", Fix64.FromInt(1000));
        Assert.Equal(Fix64.FromInt(1000), map.GetGlobal("treasury"));

        // Spawn
        int somchai = chars.Insert();
        nameCol.Set(somchai, "Somchai");
        jobCol.Set(somchai, 1);
        units.Add(somchai, 10, 10);
        Assert.Equal(1, units.CountAt(10, 10));

        // Place temple
        var temple = new BuildingDefinition("temple", 3, 3, 2);
        Assert.True(BuildingPlacer.CanPlace(map.Grid, 20, 20, temple, building, walkable));
        BuildingPlacer.Place(map.Grid, 20, 20, temple, building, walkable);
        Assert.Equal(2, building.Get(20, 20));
        Assert.False(walkable.Get(20, 20));

        // Pathfind around building
        var pf = new DetPathfinder(64, 64);
        var path = pf.FindPath(10, 10, 25, 25, walkable);
        Assert.True(path.IsValid);
        paths.Set(somchai, path);

        // Simulate tick
        map.AdvanceTick();
        Assert.Equal(1UL, map.Tick);

        ref DetPath p = ref paths.Get(somchai);
        Assert.False(p.IsComplete);
        p.Advance();
        var (nx, ny) = p.Current(64);
        units.Move(somchai, nx, ny);
        Assert.Equal(1, units.CountAt(nx, ny));
        Assert.Equal(0, units.CountAt(10, 10));
    }

    [Fact]
    public void Globals_SetGet_Deterministic()
    {
        var map = new DetMap.Core.DetMap(16, 16);
        map.SetGlobal("season", Fix64.FromInt(2));
        map.SetGlobal("population", Fix64.FromInt(500));
        Assert.Equal(Fix64.FromInt(2), map.GetGlobal("season"));
        Assert.Equal(Fix64.FromInt(500), map.GetGlobal("population"));
        Assert.Equal(Fix64.Zero, map.GetGlobal("nonexistent"));
    }

    [Fact]
    public void AdvanceTick_IncrementsTick()
    {
        var map = new DetMap.Core.DetMap(16, 16);
        Assert.Equal(0UL, map.Tick);
        map.AdvanceTick();
        map.AdvanceTick();
        Assert.Equal(2UL, map.Tick);
    }
}
