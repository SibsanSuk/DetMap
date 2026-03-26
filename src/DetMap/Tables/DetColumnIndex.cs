using DetMath;

namespace DetMap.Tables;

internal interface IDetColumnIndexData
{
    string Name { get; }
    string ColumnName { get; }
    DetColumnKind Kind { get; }
    void AddRowFromCurrentValue(int rowId);
    void RemoveRowFromCurrentValue(int rowId);
    void Clear();
}

public sealed class DetColumnIndex<T> : IDetColumnIndexData where T : unmanaged
{
    private readonly DetColumn<T> _column;
    private readonly Func<int, bool> _rowExists;
    private readonly Dictionary<T, SortedSet<int>> _rowsByKey = new();

    public string Name { get; }
    public string ColumnName { get; }
    public DetColumnKind Kind =>
        typeof(T) == typeof(byte) ? DetColumnKind.Byte :
        typeof(T) == typeof(int) ? DetColumnKind.Int :
        typeof(T) == typeof(Fix64) ? DetColumnKind.Fix64 :
        throw new NotSupportedException($"DetColumnIndex<{typeof(T).Name}> has no registered DetColumnKind");

    internal DetColumnIndex(string name, string columnName, DetColumn<T> column, Func<int, bool> rowExists)
    {
        Name = name;
        ColumnName = columnName;
        _column = column;
        _rowExists = rowExists;
        _column.RegisterValueChanged(OnColumnValueChanged);
    }

    public int Count(T key)
        => _rowsByKey.TryGetValue(key, out var rows) ? rows.Count : 0;

    public bool Contains(T key, int rowId)
        => _rowsByKey.TryGetValue(key, out var rows) && rows.Contains(rowId);

    public IEnumerable<int> GetRowIds(T key)
        => _rowsByKey.TryGetValue(key, out var rows) ? rows : Array.Empty<int>();

    internal void AddRowFromCurrentValue(int rowId)
        => AddRow(rowId, _column.Get(rowId));

    internal void RemoveRowFromCurrentValue(int rowId)
        => RemoveRow(rowId, _column.Get(rowId));

    internal void Clear()
        => _rowsByKey.Clear();

    private void OnColumnValueChanged(int rowId, T oldValue, T newValue)
    {
        if (!_rowExists(rowId) || EqualityComparer<T>.Default.Equals(oldValue, newValue))
            return;

        RemoveRow(rowId, oldValue);
        AddRow(rowId, newValue);
    }

    private void AddRow(int rowId, T key)
    {
        if (!_rowsByKey.TryGetValue(key, out var rows))
        {
            rows = new SortedSet<int>();
            _rowsByKey[key] = rows;
        }

        rows.Add(rowId);
    }

    private void RemoveRow(int rowId, T key)
    {
        if (!_rowsByKey.TryGetValue(key, out var rows))
            return;

        rows.Remove(rowId);
        if (rows.Count == 0)
            _rowsByKey.Remove(key);
    }

    void IDetColumnIndexData.AddRowFromCurrentValue(int rowId) => AddRowFromCurrentValue(rowId);
    void IDetColumnIndexData.RemoveRowFromCurrentValue(int rowId) => RemoveRowFromCurrentValue(rowId);
    void IDetColumnIndexData.Clear() => Clear();
}
