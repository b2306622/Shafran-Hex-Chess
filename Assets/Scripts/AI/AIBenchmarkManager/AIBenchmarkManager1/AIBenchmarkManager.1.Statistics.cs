public partial class AIBenchmarkManager
{
    private void AppendAblationConfigurationSummary(
        AblationStatistics stats,
        int depth)
    {
        if (stats == null)
        {
            return;
        }

        DepthAblationStatistics depthStats;

        if (!stats.DepthStatistics.TryGetValue(
            depth,
            out depthStats))
        {
            return;
        }

        if (
            depthStats == null ||
            depthStats.PositionCount <= 0)
        {
            return;
        }

        double averageNodes =
            (double)
            depthStats.TotalNodes /
            depthStats.PositionCount;

        double averageLeaf =
            (double)
            depthStats.TotalLeafNodes /
            depthStats.PositionCount;

        double averageTime =
            depthStats.TotalSearchTime /
            depthStats.PositionCount;

        double aggregateNPS =
            depthStats.TotalSearchTime > 0.0
                ? depthStats.TotalNodes /
                  depthStats.TotalSearchTime
                : depthStats.TotalNodes;

        long totalTT =
            depthStats.TotalTTHits +
            depthStats.TotalTTMisses;

        double ttHitRate =
            totalTT > 0
                ? (double)
                    depthStats.TotalTTHits /
                    totalTT *
                    100.0
                : 0.0;

        double cutoffRate =
            depthStats.TotalNodes > 0
                ? (double)
                    depthStats.TotalCutoffs /
                    depthStats.TotalNodes *
                    100.0
                : 0.0;

        double averagePV =
            (double)
            depthStats.TotalPVLength /
            depthStats.PositionCount;

        double averageAspirationAttempts =
            (double)
            depthStats.TotalAspirationAttempts /
            depthStats.PositionCount;

        double aspirationReSearchRate =
            depthStats.TotalAspirationAttempts > 0
                ? (double)
                    depthStats.TotalAspirationReSearches /
                    depthStats.TotalAspirationAttempts *
                    100.0
                : 0.0;

        double aspirationSuccessRate =
            depthStats.TotalAspirationAttempts > 0
                ? (
                    1.0 -
                    (
                        (double)
                        depthStats.TotalAspirationReSearches /
                        depthStats.TotalAspirationAttempts
                    )
                ) *
                100.0
                : 0.0;

        double pvCompleteness =
            depthStats.PVCompletenessSamples.Count > 0
                ? CalculateMean(
                    depthStats.PVCompletenessSamples
                )
                : 0.0;

        double sdTime =
            CalculateSampleStandardDeviation(
                depthStats.SearchTimes
            );

        double ciTime =
            CalculateConfidenceInterval95(
                depthStats.SearchTimes
            );

        double sdNodes =
            CalculateSampleStandardDeviation(
                depthStats.NodeSamples
            );

        double ciNodes =
            CalculateConfidenceInterval95(
                depthStats.NodeSamples
            );

        AppendReport("");
        AppendReport(
            $"SUMMARY | {stats.Configuration} | DEPTH {depth}"
        );

        AppendReport(
            $"Positions Tested          : " +
            $"{depthStats.PositionCount}"
        );

        AppendReport(
            $"Average Nodes             : " +
            $"{averageNodes:F2}"
        );

        AppendReport(
            $"Average Leaf Nodes        : " +
            $"{averageLeaf:F2}"
        );

        AppendReport(
            $"Average Search Time       : " +
            $"{averageTime:F6}s"
        );

        AppendReport(
            $"Sample SD Search Time     : " +
            $"{sdTime:F6}s"
        );

        AppendReport(
            $"95% CI Search Time        : " +
            $"±{ciTime:F6}s"
        );

        AppendReport(
            $"Sample SD Nodes           : " +
            $"{sdNodes:F2}"
        );

        AppendReport(
            $"95% CI Nodes              : " +
            $"±{ciNodes:F2}"
        );

        AppendReport(
            $"Aggregate NPS             : " +
            $"{aggregateNPS:F0}"
        );

        AppendReport(
            $"TT Hits                   : " +
            $"{depthStats.TotalTTHits}"
        );

        AppendReport(
            $"TT Misses                 : " +
            $"{depthStats.TotalTTMisses}"
        );

        AppendReport(
            $"TT Hit Rate               : " +
            $"{ttHitRate:F2}%"
        );

        AppendReport(
            $"Average Cutoffs           : " +
            $"{(double)depthStats.TotalCutoffs / depthStats.PositionCount:F2}"
        );

        AppendReport(
            $"Cutoff Rate               : " +
            $"{cutoffRate:F2}%"
        );

        AppendReport(
            $"Average PV Length         : " +
            $"{averagePV:F2}"
        );

        AppendReport(
            $"PV Completeness           : " +
            $"{pvCompleteness:F2}%"
        );

        AppendReport(
            $"Completed Depth Success   : " +
            $"{depthStats.CompletedDepthSuccess}/" +
            $"{depthStats.PositionCount}"
        );

        AppendReport(
            $"Completed Depth Mismatch  : " +
            $"{depthStats.CompletedDepthMismatch}"
        );

        AppendReport(
            $"Aspiration Attempts       : " +
            $"{depthStats.TotalAspirationAttempts}"
        );

        AppendReport(
            $"Aspiration Fail High      : " +
            $"{depthStats.TotalAspirationFailHigh}"
        );

        AppendReport(
            $"Aspiration Fail Low       : " +
            $"{depthStats.TotalAspirationFailLow}"
        );

        AppendReport(
            $"Aspiration Re-search      : " +
            $"{depthStats.TotalAspirationReSearches}"
        );

        AppendReport(
            $"Avg Aspiration Attempts   : " +
            $"{averageAspirationAttempts:F2}"
        );

        AppendReport(
            $"Aspiration Re-search Rate : " +
            $"{aspirationReSearchRate:F2}%"
        );

        AppendReport(
            $"Aspiration Success Rate   : " +
            $"{aspirationSuccessRate:F2}%"
        );
    }
}
