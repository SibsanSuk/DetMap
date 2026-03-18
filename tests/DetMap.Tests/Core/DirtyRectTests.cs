using DetMap.Core;

namespace DetMap.Tests.Core;

public class DirtyRectTests
{
    [Fact]
    public void Initially_NotDirty()
    {
        var dr = new DirtyRect();
        Assert.False(dr.IsDirty);
    }

    [Fact]
    public void Expand_SinglePoint_SetsDirty()
    {
        var dr = new DirtyRect();
        dr.Expand(5, 3);
        Assert.True(dr.IsDirty);
        Assert.Equal(5, dr.MinX);
        Assert.Equal(5, dr.MaxX);
        Assert.Equal(3, dr.MinY);
        Assert.Equal(3, dr.MaxY);
    }

    [Fact]
    public void Expand_MultiplePoints_GrowsRect()
    {
        var dr = new DirtyRect();
        dr.Expand(2, 2);
        dr.Expand(8, 1);
        dr.Expand(5, 9);
        Assert.Equal(2, dr.MinX);
        Assert.Equal(8, dr.MaxX);
        Assert.Equal(1, dr.MinY);
        Assert.Equal(9, dr.MaxY);
    }

    [Fact]
    public void Clear_ResetsDirtyFlag()
    {
        var dr = new DirtyRect();
        dr.Expand(1, 1);
        dr.Clear();
        Assert.False(dr.IsDirty);
    }

    [Fact]
    public void Contains_PointInside_ReturnsTrue()
    {
        var dr = new DirtyRect();
        dr.Expand(2, 2);
        dr.Expand(8, 8);
        Assert.True(dr.Contains(5, 5));
        Assert.True(dr.Contains(2, 2)); // boundary
        Assert.True(dr.Contains(8, 8)); // boundary
    }

    [Fact]
    public void Contains_PointOutside_ReturnsFalse()
    {
        var dr = new DirtyRect();
        dr.Expand(2, 2);
        dr.Expand(8, 8);
        Assert.False(dr.Contains(1, 5));
        Assert.False(dr.Contains(9, 5));
    }

    [Fact]
    public void Contains_WhenNotDirty_ReturnsFalse()
    {
        var dr = new DirtyRect();
        Assert.False(dr.Contains(0, 0));
    }
}
