namespace DetMap.Spatial;

public readonly struct SpatialDefinition
{
    public readonly string Id;
    public readonly int Width;
    public readonly int Height;
    public readonly int TypeId;
    public readonly bool[]? FootprintMask; // null = full rect

    public SpatialDefinition(string id, int width, int height, int typeId, bool[]? footprintMask = null)
    {
        Id = id;
        Width = width;
        Height = height;
        TypeId = typeId;
        FootprintMask = footprintMask;
    }

    public bool OccupiesLocalCell(int localX, int localY)
        => FootprintMask == null || FootprintMask[localY * Width + localX];

    /// <summary>Creates an L-shaped footprint mask (fills all except top-right quadrant).</summary>
    public static bool[] CreateLShapeMask(int width, int height)
    {
        var mask = new bool[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                mask[y * width + x] = !(x >= width / 2 && y < height / 2);
        return mask;
    }
}
