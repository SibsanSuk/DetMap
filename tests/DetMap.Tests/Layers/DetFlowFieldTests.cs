using DetMath;
using DetMap.Layers;

namespace DetMap.Tests.Layers;

public class DetFlowFieldTests
{
    [Fact]
    public void Default_AllDirectionsAreBlocked()
    {
        var ff = new DetFlowField("flow", 8, 8);
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
            Assert.Equal(DetFlowField.Blocked, ff.Get(x, y));
    }

    [Fact]
    public void Set_Get_Direction_RoundTrips()
    {
        var ff = new DetFlowField("flow", 8, 8);
        ff.Set(3, 4, 1, Fix64.FromInt(5)); // direction East, cost 5
        Assert.Equal(1, ff.Get(3, 4));
    }

    [Fact]
    public void Set_Get_Cost_RoundTrips()
    {
        var ff = new DetFlowField("flow", 8, 8);
        var cost = Fix64.FromRatio(7, 2); // 3.50
        ff.Set(2, 2, 0, cost);
        Assert.Equal(cost, ff.GetCost(2, 2));
    }

    [Fact]
    public void Set_MarksDirty()
    {
        var ff = new DetFlowField("flow", 8, 8);
        Assert.False(ff.Dirty.IsDirty);
        ff.Set(1, 1, 0, Fix64.One);
        Assert.True(ff.Dirty.IsDirty);
    }

    [Fact]
    public void ClearDirty_ResetsDirty()
    {
        var ff = new DetFlowField("flow", 8, 8);
        ff.Set(1, 1, 0, Fix64.One);
        ff.ClearDirty();
        Assert.False(ff.Dirty.IsDirty);
    }

    [Fact]
    public void Reset_RestoresAllToBlocked()
    {
        var ff = new DetFlowField("flow", 8, 8);
        ff.Set(0, 0, 1, Fix64.FromInt(10));
        ff.Set(3, 3, 2, Fix64.FromInt(5));
        ff.Reset();
        Assert.Equal(DetFlowField.Blocked, ff.Get(0, 0));
        Assert.Equal(DetFlowField.Blocked, ff.Get(3, 3));
    }

    [Fact]
    public void AllEightDirections_StoredCorrectly()
    {
        var ff = new DetFlowField("flow", 10, 10);
        for (byte dir = 0; dir < 8; dir++)
        {
            ff.Set(dir, 0, dir, Fix64.FromInt(dir));
            Assert.Equal(dir, ff.Get(dir, 0));
        }
    }

    [Fact]
    public void BlockedConstant_Is255()
    {
        Assert.Equal(255, DetFlowField.Blocked);
    }

    [Fact]
    public void Set_Overwrite_UpdatesDirection()
    {
        var ff = new DetFlowField("flow", 8, 8);
        ff.Set(5, 5, 0, Fix64.One);   // North
        ff.Set(5, 5, 2, Fix64.One);   // South
        Assert.Equal(2, ff.Get(5, 5)); // last write wins
    }
}
