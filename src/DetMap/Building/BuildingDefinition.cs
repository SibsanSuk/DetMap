namespace DetMap.Building;

public readonly struct BuildingDefinition
{
    public readonly string Id;
    public readonly int W, H;
    public readonly int BuildingTypeId;
    public readonly bool[]? Mask; // null = full rect

    public BuildingDefinition(string id, int w, int h, int buildingTypeId, bool[]? mask = null)
    {
        Id = id;
        W = w;
        H = h;
        BuildingTypeId = buildingTypeId;
        Mask = mask;
    }

    public bool IsSolid(int lx, int ly)
        => Mask == null || Mask[ly * W + lx];

    /// <summary>Creates an L-shaped building mask (fills all except top-right quadrant).</summary>
    public static bool[] CreateLShapeMask(int w, int h)
    {
        var mask = new bool[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                mask[y * w + x] = !(x >= w / 2 && y < h / 2);
        return mask;
    }
}
