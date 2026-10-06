using UnityEngine;

public partial class AIBenchmarkManager
{
    private bool IsInitialStateValid(
        GameState state)
    {
        if (state == null)
        {
            return false;
        }

        int whitePieces = 0;
        int blackPieces = 0;

        bool whiteKingFound = false;
        bool blackKingFound = false;

        foreach (
            Vector2Int position
            in HexChessRules.CellCoordinates)
        {
            StatePiece? piece =
                state.GetPiece(
                    position.x,
                    position.y
                );

            if (!piece.HasValue)
            {
                continue;
            }

            if (
                piece.Value.Player ==
                EPlayer.WHITE)
            {
                whitePieces++;

                if (
                    piece.Value.Type ==
                    StatePieceType.KING)
                {
                    whiteKingFound = true;
                }
            }
            else if (
                piece.Value.Player ==
                EPlayer.BLACK)
            {
                blackPieces++;

                if (
                    piece.Value.Type ==
                    StatePieceType.KING)
                {
                    blackKingFound = true;
                }
            }
        }

        return
            whitePieces > 0 &&
            blackPieces > 0 &&
            whiteKingFound &&
            blackKingFound;
    }

    private bool IsBenchmarkStateValid(
        GameState state)
    {
        if (state == null)
        {
            return false;
        }

        bool whiteKing = false;
        bool blackKing = false;

        int whitePieces = 0;
        int blackPieces = 0;

        foreach (
            Vector2Int position
            in HexChessRules.CellCoordinates)
        {
            StatePiece? piece =
                state.GetPiece(
                    position.x,
                    position.y
                );

            if (!piece.HasValue)
            {
                continue;
            }

            StatePiece value =
                piece.Value;

            if (
                value.Player ==
                EPlayer.WHITE)
            {
                whitePieces++;

                if (
                    value.Type ==
                    StatePieceType.KING)
                {
                    whiteKing = true;
                }
            }
            else if (
                value.Player ==
                EPlayer.BLACK)
            {
                blackPieces++;

                if (
                    value.Type ==
                    StatePieceType.KING)
                {
                    blackKing = true;
                }
            }
        }

        return
            whitePieces > 0 &&
            blackPieces > 0 &&
            whiteKing &&
            blackKing;
    }
}
