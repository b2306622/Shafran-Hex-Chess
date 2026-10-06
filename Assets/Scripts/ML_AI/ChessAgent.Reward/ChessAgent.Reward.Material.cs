using System.Collections.Generic;
using UnityEngine;

public partial class ChessAgent
{
    // =========================================================
    // MATERIAL VALUE
    // =========================================================

    private float GetMaterialValue(BasePiece piece)
    {
        if (piece == null)
        {
            return 0f;
        }

        // V10 Shafran reward values. Use the serialized fields so the
        // values remain configurable and are not dead fields.
        if (piece is P_Pawn)
        {
            return pawnValue;
        }

        if (piece is P_Knight)
        {
            return knightValue;
        }

        if (piece is P_Bishop)
        {
            return bishopValue;
        }

        if (piece is P_Rook)
        {
            return rookValue;
        }

        if (piece is P_Queen)
        {
            return queenValue;
        }

        if (piece is P_King)
        {
            return kingValue;
        }

        return 0f;
    }

    // =========================================================
    // STATE PIECE MATERIAL VALUE
    // =========================================================

    private float GetMaterialValue(StatePiece piece)
    {
        if (!piece.IsValid)
        {
            return 0f;
        }

        if (piece.Type == StatePieceType.KING)
        {
            return kingValue;
        }

        switch (piece.Type)
        {
            case StatePieceType.PAWN:
                return pawnValue;

            case StatePieceType.KNIGHT:
                return knightValue;

            case StatePieceType.BISHOP:
                return bishopValue;

            case StatePieceType.ROOK:
                return rookValue;

            case StatePieceType.QUEEN:
                return queenValue;

            default:
                return 0f;
        }
    }

    // =========================================================
    // MATERIAL DELTA
    // =========================================================

    private float CalculateMaterialDeltaReward(
        GameState before,
        GameState after)
    {
        if (before == null || after == null)
        {
            return 0f;
        }

        float beforeOwn =
            CalculateMaterial(
                before,
                agentPlayer
            );

        float afterOwn =
            CalculateMaterial(
                after,
                agentPlayer
            );

        EPlayer opponent =
            HexChessRules.GetOpponent(
                agentPlayer
            );

        float beforeEnemy =
            CalculateMaterial(
                before,
                opponent
            );

        float afterEnemy =
            CalculateMaterial(
                after,
                opponent
            );

        float ownDelta =
            afterOwn - beforeOwn;

        float enemyDelta =
            afterEnemy - beforeEnemy;

        float materialSwing =
            ownDelta - enemyDelta;

        if (Mathf.Abs(materialSwing) < 0.0001f)
        {
            return 0f;
        }

        float reward =
            materialSwing *
            materialDeltaRewardScale;

        return Mathf.Clamp(
            reward,
            -materialLossRewardCap,
            materialGainRewardCap
        );
    }

    // =========================================================
    // TOTAL MATERIAL
    // =========================================================

    private float CalculateMaterial(
        GameState state,
        EPlayer player)
    {
        if (state == null)
        {
            return 0f;
        }

        float total = 0f;

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

            if (!value.IsValid)
            {
                continue;
            }

            if (value.Player != player)
            {
                continue;
            }

            // King không tính vào material.
            if (value.Type == StatePieceType.KING)
            {
                continue;
            }

            total +=
                GetMaterialValue(value);
        }

