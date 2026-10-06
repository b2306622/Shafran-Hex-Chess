using System.Collections.Generic;
using UnityEngine;

public class Evaluation
{
    // =========================================================
    // AI SIDE
    // =========================================================

    private readonly EPlayer _aiPlayer;
    private readonly MoveGenerator _legalMoveGenerator;

    // =========================================================
    // BASE WEIGHTS
    // =========================================================

    private const float MATERIAL_WEIGHT = 1.0f;

    private const float CENTER_WEIGHT = 1.5f;

    private const float PAWN_ADVANCEMENT_WEIGHT = 2.2f;

    private const float MOBILITY_WEIGHT = 2.2f;

    private const float ACTIVITY_WEIGHT = 1.1f;

    private const float KING_SAFETY_WEIGHT = 4.5f;

    private const float PAWN_STRUCTURE_WEIGHT = 1.6f;

    private const float PROMOTION_WEIGHT = 3.2f;

    // =========================================================
    // 7.5 EVALUATION FEATURES
    // =========================================================

    // Tốt thông qua
    private const float PASSED_PAWN_WEIGHT = 4.0f;

    // Xe / Hậu trên file mở
    private const float OPEN_FILE_WEIGHT = 1.5f;

    // Tốt bảo vệ Vua
    private const float KING_SHELTER_WEIGHT = 1.5f;

    // Có lượt đi
    private const float TEMPO_WEIGHT = 0.15f;

    // =========================================================
    // ENDGAME CONVERSION
    // =========================================================

    private const float ENDGAME_CENTER_WEIGHT = 0.55f;
    private const float ENDGAME_MOBILITY_WEIGHT = 3.4f;
    private const float ENDGAME_PASSED_PAWN_WEIGHT = 5.5f;
    private const float ENDGAME_PROMOTION_WEIGHT = 4.5f;
    private const float ENDGAME_KING_SAFETY_WEIGHT = 2.5f;
    private const float ENDGAME_KING_ACTIVITY_WEIGHT = 2.2f;
    private const float ENDGAME_KING_CONFINEMENT_WEIGHT = 3.0f;

    // =========================================================
    // PHASE-AWARE TACTICAL FEATURES
    // =========================================================

    // Loose / hanging material. This is deliberately stronger in the
    // middlegame so Depth 1/2 avoids blunders and values safe captures.
    private const float OPENING_LOOSE_PIECE_WEIGHT = 0.6f;
    private const float MIDDLEGAME_LOOSE_PIECE_WEIGHT = 3.0f;
    private const float ENDGAME_LOOSE_PIECE_WEIGHT = 2.2f;

    // Direct pressure around the opposing King.
    private const float OPENING_KING_ATTACK_WEIGHT = 0.35f;
    private const float MIDDLEGAME_KING_ATTACK_WEIGHT = 3.6f;
    private const float ENDGAME_KING_ATTACK_WEIGHT = 2.0f;

    // Tactical capture availability / initiative.
    private const float OPENING_TACTICAL_WEIGHT = 0.45f;
    private const float MIDDLEGAME_TACTICAL_WEIGHT = 2.5f;
    private const float ENDGAME_TACTICAL_WEIGHT = 2.7f;

    private const float OPENING_CENTER_PHASE_WEIGHT = 2.0f;
    private const float MIDDLEGAME_CENTER_PHASE_WEIGHT = 1.1f;
    private const float ENDGAME_CENTER_PHASE_WEIGHT = 0.55f;

    private const float OPENING_MOBILITY_PHASE_WEIGHT = 1.9f;
    private const float MIDDLEGAME_MOBILITY_PHASE_WEIGHT = 2.9f;
    private const float ENDGAME_MOBILITY_PHASE_WEIGHT = 3.4f;

    private const float OPENING_ACTIVITY_PHASE_WEIGHT = 1.2f;
    private const float MIDDLEGAME_ACTIVITY_PHASE_WEIGHT = 1.7f;
    private const float ENDGAME_ACTIVITY_PHASE_WEIGHT = 1.2f;

    private const float OPENING_KING_SAFETY_PHASE_WEIGHT = 5.0f;
    private const float MIDDLEGAME_KING_SAFETY_PHASE_WEIGHT = 5.2f;
    private const float ENDGAME_KING_SAFETY_PHASE_WEIGHT = 2.2f;

    private const float OPENING_OPEN_FILE_WEIGHT = 1.0f;
    private const float MIDDLEGAME_OPEN_FILE_WEIGHT = 2.4f;
    private const float ENDGAME_OPEN_FILE_WEIGHT = 2.0f;

    // When ahead, simplifying and reducing the opponent's freedom is more
    // valuable than holding static positional advantages.
    private const float MIDDLEGAME_CONVERSION_WEIGHT = 1.35f;
    private const float ENDGAME_CONVERSION_WEIGHT = 2.40f;


    // =========================================================
    // CONSTRUCTOR
    // =========================================================

    public Evaluation(
        EPlayer aiPlayer)
    {
        _aiPlayer =
            aiPlayer;
        _legalMoveGenerator =
            new MoveGenerator();
    }


    // =========================================================
    // MAIN EVALUATION
    // =========================================================

