public partial class AIBenchmarkManager
{
    private void AppendAblationFinalComparison()
    {
        AppendReport("");

        AppendReport(
            "=================================================="
        );

        AppendReport(
            "So sánh mức độ cắt bỏ tích lũy"
        );

        AppendReport(
            "=================================================="
        );

        int[] depths =
            { 2, 3, 4 };

        for (
            int d = 0;
            d < depths.Length;
            d++)
        {
            int depth =
                depths[d];

            bool enabled =
                (
                    depth == 2 &&
                    benchmarkDepth2
                ) ||
                (
                    depth == 3 &&
                    benchmarkDepth3
                ) ||
                (
                    depth == 4 &&
                    benchmarkDepth4
                );

            if (!enabled)
            {
                continue;
            }

            AppendReport("");
            AppendReport(
                $"DEPTH {depth}"
            );

            AppendReport(
                "--------------------------------------------------"
            );

            for (
                int i = 0;
                i < _ablationConfigurations.Length;
                i++)
            {
                string configuration =
                    _ablationConfigurations[i];

                AblationStatistics stats =
                    _ablationStatistics[
                        configuration
                    ];

                DepthProjection projection =
                    CalculateDepthProjection(
                        stats,
                        depth
                    );

                if (
                    projection.PositionCount <= 0)
                {
                    continue;
                }

                string nodeDelta = "N/A";
                string timeDelta = "N/A";

                if (i > 0)
                {
                    AblationStatistics previousStats =
                        _ablationStatistics[
                            _ablationConfigurations[i - 1]
                        ];

                    DepthProjection previous =
                        CalculateDepthProjection(
                            previousStats,
                            depth
                        );

                    if (
                        previous.PositionCount > 0 &&
                        previous.AverageNodes > 0.0)
                    {
                        double reduction =
                            (
                                1.0 -
                                projection.AverageNodes /
                                previous.AverageNodes
                            ) *
                            100.0;

                        nodeDelta =
                            $"{reduction:F2}%";
                    }

                    if (
                        previous.AverageTime > 0.0)
                    {
                        double reduction =
                            (
                                1.0 -
                                projection.AverageTime /
                                previous.AverageTime
                            ) *
                            100.0;

                        timeDelta =
                            $"{reduction:F2}%";
                    }
                }

                AppendReport(
                    $"{configuration,-15} | " +
                    $"Nodes={projection.AverageNodes:F2} | " +
                    $"Time={projection.AverageTime:F6}s | " +
                    $"NPS={projection.NPS:F0} | " +
                    $"Node Δ={nodeDelta} | " +
                    $"Time Δ={timeDelta}"
                );

                AppendReport(
                    $"{string.Empty,15} | " +
                    $"TT={projection.TTHitRate:F2}% | " +
                    $"Cutoff={projection.CutoffRate:F2}% | " +
                    $"PV={projection.AveragePV:F2}"
                );

                if (
                    configuration ==
                    CONFIG_ASPIRATION)
                {
                    AppendReport(
                        $"{string.Empty,15} | " +
                        $"Asp Attempts={projection.AspirationAttempts:F2} | " +
                        $"Asp Re-search={projection.AspirationReSearchRate:F2}% | " +
                        $"Asp Success={projection.AspirationSuccessRate:F2}%"
                    );
                }
            }
        }
    }

    private void AppendAblationInterpretation()
    {
        AppendReport("");

        AppendReport(
            "=================================================="
        );

        AppendReport(
            "INTERPRETATION"
        );

        AppendReport(
            "=================================================="
        );

        AppendReport(
            "- Baseline uses plain iterative-deepening alpha-beta search."
        );

        AppendReport(
            "- + History adds the history heuristic for move ordering."
        );

        AppendReport(
            "- + Killer adds killer-move ordering on top of History."
        );

        AppendReport(
            "- + TT adds the transposition table on top of History + Killer."
        );

        AppendReport(
            "- + Aspiration adds aspiration-window search on top of the previous configuration."
        );

        AppendReport(
            "- Each stage keeps the same benchmark positions and search depth."
        );

        AppendReport(
            "- Node and time differences are measured incrementally against the immediately preceding stage."
        );

        AppendReport(
            "- TT Hit Rate measures the proportion of TT lookups that were hits."
        );

        AppendReport(
            "- PV Completeness measures whether the reported principal variation reaches the completed search depth."
        );

        AppendReport(
            "- Aspiration Re-search Rate measures the proportion of aspiration attempts that required another search."
        );

        AppendReport(
            "- Aspiration Success Rate measures the proportion of aspiration attempts completed without re-search."
        );

        AppendReport(
            "- A positive node/time reduction indicates an efficiency improvement relative to the preceding configuration."
        );

        AppendReport(
            "- A negative reduction indicates that the added technique increased measured search cost for that stage."
        );
    }
}
