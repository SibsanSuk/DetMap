using DetMap.Layers;
using DetMap.Pathfinding;

namespace DetMap.Tests.Pathfinding;

public class DetPathStoreTests
{
    private static DetPath MakePath(int length, int currentStep = 0)
    {
        var steps = new int[length];
        for (int i = 0; i < length; i++) steps[i] = i * 3;
        return new DetPath { Steps = steps, Length = length, CurrentStep = currentStep };
    }

    [Fact]
    public void Set_Get_ReturnsStoredPath()
    {
        var store = new DetPathStore("paths");
        var path = MakePath(5);
        store.Set(0, path);
        ref DetPath p = ref store.Get(0);
        Assert.Equal(5, p.Length);
        Assert.Equal(0, p.CurrentStep);
    }

    [Fact]
    public void Get_ReturnsRef_AllowsInPlaceModify()
    {
        var store = new DetPathStore("paths");
        store.Set(0, MakePath(5));
        ref DetPath p = ref store.Get(0);
        p.Advance();
        Assert.Equal(1, store.Get(0).CurrentStep);
    }

    [Fact]
    public void Clear_MakesPathInvalid()
    {
        var store = new DetPathStore("paths");
        store.Set(0, MakePath(5));
        store.Clear(0);
        Assert.False(store.Get(0).IsValid);
    }

    [Fact]
    public void DefaultSlot_IsInvalid()
    {
        var store = new DetPathStore("paths");
        Assert.False(store.Get(0).IsValid);
    }

    [Fact]
    public void Set_EntityIdBeyondCapacity_GrowsCorrectly()
    {
        var store = new DetPathStore("paths", capacity: 4);
        store.Set(10, MakePath(3));
        Assert.Equal(3, store.Get(10).Length);
    }

    [Fact]
    public void Set_MultipleEntities_IndependentPaths()
    {
        var store = new DetPathStore("paths");
        store.Set(0, MakePath(3));
        store.Set(1, MakePath(7, currentStep: 2));
        Assert.Equal(3, store.Get(0).Length);
        Assert.Equal(7, store.Get(1).Length);
        Assert.Equal(2, store.Get(1).CurrentStep);
    }

    // ── Serialization round-trip ──────────────────────────────────────────────

    [Fact]
    public void RoundTrip_ValidPath_Preserved()
    {
        var store = new DetPathStore("paths");
        store.Set(0, MakePath(5, currentStep: 2));

        using var ms = new MemoryStream();
        store.WriteToStream(new BinaryWriter(ms));
        ms.Position = 0;

        var store2 = new DetPathStore("paths");
        store2.ReadFromStream(new BinaryReader(ms));

        ref DetPath p = ref store2.Get(0);
        Assert.True(p.IsValid);
        Assert.Equal(5, p.Length);
        Assert.Equal(2, p.CurrentStep);
        Assert.Equal(0,  p.Steps![0]);
        Assert.Equal(6,  p.Steps![2]);
    }

    [Fact]
    public void RoundTrip_InvalidPath_RemainsInvalid()
    {
        var store = new DetPathStore("paths", capacity: 4);
        // leave slot 0 as default (invalid)

        using var ms = new MemoryStream();
        store.WriteToStream(new BinaryWriter(ms));
        ms.Position = 0;

        var store2 = new DetPathStore("paths");
        store2.ReadFromStream(new BinaryReader(ms));

        Assert.False(store2.Get(0).IsValid);
    }

    [Fact]
    public void RoundTrip_MixedSlots_AllCorrect()
    {
        var store = new DetPathStore("paths", capacity: 4);
        store.Set(1, MakePath(4, currentStep: 1));
        store.Set(3, MakePath(2, currentStep: 0));

        using var ms = new MemoryStream();
        store.WriteToStream(new BinaryWriter(ms));
        ms.Position = 0;

        var store2 = new DetPathStore("paths");
        store2.ReadFromStream(new BinaryReader(ms));

        Assert.False(store2.Get(0).IsValid);
        Assert.Equal(4, store2.Get(1).Length);
        Assert.Equal(1, store2.Get(1).CurrentStep);
        Assert.False(store2.Get(2).IsValid);
        Assert.Equal(2, store2.Get(3).Length);
    }

    // ── Integration with DetPathfinder ───────────────────────────────────────

    [Fact]
    public void PathfinderResult_StoreAndAdvance_WorksCorrectly()
    {
        var walkable = new DetBitLayer("walkable", 16, 16);
        walkable.SetAll(true);

        var pf    = new DetPathfinder(16, 16);
        var path  = pf.FindPath(0, 0, 5, 5, walkable);
        var store = new DetPathStore("paths");

        store.Set(0, path);
        ref DetPath p = ref store.Get(0);

        Assert.True(p.IsValid);
        p.Advance();
        var (x, y) = p.Current(16);
        Assert.True(x >= 0 && y >= 0);
    }
}
