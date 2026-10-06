using System.Collections.Generic;
using UnityEngine;

public partial class ChessAgent
{
    // =========================================================
    // V9 TACTICAL DECISION QUALITY
    // =========================================================
    // This is the main tactical upgrade over V8.
    //
    // V8: mainly same-square capture/recapture sequence.
    // V9: legal attack/defense map + whole-board forcing-reply scan.
    // It catches:
    //   * R x R followed by Q x R elsewhere/on target
    //   * a move that leaves Q/R/B/N hanging
    //   * missed recapture
    //   * forks / double attacks
    //   * removing a defender
    //   * checks / promotions that change the evaluation
    //   * pinned defenders being counted correctly
    // =========================================================

    private float CalculateV9TacticalDecisionQualityReward(
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
            0.55f * opening +
            1.55f * middlegame +
            1.10f * endgame
        );

        if (phase <= 0.05f)
            return 0f;

        EPlayer opponent =
            HexChessRules.GetOpponent(agentPlayer);

        float reward = 0f;

        // -----------------------------------------------------
        // 1. PROTECT OWN HIGH-VALUE MATERIAL
        // -----------------------------------------------------
        float ownCriticalBefore =
            GetCriticalHangingMaterialScore(
                before,
                agentPlayer
            );

        float ownCriticalAfter =
            GetCriticalHangingMaterialScore(
                after,
                agentPlayer
            );

        float saved = Mathf.Max(
            0f,
            ownCriticalBefore - ownCriticalAfter
        );

        reward +=
            Mathf.Clamp(
                saved * 0.022f,
                0f,
                0.040f
            );

        // A new hanging high-value piece is strongly negative. A checking
        // sacrifice gets a partial discount because it may be intentional.
        float newlyHanging = Mathf.Max(
            0f,
            ownCriticalAfter - ownCriticalBefore
        );

        bool givesCheck =
            _moveGenerator.IsInCheck(
                after,
                opponent
            );

        if (newlyHanging > 0.25f)
        {
            float penalty =
                Mathf.Clamp(
                    newlyHanging * 0.035f,
                    0f,
                    0.075f
                );

            if (givesCheck)
            {
                penalty *= 0.65f;
            }

            reward -= penalty;
        }

        // -----------------------------------------------------
        // 2. WHOLE-BOARD TACTICAL REPLY SEARCH
        // -----------------------------------------------------
        float worstReplyLoss = 0f;
        bool tacticalTrigger =
            selectedMove.IsCapture ||
            givesCheck ||
            ownCriticalBefore > 0.25f ||
            ownCriticalAfter > 0.25f;

        if (tacticalTrigger)
        {
            // Depth-1 is the normal PPO path. Depth-2 is reserved for
            // concrete captures/checks in compact positions; this keeps the
            // tactical signal while preventing a search explosion on every
            // quiet move.
            int nonKingPieces = CountNonKingPiecesForReward(after, agentPlayer) +
                                CountNonKingPiecesForReward(after, opponent);
            int searchDepth =
                (selectedMove.IsCapture || givesCheck) &&
                GetMaterialValue(selectedMove.MovingPiece) >= 4.25f &&
                nonKingPieces <= 8
                    ? 2
                    : 1;

            worstReplyLoss =
                EvaluateWorstTacticalReplyLossV9(
                    after,
                    opponent,
                    agentPlayer,
                    searchDepth
                );

            if (worstReplyLoss >= 1.5f)
            {
                reward -=
                    Mathf.Clamp(
                        worstReplyLoss * 0.018f,
                        0f,
                        0.080f
                    );
            }

            if (worstReplyLoss <= 0.75f &&
                (selectedMove.IsCapture || givesCheck))
            {
                reward += 0.010f;
            }
        }

