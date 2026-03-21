using DetMath;
using DetMap.Core;
using DetMap.Layers;
using DetMap.Tables;

namespace DetMap.Commands;

public enum DetCommandKind : byte
{
    SetGlobalFix64 = 0,
    CreateRow = 1,
    DeleteRow = 2,
    SetByteColumn = 3,
    SetIntColumn = 4,
    SetFix64Column = 5,
    SetStringColumn = 6,
    SetBooleanCell = 7,
    SetByteCell = 8,
    SetIntCell = 9,
    SetFix64Cell = 10,
    PlaceRow = 11,
    MoveRow = 12,
    RemoveRow = 13,
}

public interface IDetCommand
{
    DetCommandKind Kind { get; }
    void ApplyTo(DetSpatialDatabase database);
}

public sealed class DetCommandBatch
{
    private readonly List<IDetCommand> _commands = new();

    public int Count => _commands.Count;
    public IReadOnlyList<IDetCommand> Commands => _commands;

    public void SetGlobal(string key, Fix64 value)
        => _commands.Add(new SetGlobalFix64Command(key, value));

    public void CreateRow(string tableName, int expectedRowId)
        => _commands.Add(new CreateRowCommand(tableName, expectedRowId));

    public void DeleteRow(string tableName, int rowId)
        => _commands.Add(new DeleteRowCommand(tableName, rowId));

    public void SetByte(string tableName, string columnName, int rowId, byte value)
        => _commands.Add(new SetByteColumnCommand(tableName, columnName, rowId, value));

    public void SetInt(string tableName, string columnName, int rowId, int value)
        => _commands.Add(new SetIntColumnCommand(tableName, columnName, rowId, value));

    public void SetFix64(string tableName, string columnName, int rowId, Fix64 value)
        => _commands.Add(new SetFix64ColumnCommand(tableName, columnName, rowId, value));

    public void SetString(string tableName, string columnName, int rowId, string? value)
        => _commands.Add(new SetStringColumnCommand(tableName, columnName, rowId, value));

    public void SetBooleanCell(string layerName, int x, int y, bool value)
        => _commands.Add(new SetBooleanCellCommand(layerName, x, y, value));

    public void SetByteCell(string layerName, int x, int y, byte value)
        => _commands.Add(new SetByteCellCommand(layerName, x, y, value));

    public void SetIntCell(string layerName, int x, int y, int value)
        => _commands.Add(new SetIntCellCommand(layerName, x, y, value));

    public void SetFix64Cell(string layerName, int x, int y, Fix64 value)
        => _commands.Add(new SetFix64CellCommand(layerName, x, y, value));

    public void PlaceRow(string indexName, int rowId, int x, int y)
        => _commands.Add(new PlaceRowCommand(indexName, rowId, x, y));

    public void MoveRow(string indexName, int rowId, int x, int y)
        => _commands.Add(new MoveRowCommand(indexName, rowId, x, y));

    public void RemoveRow(string indexName, int rowId)
        => _commands.Add(new RemoveRowCommand(indexName, rowId));

    public void ApplyTo(DetSpatialDatabase database)
    {
        foreach (var command in _commands)
            command.ApplyTo(database);
    }

    public void Clear() => _commands.Clear();
}

public sealed class SetGlobalFix64Command : IDetCommand
{
    public DetCommandKind Kind => DetCommandKind.SetGlobalFix64;
    public string Key { get; }
    public Fix64 Value { get; }

    public SetGlobalFix64Command(string key, Fix64 value)
    {
        Key = key;
        Value = value;
    }

    public void ApplyTo(DetSpatialDatabase database)
        => database.SetGlobal(Key, Value);
}

public sealed class CreateRowCommand : IDetCommand
{
    public DetCommandKind Kind => DetCommandKind.CreateRow;
    public string TableName { get; }
    public int ExpectedRowId { get; }

    public CreateRowCommand(string tableName, int expectedRowId)
    {
        TableName = tableName;
        ExpectedRowId = expectedRowId;
    }

    public void ApplyTo(DetSpatialDatabase database)
    {
        int actualRowId = database.GetTable(TableName).CreateRow();
        if (actualRowId != ExpectedRowId)
            throw new InvalidOperationException($"CreateRow mismatch for table '{TableName}': expected {ExpectedRowId}, got {actualRowId}.");
    }
}

public sealed class DeleteRowCommand : IDetCommand
{
    public DetCommandKind Kind => DetCommandKind.DeleteRow;
    public string TableName { get; }
    public int RowId { get; }

    public DeleteRowCommand(string tableName, int rowId)
    {
        TableName = tableName;
        RowId = rowId;
    }

    public void ApplyTo(DetSpatialDatabase database)
        => database.GetTable(TableName).DeleteRow(RowId);
}

public sealed class SetByteColumnCommand : IDetCommand
{
    public DetCommandKind Kind => DetCommandKind.SetByteColumn;
    private readonly string _tableName;
    private readonly string _columnName;
    private readonly int _rowId;
    private readonly byte _value;

    public SetByteColumnCommand(string tableName, string columnName, int rowId, byte value)
    {
        _tableName = tableName;
        _columnName = columnName;
        _rowId = rowId;
        _value = value;
    }

    public void ApplyTo(DetSpatialDatabase database)
        => database.GetTable(_tableName).GetColumn<byte>(_columnName).Set(_rowId, _value);
}

public sealed class SetIntColumnCommand : IDetCommand
{
    public DetCommandKind Kind => DetCommandKind.SetIntColumn;
    private readonly string _tableName;
    private readonly string _columnName;
    private readonly int _rowId;
    private readonly int _value;

