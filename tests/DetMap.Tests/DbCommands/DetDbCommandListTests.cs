using DetMath;
using DetMap.Core;
using DetMap.DbCommands;

namespace DetMap.Tests.DbCommands;

public class DetDbCommandListTests
{
    [Fact]
    public void ApplyTo_WritesGlobalsRowsColumnsLayersAndIndexes()
    {
        var database = new DetSpatialDatabase(16, 16);
        database.Grid.CreateFix64Layer("height");
        database.Grid.CreateBitLayer("walkable");
        database.Grid.CreateCellIndex("units");

        var workers = database.CreateTable("workers");
        workers.CreateStringColumn("name");
        workers.CreateIntColumn("hp");

        var batch = new DetDbCommandList();
        int rowId = workers.PeekNextRowId();

        batch.SetGlobal("population", Fix64.FromInt(1));
        batch.CreateRow("workers", rowId);
        batch.SetString("workers", "name", rowId, "Somchai");
        batch.SetInt("workers", "hp", rowId, 100);
        batch.SetBitCell("walkable", 3, 4, true);
        batch.SetFix64Cell("height", 3, 4, Fix64.FromInt(7));
        batch.PlaceRow("units", rowId, 3, 4);

        var result = DetDbCommandApplier.ApplyFrame(database, batch);

        Assert.Equal(Fix64.FromInt(1), database.GetGlobal("population"));
        Assert.True(workers.RowExists(rowId));
        Assert.Equal("Somchai", workers.GetStringColumn("name").Get(rowId));
        Assert.Equal(100, workers.GetIntColumn("hp").Get(rowId));
        Assert.True(database.Grid.GetBitLayer("walkable").Get(3, 4));
        Assert.Equal(Fix64.FromInt(7), database.Grid.GetFix64Layer("height").Get(3, 4));
        Assert.Equal(1, database.Grid.GetCellIndex("units").CountAt(3, 4));

        Assert.Equal(7, result.CommandCount);
        Assert.Equal(database.Tick, result.Tick);
        Assert.Equal(database.ComputeStateHashHex(), result.StateHashHex);
        Assert.Equal(database.ComputeFrameHashHex(), result.FrameHashHex);
        Assert.Equal(1, result.Summary.GlobalWriteCount);
        Assert.Equal(1, result.Summary.CreatedRowCount);
        Assert.Equal(2, result.Summary.ColumnWriteCount);
        Assert.Equal(2, result.Summary.LayerWriteCount);
        Assert.Equal(1, result.Summary.IndexWriteCount);
        Assert.Contains("population", result.Summary.ChangedGlobals);
        Assert.Contains("workers", result.Summary.ChangedTables);
        Assert.Contains("workers.name", result.Summary.ChangedColumns);
        Assert.Contains("workers.hp", result.Summary.ChangedColumns);
        Assert.Contains("walkable", result.Summary.ChangedLayers);
        Assert.Contains("height", result.Summary.ChangedLayers);
        Assert.Contains("units", result.Summary.ChangedIndices);
        Assert.Contains($"workers:{rowId}", result.Summary.TouchedRows);
        Assert.Contains("walkable(3,4)", result.Summary.TouchedCells);
        Assert.Contains("height(3,4)", result.Summary.TouchedCells);
        Assert.Contains("units(3,4)", result.Summary.TouchedCells);
    }

    [Fact]
    public void CreateRowCommand_ThrowsWhenExpectedRowIdDoesNotMatch()
    {
        var database = new DetSpatialDatabase(8, 8);
        database.CreateTable("workers");

        var batch = new DetDbCommandList();
        batch.CreateRow("workers", expectedRowId: 3);

        Assert.Throws<InvalidOperationException>(() => DetDbCommandApplier.ApplyFrame(database, batch));
    }

