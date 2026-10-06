using System;
using System.Collections.Generic;
using UnityEngine;

public partial class AIBenchmarkManager
{
    private void InitializeConfigurationStatistics()
    {
        for (
            int i = 0;
            i < _ablationConfigurations.Length;
            i++)
        {
            string configuration =
                _ablationConfigurations[i];

            if (
                !_ablationStatistics.ContainsKey(
                    configuration))
            {
                _ablationStatistics.Add(
                    configuration,
                    new AblationStatistics(
                        configuration
                    )
                );
            }
        }
    }

    private MinimaxAI CreateAblationAI(
        EPlayer player,
        int depth,
        string configuration)
    {
        bool enableHistory = false;
        bool enableKiller = false;
        bool enableTT = false;
        bool enableAspiration = false;

        switch (configuration)
        {
            case CONFIG_BASELINE:
                break;

            case CONFIG_HISTORY:
                enableHistory = true;
                break;

            case CONFIG_KILLER:
                enableHistory = true;
                enableKiller = true;
                break;

            case CONFIG_TT:
                enableHistory = true;
                enableKiller = true;
                enableTT = true;
                break;

            case CONFIG_ASPIRATION:
                enableHistory = true;
                enableKiller = true;
                enableTT = true;
                enableAspiration = true;
                break;

            default:
                Debug.LogError(
                    "[AIBenchmark] Unknown configuration: " +
                    configuration
                );
                break;
        }

        return new MinimaxAI(
            player,
            Mathf.Max(1, depth),
            enableHistory,
            enableKiller,
            enableTT,
            enableAspiration
        );
    }

    private void RunAblationBenchmark(
        List<BenchmarkPosition> positions,
        int depth)
    {
        if (
            positions == null ||
            positions.Count == 0)
        {
            return;
        }

        AppendReport("");
        AppendReport(
            "=================================================="
        );
        AppendReport(
            $"1 ABLATION BENCHMARK | DEPTH {depth}"
        );
        AppendReport(
            "=================================================="
        );

        AppendReport("BASELINE");
        AppendReport("    ");
        AppendReport("+ HISTORY");
        AppendReport("    ");
        AppendReport("+ KILLER");
        AppendReport("    ");
        AppendReport("+ TT");
        AppendReport("    ");
        AppendReport("+ ASPIRATION");
        AppendReport("");

        for (
            int configurationIndex = 0;
            configurationIndex <
            _ablationConfigurations.Length;
            configurationIndex++)
        {
            if (!_benchmarkRunning)
            {
                return;
            }

            string configuration =
                _ablationConfigurations[
                    configurationIndex
                ];

            AblationStatistics stats =
                _ablationStatistics[
                    configuration
                ];

            AppendReport("");
            AppendReport(
                "--------------------------------------------------"
            );
            AppendReport(
                $"{configuration} | DEPTH {depth}"
            );
            AppendReport(
                "--------------------------------------------------"
            );

            for (
                int positionIndex = 0;
                positionIndex < positions.Count;
                positionIndex++)
            {
                if (!_benchmarkRunning)
                {
                    return;
                }

                BenchmarkPosition position =
                    positions[positionIndex];

                GameState state =
                    position.State.Clone();

                MinimaxAI ai =
                    CreateAblationAI(
                        position.State.SideToMove,
                        depth,
                        configuration
                    );

                DateTime start =
                    DateTime.UtcNow;

                StateMove bestMove =
                    ai.GetBestStateMove(
                        state
                    );

                double elapsed =
                    (
                        DateTime.UtcNow -
                        start
                    ).TotalSeconds;

                SearchStatistics searchStats =
                    ai.Statistics;

                RecordAblationSearch(
                    stats,
                    depth,
                    searchStats,
                    elapsed
                );

                if (log1EachPosition)
                {
                    AppendReport("");
                    AppendReport(
                        $"1 POSITION " +
                        $"{positionIndex + 1}/{positions.Count} | " +
                        $"Config={configuration} | " +
                        $"Depth={depth} | " +
                        $"Seed={position.Seed}"
                    );

                    AppendReport(
                        $"Best Move        : " +
                        $"{FormatMove(bestMove)}"
                    );

                    AppendReport(
                        $"Best Score       : " +
                        $"{searchStats.BestScore:F2}"
                    );

                    AppendReport(
                        $"Completed Depth  : " +
                        $"{searchStats.CompletedDepth}"
                    );

                    AppendReport(
                        $"Nodes            : " +
                        $"{searchStats.Nodes}"
                    );

                    AppendReport(
                        $"Leaf Nodes       : " +
                        $"{searchStats.LeafNodes}"
                    );

                    AppendReport(
                        $"Search Time      : " +
                        $"{elapsed:F6}s"
                    );

                    AppendReport(
                        $"NPS              : " +
                        $"{searchStats.NodesPerSecond:F0}"
                    );

                    AppendReport(
                        $"TT Hits          : " +
                        $"{searchStats.TT_Hits}"
                    );

                    AppendReport(
                        $"TT Misses        : " +
                        $"{searchStats.TT_Misses}"
                    );

                    AppendReport(
                        $"TT Hit Rate      : " +
                        $"{searchStats.TTHitRate:F2}%"
                    );

                    AppendReport(
                        $"Cutoffs          : " +
                        $"{searchStats.Cutoffs}"
                    );

                    AppendReport(
                        $"Cutoff Rate      : " +
                        $"{searchStats.CutoffRate:F2}%"
                    );

                    AppendReport(
                        $"PV Length        : " +
                        $"{searchStats.PrincipalVariationLength}"
                    );

                    AppendReport(
                        $"PV               : " +
                        $"{searchStats.GetPrincipalVariationString()}"
                    );

                    AppendReport(
                        $"Aspiration Attempts : " +
                        $"{searchStats.AspirationAttempts}"
                    );

                    AppendReport(
                        $"Aspiration Fail High : " +
                        $"{searchStats.AspirationFailHigh}"
                    );

                    AppendReport(
                        $"Aspiration Fail Low  : " +
                        $"{searchStats.AspirationFailLow}"
                    );

                    AppendReport(
                        $"Aspiration Re-search : " +
                        $"{searchStats.AspirationReSearches}"
                    );

                    AppendReport(
                        $"Aspiration Success   : " +
                        $"{searchStats.AspirationSuccessRate:F2}%"
                    );
                }
            }

            AppendAblationConfigurationSummary(
                stats,
                depth
            );
        }
    }

