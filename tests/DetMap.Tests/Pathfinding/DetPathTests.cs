using DetMap.Pathfinding;

namespace DetMap.Tests.Pathfinding;

public class DetPathTests
{
    [Fact]
    public void Default_IsInvalid()
    {
        DetPath path = default;
        Assert.False(path.IsValid);
    }

    [Fact]
    public void Default_IsComplete()
    {
        DetPath path = default;
        Assert.True(path.IsComplete);
    }

    [Fact]
    public void IsComplete_AtLastStep_ReturnsTrue()
    {
        var path = new DetPath
        {
            Steps = new[] { 0, 1, 2 },
            Length = 3,
            CurrentStep = 2
        };
        Assert.True(path.IsComplete);
    }

    [Fact]
    public void Advance_StopsAtLastStep()
    {
        var path = new DetPath
        {
            Steps = new[] { 0, 1 },
            Length = 2,
            CurrentStep = 0
        };
        path.Advance();
        Assert.Equal(1, path.CurrentStep);
        path.Advance(); // already at last step — should not move further
        Assert.Equal(1, path.CurrentStep);
    }

    [Fact]
    public void Peek_ReturnsNextCell()
    {
        // Grid width = 10, steps = cell index
        // cell 0 = (0,0), cell 1 = (1,0), cell 2 = (2,0)
        var path = new DetPath
        {
            Steps = new[] { 0, 1, 2 },
            Length = 3,
            CurrentStep = 0
        };
        var (px, py) = path.Peek(10);
        Assert.Equal(1, px);
        Assert.Equal(0, py);
    }

    [Fact]
    public void Peek_AtLastStep_ReturnsMinusOne()
    {
        var path = new DetPath
        {
            Steps = new[] { 0, 1 },
            Length = 2,
            CurrentStep = 1 // last step
        };
        var (px, py) = path.Peek(10);
        Assert.Equal(-1, px);
        Assert.Equal(-1, py);
    }

    [Fact]
    public void Current_InvalidPath_ReturnsMinusOne()
    {
        DetPath path = default;
        var (x, y) = path.Current(10);
        Assert.Equal(-1, x);
        Assert.Equal(-1, y);
    }

    [Fact]
    public void Current_CellIndex_DecodesCorrectly()
    {
        // cell index = y * width + x, width = 10
        // index 23 = (3, 2)
        var path = new DetPath
        {
            Steps = new[] { 23 },
            Length = 1,
            CurrentStep = 0
        };
        var (x, y) = path.Current(10);
        Assert.Equal(3, x);
        Assert.Equal(2, y);
    }
}
