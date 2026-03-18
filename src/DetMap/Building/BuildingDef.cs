using DetMath;

namespace DetMap.Building;

public readonly struct BuildingDef
{
    public readonly string Id;
    public readonly int W, H;
    public readonly Fix64 BuildingId;
    public readonly bool[]? Mask; // null = full rect

    public BuildingDef(string id, int w, int h, Fix64 buildingId, bool[]? mask = null)
    {
        Id = id;
        W = w;
        H = h;
        BuildingId = buildingId;
        Mask = mask;
    }

    public bool IsSolid(int lx, int ly)
        => Mask == null || Mask[ly * W + lx];

    /// <summary>Creates an L-shaped building mask (fills all except top-right quadrant).</summary>
    public static bool[] MakeLShape(int w, int h)
    {
        var mask = new bool[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                mask[y * w + x] = !(x >= w / 2 && y < h / 2);
        return mask;
    }
}
