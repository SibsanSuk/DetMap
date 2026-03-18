namespace DetMap.Core;

public enum DetLayerKind : byte
{
    LayerByte  = 0,
    LayerInt   = 1,
    LayerFix64 = 2,
    BitLayer   = 3,
    EntityMap  = 4,
    TagMap     = 5,
    FlowField  = 6,
}

public interface IDetLayer
{
    string Name { get; }
    DetLayerKind Kind { get; }
    DirtyRect Dirty { get; }
    void ClearDirty();
    void WriteToStream(BinaryWriter bw);
    void ReadFromStream(BinaryReader br, int cellCount);
}

public interface IDetSpatial
{
    int CountAt(int x, int y);
}

public interface IDetReadable<T>
{
    T Get(int x, int y);
}
