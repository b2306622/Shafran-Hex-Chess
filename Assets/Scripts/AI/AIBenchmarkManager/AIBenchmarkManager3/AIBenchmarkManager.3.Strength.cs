using UnityEngine;

public partial class AIBenchmarkManager
{
    private void Run3PlayingStrength()
    {
        if (!enable3Benchmark)
        {
            return;
        }

        int maxPlies =
            Mathf.Max(
                1,
                benchmark3MaxPlies
            );

        System.Collections.Generic.List<BenchmarkPosition>
            positions =
            Generate3BenchmarkPositions();

        if (
            positions == null ||
            positions.Count == 0)
        {
            AppendReport("");

            AppendReport(
                "=================================================="
            );

            AppendReport(
                "AI BENCHMARK"
            );

            AppendReport(
                "No valid benchmark positions generated."
            );

            AppendReport(
                "=================================================="
            );

            return;
        }

        AppendReport("");

        AppendReport(
            "=================================================="
        );

        AppendReport(
            "AI BENCHMARK"
        );

        AppendReport(
            "COMPETITIVE SEARCH-DEPTH EVALUATION"
        );

        AppendReport(
            "PLAYING STRENGTH / DEPTH SENSITIVITY"
        );

        AppendReport(
            "=================================================="
        );

        /*
         * Không dùng multiline interpolated string.
         * Cách này tương thích tốt với C# 9.
         */

        AppendReport(
            "Random Positions        : " +
            positions.Count
        );

        AppendReport(
            "Random Opening Plies    : " +
            Mathf.Max(
                0,
                benchmark3RandomOpeningPlies
            )
        );

        AppendReport(
            "Random Seed             : " +
            benchmark3RandomSeed
        );

        AppendReport(
            "Maximum Plies / Game    : " +
            maxPlies
        );

        AppendReport(
            "Depth Configuration     : " +
            Build3DepthConfigurationText()
        );

        AppendReport(
            "AI Configuration        : " +
            "+ HISTORY + KILLER + TT + ASPIRATION"
        );

        AppendReport(
            "Controlled Variables    : " +
            "Same Position + Same AI + Same Rules"
        );

        AppendReport(
            "Experimental Variable   : " +
            "SEARCH DEPTH"
        );

        AppendReport(
            "Position Generation     : " +
            "RANDOMIZED"
        );

        AppendReport(
            "Paired Game Design      : " +
            (
                benchmark3SwapSides
                    ? "Two games with depth-side swapping"
                    : "Single game per position"
            )
        );

        AppendReport(
            "Primary Comparison      : " +
            "DEPTH, NOT COLOR"
        );

        AppendReport(
            "Depth Pairings          : " +
            Build3DepthPairingsText()
        );

        AppendReport(
            "=================================================="
        );


        /*
         * ==========================================================
         * CREATE STATISTICS
         * ==========================================================
         */

        DepthPairStatistics depth2vs3 =
            new DepthPairStatistics(
                "D2 VS D3",
                DEPTH_2,
                DEPTH_3
            );

        DepthPairStatistics depth3vs4 =
            new DepthPairStatistics(
                "D3 VS D4",
                DEPTH_3,
                DEPTH_4
            );

        DepthPairStatistics depth2vs4 =
            new DepthPairStatistics(
                "D2 VS D4",
                DEPTH_2,
                DEPTH_4
            );


        /*
         * ==========================================================
         * D2 VS D3
         * ==========================================================
         */

        if (
            benchmark3Depth2 &&
            benchmark3Depth3)
        {
            depth2vs3 =
                Run3DepthPairBenchmark(
                    positions,
                    DEPTH_2,
                    DEPTH_3,
                    "D2 VS D3",
                    maxPlies
                );
        }


        /*
         * ==========================================================
         * D3 VS D4
         * ==========================================================
         */

        if (
            benchmark3Depth3 &&
            benchmark3Depth4)
        {
            depth3vs4 =
                Run3DepthPairBenchmark(
                    positions,
                    DEPTH_3,
                    DEPTH_4,
                    "D3 VS D4",
                    maxPlies
                );
        }


        /*
         * ==========================================================
         * D2 VS D4
         * ==========================================================
         */

        if (
            benchmark3Depth2 &&
            benchmark3Depth4)
        {
            depth2vs4 =
                Run3DepthPairBenchmark(
                    positions,
                    DEPTH_2,
                    DEPTH_4,
                    "D2 VS D4",
                    maxPlies
                );
        }


        /*
         * ==========================================================
         * OVERALL COMPARISON
         * ==========================================================
         */

        AppendReport("");

        AppendReport(
            "=================================================="
        );

        AppendReport(
            "OVERALL DEPTH COMPARISON"
        );

        AppendReport(
            "=================================================="
        );

        bool hasComparison = false;


        if (
            benchmark3Depth2 &&
            benchmark3Depth3)
        {
            Append3DepthPairComparison(
                depth2vs3
            );

            hasComparison = true;
        }


        if (
            benchmark3Depth3 &&
            benchmark3Depth4)
        {
            Append3DepthPairComparison(
                depth3vs4
            );

            hasComparison = true;
        }


        if (
            benchmark3Depth2 &&
            benchmark3Depth4)
        {
            Append3DepthPairComparison(
                depth2vs4
            );

            hasComparison = true;
        }


        if (!hasComparison)
        {
            AppendReport(
                "No valid depth pair selected."
            );

            AppendReport(
                "Enable at least two depth options."
            );
        }


        Append3DepthInterpretation(
            depth2vs3,
            depth3vs4,
            depth2vs4
        );


        AppendReport(
            "=================================================="
        );
    }


    /*
     * ============================================================
     * DEPTH WIN DETECTION
     * ============================================================
     *
     * GameResult là struct trong project.
     * Vì vậy không được kiểm tra:
     *
     *     result == null
     *
     * ============================================================
     */
    private bool IsHigherDepthWin(
        GameResult result)
    {
        if (!result.Completed)
        {
            return false;
        }

        if (
            result.Result ==
            GameOutcome.DRAW)
        {
            return false;
        }

        /*
         * Higher depth is WHITE.
         */
        if (result.HigherDepthIsWhite)
        {
            return
                result.Result ==
                GameOutcome.WHITE_WIN;
        }

        /*
         * Higher depth is BLACK.
         */
        return
            result.Result ==
            GameOutcome.BLACK_WIN;
    }
}