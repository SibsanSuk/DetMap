using DetMap.Core;

namespace DetMap.Layers;

public sealed class DetBooleanLayer : IDetLayer, IDetReadable<bool>
{
    private readonly ulong[] _bits;
    private readonly int _width;
    private readonly int _height;

    public string Name { get; }
    public DetLayerKind Kind => DetLayerKind.Boolean;
    public DirtyRect Dirty { get; private set; }

    public DetBooleanLayer(string name, int width, int height)
    {
        Name = name;
        _width = width;
        _height = height;
        int cellCount = width * height;
        _bits = new ulong[(cellCount + 63) / 64];
    }

    public bool Get(int x, int y)
    {
        int idx = y * _width + x;
        return (_bits[idx >> 6] & (1UL << (idx & 63))) != 0;
    }

    public void Set(int x, int y, bool value)
    {
        int idx = y * _width + x;
        int word = idx >> 6;
        ulong mask = 1UL << (idx & 63);
        if (value) _bits[word] |= mask;
        else _bits[word] &= ~mask;

        var dirty = Dirty;
        dirty.Expand(x, y);
        Dirty = dirty;
    }

    public void SetAll(bool value)
    {
        ulong fill = value ? ulong.MaxValue : 0UL;
        Array.Fill(_bits, fill);
    }

    public static void And(DetBooleanLayer a, DetBooleanLayer b, DetBooleanLayer result)
    {
        for (int i = 0; i < result._bits.Length; i++)
            result._bits[i] = a._bits[i] & b._bits[i];
    }

    public static void Or(DetBooleanLayer a, DetBooleanLayer b, DetBooleanLayer result)
    {
        for (int i = 0; i < result._bits.Length; i++)
            result._bits[i] = a._bits[i] | b._bits[i];
    }

    public static void Xor(DetBooleanLayer a, DetBooleanLayer b, DetBooleanLayer result)
    {
        for (int i = 0; i < result._bits.Length; i++)
            result._bits[i] = a._bits[i] ^ b._bits[i];
    }

    public void ClearDirty()
    {
        var dirty = Dirty;
        dirty.Clear();
        Dirty = dirty;
    }

    public void WriteToStream(BinaryWriter bw)
    {
        bw.Write(_bits.Length);
        foreach (var w in _bits) bw.Write(w);
    }

    public void ReadFromStream(BinaryReader br, int cellCount)
    {
        int len = br.ReadInt32();
        for (int i = 0; i < len; i++) _bits[i] = br.ReadUInt64();
    }
}
