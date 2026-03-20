using DetMath;
using DetMap.Layers;

namespace DetMap.Tests.Layers;

public class DetValueLayerTests
{
    [Fact]
    public void Get_DefaultValue_IsZero()
    {
        var layer = new DetValueLayer<int>("test", 10, 10);
        Assert.Equal(0, layer.Get(5, 5));
    }

    [Fact]
    public void Set_Get_RoundTrips()
    {
        var layer = new DetValueLayer<int>("test", 10, 10);
        layer.Set(3, 4, 42);
        Assert.Equal(42, layer.Get(3, 4));
    }

    [Fact]
    public void Set_MarksDirty()
    {
        var layer = new DetValueLayer<int>("test", 10, 10);
        Assert.False(layer.Dirty.IsDirty);
        layer.Set(2, 3, 99);
        Assert.True(layer.Dirty.IsDirty);
        Assert.Equal(2, layer.Dirty.MinX);
        Assert.Equal(3, layer.Dirty.MinY);
    }

    [Fact]
    public void ClearDirty_ResetsDirty()
    {
        var layer = new DetValueLayer<int>("test", 10, 10);
        layer.Set(1, 1, 1);
        layer.ClearDirty();
        Assert.False(layer.Dirty.IsDirty);
    }

    [Fact]
    public void Fix64Layer_SetGet_Deterministic()
    {
        var layer = new DetValueLayer<Fix64>("height", 16, 16);
        var val = Fix64.FromInt(7);
        layer.Set(0, 0, val);
        Assert.Equal(val, layer.Get(0, 0));
    }

    [Fact]
    public void Fill_SetsAllCells()
    {
        var layer = new DetValueLayer<byte>("flags", 4, 4);
        layer.Fill(1);
        for (int y = 0; y < 4; y++)
        for (int x = 0; x < 4; x++)
            Assert.Equal(1, layer.Get(x, y));
    }
}
