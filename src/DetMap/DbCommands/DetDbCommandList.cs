using DetMath;
using DetMap.Core;
using DetMap.Layers;
using DetMap.Tables;

namespace DetMap.DbCommands;

public enum DetDbCommandKind : byte
{
    SetGlobalFix64 = 0,
    CreateRow = 1,
    DeleteRow = 2,
    SetByteColumn = 3,
    SetIntColumn = 4,
    SetFix64Column = 5,
    SetStringColumn = 6,
    SetBitCell = 7,
    SetByteCell = 8,
    SetIntCell = 9,
    SetFix64Cell = 10,
    PlaceRow = 11,
    MoveRow = 12,
    RemoveRow = 13,
}

public interface IDetDbCommand
{
    DetDbCommandKind Kind { get; }
    void ApplyTo(DetSpatialDatabase database);
}

public sealed class DetDbCommandList
{
    private readonly List<IDetDbCommand> _commands = new();

    public int Count => _commands.Count;
    public IReadOnlyList<IDetDbCommand> Commands => _commands;

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

    public void SetBitCell(string layerName, int x, int y, bool value)
        => _commands.Add(new SetBitCellCommand(layerName, x, y, value));

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

    public DetDbChangeSummary BuildSummary()
    {
        var builder = new DetDbChangeSummaryBuilder();
        foreach (var command in _commands)
        {
            switch (command)
            {
                case SetGlobalFix64Command c:
                    builder.AddGlobal(c.Key);
                    break;
                case CreateRowCommand c:
                    builder.AddCreateRow(c.TableName, c.ExpectedRowId);
                    break;
                case DeleteRowCommand c:
                    builder.AddDeleteRow(c.TableName, c.RowId);
                    break;
                case SetByteColumnCommand c:
                    builder.AddColumnWrite(c.TableName, c.ColumnName, c.RowId);
                    break;
                case SetIntColumnCommand c:
                    builder.AddColumnWrite(c.TableName, c.ColumnName, c.RowId);
                    break;
                case SetFix64ColumnCommand c:
                    builder.AddColumnWrite(c.TableName, c.ColumnName, c.RowId);
                    break;
                case SetStringColumnCommand c:
                    builder.AddColumnWrite(c.TableName, c.ColumnName, c.RowId);
                    break;
                case SetBitCellCommand c:
                    builder.AddLayerWrite(c.LayerName, c.X, c.Y);
                    break;
                case SetByteCellCommand c:
                    builder.AddLayerWrite(c.LayerName, c.X, c.Y);
                    break;
                case SetIntCellCommand c:
                    builder.AddLayerWrite(c.LayerName, c.X, c.Y);
                    break;
                case SetFix64CellCommand c:
                    builder.AddLayerWrite(c.LayerName, c.X, c.Y);
                    break;
                case PlaceRowCommand c:
                    builder.AddIndexWrite(c.IndexName, c.RowId, c.X, c.Y);
                    break;
                case MoveRowCommand c:
                    builder.AddIndexWrite(c.IndexName, c.RowId, c.X, c.Y);
                    break;
                case RemoveRowCommand c:
                    builder.AddIndexWrite(c.IndexName, c.RowId, null, null);
                    break;
            }
        }

        return builder.Build(_commands.Count);
    }

    public void Clear() => _commands.Clear();
}

public sealed class SetGlobalFix64Command : IDetDbCommand
{
    public DetDbCommandKind Kind => DetDbCommandKind.SetGlobalFix64;
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

public sealed class CreateRowCommand : IDetDbCommand
{
    public DetDbCommandKind Kind => DetDbCommandKind.CreateRow;
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

public sealed class DeleteRowCommand : IDetDbCommand
{
    public DetDbCommandKind Kind => DetDbCommandKind.DeleteRow;
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

public sealed class SetByteColumnCommand : IDetDbCommand
{
    public DetDbCommandKind Kind => DetDbCommandKind.SetByteColumn;
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
        => database.GetTable(_tableName).GetByteColumn(_columnName).Set(_rowId, _value);