        // -----------------------------------------------------
        // 3. CAPTURE QUALITY
        // -----------------------------------------------------
        if (selectedMove.IsCapture &&
            selectedMove.CapturedPiece.IsValid &&
            selectedMove.CapturedPiece.Type != StatePieceType.KING)
        {
            int totalNonKingPieces =
                CountNonKingPiecesForReward(after, agentPlayer) +
                CountNonKingPiecesForReward(after, opponent);

            int captureDepth =
                GetMaterialValue(selectedMove.CapturedPiece) >= 7.0f &&
                totalNonKingPieces <= 8
                    ? 2
                    : 1;

            float net =
                CalculateV9CaptureNet(
                    before,
                    selectedMove,
                    agentPlayer,
                    captureDepth
                );

            // Exact destination-square legality check: if the capturing piece
            // lands on a square with more legal attackers than legal defenders,
            // treat the exchange as unsafe even when gross material looked good.
            int destinationAttackers =
                ShafranTacticalState.GetLegalCaptureCount(
                    after, opponent, selectedMove.To.X, selectedMove.To.Y);
            int destinationDefenders =
                ShafranTacticalState.GetLegalDefenseCount(
                    after, agentPlayer, selectedMove.To.X, selectedMove.To.Y);

            if (destinationAttackers > destinationDefenders)
            {
                net -= 0.75f *
                    Mathf.Max(0f, GetMaterialValue(selectedMove.MovingPiece));
            }

            if (net >= 1.0f)
            {
                reward +=
                    Mathf.Clamp(
                        0.014f + net * 0.004f,
                        0f,
                        0.036f
                    );
            }
            else if (net <= -1.0f)
            {
                reward -=
                    Mathf.Clamp(
                        Mathf.Abs(net) * 0.014f,
                        0f,
                        0.070f
                    );
            }
        }

        // -----------------------------------------------------
        // 4. RECAPTURE / THREAT REMOVAL
        // -----------------------------------------------------
        if (IsImmediateRecaptureMove(
                before,
                selectedMove))
        {
            reward += 0.028f;
        }
        else if (HasImmediateRecaptureOpportunity(
                     before,
                     agentPlayer))
        {
            reward -= 0.045f;
        }

        if (tacticalTrigger)
        {
            float threatBefore =
                GetBestImmediateCaptureValueV9(
                    before,
                    opponent
                );

            float threatAfter =
                GetBestImmediateCaptureValueV9(
                    after,
                    opponent
                );

            if (threatBefore - threatAfter >= 1.0f)
            {
                reward += 0.020f *
                    Mathf.Clamp01(
                        (threatBefore - threatAfter) / 7f
                    );
            }
            else if (threatAfter - threatBefore >= 1.5f)
            {
                reward -= 0.035f *
                    Mathf.Clamp01(
                        (threatAfter - threatBefore) / 7f
                    );
            }
        }

        // -----------------------------------------------------
        // 5. DOUBLE ATTACK / FORK
        // -----------------------------------------------------
        if (selectedMove.MovingPiece.Type !=
            StatePieceType.KING)
        {
            int highValueForkTargets =
                CountHighValueCaptureTargetsFromSquareV9(
                    after,
                    agentPlayer,
                    selectedMove.To.X,
                    selectedMove.To.Y
                );

            if (highValueForkTargets >= 2)
            {
                reward +=
                    Mathf.Clamp(
                        0.014f +
                        (highValueForkTargets - 2) * 0.006f,
                        0f,
                        0.030f
                    );
            }
        }

        // -----------------------------------------------------
        // 6. REMOVE A DEFENDER
        // -----------------------------------------------------
        reward +=
            CalculateDefenderRemovalRewardV9(
                before,
                after
            );

        // -----------------------------------------------------
        // 7. DEVELOPMENT QUALITY
        // -----------------------------------------------------
        if (opening > 0.10f &&
            (selectedMove.MovingPiece.Type == StatePieceType.KNIGHT ||
             selectedMove.MovingPiece.Type == StatePieceType.BISHOP))
        {
            float safety =
                ShafranTacticalState.GetSquareSafetyFast(
                    after,
                    agentPlayer,
                    selectedMove.To.X,
                    selectedMove.To.Y
                );

            if (safety >= 0f)
            {
                reward += 0.012f * opening;
            }
            else if (!givesCheck)
            {
                reward -= 0.018f * opening;
            }
        }

