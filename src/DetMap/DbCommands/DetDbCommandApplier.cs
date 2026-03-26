using DetMap.Core;

namespace DetMap.DbCommands;

public static class DetDbCommandApplier
{
    public static DetDbApplyResult ApplyFrame(DetSpatialDatabase database, DetDbCommandList commandList)
    {
        DetDbApplyResult result = ApplyToNextFrame(database, commandList);
        database.CommitNextFrame();
        return result;
    }

    private static DetDbApplyResult ApplyInPlace(DetSpatialDatabase database, DetDbCommandList commandList)
    {
        DetDbChangeSummary summary = commandList.BuildSummary();
        foreach (var command in commandList.Commands)
            command.ApplyTo(database);

        return new DetDbApplyResult
        {
            Tick = database.Tick,
            CommandCount = commandList.Count,
            Summary = summary,
            StateHashHex = database.ComputeStateHashHex(),
            FrameHashHex = database.ComputeFrameHashHex(),
        };
    }

    public static DetDbApplyResult ApplyToNextFrame(DetSpatialDatabase database, DetDbCommandList commandList)
    {
        var nextFrame = database.HasNextFrame
            ? database.GetNextFrame()
            : database.PrepareNextFrame();

        return ApplyInPlace(nextFrame, commandList);
    }

    public static DetDbApplyResult ApplyToPreparedNextFrame(DetSpatialDatabase database, DetDbCommandList commandList)
        => ApplyInPlace(database.GetNextFrame(), commandList);
}