    public string TableName => _tableName;
    public string ColumnName => _columnName;
    public int RowId => _rowId;
    public byte Value => _value;
}

public sealed class SetIntColumnCommand : IDetDbCommand
{
    public DetDbCommandKind Kind => DetDbCommandKind.SetIntColumn;
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
        => database.GetTable(_tableName).GetIntColumn(_columnName).Set(_rowId, _value);

    public string TableName => _tableName;
    public string ColumnName => _columnName;
    public int RowId => _rowId;
    public int Value => _value;
}

public sealed class SetFix64ColumnCommand : IDetDbCommand
{
    public DetDbCommandKind Kind => DetDbCommandKind.SetFix64Column;
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
        => database.GetTable(_tableName).GetFix64Column(_columnName).Set(_rowId, _value);

    public string TableName => _tableName;
    public string ColumnName => _columnName;
    public int RowId => _rowId;
    public Fix64 Value => _value;
}

public sealed class SetStringColumnCommand : IDetDbCommand
{
    public DetDbCommandKind Kind => DetDbCommandKind.SetStringColumn;
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

    public string TableName => _tableName;
    public string ColumnName => _columnName;
    public int RowId => _rowId;
    public string? Value => _value;
}

public sealed class SetBitCellCommand : IDetDbCommand
{
    public DetDbCommandKind Kind => DetDbCommandKind.SetBitCell;
    private readonly string _layerName;
    private readonly int _x;
    private readonly int _y;
    private readonly bool _value;

    public SetBitCellCommand(string layerName, int x, int y, bool value)
    {
        _layerName = layerName;
        _x = x;
        _y = y;
        _value = value;
    }

    public void ApplyTo(DetSpatialDatabase database)
        => database.Grid.GetBitLayer(_layerName).Set(_x, _y, _value);

    public string LayerName => _layerName;
    public int X => _x;
    public int Y => _y;
    public bool Value => _value;
}

public sealed class SetByteCellCommand : IDetDbCommand
{
    public DetDbCommandKind Kind => DetDbCommandKind.SetByteCell;
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
        => database.Grid.GetByteLayer(_layerName).Set(_x, _y, _value);

    public string LayerName => _layerName;
    public int X => _x;
    public int Y => _y;
    public byte Value => _value;
}

public sealed class SetIntCellCommand : IDetDbCommand
{
    public DetDbCommandKind Kind => DetDbCommandKind.SetIntCell;
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
        => database.Grid.GetIntLayer(_layerName).Set(_x, _y, _value);

    public string LayerName => _layerName;
    public int X => _x;
    public int Y => _y;
    public int Value => _value;
}

public sealed class SetFix64CellCommand : IDetDbCommand
{
    public DetDbCommandKind Kind => DetDbCommandKind.SetFix64Cell;
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
        => database.Grid.GetFix64Layer(_layerName).Set(_x, _y, _value);

    public string LayerName => _layerName;
    public int X => _x;
    public int Y => _y;
    public Fix64 Value => _value;
}

public sealed class PlaceRowCommand : IDetDbCommand
{
    public DetDbCommandKind Kind => DetDbCommandKind.PlaceRow;
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

    public string IndexName => _indexName;
    public int RowId => _rowId;
    public int X => _x;
    public int Y => _y;
}

public sealed class MoveRowCommand : IDetDbCommand
{
    public DetDbCommandKind Kind => DetDbCommandKind.MoveRow;
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

    public string IndexName => _indexName;
    public int RowId => _rowId;
    public int X => _x;
    public int Y => _y;
}

public sealed class RemoveRowCommand : IDetDbCommand
{
    public DetDbCommandKind Kind => DetDbCommandKind.RemoveRow;
    private readonly string _indexName;
    private readonly int _rowId;

    public RemoveRowCommand(string indexName, int rowId)
    {
        _indexName = indexName;
        _rowId = rowId;
    }

    public void ApplyTo(DetSpatialDatabase database)
        => database.Grid.GetCellIndex(_indexName).Remove(_rowId);

    public string IndexName => _indexName;
    public int RowId => _rowId;
}
