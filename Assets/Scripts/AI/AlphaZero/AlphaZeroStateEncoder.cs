using System.Collections.Generic;
using UnityEngine;

public static class AlphaZeroStateEncoder
{
    public const int PieceStateCount = 13;
    public const int ObservationSize =
        HexChessRules.CELL_COUNT * 14 + 4 + 1 + 1;

    public static float[] Encode(GameState state, EPlayer perspective)
    {
        float[] observations = new float[ObservationSize];

        if (state == null)
            return observations;

        int cursor = 0;

        foreach (Vector2Int position in HexChessRules.CellCoordinates)
        {
            StatePiece? piece = state.GetPiece(position.x, position.y);
            int stateIndex = GetRelativePieceStateIndex(piece, perspective);

            for (int i = 0; i < PieceStateCount; i++)
            {
                float value = i == stateIndex ? 1f : 0f;
                if (i == stateIndex &&
                    piece.HasValue &&
                    piece.Value.HasMoved)
                {
                    value = 0.5f;
                }
                observations[cursor++] = value;
            }

            observations[cursor++] =
                ShafranTacticalState.GetSquareSafety(
                    state,
                    perspective,
                    position.x,
                    position.y
                );
        }

        if (state.LastMove.HasValue)
        {
            StateMove lastMove = state.LastMove.Value;

            int fromIndex = HexChessRules.GetCellIndex(
                lastMove.From.X,
                lastMove.From.Y
            );

            int toIndex = HexChessRules.GetCellIndex(
                lastMove.To.X,
                lastMove.To.Y
            );

            float maxIndex = Mathf.Max(1f, HexChessRules.CELL_COUNT - 1);

            observations[cursor++] = fromIndex / maxIndex;
            observations[cursor++] = toIndex / maxIndex;
            observations[cursor++] =
                lastMove.MovingPiece.Type == StatePieceType.PAWN &&
                Mathf.Abs(lastMove.From.Y - lastMove.To.Y) > 1
                    ? 1f
                    : 0f;
            observations[cursor++] = lastMove.IsCapture ? 1f : 0f;
        }
        else
        {
            observations[cursor++] = -1f;
            observations[cursor++] = -1f;
            observations[cursor++] = 0f;
            observations[cursor++] = 0f;
        }

        observations[cursor++] =
            state.SideToMove == perspective ? 1f : 0f;

        observations[cursor] =
            perspective == EPlayer.WHITE ? 1f : -1f;

        return observations;
    }

    public static void EncodeTo(
        GameState state,
        EPlayer perspective,
        float[] destination)
    {
        if (destination == null || destination.Length < ObservationSize)
            throw new System.ArgumentException("Destination buffer is too small.", nameof(destination));

        float[] encoded = Encode(state, perspective);
        System.Array.Copy(encoded, destination, ObservationSize);
    }

    private static int GetRelativePieceStateIndex(
        StatePiece? piece,
        EPlayer perspective)
    {
        if (!piece.HasValue || !piece.Value.IsValid)
            return 0;

        int baseIndex;

        switch (piece.Value.Type)
        {
            case StatePieceType.PAWN:
                baseIndex = 1;
                break;
            case StatePieceType.KNIGHT:
                baseIndex = 2;
                break;
            case StatePieceType.BISHOP:
                baseIndex = 3;
                break;
            case StatePieceType.ROOK:
                baseIndex = 4;
                break;
            case StatePieceType.QUEEN:
                baseIndex = 5;
                break;
            case StatePieceType.KING:
                baseIndex = 6;
                break;
            default:
                return 0;
        }

        return piece.Value.Player == perspective
            ? baseIndex
            : baseIndex + 6;
    }
}
