using System;
using System.Collections.Generic;

public partial class AIBenchmarkManager
{
    // =========================================================
    // COMMON TYPES
    // =========================================================

    protected struct BenchmarkPosition
    {
        public GameState State;
        public int Seed;
        public int OpeningPlies;
    }

    // =========================================================
    // TYPES
    // =========================================================

    protected struct DepthProjection
    {
        public int PositionCount;

        public double AverageNodes;
        public double AverageTime;
        public double NPS;
        public double AveragePV;

        public double TTHitRate;
        public double CutoffRate;

        public double AspirationAttempts;
        public double AspirationReSearchRate;
        public double AspirationSuccessRate;
    }

    protected class AblationStatistics
    {
        public readonly string Configuration;

        public int PositionCount;

        public long TotalNodes;
        public long TotalLeafNodes;

        public long TotalTTHits;
        public long TotalTTMisses;

        public long TotalCutoffs;

        public long TotalPVLength;

        public double TotalSearchTime;

        public long TotalAspirationAttempts;
        public long TotalAspirationFailHigh;
        public long TotalAspirationFailLow;
        public long TotalAspirationReSearches;

        public int CompletedDepthSuccess;
        public int CompletedDepthMismatch;

        public int CompletePVCount;

        public readonly List<int>
            DepthSamples =
                new List<int>();

        public readonly List<double>
            SearchTimes =
                new List<double>();

        public readonly List<double>
            NodeSamples =
                new List<double>();

        public readonly List<double>
            LeafSamples =
                new List<double>();

        public readonly List<double>
            PVSamples =
                new List<double>();

        public readonly List<double>
            PVCompletenessSamples =
                new List<double>();

        public readonly Dictionary<int, DepthAblationStatistics>
            DepthStatistics =
                new Dictionary<int, DepthAblationStatistics>();

        public AblationStatistics(
            string configuration)
        {
            Configuration = configuration;
        }

        public DepthAblationStatistics
            GetOrCreateDepth(
                int depth)
        {
            DepthAblationStatistics stats;

            if (!DepthStatistics.TryGetValue(
                depth,
                out stats))
            {
                stats =
                    new DepthAblationStatistics();

                stats.Depth = depth;

                DepthStatistics.Add(
                    depth,
                    stats
                );
            }

            return stats;
        }
    }

    protected class DepthAblationStatistics
    {
        public int Depth;

        public int PositionCount;

        public long TotalNodes;
        public long TotalLeafNodes;

        public long TotalTTHits;
        public long TotalTTMisses;

        public long TotalCutoffs;

        public long TotalPVLength;

        public double TotalSearchTime;

        public long TotalAspirationAttempts;
        public long TotalAspirationFailHigh;
        public long TotalAspirationFailLow;
        public long TotalAspirationReSearches;

        public int CompletedDepthSuccess;
        public int CompletedDepthMismatch;

        public int CompletePVCount;

        public readonly List<double>
            SearchTimes =
                new List<double>();

        public readonly List<double>
            NodeSamples =
                new List<double>();

        public readonly List<double>
            LeafSamples =
                new List<double>();

        public readonly List<double>
            PVSamples =
                new List<double>();

        public readonly List<double>
            PVCompletenessSamples =
                new List<double>();
    }

    // =========================================================
    // TYPES
    // =========================================================

    private struct MoveQualitySample
    {
        public int Depth;
        public EPlayer Side;
        public StateMove BestMove;
        public float BestScore;
        public long Nodes;
        public double SearchTimeSeconds;
        public double NPS;
        public int CompletedDepth;
        public List<StateMove> PV;
        public int PVLength;
        public bool PVComplete;
    }

    private class MoveQualityDepthStatistics
    {
        public int Depth;
        public int SampleCount;

        public long TotalNodes;
        public double TotalSearchTime;
        public double TotalScore;
        public long TotalPVLength;
        public int PVCompleteCount;

        public readonly List<double>
            ScoreSamples =
                new List<double>();

        public readonly List<double>
            SearchTimeSamples =
                new List<double>();

        public readonly List<double>
            NodeSamples =
                new List<double>();

        public readonly List<double>
            NPSSamples =
                new List<double>();

        public readonly List<double>
            PVLengthSamples =
                new List<double>();

        public MoveQualityDepthStatistics(
            int depth)
        {
            Depth = depth;
        }
    }

    private class MoveQualityComparisonStatistics
    {
        public int LowerDepth;
        public int HigherDepth;
        public int PositionCount;
        public int SameBestMoveCount;
        public double PVAgreementSum;

        public readonly List<double>
            ScoreDifferenceSamples =
                new List<double>();

        public readonly List<double>
            AbsoluteScoreDifferenceSamples =
                new List<double>();

        public readonly List<double>
            PVAgreementSamples =
                new List<double>();

        public MoveQualityComparisonStatistics(
            int lowerDepth,
            int higherDepth)
        {
            LowerDepth = lowerDepth;
            HigherDepth = higherDepth;
        }
    }

    [Serializable]
    protected class DivergencePosition2
    {
        public string CaseId;

        public int PositionNumber;

        public int Seed;

        public int PreplayedPlies;

        public EPlayer SideToMove;

        [NonSerialized]
        public GameState SourceState;

        [NonSerialized]
        public StateMove D2Move;

        [NonSerialized]
        public StateMove D3Move;

        [NonSerialized]
        public StateMove D4Move;

        public bool HasD2;
        public bool HasD3;
        public bool HasD4;

        public bool D2vsD3Different;
        public bool D3vsD4Different;

        public bool IsRealDivergence;
    }

    protected readonly List<DivergencePosition2>
        _divergencePositions2 =
        new List<DivergencePosition2>();

    // =========================================================
    // TYPES
    // =========================================================

    private enum GameOutcome
    {
        WHITE_WIN,
        BLACK_WIN,
        DRAW
    }

    private struct GameResult
    {
        public bool Completed;

        public GameOutcome Result;

        public int PositionIndex;
        public int GameNumber;
        public int Seed;

        public int LowerDepth;
        public int HigherDepth;

        public bool HigherDepthIsWhite;

        public int Plies;

        public long TotalNodes;

        public double TotalSearchTime;

        public double GameTimeSeconds;
    }

    private class DepthPairStatistics
    {
        public readonly string PairName;

        public readonly int LowerDepth;
        public readonly int HigherDepth;

        public int CompletedGames;

        public int HigherDepthWins;
        public int LowerDepthWins;
        public int Draws;

        public long TotalPlies;
        public long TotalNodes;

        public double TotalSearchTime;
        public double TotalGameTime;

        public int PairedPositionCount;

        public int SideBalancedGameCount;

        public int BothHigherDepthWins;
        public int SplitOutcomeGames;
        public int BothLowerDepthWins;
        public int BothDrawGames;

        public DepthPairStatistics(
            string pairName,
            int lowerDepth,
            int higherDepth)
        {
            PairName =
                pairName;

            LowerDepth =
                lowerDepth;

            HigherDepth =
                higherDepth;
        }
    }
}
