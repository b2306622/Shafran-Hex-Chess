using System.Collections.Generic;
using UnityEngine;

public partial class ChessAgent
{
    // =========================================================
    // STRATEGIC REWARD PIPELINE
    // =========================================================

    private void ApplyStrategicReward(
        GameState before,
        GameState after,
        StateMove selectedMove,
        float evaluationDelta)
    {
        if (before == null || after == null)
            return;

        EPlayer opponent =
            HexChessRules.GetOpponent(agentPlayer);

        int currentPly =
            BaseGameController.Current != null
                ? BaseGameController.Current.GamePlyCount
                : 0;

        // Exact legal-move / tactical searches are reserved for a small set
        // of high-value events. Quiet moves use the fast Shafran attack map.
        float capturedValueForGate =
            selectedMove.IsCapture && selectedMove.CapturedPiece.IsValid
                ? GetMaterialValue(selectedMove.CapturedPiece)
                : 0f;

        bool highValueCapture =
            selectedMove.IsCapture && capturedValueForGate >= 4.25f;

        bool sparseTacticalCadence =
            currentPly >= 24 && (currentPly % 32) == 0;

        bool probeCheck = highValueCapture || sparseTacticalCadence;

        bool givesCheck =
            probeCheck &&
            _moveGenerator != null &&
            _moveGenerator.IsInCheck(after, opponent);

        bool runExactTactical =
            highValueCapture || givesCheck || sparseTacticalCadence;

        // 1. Base evaluation (cheap)
        ApplyShapingComponent(
            evaluationDelta * evaluationRewardScale);

        // 1B. Opening initiative uses legal king-move generation, so sample
        // it only on tactical/cadence plies.
        if (runExactTactical)
        {
            ApplyShapingComponent(
                CalculateOpeningInitiativeReward(
                    before, after, evaluationDelta));
        }

        // 2. Material (cheap)
        ApplyShapingComponent(
            CalculateMaterialDeltaReward(before, after));

        // 3. Exact hanging/exposure analysis
        if (runExactTactical)
        {
            ApplyShapingComponent(
                CalculateExposedPiecePenalty(before, after));
            ApplyShapingComponent(
                CalculateHangingPiecePenalty(before, after));

            ApplyShapingComponent(
                CalculateOpeningMiddlegameTacticalPriorityReward(
                    before, after, selectedMove));

            ApplyShapingComponent(
                CalculateV9TacticalDecisionQualityReward(
                    before, after, selectedMove));

            ApplyShapingComponent(
                CalculateQueenHangingPenalty(after));
        }

        // 4-5. Positional signals are cheap enough to retain every ply.
        ApplyShapingComponent(
            CalculateCenterControlReward(before, after));
        ApplyShapingComponent(
            CalculatePiecePositionReward(before, after));

        // 6. King pressure / forcing / mating net use legal move generation.
        if (runExactTactical)
        {
            ApplyShapingComponent(
                CalculateKingPressureReward(before, after));
            ApplyShapingComponent(
                CalculateV10MatingNetReward(before, after));
            ApplyShapingComponent(
                CalculateV10ForcingMoveReward(
                    before, after, selectedMove));
        }

        // Three-color bishop activity is cheap.
        ApplyShapingComponent(
            CalculateV10BishopColorReward(before, after));

        // 7-9. Conversion and advanced tactics are exact-heavy.
        if (runExactTactical)
        {
            ApplyShapingComponent(
                CalculateEndgameConversionReward(
                    before, after, selectedMove));
            ApplyShapingComponent(
                CalculateMateConversionReward(
                    before, after, selectedMove));
            ApplyShapingComponent(
                CalculateAdvancedTacticalReward(
                    before, after, selectedMove));
        }

        // Development is a simple piece-state signal.
        ApplyShapingComponent(
            CalculateDevelopmentReward(before, selectedMove));

        if (runExactTactical)
        {
            ApplyShapingComponent(
                CalculateDefensiveRetreatReward(
                    before, after, selectedMove));
            ApplyShapingComponent(
                CalculateEndgameKingApproachReward(
                    before, after));
        }

        // Intrusion, repetition and no-progress are lightweight.
        ApplyShapingComponent(
            CalculateKingIntrusionDefenseReward(before, after));
        ApplyShapingComponent(
            CalculateAntiRepeatReward(
                before, after, selectedMove));
        ApplyShapingComponent(
            CalculateNoProgressReward(
                before, after, selectedMove, evaluationDelta));

        // Model A keeps Minimax guidance disabled; keep the call sparse for
        // Model B/C so it cannot dominate PPO environment throughput.
        if (runExactTactical)
        {
            ApplyShapingComponent(
                CalculateMinimaxGuidanceReward(
                    before, selectedMove));
        }
    }

    // =========================================================
    // COMPONENT CAP
    // =========================================================

    private void ApplyShapingComponent(
        float reward)
    {
        float clamped =
            Mathf.Clamp(
                reward,
                -strategicRewardPerComponentCap,
                strategicRewardPerComponentCap
            );

        AddShapingReward(clamped);
    }

    // =========================================================
    // CENTER CONTROL
    // =========================================================
    //
    // Old version counted only attacked center cells. That rewarded
    // long-range attacks from far away and did not distinguish actual
    // occupation of the center.
    //
    // New score combines:
    //   occupation = 1.00
    //   attack      = 0.35
    // and weights cells by their distance to the board center.
    // The score is normalized to approximately [0, 1], so the reward
    // remains stable across different board material configurations.
    // =========================================================

    private float CalculateCenterControlReward(
        GameState before,
        GameState after)
    {
        float beforeControl =
            CalculateCenterControlScore(
                before,
                agentPlayer
            );

        float afterControl =
            CalculateCenterControlScore(
                after,
                agentPlayer
            );

        float delta =
            afterControl -
            beforeControl;

        // Keep the existing metric as an approximate human-readable
        // center-control delta. Ten score points ~= one effective cell.

        // Center is valuable in the opening/middlegame, but should not
        // distract the agent from conversion in the endgame.
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

        float phaseMultiplier =
            0.65f * opening +
            1.00f * middlegame +
            0.25f * endgame;

        float rewardScale =
            Mathf.Max(
                centerControlRewardPerCell,
                0.06f
            );

        return
            delta *
            rewardScale *
            phaseMultiplier;
    }

    private float CalculateCenterControlScore(
        GameState state,
        EPlayer player)
    {
        if (state == null)
        {
            return 0f;
        }

        const float occupationWeight = 1.00f;
        const float attackWeight = 0.35f;

        float weightedScore = 0f;
        float totalWeight = 0f;

        foreach (
            Vector2Int cell
            in HexChessRules.CellCoordinates)
        {
            float centerWeight =
                GetCenterScore(
                    cell.x,
                    cell.y
                );

            // Ignore the outer ring. It is still part of the board but
            // should not be treated as meaningful central territory.
            if (centerWeight < 0.50f)
            {
                continue;
            }

            totalWeight += centerWeight;

            StatePiece? piece =
                state.GetPiece(
                    cell.x,
                    cell.y
                );

            if (piece.HasValue &&
                piece.Value.IsValid &&
                piece.Value.Player == player)
            {
                weightedScore +=
                    centerWeight *
                    occupationWeight;
            }

            if (_moveGenerator.IsSquareAttacked(
                    state,
                    cell.x,
                    cell.y,
                    player))
            {
                weightedScore +=
                    centerWeight *
                    attackWeight;
            }
        }

        if (totalWeight <= 0.0001f)
        {
            return 0f;
        }

        float maxScore =
            totalWeight *
            (occupationWeight + attackWeight);

        return Mathf.Clamp01(
            weightedScore /
            maxScore
        );
    }

    private float GetCenterScore(
        int x,
        int y)
    {
        int distance =
            HexChessRules.HexDistance(
                4,
                5,
                x,
                y
            );

        return Mathf.Clamp01(
            1f -
            distance / 7f
        );
    }

    // =========================================================
    // PIECE POSITION
    // =========================================================

    private float CalculatePiecePositionReward(
        GameState before,
        GameState after)
    {
        float beforeScore =
            GetPiecePositionScore(
                before,
                agentPlayer
            );

        float afterScore =
            GetPiecePositionScore(
                after,
                agentPlayer
            );

        return
            (afterScore - beforeScore) *
            positionDeltaRewardScale;
    }

    private float GetPiecePositionScore(
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

            if (!piece.HasValue ||
                !piece.Value.IsValid ||
                piece.Value.Player != player)
            {
                continue;
            }

            total +=
                GetPiecePositionScore(
                    state,
                    position,
                    piece.Value
                );
        }

