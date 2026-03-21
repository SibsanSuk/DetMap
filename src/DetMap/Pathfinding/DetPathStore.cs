namespace DetMap.Pathfinding;

/// <summary>
/// Stores one <see cref="DetPath"/> per row ID — a named, DB-level structure
/// that is serialized by <c>DetSnapshot</c> alongside layers and tables.
/// </summary>
public sealed class DetPathStore
{
    private DetPath[] _paths;

    public string Name { get; }

    public DetPathStore(string name, int capacity = 256)
    {
        Name = name;
        _paths = new DetPath[capacity];
    }

    public void Set(int rowId, DetPath path)
    {
        EnsureCapacity(rowId);
        _paths[rowId] = path;
    }

    /// <summary>Returns a ref to the stored path — modify in-place without copying.</summary>
    public ref DetPath Get(int rowId)
    {
        EnsureCapacity(rowId);
        return ref _paths[rowId];
    }

    public void Clear(int rowId)
    {
        if (rowId < _paths.Length)
            _paths[rowId] = default;
    }

    private void EnsureCapacity(int rowId)
    {
        if (rowId >= _paths.Length)
            Array.Resize(ref _paths, Math.Max(rowId + 1, _paths.Length * 2));
    }

    // ── Serialization ────────────────────────────────────────────────────────
    // Format per store:
    //   [4] slot count
    //   per slot: [4] length
    //             if length > 0: [4] currentStep  [length × 4] steps

    public void WriteToStream(BinaryWriter bw)
    {
        bw.Write(_paths.Length);
        foreach (var p in _paths)
        {
            bw.Write(p.Length);
            if (p.Length > 0)
            {
                bw.Write(p.CurrentStep);
                foreach (var step in p.Steps!) bw.Write(step);
            }
        }
    }

    public void ReadFromStream(BinaryReader br)
    {
        int len = br.ReadInt32();
        _paths = new DetPath[len];
        for (int i = 0; i < len; i++)
        {
            int pathLen = br.ReadInt32();
            if (pathLen > 0)
            {
                int currentStep = br.ReadInt32();
                var steps = new int[pathLen];
                for (int j = 0; j < pathLen; j++) steps[j] = br.ReadInt32();
                _paths[i] = new DetPath { Steps = steps, Length = pathLen, CurrentStep = currentStep };
            }
            // length == 0 → leave as default (invalid path)
        }
    }
}
