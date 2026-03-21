using DetMap.Core;
using DetMap.Layers;

namespace DetMap.Building;

public delegate bool CellPredicate(DetGrid grid, int x, int y);

public static class BuildingPlacer
{
    public static bool CanPlace(
        DetGrid grid,
        int ox, int oy,
        BuildingDefinition def,
        DetValueLayer<int> buildingLayer,
        DetBooleanLayer walkable,
        CellPredicate? extraCheck = null)
    {
        for (int ly = 0; ly < def.H; ly++)
        for (int lx = 0; lx < def.W; lx++)
        {
            if (!def.IsSolid(lx, ly)) continue;
            int wx = ox + lx, wy = oy + ly;
            if (!grid.InBounds(wx, wy)) return false;
            if (buildingLayer.Get(wx, wy) != 0) return false;
            if (extraCheck != null && !extraCheck(grid, wx, wy)) return false;
        }
        return true;
    }

    public static void Place(
        DetGrid grid,
        int ox, int oy,
        BuildingDefinition def,
        DetValueLayer<int> buildingLayer,
        DetBooleanLayer walkable)
    {
        int id = def.BuildingTypeId;
        for (int ly = 0; ly < def.H; ly++)
        for (int lx = 0; lx < def.W; lx++)
        {
            if (!def.IsSolid(lx, ly)) continue;
            int wx = ox + lx, wy = oy + ly;
            buildingLayer.Set(wx, wy, id);
            walkable.Set(wx, wy, false);
        }
    }

    public static void Remove(
        DetGrid grid,
        int ox, int oy,
        BuildingDefinition def,
        DetValueLayer<int> buildingLayer,
        DetBooleanLayer walkable)
    {
        for (int ly = 0; ly < def.H; ly++)
        for (int lx = 0; lx < def.W; lx++)
        {
            if (!def.IsSolid(lx, ly)) continue;
            int wx = ox + lx, wy = oy + ly;
            buildingLayer.Set(wx, wy, 0);
            walkable.Set(wx, wy, true);
        }
    }
}
