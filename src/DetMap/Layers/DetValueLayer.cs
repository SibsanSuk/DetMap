using System.Runtime.InteropServices;
using DetMath;
using DetMap.Core;

namespace DetMap.Layers;

public sealed class DetValueLayer<T> : IDetLayer, IDetReadable<T> where T : unmanaged
{
    private readonly T[] _data;
    private readonly int _width;
    private readonly int _height;

    public string Name { get; }

    public DetLayerKind Kind =>
        typeof(T) == typeof(byte)  ? DetLayerKind.ValueByte  :
        typeof(T) == typeof(int)   ? DetLayerKind.ValueInt   :
        typeof(T) == typeof(Fix64) ? DetLayerKind.ValueFix64 :
        throw new NotSupportedException($"DetValueLayer<{typeof(T).Name}> has no registered DetLayerKind");

    public DirtyRect Dirty { get; private set; }

    public DetValueLayer(string name, int width, int height, T defaultValue = default)
    {
        Name = name;
        _width = width;
        _height = height;
        _data = new T[width * height];
        if (!defaultValue.Equals(default(T)))
            Array.Fill(_data, defaultValue);
    }

    public T Get(int x, int y) => _data[y * _width + x];

    public void Set(int x, int y, T value)
    {
        _data[y * _width + x] = value;
        var dirty = Dirty;
        dirty.Expand(x, y);
        Dirty = dirty;
    }

    public void Fill(T value) => Array.Fill(_data, value);

    public Span<T> AsSpan() => _data.AsSpan();

    public void ClearDirty()
    {
        var dirty = Dirty;
        dirty.Clear();
        Dirty = dirty;
    }

    public void WriteToStream(BinaryWriter bw)
    {
        var bytes = MemoryMarshal.AsBytes(_data.AsSpan());
        bw.Write(bytes.Length);
        bw.Write(bytes);
    }

    public void ReadFromStream(BinaryReader br, int cellCount)
    {
        var bytes = br.ReadBytes(br.ReadInt32());
        MemoryMarshal.AsBytes(_data.AsSpan()).Clear();
        bytes.CopyTo(MemoryMarshal.AsBytes(_data.AsSpan()));
    }
}
