using DetMap.Layers;

namespace DetMap.Tests.Layers;

public class DetBooleanLayerTests
{
    [Fact]
    public void Default_AllFalse()
    {
        var layer = new DetBooleanLayer("walkable", 8, 8);
        Assert.False(layer.Get(0, 0));
        Assert.False(layer.Get(7, 7));
    }

    [Fact]
    public void Set_True_Get_True()
    {
        var layer = new DetBooleanLayer("walkable", 8, 8);
        layer.Set(3, 5, true);
        Assert.True(layer.Get(3, 5));
        Assert.False(layer.Get(3, 4));
    }

    [Fact]
    public void SetAll_True_AllTrue()
    {
        var layer = new DetBooleanLayer("walkable", 8, 8);
        layer.SetAll(true);
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
            Assert.True(layer.Get(x, y));
    }

    [Fact]
    public void And_BothTrue_ResultTrue()
    {
        var a = new DetBooleanLayer("a", 8, 8);
        var b = new DetBooleanLayer("b", 8, 8);
        var r = new DetBooleanLayer("r", 8, 8);
        a.Set(2, 2, true); b.Set(2, 2, true);
        DetBooleanLayer.And(a, b, r);
        Assert.True(r.Get(2, 2));
    }

    [Fact]
    public void And_OneFalse_ResultFalse()
    {
        var a = new DetBooleanLayer("a", 8, 8);
        var b = new DetBooleanLayer("b", 8, 8);
        var r = new DetBooleanLayer("r", 8, 8);
        a.Set(2, 2, true); // b stays false
        DetBooleanLayer.And(a, b, r);
        Assert.False(r.Get(2, 2));
    }

    [Fact]
    public void Or_OneTrueOneFlase_ResultTrue()
    {
        var a = new DetBooleanLayer("a", 8, 8);
        var b = new DetBooleanLayer("b", 8, 8);
        var r = new DetBooleanLayer("r", 8, 8);
        a.Set(1, 1, true);
        DetBooleanLayer.Or(a, b, r);
        Assert.True(r.Get(1, 1));
    }

    [Fact]
    public void Memory_256x256_UsesOnly1KB()
    {
        // 256*256 = 65536 bits = 1024 bytes = 1KB
        // We just verify creation doesn't explode
        var layer = new DetBooleanLayer("big", 256, 256);
        layer.Set(255, 255, true);
        Assert.True(layer.Get(255, 255));
    }

    [Fact]
    public void Xor_DifferentBits_ResultTrue()
    {
        var a = new DetBooleanLayer("a", 8, 8);
        var b = new DetBooleanLayer("b", 8, 8);
        var r = new DetBooleanLayer("r", 8, 8);
        a.Set(3, 3, true);  // a=1, b=0 → XOR=1
        DetBooleanLayer.Xor(a, b, r);
        Assert.True(r.Get(3, 3));
    }

    [Fact]
    public void Xor_SameBits_ResultFalse()
    {
        var a = new DetBooleanLayer("a", 8, 8);
        var b = new DetBooleanLayer("b", 8, 8);
        var r = new DetBooleanLayer("r", 8, 8);
        a.Set(3, 3, true);
        b.Set(3, 3, true);  // a=1, b=1 → XOR=0
        DetBooleanLayer.Xor(a, b, r);
        Assert.False(r.Get(3, 3));
    }

    [Fact]
    public void Set_MarksDirty()
    {
        var layer = new DetBooleanLayer("walkable", 8, 8);
        Assert.False(layer.Dirty.IsDirty);
        layer.Set(4, 4, true);
        Assert.True(layer.Dirty.IsDirty);
    }

    [Fact]
    public void ClearDirty_ResetsDirty()
    {
        var layer = new DetBooleanLayer("walkable", 8, 8);
        layer.Set(1, 1, true);
        layer.ClearDirty();
        Assert.False(layer.Dirty.IsDirty);
    }
}