    [Fact]
    public void ApplyTo_PreservesCommandOrder()
    {
        var database = new DetSpatialDatabase(8, 8);
        database.Grid.CreateCellIndex("units");
        var workers = database.CreateTable("workers");
        workers.CreateIntColumn("hp");

        int rowId = workers.PeekNextRowId();
        var batch = new DetDbCommandList();
        batch.CreateRow("workers", rowId);
        batch.SetInt("workers", "hp", rowId, 10);
        batch.PlaceRow("units", rowId, 1, 1);
        batch.MoveRow("units", rowId, 2, 1);
        batch.RemoveRow("units", rowId);

        DetDbCommandApplier.ApplyFrame(database, batch);

        Assert.True(workers.RowExists(rowId));
        Assert.Equal(10, workers.GetIntColumn("hp").Get(rowId));
        Assert.Equal(0, database.Grid.GetCellIndex("units").CountAt(1, 1));
        Assert.Equal(0, database.Grid.GetCellIndex("units").CountAt(2, 1));
    }

    [Fact]
    public void BuildSummary_CollectsTouchedTargetsWithoutApplying()
    {
        var batch = new DetDbCommandList();
        batch.SetGlobal("population", Fix64.FromInt(2));
        batch.CreateRow("workers", 4);
        batch.SetInt("workers", "hp", 4, 100);
        batch.SetInt("workers", "hp", 4, 120);
        batch.SetBitCell("walkable", 3, 4, true);
        batch.MoveRow("units", 4, 5, 6);
        batch.RemoveRow("units", 4);

        var summary = batch.BuildSummary();

        Assert.Equal(7, summary.CommandCount);
        Assert.Equal(1, summary.GlobalWriteCount);
        Assert.Equal(1, summary.CreatedRowCount);
        Assert.Equal(2, summary.ColumnWriteCount);
        Assert.Equal(1, summary.LayerWriteCount);
        Assert.Equal(2, summary.IndexWriteCount);
        Assert.Equal(new[] { "population" }, summary.ChangedGlobals);
        Assert.Equal(new[] { "workers" }, summary.ChangedTables);
        Assert.Equal(new[] { "workers.hp" }, summary.ChangedColumns);
        Assert.Equal(new[] { "walkable" }, summary.ChangedLayers);
        Assert.Equal(new[] { "units" }, summary.ChangedIndices);
        Assert.Contains("workers:4", summary.TouchedRows);
        Assert.Contains("units:4", summary.TouchedRows);
        Assert.Contains("walkable(3,4)", summary.TouchedCells);
        Assert.Contains("units(5,6)", summary.TouchedCells);
    }

    [Fact]
    public void ApplyToNextFrame_WritesOnlyPreparedFrameUntilCommit()
    {
        var database = new DetSpatialDatabase(16, 16);
        var workers = database.CreateTable("workers");
        workers.CreateIntColumn("hp");

        int rowId = workers.PeekNextRowId();
        var batch = new DetDbCommandList();
        batch.CreateRow("workers", rowId);
        batch.SetInt("workers", "hp", rowId, 25);

        var result = DetDbCommandApplier.ApplyToNextFrame(database, batch);

        Assert.False(database.GetTable("workers").RowExists(rowId));
        Assert.True(database.HasNextFrame);

        DetSpatialDatabase next = database.GetNextFrame();
        Assert.Equal(1UL, next.Tick);
        Assert.True(next.GetTable("workers").RowExists(rowId));
        Assert.Equal(25, next.GetTable("workers").GetIntColumn("hp").Get(rowId));
        Assert.Equal(next.Tick, result.Tick);
        Assert.Equal(next.ComputeStateHashHex(), result.StateHashHex);
        Assert.Equal(next.ComputeFrameHashHex(), result.FrameHashHex);

        database.CommitNextFrame();

        Assert.True(database.GetTable("workers").RowExists(rowId));
        Assert.Equal(25, database.GetTable("workers").GetIntColumn("hp").Get(rowId));
    }
}
