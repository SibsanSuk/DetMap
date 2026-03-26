using DetMath;
using DetMap.Core;
using DetMap.DbCommands;
using DetMap.Layers;
using DetMap.Pathfinding;
using DetMap.Schema;
using DetMap.Tables;

namespace DetMap.Serialization;

/// <summary>
/// Binary save/load for DetMap state.
///
/// Format (version 5):
///   [4]  magic: 'D','M','A','P'
///   [2]  version: 5
///   ── SCHEMA ──────────────────────────────────────────
///   [4]  grid width
///   [4]  grid height
///   [4]  layer count
///     per layer: [1] kind  [str] name
///   [4]  table count
///     per table: [str] name  [4] colCount
///       per col: [1] kind  [str] name  [1] isDerived  [1] isEditable  [str] source
///       [4] indexCount
///       per index: [1] kind  [str] name  [str] columnName
///   [4]  global count (keys in ordinal-sorted order)
///     per global: [str] key
///   [4]  pathstore count
///     per pathstore: [str] name
///   ── DATA ────────────────────────────────────────────
///   [8]  tick (ulong)
///   layer data × N (raw bytes, schema order)
///   global values × G (Fix64 RawValue, schema order): [8] each
///   table data × T: [4] highWater  [4] freeCount  freeList[]
///                   alive col data  col data × colCount
///   pathstore data × P: [4] slotCount
///     per slot: [4] length  if length>0: [4] currentStep  [length×4] steps
///   [1]  hasFrameRecord
///     if true:
///       [8] tick
///       [str] stateHashHex
///       [str] frameHashHex
///       [4] commandCount
///       summary counts
///       summary name lists
///       command records × N
/// </summary>
public static class DetSnapshot
{
    private static readonly byte[] Magic = { (byte)'D', (byte)'M', (byte)'A', (byte)'P' };
    private const ushort Version = 5;

    public static byte[] Serialize(DetSpatialDatabase database, DetDbFrameRecord? frameRecord = null)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write(Magic);
        bw.Write(Version);

        // ── SCHEMA ──────────────────────────────────────────────────────────
        bw.Write(database.Grid.Width);
        bw.Write(database.Grid.Height);

        var layers = new List<IDetLayer>(database.Grid.LayerOrder.Count);
        foreach (var name in database.Grid.LayerOrder)
            layers.Add(database.Grid.AllLayers[name]);
        bw.Write(layers.Count);
        foreach (var layer in layers)
        {
            bw.Write((byte)layer.Kind);
            bw.Write(layer.Name);
        }

        var tables = new List<DetTable>(database.TableOrder.Count);
        foreach (var name in database.TableOrder)
            tables.Add(database.Tables[name]);
        bw.Write(tables.Count);
        foreach (var table in tables)
        {
            bw.Write(table.Name);
            bw.Write(table.ColumnOrder.Count);
            foreach (var colName in table.ColumnOrder)
            {
                DetColumnSchema columnSchema = table.GetColumnSchema(colName);
                bw.Write((byte)columnSchema.Kind);
                bw.Write(colName);
                bw.Write(columnSchema.IsDerived);
                bw.Write(columnSchema.IsEditable);
                bw.Write(columnSchema.Source);
            }

            bw.Write(table.IndexOrder.Count);
            foreach (var indexName in table.IndexOrder)
            {
                DetColumnIndexSchema indexSchema = table.GetIndexSchema(indexName);
                bw.Write((byte)indexSchema.Kind);
                bw.Write(indexSchema.Name);
                bw.Write(indexSchema.ColumnName);
            }
        }

        var globalKeys = new List<string>(database.Globals.Keys);
        globalKeys.Sort(StringComparer.Ordinal);
        bw.Write(globalKeys.Count);
        foreach (var key in globalKeys) bw.Write(key);

        var pathStores = new List<DetPathStore>(database.PathStoreOrder.Count);
        foreach (var name in database.PathStoreOrder)
            pathStores.Add(database.PathStores[name]);
        bw.Write(pathStores.Count);
        foreach (var store in pathStores) bw.Write(store.Name);

        // ── DATA ────────────────────────────────────────────────────────────
        bw.Write(database.Tick);

        int cellCount = database.Grid.Width * database.Grid.Height;
        foreach (var layer in layers)
            layer.WriteToStream(bw);

        foreach (var key in globalKeys)
            bw.Write(database.Globals[key].RawValue);

        foreach (var table in tables)
            table.WriteDataToStream(bw);

        foreach (var store in pathStores)
            store.WriteToStream(bw);

        bw.Write(frameRecord is not null);
        if (frameRecord is not null)
            WriteFrameRecord(bw, frameRecord);