        return total;
    }

    // =========================================================
    // HANGING PIECE PENALTY
    //
    // ExposedPiecePenalty only catches a piece that becomes attacked after
    // the move. This catches a piece that is ALREADY hanging and remains
    // available for the opponent King to capture on the next turn.
    // =========================================================

    private float CalculateHangingPiecePenalty(
        GameState before,
        GameState after)
    {
        if (after == null)
        {
            return 0f;
        }

        EPlayer opponent =
            HexChessRules.GetOpponent(
                agentPlayer
            );

        float ownMaterial =
            HexChessRules.CalculateNonKingMaterial(
                after,
                agentPlayer
            );

        float enemyMaterial =
            HexChessRules.CalculateNonKingMaterial(
                after,
                opponent
            );

        float materialLead =
            ownMaterial - enemyMaterial;

        float leadGate =
            Mathf.InverseLerp(
                0.5f,
                5.0f,
                materialLead
            );

        float endgamePhase =
            GetEndgameConversionPhase(after);

        float phaseMultiplier =
            0.50f +
            endgamePhase *
            (0.75f + 1.25f * leadGate);

        float hangingScore = 0f;

        foreach (
            Vector2Int position
            in HexChessRules.CellCoordinates)
        {
            StatePiece? piece =
                after.GetPiece(
                    position.x,
                    position.y
                );

            if (!piece.HasValue ||
                !piece.Value.IsValid ||
                piece.Value.Player != agentPlayer ||
                piece.Value.Type == StatePieceType.KING)
            {
                continue;
            }

            int incoming =
                ShafranTacticalState.GetPseudoAttackCount(
                    after, opponent, position.x, position.y);

            if (incoming <= 0)
            {
                continue;
            }

            int defenders =
                ShafranTacticalState.GetPseudoAttackCount(
                    after, agentPlayer, position.x, position.y);

            if (defenders >= incoming)
            {
                continue;
            }

            float value =
                Mathf.Clamp01(
                    GetMaterialValue(piece.Value) /
                    queenValue
                );

            hangingScore += value;
        }

        float penalty =
            hangingScore *
            hangingPiecePenaltyScale *
            phaseMultiplier;

        return -Mathf.Min(
            penalty,
            hangingPiecePenaltyCap
        );
    }

    // =========================================================
    // QUEEN / MAJOR PIECE LOSS
    // =========================================================
    //
    // Important: when the opponent captures our Queen, that capture
    // happens between our previous move and our next decision. Therefore
    // normal CalculateMaterialDeltaReward(before, after) cannot see it.
    // This helper compares the stored post-move snapshot with the current
    // position after the opponent reply.
    // =========================================================

    private int CountStatePieces(
        GameState state,
        EPlayer player,
        StatePieceType type)
    {
        if (state == null)
        {
            return 0;
        }

        int count = 0;

        foreach (Vector2Int position in HexChessRules.CellCoordinates)
        {
            StatePiece? piece =
                state.GetPiece(
                    position.x,
                    position.y
                );

            if (!piece.HasValue ||
                !piece.Value.IsValid ||
                piece.Value.Player != player ||
                piece.Value.Type != type)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private void CheckOpponentResponseMaterialLoss()
    {
        if (!IsActionEnvironmentReady() ||
            _lastAgentMoveState == null)
        {
            return;
        }

        // The check is meaningful only after the opponent has replied, i.e.
        // when it is our turn again. Skipping the waiting half-turn removes
        // an unnecessary GameState snapshot.
        if (BaseGameController.Current.CurrentPlayer != agentPlayer)
        {
            return;
        }

        GameState current =
            GameState.FromUnityBoard();

        if (current == null)
        {
            return;
        }

        // Evaluate each board position only once.
        if (_lastOpponentResponseHash == current.Hash)
        {
            return;
        }

        // Do not consume the snapshot while the board is still exactly
        // the position after our own move.
        if (_lastAgentMoveState.Hash == current.Hash)
        {
            return;
        }

        _lastOpponentResponseHash =
            current.Hash;

        int beforeQueens =
            CountStatePieces(
                _lastAgentMoveState,
                agentPlayer,
                StatePieceType.QUEEN
            );

        int afterQueens =
            CountStatePieces(
                current,
                agentPlayer,
                StatePieceType.QUEEN
            );

        int beforeRooks =
            CountStatePieces(
                _lastAgentMoveState,
                agentPlayer,
                StatePieceType.ROOK
            );

        int afterRooks =
            CountStatePieces(
                current,
                agentPlayer,
                StatePieceType.ROOK
            );

        int beforeMinors =
            CountStatePieces(
                _lastAgentMoveState,
                agentPlayer,
                StatePieceType.BISHOP
            ) +
            CountStatePieces(
                _lastAgentMoveState,
                agentPlayer,
                StatePieceType.KNIGHT
            );

        int afterMinors =
            CountStatePieces(
                current,
                agentPlayer,
                StatePieceType.BISHOP
            ) +
            CountStatePieces(
                current,
                agentPlayer,
                StatePieceType.KNIGHT
            );

        int queensLost =
            Mathf.Max(
                0,
                beforeQueens - afterQueens
            );

        int rooksLost =
            Mathf.Max(
                0,
                beforeRooks - afterRooks
            );

        int minorsLost =
            Mathf.Max(
                0,
                beforeMinors - afterMinors
            );

        bool anyMaterialLoss =
            queensLost > 0 ||
            rooksLost > 0 ||
            minorsLost > 0;

        // A freshly promoted Queen that survives the opponent reply gets a
        // small one-time bonus. This teaches PPO that promotion is only the
        // midpoint of conversion, not the endpoint.
        if (_lastAgentMoveWasPromotion)
        {
            if (afterQueens >= beforeQueens)
            {
                AddShapingReward(
                    queenSurvivesReplyBonus
                );

                if (logQueenDiagnostics)
                {
                    ShafranDebug.Log(
                        $"[SHAFRAN QUEEN SURVIVED] " +
                        $"Episode={_sharedDebugEpisodeId} | " +
                        $"Ply={BaseGameController.Current.GamePlyCount} | " +
                        $"Q {beforeQueens}->{afterQueens} | " +
                        $"Bonus={queenSurvivesReplyBonus:0.000}"
                    );
                }
            }

            _lastAgentMoveWasPromotion = false;
        }

        if (!anyMaterialLoss)
        {
            return;
        }

        // A Queen loss is the main signal. Do not let a single capture
        // become an enormous terminal-like reward, but make it clearly
        // stronger than the old generic -0.08 material cap.
        float penalty =
            queensLost *
            queenLossPenalty +
            rooksLost *
            majorPieceLossPenalty +
            minorsLost *
            0.025f;

        penalty =
            Mathf.Min(
                penalty,
                0.30f
            );

        AddShapingReward(
            -penalty
        );

        if (logQueenDiagnostics)
        {
            string label =
                queensLost > 0
                    ? "[SHAFRAN QUEEN LOST]"
                    : "[SHAFRAN MATERIAL LOSS]";

            ShafranDebug.Log(
                $"{label} " +
                $"Episode={_sharedDebugEpisodeId} | " +
                $"Ply={BaseGameController.Current.GamePlyCount} | " +
                $"Q {beforeQueens}->{afterQueens} | " +
                $"R {beforeRooks}->{afterRooks} | " +
                $"Minor {beforeMinors}->{afterMinors} | " +
                $"Penalty={-penalty:0.000}"
            );
        }
    }

    private float CalculateQueenHangingPenalty(
        GameState after)
    {
        if (after == null)
        {
            return 0f;
        }

        EPlayer opponent =
            HexChessRules.GetOpponent(
                agentPlayer
            );

        float penalty = 0f;

        foreach (Vector2Int position in HexChessRules.CellCoordinates)
        {
            StatePiece? piece =
                after.GetPiece(
                    position.x,
                    position.y
                );

            if (!piece.HasValue ||
                !piece.Value.IsValid ||
                piece.Value.Player != agentPlayer ||
                piece.Value.Type != StatePieceType.QUEEN)
            {
                continue;
            }

            bool attacked =
                _moveGenerator.IsSquareAttacked(
                    after,
                    position.x,
                    position.y,
                    opponent
                );

            if (!attacked)
            {
                continue;
            }

            bool defended =
                _moveGenerator.IsSquareAttacked(
                    after,
                    position.x,
                    position.y,
                    agentPlayer
                );

            if (!defended)
            {
                penalty += queenHangingPenalty;
            }
        }

        return -Mathf.Min(
            penalty,
            queenHangingPenalty
        );
    }



    // =========================================================
    // V10 NOTE
    // =========================================================
    //
    // The old V9 "enemy King capture danger" and "King escape safety"
    // rewards were intentionally removed from the reward pipeline.
    //
    // Reason:
    //   V9 could teach:
    //       enemy King approaches -> run the piece
    //
    //   V10 teaches:
    //       enemy King approaches -> restrict escape squares -> check
    //       -> create mating threat -> checkmate.
    //
    // Normal hanging-piece detection remains active below.
    // =========================================================

    // =========================================================
    // EXPOSED PIECE PENALTY
    //
    // Chỉ phạt:
    //
    // trước: an toàn
    // sau:   bị đối phương attack
    //
    // Không phạt quân vốn đã bị attack.
    // =========================================================

    private float CalculateExposedPiecePenalty(
        GameState before,
        GameState after)
    {
        if (before == null || after == null)
        {
            return 0f;
        }

        EPlayer opponent =
            HexChessRules.GetOpponent(
                agentPlayer
            );

        float penalty = 0f;

        foreach (
            Vector2Int position
            in HexChessRules.CellCoordinates)
        {
            StatePiece? beforePiece =
                before.GetPiece(
                    position.x,
                    position.y
                );

            StatePiece? afterPiece =
                after.GetPiece(
                    position.x,
                    position.y
                );

            if (!afterPiece.HasValue ||
                !afterPiece.Value.IsValid)
            {
                continue;
            }

            StatePiece current =
                afterPiece.Value;

            if (current.Player != agentPlayer)
            {
                continue;
            }

            if (current.Type == StatePieceType.KING)
            {
                continue;
            }

            int incomingAfter =
                ShafranTacticalState.GetPseudoAttackCount(
                    after, opponent, position.x, position.y);

            if (incomingAfter <= 0)
            {
                continue;
            }

            int defendersAfter =
                ShafranTacticalState.GetPseudoAttackCount(
                    after, agentPlayer, position.x, position.y);

            if (defendersAfter >= incomingAfter)
            {
                continue;
            }

            bool attackedBefore = false;

            if (
                beforePiece.HasValue &&
                beforePiece.Value.IsValid &&
                beforePiece.Value.Player == agentPlayer
            )
            {
                attackedBefore =
                    ShafranTacticalState.GetPseudoAttackCount(
                        before, opponent, position.x, position.y) > 0;
            }

            if (attackedBefore)
            {
                continue;
            }

            float pieceValue =
                GetMaterialValue(current);

            float normalized =
                Mathf.Clamp01(
                    pieceValue /
                    queenValue
                );

            penalty +=
                normalized *
                exposedPiecePenaltyScale;
        }

        return -Mathf.Min(
            penalty,
            exposedPiecePenaltyCap
        );
    }
}