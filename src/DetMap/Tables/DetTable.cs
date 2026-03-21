using DetMath;
using DetMap.Core;
using DetMap.Schema;

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

    public int CreateRow()
    {
        int id = _freeList.Count > 0 ? _freeList.Pop() : _highWater++;
        _alive.Set(id, 1);
        return id;
    }

    public int PeekNextRowId()
        => _freeList.Count > 0 ? _freeList.Peek() : _highWater;

    public void DeleteRow(int id)
    {
        _alive.Set(id, 0);
        _freeList.Push(id);
    }

    public bool RowExists(int id) => _alive.Get(id) == 1;

    /// <summary>
    /// Low-level generic column factory. Prefer the typed helpers when the column kind is known.
    /// </summary>
    /// <param name="type">Use <see cref="DetType.Byte"/>, <see cref="DetType.Int"/>, or <see cref="DetType.Fix64"/>.</param>
    public DetColumn<T> CreateColumn<T>(string name, DetType<T> type) where T : unmanaged
    {
        var column = new DetColumn<T>(Math.Max(_highWater, 64));
        _columns[name] = column;
        _columnOrder.Add(name);
        return column;
    }

    public DetColumn<byte> CreateByteColumn(string name)
        => CreateColumn(name, DetType.Byte);

    public DetColumn<int> CreateIntColumn(string name)
        => CreateColumn(name, DetType.Int);

    public DetColumn<Fix64> CreateFix64Column(string name)
        => CreateColumn(name, DetType.Fix64);

    public DetStringColumn CreateStringColumn(string name)
    {
        var column = new DetStringColumn(Math.Max(_highWater, 64));
        _columns[name] = column;
        _columnOrder.Add(name);
        return column;
    }

    public DetColumn<T> GetColumn<T>(string name) where T : unmanaged => (DetColumn<T>)_columns[name];
    public DetColumn<byte> GetByteColumn(string name) => (DetColumn<byte>)_columns[name];
    public DetColumn<int> GetIntColumn(string name) => (DetColumn<int>)_columns[name];
    public DetColumn<Fix64> GetFix64Column(string name) => (DetColumn<Fix64>)_columns[name];
    public DetStringColumn GetStringColumn(string name) => (DetStringColumn)_columns[name];

    /// <summary>Iterate existing rows in deterministic order (0..highWater).</summary>
    public IEnumerable<int> GetRowIds()
    {
        for (int i = 0; i < _highWater; i++)
            if (_alive.Get(i) == 1) yield return i;
    }

    public int HighWater => _highWater;

    /// <summary>Column names in insertion order — used for deterministic serialization.</summary>
    public IReadOnlyList<string> ColumnOrder => _columnOrder;

    /// <summary>Access a column by name as <see cref="IDetColumnData"/> for schema-driven serialization.</summary>
    public IDetColumnData GetColumnData(string name) => _columns[name];

    public DetTableSchema GetSchema()
    {
        var columns = new DetColumnSchema[_columnOrder.Count];
        for (int i = 0; i < _columnOrder.Count; i++)
        {
            string name = _columnOrder[i];
            columns[i] = new DetColumnSchema(name, _columns[name].Kind);
        }

        return new DetTableSchema(Name, columns);
    }

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
