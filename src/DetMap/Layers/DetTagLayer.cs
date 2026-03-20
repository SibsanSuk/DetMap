using DetMap.Core;

namespace DetMap.Layers;

public sealed class DetTagLayer : IDetLayer, IDetSpatial
{
    private readonly int _width;
    private readonly Dictionary<int, List<string>> _cellTags = new();

    public string Name { get; }
    public DetLayerKind Kind => DetLayerKind.Tag;
    public DirtyRect Dirty { get; private set; }

    public DetTagLayer(string name, int width, int height)
    {
        Name = name;
        _width = width;
    }

    private int CellKey(int x, int y) => y * _width + x;

    public void AddTag(int x, int y, string tag)
    {
        int cell = CellKey(x, y);
        if (!_cellTags.TryGetValue(cell, out var list))
        {
            list = new List<string>();
            _cellTags[cell] = list;
        }
        if (!list.Contains(tag)) list.Add(tag);

        var dirty = Dirty;
        dirty.Expand(x, y);
        Dirty = dirty;
    }

    public void RemoveTag(int x, int y, string tag)
    {
        int cell = CellKey(x, y);
        if (_cellTags.TryGetValue(cell, out var list))
        {
            list.Remove(tag);
            if (list.Count == 0) _cellTags.Remove(cell);
        }
    }

    public bool HasTag(int x, int y, string tag)
    {
        int cell = CellKey(x, y);
        return _cellTags.TryGetValue(cell, out var list) && list.Contains(tag);
    }

    public bool HasAllTags(int x, int y, IEnumerable<string> tags)
    {
        int cell = CellKey(x, y);
        if (!_cellTags.TryGetValue(cell, out var list)) return false;
        foreach (var t in tags)
            if (!list.Contains(t)) return false;
        return true;
    }

    public int CountAt(int x, int y)
    {
        int cell = CellKey(x, y);
        return _cellTags.TryGetValue(cell, out var list) ? list.Count : 0;
    }

    public IReadOnlyList<string> GetTags(int x, int y)
    {
        int cell = CellKey(x, y);
        return _cellTags.TryGetValue(cell, out var list) ? list : Array.Empty<string>();
    }

    public void ClearDirty()
    {
        var dirty = Dirty;
        dirty.Clear();
        Dirty = dirty;
    }

    public void WriteToStream(BinaryWriter bw)
    {
        bw.Write(_cellTags.Count);
        foreach (var kv in _cellTags)
        {
            bw.Write(kv.Key);
            bw.Write(kv.Value.Count);
            foreach (var t in kv.Value) bw.Write(t);
        }
    }

    public void ReadFromStream(BinaryReader br, int cellCount)
    {
        _cellTags.Clear();
        int count = br.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            int cell = br.ReadInt32();
            int tagCount = br.ReadInt32();
            var list = new List<string>(tagCount);
            for (int j = 0; j < tagCount; j++) list.Add(br.ReadString());
            _cellTags[cell] = list;
        }
    }
}