        return ms.ToArray();
    }

    public static DetSpatialDatabase Deserialize(byte[] data)
        => Deserialize(data, frameCount: 3, supportsFramePool: true);

    internal static DetSpatialDatabase Deserialize(byte[] data, int frameCount, bool supportsFramePool)
    {
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);

        var magic = br.ReadBytes(4);
        if (magic[0] != 'D' || magic[1] != 'M' || magic[2] != 'A' || magic[3] != 'P')
            throw new InvalidDataException("Not a DetMap save file.");
        ushort version = br.ReadUInt16();
        if (version < 2 || version > Version)
            throw new InvalidDataException($"Unsupported snapshot version: {version}.");

        // ── SCHEMA ──────────────────────────────────────────────────────────
        int width  = br.ReadInt32();
        int height = br.ReadInt32();

        int layerCount = br.ReadInt32();
        var layerSchema = new (DetLayerKind Kind, string Name)[layerCount];
        for (int i = 0; i < layerCount; i++)
            layerSchema[i] = ((DetLayerKind)br.ReadByte(), br.ReadString());

        int tableCount = br.ReadInt32();
        var tableSchema = new (string Name, DetColumnSchema[] Cols, DetColumnIndexSchema[] Indexes)[tableCount];
        for (int i = 0; i < tableCount; i++)
        {
            string tName = br.ReadString();
            int colCount = br.ReadInt32();
            var cols = new DetColumnSchema[colCount];
            for (int j = 0; j < colCount; j++)
            {
                DetColumnKind kind = (DetColumnKind)br.ReadByte();
                string colName = br.ReadString();
                bool isDerived = version >= 3 && br.ReadBoolean();
                bool isEditable = version >= 3 ? br.ReadBoolean() : true;
                string source = version >= 3 ? br.ReadString() : string.Empty;
                cols[j] = new DetColumnSchema(colName, kind, isDerived, source, isEditable);
            }

            int indexCount = version >= 5 ? br.ReadInt32() : 0;
            var indexes = new DetColumnIndexSchema[indexCount];
            for (int j = 0; j < indexCount; j++)
            {
                DetColumnKind kind = (DetColumnKind)br.ReadByte();
                string indexName = br.ReadString();
                string columnName = br.ReadString();
                indexes[j] = new DetColumnIndexSchema(indexName, kind, columnName);
            }

            tableSchema[i] = (tName, cols, indexes);
        }

        int globalCount = br.ReadInt32();
        var globalKeys = new string[globalCount];
        for (int i = 0; i < globalCount; i++) globalKeys[i] = br.ReadString();

        int pathStoreCount = br.ReadInt32();
        var pathStoreNames = new string[pathStoreCount];
        for (int i = 0; i < pathStoreCount; i++) pathStoreNames[i] = br.ReadString();

        // ── DATA ────────────────────────────────────────────────────────────
        ulong tick = br.ReadUInt64();

        var database = DetSpatialDatabase.CreateSnapshotInstance(width, height, frameCount, supportsFramePool);
        database.SetTick(tick);

        int cellCount = width * height;
        foreach (var (kind, name) in layerSchema)
        {
            IDetLayer layer = CreateLayerFromKind(database, kind, name);
            layer.ReadFromStream(br, cellCount);
        }

        for (int i = 0; i < globalCount; i++)
            database.SetGlobal(globalKeys[i], Fix64.FromRaw(br.ReadInt64()));

        foreach (var (tName, cols, indexes) in tableSchema)
        {
            var table = database.CreateTable(tName);
            foreach (DetColumnSchema columnSchema in cols)
                RegisterColumn(table, columnSchema);
            foreach (DetColumnIndexSchema indexSchema in indexes)
                RegisterColumnIndex(table, indexSchema);
            table.ReadDataFromStream(br);
        }

        foreach (var name in pathStoreNames)
        {
            var store = database.CreatePathStore(name);
            store.ReadFromStream(br);
        }

        if (version >= 4 && br.ReadBoolean())
            SkipFrameRecord(br);

        return database;
    }

    private static void WriteFrameRecord(BinaryWriter bw, DetDbFrameRecord frameRecord)
    {
        bw.Write(frameRecord.Tick);
        bw.Write(frameRecord.StateHashHex ?? string.Empty);
        bw.Write(frameRecord.FrameHashHex ?? string.Empty);
        bw.Write(frameRecord.CommandCount);

        var summary = frameRecord.Summary ?? new DetDbChangeSummary();
        bw.Write(summary.GlobalWriteCount);
        bw.Write(summary.CreatedRowCount);
        bw.Write(summary.DeletedRowCount);
        bw.Write(summary.ColumnWriteCount);
        bw.Write(summary.LayerWriteCount);
        bw.Write(summary.IndexWriteCount);
        WriteStringList(bw, summary.ChangedGlobals);
        WriteStringList(bw, summary.ChangedTables);
        WriteStringList(bw, summary.ChangedColumns);
        WriteStringList(bw, summary.ChangedLayers);
        WriteStringList(bw, summary.ChangedIndices);
        WriteStringList(bw, summary.TouchedRows);
        WriteStringList(bw, summary.TouchedCells);

        bw.Write(frameRecord.Commands.Count);
        foreach (var command in frameRecord.Commands)
            WriteCommandRecord(bw, command);
    }

    private static void WriteCommandRecord(BinaryWriter bw, DetDbCommandRecord command)
    {
        bw.Write(command.Order);
        bw.Write((byte)command.Kind);
        bw.Write(command.TargetName ?? string.Empty);
        bw.Write(command.FieldName ?? string.Empty);
        bw.Write(command.RowId);
        bw.Write(command.X);
        bw.Write(command.Y);
        bw.Write(command.BoolValue);
        bw.Write(command.ByteValue);
        bw.Write(command.IntValue);
        bw.Write(command.Fix64RawValue);
        bw.Write(command.StringValue is not null);
        if (command.StringValue is not null)
            bw.Write(command.StringValue);
    }

    private static void WriteStringList(BinaryWriter bw, IReadOnlyList<string> values)
    {
        bw.Write(values.Count);
        foreach (var value in values)
            bw.Write(value);
    }

    private static void SkipFrameRecord(BinaryReader br)
    {
        br.ReadUInt64();
        br.ReadString();
        br.ReadString();
        br.ReadInt32();
        SkipSummary(br);

        int commandCount = br.ReadInt32();
        for (int i = 0; i < commandCount; i++)
            SkipCommandRecord(br);
    }

    private static void SkipSummary(BinaryReader br)
    {
        br.ReadInt32();
        br.ReadInt32();
        br.ReadInt32();
        br.ReadInt32();
        br.ReadInt32();
        br.ReadInt32();
        SkipStringList(br);
        SkipStringList(br);
        SkipStringList(br);
        SkipStringList(br);
        SkipStringList(br);
        SkipStringList(br);
        SkipStringList(br);
    }

    private static void SkipStringList(BinaryReader br)
    {
        int count = br.ReadInt32();
        for (int i = 0; i < count; i++)
            br.ReadString();
    }

    private static void SkipCommandRecord(BinaryReader br)
    {
        br.ReadInt32();
        br.ReadByte();
        br.ReadString();
        br.ReadString();
        br.ReadInt32();
        br.ReadInt32();
        br.ReadInt32();
        br.ReadBoolean();
        br.ReadByte();
        br.ReadInt32();
        br.ReadInt64();
        if (br.ReadBoolean())
            br.ReadString();
    }

    private static IDetLayer CreateLayerFromKind(DetSpatialDatabase database, DetLayerKind kind, string name)
        => kind switch
        {
            DetLayerKind.ValueByte  => database.Grid.CreateByteLayer(name),
            DetLayerKind.ValueInt   => database.Grid.CreateIntLayer(name),
            DetLayerKind.ValueFix64 => database.Grid.CreateFix64Layer(name),
            DetLayerKind.Bit        => database.Grid.CreateBitLayer(name),
            DetLayerKind.CellIndex => database.Grid.CreateCellIndex(name),
            DetLayerKind.Tag => database.Grid.CreateTagLayer(name),
            DetLayerKind.Flow => database.Grid.CreateFlowLayer(name),
            _ => throw new InvalidDataException($"Unknown layer kind: {(byte)kind}"),
        };

    private static void RegisterColumn(DetTable table, DetColumnSchema columnSchema)
    {
        DetColumnOptions options = new(columnSchema.IsDerived, columnSchema.Source, columnSchema.IsEditable);
        switch (columnSchema.Kind)
        {
            case DetColumnKind.Byte:   table.CreateByteColumn(columnSchema.Name, options);   break;
            case DetColumnKind.Int:    table.CreateIntColumn(columnSchema.Name, options);    break;
            case DetColumnKind.Fix64:  table.CreateFix64Column(columnSchema.Name, options);  break;
            case DetColumnKind.String: table.CreateStringColumn(columnSchema.Name, options); break;
            default: throw new InvalidDataException($"Unknown col kind: {(byte)columnSchema.Kind}");
        }
    }

    private static void RegisterColumnIndex(DetTable table, DetColumnIndexSchema indexSchema)
    {
        switch (indexSchema.Kind)
        {
            case DetColumnKind.Byte:
                table.CreateByteIndex(indexSchema.Name, table.GetByteColumn(indexSchema.ColumnName));
                break;
            case DetColumnKind.Int:
                table.CreateIntIndex(indexSchema.Name, table.GetIntColumn(indexSchema.ColumnName));
                break;
            case DetColumnKind.Fix64:
                table.CreateFix64Index(indexSchema.Name, table.GetFix64Column(indexSchema.ColumnName));
                break;
            default:
                throw new InvalidDataException($"Unsupported column index kind: {(byte)indexSchema.Kind}");
        }
    }
}
