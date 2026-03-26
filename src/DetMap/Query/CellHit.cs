namespace DetMap.Query;

public readonly struct CellHit
{
    public readonly int X;
    public readonly int Y;

    public CellHit(int x, int y)
    {
        X = x;
        Y = y;
    }
}
