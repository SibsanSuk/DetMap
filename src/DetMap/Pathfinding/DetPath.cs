namespace DetMap.Pathfinding;

public struct DetPath
{
    public int[]? Steps;     // cell indices (y * width + x) from start to goal
    public int Length;
    public int CurrentStep;

    public bool IsValid => Steps != null && Length > 0;
    public bool IsComplete => !IsValid || CurrentStep >= Length - 1;

    public void Advance()
    {
        if (!IsComplete) CurrentStep++;
    }

    public (int x, int y) Current(int width)
    {
        if (!IsValid) return (-1, -1);
        int idx = Steps![CurrentStep];
        return (idx % width, idx / width);
    }

    public (int x, int y) Peek(int width)
    {
        if (!IsValid || IsComplete) return (-1, -1);
        int idx = Steps![CurrentStep + 1];
        return (idx % width, idx / width);
    }
}
