using DetMath;
using DetMap.Serialization;
using DetMap.Tables;

namespace DetMap.Core;

public sealed class DetMap
{
    public readonly DetGrid Grid;
    public ulong Tick { get; private set; }

    private readonly Dictionary<string, Fix64> _globals = new();
    private readonly Dictionary<string, DetTable> _tables = new();

    public DetMap(int width, int height)
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
        return table;
    }

    public DetTable Table(string name) => _tables[name];

    public IReadOnlyDictionary<string, Fix64> Globals => _globals;
    public IReadOnlyDictionary<string, DetTable> Tables => _tables;

    public byte[] ToBytes() => Snapshot.Serialize(this);

    public static DetMap FromBytes(byte[] data) => Snapshot.Deserialize(data);
}
