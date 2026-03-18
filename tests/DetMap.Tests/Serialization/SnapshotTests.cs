using DetMath;
using DetMap.Building;
using DetMap.Core;
using DetMap.Layers;
using DetMap.Tables;

namespace DetMap.Tests.Serialization;

public class SnapshotTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static DetMap.Core.DetMap BuildFullMap()
    {
        var map = new DetMap.Core.DetMap(16, 16);

        // layers of every kind
        var byteLayer = map.Grid.CreateLayer("flags",    DetType.Byte);
        var intLayer  = map.Grid.CreateLayer("ids",      DetType.Int);
        var f64Layer  = map.Grid.CreateLayer("height",   DetType.Fix64);
        var bitLayer  = map.Grid.CreateBitLayer("walkable");
        var entities  = map.Grid.CreateEntityMap("units");
        var tags      = map.Grid.CreateTagMap("services");
        var flow      = map.Grid.CreateFlowField("flowA");

        byteLayer.Set(3, 4, 42);
        intLayer.Set(7, 7, 999);
        f64Layer.Set(1, 2, Fix64.FromInt(77));
        bitLayer.SetAll(true);
        bitLayer.Set(5, 5, false);
        entities.Add(0, 2, 3);
        entities.Add(1, 2, 3);
        tags.AddTag(8, 9, "market");
        tags.AddTag(8, 9, "water");
        flow.Set(0, 0, 2, Fix64.FromInt(5));

        // globals
        map.SetGlobal("gold",   Fix64.FromInt(500));
        map.SetGlobal("morale", Fix64.FromInt(80));

        // table with every col kind
        var chars   = map.CreateTable("heroes");
        var nameCol = chars.CreateStringCol("name");
        var hpCol   = chars.CreateCol("hp", DetType.Int);
        var lvlCol  = chars.CreateCol("level", DetType.Byte);
        var xpCol   = chars.CreateCol("xp", DetType.Fix64);

        int id0 = chars.Insert();
        nameCol.Set(id0, "Alice");
        hpCol.Set(id0, 100);
        lvlCol.Set(id0, 5);
        xpCol.Set(id0, Fix64.FromInt(1234));

        int id1 = chars.Insert();
        nameCol.Set(id1, "Bob");
        hpCol.Set(id1, 80);
        lvlCol.Set(id1, 3);
        xpCol.Set(id1, Fix64.FromInt(500));

        chars.Delete(id0); // creates a free-list entry

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
        var map2 = DetMap.Core.DetMap.FromBytes(map.ToBytes());
        Assert.Equal(map.Tick, map2.Tick);
    }

    // ── globals ───────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_Globals_Preserved()
    {
        var map = BuildFullMap();
        var map2 = DetMap.Core.DetMap.FromBytes(map.ToBytes());
        foreach (var key in new[] { "gold", "morale" })
            Assert.Equal(map.GetGlobal(key).RawValue, map2.GetGlobal(key).RawValue);
    }

    // ── layers ───────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_LayerByte_Preserved()
    {
        var map = BuildFullMap();
        var map2 = DetMap.Core.DetMap.FromBytes(map.ToBytes());
        var l1 = map.Grid.Layer<byte>("flags");
        var l2 = map2.Grid.Layer<byte>("flags");
        Assert.Equal(l1.Get(3, 4), l2.Get(3, 4));
        Assert.Equal(l1.Get(0, 0), l2.Get(0, 0));
    }

    [Fact]
    public void RoundTrip_LayerInt_Preserved()
    {
        var map = BuildFullMap();
        var map2 = DetMap.Core.DetMap.FromBytes(map.ToBytes());
        var l1 = map.Grid.Layer<int>("ids");
        var l2 = map2.Grid.Layer<int>("ids");
        Assert.Equal(l1.Get(7, 7), l2.Get(7, 7));
    }

    [Fact]
    public void RoundTrip_LayerFix64_RawValuePreserved()
    {
        var map = BuildFullMap();
        var map2 = DetMap.Core.DetMap.FromBytes(map.ToBytes());
        var l1 = map.Grid.Layer<Fix64>("height");
        var l2 = map2.Grid.Layer<Fix64>("height");
        Assert.Equal(l1.Get(1, 2).RawValue, l2.Get(1, 2).RawValue);
    }

    [Fact]
    public void RoundTrip_BitLayer_Preserved()
    {
        var map = BuildFullMap();
        var map2 = DetMap.Core.DetMap.FromBytes(map.ToBytes());
        var w1 = map.Grid.Structure<DetBitLayer>("walkable");
        var w2 = map2.Grid.Structure<DetBitLayer>("walkable");
        Assert.True(w1.Get(0, 0));
        Assert.Equal(w1.Get(0, 0),  w2.Get(0, 0));
        Assert.Equal(w1.Get(5, 5),  w2.Get(5, 5)); // false
        Assert.False(w2.Get(5, 5));
    }

    [Fact]
    public void RoundTrip_EntityMap_CountsPreserved()
    {
        var map = BuildFullMap();
        var map2 = DetMap.Core.DetMap.FromBytes(map.ToBytes());
        var u1 = map.Grid.Structure<DetEntityMap>("units");
        var u2 = map2.Grid.Structure<DetEntityMap>("units");
        Assert.Equal(u1.CountAt(2, 3), u2.CountAt(2, 3));
        Assert.Equal(2, u2.CountAt(2, 3));
        Assert.Equal(0, u2.CountAt(0, 0));
    }

    [Fact]
    public void RoundTrip_TagMap_TagsPreserved()
    {
        var map = BuildFullMap();
        var map2 = DetMap.Core.DetMap.FromBytes(map.ToBytes());
        var t2 = map2.Grid.Structure<DetTagMap>("services");
        Assert.True(t2.HasTag(8, 9, "market"));
        Assert.True(t2.HasTag(8, 9, "water"));
        Assert.False(t2.HasTag(0, 0, "market"));
    }

    [Fact]
    public void RoundTrip_FlowField_DirectionAndCostPreserved()
    {
        var map = BuildFullMap();
        var map2 = DetMap.Core.DetMap.FromBytes(map.ToBytes());
        var f1 = map.Grid.Structure<DetFlowField>("flowA");
        var f2 = map2.Grid.Structure<DetFlowField>("flowA");
        Assert.Equal(f1.Get(0, 0),          f2.Get(0, 0));
        Assert.Equal(f1.GetCost(0, 0).RawValue, f2.GetCost(0, 0).RawValue);
        Assert.Equal(DetFlowField.Blocked,   f2.Get(1, 1));
    }

    // ── tables ────────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_Table_HighWater_Preserved()
    {
        var map = BuildFullMap();
        var map2 = DetMap.Core.DetMap.FromBytes(map.ToBytes());
        Assert.Equal(map.Table("heroes").HighWater, map2.Table("heroes").HighWater);
    }

    [Fact]
    public void RoundTrip_Table_AliveFlags_Preserved()
    {
        var map = BuildFullMap();
        var map2 = DetMap.Core.DetMap.FromBytes(map.ToBytes());
        var t1 = map.Table("heroes");
        var t2 = map2.Table("heroes");
        Assert.Equal(t1.Exists(0), t2.Exists(0)); // false (despawned)
        Assert.Equal(t1.Exists(1), t2.Exists(1)); // true
    }

    [Fact]
    public void RoundTrip_Table_FreeList_NextSpawnReusesId()
    {
        var map = BuildFullMap();
        var map2 = DetMap.Core.DetMap.FromBytes(map.ToBytes());
        // id0 was despawned → next Insert() should recycle it
        int newId = map2.Table("heroes").Insert();
        Assert.Equal(0, newId);
    }

    [Fact]
    public void RoundTrip_Table_StringCol_Preserved()
    {
        var map = BuildFullMap();
        var map2 = DetMap.Core.DetMap.FromBytes(map.ToBytes());
        var nameCol = map2.Table("heroes").GetStringCol("name");
        Assert.Equal("Bob", nameCol.Get(1));
    }

    [Fact]
    public void RoundTrip_Table_IntCol_Preserved()
    {
        var map = BuildFullMap();
        var map2 = DetMap.Core.DetMap.FromBytes(map.ToBytes());
        var hpCol = map2.Table("heroes").GetCol<int>("hp");
        Assert.Equal(80, hpCol.Get(1));
    }

    [Fact]
    public void RoundTrip_Table_ByteCol_Preserved()
    {
        var map = BuildFullMap();
        var map2 = DetMap.Core.DetMap.FromBytes(map.ToBytes());
        var lvlCol = map2.Table("heroes").GetCol<byte>("level");
        Assert.Equal((byte)3, lvlCol.Get(1));
    }

    [Fact]
    public void RoundTrip_Table_Fix64Col_RawValuePreserved()
    {
        var map = BuildFullMap();
        var map2 = DetMap.Core.DetMap.FromBytes(map.ToBytes());
        var xpCol1 = map.Table("heroes").GetCol<Fix64>("xp");
        var xpCol2 = map2.Table("heroes").GetCol<Fix64>("xp");
        Assert.Equal(xpCol1.Get(1).RawValue, xpCol2.Get(1).RawValue);
    }

    // ── error handling ────────────────────────────────────────────────────────

    [Fact]
    public void Deserialize_BadMagic_Throws()
    {
        var bytes = DetMap.Core.DetMap.FromBytes(BuildFullMap().ToBytes()) // warm-up
            .ToBytes();
        bytes[0] = (byte)'X'; // corrupt magic
        Assert.Throws<InvalidDataException>(() => DetMap.Core.DetMap.FromBytes(bytes));
    }

    [Fact]
    public void Deserialize_BadVersion_Throws()
    {
        var bytes = BuildFullMap().ToBytes();
        // Version is at offset 4 (after 4-byte magic)
        bytes[4] = 99; // unsupported version
        Assert.Throws<InvalidDataException>(() => DetMap.Core.DetMap.FromBytes(bytes));
    }

    // ── idempotency ───────────────────────────────────────────────────────────

    [Fact]
    public void Serialize_Deserialize_ReserializeBytes_Match()
    {
        var map = BuildFullMap();
        byte[] bytes1 = map.ToBytes();
        byte[] bytes2 = DetMap.Core.DetMap.FromBytes(bytes1).ToBytes();
        Assert.Equal(bytes1, bytes2);
    }
}
