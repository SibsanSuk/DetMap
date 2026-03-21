using DetMap.Core;
using DetMap.Schema;
using DetMap.Tables;

namespace DetMap.Tests.Tables;

public class DetTableTests
{
    [Fact]
    public void Spawn_ReturnsSequentialIds()
    {
        var table = new DetTable("chars");
        int id0 = table.CreateRow();
        int id1 = table.CreateRow();
        Assert.Equal(0, id0);
        Assert.Equal(1, id1);
    }

    [Fact]
    public void Despawn_RecyclesId()
    {
        var table = new DetTable("chars");
        int id = table.CreateRow();
        table.DeleteRow(id);
        int recycled = table.CreateRow();
        Assert.Equal(id, recycled);
    }

    [Fact]
    public void IsAlive_AfterSpawn_IsTrue()
    {
        var table = new DetTable("chars");
        int id = table.CreateRow();
        Assert.True(table.RowExists(id));
    }

    [Fact]
    public void IsAlive_AfterDespawn_IsFalse()
    {
        var table = new DetTable("chars");
        int id = table.CreateRow();
        table.DeleteRow(id);
        Assert.False(table.RowExists(id));
    }

    [Fact]
    public void Column_SetGet_RoundTrips()
    {
        var table = new DetTable("chars");
        var jobCol = table.CreateByteColumn("job");
        int id = table.CreateRow();
        jobCol.Set(id, 3);
        Assert.Equal(3, jobCol.Get(id));
    }

    [Fact]
    public void StringColumn_SetGet_RoundTrips()
    {
        var table = new DetTable("chars");
        var nameCol = table.CreateStringColumn("name");
        int id = table.CreateRow();
        nameCol.Set(id, "Somchai");
        Assert.Equal("Somchai", nameCol.Get(id));
    }

    [Fact]
    public void GetRowIds_IteratesInDeterministicOrder()
    {
        var table = new DetTable("chars");
        int a = table.CreateRow();
        int b = table.CreateRow();
        int c = table.CreateRow();
        table.DeleteRow(b);

        var alive = table.GetRowIds().ToList();
        Assert.Equal(new[] { a, c }, alive);
    }

    [Fact]
    public void FreeList_LIFO_EnsuresDeterministicRecycle()
    {
        var table = new DetTable("chars");
        int a = table.CreateRow();
        int b = table.CreateRow();
        table.DeleteRow(a);
        table.DeleteRow(b);
        // LIFO: b despawned last → b recycled first
        Assert.Equal(b, table.CreateRow());
        Assert.Equal(a, table.CreateRow());
    }

    [Fact]
    public void GetColumn_RetrievesColumnByName()
    {
        var table = new DetTable("chars");
        var added = table.CreateColumn("job", DetType.Byte);
        var retrieved = table.GetColumn<byte>("job");
        int id = table.CreateRow();
        retrieved.Set(id, 7);
        Assert.Equal(7, added.Get(id));
    }

    [Fact]
    public void GetStringColumn_RetrievesColumnByName()
    {
        var table = new DetTable("chars");
        var added = table.CreateStringColumn("name");
        var retrieved = table.GetStringColumn("name");
        int id = table.CreateRow();
        retrieved.Set(id, "Test");
        Assert.Equal("Test", added.Get(id));
    }

    [Fact]
    public void TypedColumnFactories_RetrieveTypedColumnsByName()
    {
        var table = new DetTable("chars");

        var byteColumn = table.CreateByteColumn("job");
        var intColumn = table.CreateIntColumn("hp");
        var fix64Column = table.CreateFix64Column("xp");

        Assert.Same(byteColumn, table.GetByteColumn("job"));
        Assert.Same(intColumn, table.GetIntColumn("hp"));
        Assert.Same(fix64Column, table.GetFix64Column("xp"));
    }

    [Fact]
    public void HighWater_TracksMaxIdAllocated()
    {
        var table = new DetTable("chars");
        table.CreateRow();
        table.CreateRow();
        table.CreateRow();
        Assert.Equal(3, table.HighWater);
    }

    [Fact]
    public void HighWater_DoesNotDecreaseOnDespawn()
    {
        var table = new DetTable("chars");
        int id = table.CreateRow();
        table.DeleteRow(id);
        Assert.Equal(1, table.HighWater);
    }

    [Fact]
    public void PeekNextRowId_MatchesNextCreateRow()
    {
        var table = new DetTable("chars");
        Assert.Equal(0, table.PeekNextRowId());

        int first = table.CreateRow();
        Assert.Equal(first + 1, table.PeekNextRowId());

        table.DeleteRow(first);
        Assert.Equal(first, table.PeekNextRowId());
        Assert.Equal(first, table.CreateRow());
    }

    [Fact]
    public void Name_ReturnsTableName()
    {
        var table = new DetTable("characters");
        Assert.Equal("characters", table.Name);
    }

    [Fact]
    public void GetSchema_PreservesColumnOrderAndKinds()
    {
        var table = new DetTable("workers");
        table.CreateStringColumn("name");
        table.CreateByteColumn("job");
        table.CreateIntColumn("hp");

        DetTableSchema schema = table.GetSchema();

        Assert.Equal("workers", schema.Name);
        Assert.Equal(3, schema.Columns.Count);
        Assert.Equal("name", schema.Columns[0].Name);
        Assert.Equal(DetColumnKind.String, schema.Columns[0].Kind);
        Assert.Equal("job", schema.Columns[1].Name);
        Assert.Equal(DetColumnKind.Byte, schema.Columns[1].Kind);
        Assert.Equal("hp", schema.Columns[2].Name);
        Assert.Equal(DetColumnKind.Int, schema.Columns[2].Kind);
    }
}
