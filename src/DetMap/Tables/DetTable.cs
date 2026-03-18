using DetMap.Core;

namespace DetMap.Tables;

public sealed class DetTable
{
    private readonly DetCol<byte> _alive;
    private readonly Stack<int> _freeList = new();
    private int _highWater;
    private readonly Dictionary<string, IDetColData> _cols = new();
    private readonly List<string> _colOrder = new();

    public string Name { get; }

    public DetTable(string name, int capacity = 256)
    {
        Name = name;
        _alive = new DetCol<byte>(capacity);
    }

    public int Spawn()
    {
        int id = _freeList.Count > 0 ? _freeList.Pop() : _highWater++;
        _alive.Set(id, 1);
        return id;
    }

    public void Despawn(int id)
    {
        _alive.Set(id, 0);
        _freeList.Push(id);
    }

    public bool IsAlive(int id) => _alive.Get(id) == 1;

    /// <param name="type">Use <see cref="DetType.Byte"/>, <see cref="DetType.Int"/>, or <see cref="DetType.Fix64"/>.</param>
    public DetCol<T> CreateCol<T>(string name, DetType<T> type) where T : unmanaged
    {
        var col = new DetCol<T>(Math.Max(_highWater, 64));
        _cols[name] = col;
        _colOrder.Add(name);
        return col;
    }

    public DetStringCol CreateStringCol(string name)
    {
        var col = new DetStringCol(Math.Max(_highWater, 64));
        _cols[name] = col;
        _colOrder.Add(name);
        return col;
    }

    public DetCol<T>    GetCol<T>(string name)      where T : unmanaged => (DetCol<T>)_cols[name];
    public DetStringCol GetStringCol(string name)   => (DetStringCol)_cols[name];

    /// <summary>Iterate alive entities in deterministic order (0..highWater).</summary>
    public IEnumerable<int> GetAlive()
    {
        for (int i = 0; i < _highWater; i++)
            if (_alive.Get(i) == 1) yield return i;
    }

    public int HighWater => _highWater;

    /// <summary>Column names in insertion order — used for deterministic serialization.</summary>
    public IReadOnlyList<string> ColOrder => _colOrder;

    /// <summary>Access a column by name as <see cref="IDetColData"/> for schema-driven serialization.</summary>
    public IDetColData GetColData(string name) => _cols[name];

    public void WriteDataToStream(BinaryWriter bw)
    {
        bw.Write(_highWater);

        // Preserve stack order: ToArray() returns [top, ..., bottom]
        var free = _freeList.ToArray();
        bw.Write(free.Length);
        foreach (var f in free) bw.Write(f);

        _alive.WriteToStream(bw);

        foreach (var name in _colOrder)
            _cols[name].WriteToStream(bw);
    }

    public void ReadDataFromStream(BinaryReader br)
    {
        _highWater = br.ReadInt32();

        int freeCount = br.ReadInt32();
        _freeList.Clear();
        // Push in reverse so the restored stack order matches the saved one
        var free = new int[freeCount];
        for (int i = 0; i < freeCount; i++) free[i] = br.ReadInt32();
        for (int i = free.Length - 1; i >= 0; i--) _freeList.Push(free[i]);

        _alive.ReadFromStream(br);

        foreach (var name in _colOrder)
            _cols[name].ReadFromStream(br);
    }
}
