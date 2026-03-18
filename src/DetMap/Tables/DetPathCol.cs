using DetMap.Pathfinding;

namespace DetMap.Tables;

public sealed class DetPathCol
{
    private DetPath[] _data;

    public DetPathCol(int capacity) => _data = new DetPath[capacity];

    public ref DetPath Get(int id)
    {
        EnsureCapacity(id);
        return ref _data[id];
    }

    public void Set(int id, DetPath path)
    {
        EnsureCapacity(id);
        _data[id] = path;
    }

    public void Clear(int id)
    {
        EnsureCapacity(id);
        _data[id] = default;
    }

    private void EnsureCapacity(int id)
    {
        if (id >= _data.Length)
            Array.Resize(ref _data, Math.Max(id + 1, _data.Length * 2));
    }
}
