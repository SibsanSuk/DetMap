namespace DetMap.DbCommands;

public sealed class DetDbChangeSummary
{
    public int CommandCount { get; set; }
    public int GlobalWriteCount { get; set; }
    public int CreatedRowCount { get; set; }
    public int DeletedRowCount { get; set; }
    public int ColumnWriteCount { get; set; }
    public int LayerWriteCount { get; set; }
    public int IndexWriteCount { get; set; }
    public IReadOnlyList<string> ChangedGlobals { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ChangedTables { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ChangedColumns { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ChangedLayers { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ChangedIndices { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> TouchedRows { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> TouchedCells { get; set; } = Array.Empty<string>();
}

public sealed class DetDbApplyResult
{
    public ulong Tick { get; set; }
    public int CommandCount { get; set; }
    public string StateHashHex { get; set; } = string.Empty;
    public string FrameHashHex { get; set; } = string.Empty;
    public DetDbChangeSummary Summary { get; set; } = new();
}

internal sealed class DetDbChangeSummaryBuilder
{
    private readonly List<string> _globals = new();
    private readonly HashSet<string> _globalSet = new(StringComparer.Ordinal);
    private readonly List<string> _tables = new();
    private readonly HashSet<string> _tableSet = new(StringComparer.Ordinal);
    private readonly List<string> _columns = new();
    private readonly HashSet<string> _columnSet = new(StringComparer.Ordinal);
    private readonly List<string> _layers = new();
    private readonly HashSet<string> _layerSet = new(StringComparer.Ordinal);
    private readonly List<string> _indices = new();
    private readonly HashSet<string> _indexSet = new(StringComparer.Ordinal);
    private readonly List<string> _rows = new();
    private readonly HashSet<string> _rowSet = new(StringComparer.Ordinal);
    private readonly List<string> _cells = new();
    private readonly HashSet<string> _cellSet = new(StringComparer.Ordinal);

    public int GlobalWriteCount { get; private set; }
    public int CreatedRowCount { get; private set; }
    public int DeletedRowCount { get; private set; }
    public int ColumnWriteCount { get; private set; }
    public int LayerWriteCount { get; private set; }
    public int IndexWriteCount { get; private set; }

    public void AddGlobal(string key)
    {
        GlobalWriteCount++;
        AddUnique(_globals, _globalSet, key);
    }

    public void AddCreateRow(string tableName, int rowId)
    {
        CreatedRowCount++;
        AddUnique(_tables, _tableSet, tableName);
        AddTouchedRow(tableName, rowId);
    }

    public void AddDeleteRow(string tableName, int rowId)
    {
        DeletedRowCount++;
        AddUnique(_tables, _tableSet, tableName);
        AddTouchedRow(tableName, rowId);
    }

    public void AddColumnWrite(string tableName, string columnName, int rowId)
    {
        ColumnWriteCount++;
        AddUnique(_tables, _tableSet, tableName);
        AddUnique(_columns, _columnSet, $"{tableName}.{columnName}");
        AddTouchedRow(tableName, rowId);
    }

    public void AddLayerWrite(string layerName, int x, int y)
    {
        LayerWriteCount++;
        AddUnique(_layers, _layerSet, layerName);
        AddTouchedCell(layerName, x, y);
    }

    public void AddIndexWrite(string indexName, int rowId, int? x, int? y)
    {
        IndexWriteCount++;
        AddUnique(_indices, _indexSet, indexName);
        AddUnique(_rows, _rowSet, $"{indexName}:{rowId}");
        if (x.HasValue && y.HasValue)
            AddTouchedCell(indexName, x.Value, y.Value);
    }

    public DetDbChangeSummary Build(int commandCount)
    {
        return new DetDbChangeSummary
        {
            CommandCount = commandCount,
            GlobalWriteCount = GlobalWriteCount,
            CreatedRowCount = CreatedRowCount,
            DeletedRowCount = DeletedRowCount,
            ColumnWriteCount = ColumnWriteCount,
            LayerWriteCount = LayerWriteCount,
            IndexWriteCount = IndexWriteCount,
            ChangedGlobals = _globals.ToArray(),
            ChangedTables = _tables.ToArray(),
            ChangedColumns = _columns.ToArray(),
            ChangedLayers = _layers.ToArray(),
            ChangedIndices = _indices.ToArray(),
            TouchedRows = _rows.ToArray(),
            TouchedCells = _cells.ToArray(),
        };
    }

    private void AddTouchedRow(string ownerName, int rowId)
        => AddUnique(_rows, _rowSet, $"{ownerName}:{rowId}");

    private void AddTouchedCell(string ownerName, int x, int y)
        => AddUnique(_cells, _cellSet, $"{ownerName}({x},{y})");

    private static void AddUnique(List<string> list, HashSet<string> set, string value)
    {
        if (set.Add(value))
            list.Add(value);
    }
}
