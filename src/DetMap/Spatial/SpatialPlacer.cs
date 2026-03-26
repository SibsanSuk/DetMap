using DetMap.Core;
using DetMap.Layers;

namespace DetMap.Spatial;

public static class SpatialPlacer
{
    public static bool CanPlace(
        DetGrid grid,
        int ox, int oy,
        SpatialDefinition definition,
        DetValueLayer<int> placementLayer,
        DetBitLayer walkable,
        DetCellPredicate? extraCheck = null)
    {
        for (int ly = 0; ly < definition.Height; ly++)
        for (int lx = 0; lx < definition.Width; lx++)
        {
            if (!definition.OccupiesLocalCell(lx, ly)) continue;
            int wx = ox + lx, wy = oy + ly;
            if (!grid.InBounds(wx, wy)) return false;
            if (placementLayer.Get(wx, wy) != 0) return false;
            if (!walkable.Get(wx, wy)) return false;
            if (extraCheck != null && !extraCheck(grid, wx, wy)) return false;
        }
        return true;
    }

    public static void Place(
        DetGrid grid,
        int ox, int oy,
        SpatialDefinition definition,
        DetValueLayer<int> placementLayer,
        DetBitLayer walkable)
    {
        int id = definition.TypeId;
        for (int ly = 0; ly < definition.Height; ly++)
        for (int lx = 0; lx < definition.Width; lx++)
        {
            if (!definition.OccupiesLocalCell(lx, ly)) continue;
            int wx = ox + lx, wy = oy + ly;
            placementLayer.Set(wx, wy, id);
            walkable.Set(wx, wy, false);
        }
    }

    public static void Remove(
        DetGrid grid,
        int ox, int oy,
        SpatialDefinition definition,
        DetValueLayer<int> placementLayer,
        DetBitLayer walkable)
    {
        for (int ly = 0; ly < definition.Height; ly++)
        for (int lx = 0; lx < definition.Width; lx++)
        {
            if (!definition.OccupiesLocalCell(lx, ly)) continue;
            int wx = ox + lx, wy = oy + ly;
            placementLayer.Set(wx, wy, 0);
            walkable.Set(wx, wy, true);
        }
    }
}
