using DetMap.Layers;

namespace DetMap.Tests.Layers;

public class DetEntityMapTests
{
    [Fact]
    public void Add_Entity_CountIsOne()
    {
        var map = new DetEntityMap("units", 16, 16);
        map.Add(0, 5, 5);
        Assert.Equal(1, map.CountAt(5, 5));
    }

    [Fact]
    public void Add_MultipleEntities_SameCell_CountCorrect()
    {
        var map = new DetEntityMap("units", 16, 16);
        map.Add(0, 3, 3);
        map.Add(1, 3, 3);
        map.Add(2, 3, 3);
        Assert.Equal(3, map.CountAt(3, 3));
    }

    [Fact]
    public void Remove_Entity_CountDecreases()
    {
        var map = new DetEntityMap("units", 16, 16);
        map.Add(0, 2, 2);
        map.Add(1, 2, 2);
        map.Remove(0);
        Assert.Equal(1, map.CountAt(2, 2));
    }

    [Fact]
    public void Move_Entity_UpdatesCount()
    {
        var map = new DetEntityMap("units", 16, 16);
        map.Add(0, 0, 0);
        map.Move(0, 5, 5);
        Assert.Equal(0, map.CountAt(0, 0));
        Assert.Equal(1, map.CountAt(5, 5));
    }

    [Fact]
    public void GetEntitiesAt_ReturnsCorrectIds()
    {
        var map = new DetEntityMap("units", 16, 16);
        map.Add(10, 4, 4);
        map.Add(20, 4, 4);

        var found = new List<int>();
        foreach (int id in map.GetEntitiesAt(4, 4))
            found.Add(id);

        Assert.Contains(10, found);
        Assert.Contains(20, found);
        Assert.Equal(2, found.Count);
    }

    [Fact]
    public void EmptyCell_CountIsZero()
    {
        var map = new DetEntityMap("units", 16, 16);
        Assert.Equal(0, map.CountAt(0, 0));
    }

    [Fact]
    public void Remove_LastEntity_CountIsZero()
    {
        var map = new DetEntityMap("units", 16, 16);
        map.Add(5, 7, 7);
        map.Remove(5);
        Assert.Equal(0, map.CountAt(7, 7));
    }

    // ── EnsureCapacity regression tests ────────────────────────────────────────
    // Bug: after Array.Resize the loop used _cellOf.Length (= newSize already)
    // so new slots were never initialized to -1, corrupting the linked list.

    [Fact]
    public void Add_EntityIdBeyondInitialCapacity_CountIsCorrect()
    {
        // maxEntities=4 so entity ID 4 triggers EnsureCapacity
        var map = new DetEntityMap("units", 16, 16, maxEntities: 4);
        map.Add(0, 0, 0);
        map.Add(1, 1, 1);
        map.Add(2, 2, 2);
        map.Add(3, 3, 3);
        map.Add(4, 4, 4); // triggers growth
        Assert.Equal(1, map.CountAt(4, 4));
    }

    [Fact]
    public void Add_EntityIdBeyondInitialCapacity_LinkedListIntact()
    {
        // After capacity growth, new entity must not be falsely linked to entity 0.
        // If new slots defaulted to 0 instead of -1, GetEntitiesAt would
        // return entity 0 as a ghost next-node of entity 4.
        var map = new DetEntityMap("units", 16, 16, maxEntities: 4);
        map.Add(0, 0, 0); // entity 0 at cell (0,0)
        map.Add(4, 5, 5); // triggers growth — new slot for id=4 must be -1, not 0

        var found = new List<int>();
        foreach (int id in map.GetEntitiesAt(5, 5))
            found.Add(id);

        Assert.Equal(new[] { 4 }, found); // must contain only entity 4, not entity 0
    }

    [Fact]
    public void Move_EntityBeyondInitialCapacity_UpdatesCorrectly()
    {
        var map = new DetEntityMap("units", 16, 16, maxEntities: 4);
        map.Add(4, 2, 2); // triggers growth
        map.Move(4, 8, 8);
        Assert.Equal(0, map.CountAt(2, 2));
        Assert.Equal(1, map.CountAt(8, 8));
    }

    [Fact]
    public void Remove_EntityBeyondInitialCapacity_CountIsZero()
    {
        var map = new DetEntityMap("units", 16, 16, maxEntities: 4);
        map.Add(4, 6, 6); // triggers growth
        map.Remove(4);
        Assert.Equal(0, map.CountAt(6, 6));
    }

    [Fact]
    public void Add_MultipleGrowthCycles_StillCorrect()
    {
        // Grow twice: 4 → 8 → 16
        var map = new DetEntityMap("units", 32, 32, maxEntities: 4);
        for (int i = 0; i < 16; i++)
            map.Add(i, i % 8, i % 8);

        // Each cell (x,x) has 2 entities (i and i+8 share same cell pattern)
        Assert.True(map.CountAt(0, 0) > 0);
        // Total entities added = 16, verify none lost
        int total = 0;
        for (int x = 0; x < 8; x++) total += map.CountAt(x, x);
        Assert.Equal(16, total);
    }
}
