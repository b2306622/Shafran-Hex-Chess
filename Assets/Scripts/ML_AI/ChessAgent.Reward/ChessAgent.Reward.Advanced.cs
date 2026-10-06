using UnityEngine;

public partial class ChessAgent
{
    // =========================================================
    // OPENING INITIATIVE (FIRST 30 PLIES)
    // =========================================================
    // The opening is intentionally not rewarded for "owning" the center
    // or endlessly developing pieces. During the first 30 plies the agent
    // is instead rewarded for creating a concrete advantage that can be
    // carried into the middlegame: material gain, king restriction, direct
    // king attack, and positive tactical/evaluation swing.

    private float CalculateOpeningInitiativeReward(
        GameState before,
        GameState after,
        float evaluationDelta)
    {
        if (before == null || after == null)
            return 0f;

        int currentPly =
            BaseGameController.Current != null
                ? BaseGameController.Current.GamePlyCount
                : 0;

        if (currentPly <= 0 || currentPly > 30)
            return 0f;

        EPlayer opponent =
            HexChessRules.GetOpponent(agentPlayer);

        int beforeKingMoves =
            CountKingLegalMoves(before, opponent);

        int afterKingMoves =
            CountKingLegalMoves(after, opponent);

        int kingMobilityReduction =
            Mathf.Max(
                0,
                beforeKingMoves - afterKingMoves
            );

        float materialLeadBefore =
            HexChessRules.CalculateNonKingMaterial(
                before,
                agentPlayer
            ) -
            HexChessRules.CalculateNonKingMaterial(
                before,
                opponent
            );

        float materialLeadAfter =
            HexChessRules.CalculateNonKingMaterial(
                after,
                agentPlayer
            ) -
            HexChessRules.CalculateNonKingMaterial(
                after,
                opponent
            );

        float leadGain =
            Mathf.Max(
                0f,
                materialLeadAfter - materialLeadBefore
            );

        float kingAttackBefore =
            GetKingAttackScore(
                before,
                agentPlayer,
                opponent
            );

        float kingAttackAfter =
            GetKingAttackScore(
                after,
                agentPlayer,
                opponent
            );

        float kingAttackGain =
            Mathf.Max(
                0f,
                kingAttackAfter - kingAttackBefore
            );

        float reward = 0f;

        // Concrete material advantage.
        reward +=
            Mathf.Clamp(
                leadGain * 0.010f,
                0f,
                0.022f
            );

        // Restricting the opposing King is the key early pressure signal.
        reward +=
            Mathf.Min(
                3,
                kingMobilityReduction
            ) *
            0.009f;

        // Reward an actual improvement in direct King attack.
        reward +=
            Mathf.Clamp(
                kingAttackGain * 0.005f,
                0f,
                0.014f
            );

        // A check in the opening is a concrete threat, not just a positional
        // occupation signal.
        if (_moveGenerator != null &&
            _moveGenerator.IsInCheck(after, opponent))
        {
            reward += 0.018f;
        }

        // Small bonus only when the move improved the side's own evaluation.
        // This keeps the signal focused on purposeful attacks rather than
        // rewarding every capture or every forward move.
        float positiveEvaluationDelta =
            Mathf.Max(0f, evaluationDelta);

        reward +=
            Mathf.Clamp(
                positiveEvaluationDelta * 0.00020f,
                0f,
                0.012f
            );

        float earlyWeight =
            Mathf.Lerp(
                1.30f,
                0.85f,
                Mathf.InverseLerp(1f, 30f, currentPly)
            );

        // Captures are already rewarded separately. Keep only the strategic
        // initiative terms above so the agent does not learn "capture at all
        // costs" in equal opening positions.
        return reward * earlyWeight * 0.50f;
    }

    // =========================================================
    // ADVANCED TACTICAL / CONVERSION REWARD
    // =========================================================
    // This component deliberately stays small. It adds tactical quality
    // signals without replacing the existing material/center/endgame
    // reward structure.

