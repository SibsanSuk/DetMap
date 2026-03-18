namespace DetMap.Core;

public struct DirtyRect
{
    public int MinX, MinY, MaxX, MaxY;
    public bool IsDirty;

    public void Expand(int x, int y)
    {
        if (!IsDirty)
        {
            MinX = MaxX = x;
            MinY = MaxY = y;
            IsDirty = true;
        }
        else
        {
            if (x < MinX) MinX = x;
            if (x > MaxX) MaxX = x;
            if (y < MinY) MinY = y;
            if (y > MaxY) MaxY = y;
        }
    }

    public void Clear()
    {
        MinX = MinY = MaxX = MaxY = 0;
        IsDirty = false;
    }

    public bool Contains(int x, int y)
        => IsDirty && x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
}
