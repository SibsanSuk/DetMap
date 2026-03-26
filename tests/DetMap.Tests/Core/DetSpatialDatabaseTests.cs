using DetMath;
using DetMap.Core;
using DetMap.DbCommands;
using DetMap.Schema;
using DetMap.Tables;

namespace DetMap.Tests.Core;

public class DetSpatialDatabaseTests
{
    [Fact]
    public void GetSchema_ReturnsDatabaseMetadataForInspectorUse()
    {
        var database = new DetSpatialDatabase(32, 16);
        database.Grid.CreateFix64Layer("height");
        database.Grid.CreateBitLayer("walkable");
        database.Grid.CreateCellIndex("units");

        var workers = database.CreateTable("workers");
        workers.CreateStringColumn("name");
        var hp = workers.CreateIntColumn("hp");
        workers.CreateStringColumn("statusLabel", DetColumnOptions.Derived("hp"));
        workers.CreateIntIndex("workersByHp", hp);

        database.SetGlobal("population", Fix64.FromInt(12));
        database.SetGlobal("treasury", Fix64.FromInt(500));
        database.CreatePathStore("workerPaths");

        DetDatabaseSchema schema = database.GetSchema();

        Assert.Equal(32, schema.Width);
        Assert.Equal(16, schema.Height);

        Assert.Equal(new[] { "height", "walkable", "units" }, schema.Layers.Select(x => x.Name).ToArray());
        Assert.Equal(new[] { DetLayerKind.ValueFix64, DetLayerKind.Bit, DetLayerKind.CellIndex }, schema.Layers.Select(x => x.Kind).ToArray());

        Assert.Single(schema.Tables);
        Assert.Equal("workers", schema.Tables[0].Name);
        Assert.Equal(new[] { "name", "hp", "statusLabel" }, schema.Tables[0].Columns.Select(x => x.Name).ToArray());
        Assert.False(schema.Tables[0].Columns[0].IsDerived);
        Assert.True(schema.Tables[0].Columns[2].IsDerived);
        Assert.Equal("hp", schema.Tables[0].Columns[2].Source);
        Assert.Single(schema.Tables[0].Indexes);
        Assert.Equal("workersByHp", schema.Tables[0].Indexes[0].Name);
        Assert.Equal(DetColumnKind.Int, schema.Tables[0].Indexes[0].Kind);
        Assert.Equal("hp", schema.Tables[0].Indexes[0].ColumnName);

        Assert.Equal(new[] { "population", "treasury" }, schema.GlobalKeys.ToArray());

        Assert.Single(schema.Stores);
        Assert.Equal("workerPaths", schema.Stores[0].Name);
        Assert.Equal(DetStoreKind.Path, schema.Stores[0].Kind);
    }

    [Fact]
    public void CopyStateFrom_OverwritesTablesLayersGlobalsAndStores()
    {
        var source = new DetSpatialDatabase(16, 16);
        var walkable = source.Grid.CreateBitLayer("walkable");
        walkable.Set(2, 3, true);

        var workers = source.CreateTable("workers");
        var hp = workers.CreateIntColumn("hp");
        int rowId = workers.CreateRow();
        hp.Set(rowId, 42);

        var paths = source.CreatePathStore("workerPaths");
        paths.Set(rowId, new DetMap.Pathfinding.DetPath
        {
            Steps = new[] { 1, 2, 3 },
            Length = 3,
            CurrentStep = 1,
        });

        source.SetGlobal("population", Fix64.FromInt(9));
        source.AdvanceFrame();

        var target = source.Clone();
        target.SetGlobal("population", Fix64.FromInt(1));
        target.Grid.GetBitLayer("walkable").Set(2, 3, false);
        target.GetTable("workers").GetIntColumn("hp").Set(rowId, 7);
        target.GetPathStore("workerPaths").Clear(rowId);

        target.CopyStateFrom(source);

        Assert.Equal(source.Tick, target.Tick);
        Assert.Equal(Fix64.FromInt(9), target.GetGlobal("population"));
        Assert.True(target.Grid.GetBitLayer("walkable").Get(2, 3));
        Assert.Equal(42, target.GetTable("workers").GetIntColumn("hp").Get(rowId));
        Assert.True(target.GetPathStore("workerPaths").Get(rowId).IsValid);
        Assert.Equal(1, target.GetPathStore("workerPaths").Get(rowId).CurrentStep);
    }

    [Fact]
    public void Constructor_DefaultsToThreeFramePool()
    {
        var database = new DetSpatialDatabase(8, 8);

        Assert.Equal(3, database.FrameCount);
        Assert.Equal(0, database.CurrentFrameIndex);
        Assert.Null(database.NextFrameIndex);
        Assert.False(database.HasNextFrame);
    }