    public float Evaluate(
        GameState state)
    {
        if (state == null)
        {
            return 0f;
        }

        // Material elimination is NOT a training terminal condition.
        // A side with only its King must still lose by checkmate. Keep the
        // normal heuristic evaluation active so PPO is taught to convert
        // the remaining material advantage into an actual mate.

        float score = 0f;

        EPlayer opponent =
            HexChessRules.GetOpponent(
                _aiPlayer
            );

        float openingPhase;
        float middlegamePhase;
        float endgamePhase;

        HexChessRules.GetStrategicPhase(
            state,
            _aiPlayer,
            out openingPhase,
            out middlegamePhase,
            out endgamePhase
        );

        float centerWeight =
            GetPhaseWeight(
                openingPhase,
                middlegamePhase,
                endgamePhase,
                OPENING_CENTER_PHASE_WEIGHT,
                MIDDLEGAME_CENTER_PHASE_WEIGHT,
                ENDGAME_CENTER_PHASE_WEIGHT
            );

        float mobilityWeight =
            GetPhaseWeight(
                openingPhase,
                middlegamePhase,
                endgamePhase,
                OPENING_MOBILITY_PHASE_WEIGHT,
                MIDDLEGAME_MOBILITY_PHASE_WEIGHT,
                ENDGAME_MOBILITY_PHASE_WEIGHT
            );

        float activityWeight =
            GetPhaseWeight(
                openingPhase,
                middlegamePhase,
                endgamePhase,
                OPENING_ACTIVITY_PHASE_WEIGHT,
                MIDDLEGAME_ACTIVITY_PHASE_WEIGHT,
                ENDGAME_ACTIVITY_PHASE_WEIGHT
            );

        float kingSafetyWeight =
            GetPhaseWeight(
                openingPhase,
                middlegamePhase,
                endgamePhase,
                OPENING_KING_SAFETY_PHASE_WEIGHT,
                MIDDLEGAME_KING_SAFETY_PHASE_WEIGHT,
                ENDGAME_KING_SAFETY_PHASE_WEIGHT
            );

        float openFileWeight =
            GetPhaseWeight(
                openingPhase,
                middlegamePhase,
                endgamePhase,
                OPENING_OPEN_FILE_WEIGHT,
                MIDDLEGAME_OPEN_FILE_WEIGHT,
                ENDGAME_OPEN_FILE_WEIGHT
            );

        float passedPawnWeight =
            GetPhaseWeight(
                openingPhase,
                middlegamePhase,
                endgamePhase,
                PASSED_PAWN_WEIGHT,
                (PASSED_PAWN_WEIGHT + 0.7f),
                ENDGAME_PASSED_PAWN_WEIGHT
            );

        float promotionWeight =
            GetPhaseWeight(
                openingPhase,
                middlegamePhase,
                endgamePhase,
                PROMOTION_WEIGHT,
                (PROMOTION_WEIGHT + 0.25f),
                ENDGAME_PROMOTION_WEIGHT
            );


        // =====================================================
        // 1. MATERIAL + POSITION
        // =====================================================

        foreach (Vector2Int position in
                 HexChessRules.CellCoordinates)
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

            StatePiece currentPiece =
                piece.Value;

            float pieceScore = 0f;


            // -------------------------------------------------
            // MATERIAL
            // -------------------------------------------------

            pieceScore +=
                currentPiece.Value *
                MATERIAL_WEIGHT;


            // -------------------------------------------------
            // CENTER
            // -------------------------------------------------

            pieceScore +=
                GetCenterBonus(
                    position.x,
                    position.y,
                    currentPiece.Type
                ) *
                centerWeight;


            // -------------------------------------------------
            // PAWN ADVANCEMENT
            // -------------------------------------------------

            pieceScore +=
                GetPawnBonus(
                    position,
                    currentPiece
                ) *
                Mathf.Lerp(
                    PAWN_ADVANCEMENT_WEIGHT,
                    PAWN_ADVANCEMENT_WEIGHT + 0.8f,
                    endgamePhase
                );


            // -------------------------------------------------
            // PROMOTION POTENTIAL
            // -------------------------------------------------

            pieceScore +=
                GetPromotionPotentialBonus(
                    position,
                    currentPiece
                ) *
                promotionWeight;


            // -------------------------------------------------
            // PAWN STRUCTURE
            // -------------------------------------------------

            pieceScore +=
                GetPawnStructureBonus(
                    state,
                    position,
                    currentPiece
                ) *
                PAWN_STRUCTURE_WEIGHT;


            // -------------------------------------------------
            // PASSED PAWN
            // -------------------------------------------------

            pieceScore +=
                GetPassedPawnBonus(
                    state,
                    position,
                    currentPiece
                ) *
                passedPawnWeight;


            // -------------------------------------------------
            // SIDE
            // -------------------------------------------------

            if (currentPiece.Player ==
                _aiPlayer)
            {
                score += pieceScore;
            }
            else
            {
                score -= pieceScore;
            }
        }


        // =====================================================
        // 2. MOBILITY
        // =====================================================

        float aiMobility =
            GetMobilityScore(
                state,
                _aiPlayer
            );

        float opponentMobility =
            GetMobilityScore(
                state,
                opponent
            );

        score +=
            (
                aiMobility -
                opponentMobility
            ) *
            mobilityWeight;


        // =====================================================
        // 3. PIECE ACTIVITY
        // =====================================================

        float aiActivity =
            GetPieceActivity(
                state,
                _aiPlayer
            );

        float opponentActivity =
            GetPieceActivity(
                state,
                opponent
            );

        score +=
            (
                aiActivity -
                opponentActivity
            ) *
            activityWeight;


        // =====================================================
        // 4. KING SAFETY
        // =====================================================

        float aiKingSafety =
            GetKingSafety(
                state,
                _aiPlayer
            );

        float opponentKingSafety =
            GetKingSafety(
                state,
                opponent
            );

        score +=
            (
                aiKingSafety -
                opponentKingSafety
            ) *
            kingSafetyWeight;

        // =====================================================
        // 5. TACTICAL STABILITY / LOOSE PIECES
        // =====================================================

        float looseWeight =
            GetPhaseWeight(
                openingPhase,
                middlegamePhase,
                endgamePhase,
                OPENING_LOOSE_PIECE_WEIGHT,
                MIDDLEGAME_LOOSE_PIECE_WEIGHT,
                ENDGAME_LOOSE_PIECE_WEIGHT
            );

        float aiLooseMaterial =
            GetLoosePieceScore(
                state,
                _aiPlayer
            );

        float opponentLooseMaterial =
            GetLoosePieceScore(
                state,
                opponent
            );

        // More opponent loose material is good; own loose material is bad.
        score +=
            (opponentLooseMaterial - aiLooseMaterial) *
            looseWeight;

        // =====================================================
        // 6. KING ATTACK / INITIATIVE
        // =====================================================

