using DetMap.Layers;

namespace DetMap.Tests.Layers;

public class DetCellIndexTests
{
    [Fact]
    public void Place_Row_CountIsOne()
    {
        var map = new DetCellIndex("units", 16, 16);
        map.Place(0, 5, 5);
        Assert.Equal(1, map.CountAt(5, 5));
    }

    [Fact]
    public void Place_MultipleRows_SameCell_CountCorrect()
    {
        var map = new DetCellIndex("units", 16, 16);
        map.Place(0, 3, 3);
        map.Place(1, 3, 3);
        map.Place(2, 3, 3);
        Assert.Equal(3, map.CountAt(3, 3));
    }

    [Fact]
    public void Remove_Row_CountDecreases()
    {
        var map = new DetCellIndex("units", 16, 16);
        map.Place(0, 2, 2);
        map.Place(1, 2, 2);
        map.Remove(0);
        Assert.Equal(1, map.CountAt(2, 2));
    }

    [Fact]
    public void MoveTo_Row_UpdatesCount()
    {
        var map = new DetCellIndex("units", 16, 16);
        map.Place(0, 0, 0);
        map.MoveTo(0, 5, 5);
        Assert.Equal(0, map.CountAt(0, 0));
        Assert.Equal(1, map.CountAt(5, 5));
    }

    [Fact]
    public void GetRowIdsAt_ReturnsCorrectIds()
    {
        var map = new DetCellIndex("units", 16, 16);
        map.Place(10, 4, 4);
        map.Place(20, 4, 4);

        var found = new List<int>();
        foreach (int id in map.GetRowIdsAt(4, 4))
            found.Add(id);

        Assert.Contains(10, found);
        Assert.Contains(20, found);
        Assert.Equal(2, found.Count);
    }

    [Fact]
    public void EmptyCell_CountIsZero()
    {
        var map = new DetCellIndex("units", 16, 16);
        Assert.Equal(0, map.CountAt(0, 0));
    }

    [Fact]
    public void Remove_LastRow_CountIsZero()
    {
        var map = new DetCellIndex("units", 16, 16);
        map.Place(5, 7, 7);
        map.Remove(5);
        Assert.Equal(0, map.CountAt(7, 7));
    }

    // ── EnsureCapacity regression tests ────────────────────────────────────────
    // Bug: after Array.Resize the loop used _cellOf.Length (= newSize already)
    // so new slots were never initialized to -1, corrupting the linked list.

    [Fact]
    public void Place_RowIdBeyondInitialCapacity_CountIsCorrect()
    {
        // maxRows=4 so row ID 4 triggers EnsureCapacity
        var map = new DetCellIndex("units", 16, 16, maxRows: 4);
        map.Place(0, 0, 0);
        map.Place(1, 1, 1);
        map.Place(2, 2, 2);
        map.Place(3, 3, 3);
        map.Place(4, 4, 4); // triggers growth
        Assert.Equal(1, map.CountAt(4, 4));
    }

    [Fact]
    public void Place_RowIdBeyondInitialCapacity_LinkedListIntact()
    {
        // After capacity growth, a new row must not be falsely linked to row 0.
        // If new slots defaulted to 0 instead of -1, GetRowIdsAt would
        // return row 0 as a ghost next-node of row 4.
        var map = new DetCellIndex("units", 16, 16, maxRows: 4);
        map.Place(0, 0, 0); // row 0 at cell (0,0)
        map.Place(4, 5, 5); // triggers growth — new slot for id=4 must be -1, not 0

        var found = new List<int>();
        foreach (int id in map.GetRowIdsAt(5, 5))
            found.Add(id);

        Assert.Equal(new[] { 4 }, found); // must contain only row 4, not row 0
    }

    [Fact]
    public void MoveTo_RowBeyondInitialCapacity_UpdatesCorrectly()
    {
        var map = new DetCellIndex("units", 16, 16, maxRows: 4);
        map.Place(4, 2, 2); // triggers growth
        map.MoveTo(4, 8, 8);
        Assert.Equal(0, map.CountAt(2, 2));
        Assert.Equal(1, map.CountAt(8, 8));
    }

    [Fact]
    public void Remove_RowBeyondInitialCapacity_CountIsZero()
    {
        var map = new DetCellIndex("units", 16, 16, maxRows: 4);
        map.Place(4, 6, 6); // triggers growth
        map.Remove(4);
        Assert.Equal(0, map.CountAt(6, 6));
    }

    [Fact]
    public void Place_MultipleGrowthCycles_StillCorrect()
    {
        // Grow twice: 4 → 8 → 16
        var map = new DetCellIndex("units", 32, 32, maxRows: 4);
        for (int i = 0; i < 16; i++)
            map.Place(i, i % 8, i % 8);

        // Each cell (x,x) has 2 rows (i and i+8 share the same cell pattern)
        Assert.True(map.CountAt(0, 0) > 0);
        // Total rows added = 16, verify none lost
        int total = 0;
        for (int x = 0; x < 8; x++) total += map.CountAt(x, x);
        Assert.Equal(16, total);
    }
}