    [Fact]
    public void Constructor_RequiresAtLeastTwoFramesWhenFramePoolIsEnabled()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DetSpatialDatabase(8, 8, frameCount: 1));
    }

    [Fact]
    public void PrepareNextFrame_CopiesCurrentStateAndAdvancesTick()
    {
        var database = new DetSpatialDatabase(16, 16);
        database.SetGlobal("population", Fix64.FromInt(4));
        database.AdvanceFrame();

        var workers = database.CreateTable("workers");
        var hp = workers.CreateIntColumn("hp");
        int rowId = workers.CreateRow();
        hp.Set(rowId, 10);

        DetSpatialDatabase next = database.PrepareNextFrame();

        Assert.NotSame(database, next);
        Assert.Equal((database.CurrentFrameIndex + 1) % database.FrameCount, database.NextFrameIndex);
        Assert.Equal(1UL, database.Tick);
        Assert.Equal(2UL, next.Tick);
        Assert.Equal(Fix64.FromInt(4), next.GetGlobal("population"));
        Assert.True(next.GetTable("workers").RowExists(rowId));
        Assert.Equal(10, next.GetTable("workers").GetIntColumn("hp").Get(rowId));
    }

    [Fact]
    public void CommitNextFrame_UpdatesStableCurrentDatabase()
    {
        var database = new DetSpatialDatabase(8, 8);
        database.SetGlobal("population", Fix64.FromInt(1));

        DetSpatialDatabase next = database.PrepareNextFrame();
        next.SetGlobal("population", Fix64.FromInt(2));

        DetSpatialDatabase committed = database.CommitNextFrame();

        Assert.Same(database, committed);
        Assert.False(database.HasNextFrame);
        Assert.Equal(1, database.CurrentFrameIndex);
        Assert.Equal(1UL, database.Tick);
        Assert.Equal(Fix64.FromInt(2), database.GetGlobal("population"));
    }

    [Fact]
    public void DiscardNextFrame_KeepsCurrentStateUnchanged()
    {
        var database = new DetSpatialDatabase(8, 8);
        database.SetGlobal("population", Fix64.FromInt(3));

        DetSpatialDatabase next = database.PrepareNextFrame();
        next.SetGlobal("population", Fix64.FromInt(9));

        database.DiscardNextFrame();

        Assert.False(database.HasNextFrame);
        Assert.Equal(0UL, database.Tick);
        Assert.Equal(Fix64.FromInt(3), database.GetGlobal("population"));
    }

    [Fact]
    public void CommitNextFrame_WrapsAroundInternalRingPool()
    {
        var database = new DetSpatialDatabase(8, 8, frameCount: 3);
        database.SetGlobal("population", Fix64.FromInt(1));

        var next1 = database.PrepareNextFrame();
        next1.SetGlobal("population", Fix64.FromInt(2));
        database.CommitNextFrame();

        var next2 = database.PrepareNextFrame();
        next2.SetGlobal("population", Fix64.FromInt(3));
        database.CommitNextFrame();

        var next3 = database.PrepareNextFrame();
        next3.SetGlobal("population", Fix64.FromInt(4));
        database.CommitNextFrame();

        Assert.Equal(0, database.CurrentFrameIndex);
        Assert.Equal(3UL, database.Tick);
        Assert.Equal(Fix64.FromInt(4), database.GetGlobal("population"));
    }

    [Fact]
    public void PrepareNextFrame_ReusesExistingRingSlotInstances()
    {
        var database = new DetSpatialDatabase(8, 8, frameCount: 3);

        DetSpatialDatabase slot1First = database.PrepareNextFrame();
        database.CommitNextFrame();

        database.PrepareNextFrame();
        database.CommitNextFrame();

        DetSpatialDatabase slot0Again = database.PrepareNextFrame();
        database.CommitNextFrame();

        DetSpatialDatabase slot1Again = database.PrepareNextFrame();

        Assert.NotSame(database, slot0Again);
        Assert.Same(slot1First, slot1Again);
    }

    [Fact]
    public void CachedHandlesRemainValidAfterNextFrameCommit()
    {
        var database = new DetSpatialDatabase(8, 8, frameCount: 3);
        var workers = database.CreateTable("workers");
        var hp = workers.CreateIntColumn("hp");

        int rowId = workers.PeekNextRowId();
        var commands = new DetDbCommandList();
        commands.CreateRow("workers", rowId);
        commands.SetInt("workers", "hp", rowId, 55);

        DetDbCommandApplier.ApplyToNextFrame(database, commands);
        Assert.False(workers.RowExists(rowId));

        database.CommitNextFrame();

        Assert.True(workers.RowExists(rowId));
        Assert.Equal(55, hp.Get(rowId));
        Assert.Same(workers, database.GetTable("workers"));
    }
}