        return total;
    }

    private float GetPiecePositionScore(
        GameState state,
        Vector2Int position,
        StatePiece piece)
    {
        float center =
            GetCenterScore(
                position.x,
                position.y
            );

        int degree =
            GetValidNeighborCount(
                position.x,
                position.y
            );

        bool corner = degree <= 2;
        bool edge = degree <= 4;

        switch (piece.Type)
        {
            case StatePieceType.PAWN:

                return
                    center * 0.9f +
                    (corner
                        ? -0.08f
                        : edge
                            ? -0.02f
                            : 0f);

            case StatePieceType.KNIGHT:

                return
                    center * 1.5f +
                    (corner
                        ? -0.80f
                        : edge
                            ? -0.30f
                            : 0f);

            case StatePieceType.BISHOP:

                return
                    center * 1.2f +
                    (corner
                        ? -0.50f
                        : edge
                            ? -0.15f
                            : 0f);

            case StatePieceType.ROOK:

                return
                    center * 0.4f +
                    GetEnemyTerritoryScore(
                        position,
                        piece.Player
                    ) * 0.8f;

            case StatePieceType.QUEEN:

                return
                    center * 0.7f +
                    GetEnemyTerritoryScore(
                        position,
                        piece.Player
                    ) * 0.5f +
                    (corner
                        ? -0.20f
                        : edge
                            ? -0.05f
                            : 0f);

            case StatePieceType.KING:
                return 0f;

            default:
                return 0f;
        }
    }

    private int GetValidNeighborCount(
        int x,
        int y)
    {
        int count = 0;

        foreach (
            Vector2Int direction
            in HexChessRules.RookDirections)
        {
            if (HexChessRules.IsValidCell(
                    x + direction.x,
                    y + direction.y))
            {
                count++;
            }
        }

        return count;
    }

    private float GetEnemyTerritoryScore(
        Vector2Int position,
        EPlayer player)
    {
        if (player == EPlayer.WHITE)
        {
            return Mathf.Clamp01(
                (position.y - 5f) /
                5f
            );
        }

        return Mathf.Clamp01(
            (5f - position.y) /
            5f
        );
    }

    // =========================================================
    // ENDGAME CONVERSION
    // =========================================================

    private float CalculateEndgameConversionReward(
        GameState before,
        GameState after,
        StateMove selectedMove)
    {
        if (before == null ||
            after == null ||
            !selectedMove.MovingPiece.IsValid ||
            _moveGenerator == null)
        {
            return 0f;
        }

        float phase =
            GetEndgameConversionPhase(after);

        if (phase <= 0.15f)
        {
            return 0f;
        }

        EPlayer opponent =
            HexChessRules.GetOpponent(agentPlayer);

        int ownNonKing =
            CountNonKingPiecesForReward(
                after,
                agentPlayer
            );

        int enemyNonKing =
            CountNonKingPiecesForReward(
                after,
                opponent
            );

        int ownNonPawnNonKing =
            CountNonPawnNonKingPiecesForReward(
                after,
                agentPlayer
            );

        int ownQueens =
            CountStatePieces(
                after,
                agentPlayer,
                StatePieceType.QUEEN
            );

        int ownPawns =
            CountStatePieces(
                after,
                agentPlayer,
                StatePieceType.PAWN
            );

        float reward = 0f;

        int beforeEnemyKingMoves =
            CountKingLegalMoves(
                before,
                opponent
            );

        int afterEnemyKingMoves =
            CountKingLegalMoves(
                after,
                opponent
            );

        int mobilityReduction =
            Mathf.Max(
                0,
                beforeEnemyKingMoves -
                afterEnemyKingMoves
            );

        bool givesCheck =
            _moveGenerator.IsInCheck(
                after,
                opponent
            );

        // IMPORTANT:
        // Pawns are NOT counted when deciding whether we are in
        // "major-piece mode". This prevents K+R+4P from being treated
        // as a "many pieces" position. The promotion system owns the pawn
        // conversion whenever <=4 non-pawn pieces remain.
        bool majorPieceMode =
            ownNonPawnNonKing >
            endgameMajorPieceCountThreshold;

        // After all pawns are promoted/lost, keep the strongest remaining
        // piece active even if only one Queen/Rook/etc. remains. This is
        // especially important for K+Q vs K so the King does not become
        // the default move generator.
        bool postPromotionMajorMode =
            ownPawns == 0 &&
            ownNonPawnNonKing > 0;

        bool promotionMode =
            ownPawns > 0 &&
            ownNonPawnNonKing <=
            endgameMajorPieceCountThreshold;

        // -----------------------------------------------------
        // MODE A / C:
        // MORE THAN FOUR NON-PAWN PIECES, OR POST-PROMOTION
        //
        // Priority:
        //   Queen > Rook > Bishop > Knight
        //
        // The priority is soft, not a hard move prohibition:
        // tactical captures/checks remain valid and a King may support
        // a concrete conversion objective.
        // -----------------------------------------------------
        if (majorPieceMode ||
            postPromotionMajorMode)
        {
            StatePieceType priorityType =
                GetStrongestAvailablePieceType(
                    after,
                    agentPlayer
                );

            bool priorityPieceHasLegalMove =
                HasLegalMoveOfType(
                    before,
                    agentPlayer,
                    priorityType
                );

            if (selectedMove.MovingPiece.Type ==
                priorityType)
            {
                reward +=
                    endgameMajorPieceActionReward *
                    phase;

                reward +=
                    endgameMajorPiecePriorityReward *
                    phase;

                if (selectedMove.IsCapture &&
                    selectedMove.CapturedPiece.IsValid &&
                    selectedMove.CapturedPiece.Type !=
                    StatePieceType.KING)
                {
                    reward +=
                        endgameMajorPieceCaptureBonus *
                        phase;
                }

                if (givesCheck)
                {
                    reward +=
                        endgameMajorPieceCheckBonus *
                        phase;
                }

                reward +=
                    Mathf.Min(
                        mobilityReduction,
                        3
                    ) *
                    endgameMajorPiecePressureBonus *
                    0.40f *
                    phase;
            }
            else if (selectedMove.MovingPiece.Type !=
                     StatePieceType.KING &&
                     selectedMove.MovingPiece.Type !=
                     StatePieceType.PAWN)
            {
                // A lower-priority piece is still allowed to make a
                // concrete tactical move. Quietly using a weaker piece
                // while the strongest piece has a legal move is discouraged.
                bool concrete =
                    selectedMove.IsCapture ||
                    givesCheck ||
                    mobilityReduction > 0;

                if (!concrete &&
                    priorityPieceHasLegalMove)
                {
                    reward -=
                        endgameLowerPriorityMovePenalty *
                        phase;
                }
            }

            // A pawn can still be advanced in a major-piece position.
            // Do not punish a pawn for making genuine promotion progress.
            if (selectedMove.MovingPiece.Type ==
                StatePieceType.PAWN)
            {
                int beforePromotionDistance =
                    GetPromotionDistance(
                        before,
                        selectedMove.From,
                        agentPlayer
                    );

                int afterPromotionDistance =
                    GetPromotionDistance(
                        after,
                        selectedMove.To,
                        agentPlayer
                    );

                if (beforePromotionDistance >= 0 &&
                    afterPromotionDistance >= 0 &&
                    afterPromotionDistance <
                    beforePromotionDistance)
                {
                    reward +=
                        endgamePawnAdvanceReward *
                        0.75f *
                        phase;
                }
            }

            if (selectedMove.MovingPiece.Type ==
                StatePieceType.KING)
            {
                bool kingSupports =
                    DoesKingMoveSupportConversion(
                        before,
                        after,
                        agentPlayer,
                        false,
                        true
                    );

                bool concreteKingMove =
                    givesCheck ||
                    mobilityReduction > 0;

                if (kingSupports)
                {
                    reward +=
                        endgameKingSupportReward *
                        phase;
                }
                else if (!concreteKingMove &&
                         priorityPieceHasLegalMove)
                {
                    // The strongest piece is available. A quiet King move
                    // is therefore explicitly undesirable.
                    reward -=
                        endgameKingMajorWanderingPenalty *
                        phase;
                }
                else if (!concreteKingMove)
                {
                    reward -=
                        endgameKingWanderingPenalty *
                        0.50f *
                        phase;
                }
            }
        }

        // -----------------------------------------------------
        // MODE B:
        // <= FOUR NON-PAWN PIECES + PAWN
        //
        // Promotion mode:
        //   1. Break pawn blockades
        //   2. Protect advanced pawns
        //   3. Advance
        //   4. Become passed
        //   5. Promote
        //
        // Two locked pawns are a special high-priority case.
        // -----------------------------------------------------
        if (promotionMode)
        {
            int blockedBefore =
                CountBlockedOwnPawns(
                    before,
                    agentPlayer
                );

            int blockedAfter =
                CountBlockedOwnPawns(
                    after,
                    agentPlayer
                );

            int blockadeReduction =
                Mathf.Max(
                    0,
                    blockedBefore -
                    blockedAfter
                );

            if (blockadeReduction > 0)
            {
                reward +=
                    blockadeReduction *
                    endgamePawnBlockadeBreakBonus *
                    phase;

                // Breaking a double-pawn lock is especially valuable:
                // it directly removes the "King-King dancing" loop.
                if (blockedBefore >= 2)
                {
                    reward +=
                        endgamePawnBreakthroughBonus *
                        phase;
                }
            }

            if (selectedMove.MovingPiece.Type ==
                StatePieceType.PAWN)
            {
                int beforePromotionDistance =
                    GetPromotionDistance(
                        before,
                        selectedMove.From,
                        agentPlayer
                    );

                int afterPromotionDistance =
                    GetPromotionDistance(
                        after,
                        selectedMove.To,
                        agentPlayer
                    );

                int progress =
                    beforePromotionDistance >= 0 &&
                    afterPromotionDistance >= 0
                        ? Mathf.Max(
                            0,
                            beforePromotionDistance -
                            afterPromotionDistance
                        )
                        : 0;

                if (progress > 0)
                {
                    reward +=
                        progress *
                        endgamePawnAdvanceReward *
                        phase;
                }

                if (afterPromotionDistance >= 0 &&
                    afterPromotionDistance <= 3)
                {
                    reward +=
                        endgamePawnNearPromotionReward *
                        Mathf.InverseLerp(
                            3f,
                            0f,
                            afterPromotionDistance
                        ) *
                        phase;
                }

                if (IsPassedPawn(
                        after,
                        selectedMove.To,
                        agentPlayer))
                {
                    reward +=
                        endgamePassedPawnReward *
                        phase;
                }

                if (IsProtectedByOwnPiece(
                        after,
                        selectedMove.To,
                        agentPlayer))
                {
                    reward +=
                        endgameProtectedPawnReward *
                        phase;
                }

                if (selectedMove.IsCapture &&
                    selectedMove.CapturedPiece.IsValid &&
                    selectedMove.CapturedPiece.Type ==
                    StatePieceType.PAWN)
                {
                    bool capturedBlocker =
                        IsPawnBlockingOwnPawn(
                            before,
                            new CLocation(
                                selectedMove.To.X,
                                selectedMove.To.Y
                            ),
                            agentPlayer
                        );

                    if (capturedBlocker)
                    {
                        reward +=
                            endgamePawnBlockerCaptureBonus *
                            phase;
                    }
                    else
                    {
                        reward +=
                            endgamePawnBlockerCaptureBonus *
                            0.35f *
                            phase;
                    }
                }
            }
            else if (selectedMove.MovingPiece.Type !=
                     StatePieceType.KING)
            {
                // A non-pawn quiet move should not steal the move from a
                // usable promotion plan unless it creates concrete tactics.
                if (!selectedMove.IsCapture &&
                    !givesCheck &&
                    mobilityReduction <= 0)
                {
                    reward -=
                        endgameQuietPawnFailurePenalty *
                        phase;
                }
            }

            // Reward an increase in the number of protected advanced pawns.
            int protectedBefore =
                CountAdvancedProtectedPawns(
                    before,
                    agentPlayer
                );

            int protectedAfter =
                CountAdvancedProtectedPawns(
                    after,
                    agentPlayer
                );

            int protectionGain =
                Mathf.Max(
                    0,
                    protectedAfter -
                    protectedBefore
                );

            if (protectionGain > 0)
            {
                reward +=
                    protectionGain *
                    endgameAdvancedPawnProtectionReward *
                    phase;
            }

            // If an advanced pawn is hanging, a quiet non-supporting move is
            // discouraged. Material-loss penalties remain the main safety
            // signal when the pawn is actually captured later.
            if (HasHangingAdvancedPawn(
                    after,
                    agentPlayer
                ) &&
                selectedMove.MovingPiece.Type !=
                StatePieceType.PAWN &&
                !selectedMove.IsCapture &&
                !givesCheck &&
                mobilityReduction <= 0)
            {
                reward -=
                    endgameAdvancedPawnProtectionReward *
                    0.75f *
                    phase;
            }

            if (selectedMove.MovingPiece.Type ==
                StatePieceType.KING)
            {
                bool kingSupportsPawn =
                    DoesKingMoveSupportConversion(
                        before,
                        after,
                        agentPlayer,
                        true,
                        false
                    );

                bool concreteKingMove =
                    givesCheck ||
                    mobilityReduction > 0;

                bool pawnBlockerCaptureAvailable =
                    HasLegalPawnBlockerCapture(
                        before,
                        agentPlayer
                    );

                if (kingSupportsPawn)
                {
                    reward +=
                        endgameKingSupportReward *
                        0.80f *
                        phase;
                }
                else if (pawnBlockerCaptureAvailable &&
                         !concreteKingMove)
                {
                    // There is a direct pawn-blocking capture available:
                    // King wandering must lose to the pawn conversion action.
                    reward -=
                        endgameKingWanderingPenalty *
                        phase;
                }
                else if (!concreteKingMove)
                {
                    reward -=
                        endgameKingWanderingPenalty *
                        0.75f *
                        phase;
                }
            }
        }

        // If the opponent has no non-King pieces, the final objective is
        // checkmate. Do not keep rewarding material hunting or King wandering.
        if (enemyNonKing == 0)
        {
            if (givesCheck)
            {
                reward +=
                    endgameMajorPieceCheckBonus *
                    1.25f *
                    phase;
            }

            reward +=
                Mathf.Min(
                    mobilityReduction,
                    4
                ) *
                endgameMajorPiecePressureBonus *
                0.85f *
                phase;

            // With a real piece available, quiet King moves should remain
            // secondary to using that piece for the mating net.
            if (selectedMove.MovingPiece.Type ==
                StatePieceType.KING &&
                ownNonPawnNonKing > 0 &&
                !givesCheck &&
                mobilityReduction <= 0)
            {
                StatePieceType priorityType =
                    GetStrongestAvailablePieceType(
                        after,
                        agentPlayer
                    );

                if (HasLegalMoveOfType(
                        before,
                        agentPlayer,
                        priorityType))
                {
                    reward -=
                        endgameKingMajorWanderingPenalty *
                        0.75f *
                        phase;
                }
            }
        }

        // Queen safety remains important during conversion.
        if (ownQueens > 0 &&
            selectedMove.MovingPiece.Type ==
            StatePieceType.QUEEN &&
            !selectedMove.IsCapture &&
            !givesCheck)
        {
            if (!IsPieceImmediatelyHanging(
                    after,
                    selectedMove.To,
                    opponent))
            {
                reward +=
                    endgameMajorPieceActionReward *
                    0.50f *
                    phase;
            }
        }

        _ = ownNonKing;

        return Mathf.Clamp(
            reward,
            -0.08f,
            0.12f
        );
    }

    private float GetEndgameConversionPhase(
        GameState state)
    {
        if (state == null)
        {
            return 0f;
        }

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
            HexChessRules.GetOpponent(
                agentPlayer
            );

        // Use the serialized Endgame Conversion thresholds instead of
        // leaving them as unused inspector fields. This makes the PPO
        // endgame-conversion trigger tunable without changing code.
        int opponentNonKing =
            CountNonKingPiecesForReward(
                state,
                opponent
            );

        float ownMaterial =
            HexChessRules.CalculateNonKingMaterial(
                state,
                agentPlayer
            );

        float opponentMaterial =
            HexChessRules.CalculateNonKingMaterial(
                state,
                opponent
            );

        float materialLead =
            ownMaterial - opponentMaterial;

        // Continuous trigger around the configured piece-count threshold.
        // At/below the threshold the signal is fully active; several extra
        // pieces fade it out smoothly rather than switching abruptly.
        float countTrigger =
            Mathf.InverseLerp(
                endgameOpponentNonKingThreshold + 4f,
                endgameOpponentNonKingThreshold,
                opponentNonKing
            );

        // Material lead trigger. It is gated by board simplification so a
        // large early advantage does not immediately turn the whole opening
        // into endgame play.
        float leadTrigger =
            Mathf.InverseLerp(
                endgameMaterialLeadThreshold * 0.5f,
                Mathf.Max(
                    endgameMaterialLeadThreshold,
                    endgameMaterialLeadThreshold * 0.5f + 0.001f
                ),
                materialLead
            );

        int ownNonKing =
            CountNonKingPiecesForReward(
                state,
                agentPlayer
            );

        float simplificationGate =
            Mathf.InverseLerp(
                18f,
                8f,
                ownNonKing + opponentNonKing
            );

        leadTrigger *= simplificationGate;

        // The explicit inspector thresholds are an additional strategic
        // trigger on top of the shared board-level phase detector.
        float thresholdPhase =
            Mathf.Max(
                countTrigger,
                leadTrigger
            );

        return Mathf.Clamp01(
            Mathf.Max(
                endgame,
                thresholdPhase
            )
        );
    }

    private int CountNonKingPiecesForReward(
        GameState state,
        EPlayer player)
    {
        if (state == null)
        {
            return 0;
        }

        int count = 0;

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

            count++;
        }

        return count;
    }

    // Counts only Q/R/B/N. Pawns are deliberately excluded so a position
    // such as K+R+4P enters pawn/promotion conversion rather than being
    // classified as a "many pieces" position.
    private int CountNonPawnNonKingPiecesForReward(
        GameState state,
        EPlayer player)
    {
        if (state == null)
        {
            return 0;
        }

        int count = 0;

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
                piece.Value.Type == StatePieceType.KING ||
                piece.Value.Type == StatePieceType.PAWN)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private bool HasLegalMoveOfType(
        GameState state,
        EPlayer player,
        StatePieceType type)
    {
        if (state == null ||
            _moveGenerator == null)
        {
            return false;
        }

        List<StateMove> legalMoves =
            _moveGenerator.GenerateLegalMoves(
                state,
                player
            );

        for (int i = 0; i < legalMoves.Count; i++)
        {
            if (legalMoves[i].MovingPiece.Type == type)
            {
                return true;
            }
        }

        return false;
    }

    // Returns true when the pawn located at blockerLocation is an enemy
    // pawn that currently blocks the forward path of at least one own pawn.
    private bool IsPawnBlockingOwnPawn(
        GameState state,
        CLocation blockerLocation,
        EPlayer player)
    {
        if (state == null) return false;

        StatePiece? blocker = state.GetPiece(
            blockerLocation.X, blockerLocation.Y);

        EPlayer opponent = HexChessRules.GetOpponent(player);
        if (!blocker.HasValue || !blocker.Value.IsValid ||
            blocker.Value.Player != opponent ||
            blocker.Value.Type != StatePieceType.PAWN)
        {
            return false;
        }

        // In Shafran the pawn moves forward on the same file (dy = +/-1).
        // A real blocker is therefore an enemy pawn on the exact forward cell,
        // not merely any enemy pawn somewhere ahead/diagonally.
        int direction = player == EPlayer.WHITE ? 1 : -1;
        int pawnX = blockerLocation.X;
        int pawnY = blockerLocation.Y - direction;

        if (!HexChessRules.IsValidCell(pawnX, pawnY))
            return false;

        StatePiece? ownPawn = state.GetPiece(pawnX, pawnY);
        return ownPawn.HasValue && ownPawn.Value.IsValid &&
               ownPawn.Value.Player == player &&
               ownPawn.Value.Type == StatePieceType.PAWN;
    }

    private int CountBlockedOwnPawns(
        GameState state,
        EPlayer player)
    {
        if (state == null) return 0;

        int count = 0;
        int direction = player == EPlayer.WHITE ? 1 : -1;

        foreach (Vector2Int position in HexChessRules.CellCoordinates)
        {
            StatePiece? pawn = state.GetPiece(position.x, position.y);
            if (!pawn.HasValue || !pawn.Value.IsValid ||
                pawn.Value.Player != player ||
                pawn.Value.Type != StatePieceType.PAWN)
            {
                continue;
            }

            int frontY = position.y + direction;
            if (!HexChessRules.IsValidCell(position.x, frontY))
                continue;

            StatePiece? front = state.GetPiece(position.x, frontY);
            if (front.HasValue && front.Value.IsValid &&
                front.Value.Player == HexChessRules.GetOpponent(player) &&
                front.Value.Type == StatePieceType.PAWN)
            {
                count++;
            }
        }

        return count;
    }

    private bool HasLegalPawnBlockerCapture(
        GameState state,
        EPlayer player)
    {
        if (state == null ||
            _moveGenerator == null)
        {
            return false;
        }

        List<StateMove> legalMoves =
            _moveGenerator.GenerateLegalMoves(
                state,
                player
            );

        for (int i = 0; i < legalMoves.Count; i++)
        {
            StateMove move = legalMoves[i];

            if (!move.IsCapture ||
                !move.CapturedPiece.IsValid ||
                move.CapturedPiece.Type !=
                StatePieceType.PAWN)
            {
                continue;
            }

            if (IsPawnBlockingOwnPawn(
                    state,
                    new CLocation(
                        move.To.X,
                        move.To.Y
                    ),
                    player))
            {
                return true;
            }
        }

        return false;
    }

    private int CountAdvancedProtectedPawns(
        GameState state,
        EPlayer player)
    {
        if (state == null ||
            _moveGenerator == null)
        {
            return 0;
        }

        int count = 0;

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
                piece.Value.Type != StatePieceType.PAWN)
            {
                continue;
            }

            int promotionDistance =
                GetPromotionDistance(
                    state,
                    new CLocation(
                        position.x,
                        position.y
                    ),
                    player
                );

            if (promotionDistance < 0 ||
                promotionDistance > 4)
            {
                continue;
            }

            if (IsProtectedByOwnPiece(
                    state,
                    new CLocation(
                        position.x,
                        position.y
                    ),
                    player))
            {
                count++;
            }
        }

        return count;
    }

    private bool HasHangingAdvancedPawn(
        GameState state,
        EPlayer player)
    {
        if (state == null ||
            _moveGenerator == null)
        {
            return false;
        }

        EPlayer opponent =
            HexChessRules.GetOpponent(player);

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
                piece.Value.Type != StatePieceType.PAWN)
            {
                continue;
            }

            int promotionDistance =
                GetPromotionDistance(
                    state,
                    new CLocation(
                        position.x,
                        position.y
                    ),
                    player
                );

            if (promotionDistance < 0 ||
                promotionDistance > 4)
            {
                continue;
            }

            bool attacked =
                ShafranTacticalState.GetLegalCaptureCount(
                    state, opponent, position.x, position.y) > 0;

            bool isProtected =
                IsProtectedByOwnPiece(
                    state,
                    new CLocation(
                        position.x,
                        position.y
                    ),
                    player
                );

            if (attacked && !isProtected)
            {
                return true;
            }
        }

        return false;
    }

    // =========================================================
    // OPENING / MIDDLEGAME TACTICAL PRIORITY
    // =========================================================

    private float CalculateOpeningMiddlegameTacticalPriorityReward(
        GameState before,
        GameState after,
        StateMove selectedMove)
    {
        if (before == null ||
            after == null ||
            _moveGenerator == null)
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

        // This component is intentionally focused on opening and
        // middlegame mistakes. Endgame conversion has its own system.
        float phase =
            Mathf.Clamp01(
                0.80f * opening +
                1.25f * middlegame
            );

        if (phase <= 0.01f)
        {
            return 0f;
        }

        EPlayer opponent =
            HexChessRules.GetOpponent(agentPlayer);

        float reward = 0f;

        // -----------------------------------------------------
        // 1. SAVE CRITICAL OWN MATERIAL
        // -----------------------------------------------------
        float criticalOwnBefore =
            GetCriticalHangingMaterialScore(
                before,
                agentPlayer
            );

        float criticalOwnAfter =
            GetCriticalHangingMaterialScore(
                after,
                agentPlayer
            );

        float savedMaterial =
            Mathf.Max(
                0f,
                criticalOwnBefore -
                criticalOwnAfter
            );

        if (savedMaterial > 0f)
        {
            reward +=
                Mathf.Clamp(
                    savedMaterial *
                    criticalDefenseReward,
                    0f,
                    0.040f
                );
        }

        // When a valuable piece is hanging and the move does not solve
        // that problem, apply a direct tactical penalty. Counterplay is
        // allowed when the move captures or gives a concrete check.
        bool givesCheck =
            _moveGenerator.IsInCheck(
                after,
                opponent
            );

        bool concreteCounterplay =
            selectedMove.IsCapture ||
            givesCheck;

        if (criticalOwnBefore > 0.25f &&
            criticalOwnAfter >= criticalOwnBefore - 0.05f &&
            !concreteCounterplay)
        {
            reward -=
                Mathf.Clamp(
                    criticalOwnBefore *
                    criticalDefensePenalty,
                    0f,
                    0.045f
                );
        }

        // -----------------------------------------------------
        // 2. TAKE A FREE VALUABLE PIECE
        // -----------------------------------------------------
        if (selectedMove.IsCapture &&
            selectedMove.CapturedPiece.IsValid)
        {
            StatePiece? targetBefore =
                before.GetPiece(
                    selectedMove.To.X,
                    selectedMove.To.Y
                );

            if (targetBefore.HasValue &&
                targetBefore.Value.IsValid &&
                targetBefore.Value.Player == opponent)
            {
                float targetValue =
                    GetMaterialValue(
                        targetBefore.Value
                    );

                float exchangeNet =
                    CalculateCaptureExchangeNet(
                        before,
                        selectedMove,
                        agentPlayer,
                        tacticalExchangeDepth
                    );

                // A target being undefended is no longer enough by itself.
                // What matters is whether the whole capture sequence is
                // actually profitable after the opponent's legal replies.
                if (targetValue >= 3.50f &&
                    exchangeNet >= 0.75f)
                {
                    reward +=
                        freeCaptureReward *
                        Mathf.Clamp01(
                            targetValue / 12.5f
                        );
                }
            }
        }
        else if (HasFavorableHighValueCaptureOpportunity(
                     before,
                     agentPlayer))
        {
            // Missing a clearly favorable Q/R/B/N exchange is one of the
            // main early/middlegame failure modes. V8 uses exchange
            // safety rather than only a simple "undefended" test.
            reward -=
                freeCaptureMissPenalty;
        }

        // -----------------------------------------------------
        // 3. PROMPT RECAPTURE AFTER LOSING MATERIAL
        // -----------------------------------------------------
        bool immediateRecapture =
            IsImmediateRecaptureMove(
                before,
                selectedMove
            );

        if (immediateRecapture)
        {
            reward +=
                Mathf.Max(
                    recaptureReward,
                    accurateRecaptureReward
                );
        }
        else if (HasImmediateRecaptureOpportunity(
                     before,
                     agentPlayer))
        {
            // The opponent just captured something on this square. If an
            // immediate legal recapture exists and the agent ignores it
            // with a quiet move, give a direct tactical negative signal.
            reward -=
                missedRecapturePenalty;
        }

        // -----------------------------------------------------
        // 4. ATTACK ENEMY VALUABLE / LOOSE MATERIAL
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

        float targetGain =
            Mathf.Max(
                0f,
                enemyLooseAfter -
                enemyLooseBefore
            );

        if (targetGain > 0f)
        {
            reward +=
                Mathf.Clamp(
                    targetGain *
                    valuableAttackReward,
                    0f,
                    0.022f
                );
        }

        // -----------------------------------------------------
        // 5. OPENING DEVELOPMENT QUALITY
        // -----------------------------------------------------
        // A first-time minor-piece move is more useful when it is also
        // safe. This complements the existing development reward.
        if (opening > 0.10f &&
            (selectedMove.MovingPiece.Type == StatePieceType.KNIGHT ||
             selectedMove.MovingPiece.Type == StatePieceType.BISHOP))
        {
            StatePiece? movedAfter =
                after.GetPiece(
                    selectedMove.To.X,
                    selectedMove.To.Y
                );

            if (movedAfter.HasValue &&
                movedAfter.Value.IsValid)
            {
                bool attackedAfter =
                    ShafranTacticalState.GetLegalCaptureCount(
                        after, opponent, selectedMove.To.X, selectedMove.To.Y) > 0;

                bool protectedAfter =
                    ShafranTacticalState.GetLegalDefenseCount(
                        after, agentPlayer, selectedMove.To.X, selectedMove.To.Y) > 0;

                if (!attackedAfter || protectedAfter)
                {
                    reward +=
                        openingDevelopmentQualityBonus *
                        opening;
                }
            }
        }

        return
            reward *
            phase *
            Mathf.Max(0.50f, tacticalPhaseWeight);
    }

    private float GetCriticalHangingMaterialScore(
        GameState state,
        EPlayer player)
    {
        if (state == null ||
            _moveGenerator == null)
        {
            return 0f;
        }

        EPlayer opponent =
            HexChessRules.GetOpponent(player);

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

            float value =
                GetMaterialValue(
                    piece.Value
                );

            // Pawns are tracked separately by the endgame system.
            // Here the focus is Q/R/B/N tactical safety.
            if (piece.Value.Type == StatePieceType.PAWN ||
                value < 3.50f)
            {
                continue;
            }

            // Fast first pass: only pieces that are geometrically attacked
            // and have no geometric support are candidates for exact legal
            // verification. This removes repeated full legal-move generation
            // from every ordinary PPO step.
            int pseudoAttackers =
                ShafranTacticalState.GetPseudoAttackCount(
                    state, opponent, position.x, position.y);
            int pseudoDefenders =
                ShafranTacticalState.GetPseudoAttackCount(
                    state, player, position.x, position.y);

            if (pseudoAttackers <= 0 ||
                pseudoDefenders > 0)
            {
                continue;
            }

            bool attacked =
                ShafranTacticalState.GetPseudoAttackCount(
                    state, opponent, position.x, position.y) > 0;

            if (!attacked)
                continue;

            bool isProtected =
                ShafranTacticalState.GetPseudoAttackCount(
                    state, player, position.x, position.y) > 0;

            if (!isProtected)
                score += value;
        }

        return score;
    }

    private bool ResolvesCriticalPieceThreat(
        GameState before,
        GameState after,
        StateMove selectedMove)
    {
        if (before == null ||
            after == null ||
            _moveGenerator == null)
        {
            return false;
        }

        if (selectedMove.MovingPiece.Type ==
            StatePieceType.KING)
        {
            return false;
        }

        StatePiece? movedBefore =
            before.GetPiece(
                selectedMove.From.X,
                selectedMove.From.Y
            );

        StatePiece? movedAfter =
            after.GetPiece(
                selectedMove.To.X,
                selectedMove.To.Y
            );

        if (!movedBefore.HasValue ||
            !movedAfter.HasValue ||
            !movedBefore.Value.IsValid ||
            !movedAfter.Value.IsValid)
        {
            return false;
        }

        if (movedBefore.Value.Type ==
            StatePieceType.PAWN)
        {
            return false;
        }

        float value =
            GetMaterialValue(
                movedBefore.Value
            );

        if (value < 3.50f)
        {
            return false;
        }

        EPlayer opponent =
            HexChessRules.GetOpponent(agentPlayer);

        bool attackedBefore =
            ShafranTacticalState.GetLegalCaptureCount(
                before, opponent, selectedMove.From.X, selectedMove.From.Y) > 0;

        if (!attackedBefore)
        {
            return false;
        }

        bool safeAfter =
            ShafranTacticalState.GetLegalCaptureCount(
                after, opponent, selectedMove.To.X, selectedMove.To.Y) <= 0;

        bool protectedAfter =
            ShafranTacticalState.GetLegalDefenseCount(
                after, agentPlayer, selectedMove.To.X, selectedMove.To.Y) > 0;

        return safeAfter || protectedAfter;
    }

    // =========================================================
    // V8 TACTICAL EXCHANGE / THREAT AWARENESS
    // =========================================================

    private float CalculateV8TacticalExchangeSafetyReward(
        GameState before,
        GameState after,
        StateMove selectedMove)
    {
        if (before == null ||
            after == null ||
            _moveGenerator == null)
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

        float phase = Mathf.Clamp01(
            0.75f * opening +
            1.20f * middlegame +
            0.95f * endgame
        );

        if (phase <= 0.05f)
        {
            return 0f;
        }

        EPlayer opponent =
            HexChessRules.GetOpponent(agentPlayer);

        float reward = 0f;

        // -----------------------------------------------------
        // 1. SELECTED MOVE EXCHANGE SAFETY
        // -----------------------------------------------------
        // Treat the move's destination as the tactical square. For a
        // capture, initial gain = captured value. For a quiet move,
        // initial gain = 0, so the continuation directly measures how
        // much the opponent can win by taking our moved piece.
        if (selectedMove.MovingPiece.Type != StatePieceType.KING)
        {
            float exchangeNet =
                CalculateMoveTacticalExchangeNet(
                    after,
                    selectedMove,
                    agentPlayer,
                    tacticalExchangeDepth
                );

            if (logTacticalExchangeDiagnostics &&
                (exchangeNet >= 1.0f || exchangeNet <= -1.0f))
            {
                ShafranDebug.Log(
                    $"[V8 TACTICAL EXCHANGE] " +
                    $"Episode={DebugEpisodeId} | " +
                    $"Move={selectedMove.From.X},{selectedMove.From.Y}->" +
                    $"{selectedMove.To.X},{selectedMove.To.Y} | " +
                    $"Capture={(selectedMove.IsCapture ? selectedMove.CapturedPiece.Type.ToString() : "NONE")} | " +
                    $"SEE={exchangeNet:0.00}"
                );
            }

            bool givesCheck =
                _moveGenerator.IsInCheck(
                    after,
                    opponent
                );

            if (exchangeNet >= 1.0f)
            {
                reward +=
                    favorableExchangeReward *
                    Mathf.Clamp01(exchangeNet / 7.0f);
            }
            else if (exchangeNet <= -4.0f)
            {
                float penalty =
                    severeExchangePenalty;

                if (givesCheck)
                {
                    penalty *=
                        tacticalCheckSacrificeDiscount;
                }

                reward -= penalty;
            }
            else if (exchangeNet <= -1.0f)
            {
                float penalty =
                    badExchangePenalty *
                    Mathf.Clamp01(
                        Mathf.Abs(exchangeNet) / 7.0f
                    );

                if (givesCheck)
                {
                    penalty *=
                        tacticalCheckSacrificeDiscount;
                }

                reward -= penalty;
            }

            // A defended Q/R/B/N capture that is only an equal trade is not
            // a tactical "free win". Keep a small discouraging signal unless
            // it is an exact recapture or forcing check.
            if (selectedMove.IsCapture &&
                selectedMove.CapturedPiece.IsValid &&
                selectedMove.CapturedPiece.Type != StatePieceType.KING)
            {
                bool targetDefended =
                    ShafranTacticalState.GetLegalDefenseCount(
                        before, opponent, selectedMove.To.X, selectedMove.To.Y) > 0;

                float capturedValue =
                    GetMaterialValue(
                        selectedMove.CapturedPiece
                    );

                if (targetDefended &&
                    capturedValue >= 3.50f &&
                    exchangeNet < 0.75f &&
                    !IsImmediateRecaptureMove(
                        before,
                        selectedMove
                    ) &&
                    !givesCheck)
                {
                    reward -=
                        protectedNeutralCapturePenalty;
                }
            }
        }

        // -----------------------------------------------------
        // 2. REMOVE THE OPPONENT'S TACTICAL THREAT
        // -----------------------------------------------------
        // Before our move, estimate the opponent's strongest favorable
        // Q/R/B/N capture. After our move, estimate it again. A good move
        // should reduce a concrete enemy capture opportunity rather than
        // leave the same hanging piece in place.
        float enemyThreatBefore =
            GetBestHighValueCaptureGain(
                before,
                opponent,
                tacticalOpportunityDepth
            );

        float enemyThreatAfter =
            GetBestHighValueCaptureGain(
                after,
                opponent,
                tacticalOpportunityDepth
            );

        float threatDelta =
            enemyThreatBefore -
            enemyThreatAfter;

        if (threatDelta >= 1.0f)
        {
            reward +=
                threatRemovalReward *
                Mathf.Clamp01(
                    threatDelta / 7.0f
                );
        }
        else if (threatDelta <= -1.0f)
        {
            float penalty =
                immediateThreatPenalty *
                Mathf.Clamp01(
                    Mathf.Abs(threatDelta) / 7.0f
                );

            // A forcing check can intentionally postpone material defense.
            if (_moveGenerator.IsInCheck(after, opponent))
            {
                penalty *=
                    tacticalCheckSacrificeDiscount;
            }

            reward -= penalty;
        }

        // -----------------------------------------------------
        // 3. DIRECT RECAPTURE ACCURACY
        // -----------------------------------------------------
        if (IsImmediateRecaptureMove(
                before,
                selectedMove))
        {
            reward +=
                accurateRecaptureReward;
        }

        return
            reward *
            phase *
            Mathf.Max(0.65f, tacticalPhaseWeight);
    }

    private float CalculateMoveTacticalExchangeNet(
        GameState after,
        StateMove selectedMove,
        EPlayer mover,
        int depth)
    {
        if (after == null ||
            _moveGenerator == null ||
            selectedMove.MovingPiece.Type == StatePieceType.KING)
        {
            return 0f;
        }

        float initialGain =
            selectedMove.IsCapture &&
            selectedMove.CapturedPiece.IsValid
                ? GetMaterialValue(
                    selectedMove.CapturedPiece
                )
                : 0f;

        float opponentContinuation =
            EvaluateWorstTacticalReplyLossV9(
                after,
                HexChessRules.GetOpponent(mover),
                mover,
                Mathf.Max(1, depth)
            );

        return
            initialGain -
            opponentContinuation;
    }

    private float CalculateCaptureExchangeNet(
        GameState state,
        StateMove capture,
        EPlayer capturer,
        int depth)
    {
        if (state == null ||
            _moveGenerator == null ||
            !capture.IsCapture ||
            !capture.CapturedPiece.IsValid)
        {
            return -999f;
        }

        if (capture.CapturedPiece.Type ==
            StatePieceType.KING)
        {
            return kingValue;
        }

        float initialGain =
            GetMaterialValue(
                capture.CapturedPiece
            );

        GameState after =
            state.Clone();

        _moveGenerator.MakeMove(
            after,
            capture
        );

        float opponentContinuation =
            EvaluateWorstTacticalReplyLossV9(
                after,
                HexChessRules.GetOpponent(capturer),
                capturer,
                Mathf.Max(1, depth)
            );

        return
            initialGain -
            opponentContinuation;
    }

    private float EvaluateCaptureContinuation(
        GameState state,
        EPlayer sideToMove,
        int targetX,
        int targetY,
        int depth)
    {
        if (state == null ||
            _moveGenerator == null ||
            depth <= 0)
        {
            return 0f;
        }

        List<StateMove> legalCaptures =
            _moveGenerator.GenerateLegalCaptureMoves(
                state,
                sideToMove
            );

        bool found = false;
        float best = 0f;

        for (int i = 0; i < legalCaptures.Count; i++)
        {
            StateMove move =
                legalCaptures[i];

            if (!move.IsCapture ||
                move.To.X != targetX ||
                move.To.Y != targetY ||
                !move.CapturedPiece.IsValid)
            {
                continue;
            }

            float gained =
                move.CapturedPiece.Type == StatePieceType.KING
                    ? kingValue
                    : GetMaterialValue(
                        move.CapturedPiece
                    );

            GameState next =
                state.Clone();

            _moveGenerator.MakeMove(
                next,
                move
            );

            float reply =
                EvaluateCaptureContinuation(
                    next,
                    HexChessRules.GetOpponent(sideToMove),
                    targetX,
                    targetY,
                    depth - 1
                );

            float net =
                gained -
                reply;

            if (!found || net > best)
            {
                best = net;
                found = true;
            }
        }

        return found
            ? best
            : 0f;
    }

    private float GetBestHighValueCaptureGain(
        GameState state,
        EPlayer player,
        int depth)
    {
        if (state == null ||
            _moveGenerator == null)
        {
            return 0f;
        }

        List<StateMove> legalCaptures =
            _moveGenerator.GenerateLegalCaptureMoves(
                state,
                player
            );

        float best = 0f;

        for (int i = 0; i < legalCaptures.Count; i++)
        {
            StateMove move =
                legalCaptures[i];

            if (!move.IsCapture ||
                !move.CapturedPiece.IsValid)
            {
                continue;
            }

            StatePieceType capturedType =
                move.CapturedPiece.Type;

            if (capturedType == StatePieceType.KING ||
                capturedType == StatePieceType.PAWN)
            {
                continue;
            }

            float capturedValue =
                GetMaterialValue(
                    move.CapturedPiece
                );

            if (capturedValue < 3.50f)
            {
                continue;
            }

            float net =
                CalculateV9CaptureNet(
                    state,
                    move,
                    player,
                    depth
                );

            if (net > best)
            {
                best = net;
            }
        }

        return best;
    }

    private bool HasFavorableHighValueCaptureOpportunity(
        GameState state,
        EPlayer player)
    {
        if (state == null || _moveGenerator == null)
            return false;

        // Cheap quiet-move screen. Deep exchange analysis is reserved for
        // concrete captures so ordinary PPO steps stay fast.
        List<StateMove> captures =
            _moveGenerator.GenerateLegalCaptureMoves(state, player);

        for (int i = 0; i < captures.Count; i++)
        {
            StateMove move = captures[i];
            if (!move.IsCapture ||
                !move.CapturedPiece.IsValid ||
                move.CapturedPiece.Type == StatePieceType.KING ||
                move.CapturedPiece.Type == StatePieceType.PAWN)
            {
                continue;
            }

            float value = GetMaterialValue(move.CapturedPiece);
            if (value < 7.0f)
                continue;

            int support = ShafranTacticalState.GetPseudoAttackCount(
                state,
                HexChessRules.GetOpponent(player),
                move.To.X,
                move.To.Y);

            if (support <= 1)
                return true;
        }

        return false;
    }

    private bool IsImmediateRecaptureMove(
        GameState before,
        StateMove selectedMove)
    {
        if (before == null ||
            !selectedMove.IsCapture ||
            !before.LastMove.HasValue)
        {
            return false;
        }

        StateMove lastMove =
            before.LastMove.Value;

        if (lastMove.MovingPiece.Player == agentPlayer)
        {
            return false;
        }

        return
            selectedMove.To.X == lastMove.To.X &&
            selectedMove.To.Y == lastMove.To.Y;
    }

    private bool HasImmediateRecaptureOpportunity(
        GameState state,
        EPlayer player)
    {
        if (state == null ||
            !state.LastMove.HasValue ||
            _moveGenerator == null)
        {
            return false;
        }

        StateMove lastMove = state.LastMove.Value;
        if (lastMove.MovingPiece.Player == player)
            return false;

        if (ShafranTacticalState.GetPseudoAttackCount(
                state, player, lastMove.To.X, lastMove.To.Y) <= 0)
        {
            return false;
        }

        return ShafranTacticalState.GetLegalCaptureCount(
            state, player, lastMove.To.X, lastMove.To.Y) > 0;
    }

    // =========================================================
    // DEVELOPMENT
    // =========================================================

    private float CalculateDevelopmentReward(
        GameState before,
        StateMove selectedMove)
    {
        if (before == null)
        {
            return 0f;
        }

        float opening;
        float middlegame;
        float endgame;

        HexChessRules.GetStrategicPhase(
            before,
            agentPlayer,
            out opening,
            out middlegame,
            out endgame
        );

        // Development is an opening signal. Keep it useful in early play
        // and almost neutral once the middlegame/endgame begins.
        float developmentScale =
            Mathf.Clamp01(
                opening * 0.35f +
                middlegame * 0.08f
            );

        if (developmentScale <= 0f)
        {
            return 0f;
        }

        StatePiece? movedBefore =
            before.GetPiece(
                selectedMove.From.X,
                selectedMove.From.Y
            );

        if (!movedBefore.HasValue ||
            !movedBefore.Value.IsValid ||
            movedBefore.Value.HasMoved)
        {
            return 0f;
        }

        GameState after =
            GameState.FromUnityBoard();

        EPlayer opponent =
            HexChessRules.GetOpponent(agentPlayer);

        bool givesCheck =
            after != null &&
            _moveGenerator != null &&
            _moveGenerator.IsInCheck(
                after,
                opponent
            );

        bool resolvesCriticalThreat =
            after != null &&
            ResolvesCriticalPieceThreat(
                before,
                after,
                selectedMove
            );

        // Development is not a reason to ignore a concrete tactical move.
        // In particular, an early Queen/Rook capture or a defensive move
        // that saves a hanging piece must not receive the normal opening
        // "early piece" penalty.
        bool urgentTacticalMove =
            selectedMove.IsCapture ||
            givesCheck ||
            resolvesCriticalThreat;

        switch (movedBefore.Value.Type)
        {
            case StatePieceType.KNIGHT:

                if (resolvesCriticalThreat)
                {
                    return
                        knightDevelopmentReward *
                        developmentScale +
                        openingDevelopmentQualityBonus *
                        developmentScale;
                }

                return
                    knightDevelopmentReward *
                    developmentScale;

            case StatePieceType.BISHOP:

                if (resolvesCriticalThreat)
                {
                    return
                        bishopDevelopmentReward *
                        developmentScale +
                        openingDevelopmentQualityBonus *
                        developmentScale;
                }

                return
                    bishopDevelopmentReward *
                    developmentScale;

            case StatePieceType.ROOK:

                if (urgentTacticalMove)
                {
                    return 0f;
                }

                return
                    -earlyRookMovePenalty *
                    developmentScale;

            case StatePieceType.QUEEN:

                if (urgentTacticalMove)
                {
                    return 0f;
                }

                return
                    -earlyQueenMovePenalty *
                    developmentScale;

            default:

                return 0f;
        }
    }

    // =========================================================
    // KING PRESSURE
    // =========================================================

    private float CalculateKingPressureReward(
        GameState before,
        GameState after)
    {
        EPlayer opponent =
            HexChessRules.GetOpponent(
                agentPlayer
            );

        int beforeKingMoves =
            CountKingLegalMoves(
                before,
                opponent
            );

        int afterKingMoves =
            CountKingLegalMoves(
                after,
                opponent
            );

        int reduction =
            beforeKingMoves -
            afterKingMoves;

        return
            reduction *
            kingPressurePerMove;
    }

    private int CountKingLegalMoves(
        GameState state,
        EPlayer player)
    {
        if (state == null)
        {
            return 0;
        }

        List<StateMove> moves =
            _moveGenerator.GenerateLegalMoves(
                state,
                player
            );

        int count = 0;

        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].MovingPiece.Type ==
                StatePieceType.KING)
            {
                count++;
            }
        }

        return count;
    }

    // =========================================================
    // KING INTRUSION
    // =========================================================

    private float CalculateKingIntrusionDefenseReward(
        GameState before,
        GameState after)
    {
        float beforeIntrusion =
            GetEnemyKingIntrusion(
                before,
                agentPlayer
            );

        float afterIntrusion =
            GetEnemyKingIntrusion(
                after,
                agentPlayer
            );

        float improvement =
            beforeIntrusion -
            afterIntrusion;

        return
            improvement *
            kingIntrusionRewardScale;
    }

    // =========================================================
    // DEFENSIVE RETREAT
    // =========================================================

    private float CalculateDefensiveRetreatReward(
        GameState before,
        GameState after,
        StateMove selectedMove)
    {
        if (before == null || after == null)
        {
            return 0f;
        }

        StatePiece? movedBefore =
            before.GetPiece(
                selectedMove.From.X,
                selectedMove.From.Y
            );

        StatePiece? movedAfter =
            after.GetPiece(
                selectedMove.To.X,
                selectedMove.To.Y
            );

        if (!movedBefore.HasValue ||
            !movedAfter.HasValue)
        {
            return 0f;
        }

        if (movedBefore.Value.Type ==
            StatePieceType.PAWN)
        {
            return 0f;
        }

        Vector2Int ownKingBefore =
            FindKing(
                before,
                agentPlayer
            );

        if (ownKingBefore.x < 0)
        {
            return 0f;
        }

        int beforeDistance =
            HexDistance(
                selectedMove.From.X,
                selectedMove.From.Y,
                ownKingBefore.x,
                ownKingBefore.y
            );

        int afterDistance =
            HexDistance(
                selectedMove.To.X,
                selectedMove.To.Y,
                ownKingBefore.x,
                ownKingBefore.y
            );

        float beforeIntrusion =
            GetEnemyKingIntrusion(
                before,
                agentPlayer
            );

        float afterIntrusion =
            GetEnemyKingIntrusion(
                after,
                agentPlayer
            );

        if (afterDistance < beforeDistance &&
            afterIntrusion < beforeIntrusion)
        {
            return defensiveRetreatReward;
        }

        return 0f;
    }

    private int HexDistance(
        int x1,
        int y1,
        int x2,
        int y2)
    {
        return HexChessRules.HexDistance(
            x1,
            y1,
            x2,
            y2
        );
    }

    private float GetEnemyKingIntrusion(
        GameState state,
        EPlayer player)
    {
        EPlayer enemy =
            HexChessRules.GetOpponent(
                player
            );

        Vector2Int enemyKing =
            FindKing(
                state,
                enemy
            );

        if (enemyKing.x < 0)
        {
            return 0f;
        }

        if (player == EPlayer.WHITE)
        {
            return Mathf.Max(
                0f,
                5.5f - enemyKing.y
            );
        }

        return Mathf.Max(
            0f,
            enemyKing.y - 5.5f
        );
    }

    // =========================================================
    // ENDGAME KING APPROACH
    // =========================================================

    private float CalculateEndgameKingApproachReward(
        GameState before,
        GameState after)
    {
        if (before == null ||
            after == null)
        {
            return 0f;
        }

        float phase =
            GetEndgameConversionPhase(after);

        if (phase <= 0.15f)
        {
            return 0f;
        }

        Vector2Int ownKingBefore =
            FindKing(
                before,
                agentPlayer
            );

        Vector2Int ownKingAfter =
            FindKing(
                after,
                agentPlayer
            );

        if (ownKingBefore.x < 0 ||
            ownKingAfter.x < 0 ||
            ownKingBefore == ownKingAfter)
        {
            return 0f;
        }

        EPlayer opponent =
            HexChessRules.GetOpponent(agentPlayer);

        bool supported =
            DoesKingMoveSupportConversion(
                before,
                after,
                agentPlayer,
                CountStatePieces(after, agentPlayer, StatePieceType.PAWN) > 0 &&
                CountNonPawnNonKingPiecesForReward(after, agentPlayer) <=
                    endgameMajorPieceCountThreshold,
                CountNonPawnNonKingPiecesForReward(after, agentPlayer) >
                    endgameMajorPieceCountThreshold ||
                (CountStatePieces(after, agentPlayer, StatePieceType.PAWN) == 0 &&
                 CountNonPawnNonKingPiecesForReward(after, agentPlayer) > 0)
            );

        int beforeEnemyMoves =
            CountKingLegalMoves(
                before,
                opponent
            );

        int afterEnemyMoves =
            CountKingLegalMoves(
                after,
                opponent
            );

        bool givesCheck =
            _moveGenerator != null &&
            _moveGenerator.IsInCheck(
                after,
                opponent
            );

        int enemyMobilityReduction =
            Mathf.Max(
                0,
                beforeEnemyMoves - afterEnemyMoves
            );

        if (supported)
        {
            return
                endgameKingSupportReward *
                phase;
        }

        if (givesCheck || enemyMobilityReduction > 0)
        {
            return
                0.50f *
                endgameKingSupportReward *
                phase;
        }

        // No support, no pawn assistance, no real pressure: wandering is
        // explicitly negative during conversion.
        return
            -endgameKingWanderingPenalty *
            phase;
    }

    // =========================================================
    // ENDGAME PRIORITY HELPERS
    // =========================================================

    private StatePieceType GetStrongestAvailablePieceType(
        GameState state,
        EPlayer player)
    {
        if (CountStatePieces(state, player, StatePieceType.QUEEN) > 0)
        {
            return StatePieceType.QUEEN;
        }

        if (CountStatePieces(state, player, StatePieceType.ROOK) > 0)
        {
            return StatePieceType.ROOK;
        }

        if (CountStatePieces(state, player, StatePieceType.BISHOP) > 0)
        {
            return StatePieceType.BISHOP;
        }

        return StatePieceType.KNIGHT;
    }

    private int GetPromotionDistance(
        GameState state,
        CLocation location,
        EPlayer player)
    {
        if (state == null ||
            !HexChessRules.IsValidCell(location.X, location.Y))
        {
            return -1;
        }

        int targetRank =
            player == EPlayer.WHITE
                ? HexChessRules.GetMaxRank(location.X)
                : HexChessRules.GetMinRank(location.X);

        return Mathf.Max(
            0,
            player == EPlayer.WHITE
                ? targetRank - location.Y
                : location.Y - targetRank
        );
    }

    private bool IsPassedPawn(
        GameState state,
        CLocation pawnLocation,
        EPlayer player)
    {
        if (state == null)
        {
            return false;
        }

        EPlayer opponent =
            HexChessRules.GetOpponent(player);

        int direction =
            player == EPlayer.WHITE
                ? 1
                : -1;

        foreach (Vector2Int position in HexChessRules.CellCoordinates)
        {
            StatePiece? piece =
                state.GetPiece(
                    position.x,
                    position.y
                );

            if (!piece.HasValue ||
                !piece.Value.IsValid ||
                piece.Value.Player != opponent ||
                piece.Value.Type != StatePieceType.PAWN)
            {
                continue;
            }

            int forwardDelta =
                (position.y - pawnLocation.Y) *
                direction;

            if (forwardDelta > 0 &&
                Mathf.Abs(position.x - pawnLocation.X) <= 1)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsProtectedByOwnPiece(
        GameState state,
        CLocation location,
        EPlayer player)
    {
        return
            ShafranTacticalState.GetLegalDefenseCount(
                state,
                player,
                location.X,
                location.Y
            ) > 0;
    }

    private bool IsPieceImmediatelyHanging(
        GameState state,
        CLocation location,
        EPlayer opponent)
    {
        if (state == null || _moveGenerator == null)
            return false;

        StatePiece? piece = state.GetPiece(location.X, location.Y);
        if (!piece.HasValue || !piece.Value.IsValid ||
            piece.Value.Player != agentPlayer ||
            piece.Value.Type == StatePieceType.KING)
        {
            return false;
        }

        int incoming = ShafranTacticalState.GetLegalCaptureCount(
            state, opponent, location.X, location.Y);

        if (incoming <= 0)
            return false;

        int defenders = ShafranTacticalState.GetLegalDefenseCount(
            state, agentPlayer, location.X, location.Y);

        // One legal attacker versus zero legal defenders is a true hanging
        // piece. Multiple attackers still count as tactically unsafe when
        // they outnumber the available legal defenders.
        return incoming > defenders;
    }

    private bool DoesKingMoveSupportConversion(
        GameState before,
        GameState after,
        EPlayer player,
        bool pawnMode,
        bool majorMode)
    {
        Vector2Int kingBefore =
            FindKing(
                before,
                player
            );

        Vector2Int kingAfter =
            FindKing(
                after,
                player
            );

        if (kingBefore.x < 0 ||
            kingAfter.x < 0 ||
            kingBefore == kingAfter)
        {
            return false;
        }

        EPlayer opponent =
            HexChessRules.GetOpponent(player);

        // Support = becoming closer to a concrete conversion asset.
        if (pawnMode)
        {
            int beforeBest =
                GetNearestAdvancedPawnDistance(
                    before,
                    kingBefore,
                    player
                );

            int afterBest =
                GetNearestAdvancedPawnDistance(
                    after,
                    kingAfter,
                    player
                );

            if (beforeBest >= 0 &&
                afterBest >= 0 &&
                afterBest < beforeBest)
            {
                return true;
            }

            // If the forward pawn is blocked, the King may still be useful
            // when it approaches the exact blocked-pawn structure. This is
            // support for a real breakthrough, not generic King wandering.
            int beforeBlocked =
                GetNearestBlockedPawnDistance(
                    before,
                    kingBefore,
                    player
                );

            int afterBlocked =
                GetNearestBlockedPawnDistance(
                    after,
                    kingAfter,
                    player
                );

            if (beforeBlocked >= 0 &&
                afterBlocked >= 0 &&
                afterBlocked < beforeBlocked)
            {
                return true;
            }
        }

        if (majorMode ||
            (!pawnMode &&
             CountNonPawnNonKingPiecesForReward(after, player) > 0))
        {
            StatePieceType priorityType =
                GetStrongestAvailablePieceType(
                    after,
                    player
                );

            int beforeBest =
                GetNearestPieceDistance(
                    before,
                    kingBefore,
                    player,
                    priorityType
                );

            int afterBest =
                GetNearestPieceDistance(
                    after,
                    kingAfter,
                    player,
                    priorityType
                );

            if (beforeBest >= 0 &&
                afterBest >= 0 &&
                afterBest < beforeBest)
            {
                return true;
            }

            // A King step that reduces the opponent King's mobility or
            // delivers check is still concrete support for the conversion.
            int beforeEnemyMoves =
                CountKingLegalMoves(
                    before,
                    opponent
                );

            int afterEnemyMoves =
                CountKingLegalMoves(
                    after,
                    opponent
                );

            if (afterEnemyMoves < beforeEnemyMoves ||
                (_moveGenerator != null &&
                 _moveGenerator.IsInCheck(after, opponent)))
            {
                return true;
            }
        }

        return false;
    }

    private int GetNearestAdvancedPawnDistance(
        GameState state,
        Vector2Int from,
        EPlayer player)
    {
        int best = int.MaxValue;

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
                piece.Value.Type != StatePieceType.PAWN)
            {
                continue;
            }

            int distanceToPromotion =
                GetPromotionDistance(
                    state,
                    new CLocation(position.x, position.y),
                    player
                );

            if (distanceToPromotion > 0 &&
                distanceToPromotion < best)
            {
                best = distanceToPromotion;
            }
        }

        return best == int.MaxValue
            ? -1
            : best;
    }

    private int GetNearestBlockedPawnDistance(
        GameState state,
        Vector2Int from,
        EPlayer player)
    {
        int best = int.MaxValue;

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
                piece.Value.Type != StatePieceType.PAWN)
            {
                continue;
            }

            if (IsPassedPawn(
                    state,
                    new CLocation(
                        position.x,
                        position.y
                    ),
                    player))
            {
                continue;
            }

            best = Mathf.Min(
                best,
                HexDistance(
                    from.x,
                    from.y,
                    position.x,
                    position.y
                )
            );
        }

        return best == int.MaxValue
            ? -1
            : best;
    }

    private int GetNearestPieceDistance(
        GameState state,
        Vector2Int from,
        EPlayer player,
        StatePieceType type)
    {
        int best = int.MaxValue;

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

            best = Mathf.Min(
                best,
                HexDistance(
                    from.x,
                    from.y,
                    position.x,
                    position.y
                )
            );
        }

        return best == int.MaxValue
            ? -1
            : best;
    }

    // =========================================================
    // ANTI REPEAT
    // =========================================================

    private float CalculateAntiRepeatReward(
        GameState before,
        GameState after,
        StateMove selectedMove)
    {
        float reward = 0f;

        if (IsImmediateReverseMove(
                before,
                selectedMove))
        {
            reward -= immediateReversePenalty;
        }

        // BaseGameController is the authoritative history for the actual
        // Unity game and records the position during SwitchTurn() before
        // this post-move reward runs. Therefore subtract the current move
        // itself to recover the number of earlier occurrences.
        int currentCount =
            GetPositionVisitCount(
                after.Hash
            );

        int previousCount =
            Mathf.Max(
                0,
                currentCount - 1
            );

        if (previousCount >= 1)
        {
            float repetitionPenalty;

            if (previousCount == 1)
            {
                repetitionPenalty =
                    repetitionPenaltySecondOccurrence;
            }
            else if (previousCount == 2)
            {
                repetitionPenalty =
                    repetitionPenaltyThirdOccurrence;
            }
            else
            {
                repetitionPenalty =
                    repetitionPenaltyFourthOccurrence;
            }

            // A repetition is especially undesirable when this agent is
            // materially ahead: it is giving up a winning position instead
            // of converting the advantage. Scale the local anti-repeat
            // signal accordingly, while leaving a trailing side's defensive
            // repetition penalty at the normal level.
            EPlayer opponent =
                HexChessRules.GetOpponent(
                    agentPlayer
                );

            float ownMaterial =
                HexChessRules.CalculateNonKingMaterial(
                    after,
                    agentPlayer
                );

            float opponentMaterial =
                HexChessRules.CalculateNonKingMaterial(
                    after,
                    opponent
                );

            float materialLead =
                ownMaterial - opponentMaterial;

            float aheadFactor =
                Mathf.InverseLerp(
                    drawMaterialLeadThreshold,
                    drawMaterialLeadThreshold + 5f,
                    materialLead
                );

            float multiplier =
                Mathf.Lerp(
                    1f,
                    repetitionPenaltyAheadMultiplier,
                    aheadFactor
                );

            reward -=
                repetitionPenalty *
                multiplier;
        }

        return reward;
    }

    // =========================================================
    // NO PROGRESS
    // =========================================================

    private float CalculateNoProgressReward(
        GameState before,
        GameState after,
        StateMove selectedMove,
        float evaluationDelta)
    {
        bool capture =
            selectedMove.IsCapture;

        bool promotion =
            selectedMove.IsPromotion;

        bool pawnAdvance =
            selectedMove.MovingPiece.Type ==
                StatePieceType.PAWN &&
            selectedMove.From.Y !=
                selectedMove.To.Y;

        bool meaningfulProgress =
            capture ||
            promotion ||
            pawnAdvance ||
            evaluationDelta >=
                progressEvaluationThreshold;

        float endgamePhase =
            GetEndgameConversionPhase(
                after
            );

        EPlayer opponent =
            HexChessRules.GetOpponent(
                agentPlayer
            );

        bool givesCheck =
            _moveGenerator != null &&
            _moveGenerator.IsInCheck(
                after,
                opponent
            );

        if (endgamePhase > 0.20f &&
            givesCheck)
        {
            meaningfulProgress = true;
        }

        if (meaningfulProgress)
        {
            return 0f;
        }

        // V10 uses the authoritative game-level clock. It counts BOTH
        // sides, so it is consistent with the 50-move rule:
        // 100 plies = 50 moves by both sides.
        int noProgressPlies =
            BaseGameController.Current != null
                ? BaseGameController.Current.NoProgressPlyCount
                : 0;

        // Existing inspector values are expressed as "moves" by one side.
        // Convert them to plies for the shared game clock.
        int effectiveGracePlies =
            Mathf.RoundToInt(
                Mathf.Lerp(
                    noProgressGraceMoves * 2f,
                    endgameNoProgressGraceMoves * 2f,
                    endgamePhase
                )
            );

        if (noProgressPlies <=
            effectiveGracePlies)
        {
            return 0f;
        }

        float effectivePenaltyPerPly =
            Mathf.Lerp(
                noProgressPenaltyPerMove * 0.5f,
                endgameNoProgressPenaltyPerMove * 0.5f,
                endgamePhase
            );

        float effectivePenaltyCap =
            Mathf.Lerp(
                noProgressPenaltyCap,
                endgameNoProgressPenaltyCap,
                endgamePhase
            );

        int excess =
            noProgressPlies -
            effectiveGracePlies;

        float penalty =
            Mathf.Min(
                effectivePenaltyCap,
                excess *
                effectivePenaltyPerPly
            );

        return -penalty;
    }

    // =========================================================
    // MINIMAX GUIDANCE
    // =========================================================

    private float CalculateMinimaxGuidanceReward(
        GameState before,
        StateMove selectedMove)
    {
        if (!enableMinimaxGuidance ||
            before == null)
        {
            return 0f;
        }

        if (!HasGuidanceMove(before))
        {
            return 0f;
        }

        StateMove teacherMove =
            GetGuidanceMove();

        float selectedScore =
            EvaluateCandidateMove(
                before,
                selectedMove
            );

        // Teacher score is captured during the Minimax search.
        float teacherScore =
            GetGuidanceScore();

        bool selectedExact =
            SameMove(
                teacherMove,
                selectedMove
            );

        float selectedGap =
            Mathf.Max(
                0f,
                teacherScore - selectedScore
            );

        bool selectedNear =
            selectedExact ||
            selectedGap <=
                minimaxGuidanceNearBestThreshold;

        if (selectedExact)
        {
            return minimaxGuidanceExactReward;
        }

        if (selectedNear)
        {
            return minimaxGuidanceNearBestReward;
        }

        float normalized =
            Mathf.InverseLerp(
                minimaxGuidanceNearBestThreshold,
                minimaxGuidanceBadThreshold,
                selectedGap
            );

        return
            -Mathf.Lerp(
                0f,
                minimaxGuidancePenalty,
                normalized
            );
    }

    // =========================================================
    // FIND KING
    // =========================================================

    private Vector2Int FindKing(
        GameState state,
        EPlayer player)
    {
        if (state == null)
        {
            return new Vector2Int(-1, -1);
        }

        foreach (
            Vector2Int position
            in HexChessRules.CellCoordinates)
        {
            StatePiece? piece =
                state.GetPiece(
                    position.x,
                    position.y
                );

            if (
                piece.HasValue &&
                piece.Value.IsValid &&
                piece.Value.Player == player &&
                piece.Value.Type ==
                    StatePieceType.KING
            )
            {
                return position;
            }
        }

        return new Vector2Int(-1, -1);
    }
}