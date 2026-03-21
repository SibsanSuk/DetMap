using DetMath;
using DetMap.Commands;
using DetMap.Core;

namespace DetMap.Tests.Commands;

public class DetCommandBatchTests
{
    [Fact]
    public void ApplyTo_WritesGlobalsRowsColumnsLayersAndIndexes()
    {
        var database = new DetSpatialDatabase(16, 16);
        database.Grid.CreateValueLayer("height", DetType.Fix64);
        database.Grid.CreateBooleanLayer("walkable");
        database.Grid.CreateCellIndex("units");

        var workers = database.CreateTable("workers");
        workers.CreateStringColumn("name");
        workers.CreateColumn("hp", DetType.Int);

        var batch = new DetCommandBatch();
        int rowId = workers.PeekNextRowId();

        batch.SetGlobal("population", Fix64.FromInt(1));
        batch.CreateRow("workers", rowId);
        batch.SetString("workers", "name", rowId, "Somchai");
        batch.SetInt("workers", "hp", rowId, 100);
        batch.SetBooleanCell("walkable", 3, 4, true);
        batch.SetFix64Cell("height", 3, 4, Fix64.FromInt(7));
        batch.PlaceRow("units", rowId, 3, 4);

        database.Apply(batch);

        Assert.Equal(Fix64.FromInt(1), database.GetGlobal("population"));
        Assert.True(workers.RowExists(rowId));
        Assert.Equal("Somchai", workers.GetStringColumn("name").Get(rowId));
        Assert.Equal(100, workers.GetColumn<int>("hp").Get(rowId));
        Assert.True(database.Grid.GetBooleanLayer("walkable").Get(3, 4));
        Assert.Equal(Fix64.FromInt(7), database.Grid.GetValueLayer<Fix64>("height").Get(3, 4));
        Assert.Equal(1, database.Grid.GetCellIndex("units").CountAt(3, 4));
    }

    [Fact]
    public void CreateRowCommand_ThrowsWhenExpectedRowIdDoesNotMatch()
    {
        var database = new DetSpatialDatabase(8, 8);
        database.CreateTable("workers");

        var batch = new DetCommandBatch();
        batch.CreateRow("workers", expectedRowId: 3);

        Assert.Throws<InvalidOperationException>(() => database.Apply(batch));
    }

    [Fact]
    public void ApplyTo_PreservesCommandOrder()
    {
        var database = new DetSpatialDatabase(8, 8);
        database.Grid.CreateCellIndex("units");
        var workers = database.CreateTable("workers");
        workers.CreateColumn("hp", DetType.Int);

        int rowId = workers.PeekNextRowId();
        var batch = new DetCommandBatch();
        batch.CreateRow("workers", rowId);
        batch.SetInt("workers", "hp", rowId, 10);
        batch.PlaceRow("units", rowId, 1, 1);
        batch.MoveRow("units", rowId, 2, 1);
        batch.RemoveRow("units", rowId);

        database.Apply(batch);

        Assert.True(workers.RowExists(rowId));
        Assert.Equal(10, workers.GetColumn<int>("hp").Get(rowId));
        Assert.Equal(0, database.Grid.GetCellIndex("units").CountAt(1, 1));
        Assert.Equal(0, database.Grid.GetCellIndex("units").CountAt(2, 1));
    }
}
