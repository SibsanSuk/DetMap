using DetMath;
using DetMap.Core;
using DetMap.DbCommands;
using DetMap.Serialization;

namespace DetMap.Tests.Serialization;

public class DetStateHashTests
{
    [Fact]
    public void ComputeStateHashHex_IgnoresTick()
    {
        var database = new DetSpatialDatabase(8, 8);
        database.SetGlobal("gold", Fix64.FromInt(100));
        var height = database.Grid.CreateIntLayer("height");
        height.Set(2, 3, 5);

        var units = database.CreateTable("units");
        var hp = units.CreateIntColumn("hp");
        int rowId = units.CreateRow();
        hp.Set(rowId, 42);

        string before = database.ComputeStateHashHex();

        database.AdvanceFrame();
        database.AdvanceFrame();

        string after = database.ComputeStateHashHex();

        Assert.Equal(before, after);
    }

    [Fact]
    public void ComputeStateHashHex_ChangesWhenStateChanges()
    {
        var database = new DetSpatialDatabase(8, 8);
        database.SetGlobal("gold", Fix64.FromInt(100));
        var height = database.Grid.CreateIntLayer("height");
        height.Set(2, 3, 5);

        string before = database.ComputeStateHashHex();

        height.Set(2, 3, 6);

        string after = database.ComputeStateHashHex();

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void ComputeHex_FromSnapshotBytes_MatchesDatabaseStateHash()
    {
        var database = new DetSpatialDatabase(8, 8);
        database.SetGlobal("gold", Fix64.FromInt(100));
        database.Grid.CreateIntLayer("height").Set(2, 3, 5);

        string fromDatabase = database.ComputeStateHashHex();
        string fromBytes = DetStateHash.ComputeHex(database.ToBytes());

        Assert.Equal(fromDatabase, fromBytes);
    }

    [Fact]
    public void ComputeHex_FromSnapshotBytes_IgnoresOptionalDbFrameRecord()
    {
        var database = new DetSpatialDatabase(8, 8);
        database.SetGlobal("gold", Fix64.FromInt(100));
        database.Grid.CreateIntLayer("height").Set(2, 3, 5);

        var commands = new DetDbCommandList();
        commands.SetIntCell("height", 2, 3, 999);
        commands.SetGlobal("gold", Fix64.FromInt(101));

        byte[] withoutFrame = DetSnapshot.Serialize(database);
        byte[] withFrame = DetSnapshot.Serialize(
            database,
            DetDbFrameRecord.Create(
                database.Tick,
                database.ComputeStateHashHex(),
                database.ComputeFrameHashHex(),
                commands));

        Assert.Equal(DetStateHash.ComputeHex(withoutFrame), DetStateHash.ComputeHex(withFrame));
    }
}
