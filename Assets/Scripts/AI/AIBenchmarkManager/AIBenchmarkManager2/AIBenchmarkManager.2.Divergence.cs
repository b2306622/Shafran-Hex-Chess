using System;

public partial class AIBenchmarkManager
{
    private void Reset2DivergenceData()
    {
        _divergencePositions2.Clear();
    }

    private void Record2DivergencePosition(
        BenchmarkPosition position,
        int positionNumber,
        MoveQualitySample sample2,
        bool has2,
        MoveQualitySample sample3,
        bool has3,
        MoveQualitySample sample4,
        bool has4)
    {
        if (position.State == null)
        {
            return;
        }

        bool different23 =
            has2 &&
            has3 &&
            !AreSameMove(
                sample2.BestMove,
                sample3.BestMove
            );

        bool different34 =
            has3 &&
            has4 &&
            !AreSameMove(
                sample3.BestMove,
                sample4.BestMove
            );

        if (
            !different23 &&
            !different34)
        {
            return;
        }

        GameState snapshot =
            position.State.Clone();

        if (snapshot == null)
        {
            return;
        }

        DivergencePosition2 divergence =
            new DivergencePosition2();

        divergence.CaseId =
            $"DIVERGENCE-" +
            $"{_divergencePositions2.Count + 1:00}";

        divergence.PositionNumber =
            positionNumber;

        divergence.Seed =
            position.Seed;

        divergence.PreplayedPlies =
            position.OpeningPlies;

        divergence.SideToMove =
            snapshot.SideToMove;

        divergence.SourceState =
            snapshot;

        divergence.HasD2 =
            has2;

        divergence.HasD3 =
            has3;

        divergence.HasD4 =
            has4;

        divergence.IsRealDivergence =
            true;

        divergence.D2vsD3Different =
            different23;

        divergence.D3vsD4Different =
            different34;

        if (has2)
        {
            divergence.D2Move =
                sample2.BestMove;
        }

        if (has3)
        {
            divergence.D3Move =
                sample3.BestMove;
        }

        if (has4)
        {
            divergence.D4Move =
                sample4.BestMove;
        }

        _divergencePositions2.Add(
            divergence
        );
    }
}
