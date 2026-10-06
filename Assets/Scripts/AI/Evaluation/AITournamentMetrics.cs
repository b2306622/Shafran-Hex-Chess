using System;

[Serializable]
public sealed class AITournamentMetrics
{
    public string playerWhite;
    public string playerBlack;
    public int games;
    public int whiteWins;
    public int blackWins;
    public int draws;

    public long totalPlies;
    public long totalCaptures;
    public long totalChecks;
    public long totalPromotions;

    public long whiteWinsUnder100;
    public long whiteWinsUnder150;
    public long whiteWinsUnder200;
    public long whiteWinsUnder300;

    public long blackWinsUnder100;
    public long blackWinsUnder150;
    public long blackWinsUnder200;
    public long blackWinsUnder300;

    public double totalWhiteSearchMs;
    public double totalBlackSearchMs;
    public long totalWhiteNodes;
    public long totalBlackNodes;
    public long totalMctsSimulations;
    public double totalMctsSearchMs;

    public int BestMoveAgreements;
    public int MeasuredMoves;

    public double WhiteWinRate => games <= 0 ? 0.0 : (double)whiteWins / games;
    public double BlackWinRate => games <= 0 ? 0.0 : (double)blackWins / games;
    public double DrawRate => games <= 0 ? 0.0 : (double)draws / games;
    public double ScoreRate => games <= 0 ? 0.0 : (whiteWins + blackWins + 0.5 * draws) / games;
    public double AveragePlies => games <= 0 ? 0.0 : (double)totalPlies / games;
    public double AverageCaptures => games <= 0 ? 0.0 : (double)totalCaptures / games;
    public double AverageChecks => games <= 0 ? 0.0 : (double)totalChecks / games;
    public double AveragePromotions => games <= 0 ? 0.0 : (double)totalPromotions / games;
    public double MoveAgreementRate => MeasuredMoves <= 0 ? 0.0 : (double)BestMoveAgreements / MeasuredMoves;

    public AITournamentMetrics(string white, string black)
    {
        playerWhite = white;
        playerBlack = black;
    }

    public void AddGame(
        EPlayer? winner,
        int plies,
        int captures,
        int checks,
        int promotions)
    {
        games++;
        totalPlies += Math.Max(0, plies);
        totalCaptures += Math.Max(0, captures);
        totalChecks += Math.Max(0, checks);
        totalPromotions += Math.Max(0, promotions);

        if (!winner.HasValue)
        {
            draws++;
            return;
        }

        if (winner.Value == EPlayer.WHITE)
        {
            whiteWins++;
            if (plies <= 100) whiteWinsUnder100++;
            if (plies <= 150) whiteWinsUnder150++;
            if (plies <= 200) whiteWinsUnder200++;
            if (plies <= 300) whiteWinsUnder300++;
        }
        else
        {
            blackWins++;
            if (plies <= 100) blackWinsUnder100++;
            if (plies <= 150) blackWinsUnder150++;
            if (plies <= 200) blackWinsUnder200++;
            if (plies <= 300) blackWinsUnder300++;
        }
    }

    public void AddSearch(
        EPlayer side,
        double milliseconds,
        long nodes)
    {
        if (side == EPlayer.WHITE)
        {
            totalWhiteSearchMs += Math.Max(0.0, milliseconds);
            totalWhiteNodes += Math.Max(0L, nodes);
        }
        else
        {
            totalBlackSearchMs += Math.Max(0.0, milliseconds);
            totalBlackNodes += Math.Max(0L, nodes);
        }
    }

    public void AddMctsSearch(double milliseconds, int simulations)
    {
        totalMctsSearchMs += Math.Max(0.0, milliseconds);
        totalMctsSimulations += Math.Max(0, simulations);
    }

    public void AddAgreement(bool agreement)
    {
        MeasuredMoves++;
        if (agreement)
            BestMoveAgreements++;
    }
}
