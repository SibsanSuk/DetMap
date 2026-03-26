using DetMath;

namespace DetMap.DbCommands;

public sealed class DetDbCommandRecord
{
    public int Order { get; set; }
    public DetDbCommandKind Kind { get; set; }
    public string TargetName { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public int RowId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public bool BoolValue { get; set; }
    public byte ByteValue { get; set; }
    public int IntValue { get; set; }
    public long Fix64RawValue { get; set; }
    public string? StringValue { get; set; }
}

public sealed class DetDbFrameRecord
{
    public ulong Tick { get; set; }
    public int CommandCount { get; set; }
    public string StateHashHex { get; set; } = string.Empty;
    public string FrameHashHex { get; set; } = string.Empty;
    public DetDbChangeSummary Summary { get; set; } = new();
    public IReadOnlyList<DetDbCommandRecord> Commands { get; set; } = Array.Empty<DetDbCommandRecord>();

    public static DetDbFrameRecord Create(ulong tick, string stateHashHex, string frameHashHex, DetDbCommandList commandList)
    {
        var commands = new DetDbCommandRecord[commandList.Count];
        for (int i = 0; i < commandList.Count; i++)
            commands[i] = CreateCommandRecord(i, commandList.Commands[i]);

        return new DetDbFrameRecord
        {
            Tick = tick,
            CommandCount = commandList.Count,
            StateHashHex = stateHashHex,
            FrameHashHex = frameHashHex,
            Summary = commandList.BuildSummary(),
            Commands = commands,
        };
    }

    private static DetDbCommandRecord CreateCommandRecord(int order, IDetDbCommand command)
    {
        var record = new DetDbCommandRecord
        {
            Order = order,
            Kind = command.Kind,
        };

        switch (command)
        {
            case SetGlobalFix64Command c:
                record.TargetName = c.Key;
                record.Fix64RawValue = c.Value.RawValue;
                break;
            case CreateRowCommand c:
                record.TargetName = c.TableName;
                record.RowId = c.ExpectedRowId;
                break;
            case DeleteRowCommand c:
                record.TargetName = c.TableName;
                record.RowId = c.RowId;
                break;
            case SetByteColumnCommand c:
                record.TargetName = c.TableName;
                record.FieldName = c.ColumnName;
                record.RowId = c.RowId;
                record.ByteValue = c.Value;
                break;
            case SetIntColumnCommand c:
                record.TargetName = c.TableName;
                record.FieldName = c.ColumnName;
                record.RowId = c.RowId;
                record.IntValue = c.Value;
                break;
            case SetFix64ColumnCommand c:
                record.TargetName = c.TableName;
                record.FieldName = c.ColumnName;
                record.RowId = c.RowId;
                record.Fix64RawValue = c.Value.RawValue;
                break;
            case SetStringColumnCommand c:
                record.TargetName = c.TableName;
                record.FieldName = c.ColumnName;
                record.RowId = c.RowId;
                record.StringValue = c.Value;
                break;
            case SetBitCellCommand c:
                record.TargetName = c.LayerName;
                record.X = c.X;
                record.Y = c.Y;
                record.BoolValue = c.Value;
                break;
            case SetByteCellCommand c:
                record.TargetName = c.LayerName;
                record.X = c.X;
                record.Y = c.Y;
                record.ByteValue = c.Value;
                break;
            case SetIntCellCommand c:
                record.TargetName = c.LayerName;
                record.X = c.X;
                record.Y = c.Y;
                record.IntValue = c.Value;
                break;
            case SetFix64CellCommand c:
                record.TargetName = c.LayerName;
                record.X = c.X;
                record.Y = c.Y;
                record.Fix64RawValue = c.Value.RawValue;
                break;
            case PlaceRowCommand c:
                record.TargetName = c.IndexName;
                record.RowId = c.RowId;
                record.X = c.X;
                record.Y = c.Y;
                break;
            case MoveRowCommand c:
                record.TargetName = c.IndexName;
                record.RowId = c.RowId;
                record.X = c.X;
                record.Y = c.Y;
                break;
            case RemoveRowCommand c:
                record.TargetName = c.IndexName;
                record.RowId = c.RowId;
                break;
            default:
                throw new NotSupportedException($"Unsupported DB command type: {command.GetType().Name}");
        }

        return record;
    }
}
