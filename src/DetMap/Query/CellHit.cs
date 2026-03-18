using DetMath;

namespace DetMap.Query;

public readonly struct CellHit
{
    public readonly int X, Y;
    public readonly Fix64 Value;

    public CellHit(int x, int y, Fix64 value = default)
    {
        X = x;
        Y = y;
        Value = value;
    }
}
