using System.Collections.Generic;
using UnityEngine;

public partial class AIBenchmarkManager
{
    private List<BenchmarkPosition>
        GenerateBenchmarkPositions()
    {
        List<BenchmarkPosition> positions =
            new List<BenchmarkPosition>();

        int positionCount =
            Mathf.Max(
                1,
                numberOfPositions
            );

        int plies =
            Mathf.Max(
                0,
                randomOpeningPlies
            );

        for (
            int i = 0;
            i < positionCount;
            i++)
        {
            int seed =
                randomSeed +
                (i * 1009);

            GameState state =
                _initialState.Clone();

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
                    plies
                );

            if (!valid)
            {
                AppendReport(
                    $"Position {i + 1}: SKIPPED"
                );

                continue;
            }

            positions.Add(
                new BenchmarkPosition
                {
                    State = state,
                    Seed = seed,
                    OpeningPlies = plies
                }
            );
        }

        return positions;
    }

    private bool CreateRandomPosition(
        GameState state,
        MoveGenerator moveGenerator,
        System.Random random,
        int plies)
    {
        for (
            int ply = 0;
            ply < plies;
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
                return false;
            }

            int randomIndex =
                random.Next(
                    legalMoves.Count
                );

            StateMove randomMove =
                legalMoves[randomIndex];

            moveGenerator.MakeMove(
                state,
                randomMove
            );
        }

        List<StateMove> finalMoves =
            moveGenerator.GenerateLegalMoves(
                state,
                state.SideToMove
            );

        if (
            finalMoves == null ||
            finalMoves.Count == 0)
        {
            return false;
        }

        return
            IsBenchmarkStateValid(
                state
            );
    }
}