    private float CalculateAdvancedTacticalReward(
        GameState before,
        GameState after,
        StateMove selectedMove)
    {
        if (before == null || after == null)
        {
            return 0f;
        }

        float opening;
        float middlegame;
        float endgame;

        HexChessRules.GetStrategicPhase(
            after,
            agentPlayer,
            out opening,
            out middlegame,
            out endgame
        );

        EPlayer opponent =
            HexChessRules.GetOpponent(
                agentPlayer
            );

        // -----------------------------------------------------
        // 1. PROTECT OUR MATERIAL
        // -----------------------------------------------------
        float ownLooseBefore =
            GetLooseMaterialScore(
                before,
                agentPlayer
            );

        float ownLooseAfter =
            GetLooseMaterialScore(
                after,
                agentPlayer
            );

        // -----------------------------------------------------
        // 2. CREATE TARGETS
        // -----------------------------------------------------
        float enemyLooseBefore =
            GetLooseMaterialScore(
                before,
                opponent
            );

        float enemyLooseAfter =
            GetLooseMaterialScore(
                after,
                opponent
            );

        float reward =
            (ownLooseBefore - ownLooseAfter) * 0.012f +
            (enemyLooseAfter - enemyLooseBefore) *
            (0.010f + 0.010f * middlegame);

        // -----------------------------------------------------
        // 3. KING ATTACK
        // -----------------------------------------------------
        float kingAttackBefore =
            GetKingAttackScore(
                before,
                agentPlayer,
                opponent
            );

        float kingAttackAfter =
            GetKingAttackScore(
                after,
                agentPlayer,
                opponent
            );

        reward +=
            (kingAttackAfter - kingAttackBefore) *
            (0.002f + 0.006f * middlegame +
             0.003f * endgame);

        // -----------------------------------------------------
        // 4. CAPTURE TARGET QUALITY
        // -----------------------------------------------------
        // The existing capture reward handles the actual capture. This
        // term rewards moves that set up a valuable tactical target without
        // giving an unlimited reward for attacking something once.
        float targetBefore =
            GetAttackedEnemyMaterial(
                before,
                agentPlayer
            );

        float targetAfter =
            GetAttackedEnemyMaterial(
                after,
                agentPlayer
            );

        reward +=
            (targetAfter - targetBefore) *
            (0.0015f + 0.0025f * middlegame);

        // A capture/promotion/check is already rewarded elsewhere. Do not
        // double-count it here.
        if (selectedMove.IsCapture ||
            selectedMove.IsPromotion)
        {
            reward *= 0.75f;
        }

        // Shafran opening/middlegame initiative.
        reward +=
            CalculateShafranPawnInitiativeReward(
                before,
                selectedMove,
                opening,
                middlegame
            );

        // Conversion pressure is deliberately gated by a material lead so
        // both agents do not become reckless attackers in equal positions.
        reward +=
            CalculateConversionPressureReward(
                before,
                after,
                opening,
                middlegame,
                endgame
            );

        // Opening stays conservative; middlegame gets the largest tactical
        // signal; endgame gets the strongest conversion signal.
        float phaseScale =
            0.60f * opening +
            1.15f * middlegame +
            1.25f * endgame;

        return reward * phaseScale;
    }

    private float CalculateShafranPawnInitiativeReward(
        GameState before,
        StateMove move,
        float opening,
        float middlegame)
    {
        if (before == null ||
            move.MovingPiece.Type != StatePieceType.PAWN)
        {
            return 0f;
        }

        StatePiece? movedBefore =
            before.GetPiece(
                move.From.X,
                move.From.Y
            );

        if (!movedBefore.HasValue ||
            !movedBefore.Value.IsValid ||
            movedBefore.Value.HasMoved)
        {
            return 0f;
        }

        int maxInitialStep =
            HexChessRules.GetInitialPawnMoveDistance(
                move.From.X
            );

        int distance =
            Mathf.Abs(
                move.To.Y - move.From.Y
            );

        float stepQuality =
            Mathf.Clamp01(
                distance /
                Mathf.Max(1f, maxInitialStep)
            );

        // Prefer using the full Shafran first move when it creates real
        // early contact, but keep the signal small.
        float reward =
            stepQuality *
            (shafranLongPawnMoveReward * opening);

        // In the middlegame, an advanced pawn move becomes a pawn break
        // signal rather than an opening-development reward.
        if (middlegame > 0f &&
            distance > 0)
        {
            float centerProgress =
                GetCenterScore(
                    move.To.X,
                    move.To.Y
                ) -
                GetCenterScore(
                    move.From.X,
                    move.From.Y
                );

            reward +=
                Mathf.Max(
                    0f,
                    centerProgress
                ) *
                shafranMiddlegamePawnBreakReward *
                middlegame;
        }

        return reward;
    }

