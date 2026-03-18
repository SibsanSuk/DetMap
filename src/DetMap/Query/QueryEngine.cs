using DetMap.Core;

namespace DetMap.Query;

public delegate bool CellFilter(DetGrid grid, int x, int y);

public static class QueryEngine
{
    public static int RectQuery(
        DetGrid grid,
        int minX, int minY, int maxX, int maxY,
        CellFilter predicate,
        CellHit[] resultBuffer)
    {
        int count = 0;
        for (int y = minY; y <= maxY && count < resultBuffer.Length; y++)
        for (int x = minX; x <= maxX && count < resultBuffer.Length; x++)
        {
            if (!grid.InBounds(x, y)) continue;
            if (predicate(grid, x, y))
                resultBuffer[count++] = new CellHit(x, y);
        }
        return count;
    }

    public static int RadiusQuery(
        DetGrid grid,
        int cx, int cy, int radius,
        CellFilter predicate,
        CellHit[] resultBuffer)
    {
        int count = 0;
        int r2 = radius * radius;
        for (int y = cy - radius; y <= cy + radius && count < resultBuffer.Length; y++)
        for (int x = cx - radius; x <= cx + radius && count < resultBuffer.Length; x++)
        {
            if (!grid.InBounds(x, y)) continue;
            int dx = x - cx, dy = y - cy;
            if (dx * dx + dy * dy > r2) continue;
            if (predicate(grid, x, y))
                resultBuffer[count++] = new CellHit(x, y);
        }
        return count;
    }

    public static int FloodFill(
        DetGrid grid,
        int startX, int startY,
        CellFilter canSpread,
        CellHit[] resultBuffer)
    {
        if (!grid.InBounds(startX, startY) || !canSpread(grid, startX, startY))
            return 0;

        int count = 0;
        var visited = new HashSet<int>();
        var queue = new Queue<(int x, int y)>();
        queue.Enqueue((startX, startY));

        while (queue.Count > 0 && count < resultBuffer.Length)
        {
            var (x, y) = queue.Dequeue();
            int key = y * grid.Width + x;
            if (!visited.Add(key)) continue;

            resultBuffer[count++] = new CellHit(x, y);

            foreach (var (dx, dy) in new[] { (0,-1),(1,0),(0,1),(-1,0) })
            {
                int nx = x + dx, ny = y + dy;
                if (grid.InBounds(nx, ny) && !visited.Contains(ny * grid.Width + nx) && canSpread(grid, nx, ny))
                    queue.Enqueue((nx, ny));
            }
        }
        return count;
    }
}