    private void RecordAblationSearch(
        AblationStatistics stats,
        int depth,
        SearchStatistics searchStats,
        double elapsed)
    {
        if (
            stats == null ||
            searchStats == null)
        {
            return;
        }

        stats.PositionCount++;

        stats.DepthSamples.Add(
            depth
        );

        stats.SearchTimes.Add(
            elapsed
        );

        stats.NodeSamples.Add(
            searchStats.Nodes
        );

        stats.LeafSamples.Add(
            searchStats.LeafNodes
        );

        stats.PVSamples.Add(
            searchStats.PrincipalVariationLength
        );

        stats.TotalNodes +=
            searchStats.Nodes;

        stats.TotalLeafNodes +=
            searchStats.LeafNodes;

        stats.TotalTTHits +=
            searchStats.TT_Hits;

        stats.TotalTTMisses +=
            searchStats.TT_Misses;

        stats.TotalCutoffs +=
            searchStats.Cutoffs;

        stats.TotalSearchTime +=
            elapsed;

        stats.TotalPVLength +=
            searchStats.PrincipalVariationLength;

        stats.TotalAspirationAttempts +=
            searchStats.AspirationAttempts;

        stats.TotalAspirationFailHigh +=
            searchStats.AspirationFailHigh;

        stats.TotalAspirationFailLow +=
            searchStats.AspirationFailLow;

        stats.TotalAspirationReSearches +=
            searchStats.AspirationReSearches;

        if (
            searchStats.CompletedDepth ==
            depth)
        {
            stats.CompletedDepthSuccess++;
        }
        else
        {
            stats.CompletedDepthMismatch++;
        }

        if (
            searchStats.PrincipalVariationLength >=
            searchStats.CompletedDepth)
        {
            stats.CompletePVCount++;
        }

        stats.PVCompletenessSamples.Add(
            searchStats.CompletedDepth > 0
                ? Math.Min(
                    100.0,
                    (
                        (double)
                        searchStats.PrincipalVariationLength /
                        searchStats.CompletedDepth
                    ) * 100.0
                )
                : 0.0
        );

        DepthAblationStatistics depthStats =
            stats.GetOrCreateDepth(
                depth
            );

        depthStats.PositionCount++;

        depthStats.SearchTimes.Add(
            elapsed
        );

        depthStats.NodeSamples.Add(
            searchStats.Nodes
        );

        depthStats.LeafSamples.Add(
            searchStats.LeafNodes
        );

        depthStats.PVSamples.Add(
            searchStats.PrincipalVariationLength
        );

        depthStats.TotalNodes +=
            searchStats.Nodes;

        depthStats.TotalLeafNodes +=
            searchStats.LeafNodes;

        depthStats.TotalTTHits +=
            searchStats.TT_Hits;

        depthStats.TotalTTMisses +=
            searchStats.TT_Misses;

        depthStats.TotalCutoffs +=
            searchStats.Cutoffs;

        depthStats.TotalSearchTime +=
            elapsed;

        depthStats.TotalPVLength +=
            searchStats.PrincipalVariationLength;

        depthStats.TotalAspirationAttempts +=
            searchStats.AspirationAttempts;

        depthStats.TotalAspirationFailHigh +=
            searchStats.AspirationFailHigh;

        depthStats.TotalAspirationFailLow +=
            searchStats.AspirationFailLow;

        depthStats.TotalAspirationReSearches +=
            searchStats.AspirationReSearches;

        if (
            searchStats.CompletedDepth ==
            depth)
        {
            depthStats.CompletedDepthSuccess++;
        }
        else
        {
            depthStats.CompletedDepthMismatch++;
        }

        if (
            searchStats.PrincipalVariationLength >=
            searchStats.CompletedDepth)
        {
            depthStats.CompletePVCount++;
        }

        depthStats.PVCompletenessSamples.Add(
            searchStats.CompletedDepth > 0
                ? Math.Min(
                    100.0,
                    (
                        (double)
                        searchStats.PrincipalVariationLength /
                        searchStats.CompletedDepth
                    ) * 100.0
                )
                : 0.0
        );
    }
}
