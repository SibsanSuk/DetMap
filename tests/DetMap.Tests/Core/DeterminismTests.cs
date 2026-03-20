using DetMath;
using DetMap.Building;
using DetMap.Core;
using DetMap.Layers;
using DetMap.Pathfinding;
using DetMap.Tables;

namespace DetMap.Tests.Core;

/// <summary>
/// Verifies that running identical operations twice produces bit-identical state.
/// These tests catch any float/double leakage, hash-dependent ordering,
/// or uninitialized memory that could break cross-platform determinism.
/// </summary>
public class DeterminismTests
{
    private static DetMap.Core.DetMap BuildAndSimulate()
    {
        var map = new DetMap.Core.DetMap(32, 32);

        var building = map.Grid.CreateValueLayer("building", DetType.Int);
        var height   = map.Grid.CreateValueLayer("height",   DetType.Fix64);
        var walkable = map.Grid.CreateBitLayer("walkable");
        var units    = map.Grid.CreateEntityLayer("units");
        var services = map.Grid.CreateTagLayer("services");

        walkable.SetAll(true);

        map.SetGlobal("treasury",   Fix64.FromInt(1000));
        map.SetGlobal("population", Fix64.FromInt(0));

        var chars   = map.CreateTable("characters");
        var nameCol = chars.CreateStringColumn("name");
        var jobCol  = chars.CreateColumn("job", DetType.Byte);
        var pathStore = map.CreatePathStore("unitPaths");

        // Place buildings
        var house  = new BuildingDefinition("house",  2, 2, 1);
        var market = new BuildingDefinition("market", 3, 2, 2);
        BuildingPlacer.Place(map.Grid, 0,  0, house,  building, walkable);
        BuildingPlacer.Place(map.Grid, 10, 5, market, building, walkable);

        // Set height data
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
            height.Set(x, y, Fix64.FromInt(x + y));

        // Add service coverage
        for (int y = 5; y < 10; y++)
        for (int x = 5; x < 10; x++)
            services.AddTag(x, y, "market");

        // Spawn entities
        for (int i = 0; i < 8; i++)
        {
            int id = chars.Insert();
            nameCol.Set(id, $"Unit{i}");
            jobCol.Set(id, (byte)(i % 3));
            units.Add(id, i * 2, i % 4);
        }

        // Pathfind and simulate 5 ticks
        var pf = new DetPathfinder(32, 32);
        for (int id = 0; id < 8; id++)
        {
            var path = pf.FindPath(id * 2, id % 4, 20, 20, walkable);
            pathStore.Set(id, path);
        }

        for (int tick = 0; tick < 5; tick++)
        {
            map.AdvanceTick();

            for (int id = 0; id < 8; id++)
            {
                if (!chars.Exists(id)) continue;
                ref DetPath p = ref pathStore.Get(id);
                if (!p.IsValid || p.IsComplete) continue;
                p.Advance();
                var (nx, ny) = p.Current(32);
                units.Move(id, nx, ny);
            }

            map.SetGlobal("population", Fix64.FromInt(chars.HighWater));
        }

        return map;
    }

    [Fact]
    public void SimulationTick_SameInputTwice_IdenticalTick()
    {
        var map1 = BuildAndSimulate();
        var map2 = BuildAndSimulate();
        Assert.Equal(map1.Tick, map2.Tick);
    }

    [Fact]
    public void SimulationGlobals_SameInputTwice_IdenticalValues()
    {
        var map1 = BuildAndSimulate();
        var map2 = BuildAndSimulate();
        foreach (var key in new[] { "treasury", "population" })
            Assert.Equal(map1.GetGlobal(key), map2.GetGlobal(key));
    }

    [Fact]
    public void SimulationBuildingLayer_SameInputTwice_IdenticalGrid()
    {
        var map1 = BuildAndSimulate();
        var map2 = BuildAndSimulate();
        var b1 = map1.Grid.GetValueLayer<int>("building");
        var b2 = map2.Grid.GetValueLayer<int>("building");
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
            Assert.Equal(b1.Get(x, y), b2.Get(x, y));
    }

    [Fact]
    public void SimulationHeightLayer_SameInputTwice_IdenticalGrid()
    {
        var map1 = BuildAndSimulate();
        var map2 = BuildAndSimulate();
        var h1 = map1.Grid.GetValueLayer<Fix64>("height");
        var h2 = map2.Grid.GetValueLayer<Fix64>("height");
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
            Assert.Equal(h1.Get(x, y).RawValue, h2.Get(x, y).RawValue);
    }

    [Fact]
    public void SimulationWalkable_SameInputTwice_IdenticalGrid()
    {
        var map1 = BuildAndSimulate();
        var map2 = BuildAndSimulate();
        var w1 = map1.Grid.GetBitLayer("walkable");
        var w2 = map2.Grid.GetBitLayer("walkable");
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
            Assert.Equal(w1.Get(x, y), w2.Get(x, y));
    }

    [Fact]
    public void SimulationEntityPositions_SameInputTwice_IdenticalCounts()
    {
        var map1 = BuildAndSimulate();
        var map2 = BuildAndSimulate();
        var u1 = map1.Grid.GetEntityLayer("units");
        var u2 = map2.Grid.GetEntityLayer("units");
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
            Assert.Equal(u1.CountAt(x, y), u2.CountAt(x, y));
    }

    [Fact]
    public void SimulationPath_SameInputTwice_IdenticalRoute()
    {
        var walkable = new DetBitLayer("walkable", 32, 32);
        walkable.SetAll(true);
        // Wall at y=10, x=5..20
        for (int x = 5; x <= 20; x++) walkable.Set(x, 10, false);

        var pf = new DetPathfinder(32, 32);
        var p1 = pf.FindPath(2, 5, 25, 15, walkable);
        var p2 = pf.FindPath(2, 5, 25, 15, walkable);

        Assert.Equal(p1.Length, p2.Length);
        for (int i = 0; i < p1.Length; i++)
            Assert.Equal(p1.Steps![i], p2.Steps![i]);
    }
}
