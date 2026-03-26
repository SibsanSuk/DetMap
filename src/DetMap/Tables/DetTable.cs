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
    private readonly Dictionary<string, DetColumnSchema> _columnSchemas = new();
    private readonly List<string> _columnOrder = new();
    private readonly Dictionary<string, IDetColumnIndexData> _indexes = new();
    private readonly Dictionary<string, DetColumnIndexSchema> _indexSchemas = new();
    private readonly List<string> _indexOrder = new();
    private readonly Dictionary<IDetColumnData, string> _columnNamesByData = new();

    public string Name { get; }

    public DetTable(string name, int capacity = 256)
    {
        Name = name;
        _alive = new DetColumn<byte>(capacity);
    }

    public int CreateRow()
    {
        int id;
        if (_freeList.Count > 0)
        {
            id = _freeList.Pop();
            ResetRowData(id);
        }
        else
        {
            id = _highWater++;
        }

        _alive.Set(id, 1);
        AddRowToIndexes(id);
        return id;
    }

    public int PeekNextRowId()
        => _freeList.Count > 0 ? _freeList.Peek() : _highWater;

    public void DeleteRow(int id)
    {
        RemoveRowFromIndexes(id);
        ResetRowData(id);
        _alive.Set(id, 0);
        _freeList.Push(id);
    }

    public bool RowExists(int id) => _alive.Get(id) == 1;

    /// <summary>
    /// Low-level generic column factory. Prefer the typed helpers when the column kind is known.
    /// </summary>
    /// <param name="type">Use <see cref="DetType.Byte"/>, <see cref="DetType.Int"/>, or <see cref="DetType.Fix64"/>.</param>
    public DetColumn<T> CreateColumn<T>(string name, DetType<T> type, DetColumnOptions? options = null) where T : unmanaged
    {
        var column = new DetColumn<T>(Math.Max(_highWater, 64));
        _columns[name] = column;
        _columnNamesByData[column] = name;
        options ??= new DetColumnOptions();
        _columnSchemas[name] = new DetColumnSchema(name, column.Kind, options.IsDerived, options.Source, options.IsEditable);
        _columnOrder.Add(name);
        return column;
    }

    public DetColumn<byte> CreateByteColumn(string name, DetColumnOptions? options = null)
        => CreateColumn(name, DetType.Byte, options);

    public DetColumn<int> CreateIntColumn(string name, DetColumnOptions? options = null)
        => CreateColumn(name, DetType.Int, options);

    public DetColumn<Fix64> CreateFix64Column(string name, DetColumnOptions? options = null)
        => CreateColumn(name, DetType.Fix64, options);

    public DetStringColumn CreateStringColumn(string name, DetColumnOptions? options = null)
    {
        var column = new DetStringColumn(Math.Max(_highWater, 64));
        _columns[name] = column;
        _columnNamesByData[column] = name;
        options ??= new DetColumnOptions();
        _columnSchemas[name] = new DetColumnSchema(name, column.Kind, options.IsDerived, options.Source, options.IsEditable);
        _columnOrder.Add(name);
        return column;
    }

    public DetColumnIndex<T> CreateColumnIndex<T>(string name, DetColumn<T> column) where T : unmanaged
    {
        if (!_columnNamesByData.TryGetValue(column, out string? columnName))
            throw new InvalidOperationException($"Column index '{name}' must target a column created by table '{Name}'.");

        var index = new DetColumnIndex<T>(name, columnName, column, RowExists);
        _indexes[name] = index;
        _indexSchemas[name] = new DetColumnIndexSchema(name, index.Kind, columnName);
        _indexOrder.Add(name);

        foreach (int rowId in GetRowIds())
            index.AddRowFromCurrentValue(rowId);

        return index;
    }

    public DetColumnIndex<byte> CreateByteIndex(string name, DetColumn<byte> column)
        => CreateColumnIndex(name, column);

    public DetColumnIndex<int> CreateIntIndex(string name, DetColumn<int> column)
        => CreateColumnIndex(name, column);

    public DetColumnIndex<Fix64> CreateFix64Index(string name, DetColumn<Fix64> column)
        => CreateColumnIndex(name, column);

    public DetColumn<T> GetColumn<T>(string name) where T : unmanaged => (DetColumn<T>)_columns[name];
    public DetColumn<byte> GetByteColumn(string name) => (DetColumn<byte>)_columns[name];
    public DetColumn<int> GetIntColumn(string name) => (DetColumn<int>)_columns[name];
    public DetColumn<Fix64> GetFix64Column(string name) => (DetColumn<Fix64>)_columns[name];
    public DetStringColumn GetStringColumn(string name) => (DetStringColumn)_columns[name];
    public DetColumnIndex<T> GetColumnIndex<T>(string name) where T : unmanaged => (DetColumnIndex<T>)_indexes[name];
    public DetColumnIndex<byte> GetByteIndex(string name) => (DetColumnIndex<byte>)_indexes[name];
    public DetColumnIndex<int> GetIntIndex(string name) => (DetColumnIndex<int>)_indexes[name];
    public DetColumnIndex<Fix64> GetFix64Index(string name) => (DetColumnIndex<Fix64>)_indexes[name];

    /// <summary>Iterate existing rows in deterministic order (0..highWater).</summary>
    public IEnumerable<int> GetRowIds()
    {
        for (int i = 0; i < _highWater; i++)
            if (_alive.Get(i) == 1) yield return i;
    }

    public int HighWater => _highWater;

    /// <summary>Column names in insertion order — used for deterministic serialization.</summary>
    public IReadOnlyList<string> ColumnOrder => _columnOrder;
    public IReadOnlyList<string> IndexOrder => _indexOrder;

    /// <summary>Access a column by name as <see cref="IDetColumnData"/> for schema-driven serialization.</summary>
    public IDetColumnData GetColumnData(string name) => _columns[name];
    public DetColumnSchema GetColumnSchema(string name) => _columnSchemas[name];
    public DetColumnIndexSchema GetIndexSchema(string name) => _indexSchemas[name];

    public DetTableSchema GetSchema()
    {
        var columns = new DetColumnSchema[_columnOrder.Count];
        for (int i = 0; i < _columnOrder.Count; i++)
        {
            string name = _columnOrder[i];
            columns[i] = _columnSchemas[name];
        }

        var indexes = new DetColumnIndexSchema[_indexOrder.Count];
        for (int i = 0; i < _indexOrder.Count; i++)
        {
            string name = _indexOrder[i];
            indexes[i] = _indexSchemas[name];
        }

        return new DetTableSchema(Name, columns, indexes);
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

        RebuildIndexes();
    }

    public void CopyFrom(DetTable source)
    {
        if (!HasCompatibleStructure(source))
            throw new InvalidOperationException($"Cannot copy table '{source.Name}' into '{Name}' because the schemas differ.");

        _highWater = source._highWater;
        _alive.CopyFrom(source._alive);

        _freeList.Clear();
        int[] free = source._freeList.ToArray();
        for (int i = free.Length - 1; i >= 0; i--)
            _freeList.Push(free[i]);

        foreach (var name in _columnOrder)
            _columns[name].CopyFrom(source._columns[name]);

        RebuildIndexes();
    }

    internal bool HasCompatibleStructure(DetTable source)
    {
        if (!string.Equals(Name, source.Name, StringComparison.Ordinal))
            return false;
        if (_columnOrder.Count != source._columnOrder.Count || _indexOrder.Count != source._indexOrder.Count)
            return false;

        for (int i = 0; i < _columnOrder.Count; i++)
        {
            string name = _columnOrder[i];
            if (!string.Equals(name, source._columnOrder[i], StringComparison.Ordinal))
                return false;

            if (_columnSchemas[name].Kind != source._columnSchemas[name].Kind)
                return false;
        }

        for (int i = 0; i < _indexOrder.Count; i++)
        {
            string name = _indexOrder[i];
            if (!string.Equals(name, source._indexOrder[i], StringComparison.Ordinal))
                return false;

            DetColumnIndexSchema index = _indexSchemas[name];
            DetColumnIndexSchema other = source._indexSchemas[name];
            if (index.Kind != other.Kind || !string.Equals(index.ColumnName, other.ColumnName, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private void ResetRowData(int id)
    {
        foreach (var column in _columns.Values)
            column.ResetRow(id);
    }

    internal void RebuildIndexes()
    {
        foreach (var index in _indexes.Values)
            index.Clear();

        foreach (int rowId in GetRowIds())
            AddRowToIndexes(rowId);
    }

    private void AddRowToIndexes(int id)
    {
        foreach (var index in _indexes.Values)
            index.AddRowFromCurrentValue(id);
    }

    private void RemoveRowFromIndexes(int id)
    {
        foreach (var index in _indexes.Values)
            index.RemoveRowFromCurrentValue(id);
    }
}
