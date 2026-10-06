using Unity.MLAgents.Actuators;
using UnityEngine;

public partial class ChessAgent
{
    public override void WriteDiscreteActionMask(
        IDiscreteActionMask actionMask)
    {
        if (!IsActionEnvironmentReady() ||
            !IsGameRunning() ||
            !IsAgentTurn())
        {
            return;
        }

        // Masking is valid only for the exact decision request.
        // Otherwise leave the branch unmasked to avoid an all-masked
        // discrete branch; OnActionReceived will reject stale actions.
        if (!_decisionRequestedForTurn ||
            BaseGameController.Current.CurrentPlayer != _decisionRequestedPlayer ||
            _cachedGameState == null ||
            _cachedHash != _decisionRequestedHash)
        {
            return;
        }

        bool[] legalActions =
            GetCachedLegalActionTable();

        int legalCount = 0;

        for (int i = 0; i < ACTION_COUNT; i++)
        {
            if (legalActions[i])
            {
                legalCount++;
            }
        }

        if (legalCount <= 0)
        {
            return;
        }

        for (int action = 0; action < ACTION_COUNT; action++)
        {
            if (!legalActions[action])
            {
                actionMask.SetActionEnabled(
                    0,
                    action,
                    false
                );
            }
        }
    }

    public override void OnActionReceived(
        ActionBuffers actionBuffers)
    {
        if (!IsActionEnvironmentReady() ||
            !IsGameRunning() ||
            !IsAgentTurn())
        {
            return;
        }

        // -----------------------------------------------------
        // STALE DECISION PROTECTION
        // -----------------------------------------------------

        // ML-Agents may call OnActionReceived on environment steps where
        // no new decision was requested. The action in that callback is
        // the previous action and MUST NOT be counted as a stale move.
        // It is simply ignored.
        if (!_decisionRequestedForTurn)
        {
            return;
        }

        if (BaseGameController.Current.CurrentPlayer !=
            _decisionRequestedPlayer)
        {
            _decisionRequestedForTurn = false;
            return;
        }

        if (!_legalCacheValid ||
            _cachedGameState == null ||
            _cachedHash != _decisionRequestedHash)
        {
            _decisionRequestedForTurn = false;
            return;
        }

        if (actionBuffers.DiscreteActions.Length == 0)
        {
            _decisionRequestedForTurn = false;
            return;
        }

        int action =
            actionBuffers.DiscreteActions[0];

        // The request is consumed now. Anything arriving after this point
        // belongs to an old request and cannot execute another move.
        _decisionRequestedForTurn = false;
        _decisionRequestedHash = ulong.MaxValue;

        if (!TryGetCachedMove(
                action,
                out StateMove selectedMove))
        {
            ApplyInvalidMoveReward();
            InvalidateLegalMoveCache();
            return;
        }

        if (selectedMove.MovingPiece.Player != agentPlayer)
        {
            ApplyInvalidMoveReward();
            InvalidateLegalMoveCache();
            return;
        }

        _selectedStateMove = selectedMove;

        _lastAgentMoveWasPromotion =
            selectedMove.IsPromotion;

        SaveEvaluationBeforeMove();

        Cell fromCell =
            ChessBoard.Current.GetCell(
                selectedMove.From.X,
                selectedMove.From.Y
            );

        Cell toCell =
            ChessBoard.Current.GetCell(
                selectedMove.To.X,
                selectedMove.To.Y
            );

        if (fromCell == null ||
            toCell == null ||
            fromCell.CurrentPiece == null ||
            fromCell.CurrentPiece.Player != agentPlayer)
        {
            ApplyInvalidMoveReward();
            InvalidateLegalMoveCache();
            return;
        }

        BasePiece piece =
            fromCell.CurrentPiece;

        ExecuteStateMove(
            piece,
            toCell,
            selectedMove
        );

        _terminalStateDirty = true;

        if (selectedMove.IsCapture)
        {
            ApplyCaptureReward(
                selectedMove.CapturedPiece
            );

            RewardSuccessfulCapture(
                selectedMove.CapturedPiece
            );
        }
        else
        {
            ApplyLegalMoveReward();
        }

        // Build the post-move state once and reuse it.
        GameState stateAfter = GameState.FromUnityBoard();

        AddShapingReward(
            CalculateDynamicPlyPenalty(stateAfter)
        );

        if (selectedMove.IsPromotion)
        {
            RewardPromotion();
        }

        if (selectedMove.IsCastling)
        {
            RewardCastling();
        }

        RewardCheckIfNeeded();

        ApplyPostMoveStrategicReward(stateAfter);

        InvalidateLegalMoveCache();
    }

    private void ExecuteStateMove(
        BasePiece piece,
        Cell targetCell,
        StateMove selectedMove)
    {
        if (piece == null ||
            targetCell == null)
        {
            return;
        }

        if (selectedMove.IsEnPassant &&
            piece is P_Pawn pawn)
        {
            pawn.EnPassantAttack(targetCell);
            return;
        }

        if (selectedMove.IsCapture)
        {
            piece.Attack(targetCell);
            return;
        }

        piece.Move(targetCell);
    }

    public override void Heuristic(
        in ActionBuffers actionsOut)
    {
        ActionSegment<int> actions =
            actionsOut.DiscreteActions;

        if (actions.Length == 0)
        {
            return;
        }

        actions[0] =
            FindFirstLegalAction();
    }
}
