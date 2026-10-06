using UnityEngine;

public partial class ChessAgent
{
    private void SaveEvaluationBeforeMove()
    {
        // Reuse the exact cached state used for action masking/request.
        // This avoids rebuilding a second GameState from the visual board.
        _stateBeforeMove = _cachedGameState;

        if (_stateBeforeMove == null)
        {
            _evaluationBeforeMove = 0f;
            return;
        }

        _evaluationBeforeMove =
            _evaluation.Evaluate(
                _stateBeforeMove
            );
    }

    private void ApplyPostMoveStrategicReward(GameState stateAfter)
    {
        if (stateAfter == null)
        {
            return;
        }

        float evaluationAfter =
            _evaluation.Evaluate(
                stateAfter
            );

        float evaluationDelta =
            evaluationAfter -
            _evaluationBeforeMove;

        ApplyStrategicReward(
            _stateBeforeMove,
            stateAfter,
            _selectedStateMove,
            evaluationDelta
        );

        RecordPositionAfterMove(
            stateAfter
        );

        // Keep the exact post-move snapshot so the next opponent reply can
        // be evaluated for Queen/major-piece loss.
        _lastAgentMoveState =
            stateAfter.Clone();

        _lastOpponentResponseHash =
            stateAfter.Hash;
    }
}