    public SetIntColumnCommand(string tableName, string columnName, int rowId, int value)
    {
        _tableName = tableName;
        _columnName = columnName;
        _rowId = rowId;
        _value = value;
    }

    public void ApplyTo(DetSpatialDatabase database)
        => database.GetTable(_tableName).GetColumn<int>(_columnName).Set(_rowId, _value);
}

public sealed class SetFix64ColumnCommand : IDetCommand
{
    public DetCommandKind Kind => DetCommandKind.SetFix64Column;
    private readonly string _tableName;
    private readonly string _columnName;
    private readonly int _rowId;
    private readonly Fix64 _value;

    public SetFix64ColumnCommand(string tableName, string columnName, int rowId, Fix64 value)
    {
        _tableName = tableName;
        _columnName = columnName;
        _rowId = rowId;
        _value = value;
    }

    public void ApplyTo(DetSpatialDatabase database)
        => database.GetTable(_tableName).GetColumn<Fix64>(_columnName).Set(_rowId, _value);
}

public sealed class SetStringColumnCommand : IDetCommand
{
    public DetCommandKind Kind => DetCommandKind.SetStringColumn;
    private readonly string _tableName;
    private readonly string _columnName;
    private readonly int _rowId;
    private readonly string? _value;

    public SetStringColumnCommand(string tableName, string columnName, int rowId, string? value)
    {
        _tableName = tableName;
        _columnName = columnName;
        _rowId = rowId;
        _value = value;
    }

    public void ApplyTo(DetSpatialDatabase database)
        => database.GetTable(_tableName).GetStringColumn(_columnName).Set(_rowId, _value);
}

public sealed class SetBooleanCellCommand : IDetCommand
{
    public DetCommandKind Kind => DetCommandKind.SetBooleanCell;
    private readonly string _layerName;
    private readonly int _x;
    private readonly int _y;
    private readonly bool _value;

    public SetBooleanCellCommand(string layerName, int x, int y, bool value)
    {
        _layerName = layerName;
        _x = x;
        _y = y;
        _value = value;
    }

    public void ApplyTo(DetSpatialDatabase database)
        => database.Grid.GetBooleanLayer(_layerName).Set(_x, _y, _value);
}

public sealed class SetByteCellCommand : IDetCommand
{
    public DetCommandKind Kind => DetCommandKind.SetByteCell;
    private readonly string _layerName;
    private readonly int _x;
    private readonly int _y;
    private readonly byte _value;

    public SetByteCellCommand(string layerName, int x, int y, byte value)
    {
        _layerName = layerName;
        _x = x;
        _y = y;
        _value = value;
    }

    public void ApplyTo(DetSpatialDatabase database)
        => database.Grid.GetValueLayer<byte>(_layerName).Set(_x, _y, _value);
}

public sealed class SetIntCellCommand : IDetCommand
{
    public DetCommandKind Kind => DetCommandKind.SetIntCell;
    private readonly string _layerName;
    private readonly int _x;
    private readonly int _y;
    private readonly int _value;

    public SetIntCellCommand(string layerName, int x, int y, int value)
    {
        _layerName = layerName;
        _x = x;
        _y = y;
        _value = value;
    }

    public void ApplyTo(DetSpatialDatabase database)
        => database.Grid.GetValueLayer<int>(_layerName).Set(_x, _y, _value);
}

public sealed class SetFix64CellCommand : IDetCommand
{
    public DetCommandKind Kind => DetCommandKind.SetFix64Cell;
    private readonly string _layerName;
    private readonly int _x;
    private readonly int _y;
    private readonly Fix64 _value;

    public SetFix64CellCommand(string layerName, int x, int y, Fix64 value)
    {
        _layerName = layerName;
        _x = x;
        _y = y;
        _value = value;
    }

    public void ApplyTo(DetSpatialDatabase database)
        => database.Grid.GetValueLayer<Fix64>(_layerName).Set(_x, _y, _value);
}

public sealed class PlaceRowCommand : IDetCommand
{
    public DetCommandKind Kind => DetCommandKind.PlaceRow;
    private readonly string _indexName;
    private readonly int _rowId;
    private readonly int _x;
    private readonly int _y;

    public PlaceRowCommand(string indexName, int rowId, int x, int y)
    {
        _indexName = indexName;
        _rowId = rowId;
        _x = x;
        _y = y;
    }

    public void ApplyTo(DetSpatialDatabase database)
        => database.Grid.GetCellIndex(_indexName).Place(_rowId, _x, _y);
}

public sealed class MoveRowCommand : IDetCommand
{
    public DetCommandKind Kind => DetCommandKind.MoveRow;
    private readonly string _indexName;
    private readonly int _rowId;
    private readonly int _x;
    private readonly int _y;

    public MoveRowCommand(string indexName, int rowId, int x, int y)
    {
        _indexName = indexName;
        _rowId = rowId;
        _x = x;
        _y = y;
    }

    public void ApplyTo(DetSpatialDatabase database)
        => database.Grid.GetCellIndex(_indexName).MoveTo(_rowId, _x, _y);
}

public sealed class RemoveRowCommand : IDetCommand
{
    public DetCommandKind Kind => DetCommandKind.RemoveRow;
    private readonly string _indexName;
    private readonly int _rowId;

    public RemoveRowCommand(string indexName, int rowId)
    {
        _indexName = indexName;
        _rowId = rowId;
    }

    public void ApplyTo(DetSpatialDatabase database)
        => database.Grid.GetCellIndex(_indexName).Remove(_rowId);
}
