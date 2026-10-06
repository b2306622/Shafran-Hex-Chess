using System;
using System.Collections.Generic;
using UnityEngine;

public partial class AIBenchmarkManager
{
    private List<BenchmarkPosition>
        Generate3BenchmarkPositions()
    {
        List<BenchmarkPosition> positions =
            new List<BenchmarkPosition>();

        int positionCount =
            Mathf.Max(
                1,
                benchmark3Positions
            );

        int openingPlies =
            Mathf.Max(
                0,
                benchmark3RandomOpeningPlies
            );

        for (
            int i = 0;
            i < positionCount;
            i++)
        {
            int seed =
                benchmark3RandomSeed +
                (
                    i * 1009
                );

            GameState state =
                _initialState.Clone();

            if (state == null)
            {
                continue;
            }

            System.Random random =
                new System.Random(
                    seed
                );

            MoveGenerator moveGenerator =
                new MoveGenerator();

            bool valid =
                CreateRandomPosition(
                    state,
                    moveGenerator,
                    random,
                    openingPlies
                );

            if (!valid)
            {
                AppendReport(
                    $"Position {i + 1}: " +
                    $"SKIPPED | Seed={seed}"
                );

                continue;
            }

            positions.Add(
                new BenchmarkPosition
                {
                    State = state,
                    Seed = seed,
                    OpeningPlies =
                        openingPlies
                }
            );
        }

        return positions;
    }


    private DepthPairStatistics
        Run3DepthPairBenchmark(
            List<BenchmarkPosition> positions,
            int lowerDepth,
            int higherDepth,
            string pairName,
            int maxPlies)
    {
        DepthPairStatistics statistics =
            new DepthPairStatistics(
                pairName,
                lowerDepth,
                higherDepth
            );

        if (
            positions == null ||
            positions.Count == 0)
        {
            return statistics;
        }

        AppendReport("");

        AppendReport(
            "=================================================="
        );

        AppendReport(
            $"DEPTH MATCHUP | {pairName}"
        );

        AppendReport(
            "=================================================="
        );

        AppendReport(
            "Positions Tested        : " +
            positions.Count
        );

        AppendReport(
            "Games / Position        : " +
            (
                benchmark3SwapSides
                    ? "2"
                    : "1"
            )
        );

        AppendReport(
            "Expected Games          : " +
            (
                benchmark3SwapSides
                    ? positions.Count * 2
                    : positions.Count
            )
        );

        AppendReport(
            "Lower Search Depth      : D" +
            lowerDepth
        );

        AppendReport(
            "Higher Search Depth     : D" +
            higherDepth
        );

        AppendReport(
            "Comparison Unit         : SEARCH DEPTH"
        );

        AppendReport(
            "Color Effects           : CONTROLLED BY SIDE SWAP"
        );

        AppendReport(
            "Paired Design           : SAME POSITION"
        );

        AppendReport("");

        for (
            int positionIndex = 0;
            positionIndex < positions.Count;
            positionIndex++)
        {
            if (!_benchmarkRunning)
            {
                return statistics;
            }

            BenchmarkPosition position =
                positions[positionIndex];

            GameResult firstGame =
                Run3SingleDepthMatch(
                    position,
                    lowerDepth,
                    higherDepth,
                    true,
                    maxPlies,
                    positionIndex,
                    1
                );

            Record3DepthResult(
                statistics,
                firstGame
            );

            if (log3EachGame)
            {
                Append3DepthGameSummary(
                    firstGame
                );
            }

            if (benchmark3SwapSides)
            {
                GameResult secondGame =
                    Run3SingleDepthMatch(
                        position,
                        lowerDepth,
                        higherDepth,
                        false,
                        maxPlies,
                        positionIndex,
                        2
                    );

                Record3DepthResult(
                    statistics,
                    secondGame
                );

                if (
                    firstGame.Completed &&
                    secondGame.Completed)
                {
                    statistics.SideBalancedGameCount += 2;

                    Update3PairedOutcome(
                        statistics,
                        firstGame,
                        secondGame
                    );
                }

                if (log3EachGame)
                {
                    Append3DepthGameSummary(
                        secondGame
                    );
                }
            }
            else if (firstGame.Completed)
            {
                statistics.SideBalancedGameCount++;
            }
        }

        Append3DepthPairSummary(
            statistics
        );

        return statistics;
    }


