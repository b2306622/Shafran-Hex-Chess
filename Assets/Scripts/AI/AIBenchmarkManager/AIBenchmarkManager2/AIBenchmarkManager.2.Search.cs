using System;
using System.Collections.Generic;

public partial class AIBenchmarkManager
{
    private MoveQualitySample Execute2Search(
        BenchmarkPosition position,
        int depth)
    {
        GameState state =
            position.State.Clone();

        EPlayer side =
            state.SideToMove;

        MinimaxAI ai =
            new MinimaxAI(
                side,
                depth,
                BENCHMARK_HISTORY,
                BENCHMARK_KILLER,
                BENCHMARK_TT,
                BENCHMARK_ASPIRATION
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

        SearchStatistics statistics =
            ai.Statistics;

        List<StateMove> pv =
            new List<StateMove>();

        if (
            statistics.PrincipalVariation !=
            null)
        {
            for (
                int i = 0;
                i < statistics.PrincipalVariation.Count;
                i++)
            {
                pv.Add(
                    statistics.PrincipalVariation[i]
                );
            }
        }

        bool pvComplete =
            statistics.CompletedDepth > 0 &&
            statistics.PrincipalVariationLength >=
            statistics.CompletedDepth;

        return new MoveQualitySample
        {
            Depth = depth,
            Side = side,
            BestMove = bestMove,
            BestScore = statistics.BestScore,
            Nodes = statistics.Nodes,
            SearchTimeSeconds = elapsed,
            NPS =
                elapsed > 0.0
                    ? statistics.Nodes /
                      elapsed
                    : statistics.Nodes,
            CompletedDepth =
                statistics.CompletedDepth,
            PV = pv,
            PVLength = pv.Count,
            PVComplete = pvComplete
        };
    }

    private void Accumulate2DepthStatistics(
        MoveQualityDepthStatistics stats,
        MoveQualitySample sample)
    {
        if (stats == null)
        {
            return;
        }

        stats.SampleCount++;

        stats.TotalNodes +=
            sample.Nodes;

        stats.TotalSearchTime +=
            sample.SearchTimeSeconds;

        stats.TotalScore +=
            sample.BestScore;

        stats.TotalPVLength +=
            sample.PVLength;

        stats.ScoreSamples.Add(
            sample.BestScore
        );

        stats.SearchTimeSamples.Add(
            sample.SearchTimeSeconds
        );

        stats.NodeSamples.Add(
            sample.Nodes
        );

        stats.NPSSamples.Add(
            sample.NPS
        );

        stats.PVLengthSamples.Add(
            sample.PVLength
        );

        if (sample.PVComplete)
        {
            stats.PVCompleteCount++;
        }
    }
}
