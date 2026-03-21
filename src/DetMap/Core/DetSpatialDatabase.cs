using DetMath;
using DetMap.Commands;
using DetMap.Pathfinding;
using DetMap.Schema;
using DetMap.Serialization;
using DetMap.Tables;

namespace DetMap.Core;

public sealed class DetSpatialDatabase
{
    public readonly DetGrid Grid;
    public ulong Tick { get; private set; }

    private readonly Dictionary<string, Fix64> _globals = new();
    private readonly Dictionary<string, DetTable> _tables = new();
    private readonly Dictionary<string, DetPathStore> _pathStores = new();
    private readonly List<string> _tableOrder = new();
    private readonly List<string> _pathStoreOrder = new();

    public DetSpatialDatabase(int width, int height)
    {
        Grid = new DetGrid(width, height);
    }

    public Fix64 GetGlobal(string key)
        => _globals.TryGetValue(key, out var v) ? v : Fix64.Zero;

    public void SetGlobal(string key, Fix64 value)
        => _globals[key] = value;

    public void AdvanceTick() => Tick++;
    internal void SetTick(ulong tick) => Tick = tick;

    public DetTable CreateTable(string name, int capacity = 256)
    {
        var table = new DetTable(name, capacity);
        _tables[name] = table;
        AddTableName(name);
        return table;
    }

    public DetTable GetTable(string name) => _tables[name];

    public DetPathStore CreatePathStore(string name, int capacity = 256)
    {
        var store = new DetPathStore(name, capacity);
        _pathStores[name] = store;
        AddPathStoreName(name);
        return store;
    }

    public DetPathStore GetPathStore(string name) => _pathStores[name];

    public IReadOnlyDictionary<string, Fix64> Globals => _globals;
    public IReadOnlyDictionary<string, DetTable> Tables => _tables;
    public IReadOnlyDictionary<string, DetPathStore> PathStores => _pathStores;
    public IReadOnlyList<string> TableOrder => _tableOrder;
    public IReadOnlyList<string> PathStoreOrder => _pathStoreOrder;

    public DetDatabaseSchema GetSchema()
    {
        var layerSchemas = Grid.GetLayerSchemas();

        var tableSchemas = new DetTableSchema[_tableOrder.Count];
        for (int i = 0; i < _tableOrder.Count; i++)
            tableSchemas[i] = _tables[_tableOrder[i]].GetSchema();

        var globalKeys = new List<string>(_globals.Keys);
        globalKeys.Sort(StringComparer.Ordinal);

        var storeSchemas = new DetStoreSchema[_pathStoreOrder.Count];
        for (int i = 0; i < _pathStoreOrder.Count; i++)
            storeSchemas[i] = new DetStoreSchema(_pathStoreOrder[i], DetStoreKind.Path);

        return new DetDatabaseSchema(Grid.Width, Grid.Height, layerSchemas, tableSchemas, globalKeys, storeSchemas);
    }

    public void Apply(DetCommandBatch batch)
        => batch.ApplyTo(this);

    public byte[] ToBytes() => DetSnapshot.Serialize(this);

    public static DetSpatialDatabase FromBytes(byte[] data) => DetSnapshot.Deserialize(data);

    private void AddTableName(string name)
    {
        if (!_tableOrder.Contains(name))
            _tableOrder.Add(name);
    }

    private void AddPathStoreName(string name)
    {
        if (!_pathStoreOrder.Contains(name))
            _pathStoreOrder.Add(name);
    }
}