    private GameResult
        Run3SingleDepthMatch(
            BenchmarkPosition position,
            int lowerDepth,
            int higherDepth,
            bool higherDepthIsWhite,
            int maxPlies,
            int positionIndex,
            int gameNumber)
    {
        GameState state =
            position.State.Clone();

        if (state == null)
        {
            return new GameResult
            {
                Completed = false,

                PositionIndex =
                    positionIndex,

                GameNumber =
                    gameNumber,

                Seed =
                    position.Seed,

                LowerDepth =
                    lowerDepth,

                HigherDepth =
                    higherDepth,

                HigherDepthIsWhite =
                    higherDepthIsWhite
            };
        }

        MoveGenerator moveGenerator =
            new MoveGenerator();

        long totalNodes = 0;

        double totalSearchTime =
            0.0;

        DateTime gameStart =
            DateTime.UtcNow;

        for (
            int ply = 0;
            ply < maxPlies;
            ply++)
        {
            List<StateMove> legalMoves =
                moveGenerator.GenerateLegalMoves(
                    state,
                    state.SideToMove
                );

            if (
                legalMoves == null ||
                legalMoves.Count == 0)
            {
                GameOutcome outcome =
                    state.SideToMove ==
                    EPlayer.WHITE
                        ? GameOutcome.BLACK_WIN
                        : GameOutcome.WHITE_WIN;

                return new GameResult
                {
                    Completed = true,

                    Result = outcome,

                    PositionIndex =
                        positionIndex,

                    GameNumber =
                        gameNumber,

                    Seed =
                        position.Seed,

                    LowerDepth =
                        lowerDepth,

                    HigherDepth =
                        higherDepth,

                    HigherDepthIsWhite =
                        higherDepthIsWhite,

                    Plies =
                        ply,

                    TotalNodes =
                        totalNodes,

                    TotalSearchTime =
                        totalSearchTime,

                    GameTimeSeconds =
                        (
                            DateTime.UtcNow -
                            gameStart
                        ).TotalSeconds
                };
            }

            bool currentSideIsWhite =
                state.SideToMove ==
                EPlayer.WHITE;

            bool currentSideIsHigherDepth =
                currentSideIsWhite ==
                higherDepthIsWhite;

            int currentDepth =
                currentSideIsHigherDepth
                    ? higherDepth
                    : lowerDepth;

            MinimaxAI ai =
                new MinimaxAI(
                    state.SideToMove,
                    currentDepth,
                    BENCHMARK3_HISTORY,
                    BENCHMARK3_KILLER,
                    BENCHMARK3_TT,
                    BENCHMARK3_ASPIRATION
                );

            DateTime searchStart =
                DateTime.UtcNow;

            StateMove bestMove =
                ai.GetBestStateMove(
                    state
                );

            double searchTime =
                (
                    DateTime.UtcNow -
                    searchStart
                ).TotalSeconds;

            SearchStatistics searchStatistics =
                ai.Statistics;

            if (searchStatistics != null)
            {
                totalNodes +=
                    searchStatistics.Nodes;
            }

            totalSearchTime +=
                searchTime;

            /*
             * StateMove is a struct in this project.
             * Therefore do NOT compare it with null.
             */
            moveGenerator.MakeMove(
                state,
                bestMove
            );
        }

        return new GameResult
        {
            Completed = true,

            Result =
                GameOutcome.DRAW,

            PositionIndex =
                positionIndex,

            GameNumber =
                gameNumber,

            Seed =
                position.Seed,

            LowerDepth =
                lowerDepth,

            HigherDepth =
                higherDepth,

            HigherDepthIsWhite =
                higherDepthIsWhite,

            Plies =
                maxPlies,

            TotalNodes =
                totalNodes,

            TotalSearchTime =
                totalSearchTime,

            GameTimeSeconds =
                (
                    DateTime.UtcNow -
                    gameStart
                ).TotalSeconds
        };
    }


    /*
     * ============================================================
     * RESULT RECORDING
     * ============================================================
     */
    private void Record3DepthResult(
        DepthPairStatistics statistics,
        GameResult result)
    {
        if (
            statistics == null ||
            !result.Completed)
        {
            return;
        }

        statistics.CompletedGames++;

        statistics.TotalPlies +=
            result.Plies;

        statistics.TotalNodes +=
            result.TotalNodes;

        statistics.TotalSearchTime +=
            result.TotalSearchTime;

        statistics.TotalGameTime +=
            result.GameTimeSeconds;

        if (
            result.Result ==
            GameOutcome.DRAW)
        {
            statistics.Draws++;

            return;
        }

        if (IsHigherDepthWin(result))
        {
            statistics.HigherDepthWins++;
        }
        else
        {
            statistics.LowerDepthWins++;
        }
    }


    /*
     * ============================================================
     * PAIRED OUTCOME
     * ============================================================
     */
    private void Update3PairedOutcome(
        DepthPairStatistics statistics,
        GameResult firstGame,
        GameResult secondGame)
    {
        if (
            statistics == null ||
            !firstGame.Completed ||
            !secondGame.Completed)
        {
            return;
        }

        bool firstDraw =
            firstGame.Result ==
            GameOutcome.DRAW;

        bool secondDraw =
            secondGame.Result ==
            GameOutcome.DRAW;

        bool firstHigherWin =
            !firstDraw &&
            IsHigherDepthWin(firstGame);

        bool secondHigherWin =
            !secondDraw &&
            IsHigherDepthWin(secondGame);

        statistics.PairedPositionCount++;

        /*
         * Both games are draws.
         */
        if (
            firstDraw &&
            secondDraw)
        {
            statistics.BothDrawGames++;

            return;
        }

        /*
         * Higher depth wins both
         * side assignments.
         */
        if (
            firstHigherWin &&
            secondHigherWin)
        {
            statistics.BothHigherDepthWins++;

            return;
        }

        /*
         * Lower depth wins both
         * side assignments.
         */
        if (
            !firstDraw &&
            !secondDraw &&
            !firstHigherWin &&
            !secondHigherWin)
        {
            statistics.BothLowerDepthWins++;

            return;
        }

        /*
         * Mixed result:
         *
         * Higher + Lower
         * Higher + Draw
         * Lower + Draw
         */
        statistics.SplitOutcomeGames++;
    }
}