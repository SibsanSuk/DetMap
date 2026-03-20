using DetMap.Core;

namespace DetMap.Tables;

public sealed class DetTable
{
    private readonly DetColumn<byte> _alive;
    private readonly Stack<int> _freeList = new();
    private int _highWater;
    private readonly Dictionary<string, IDetColumnData> _columns = new();
    private readonly List<string> _columnOrder = new();

    public string Name { get; }

    public DetTable(string name, int capacity = 256)
    {
        Name = name;
        _alive = new DetColumn<byte>(capacity);
    }

    public int Insert()
    {
        int id = _freeList.Count > 0 ? _freeList.Pop() : _highWater++;
        _alive.Set(id, 1);
        return id;
    }

    public void Delete(int id)
    {
        _alive.Set(id, 0);
        _freeList.Push(id);
    }

    public bool Exists(int id) => _alive.Get(id) == 1;

    /// <param name="type">Use <see cref="DetType.Byte"/>, <see cref="DetType.Int"/>, or <see cref="DetType.Fix64"/>.</param>
    public DetColumn<T> CreateColumn<T>(string name, DetType<T> type) where T : unmanaged
    {
        var column = new DetColumn<T>(Math.Max(_highWater, 64));
        _columns[name] = column;
        _columnOrder.Add(name);
        return column;
    }

    public DetStringColumn CreateStringColumn(string name)
    {
        var column = new DetStringColumn(Math.Max(_highWater, 64));
        _columns[name] = column;
        _columnOrder.Add(name);
        return column;
    }

    public DetColumn<T> GetColumn<T>(string name) where T : unmanaged => (DetColumn<T>)_columns[name];
    public DetStringColumn GetStringColumn(string name) => (DetStringColumn)_columns[name];

    /// <summary>Iterate alive entities in deterministic order (0..highWater).</summary>
    public IEnumerable<int> GetAliveIds()
    {
        for (int i = 0; i < _highWater; i++)
            if (_alive.Get(i) == 1) yield return i;
    }

    public int HighWater => _highWater;

    /// <summary>Column names in insertion order — used for deterministic serialization.</summary>
    public IReadOnlyList<string> ColumnOrder => _columnOrder;

    /// <summary>Access a column by name as <see cref="IDetColumnData"/> for schema-driven serialization.</summary>
    public IDetColumnData GetColumnData(string name) => _columns[name];

    public void WriteDataToStream(BinaryWriter bw)
    {
        bw.Write(_highWater);

        // Preserve stack order: ToArray() returns [top, ..., bottom]
        var free = _freeList.ToArray();
        bw.Write(free.Length);
        foreach (var f in free) bw.Write(f);

        _alive.WriteToStream(bw);

        foreach (var name in _columnOrder)
            _columns[name].WriteToStream(bw);
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

        foreach (var name in _columnOrder)
            _columns[name].ReadFromStream(br);
    }
}
