using DetMap.Core;

namespace DetMap.Layers;

public sealed class DetCellIndex : IDetLayer, IDetSpatial
{
    private readonly int _width;
    private readonly int _height;
    private int[] _cellOf;        // rowId -> cell index (-1 = not placed)
    private int[] _next;          // rowId -> next rowId in same cell (-1 = end)
    private readonly Dictionary<int, int> _heads = new(); // cellKey -> head rowId
    private readonly DetValueLayer<byte> _countCache;

    public string Name { get; }
    public DetLayerKind Kind => DetLayerKind.CellIndex;
    public DirtyRect Dirty => _countCache.Dirty;

    public DetCellIndex(string name, int width, int height, int maxRows = 4096)
    {
        Name = name;
        _width = width;
        _height = height;
        _cellOf = new int[maxRows];
        _next = new int[maxRows];
        Array.Fill(_cellOf, -1);
        Array.Fill(_next, -1);
        _countCache = new DetValueLayer<byte>("__" + name + "_count", width, height);
    }

    private int CellKey(int x, int y) => y * _width + x;

    public void Place(int rowId, int x, int y)
    {
        EnsureCapacity(rowId);
        int cell = CellKey(x, y);
        _cellOf[rowId] = cell;
        _next[rowId] = _heads.TryGetValue(cell, out int head) ? head : -1;
        _heads[cell] = rowId;
        byte prev = _countCache.Get(x, y);
        _countCache.Set(x, y, (byte)Math.Min(prev + 1, 255));
    }

    public void Remove(int rowId)
    {
        int cell = _cellOf[rowId];
        if (cell < 0) return;

        int x = cell % _width;
        int y = cell / _width;

        if (_heads.TryGetValue(cell, out int head))
        {
            if (head == rowId)
            {
                if (_next[rowId] < 0) _heads.Remove(cell);
                else _heads[cell] = _next[rowId];
            }
            else
            {
                int prev = head;
                while (_next[prev] != rowId && _next[prev] >= 0)
                    prev = _next[prev];
                if (_next[prev] == rowId)
                    _next[prev] = _next[rowId];
            }
        }

        _cellOf[rowId] = -1;
        _next[rowId] = -1;

        byte current = _countCache.Get(x, y);
        _countCache.Set(x, y, (byte)(current > 0 ? current - 1 : 0));
    }

    public void MoveTo(int rowId, int newX, int newY)
    {
        Remove(rowId);
        Place(rowId, newX, newY);
    }

    public int CountAt(int x, int y) => _countCache.Get(x, y);

    public RowIdEnumerator GetRowIdsAt(int x, int y)
    {
        int cell = CellKey(x, y);
        int head = _heads.TryGetValue(cell, out int h) ? h : -1;
        return new RowIdEnumerator(_next, head);
    }

    private void EnsureCapacity(int rowId)
    {
        if (rowId >= _cellOf.Length)
        {
            int oldSize = _cellOf.Length;
            int newSize = Math.Max(rowId + 1, oldSize * 2);
            Array.Resize(ref _cellOf, newSize);
            Array.Resize(ref _next, newSize);
            for (int i = oldSize; i < newSize; i++) { _cellOf[i] = -1; _next[i] = -1; }
        }
    }

    public void ClearDirty() => _countCache.ClearDirty();

    public void WriteToStream(BinaryWriter bw)
    {
        bw.Write(_heads.Count);
        foreach (var kv in _heads)
        {
            bw.Write(kv.Key);
            bw.Write(kv.Value);
        }
        bw.Write(_cellOf.Length);
        foreach (var v in _cellOf) bw.Write(v);
        foreach (var v in _next) bw.Write(v);
    }

    public void ReadFromStream(BinaryReader br, int cellCount)
    {
        _heads.Clear();
        int headCount = br.ReadInt32();
        for (int i = 0; i < headCount; i++)
            _heads[br.ReadInt32()] = br.ReadInt32();

        int len = br.ReadInt32();
        _cellOf = new int[len];
        _next = new int[len];
        for (int i = 0; i < len; i++) _cellOf[i] = br.ReadInt32();
        for (int i = 0; i < len; i++) _next[i] = br.ReadInt32();

        RebuildCountCache();
    }

    private void RebuildCountCache()
    {
        _countCache.Fill((byte)0);
        for (int id = 0; id < _cellOf.Length; id++)
        {
            int cell = _cellOf[id];
            if (cell < 0) continue;
            int x = cell % _width;
            int y = cell / _width;
            byte prev = _countCache.Get(x, y);
            _countCache.Set(x, y, (byte)Math.Min(prev + 1, 255));
        }
    }
}

public struct RowIdEnumerator
{
    private readonly int[] _next;
    private int _current;
    private bool _started;

    public RowIdEnumerator(int[] next, int head)
    {
        _next = next;
        _current = head;
        _started = false;
    }

    public int Current => _current;

    public bool MoveNext()
    {
        if (!_started) { _started = true; return _current >= 0; }
        _current = _next[_current];
        return _current >= 0;
    }

    public RowIdEnumerator GetEnumerator() => this;
}
