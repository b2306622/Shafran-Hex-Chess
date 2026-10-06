using UnityEngine;

public partial class ChessAgent
{
    // =========================================================
    // TACTICAL REWARD
    // =========================================================

    private void ApplyTacticalReward(
        float reward)
    {
        float clamped =
            Mathf.Clamp(
                reward,
                -0.10f,
                0.10f
            );

        AddShapingReward(clamped);
    }

    // =========================================================
    // SUCCESSFUL CAPTURE
    // =========================================================

    private void RewardSuccessfulCapture(
        StatePiece capturedPiece)
    {
        if (!capturedPiece.IsValid)
        {
            return;
        }

        switch (capturedPiece.Type)
        {
            case StatePieceType.QUEEN:
            case StatePieceType.ROOK:

                ApplyTacticalReward(
                    majorCaptureTacticalReward
                );

                return;

            case StatePieceType.BISHOP:
            case StatePieceType.KNIGHT:

                ApplyTacticalReward(
                    minorCaptureTacticalReward
                );

                return;

            case StatePieceType.PAWN:

                ApplyTacticalReward(
                    pawnCaptureTacticalReward
                );

                return;
        }
    }

    // =========================================================
    // PROMOTION
    // =========================================================

    private void RewardPromotion()
    {
        ApplyTacticalReward(
            promotionReward
        );
    }

    // =========================================================
    // CASTLING
    // =========================================================

    private void RewardCastling()
    {
        ApplyTacticalReward(
            Mathf.Max(
                castlingReward,
                0.010f
            )
        );
    }

    // =========================================================
    // CHECK
    // =========================================================

    private void RewardCheckIfNeeded()
    {
        if (!IsActionEnvironmentReady())
        {
            return;
        }

        if (BaseGameController.Current.GameState !=
            EGameState.PLAYING)
        {
            return;
        }

        GameState state =
            GameState.FromUnityBoard();

        if (state == null)
        {
            return;
        }

        EPlayer opponent =
            HexChessRules.GetOpponent(
                agentPlayer
            );

        if (_moveGenerator.IsInCheck(
                state,
                opponent))
        {
            ApplyTacticalReward(
                checkReward
            );

        }
    }
}