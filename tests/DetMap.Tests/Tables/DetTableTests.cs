using DetMap.Core;
using DetMap.Schema;
using DetMap.Tables;
using DetMath;

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
    public void DeleteRow_ClearsColumnValues()
    {
        var table = new DetTable("chars");
        var jobCol = table.CreateByteColumn("job");
        var hpCol = table.CreateIntColumn("hp");
        var nameCol = table.CreateStringColumn("name");

        int id = table.CreateRow();
        jobCol.Set(id, 3);
        hpCol.Set(id, 120);
        nameCol.Set(id, "Somchai");

        table.DeleteRow(id);

        Assert.Equal(0, jobCol.Get(id));
        Assert.Equal(0, hpCol.Get(id));
        Assert.Null(nameCol.Get(id));
    }

    [Fact]
    public void RecycledRow_StartsCleanWithoutLeakingOldValues()
    {
        var table = new DetTable("chars");
        var jobCol = table.CreateByteColumn("job");
        var hpCol = table.CreateIntColumn("hp");
        var nameCol = table.CreateStringColumn("name");

        int id = table.CreateRow();
        jobCol.Set(id, 7);
        hpCol.Set(id, 250);
        nameCol.Set(id, "Old Name");

        table.DeleteRow(id);
        int recycled = table.CreateRow();

        Assert.Equal(id, recycled);
        Assert.Equal(0, jobCol.Get(recycled));
        Assert.Equal(0, hpCol.Get(recycled));
        Assert.Null(nameCol.Get(recycled));
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

    [Fact]
    public void ByteIndex_TracksAliveRowsByColumnValue()
    {
        var table = new DetTable("units");
        var role = table.CreateByteColumn("role");
        var unitsByRole = table.CreateByteIndex("unitsByRole", role);

        int worker = table.CreateRow();
        int builder = table.CreateRow();

        role.Set(worker, 1);
        role.Set(builder, 2);

        Assert.Equal(new[] { worker }, unitsByRole.GetRowIds(1).ToArray());
        Assert.Equal(new[] { builder }, unitsByRole.GetRowIds(2).ToArray());

        role.Set(worker, 2);

        Assert.Empty(unitsByRole.GetRowIds(1));
        Assert.Equal(new[] { worker, builder }, unitsByRole.GetRowIds(2).ToArray());
    }

    [Fact]
    public void DeleteRow_RemovesRowFromColumnIndex()
    {
        var table = new DetTable("units");
        var role = table.CreateByteColumn("role");
        var unitsByRole = table.CreateByteIndex("unitsByRole", role);

        int rowId = table.CreateRow();
        role.Set(rowId, 3);

        Assert.True(unitsByRole.Contains(3, rowId));

        table.DeleteRow(rowId);

        Assert.False(unitsByRole.Contains(3, rowId));
        Assert.Equal(0, unitsByRole.Count(3));
    }

    [Fact]
    public void CreateColumnIndex_AfterRowsExist_IndexesCurrentAliveRows()
    {
        var table = new DetTable("units");
        var job = table.CreateIntColumn("job");

        int a = table.CreateRow();
        int b = table.CreateRow();
        int c = table.CreateRow();
        job.Set(a, 10);
        job.Set(b, 20);
        job.Set(c, 10);
        table.DeleteRow(b);

        var unitsByJob = table.CreateIntIndex("unitsByJob", job);

        Assert.Equal(new[] { a, c }, unitsByJob.GetRowIds(10).ToArray());
        Assert.Empty(unitsByJob.GetRowIds(20));
    }

    [Fact]
    public void RecycledRow_RejoinsColumnIndexWithCleanDefaultValue()
    {
        var table = new DetTable("units");
        var role = table.CreateByteColumn("role");
        var unitsByRole = table.CreateByteIndex("unitsByRole", role);

        int rowId = table.CreateRow();
        role.Set(rowId, 9);
        table.DeleteRow(rowId);

        int recycled = table.CreateRow();

        Assert.Equal(rowId, recycled);
        Assert.False(unitsByRole.Contains(9, recycled));
        Assert.True(unitsByRole.Contains(0, recycled));

        role.Set(recycled, 4);

        Assert.False(unitsByRole.Contains(0, recycled));
        Assert.True(unitsByRole.Contains(4, recycled));
    }

    [Fact]
    public void GetSchema_IncludesDerivedColumnMetadata()
    {
        var table = new DetTable("spatialDefinitions");
        table.CreateStringColumn("layoutText");
        table.CreateStringColumn("layoutPreview", DetColumnOptions.Derived("layoutText"));

        DetTableSchema schema = table.GetSchema();

        Assert.Equal(2, schema.Columns.Count);
        Assert.False(schema.Columns[0].IsDerived);
        Assert.True(schema.Columns[1].IsDerived);
        Assert.Equal("layoutText", schema.Columns[1].Source);
        Assert.False(schema.Columns[1].IsEditable);
    }

    [Fact]
    public void GetSchema_IncludesColumnIndexes()
    {
        var table = new DetTable("units");
        var role = table.CreateByteColumn("role");
        var home = table.CreateIntColumn("homeId");

        table.CreateByteIndex("unitsByRole", role);
        table.CreateIntIndex("unitsByHome", home);

        DetTableSchema schema = table.GetSchema();

        Assert.Equal(2, schema.Indexes.Count);
        Assert.Equal("unitsByRole", schema.Indexes[0].Name);
        Assert.Equal(DetColumnKind.Byte, schema.Indexes[0].Kind);
        Assert.Equal("role", schema.Indexes[0].ColumnName);
        Assert.Equal("unitsByHome", schema.Indexes[1].Name);
        Assert.Equal(DetColumnKind.Int, schema.Indexes[1].Kind);
        Assert.Equal("homeId", schema.Indexes[1].ColumnName);
    }
}
