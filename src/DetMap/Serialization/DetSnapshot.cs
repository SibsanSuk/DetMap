using DetMath;
using DetMap.Core;
using DetMap.Layers;
using DetMap.Pathfinding;
using DetMap.Tables;

namespace DetMap.Serialization;

/// <summary>
/// Binary save/load for DetMap state.
///
/// Format (version 2):
///   [4]  magic: 'D','M','A','P'
///   [2]  version: 2
///   ── SCHEMA ──────────────────────────────────────────
///   [4]  grid width
///   [4]  grid height
///   [4]  layer count
///     per layer: [1] kind  [str] name
///   [4]  table count
///     per table: [str] name  [4] colCount
///       per col: [1] kind  [str] name
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
/// </summary>
public static class DetSnapshot
{
    private static readonly byte[] Magic = { (byte)'D', (byte)'M', (byte)'A', (byte)'P' };
    private const ushort Version = 2;

    public static byte[] Serialize(DetSpatialDatabase database)
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
                bw.Write((byte)table.GetColumnData(colName).Kind);
                bw.Write(colName);
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

        return ms.ToArray();
    }

    public static DetSpatialDatabase Deserialize(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);

        var magic = br.ReadBytes(4);
        if (magic[0] != 'D' || magic[1] != 'M' || magic[2] != 'A' || magic[3] != 'P')
            throw new InvalidDataException("Not a DetMap save file.");
        ushort version = br.ReadUInt16();
        if (version != Version)
            throw new InvalidDataException($"Unsupported snapshot version: {version}.");

        // ── SCHEMA ──────────────────────────────────────────────────────────
        int width  = br.ReadInt32();
        int height = br.ReadInt32();

        int layerCount = br.ReadInt32();
        var layerSchema = new (DetLayerKind Kind, string Name)[layerCount];
        for (int i = 0; i < layerCount; i++)
            layerSchema[i] = ((DetLayerKind)br.ReadByte(), br.ReadString());

        int tableCount = br.ReadInt32();
        var tableSchema = new (string Name, (DetColumnKind Kind, string ColName)[] Cols)[tableCount];
        for (int i = 0; i < tableCount; i++)
        {
            string tName = br.ReadString();
            int colCount = br.ReadInt32();
            var cols = new (DetColumnKind, string)[colCount];
            for (int j = 0; j < colCount; j++)
                cols[j] = ((DetColumnKind)br.ReadByte(), br.ReadString());
            tableSchema[i] = (tName, cols);
        }

        int globalCount = br.ReadInt32();
        var globalKeys = new string[globalCount];
        for (int i = 0; i < globalCount; i++) globalKeys[i] = br.ReadString();

        int pathStoreCount = br.ReadInt32();
        var pathStoreNames = new string[pathStoreCount];
        for (int i = 0; i < pathStoreCount; i++) pathStoreNames[i] = br.ReadString();

        // ── DATA ────────────────────────────────────────────────────────────
        ulong tick = br.ReadUInt64();

        var database = new DetSpatialDatabase(width, height);
        database.SetTick(tick);

        int cellCount = width * height;
        foreach (var (kind, name) in layerSchema)
        {
            IDetLayer layer = CreateLayerFromKind(database, kind, name);
            layer.ReadFromStream(br, cellCount);
        }

        for (int i = 0; i < globalCount; i++)
            database.SetGlobal(globalKeys[i], Fix64.FromRaw(br.ReadInt64()));

        foreach (var (tName, cols) in tableSchema)
        {
            var table = database.CreateTable(tName);
            foreach (var (kind, colName) in cols)
                RegisterColumn(table, kind, colName);
            table.ReadDataFromStream(br);
        }

        foreach (var name in pathStoreNames)
        {
            var store = database.CreatePathStore(name);
            store.ReadFromStream(br);
        }

        return database;
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

    private static void RegisterColumn(DetTable table, DetColumnKind kind, string colName)
    {
        switch (kind)
        {
            case DetColumnKind.Byte:   table.CreateByteColumn(colName);   break;
            case DetColumnKind.Int:    table.CreateIntColumn(colName);    break;
            case DetColumnKind.Fix64:  table.CreateFix64Column(colName);  break;
            case DetColumnKind.String: table.CreateStringColumn(colName); break;
            default: throw new InvalidDataException($"Unknown col kind: {(byte)kind}");
        }
    }
}