        // -----------------------------------------------------
        // 8. KING MOVES: only reward if tactical purpose exists
        // -----------------------------------------------------
        if (selectedMove.MovingPiece.Type ==
            StatePieceType.KING)
        {
            bool ownMaterialUrgency =
                ownCriticalBefore > 0.25f;

            // If a concrete Q/R/B/N capture is available, a quiet King move
            // should not compete with that tactical action simply because the
            // King happens to gain a small positional signal.
            float bestOwnTacticalGain =
                GetBestImmediateCaptureValueV9(
                    before,
                    agentPlayer);

            if (bestOwnTacticalGain >= 3.50f &&
                !selectedMove.IsCapture &&
                !givesCheck)
            {
                reward -=
                    Mathf.Clamp(
                        0.028f + bestOwnTacticalGain * 0.004f,
                        0f,
                        0.050f);
            }

            if (ownMaterialUrgency &&
                ownCriticalAfter >= ownCriticalBefore - 0.05f &&
                !selectedMove.IsCapture &&
                !givesCheck)
            {
                reward -= 0.028f;
            }
        }

        // Slightly stronger tactical weight in middlegame, but never enough
        // to overpower the terminal result because the global shaping cap
        // still applies in ApplyShapingComponent.
        return Mathf.Clamp(
            reward * Mathf.Max(0.75f, phase),
            -0.10f,
            0.10f
        );
    }

    private float CalculateV9CaptureNet(
        GameState state,
        StateMove capture,
        EPlayer capturer,
        int depth)
    {
        if (state == null ||
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

        GameState after = state.Clone();
        _moveGenerator.MakeMove(after, capture);

        float initialGain =
            GetMaterialValue(
                capture.CapturedPiece
            );

        float replyLoss =
            EvaluateWorstTacticalReplyLossV9(
                after,
                HexChessRules.GetOpponent(capturer),
                capturer,
                Mathf.Max(1, depth)
            );

        return initialGain - replyLoss;
    }

    private float GetBestV9TacticalCaptureGain(
        GameState state,
        EPlayer player,
        int depth)
    {
        if (state == null ||
            _moveGenerator == null)
        {
            return 0f;
        }

        List<StateMove> captures =
            _moveGenerator.GenerateLegalCaptureMoves(
                state,
                player
            );

        float best = 0f;

        for (int i = 0; i < captures.Count; i++)
        {
            StateMove move = captures[i];

            if (!move.CapturedPiece.IsValid ||
                move.CapturedPiece.Type == StatePieceType.KING)
            {
                continue;
            }

            float gain =
                CalculateV9CaptureNet(
                    state,
                    move,
                    player,
                    depth
                );

            best = Mathf.Max(best, gain);
        }

        return best;
    }

    private float EvaluateWorstTacticalReplyLossV9(
        GameState state,
        EPlayer sideToMove,
        EPlayer victim,
        int depth)
    {
        if (state == null || _moveGenerator == null || depth <= 0)
            return 0f;

        List<StateMove> legalMoves =
            _moveGenerator.GenerateLegalMoves(state, sideToMove);

        if (legalMoves.Count == 0)
            return 0f;

        float baselineEvaluation =
            _evaluation != null ? _evaluation.Evaluate(state) : 0f;

        // First rank replies without cloning every candidate. Captures and
        // promotions are the only replies that normally matter for the fast
        // exchange safety check. Quiet moves are kept only as a small fallback.
        legalMoves.Sort((a, b) =>
        {
            float av =
                a.IsCapture && a.CapturedPiece.IsValid
                    ? GetMaterialValue(a.CapturedPiece)
                    : 0f;
            float bv =
                b.IsCapture && b.CapturedPiece.IsValid
                    ? GetMaterialValue(b.CapturedPiece)
                    : 0f;

            float ap = av * 10f + (a.IsPromotion ? 90f : 0f);
            float bp = bv * 10f + (b.IsPromotion ? 90f : 0f);
            return bp.CompareTo(ap);
        });

        int limit = Mathf.Min(legalMoves.Count, 4);
        float worstLoss = 0f;

        for (int i = 0; i < limit; i++)
        {
            StateMove move = legalMoves[i];
            GameState next = state.Clone();
            _moveGenerator.MakeMove(next, move);

            float capturedValue =
                move.IsCapture && move.CapturedPiece.IsValid
                    ? GetMaterialValue(move.CapturedPiece)
                    : 0f;

            bool givesCheck =
                _moveGenerator.IsInCheck(next, victim);

            float recovery =
                GetBestImmediateCaptureValueV9(next, victim);

            float materialLoss = Mathf.Max(
                0f,
                capturedValue - recovery * 0.75f);

            float nextEvaluation =
                _evaluation != null
                    ? _evaluation.Evaluate(next)
                    : baselineEvaluation;

            float evaluationLoss = victim == agentPlayer
                ? Mathf.Max(0f, baselineEvaluation - nextEvaluation) / 1000f
                : Mathf.Max(0f, nextEvaluation - baselineEvaluation) / 1000f;

            float loss = Mathf.Max(
                materialLoss,
                evaluationLoss * 1.25f);

            if (givesCheck) loss += 0.35f;
            if (move.IsPromotion) loss += 3.0f;

            if (depth > 1 &&
                (loss >= 0.75f || givesCheck || move.IsPromotion))
            {
                float continuation =
                    EvaluateBestDefensiveContinuationV9(
                        next,
                        victim,
                        depth - 1);

                loss = Mathf.Max(loss, continuation);
            }

            worstLoss = Mathf.Max(worstLoss, loss);
        }

        return worstLoss;
    }

    private float EvaluateBestDefensiveContinuationV9(
        GameState state,
        EPlayer victim,
        int depth)
    {
        if (state == null || _moveGenerator == null || depth <= 0)
            return 0f;

        List<StateMove> legalMoves =
            _moveGenerator.GenerateLegalMoves(state, victim);

        if (legalMoves.Count == 0)
            return 0f;

        legalMoves.Sort((a, b) =>
        {
            float av =
                a.IsCapture && a.CapturedPiece.IsValid
                    ? GetMaterialValue(a.CapturedPiece)
                    : 0f;
            float bv =
                b.IsCapture && b.CapturedPiece.IsValid
                    ? GetMaterialValue(b.CapturedPiece)
                    : 0f;

            float ap = av * 10f + (a.IsPromotion ? 90f : 0f);
            float bp = bv * 10f + (b.IsPromotion ? 90f : 0f);
            return bp.CompareTo(ap);
        });

        int limit = Mathf.Min(legalMoves.Count, 2);
        float best = float.MaxValue;

        for (int i = 0; i < limit; i++)
        {
            GameState candidateState = state.Clone();
            _moveGenerator.MakeMove(candidateState, legalMoves[i]);

            float opponentLoss =
                EvaluateWorstTacticalReplyLossV9(
                    candidateState,
                    HexChessRules.GetOpponent(victim),
                    victim,
                    depth - 1);

            best = Mathf.Min(best, opponentLoss);
        }

        return best == float.MaxValue ? 0f : best;
    }

    private float GetBestImmediateCaptureValueV9(
        GameState state,
        EPlayer player)
    {
        if (state == null)
            return 0f;

        float best = 0f;

        foreach (Vector2Int position in HexChessRules.CellCoordinates)
        {
            StatePiece? target = state.GetPiece(position.x, position.y);
            if (!target.HasValue ||
                !target.Value.IsValid ||
                target.Value.Player == player ||
                target.Value.Type == StatePieceType.KING)
            {
                continue;
            }

            if (ShafranTacticalState.GetPseudoAttackCount(
                    state, player, position.x, position.y) <= 0)
            {
                continue;
            }

            best = Mathf.Max(best, GetMaterialValue(target.Value));
        }

        return best;
    }

    private int CountHighValueCaptureTargetsFromSquareV9(
        GameState state,
        EPlayer player,
        int x,
        int y)
    {
        if (state == null)
            return 0;

        EPlayer opponent =
            HexChessRules.GetOpponent(player);

        int count = 0;

        foreach (Vector2Int target in HexChessRules.CellCoordinates)
        {
            StatePiece? piece =
                state.GetPiece(target.x, target.y);

            if (!piece.HasValue ||
                !piece.Value.IsValid ||
                piece.Value.Player != opponent ||
                piece.Value.Type == StatePieceType.KING ||
                piece.Value.Type == StatePieceType.PAWN)
            {
                continue;
            }

            if (GetMaterialValue(piece.Value) < 3.50f)
                continue;

            if (ShafranTacticalState.IsPseudoAttackingSquare(
                    state,
                    player,
                    x,
                    y,
                    target.x,
                    target.y))
            {
                count++;
            }
        }

        return count;
    }

    private float CalculateDefenderRemovalRewardV9(
        GameState before,
        GameState after)
    {
        if (before == null ||
            after == null)
        {
            return 0f;
        }

        EPlayer opponent =
            HexChessRules.GetOpponent(agentPlayer);

        float reward = 0f;
        int inspected = 0;

        foreach (Vector2Int position in
                 HexChessRules.CellCoordinates)
        {
            StatePiece? enemy =
                after.GetPiece(
                    position.x,
                    position.y
                );

            if (!enemy.HasValue ||
                !enemy.Value.IsValid ||
                enemy.Value.Player != opponent ||
                enemy.Value.Type == StatePieceType.PAWN ||
                enemy.Value.Type == StatePieceType.KING)
            {
                continue;
            }

            int beforePseudoDefenders =
                ShafranTacticalState.GetPseudoAttackCount(
                    before, opponent, position.x, position.y);
            int afterPseudoDefenders =
                ShafranTacticalState.GetPseudoAttackCount(
                    after, opponent, position.x, position.y);
            int ourPseudoAttacksAfter =
                ShafranTacticalState.GetPseudoAttackCount(
                    after, agentPlayer, position.x, position.y);

            // Only verify exact legal defenders when the fast geometric map
            // shows a real removal candidate. This keeps the reward cheap on
            // ordinary moves while preserving exact verification for the
            // actual defender-removal event.
            if (ourPseudoAttacksAfter > 0 &&
                beforePseudoDefenders > 0 &&
                afterPseudoDefenders == 0)
            {
                int beforeDefenders =
                    ShafranTacticalState.GetLegalDefenseCount(
                        before, opponent, position.x, position.y);
                int afterDefenders =
                    ShafranTacticalState.GetLegalDefenseCount(
                        after, opponent, position.x, position.y);
                int ourAttacksAfter =
                    ShafranTacticalState.GetLegalCaptureCount(
                        after, agentPlayer, position.x, position.y);

                if (ourAttacksAfter > 0 &&
                    beforeDefenders > 0 &&
                    afterDefenders == 0)
                {
                    reward += 0.014f;
                }
            }

            inspected++;
            if (inspected >= 12)
                break;
        }

        return Mathf.Clamp(
            reward,
            0f,
            0.035f
        );
    }

    // =========================================================
    // EXACT FORCED MATE-IN-2
    // =========================================================
    private bool HasForcedMateInTwoV9(
        GameState state,
        EPlayer opponent)
    {
        if (state == null ||
            _moveGenerator == null)
        {
            return false;
        }

        List<StateMove> opponentReplies =
            _moveGenerator.GenerateLegalMoves(
                state,
                opponent
            );

        // Keep this exact only for compact tactical positions. If there are
        // many replies, it is too expensive to prove a forced mate here.
        if (opponentReplies.Count == 0 ||
            opponentReplies.Count > 24)
        {
            return false;
        }

        for (int i = 0; i < opponentReplies.Count; i++)
        {
            GameState replyState =
                state.Clone();

            _moveGenerator.MakeMove(
                replyState,
                opponentReplies[i]
            );

            if (!CanMateInOneV9(
                    replyState,
                    opponent
                ))
            {
                return false;
            }
        }

        return true;
    }

    private bool CanMateInOneV9(
        GameState state,
        EPlayer victim)
    {
        EPlayer attacker =
            HexChessRules.GetOpponent(victim);

        List<StateMove> moves =
            _moveGenerator.GenerateLegalMoves(
                state,
                attacker
            );

        for (int i = 0; i < moves.Count; i++)
        {
            GameState candidate =
                state.Clone();

            _moveGenerator.MakeMove(
                candidate,
                moves[i]
            );

            if (!_moveGenerator.IsInCheck(
                    candidate,
                    victim
                ))
            {
                continue;
            }

            List<StateMove> replies =
                _moveGenerator.GenerateLegalMoves(
                    candidate,
                    victim
                );

            if (replies.Count == 0)
            {
                return true;
            }
        }

        return false;
    }

    private readonly struct TacticalReplyCandidateV9
    {
        public readonly StateMove Move;
        public readonly GameState Next;
        public readonly float Priority;
        public readonly float CapturedValue;
        public readonly bool GivesCheck;

        public TacticalReplyCandidateV9(
            StateMove move,
            GameState next,
            float priority,
            float capturedValue,
            bool givesCheck)
        {
            Move = move;
            Next = next;
            Priority = priority;
            CapturedValue = capturedValue;
            GivesCheck = givesCheck;
        }
    }
}
