using DetMap.Core;
using DetMap.Tables;

namespace DetMap.Tests.Tables;

public class DetTableTests
{
    [Fact]
    public void Spawn_ReturnsSequentialIds()
    {
        var table = new DetTable("chars");
        int id0 = table.Insert();
        int id1 = table.Insert();
        Assert.Equal(0, id0);
        Assert.Equal(1, id1);
    }

    [Fact]
    public void Despawn_RecyclesId()
    {
        var table = new DetTable("chars");
        int id = table.Insert();
        table.Delete(id);
        int recycled = table.Insert();
        Assert.Equal(id, recycled);
    }

    [Fact]
    public void IsAlive_AfterSpawn_IsTrue()
    {
        var table = new DetTable("chars");
        int id = table.Insert();
        Assert.True(table.Exists(id));
    }

    [Fact]
    public void IsAlive_AfterDespawn_IsFalse()
    {
        var table = new DetTable("chars");
        int id = table.Insert();
        table.Delete(id);
        Assert.False(table.Exists(id));
    }

    [Fact]
    public void Column_SetGet_RoundTrips()
    {
        var table = new DetTable("chars");
        var jobCol = table.CreateColumn("job", DetType.Byte);
        int id = table.Insert();
        jobCol.Set(id, 3);
        Assert.Equal(3, jobCol.Get(id));
    }

    [Fact]
    public void StringColumn_SetGet_RoundTrips()
    {
        var table = new DetTable("chars");
        var nameCol = table.CreateStringColumn("name");
        int id = table.Insert();
        nameCol.Set(id, "Somchai");
        Assert.Equal("Somchai", nameCol.Get(id));
    }

    [Fact]
    public void GetAliveIds_IteratesInDeterministicOrder()
    {
        var table = new DetTable("chars");
        int a = table.Insert();
        int b = table.Insert();
        int c = table.Insert();
        table.Delete(b);

        var alive = table.GetAliveIds().ToList();
        Assert.Equal(new[] { a, c }, alive);
    }

    [Fact]
    public void FreeList_LIFO_EnsuresDeterministicRecycle()
    {
        var table = new DetTable("chars");
        int a = table.Insert();
        int b = table.Insert();
        table.Delete(a);
        table.Delete(b);
        // LIFO: b despawned last → b recycled first
        Assert.Equal(b, table.Insert());
        Assert.Equal(a, table.Insert());
    }

    [Fact]
    public void GetColumn_RetrievesColumnByName()
    {
        var table = new DetTable("chars");
        var added = table.CreateColumn("job", DetType.Byte);
        var retrieved = table.GetColumn<byte>("job");
        int id = table.Insert();
        retrieved.Set(id, 7);
        Assert.Equal(7, added.Get(id));
    }

    [Fact]
    public void GetStringColumn_RetrievesColumnByName()
    {
        var table = new DetTable("chars");
        var added = table.CreateStringColumn("name");
        var retrieved = table.GetStringColumn("name");
        int id = table.Insert();
        retrieved.Set(id, "Test");
        Assert.Equal("Test", added.Get(id));
    }

    [Fact]
    public void HighWater_TracksMaxIdAllocated()
    {
        var table = new DetTable("chars");
        table.Insert();
        table.Insert();
        table.Insert();
        Assert.Equal(3, table.HighWater);
    }

    [Fact]
    public void HighWater_DoesNotDecreaseOnDespawn()
    {
        var table = new DetTable("chars");
        int id = table.Insert();
        table.Delete(id);
        Assert.Equal(1, table.HighWater);
    }

    [Fact]
    public void Name_ReturnsTableName()
    {
        var table = new DetTable("characters");
        Assert.Equal("characters", table.Name);
    }
}
