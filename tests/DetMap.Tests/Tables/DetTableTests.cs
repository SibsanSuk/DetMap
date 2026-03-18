using DetMap.Tables;

namespace DetMap.Tests.Tables;

public class DetTableTests
{
    [Fact]
    public void Spawn_ReturnsSequentialIds()
    {
        var table = new DetTable("chars");
        int id0 = table.Spawn();
        int id1 = table.Spawn();
        Assert.Equal(0, id0);
        Assert.Equal(1, id1);
    }

    [Fact]
    public void Despawn_RecyclesId()
    {
        var table = new DetTable("chars");
        int id = table.Spawn();
        table.Despawn(id);
        int recycled = table.Spawn();
        Assert.Equal(id, recycled);
    }

    [Fact]
    public void IsAlive_AfterSpawn_IsTrue()
    {
        var table = new DetTable("chars");
        int id = table.Spawn();
        Assert.True(table.IsAlive(id));
    }

    [Fact]
    public void IsAlive_AfterDespawn_IsFalse()
    {
        var table = new DetTable("chars");
        int id = table.Spawn();
        table.Despawn(id);
        Assert.False(table.IsAlive(id));
    }

    [Fact]
    public void Column_SetGet_RoundTrips()
    {
        var table = new DetTable("chars");
        var jobCol = table.AddCol<byte>("job");
        int id = table.Spawn();
        jobCol.Set(id, 3);
        Assert.Equal(3, jobCol.Get(id));
    }

    [Fact]
    public void StringColumn_SetGet_RoundTrips()
    {
        var table = new DetTable("chars");
        var nameCol = table.AddStringCol("name");
        int id = table.Spawn();
        nameCol.Set(id, "Somchai");
        Assert.Equal("Somchai", nameCol.Get(id));
    }

    [Fact]
    public void GetAlive_IteratesInDeterministicOrder()
    {
        var table = new DetTable("chars");
        int a = table.Spawn();
        int b = table.Spawn();
        int c = table.Spawn();
        table.Despawn(b);

        var alive = table.GetAlive().ToList();
        Assert.Equal(new[] { a, c }, alive);
    }

    [Fact]
    public void FreeList_LIFO_EnsuresDeterministicRecycle()
    {
        var table = new DetTable("chars");
        int a = table.Spawn();
        int b = table.Spawn();
        table.Despawn(a);
        table.Despawn(b);
        // LIFO: b despawned last → b recycled first
        Assert.Equal(b, table.Spawn());
        Assert.Equal(a, table.Spawn());
    }

    [Fact]
    public void GetCol_RetrievesColumnByName()
    {
        var table = new DetTable("chars");
        var added = table.AddCol<byte>("job");
        var retrieved = table.GetCol<byte>("job");
        int id = table.Spawn();
        retrieved.Set(id, 7);
        Assert.Equal(7, added.Get(id));
    }

    [Fact]
    public void GetStringCol_RetrievesColumnByName()
    {
        var table = new DetTable("chars");
        var added = table.AddStringCol("name");
        var retrieved = table.GetStringCol("name");
        int id = table.Spawn();
        retrieved.Set(id, "Test");
        Assert.Equal("Test", added.Get(id));
    }

    [Fact]
    public void HighWater_TracksMaxIdAllocated()
    {
        var table = new DetTable("chars");
        table.Spawn();
        table.Spawn();
        table.Spawn();
        Assert.Equal(3, table.HighWater);
    }

    [Fact]
    public void HighWater_DoesNotDecreaseOnDespawn()
    {
        var table = new DetTable("chars");
        int id = table.Spawn();
        table.Despawn(id);
        Assert.Equal(1, table.HighWater);
    }

    [Fact]
    public void Name_ReturnsTableName()
    {
        var table = new DetTable("characters");
        Assert.Equal("characters", table.Name);
    }
}
