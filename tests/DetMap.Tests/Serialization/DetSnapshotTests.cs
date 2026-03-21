using DetMath;
using DetMap.Building;
using DetMap.Core;
using DetMap.Layers;
using DetMap.Pathfinding;
using DetMap.Tables;

namespace DetMap.Tests.Serialization;

public class DetSnapshotTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static DetSpatialDatabase BuildFullMap()
    {
        var map = new DetSpatialDatabase(16, 16);

        // layers of every kind
        var byteLayer = map.Grid.CreateValueLayer("flags",    DetType.Byte);
        var intLayer  = map.Grid.CreateValueLayer("ids",      DetType.Int);
        var f64Layer  = map.Grid.CreateValueLayer("height",   DetType.Fix64);
        var bitLayer  = map.Grid.CreateBooleanLayer("walkable");
        var entities  = map.Grid.CreateCellIndex("units");
        var tags      = map.Grid.CreateTagLayer("services");
        var flow      = map.Grid.CreateFlowLayer("flowA");

        byteLayer.Set(3, 4, 42);
        intLayer.Set(7, 7, 999);
        f64Layer.Set(1, 2, Fix64.FromInt(77));
        bitLayer.SetAll(true);
        bitLayer.Set(5, 5, false);
        entities.Place(0, 2, 3);
        entities.Place(1, 2, 3);
        tags.AddTag(8, 9, "market");
        tags.AddTag(8, 9, "water");
        flow.Set(0, 0, 2, Fix64.FromInt(5));

        // globals
        map.SetGlobal("gold",   Fix64.FromInt(500));
        map.SetGlobal("morale", Fix64.FromInt(80));

        // table with every col kind
        var chars   = map.CreateTable("heroes");
        var nameCol = chars.CreateStringColumn("name");
        var hpCol   = chars.CreateColumn("hp", DetType.Int);
        var lvlCol  = chars.CreateColumn("level", DetType.Byte);
        var xpCol   = chars.CreateColumn("xp", DetType.Fix64);

        int id0 = chars.CreateRow();
        nameCol.Set(id0, "Alice");
        hpCol.Set(id0, 100);
        lvlCol.Set(id0, 5);
        xpCol.Set(id0, Fix64.FromInt(1234));

        int id1 = chars.CreateRow();
        nameCol.Set(id1, "Bob");
        hpCol.Set(id1, 80);
        lvlCol.Set(id1, 3);
        xpCol.Set(id1, Fix64.FromInt(500));

        chars.DeleteRow(id0); // creates a free-list entry

        // path store
        var walkable2 = map.Grid.GetBooleanLayer("walkable");
        var pf        = new DetPathfinder(16, 16);
        var paths     = map.CreatePathStore("unitPaths");
        paths.Set(0, pf.FindPath(0, 0, 8, 8, walkable2));
        paths.Set(1, pf.FindPath(1, 0, 8, 8, walkable2));
        ref DetPath p0 = ref paths.Get(0);
        p0.Advance();
        p0.Advance(); // currentStep = 2

        // advance tick
        map.AdvanceTick();
        map.AdvanceTick();
        map.AdvanceTick();

        return map;
    }

    // ── tick ─────────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_Tick_Preserved()
    {
        var map = BuildFullMap();
        var map2 = DetSpatialDatabase.FromBytes(map.ToBytes());
        Assert.Equal(map.Tick, map2.Tick);
    }

    // ── globals ───────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_Globals_Preserved()
    {
        var map = BuildFullMap();
        var map2 = DetSpatialDatabase.FromBytes(map.ToBytes());
        foreach (var key in new[] { "gold", "morale" })
            Assert.Equal(map.GetGlobal(key).RawValue, map2.GetGlobal(key).RawValue);
    }

    // ── layers ───────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_LayerByte_Preserved()
    {
        var map = BuildFullMap();
        var map2 = DetSpatialDatabase.FromBytes(map.ToBytes());
        var l1 = map.Grid.GetValueLayer<byte>("flags");
        var l2 = map2.Grid.GetValueLayer<byte>("flags");
        Assert.Equal(l1.Get(3, 4), l2.Get(3, 4));
        Assert.Equal(l1.Get(0, 0), l2.Get(0, 0));
    }

    [Fact]
    public void RoundTrip_LayerInt_Preserved()
    {
        var map = BuildFullMap();
        var map2 = DetSpatialDatabase.FromBytes(map.ToBytes());
        var l1 = map.Grid.GetValueLayer<int>("ids");
        var l2 = map2.Grid.GetValueLayer<int>("ids");
        Assert.Equal(l1.Get(7, 7), l2.Get(7, 7));
    }

    [Fact]
    public void RoundTrip_LayerFix64_RawValuePreserved()
    {
        var map = BuildFullMap();
        var map2 = DetSpatialDatabase.FromBytes(map.ToBytes());
        var l1 = map.Grid.GetValueLayer<Fix64>("height");
        var l2 = map2.Grid.GetValueLayer<Fix64>("height");
        Assert.Equal(l1.Get(1, 2).RawValue, l2.Get(1, 2).RawValue);
    }

    [Fact]
    public void RoundTrip_BitLayer_Preserved()
    {
        var map = BuildFullMap();
        var map2 = DetSpatialDatabase.FromBytes(map.ToBytes());
        var w1 = map.Grid.GetBooleanLayer("walkable");
        var w2 = map2.Grid.GetBooleanLayer("walkable");
        Assert.True(w1.Get(0, 0));
        Assert.Equal(w1.Get(0, 0),  w2.Get(0, 0));
        Assert.Equal(w1.Get(5, 5),  w2.Get(5, 5)); // false
        Assert.False(w2.Get(5, 5));
    }

    [Fact]
    public void RoundTrip_CellIndex_CountsPreserved()
    {
        var map = BuildFullMap();
        var map2 = DetSpatialDatabase.FromBytes(map.ToBytes());
        var u1 = map.Grid.GetCellIndex("units");
        var u2 = map2.Grid.GetCellIndex("units");
        Assert.Equal(u1.CountAt(2, 3), u2.CountAt(2, 3));
        Assert.Equal(2, u2.CountAt(2, 3));
        Assert.Equal(0, u2.CountAt(0, 0));
    }

    [Fact]
    public void RoundTrip_TagMap_TagsPreserved()
    {
        var map = BuildFullMap();
        var map2 = DetSpatialDatabase.FromBytes(map.ToBytes());
        var t2 = map2.Grid.GetTagLayer("services");
        Assert.True(t2.HasTag(8, 9, "market"));
        Assert.True(t2.HasTag(8, 9, "water"));
        Assert.False(t2.HasTag(0, 0, "market"));
    }

    [Fact]
    public void RoundTrip_FlowField_DirectionAndCostPreserved()
    {
        var map = BuildFullMap();
        var map2 = DetSpatialDatabase.FromBytes(map.ToBytes());
        var f1 = map.Grid.GetFlowLayer("flowA");
        var f2 = map2.Grid.GetFlowLayer("flowA");
        Assert.Equal(f1.Get(0, 0),          f2.Get(0, 0));
        Assert.Equal(f1.GetCost(0, 0).RawValue, f2.GetCost(0, 0).RawValue);
        Assert.Equal(DetFlowLayer.Blocked,   f2.Get(1, 1));
    }

    // ── tables ────────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_Table_HighWater_Preserved()
    {
        var map = BuildFullMap();
        var map2 = DetSpatialDatabase.FromBytes(map.ToBytes());
        Assert.Equal(map.GetTable("heroes").HighWater, map2.GetTable("heroes").HighWater);
    }

    [Fact]
    public void RoundTrip_Table_AliveFlags_Preserved()
    {
        var map = BuildFullMap();
        var map2 = DetSpatialDatabase.FromBytes(map.ToBytes());
        var t1 = map.GetTable("heroes");
        var t2 = map2.GetTable("heroes");
        Assert.Equal(t1.RowExists(0), t2.RowExists(0)); // false (despawned)
        Assert.Equal(t1.RowExists(1), t2.RowExists(1)); // true
    }

    [Fact]
    public void RoundTrip_Table_FreeList_NextSpawnReusesId()
    {
        var map = BuildFullMap();
        var map2 = DetSpatialDatabase.FromBytes(map.ToBytes());
        // id0 was deleted -> next CreateRow() should recycle it
        int newId = map2.GetTable("heroes").CreateRow();
        Assert.Equal(0, newId);
    }

    [Fact]
    public void RoundTrip_Table_StringCol_Preserved()
    {
        var map = BuildFullMap();
        var map2 = DetSpatialDatabase.FromBytes(map.ToBytes());
        var nameCol = map2.GetTable("heroes").GetStringColumn("name");
        Assert.Equal("Bob", nameCol.Get(1));
    }

    [Fact]
    public void RoundTrip_Table_IntCol_Preserved()
    {
        var map = BuildFullMap();
        var map2 = DetSpatialDatabase.FromBytes(map.ToBytes());
        var hpCol = map2.GetTable("heroes").GetColumn<int>("hp");
        Assert.Equal(80, hpCol.Get(1));
    }

    [Fact]
    public void RoundTrip_Table_ByteCol_Preserved()
    {
        var map = BuildFullMap();
        var map2 = DetSpatialDatabase.FromBytes(map.ToBytes());
        var lvlCol = map2.GetTable("heroes").GetColumn<byte>("level");
        Assert.Equal((byte)3, lvlCol.Get(1));
    }

    [Fact]
    public void RoundTrip_Table_Fix64Col_RawValuePreserved()
    {
        var map = BuildFullMap();
        var map2 = DetSpatialDatabase.FromBytes(map.ToBytes());
        var xpCol1 = map.GetTable("heroes").GetColumn<Fix64>("xp");
        var xpCol2 = map2.GetTable("heroes").GetColumn<Fix64>("xp");
        Assert.Equal(xpCol1.Get(1).RawValue, xpCol2.Get(1).RawValue);
    }

    // ── error handling ────────────────────────────────────────────────────────

    [Fact]
    public void Deserialize_BadMagic_Throws()
    {
        var bytes = DetSpatialDatabase.FromBytes(BuildFullMap().ToBytes()) // warm-up
            .ToBytes();
        bytes[0] = (byte)'X'; // corrupt magic
        Assert.Throws<InvalidDataException>(() => DetSpatialDatabase.FromBytes(bytes));
    }

    [Fact]
    public void Deserialize_BadVersion_Throws()
    {
        var bytes = BuildFullMap().ToBytes();
        // Version is at offset 4 (after 4-byte magic)
        bytes[4] = 99; // unsupported version
        Assert.Throws<InvalidDataException>(() => DetSpatialDatabase.FromBytes(bytes));
    }

    // ── path store ────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_PathStore_IsRegistered()
    {
        var map  = BuildFullMap();
        var map2 = DetSpatialDatabase.FromBytes(map.ToBytes());
        Assert.True(map2.PathStores.ContainsKey("unitPaths"));
    }

    [Fact]
    public void RoundTrip_PathStore_ValidPath_LengthPreserved()
    {
        var map  = BuildFullMap();
        var map2 = DetSpatialDatabase.FromBytes(map.ToBytes());
        ref DetPath p = ref map2.GetPathStore("unitPaths").Get(0);
        Assert.True(p.IsValid);
        Assert.Equal(map.GetPathStore("unitPaths").Get(0).Length, p.Length);
    }

    [Fact]
    public void RoundTrip_PathStore_CurrentStepPreserved()
    {
        var map  = BuildFullMap();
        var map2 = DetSpatialDatabase.FromBytes(map.ToBytes());
        // BuildFullMap advances path[0] by 2 steps
        Assert.Equal(2, map2.GetPathStore("unitPaths").Get(0).CurrentStep);
    }

    [Fact]
    public void RoundTrip_PathStore_StepsPreserved()
    {
        var map  = BuildFullMap();
        var map2 = DetSpatialDatabase.FromBytes(map.ToBytes());
        var p1 = map.GetPathStore("unitPaths").Get(1);
        var p2 = map2.GetPathStore("unitPaths").Get(1);
        Assert.Equal(p1.Length, p2.Length);
        for (int i = 0; i < p1.Length; i++)
            Assert.Equal(p1.Steps![i], p2.Steps![i]);
    }

    // ── idempotency ───────────────────────────────────────────────────────────

    [Fact]
    public void Serialize_Deserialize_ReserializeBytes_Match()
    {
        var map = BuildFullMap();
        byte[] bytes1 = map.ToBytes();
        byte[] bytes2 = DetSpatialDatabase.FromBytes(bytes1).ToBytes();
        Assert.Equal(bytes1, bytes2);
    }
}
