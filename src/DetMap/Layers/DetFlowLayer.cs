using DetMath;
using DetMap.Core;

namespace DetMap.Layers;

/// <summary>Direction constants: 0=N,1=E,2=S,3=W,4=NE,5=SE,6=SW,7=NW, 255=blocked</summary>
public sealed class DetFlowLayer : IDetLayer, IDetReadable<byte>
{
    public const byte Blocked = 255;

    // Large sentinel value used as "unreachable" cost — not Fix64.MaxValue since DetMath doesn't expose it
    private static readonly Fix64 InfiniteCost = Fix64.FromRaw(long.MaxValue);

    private readonly byte[] _dir;
    private readonly Fix64[] _cost;
    private readonly int _width;
    private readonly int _height;

    public string Name { get; }
    public DetLayerKind Kind => DetLayerKind.Flow;
    public DirtyRect Dirty { get; private set; }

    public DetFlowLayer(string name, int width, int height)
    {
        Name = name;
        _width = width;
        _height = height;
        int size = width * height;
        _dir = new byte[size];
        _cost = new Fix64[size];
        Array.Fill(_dir, Blocked);
        Array.Fill(_cost, InfiniteCost);
    }

    public byte Get(int x, int y) => _dir[y * _width + x];
    public Fix64 GetCost(int x, int y) => _cost[y * _width + x];

    public void Set(int x, int y, byte direction, Fix64 cost)
    {
        int idx = y * _width + x;
        _dir[idx] = direction;
        _cost[idx] = cost;

        var dirty = Dirty;
        dirty.Expand(x, y);
        Dirty = dirty;
    }

    public void Reset()
    {
        Array.Fill(_dir, Blocked);
        Array.Fill(_cost, InfiniteCost);
    }

    public void CopyFrom(DetFlowLayer source)
    {
        if (source._width != _width || source._height != _height || source._dir.Length != _dir.Length)
            throw new InvalidOperationException($"Cannot copy layer '{source.Name}' into '{Name}' with different dimensions.");

        Array.Copy(source._dir, _dir, _dir.Length);
        Array.Copy(source._cost, _cost, _cost.Length);
        Dirty = default;
    }

    public void ClearDirty()
    {
        var dirty = Dirty;
        dirty.Clear();
        Dirty = dirty;
    }

    public void WriteToStream(BinaryWriter bw)
    {
        bw.Write(_dir.Length);
        bw.Write(_dir);
        foreach (var c in _cost) bw.Write(c.RawValue);
    }

    public void ReadFromStream(BinaryReader br, int cellCount)
    {
        int len = br.ReadInt32();
        var dirBytes = br.ReadBytes(len);
        dirBytes.CopyTo(_dir, 0);
        for (int i = 0; i < len; i++) _cost[i] = Fix64.FromRaw(br.ReadInt64());
    }
}