        float kingAttackWeight =
            GetPhaseWeight(
                openingPhase,
                middlegamePhase,
                endgamePhase,
                OPENING_KING_ATTACK_WEIGHT,
                MIDDLEGAME_KING_ATTACK_WEIGHT,
                ENDGAME_KING_ATTACK_WEIGHT
            );

        float aiKingAttack =
            GetKingAttackPressure(
                state,
                _aiPlayer,
                opponent
            );

        float opponentKingAttack =
            GetKingAttackPressure(
                state,
                opponent,
                _aiPlayer
            );

        score +=
            (aiKingAttack - opponentKingAttack) *
            kingAttackWeight;

        // =====================================================
        // 7. TACTICAL CAPTURE / INITIATIVE POTENTIAL
        // =====================================================

        float tacticalWeight =
            GetPhaseWeight(
                openingPhase,
                middlegamePhase,
                endgamePhase,
                OPENING_TACTICAL_WEIGHT,
                MIDDLEGAME_TACTICAL_WEIGHT,
                ENDGAME_TACTICAL_WEIGHT
            );

        float aiTactical =
            GetTacticalCapturePotential(
                state,
                _aiPlayer
            );

        float opponentTactical =
            GetTacticalCapturePotential(
                state,
                opponent
            );

        score +=
            (aiTactical - opponentTactical) *
            tacticalWeight;

        // =====================================================
        // 7.5 CONVERSION / SIMPLIFICATION WHEN AHEAD
        // =====================================================

        float materialLeadForConversion =
            CalculateMaterialForPlayer(
                state,
                _aiPlayer
            ) -
            CalculateMaterialForPlayer(
                state,
                opponent
            );

        float leadGate =
            Mathf.InverseLerp(
                2.5f,
                6.5f,
                materialLeadForConversion
            );

        int totalNonKingPieces =
            CountNonKingPieces(
                state
            );

        float simplificationPhase =
            Mathf.InverseLerp(
                24f,
                10f,
                totalNonKingPieces
            );

        float conversionWeight =
            GetPhaseWeight(
                openingPhase,
                middlegamePhase,
                endgamePhase,
                0f,
                MIDDLEGAME_CONVERSION_WEIGHT,
                ENDGAME_CONVERSION_WEIGHT
            );

        if (leadGate > 0f)
        {
            float opponentFreedom =
                GetMobilityScore(
                    state,
                    opponent
                );

            float confinement =
                opponentKingAttack * 0.60f +
                (
                    aiTactical -
                    opponentTactical
                ) * 0.40f;

            score +=
                leadGate *
                (simplificationPhase + 0.40f) *
                conversionWeight *
                (confinement + 0.25f *
                 Mathf.Max(0f,
                     mobilityWeight -
                     opponentFreedom * 0.01f));
        }

        // -----------------------------------------------------
        // ENDGAME KING ACTIVITY / CONFINEMENT
        // -----------------------------------------------------

        if (endgamePhase > 0f)
        {
            Vector2Int aiKing =
                FindKing(
                    state,
                    _aiPlayer
                );

            Vector2Int opponentKing =
                FindKing(
                    state,
                    opponent
                );

            if (aiKing.x >= 0 &&
                opponentKing.x >= 0)
            {
                StatePiece? aiKingPiece =
                    state.GetPiece(
                        aiKing.x,
                        aiKing.y
                    );

                StatePiece? opponentKingPiece =
                    state.GetPiece(
                        opponentKing.x,
                        opponentKing.y
                    );

                if (aiKingPiece.HasValue &&
                    opponentKingPiece.HasValue)
                {
                    float aiKingMobility =
                        GetKingMobility(
                            state,
                            aiKing,
                            aiKingPiece.Value
                        );

                    float opponentKingMobility =
                        GetKingMobility(
                            state,
                            opponentKing,
                            opponentKingPiece.Value
                        );

                    score +=
                        (aiKingMobility -
                         opponentKingMobility) *
                        ENDGAME_KING_ACTIVITY_WEIGHT *
                        endgamePhase;
                }

                float confinement =
                    GetKingConfinementScore(
                        state,
                        opponentKing,
                        _aiPlayer
                    );

                score +=
                    confinement *
                    ENDGAME_KING_CONFINEMENT_WEIGHT *
                    endgamePhase;
            }
        }


        // =====================================================
        // 5. OPEN FILE
        // =====================================================

        float aiOpenFiles =
            GetOpenFileScore(
                state,
                _aiPlayer
            );

        float opponentOpenFiles =
            GetOpenFileScore(
                state,
                opponent
            );

        score +=
            (
                aiOpenFiles -
                opponentOpenFiles
            ) *
            openFileWeight;


        // =====================================================
        // 6. KING SHELTER
        // =====================================================

        float aiKingShelter =
            GetKingShelterScore(
                state,
                _aiPlayer
            );

        float opponentKingShelter =
            GetKingShelterScore(
                state,
                opponent
            );

        score +=
            (
                aiKingShelter -
                opponentKingShelter
            ) *
            KING_SHELTER_WEIGHT;


        // =====================================================
        // 7. TEMPO
        // =====================================================

        if (state.SideToMove ==
            _aiPlayer)
        {
            score +=
                TEMPO_WEIGHT;
        }
        else
        {
            score -=
                TEMPO_WEIGHT;
        }


