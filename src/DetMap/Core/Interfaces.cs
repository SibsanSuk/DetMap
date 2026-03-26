namespace DetMap.Core;

public delegate bool DetCellPredicate(DetGrid grid, int x, int y);

public enum DetLayerKind : byte
{
    ValueByte = 0,
    ValueInt  = 1,
    ValueFix64 = 2,
    Bit = 3,
    CellIndex = 4,
    Tag = 5,
    Flow = 6,
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