    private float CalculateConversionPressureReward(
        GameState before,
        GameState after,
        float opening,
        float middlegame,
        float endgame)
    {
        if (before == null || after == null)
            return 0f;

        float ownBefore =
            HexChessRules.CalculateNonKingMaterial(
                before,
                agentPlayer
            );

        float enemyBefore =
            HexChessRules.CalculateNonKingMaterial(
                before,
                HexChessRules.GetOpponent(agentPlayer)
            );

        float ownAfter =
            HexChessRules.CalculateNonKingMaterial(
                after,
                agentPlayer
            );

        float enemyAfter =
            HexChessRules.CalculateNonKingMaterial(
                after,
                HexChessRules.GetOpponent(agentPlayer)
            );

        float leadBefore = ownBefore - enemyBefore;
        float leadAfter = ownAfter - enemyAfter;

        float leadGate =
            Mathf.InverseLerp(
                conversionLeadThreshold,
                conversionLeadStrongThreshold,
                leadAfter
            );

        if (leadGate <= 0f)
            return 0f;

        int enemyKingMovesBefore =
            CountKingLegalMoves(
                before,
                HexChessRules.GetOpponent(agentPlayer)
            );

        int enemyKingMovesAfter =
            CountKingLegalMoves(
                after,
                HexChessRules.GetOpponent(agentPlayer)
            );

        float kingReduction =
            Mathf.Max(
                0,
                enemyKingMovesBefore -
                enemyKingMovesAfter
            );

        float leadImprovement =
            Mathf.Max(
                0f,
                leadAfter - leadBefore
            );

        float pressure =
            kingReduction * 0.006f +
            leadImprovement * 0.010f;

        // Stronger in the middle/endgame; almost neutral during a level
        // opening so equal positions remain strategically stable.
        float phase =
            0.65f * opening +
            1.10f * middlegame +
            1.45f * endgame;

        return
            pressure *
            leadGate *
            phase;
    }

    // =========================================================
    // MATE CONVERSION
    // =========================================================

    private float CalculateMateConversionReward(
        GameState before,
        GameState after,
        StateMove selectedMove)
    {
        if (before == null || after == null)
            return 0f;

        EPlayer opponent = HexChessRules.GetOpponent(agentPlayer);

        int opponentNonKing =
            CountNonKingPiecesForReward(after, opponent);

        float lead =
            HexChessRules.CalculateNonKingMaterial(after, agentPlayer) -
            HexChessRules.CalculateNonKingMaterial(after, opponent);

        float leadGate = Mathf.InverseLerp(
            conversionLeadThreshold,
            conversionLeadStrongThreshold,
            lead
        );

        if (leadGate <= 0f)
            return 0f;

        int beforeKingMoves = CountKingLegalMoves(before, opponent);
        int afterKingMoves = CountKingLegalMoves(after, opponent);
        int mobilityReduction = Mathf.Max(0, beforeKingMoves - afterKingMoves);

        float reward = mobilityReduction * 0.014f;

        // Strong mating-net signal even before the opponent is reduced to
        // King-only. The agent should learn to remove escape squares rather
        // than wander around and let the enemy King capture its material.
        if (afterKingMoves <= 2)
        {
            reward += 0.015f;
        }

        if (afterKingMoves <= 1)
        {
            reward += kingTrapRewardAtOneMove;
        }

        if (_moveGenerator.IsInCheck(after, opponent))
        {
            reward += 0.045f;
        }

        if (opponentNonKing == 0)
        {
            // King-only conversion gets an additional endgame bonus.
            if (afterKingMoves <= 3) reward += 0.018f;
            if (afterKingMoves <= 1) reward += 0.028f;
        }

        return reward * leadGate;
    }