        return score;
    }


    private float GetPhaseWeight(
        float opening,
        float middlegame,
        float endgame,
        float openingWeight,
        float middlegameWeight,
        float endgameWeight)
    {
        return
            opening * openingWeight +
            middlegame * middlegameWeight +
            endgame * endgameWeight;
    }

    // Material that is currently loose enough to be tactically vulnerable.
    // Protected attacked pieces are penalized less than completely hanging
    // pieces. Kings are excluded because terminal/check logic handles them.
    private float GetLoosePieceScore(
        GameState state,
        EPlayer player)
    {
        if (state == null) return 0f;

        EPlayer attacker = HexChessRules.GetOpponent(player);
        float score = 0f;

        foreach (Vector2Int position in HexChessRules.CellCoordinates)
        {
            StatePiece? piece = state.GetPiece(position.x, position.y);
            if (!piece.HasValue || !piece.Value.IsValid ||
                piece.Value.Player != player ||
                piece.Value.Type == StatePieceType.KING)
                continue;

            int attackers = ShafranTacticalState.GetLegalCaptureCount(
                state, attacker, position.x, position.y);
            if (attackers <= 0) continue;

            int defenders = ShafranTacticalState.GetLegalDefenseCount(
                state, player, position.x, position.y);

            float pieceValue = Mathf.Max(0.25f, piece.Value.Value / 1000f);
            float unsafeFraction = Mathf.Clamp01(
                (attackers - defenders + 1f) / 3f);

            score += pieceValue * unsafeFraction;
        }

        return score;
    }

    private float GetKingAttackPressure(
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

        float pressure = 0f;

        if (IsSquareAttacked(
                state,
                king.x,
                king.y,
                attacker))
        {
            pressure += 2.5f;
        }

        foreach (Vector2Int direction in HexChessRules.QueenDirections)
        {
            int x = king.x + direction.x;
            int y = king.y + direction.y;

            if (!HexChessRules.IsValidCell(x, y))
            {
                continue;
            }

            if (IsSquareAttacked(state, x, y, attacker))
            {
                pressure += 0.60f;
            }

            StatePiece? piece =
                state.GetPiece(x, y);

            if (piece.HasValue &&
                piece.Value.Player == victim &&
                piece.Value.Type != StatePieceType.KING &&
                IsSquareAttacked(state, x, y, attacker))
            {
                pressure += 0.40f;
            }
        }

        return pressure;
    }

    // Counts meaningful capture pressure without creating child GameStates.
    // It only scans each piece's reachable rays/offsets instead of doing
    // an all-pairs 70x70 search at every Minimax leaf.
    private float GetTacticalCapturePotential(
        GameState state,
        EPlayer player)
    {
        if (state == null || _legalMoveGenerator == null)
            return 0f;

        // Use legal captures rather than geometric reachability. This keeps
        // the Minimax evaluator consistent with the tactical reward: pinned
        // pieces and illegal king-exposing captures are not counted.
        List<StateMove> captures =
            _legalMoveGenerator.GenerateLegalCaptureMoves(state, player);

        float best = 0f;
        for (int i = 0; i < captures.Count; i++)
        {
            StateMove move = captures[i];
            if (!move.CapturedPiece.IsValid ||
                move.CapturedPiece.Type == StatePieceType.KING)
                continue;

            float value =
                move.CapturedPiece.Value / 1000f;

            // Prefer captures that do not immediately leave the capturing
            // piece hanging after the exchange.
            GameState next = state.Clone();
            _legalMoveGenerator.MakeMove(next, move);
            int attackers = ShafranTacticalState.GetLegalCaptureCount(
                next,
                HexChessRules.GetOpponent(player),
                move.To.X,
                move.To.Y);
            int defenders = ShafranTacticalState.GetLegalDefenseCount(
                next,
                player,
                move.To.X,
                move.To.Y);

            if (attackers > defenders)
                value *= 0.65f;
            else if (attackers == 0)
                value *= 1.05f;

            best = Mathf.Max(best, value);
        }

        return best;
    }

    private float GetTargetValue(
        GameState state,
        int x,
        int y,
        EPlayer enemy)
    {
        if (!HexChessRules.IsValidCell(x, y))
        {
            return 0f;
        }

        StatePiece? target =
            state.GetPiece(x, y);

        if (!target.HasValue ||
            !target.Value.IsValid ||
            target.Value.Player != enemy ||
            target.Value.Type == StatePieceType.KING)
        {
            return 0f;
        }

        return target.Value.Value / 1000f;
    }

    private float GetSlidingCapturePotential(
        GameState state,
        Vector2Int from,
        Vector2Int[] directions,
        EPlayer enemy)
    {
        float best = 0f;

        foreach (Vector2Int direction in directions)
        {
            int x = from.x + direction.x;
            int y = from.y + direction.y;

            while (HexChessRules.IsValidCell(x, y))
            {
                StatePiece? target =
                    state.GetPiece(x, y);

                if (target.HasValue)
                {
                    if (target.Value.IsValid &&
                        target.Value.Player == enemy &&
                        target.Value.Type != StatePieceType.KING)
                    {
                        best =
                            Mathf.Max(
                                best,
                                target.Value.Value / 1000f);
                    }

                    break;
                }

                x += direction.x;
                y += direction.y;
            }
        }

        return best;
    }


    // =========================================================
    // CENTER BONUS
    // =========================================================

    private float GetCenterBonus(
        int x,
        int y,
        StatePieceType type)
    {
        float distance =
            HexChessRules.HexDistance(
                4,
                5,
                x,
                y
            );

        float bonus =
            Mathf.Max(
                0f,
                7f - distance
            );

        switch (type)
        {
            case StatePieceType.KNIGHT:

                return bonus * 1.5f;

            case StatePieceType.BISHOP:

                return bonus * 1.3f;

            case StatePieceType.PAWN:

                return bonus * 1.1f;

            case StatePieceType.QUEEN:

                return bonus * 0.5f;

            case StatePieceType.KING:

                return bonus * 0.15f;

            case StatePieceType.ROOK:

                return bonus * 0.3f;

            default:

                return bonus * 0.2f;
        }
    }


    // =========================================================
    // PAWN ADVANCEMENT
    // =========================================================

    private float GetPawnBonus(
        Vector2Int position,
        StatePiece pawn)
    {
        if (pawn.Type !=
            StatePieceType.PAWN)
        {
            return 0f;
        }

        int startRank =
            pawn.Player ==
            EPlayer.WHITE
                ? HexChessRules.GetWhitePawnStartRank(
                    position.x
                )
                : HexChessRules.GetBlackPawnStartRank(
                    position.x
                );

        if (startRank < 0)
        {
            return 0f;
        }

        if (pawn.Player ==
            EPlayer.WHITE)
        {
            return position.y - startRank;
        }

        return startRank - position.y;
    }


    // =========================================================
    // PROMOTION POTENTIAL
    // =========================================================

    private float GetPromotionPotentialBonus(
        Vector2Int position,
        StatePiece piece)
    {
        if (piece.Type !=
            StatePieceType.PAWN)
        {
            return 0f;
        }

        int startRank =
            piece.Player ==
            EPlayer.WHITE
                ? HexChessRules.GetWhitePawnStartRank(
                    position.x
                )
                : HexChessRules.GetBlackPawnStartRank(
                    position.x
                );

        int promotionRank =
            piece.Player ==
            EPlayer.WHITE
                ? HexChessRules.GetMaxRank(
                    position.x
                )
                : HexChessRules.GetMinRank(
                    position.x
                );

        if (startRank < 0)
        {
            return 0f;
        }

        int totalDistance =
            Mathf.Abs(
                promotionRank -
                startRank
            );

        if (totalDistance <= 0)
        {
            return 0f;
        }

        int currentDistance =
            Mathf.Abs(
                promotionRank -
                position.y
            );

        float progress =
            1f -
            Mathf.Clamp01(
                (float)currentDistance /
                totalDistance
            );

        return progress * 20f;
    }


    // =========================================================
    // PASSED PAWN
    // =========================================================

    private float GetPassedPawnBonus(
        GameState state,
        Vector2Int position,
        StatePiece pawn)
    {
        if (pawn.Type !=
            StatePieceType.PAWN)
        {
            return 0f;
        }

        int startX =
            position.x - 1;

        int endX =
            position.x + 1;


        // -----------------------------------------------------
        // Kiểm tra tốt đối phương ở 3 file lân cận
        // -----------------------------------------------------

        for (int x = startX;
             x <= endX;
             x++)
        {
            if (x < 0 ||
                x >= HexChessRules.FILE_COUNT)
            {
                continue;
            }

            foreach (Vector2Int cell in
                     HexChessRules.CellCoordinates)
            {
                if (cell.x != x)
                {
                    continue;
                }

                StatePiece? enemy =
                    state.GetPiece(
                        cell.x,
                        cell.y
                    );

                if (!enemy.HasValue)
                {
                    continue;
                }

                if (enemy.Value.Player ==
                    pawn.Player)
                {
                    continue;
                }

                if (enemy.Value.Type !=
                    StatePieceType.PAWN)
                {
                    continue;
                }


                // -------------------------------------------------
                // WHITE: enemy pawn phía trước
                // -------------------------------------------------

                if (pawn.Player ==
                    EPlayer.WHITE)
                {
                    if (cell.y > position.y)
                    {
                        return 0f;
                    }
                }


                // -------------------------------------------------
                // BLACK: enemy pawn phía trước
                // -------------------------------------------------

                else
                {
                    if (cell.y < position.y)
                    {
                        return 0f;
                    }
                }
            }
        }


        // -----------------------------------------------------
        // Càng gần phong cấp càng có giá trị
        // -----------------------------------------------------

        int promotionRank =
            pawn.Player ==
            EPlayer.WHITE
                ? HexChessRules.GetMaxRank(
                    position.x
                )
                : HexChessRules.GetMinRank(
                    position.x
                );

        int distance =
            Mathf.Abs(
                promotionRank -
                position.y
            );

        return Mathf.Max(
            1f,
            8f - distance
        );
    }


    // =========================================================
    // MOBILITY
    // =========================================================

    private float GetMobilityScore(
        GameState state,
        EPlayer player)
    {
        float mobility = 0f;

        foreach (Vector2Int position in
                 HexChessRules.CellCoordinates)
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

            if (piece.Value.Player !=
                player)
            {
                continue;
            }

            mobility +=
                GetPieceMobility(
                    state,
                    position,
                    piece.Value
                );
        }

        return mobility;
    }


    // =========================================================
    // PIECE MOBILITY
    // =========================================================

    private float GetPieceMobility(
        GameState state,
        Vector2Int from,
        StatePiece piece)
    {
        switch (piece.Type)
        {
            case StatePieceType.PAWN:

                return GetPawnMobility(
                    state,
                    from,
                    piece
                );

            case StatePieceType.KNIGHT:

                return GetKnightMobility(
                    state,
                    from,
                    piece
                );

            case StatePieceType.BISHOP:

                return GetSlidingMobility(
                    state,
                    from,
                    piece,
                    HexChessRules.BishopDirections
                );

            case StatePieceType.ROOK:

                return GetSlidingMobility(
                    state,
                    from,
                    piece,
                    HexChessRules.RookDirections
                );

            case StatePieceType.QUEEN:

                return GetSlidingMobility(
                    state,
                    from,
                    piece,
                    HexChessRules.QueenDirections
                );

            case StatePieceType.KING:

                return GetKingMobility(
                    state,
                    from,
                    piece
                );

            default:

                return 0f;
        }
    }


    // =========================================================
    // PAWN MOBILITY
    // =========================================================

    private float GetPawnMobility(
        GameState state,
        Vector2Int from,
        StatePiece pawn)
    {
        float mobility = 0f;

        int direction =
            pawn.Player ==
            EPlayer.WHITE
                ? 1
                : -1;


        // -----------------------------------------------------
        // ĐI THẲNG
        // -----------------------------------------------------

        int forwardX =
            from.x;

        int forwardY =
            from.y +
            direction;

        if (HexChessRules.IsValidCell(
                forwardX,
                forwardY))
        {
            if (!state.GetPiece(
                    forwardX,
                    forwardY
                ).HasValue)
            {
                mobility += 1f;
            }
        }


        // -----------------------------------------------------
        // ĂN CHÉO
        // -----------------------------------------------------

        int captureX1 =
            from.x - 1;

        int captureY1 =
            from.y +
            direction;

        if (HexChessRules.IsValidCell(
                captureX1,
                captureY1))
        {
            StatePiece? target =
                state.GetPiece(
                    captureX1,
                    captureY1
                );

            if (target.HasValue &&
                target.Value.Player !=
                pawn.Player)
            {
                mobility += 1.5f;
            }
        }


        int captureX2 =
            from.x + 1;

        int captureY2 =
            from.y +
            direction;

        if (HexChessRules.IsValidCell(
                captureX2,
                captureY2))
        {
            StatePiece? target =
                state.GetPiece(
                    captureX2,
                    captureY2
                );

            if (target.HasValue &&
                target.Value.Player !=
                pawn.Player)
            {
                mobility += 1.5f;
            }
        }

        return mobility;
    }


    // =========================================================
    // KNIGHT MOBILITY
    // =========================================================

    private float GetKnightMobility(
        GameState state,
        Vector2Int from,
        StatePiece knight)
    {
        float mobility = 0f;

        foreach (Vector2Int offset in
                 HexChessRules.KnightMoves)
        {
            int x =
                from.x +
                offset.x;

            int y =
                from.y +
                offset.y;

            if (!HexChessRules.IsValidCell(
                    x,
                    y))
            {
                continue;
            }

            StatePiece? target =
                state.GetPiece(
                    x,
                    y
                );

            if (!target.HasValue)
            {
                mobility += 1f;
            }
            else if (target.Value.Player !=
                     knight.Player)
            {
                mobility += 1.5f;
            }
        }

        return mobility;
    }


    // =========================================================
    // SLIDING MOBILITY
    // =========================================================

    private float GetSlidingMobility(
        GameState state,
        Vector2Int from,
        StatePiece piece,
        Vector2Int[] directions)
    {
        float mobility = 0f;

        foreach (Vector2Int direction in
                 directions)
        {
            int x =
                from.x +
                direction.x;

            int y =
                from.y +
                direction.y;

            while (
                HexChessRules.IsValidCell(
                    x,
                    y))
            {
                StatePiece? target =
                    state.GetPiece(
                        x,
                        y
                    );

                if (!target.HasValue)
                {
                    mobility += 1f;
                }
                else
                {
                    if (target.Value.Player !=
                        piece.Player)
                    {
                        mobility += 1.5f;
                    }

                    break;
                }

                x += direction.x;
                y += direction.y;
            }
        }

        return mobility;
    }


    // =========================================================
    // KING MOBILITY
    // =========================================================

    private float GetKingMobility(
        GameState state,
        Vector2Int from,
        StatePiece king)
    {
        float mobility = 0f;

        foreach (Vector2Int direction in
                 HexChessRules.QueenDirections)
        {
            int x =
                from.x +
                direction.x;

            int y =
                from.y +
                direction.y;

            if (!HexChessRules.IsValidCell(
                    x,
                    y))
            {
                continue;
            }

            StatePiece? target =
                state.GetPiece(
                    x,
                    y
                );

            if (!target.HasValue)
            {
                mobility += 1f;
            }
            else if (target.Value.Player !=
                     king.Player)
            {
                mobility += 1.5f;
            }
        }

        return mobility;
    }


    // =========================================================
    // PIECE ACTIVITY
    // =========================================================

    private float GetPieceActivity(
        GameState state,
        EPlayer player)
    {
        float activity = 0f;

        foreach (Vector2Int position in
                 HexChessRules.CellCoordinates)
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

            if (piece.Value.Player !=
                player)
            {
                continue;
            }

            switch (piece.Value.Type)
            {
                case StatePieceType.KNIGHT:

                    activity +=
                        GetCenterBonus(
                            position.x,
                            position.y,
                            StatePieceType.KNIGHT
                        );

                    break;

                case StatePieceType.BISHOP:

                    activity +=
                        GetCenterBonus(
                            position.x,
                            position.y,
                            StatePieceType.BISHOP
                        );

                    break;

                case StatePieceType.ROOK:

                    activity +=
                        GetSlidingReach(
                            state,
                            position,
                            piece.Value,
                            HexChessRules.RookDirections
                        ) *
                        0.5f;

                    break;

                case StatePieceType.QUEEN:

                    activity +=
                        GetSlidingReach(
                            state,
                            position,
                            piece.Value,
                            HexChessRules.QueenDirections
                        ) *
                        0.4f;

                    break;

                case StatePieceType.KING:

                    activity +=
                        GetCenterBonus(
                            position.x,
                            position.y,
                            StatePieceType.KING
                        );

                    break;
            }
        }

        return activity;
    }


    // =========================================================
    // SLIDING REACH
    // =========================================================

    private float GetSlidingReach(
        GameState state,
        Vector2Int from,
        StatePiece piece,
        Vector2Int[] directions)
    {
        float reach = 0f;

        foreach (Vector2Int direction in
                 directions)
        {
            int x =
                from.x +
                direction.x;

            int y =
                from.y +
                direction.y;

            int distance = 0;

            while (
                HexChessRules.IsValidCell(
                    x,
                    y))
            {
                distance++;

                StatePiece? target =
                    state.GetPiece(
                        x,
                        y
                    );

                if (target.HasValue)
                {
                    break;
                }

                x += direction.x;
                y += direction.y;
            }

            reach += distance;
        }

        return reach;
    }


    // =========================================================
    // KING SAFETY
    // =========================================================

    private float GetKingSafety(
        GameState state,
        EPlayer player)
    {
        Vector2Int kingPosition =
            FindKing(
                state,
                player
            );

        if (kingPosition.x < 0)
        {
            return -1000f;
        }

        float safety = 0f;

        EPlayer opponent =
            HexChessRules.GetOpponent(
                player
            );


        // -----------------------------------------------------
        // BỊ CHIẾU
        // -----------------------------------------------------

        if (IsSquareAttacked(
                state,
                kingPosition.x,
                kingPosition.y,
                opponent
            ))
        {
            safety -= 50f;
        }


        // -----------------------------------------------------
        // QUANH VUA
        // -----------------------------------------------------

        foreach (Vector2Int direction in
                 HexChessRules.QueenDirections)
        {
            int x =
                kingPosition.x +
                direction.x;

            int y =
                kingPosition.y +
                direction.y;

            if (!HexChessRules.IsValidCell(
                    x,
                    y))
            {
                continue;
            }

            StatePiece? piece =
                state.GetPiece(
                    x,
                    y
                );

            if (!piece.HasValue)
            {
                safety += 0.5f;
            }
            else if (piece.Value.Player ==
                     player)
            {
                safety += 1.5f;
            }
            else
            {
                safety -= 2.0f;
            }


            if (IsSquareAttacked(
                    state,
                    x,
                    y,
                    opponent
                ))
            {
                safety -= 1.5f;
            }
        }

        return safety;
    }


    // =========================================================
    // KING SHELTER
    // =========================================================

    private float GetKingShelterScore(
        GameState state,
        EPlayer player)
    {
        Vector2Int king = FindKing(state, player);
        if (king.x < 0) return -100f;

        float score = 0f;
        int pawnDirection = player == EPlayer.WHITE ? 1 : -1;

        // Exact Shafran neighbourhood: 12 king/queen directions plus a
        // second ring. Only pawns genuinely in the king's forward shelter
        // zone contribute.
        foreach (Vector2Int pos in HexChessRules.CellCoordinates)
        {
            int d = HexChessRules.HexDistance(
                king.x, king.y, pos.x, pos.y);
            if (d <= 0 || d > 2) continue;

            StatePiece? piece = state.GetPiece(pos.x, pos.y);
            if (!piece.HasValue || !piece.Value.IsValid ||
                piece.Value.Player != player ||
                piece.Value.Type != StatePieceType.PAWN)
                continue;

            int forward = (pos.y - king.y) * pawnDirection;
            if (forward < -1) continue;

            float pieceScore = d == 1 ? 1.5f : 0.6f;
            if (forward > 0) pieceScore += 0.8f;

            // A shelter pawn that is itself under more legal attacks than
            // defenders is a weak shelter.
            int attackers = ShafranTacticalState.GetLegalCaptureCount(
                state,
                HexChessRules.GetOpponent(player),
                pos.x, pos.y);
            int defenders = ShafranTacticalState.GetLegalDefenseCount(
                state,
                player,
                pos.x, pos.y);

            if (attackers > defenders) pieceScore *= 0.45f;
            score += pieceScore;
        }

        return score;
    }


    // =========================================================
    // OPEN FILE
    // =========================================================

    private float GetOpenFileScore(
        GameState state,
        EPlayer player)
    {
        if (state == null) return 0f;

        float score = 0f;
        EPlayer opponent = HexChessRules.GetOpponent(player);

        foreach (Vector2Int position in HexChessRules.CellCoordinates)
        {
            StatePiece? piece = state.GetPiece(position.x, position.y);
            if (!piece.HasValue || !piece.Value.IsValid ||
                piece.Value.Player != player ||
                (piece.Value.Type != StatePieceType.ROOK &&
                 piece.Value.Type != StatePieceType.QUEEN))
                continue;

            // A Shafran rook/queen has six long-range rays. Reward an actually
            // open ray / semi-open ray instead of scanning x == constant for
            // every cell.
            for (int i = 0; i < HexChessRules.RookDirections.Length; i++)
            {
                Vector2Int dir = HexChessRules.RookDirections[i];
                int x = position.x + dir.x;
                int y = position.y + dir.y;
                bool friendlyPawn = false;
                bool enemyPawn = false;

                while (HexChessRules.IsValidCell(x, y))
                {
                    StatePiece? other = state.GetPiece(x, y);
                    if (other.HasValue && other.Value.IsValid)
                    {
                        if (other.Value.Type == StatePieceType.PAWN)
                        {
                            if (other.Value.Player == player) friendlyPawn = true;
                            else if (other.Value.Player == opponent) enemyPawn = true;
                        }

                        // A piece blocks the ray after its square.
                        break;
                    }

                    x += dir.x;
                    y += dir.y;
                }

                if (!friendlyPawn && !enemyPawn) score += 0.45f;
                else if (!friendlyPawn && enemyPawn) score += 0.18f;
            }
        }

        return score;
    }


    // =========================================================
    // PAWN STRUCTURE
    // =========================================================

    private float GetPawnStructureBonus(
        GameState state,
        Vector2Int position,
        StatePiece pawn)
    {
        if (!pawn.IsValid || pawn.Type != StatePieceType.PAWN)
            return 0f;

        float score = 0f;
        int direction = pawn.Player == EPlayer.WHITE ? 1 : -1;
        EPlayer opponent = HexChessRules.GetOpponent(pawn.Player);

        // Exact Shafran pawn support: a pawn protects the two diagonal-forward
        // capture cells (x +/- 1, y + direction).
        bool supported = false;
        int supportOffsets = 0;
        for (int dx = -1; dx <= 1; dx += 2)
        {
            int x = position.x + dx;
            int y = position.y + direction;
            if (!HexChessRules.IsValidCell(x, y)) continue;

            StatePiece? other = state.GetPiece(x, y);
            if (other.HasValue && other.Value.IsValid &&
                other.Value.Player == pawn.Player &&
                other.Value.Type == StatePieceType.PAWN)
            {
                supported = true;
                supportOffsets++;
            }
        }

        if (supported) score += 3.0f + 0.75f * supportOffsets;
        else score -= 1.0f;

        // Same-file friendly pawn in front is a genuine stack/block, not any
        // arbitrary pawn in the same file.
        int frontY = position.y + direction;
        if (HexChessRules.IsValidCell(position.x, frontY))
        {
            StatePiece? front = state.GetPiece(position.x, frontY);
            if (front.HasValue && front.Value.IsValid &&
                front.Value.Player == pawn.Player &&
                front.Value.Type == StatePieceType.PAWN)
            {
                score -= 1.5f;
            }
            else if (front.HasValue && front.Value.IsValid &&
                     front.Value.Player == opponent &&
                     front.Value.Type == StatePieceType.PAWN)
            {
                // True enemy blocker: valuable only when it is not already an
                // impossible/illegal target.
                int captures = ShafranTacticalState.GetLegalCaptureCount(
                    state, pawn.Player, position.x, frontY);
                if (captures > 0) score += 0.9f;
                else score -= 0.4f;
            }
        }

        return score;
    }


    // =========================================================
    // FRIENDLY PAWN CHECK
    // =========================================================

    private bool HasFriendlyPawn(
        GameState state,
        int x,
        EPlayer player)
    {
        if (x < 0 ||
            x >= HexChessRules.FILE_COUNT)
        {
            return false;
        }

        foreach (Vector2Int position in
                 HexChessRules.CellCoordinates)
        {
            if (position.x != x)
            {
                continue;
            }

            StatePiece? piece =
                state.GetPiece(
                    position.x,
                    position.y
                );

            if (!piece.HasValue)
            {
                continue;
            }

            if (piece.Value.Player ==
                    player &&
                piece.Value.Type ==
                    StatePieceType.PAWN)
            {
                return true;
            }
        }

        return false;
    }


    // =========================================================
    // SQUARE ATTACKED
    // =========================================================

    private bool IsSquareAttacked(
        GameState state,
        int targetX,
        int targetY,
        EPlayer attacker)
    {
        foreach (Vector2Int position in
                 HexChessRules.CellCoordinates)
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

            if (piece.Value.Player !=
                attacker)
            {
                continue;
            }

            if (CanPieceAttack(
                    state,
                    position,
                    piece.Value,
                    targetX,
                    targetY
                ))
            {
                return true;
            }
        }

        return false;
    }


    // =========================================================
    // CAN PIECE ATTACK
    // =========================================================

    private bool CanPieceAttack(
        GameState state,
        Vector2Int from,
        StatePiece piece,
        int targetX,
        int targetY)
    {
        int dx =
            targetX -
            from.x;

        int dy =
            targetY -
            from.y;


        // -----------------------------------------------------
        // PAWN
        // -----------------------------------------------------

        if (piece.Type ==
            StatePieceType.PAWN)
        {
            int direction =
                piece.Player ==
                EPlayer.WHITE
                    ? 1
                    : -1;

            return
                dy == direction &&
                Mathf.Abs(dx) == 1;
        }


        // -----------------------------------------------------
        // KNIGHT
        // -----------------------------------------------------

        if (piece.Type ==
            StatePieceType.KNIGHT)
        {
            foreach (Vector2Int move in
                     HexChessRules.KnightMoves)
            {
                if (dx == move.x &&
                    dy == move.y)
                {
                    return true;
                }
            }

            return false;
        }


        // -----------------------------------------------------
        // KING
        // -----------------------------------------------------

        if (piece.Type ==
            StatePieceType.KING)
        {
            foreach (Vector2Int direction in
                     HexChessRules.QueenDirections)
            {
                if (dx == direction.x &&
                    dy == direction.y)
                {
                    return true;
                }
            }

            return false;
        }


        // -----------------------------------------------------
        // SLIDING PIECES
        // -----------------------------------------------------

        Vector2Int[] directions =
            null;

        switch (piece.Type)
        {
            case StatePieceType.BISHOP:

                directions =
                    HexChessRules.BishopDirections;

                break;

            case StatePieceType.ROOK:

                directions =
                    HexChessRules.RookDirections;

                break;

            case StatePieceType.QUEEN:

                directions =
                    HexChessRules.QueenDirections;

                break;
        }

        if (directions == null)
        {
            return false;
        }


        foreach (Vector2Int direction in
                 directions)
        {
            int x =
                from.x +
                direction.x;

            int y =
                from.y +
                direction.y;

            while (
                HexChessRules.IsValidCell(
                    x,
                    y))
            {
                if (x == targetX &&
                    y == targetY)
                {
                    return true;
                }

                StatePiece? target =
                    state.GetPiece(
                        x,
                        y
                    );

                if (target.HasValue)
                {
                    break;
                }

                x += direction.x;
                y += direction.y;
            }
        }

        return false;
    }


    private int CountNonKingPieces(GameState state)
    {
        if (state == null)
            return 0;

        int count = 0;

        foreach (Vector2Int position in
                 HexChessRules.CellCoordinates)
        {
            StatePiece? piece =
                state.GetPiece(
                    position.x,
                    position.y
                );

            if (piece.HasValue &&
                piece.Value.IsValid &&
                piece.Value.Type != StatePieceType.KING)
            {
                count++;
            }
        }

        return count;
    }

    // =========================================================
    // FIND KING
    // =========================================================

    private float GetEndgamePhase(
        GameState state,
        EPlayer aiPlayer,
        EPlayer opponent)
    {
        float opening;
        float middlegame;
        float endgame;

        HexChessRules.GetStrategicPhase(
            state,
            aiPlayer,
            out opening,
            out middlegame,
            out endgame
        );

        return endgame;
    }

    private float CalculateMaterialForPlayer(
        GameState state,
        EPlayer player)
    {
        return HexChessRules.CalculateNonKingMaterial(
            state,
            player
        );
    }

    private float GetKingConfinementScore(
        GameState state,
        Vector2Int kingPosition,
        EPlayer attackingPlayer)
    {
        float score = 0f;

        foreach (Vector2Int direction in
                 HexChessRules.QueenDirections)
        {
            int x =
                kingPosition.x +
                direction.x;

            int y =
                kingPosition.y +
                direction.y;

            if (!HexChessRules.IsValidCell(
                    x,
                    y))
            {
                continue;
            }

            if (IsSquareAttacked(
                    state,
                    x,
                    y,
                    attackingPlayer))
            {
                score += 1f;
            }
        }

        return score;
    }

    private Vector2Int FindKing(
        GameState state,
        EPlayer player)
    {
        foreach (Vector2Int position in
                 HexChessRules.CellCoordinates)
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

            if (piece.Value.Player ==
                    player &&
                piece.Value.Type ==
                    StatePieceType.KING)
            {
                return position;
            }
        }

        return new Vector2Int(
            -1,
            -1
        );
    }
}