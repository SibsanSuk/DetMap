using DetMath;
using DetMap.Pathfinding;
using DetMap.Schema;
using DetMap.Serialization;
using DetMap.Tables;

namespace DetMap.Core;

public sealed class DetSpatialDatabase
{
    private readonly bool _supportsFramePool;
    private readonly int _configuredFrameCount;
    private DetSpatialDatabase?[]? _framePool;
    private int _currentFrameIndex;
    private int? _nextFrameIndex;

    public readonly DetGrid Grid;
    public ulong Tick { get; private set; }

    private readonly Dictionary<string, Fix64> _globals = new();
    private readonly Dictionary<string, DetTable> _tables = new();
    private readonly Dictionary<string, DetPathStore> _pathStores = new();
    private readonly List<string> _tableOrder = new();
    private readonly List<string> _pathStoreOrder = new();

    public int FrameCount => _supportsFramePool ? _configuredFrameCount : 1;
    public int CurrentFrameIndex => _currentFrameIndex;
    public int? NextFrameIndex => _nextFrameIndex;
    public bool HasNextFrame => _nextFrameIndex.HasValue;
    public DetSpatialDatabase? NextFrame => _nextFrameIndex.HasValue ? _framePool?[_nextFrameIndex.Value] : null;

    public DetSpatialDatabase(int width, int height, int frameCount = 3)
        : this(width, height, frameCount, supportsFramePool: true)
    {
    }

    private DetSpatialDatabase(int width, int height, int frameCount, bool supportsFramePool)
    {
        if (supportsFramePool && frameCount < 2)
            throw new ArgumentOutOfRangeException(nameof(frameCount), "Frame pool must contain at least two frames.");

        Grid = new DetGrid(width, height);
        _supportsFramePool = supportsFramePool;
        _configuredFrameCount = supportsFramePool ? frameCount : 1;
    }

    public Fix64 GetGlobal(string key)
        => _globals.TryGetValue(key, out var v) ? v : Fix64.Zero;

    public void SetGlobal(string key, Fix64 value)
        => _globals[key] = value;

    internal void AdvanceTick() => Tick++;
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

    public byte[] ToBytes() => DetSnapshot.Serialize(this);
    public DetSpatialDatabase Clone() => DetSnapshot.Deserialize(ToBytes(), FrameCount, supportsFramePool: _supportsFramePool);
    public void CopyStateFrom(DetSpatialDatabase source)
    {
        if (!HasCompatibleStructure(source))
            throw new InvalidOperationException("Cannot copy database state from a different schema.");

        SetTick(source.Tick);

        _globals.Clear();
        foreach (var kv in source._globals)
            _globals[kv.Key] = kv.Value;

        Grid.CopyFrom(source.Grid);

        foreach (string name in _tableOrder)
            _tables[name].CopyFrom(source._tables[name]);

        foreach (string name in _pathStoreOrder)
            _pathStores[name].CopyFrom(source._pathStores[name]);
    }

    internal bool HasCompatibleStructure(DetSpatialDatabase source)
    {
        if (!Grid.HasCompatibleStructure(source.Grid))
            return false;
        if (_tableOrder.Count != source._tableOrder.Count || _pathStoreOrder.Count != source._pathStoreOrder.Count)
            return false;

        for (int i = 0; i < _tableOrder.Count; i++)
        {
            string name = _tableOrder[i];
            if (!string.Equals(name, source._tableOrder[i], StringComparison.Ordinal))
                return false;
            if (!_tables[name].HasCompatibleStructure(source._tables[name]))
                return false;
        }

        for (int i = 0; i < _pathStoreOrder.Count; i++)
        {
            string name = _pathStoreOrder[i];
            if (!string.Equals(name, source._pathStoreOrder[i], StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    public byte[] ComputeStateHash() => DetStateHash.Compute(this);
    public string ComputeStateHashHex() => DetStateHash.ComputeHex(this);
    public byte[] ComputeFrameHash() => DetStateHash.ComputeFrame(this);
    public string ComputeFrameHashHex() => DetStateHash.ComputeFrameHex(this);

    public static DetSpatialDatabase FromBytes(byte[] data) => DetSnapshot.Deserialize(data);

    public DetSpatialDatabase PrepareNextFrame()
    {
        EnsureFramePool();

        if (_nextFrameIndex.HasValue)
            throw new InvalidOperationException("Next frame has already been prepared.");

        int nextIndex = (_currentFrameIndex + 1) % _framePool!.Length;
        DetSpatialDatabase nextFrame = _framePool[nextIndex]!;
        if (nextFrame.HasCompatibleStructure(this))
            nextFrame.CopyStateFrom(this);
        else
        {
            nextFrame = CloneFrameSlot();
            _framePool[nextIndex] = nextFrame;
        }

        nextFrame.AdvanceTick();
        _nextFrameIndex = nextIndex;
        return nextFrame;
    }

    public DetSpatialDatabase GetNextFrame()
        => NextFrame ?? throw new InvalidOperationException("Next frame has not been prepared.");

    public DetSpatialDatabase CommitNextFrame()
    {
        if (!_nextFrameIndex.HasValue)
            throw new InvalidOperationException("Next frame has not been prepared.");

        DetSpatialDatabase prepared = _framePool![_nextFrameIndex.Value]!;
        CopyStateFrom(prepared);
        _currentFrameIndex = _nextFrameIndex.Value;
        _nextFrameIndex = null;
        return this;
    }

    public void DiscardNextFrame()
    {
        _nextFrameIndex = null;
    }

    public DetSpatialDatabase AdvanceFrame()
    {
        PrepareNextFrame();
        return CommitNextFrame();
    }

    internal static DetSpatialDatabase CreateSnapshotInstance(int width, int height, int frameCount, bool supportsFramePool)
        => new(width, height, frameCount, supportsFramePool);

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

    private void EnsureFramePool()
    {
        if (!_supportsFramePool)
            throw new InvalidOperationException("This database instance is a pooled frame slot and cannot prepare nested frames.");

        if (_framePool is not null)
            return;

        _framePool = new DetSpatialDatabase[_configuredFrameCount];
        for (int i = 0; i < _framePool.Length; i++)
            _framePool[i] = CloneFrameSlot();

        _currentFrameIndex = 0;
        _nextFrameIndex = null;
    }

    private DetSpatialDatabase CloneFrameSlot()
        => DetSnapshot.Deserialize(ToBytes(), frameCount: 1, supportsFramePool: false);
}
