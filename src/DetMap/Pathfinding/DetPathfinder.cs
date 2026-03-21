using System.Buffers;
using DetMath;
using DetMap.Layers;

namespace DetMap.Pathfinding;

public sealed class DetPathfinder
{
    private readonly int _width;
    private readonly int _height;

    // Sentinel — "unreachable" initial cost
    private static readonly Fix64 InfiniteCost = Fix64.FromRaw(long.MaxValue);

    // Neighbor directions: N, E, S, W, NE, SE, SW, NW
    private static readonly (int dx, int dy)[] Dirs = new (int, int)[]
    {
        (0, -1), (1, 0), (0, 1), (-1, 0),
        (1, -1), (1, 1), (-1, 1), (-1, -1)
    };

    // Straight = 10, Diagonal ≈ 14.14 → DetMath scale=100, so raw = 1414
    private static readonly Fix64 StraightCost = Fix64.FromInt(10);
    private static readonly Fix64 DiagonalCost = Fix64.FromRaw(1414);

    public DetPathfinder(int width, int height)
    {
        _width = width;
        _height = height;
    }

    public DetPath FindPath(
        int startX, int startY,
        int goalX, int goalY,
        DetBooleanLayer walkable,
        DetValueLayer<byte>? unitCount = null,
        int maxSearchNodes = 2048)
    {
        if (!InBounds(startX, startY) || !InBounds(goalX, goalY))
            return default;

        int cellCount = _width * _height;
        var gCost = ArrayPool<Fix64>.Shared.Rent(cellCount);
        var parent = ArrayPool<int>.Shared.Rent(cellCount);

        try
        {
            Array.Fill(parent, -1, 0, cellCount);
            for (int i = 0; i < cellCount; i++) gCost[i] = InfiniteCost;

            var open = new DetMinHeap(256);
            int startCell = CellIdx(startX, startY);
            int goalCell = CellIdx(goalX, goalY);

            gCost[startCell] = Fix64.Zero;
            open.Push(Heuristic(startX, startY, goalX, goalY), startCell);

            int nodesVisited = 0;

            while (open.Count > 0 && nodesVisited < maxSearchNodes)
            {
                var (_, current) = open.Pop();
                nodesVisited++;

                if (current == goalCell)
                    return ReconstructPath(parent, startCell, goalCell);

                int cx = current % _width, cy = current / _width;

                for (int d = 0; d < Dirs.Length; d++)
                {
                    int nx = cx + Dirs[d].dx;
                    int ny = cy + Dirs[d].dy;
                    if (!InBounds(nx, ny) || !walkable.Get(nx, ny)) continue;

                    Fix64 moveCost = d < 4 ? StraightCost : DiagonalCost;
                    if (unitCount != null)
                        moveCost = moveCost + Fix64.FromInt(unitCount.Get(nx, ny));

                    int neighbor = CellIdx(nx, ny);
                    Fix64 tentativeG = gCost[current] + moveCost;

                    if (tentativeG.RawValue < gCost[neighbor].RawValue)
                    {
                        gCost[neighbor] = tentativeG;
                        parent[neighbor] = current;
                        Fix64 f = tentativeG + Heuristic(nx, ny, goalX, goalY);
                        open.Push(f, neighbor);
                    }
                }
            }

            return default;
        }
        finally
        {
            ArrayPool<Fix64>.Shared.Return(gCost);
            ArrayPool<int>.Shared.Return(parent);
        }
    }

    private DetPath ReconstructPath(int[] parent, int start, int goal)
    {
        var path = new List<int>();
        int cur = goal;
        while (cur != start && cur >= 0)
        {
            path.Add(cur);
            cur = parent[cur];
        }
        path.Add(start);
        path.Reverse();
        return new DetPath { Steps = path.ToArray(), Length = path.Count, CurrentStep = 0 };
    }

    private Fix64 Heuristic(int x, int y, int gx, int gy)
    {
        // Chebyshev distance × StraightCost (10)
        int dx = Math.Abs(x - gx);
        int dy = Math.Abs(y - gy);
        return Fix64.FromInt(10 * Math.Max(dx, dy));
    }

    private int CellIdx(int x, int y) => y * _width + x;
    private bool InBounds(int x, int y) => (uint)x < (uint)_width && (uint)y < (uint)_height;
}
