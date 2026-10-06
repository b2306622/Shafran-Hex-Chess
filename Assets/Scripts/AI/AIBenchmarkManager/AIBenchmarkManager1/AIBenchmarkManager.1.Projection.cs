public partial class AIBenchmarkManager
{
    private DepthProjection CalculateDepthProjection(
        AblationStatistics stats,
        int depth)
    {
        DepthProjection projection =
            new DepthProjection();

        if (stats == null)
        {
            return projection;
        }

        DepthAblationStatistics depthStats;

        if (
            !stats.DepthStatistics.TryGetValue(
                depth,
                out depthStats))
        {
            return projection;
        }

        if (
            depthStats == null ||
            depthStats.PositionCount <= 0)
        {
            return projection;
        }

        projection.PositionCount =
            depthStats.PositionCount;

        projection.AverageNodes =
            CalculateMean(
                depthStats.NodeSamples
            );

        projection.AverageTime =
            CalculateMean(
                depthStats.SearchTimes
            );

        projection.AveragePV =
            CalculateMean(
                depthStats.PVSamples
            );

        projection.NPS =
            projection.AverageTime > 0.0
                ? projection.AverageNodes /
                  projection.AverageTime
                : projection.AverageNodes;

        long totalTT =
            depthStats.TotalTTHits +
            depthStats.TotalTTMisses;

        projection.TTHitRate =
            totalTT > 0
                ? (double)
                    depthStats.TotalTTHits /
                    totalTT *
                    100.0
                : 0.0;

        projection.CutoffRate =
            depthStats.TotalNodes > 0
                ? (double)
                    depthStats.TotalCutoffs /
                    depthStats.TotalNodes *
                    100.0
                : 0.0;

        projection.AspirationAttempts =
            depthStats.PositionCount > 0
                ? (double)
                    depthStats.TotalAspirationAttempts /
                    depthStats.PositionCount
                : 0.0;

        projection.AspirationReSearchRate =
            depthStats.TotalAspirationAttempts > 0
                ? (double)
                    depthStats.TotalAspirationReSearches /
                    depthStats.TotalAspirationAttempts *
                    100.0
                : 0.0;

        projection.AspirationSuccessRate =
            depthStats.TotalAspirationAttempts > 0
                ?
                (
                    1.0 -
                    (
                        (double)
                        depthStats.TotalAspirationReSearches /
                        depthStats.TotalAspirationAttempts
                    )
                ) *
                100.0
                : 0.0;

        return projection;
    }
}
