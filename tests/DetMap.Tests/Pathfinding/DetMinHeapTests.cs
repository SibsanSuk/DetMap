using DetMath;
using DetMap.Pathfinding;

namespace DetMap.Tests.Pathfinding;

public class DetMinHeapTests
{
    [Fact]
    public void Push_Pop_ReturnsMinFirst()
    {
        var heap = new DetMinHeap(8);
        heap.Push(Fix64.FromInt(5), 0);
        heap.Push(Fix64.FromInt(2), 1);
        heap.Push(Fix64.FromInt(8), 2);

        var (f, _) = heap.Pop();
        Assert.Equal(Fix64.FromInt(2), f);
    }

    [Fact]
    public void Pop_EmptyAfterAll_CountIsZero()
    {
        var heap = new DetMinHeap(4);
        heap.Push(Fix64.FromInt(3), 0);
        heap.Push(Fix64.FromInt(1), 1);
        heap.Pop();
        heap.Pop();
        Assert.Equal(0, heap.Count);
    }

    [Fact]
    public void Pop_SortedOrder()
    {
        var heap = new DetMinHeap(8);
        heap.Push(Fix64.FromInt(7), 0);
        heap.Push(Fix64.FromInt(1), 1);
        heap.Push(Fix64.FromInt(4), 2);
        heap.Push(Fix64.FromInt(9), 3);
        heap.Push(Fix64.FromInt(2), 4);

        var results = new List<int>();
        while (heap.Count > 0)
            results.Add(heap.Pop().f.ToIntTruncate());

        Assert.Equal(new[] { 1, 2, 4, 7, 9 }, results);
    }

    [Fact]
    public void TieBreaking_SameF_LowerCellIndexFirst()
    {
        // When two nodes have equal f-cost, smaller cell index wins
        var heap = new DetMinHeap(8);
        heap.Push(Fix64.FromInt(10), 5);
        heap.Push(Fix64.FromInt(10), 2);
        heap.Push(Fix64.FromInt(10), 8);

        Assert.Equal(2, heap.Pop().cell);
        Assert.Equal(5, heap.Pop().cell);
        Assert.Equal(8, heap.Pop().cell);
    }

    [Fact]
    public void TieBreaking_Deterministic_SameInputSameOrder()
    {
        var heap1 = new DetMinHeap(8);
        var heap2 = new DetMinHeap(8);

        heap1.Push(Fix64.FromInt(5), 3);
        heap1.Push(Fix64.FromInt(5), 1);
        heap1.Push(Fix64.FromInt(5), 7);

        heap2.Push(Fix64.FromInt(5), 3);
        heap2.Push(Fix64.FromInt(5), 1);
        heap2.Push(Fix64.FromInt(5), 7);

        while (heap1.Count > 0)
            Assert.Equal(heap1.Pop().cell, heap2.Pop().cell);
    }

    [Fact]
    public void Clear_ResetsCount()
    {
        var heap = new DetMinHeap(8);
        heap.Push(Fix64.FromInt(1), 0);
        heap.Push(Fix64.FromInt(2), 1);
        heap.Clear();
        Assert.Equal(0, heap.Count);
    }

    [Fact]
    public void CapacityGrowth_PushBeyondInitial_Works()
    {
        var heap = new DetMinHeap(2); // small initial capacity
        for (int i = 10; i >= 1; i--)
            heap.Push(Fix64.FromInt(i), i);

        Assert.Equal(10, heap.Count);
        Assert.Equal(1, heap.Pop().f.ToIntTruncate()); // min = 1
    }

    [Fact]
    public void Push_SingleElement_PopReturnsThatElement()
    {
        var heap = new DetMinHeap(4);
        heap.Push(Fix64.FromRatio(3, 2), 42);
        var (f, cell) = heap.Pop();
        Assert.Equal(Fix64.FromRatio(3, 2), f);
        Assert.Equal(42, cell);
    }
}
