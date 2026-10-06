public partial class AIBenchmarkManager
{
    private string Build3DepthConfigurationText()
    {
        string result = "";

        if (benchmark3Depth2)
        {
            result += "D2";
        }

        if (benchmark3Depth3)
        {
            if (result.Length > 0)
            {
                result += ", ";
            }

            result += "D3";
        }

        if (benchmark3Depth4)
        {
            if (result.Length > 0)
            {
                result += ", ";
            }

            result += "D4";
        }

        if (string.IsNullOrEmpty(result))
        {
            result = "NONE";
        }

        return result;
    }

    private string Build3DepthPairingsText()
    {
        System.Collections.Generic.List<string>
            pairings =
            new System.Collections.Generic.List<string>();

        if (
            benchmark3Depth2 &&
            benchmark3Depth3)
        {
            pairings.Add("D2-D3");
        }

        if (
            benchmark3Depth3 &&
            benchmark3Depth4)
        {
            pairings.Add("D3-D4");
        }

        if (
            benchmark3Depth2 &&
            benchmark3Depth4)
        {
            pairings.Add("D2-D4");
        }

        if (pairings.Count == 0)
        {
            return "NONE";
        }

        return string.Join(
            " | ",
            pairings
        );
    }

    private void Append3DepthPairSummary(
        DepthPairStatistics statistics)
    {
        if (
            statistics == null ||
            statistics.CompletedGames <= 0)
        {
            return;
        }

        double higherDepthWinRate =
            (double)
            statistics.HigherDepthWins /
            statistics.CompletedGames *
            100.0;

        double lowerDepthWinRate =
            (double)
            statistics.LowerDepthWins /
            statistics.CompletedGames *
            100.0;

        double drawRate =
            (double)
            statistics.Draws /
            statistics.CompletedGames *
            100.0;

        double higherDepthScoreRate =
            Calculate3ScoreRate(
                statistics
            );

        double averagePlies =
            (double)
            statistics.TotalPlies /
            statistics.CompletedGames;

        double averageNodes =
            (double)
            statistics.TotalNodes /
            statistics.CompletedGames;

        double averageSearchTime =
            statistics.TotalSearchTime /
            statistics.CompletedGames;

        double averageGameTime =
            statistics.TotalGameTime /
            statistics.CompletedGames;

        double aggregateNPS =
            statistics.TotalSearchTime > 0.0
                ? statistics.TotalNodes /
                  statistics.TotalSearchTime
                : 0.0;

        AppendReport("");

        AppendReport(
            $"=============== {statistics.PairName} RESULTS ==============="
        );

        AppendReport(
            $"Lower Depth             : " +
            $"D{statistics.LowerDepth}"
        );

        AppendReport(
            $"Higher Depth            : " +
            $"D{statistics.HigherDepth}"
        );

        AppendReport(
            $"Completed Games         : " +
            $"{statistics.CompletedGames}"
        );

        AppendReport(
            $"Higher Depth Wins       : " +
            $"{statistics.HigherDepthWins}"
        );

        AppendReport(
            $"Lower Depth Wins        : " +
            $"{statistics.LowerDepthWins}"
        );

        AppendReport(
            $"Draws                   : " +
            $"{statistics.Draws}"
        );

        AppendReport(
            $"Higher Depth Win Rate   : " +
            $"{higherDepthWinRate:F2}%"
        );

        AppendReport(
            $"Lower Depth Win Rate    : " +
            $"{lowerDepthWinRate:F2}%"
        );

        AppendReport(
            $"Draw Rate               : " +
            $"{drawRate:F2}%"
        );

        AppendReport(
            $"Higher Depth Score Rate : " +
            $"{higherDepthScoreRate:F2}%"
        );

        AppendReport(
            $"Average Game Length     : " +
            $"{averagePlies:F2} plies"
        );

        AppendReport(
            $"Average Search Time     : " +
            $"{averageSearchTime:F6}s"
        );

        AppendReport(
            $"Average Game Time       : " +
            $"{averageGameTime:F6}s"
        );

        AppendReport(
            $"Average Nodes / Game    : " +
            $"{averageNodes:F2}"
        );

        AppendReport(
            $"Aggregate Search NPS    : " +
            $"{aggregateNPS:F0}"
        );

        AppendReport(
            $"Paired Position Count   : " +
            $"{statistics.PairedPositionCount}"
        );

        AppendReport(
            $"Side-Balanced Games     : " +
            $"{statistics.SideBalancedGameCount}"
        );

        AppendReport(
            $"Both Higher Depth Win   : " +
            $"{statistics.BothHigherDepthWins}"
        );

        AppendReport(
            $"Split Outcome           : " +
            $"{statistics.SplitOutcomeGames}"
        );

        AppendReport(
            $"Both Lower Depth Win    : " +
            $"{statistics.BothLowerDepthWins}"
        );

        AppendReport(
            $"Both Draw               : " +
            $"{statistics.BothDrawGames}"
        );
    }

    private void Append3DepthPairComparison(
        DepthPairStatistics statistics)
    {
        if (
            statistics == null ||
            statistics.CompletedGames <= 0)
        {
            return;
        }

        double higherWinRate =
            (double)
            statistics.HigherDepthWins /
            statistics.CompletedGames *
            100.0;

        double lowerWinRate =
            (double)
            statistics.LowerDepthWins /
            statistics.CompletedGames *
            100.0;

        double drawRate =
            (double)
            statistics.Draws /
            statistics.CompletedGames *
            100.0;

        double scoreRate =
            Calculate3ScoreRate(
                statistics
            );

        double averagePlies =
            (double)
            statistics.TotalPlies /
            statistics.CompletedGames;

        double averageNodes =
            (double)
            statistics.TotalNodes /
            statistics.CompletedGames;

        double averageSearchTime =
            statistics.TotalSearchTime /
            statistics.CompletedGames;

        double aggregateNPS =
            statistics.TotalSearchTime > 0.0
                ? statistics.TotalNodes /
                  statistics.TotalSearchTime
                : 0.0;

        AppendReport(
            $"{statistics.PairName,-12} | " +
            $"Games={statistics.CompletedGames} | " +
            $"HigherWin={higherWinRate:F2}% | " +
            $"LowerWin={lowerWinRate:F2}% | " +
            $"Draw={drawRate:F2}% | " +
            $"Score={scoreRate:F2}% | " +
            $"AvgPlies={averagePlies:F2} | " +
            $"Nodes={averageNodes:F0} | " +
            $"SearchTime={averageSearchTime:F4}s | " +
            $"NPS={aggregateNPS:F0}"
        );

        AppendReport(
            $"{new string(' ', 15)}| " +
            $"Paired={statistics.PairedPositionCount} | " +
            $"BothHigher={statistics.BothHigherDepthWins} | " +
            $"Split={statistics.SplitOutcomeGames} | " +
            $"BothLower={statistics.BothLowerDepthWins} | " +
            $"BothDraw={statistics.BothDrawGames}"
        );
    }

    private double Calculate3ScoreRate(
        DepthPairStatistics statistics)
    {
        if (
            statistics == null ||
            statistics.CompletedGames <= 0)
        {
            return 0.0;
        }

        return
            (
                statistics.HigherDepthWins +
                (
                    0.5 *
                    statistics.Draws
                )
            ) /
            statistics.CompletedGames *
            100.0;
    }

    private void Append3DepthInterpretation(
        DepthPairStatistics depth2vs3,
        DepthPairStatistics depth3vs4,
        DepthPairStatistics depth2vs4)
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
            "- Benchmark evaluates playing strength as a function of search depth."
        );

        AppendReport(
            "- All compared agents use the same Final AI configuration."
        );

        AppendReport(
            "- History, Killer Move, Transposition Table and Aspiration Window remain enabled."
        );

        AppendReport(
            "- The controlled experimental variable is search depth."
        );

        AppendReport(
            "- Multiple randomized positions reduce dependence on a single starting position."
        );

        AppendReport(
            "- Each depth matchup reuses the same randomized position set."
        );

        AppendReport(
            "- When side swapping is enabled, each position is evaluated twice with opposite depth-side assignments."
        );

        AppendReport(
            "- Therefore the primary comparison is based on search depth rather than color."
        );

        if (
            benchmark3Depth2 &&
            benchmark3Depth3)
        {
            AppendReport(
                "- D2-D3 measures the competitive effect of increasing search depth from 2 to 3."
            );
        }

        if (
            benchmark3Depth3 &&
            benchmark3Depth4)
        {
            AppendReport(
                "- D3-D4 measures the competitive effect of increasing search depth from 3 to 4."
            );
        }

        if (
            benchmark3Depth2 &&
            benchmark3Depth4)
        {
            AppendReport(
                "- D2-D4 measures the cumulative competitive difference between shallow and deeper search."
            );
        }

        AppendReport(
            "- A higher score rate for the deeper search indicates a competitive advantage associated with greater search depth."
        );

        AppendReport(
            "- Both-higher outcomes indicate that the deeper search won both side assignments for the same position."
        );

        AppendReport(
            "- Both-lower outcomes indicate that the shallower search won both paired games."
        );

        AppendReport(
            "- Split outcomes indicate that the result changed after depth-side assignment was reversed."
        );

        AppendReport(
            "- Both-draw outcomes indicate no decisive result in either side assignment."
        );

        AppendReport(
            "- Average search time and node count quantify the computational cost of deeper search."
        );

        if (
            benchmark3Depth2 &&
            benchmark3Depth3 &&
            depth2vs3 != null &&
            depth2vs3.CompletedGames > 0)
        {
            double score23 =
                Calculate3ScoreRate(
                    depth2vs3
                );

            AppendReport(
                $"- D2-D3 higher-depth score rate: " +
                $"{score23:F2}%."
            );
        }

        if (
            benchmark3Depth3 &&
            benchmark3Depth4 &&
            depth3vs4 != null &&
            depth3vs4.CompletedGames > 0)
        {
            double score34 =
                Calculate3ScoreRate(
                    depth3vs4
                );

            AppendReport(
                $"- D3-D4 higher-depth score rate: " +
                $"{score34:F2}%."
            );
        }

        if (
            benchmark3Depth2 &&
            benchmark3Depth4 &&
            depth2vs4 != null &&
            depth2vs4.CompletedGames > 0)
        {
            double score24 =
                Calculate3ScoreRate(
                    depth2vs4
                );

            AppendReport(
                $"- D2-D4 higher-depth score rate: " +
                $"{score24:F2}%."
            );
        }

        AppendReport(
            "=================================================="
        );
    }

    private void Append3DepthGameSummary(
        GameResult result)
    {
        AppendReport("");

        AppendReport(
            $"POSITION " +
            $"{result.PositionIndex + 1} " +
            $"GAME {result.GameNumber}"
        );

        AppendReport(
            $"Seed                     : " +
            $"{result.Seed}"
        );

        AppendReport(
            $"Depth Match              : " +
            $"D{result.LowerDepth} vs " +
            $"D{result.HigherDepth}"
        );

        AppendReport(
            $"Higher Depth Assignment  : " +
            (
                result.HigherDepthIsWhite
                    ? "WHITE"
                    : "BLACK"
            )
        );

        AppendReport(
            $"Outcome                  : " +
            $"{Format3DepthOutcome(result)}"
        );

        AppendReport(
            $"Plies                    : " +
            $"{result.Plies}"
        );

        AppendReport(
            $"Nodes                    : " +
            $"{result.TotalNodes}"
        );

        AppendReport(
            $"Search Time              : " +
            $"{result.TotalSearchTime:F6}s"
        );

        AppendReport(
            $"Game Time                : " +
            $"{result.GameTimeSeconds:F6}s"
        );
    }

    private string Format3DepthOutcome(
        GameResult result)
    {
        if (
            result.Result ==
            GameOutcome.DRAW)
        {
            return "DRAW";
        }

        bool higherDepthWon =
            IsHigherDepthWin(
                result
            );

        return higherDepthWon
            ? $"D{result.HigherDepth} WIN"
            : $"D{result.LowerDepth} WIN";
    }
}