    private float CalculateDynamicPlyPenalty(
        GameState state)
    {
        if (state == null)
            return perPlyPenalty;

        float opening;
        float middlegame;
        float endgame;

        HexChessRules.GetStrategicPhase(
            state,
            agentPlayer,
            out opening,
            out middlegame,
            out endgame
        );

        EPlayer opponent =
            HexChessRules.GetOpponent(agentPlayer);

        float lead =
            HexChessRules.CalculateNonKingMaterial(
                state,
                agentPlayer
            ) -
            HexChessRules.CalculateNonKingMaterial(
                state,
                opponent
            );

        float leadGate =
            Mathf.InverseLerp(
                conversionLeadThreshold,
                conversionLeadStrongThreshold,
                lead
            );

        // Keep endgame pressure at least at the strengthened V7 level,
        // even if an older Unity scene serialized a weaker inspector value.
        float effectiveEndgameMultiplier =
            Mathf.Max(2.00f, endgameDynamicPlyPenaltyMultiplier);

        float multiplier =
            0.45f * opening +
            1.25f * middlegame +
            effectiveEndgameMultiplier * endgame;

        int currentPly =
            BaseGameController.Current != null
                ? BaseGameController.Current.GamePlyCount
                : 0;

        // Apply stronger time pressure only when this agent is ahead.
        float conversionUrgency =
            Mathf.InverseLerp(30f, 70f, currentPly);

        if (leadGate > 0f)
        {
            // After ply 30, a winning side is charged increasingly for
            // every extra move. This is deliberately much stronger than
            // the opening pressure so the policy must convert quickly.
            multiplier *=
                1f +
                2.50f * leadGate * conversionUrgency;
        }

        int opponentNonKing =
            CountNonKingPiecesForReward(state, opponent);

        // If only the enemy King remains, stop rewarding material hunting
        // and strongly discourage wandering before mate.
        if (opponentNonKing == 0 && leadGate > 0f)
        {
            // King-only conversion is the strongest anti-wandering case.
            multiplier *=
                1f +
                2.50f * Mathf.InverseLerp(30f, 65f, currentPly);
        }

        return
            perPlyPenalty *
            Mathf.Clamp(multiplier, 0.35f, 8.00f);
    }

    private float GetLooseMaterialScore(
        GameState state,
        EPlayer player)
    {
        if (state == null)
        {
            return 0f;
        }

        EPlayer attacker =
            HexChessRules.GetOpponent(
                player
            );

        float score = 0f;

        foreach (Vector2Int position in
                 HexChessRules.CellCoordinates)
        {
            StatePiece? piece =
                state.GetPiece(
                    position.x,
                    position.y
                );

            if (!piece.HasValue ||
                !piece.Value.IsValid ||
                piece.Value.Player != player ||
                piece.Value.Type == StatePieceType.KING)
            {
                continue;
            }

            if (ShafranTacticalState.GetLegalCaptureCount(
                    state, attacker, position.x, position.y) <= 0)
            {
                continue;
            }

            float value =
                Mathf.Max(
                    0.25f,
                    piece.Value.Value / 1000f
                );

            bool defended =
                ShafranTacticalState.GetLegalDefenseCount(
                    state, player, position.x, position.y) > 0;

            score +=
                value *
                (defended ? 0.25f : 1.0f);
        }

        return score;
    }

    private float GetAttackedEnemyMaterial(
        GameState state,
        EPlayer attacker)
    {
        if (state == null)
        {
            return 0f;
        }

        EPlayer enemy =
            HexChessRules.GetOpponent(
                attacker
            );

        float score = 0f;

        foreach (Vector2Int position in
                 HexChessRules.CellCoordinates)
        {
            StatePiece? piece =
                state.GetPiece(
                    position.x,
                    position.y
                );

            if (!piece.HasValue ||
                !piece.Value.IsValid ||
                piece.Value.Player != enemy ||
                piece.Value.Type == StatePieceType.KING)
            {
                continue;
            }

            if (ShafranTacticalState.GetLegalCaptureCount(
                    state, attacker, position.x, position.y) > 0)
            {
                score +=
                    Mathf.Max(
                        0.25f,
                        piece.Value.Value / 1000f
                    );
            }
        }

        return score;
    }

    private float GetKingAttackScore(
        GameState state,
        EPlayer attacker,
        EPlayer victim)
    {
        Vector2Int king =
            FindKing(
                state,
                victim
            );

        if (king.x < 0)
        {
            return 0f;
        }

        float score = 0f;

        if (_moveGenerator.IsInCheck(
                state,
                victim))
        {
            score += 3f;
        }

        foreach (Vector2Int direction in
                 HexChessRules.QueenDirections)
        {
            int x =
                king.x + direction.x;

            int y =
                king.y + direction.y;

            if (!HexChessRules.IsValidCell(x, y))
            {
                continue;
            }

            if (_moveGenerator.IsSquareAttacked(
                    state,
                    x,
                    y,
                    attacker))
            {
                score += 0.5f;
            }
        }

        return score;
    }
}
