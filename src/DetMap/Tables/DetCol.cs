using DetMath;

namespace DetMap.Tables;

public enum DetColKind : byte
{
    Byte   = 0,
    Int    = 1,
    Fix64  = 2,
    String = 3,
}

public interface IDetColData
{
    DetColKind Kind { get; }
    void WriteToStream(BinaryWriter bw);
    void ReadFromStream(BinaryReader br);
}

public sealed class DetCol<T> : IDetColData where T : unmanaged
{
    private T[] _data;

    public DetColKind Kind =>
        typeof(T) == typeof(byte)  ? DetColKind.Byte  :
        typeof(T) == typeof(int)   ? DetColKind.Int   :
        typeof(T) == typeof(Fix64) ? DetColKind.Fix64 :
        throw new NotSupportedException($"DetCol<{typeof(T).Name}> has no registered DetColKind");

    public DetCol(int capacity) => _data = new T[capacity];

    public T Get(int id) => _data[id];

    public void Set(int id, T value)
    {
        EnsureCapacity(id);
        _data[id] = value;
    }

    private void EnsureCapacity(int id)
    {
        if (id >= _data.Length)
            Array.Resize(ref _data, Math.Max(id + 1, _data.Length * 2));
    }

    public void WriteToStream(BinaryWriter bw)
    {
        bw.Write(_data.Length);
        foreach (var v in _data) WriteValue(bw, v);
    }

    public void ReadFromStream(BinaryReader br)
    {
        int len = br.ReadInt32();
        _data = new T[len];
        for (int i = 0; i < len; i++) _data[i] = ReadValue(br);
    }

    private static void WriteValue(BinaryWriter bw, T value)
    {
        if (value is byte b)   bw.Write(b);
        else if (value is int i)   bw.Write(i);
        else if (value is Fix64 f) bw.Write(f.RawValue);
        else throw new NotSupportedException($"DetCol<{typeof(T).Name}> serialization not supported");
    }

    private static T ReadValue(BinaryReader br)
    {
        if (typeof(T) == typeof(byte))  return (T)(object)br.ReadByte();
        if (typeof(T) == typeof(int))   return (T)(object)br.ReadInt32();
        if (typeof(T) == typeof(Fix64)) return (T)(object)Fix64.FromRaw(br.ReadInt64());
        throw new NotSupportedException($"DetCol<{typeof(T).Name}> deserialization not supported");
    }
}

public sealed class DetStringCol : IDetColData
{
    private string?[] _data;

    public DetColKind Kind => DetColKind.String;

    public DetStringCol(int capacity) => _data = new string?[capacity];

    public string? Get(int id) => _data[id];

    public void Set(int id, string? value)
    {
        EnsureCapacity(id);
        _data[id] = value;
    }

    private void EnsureCapacity(int id)
    {
        if (id >= _data.Length)
            Array.Resize(ref _data, Math.Max(id + 1, _data.Length * 2));
    }

    public void WriteToStream(BinaryWriter bw)
    {
        bw.Write(_data.Length);
        foreach (var s in _data) { bw.Write(s != null); if (s != null) bw.Write(s); }
    }

    public void ReadFromStream(BinaryReader br)
    {
        int len = br.ReadInt32();
        _data = new string?[len];
        for (int i = 0; i < len; i++) _data[i] = br.ReadBoolean() ? br.ReadString() : null;
    }
}
