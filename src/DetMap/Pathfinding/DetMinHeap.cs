using DetMath;

namespace DetMap.Pathfinding;

/// <summary>
/// Min-heap ordered by (f-cost, cell-index) for deterministic A* tie-breaking.
/// </summary>
public sealed class DetMinHeap
{
    private (Fix64 f, int cell)[] _heap;
    private int _count;

    public DetMinHeap(int capacity)
    {
        _heap = new (Fix64, int)[capacity];
    }

    public int Count => _count;

    public void Push(Fix64 f, int cell)
    {
        if (_count == _heap.Length)
            Array.Resize(ref _heap, _heap.Length * 2);
        _heap[_count] = (f, cell);
        SiftUp(_count++);
    }

    public (Fix64 f, int cell) Pop()
    {
        var min = _heap[0];
        _heap[0] = _heap[--_count];
        SiftDown(0);
        return min;
    }

    public void Clear() => _count = 0;

    private void SiftUp(int i)
    {
        while (i > 0)
        {
            int parent = (i - 1) >> 1;
            if (Compare(i, parent) < 0) { Swap(i, parent); i = parent; }
            else break;
        }
    }

    private void SiftDown(int i)
    {
        while (true)
        {
            int left = (i << 1) + 1, right = left + 1, smallest = i;
            if (left < _count && Compare(left, smallest) < 0) smallest = left;
            if (right < _count && Compare(right, smallest) < 0) smallest = right;
            if (smallest == i) break;
            Swap(i, smallest);
            i = smallest;
        }
    }

    private int Compare(int a, int b)
    {
        long fa = _heap[a].f.RawValue;
        long fb = _heap[b].f.RawValue;
        int fc = fa < fb ? -1 : (fa > fb ? 1 : 0);
        return fc != 0 ? fc : _heap[a].cell.CompareTo(_heap[b].cell);
    }

    private void Swap(int a, int b) => (_heap[a], _heap[b]) = (_heap[b], _heap[a]);
}
