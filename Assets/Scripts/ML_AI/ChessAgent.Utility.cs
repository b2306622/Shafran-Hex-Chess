using System.Collections.Generic;
using UnityEngine;

public partial class ChessAgent
{
    private bool IsActionEnvironmentReady()
    {
        return
            ChessBoard.Current != null &&
            ChessBoard.Current.Cells != null &&
            BaseGameController.Current != null;
    }

    private bool IsGameRunning()
    {
        return
            BaseGameController.Current != null &&
            BaseGameController.Current.GameState == EGameState.PLAYING;
    }

    private bool IsAgentTurn()
    {
        return
            BaseGameController.Current != null &&
            BaseGameController.Current.CurrentPlayer == agentPlayer;
    }

    private void InvalidateLegalMoveCache()
    {
        _legalCacheValid = false;
        _cachedLegalMoves.Clear();
        _cachedGameState = null;
        _cachedHash = ulong.MaxValue;

        for (int i = 0; i < ACTION_COUNT; i++)
        {
            _legalActionTable[i] = false;
            _cachedActionMoves[i] = default;
        }

        _cachedGuidanceHash = ulong.MaxValue;
        _hasGuidanceMove = false;
        _cachedGuidanceScore = 0f;
    }

    private void EnsureLegalMoveCache()
    {
        if (!IsActionEnvironmentReady())
        {
            return;
        }

        // The cache is explicitly invalidated on every turn change,
        // reset, and successfully executed move. This avoids rebuilding
        // GameState from the visual board several times during one decision.
        if (_legalCacheValid && _cachedGameState != null)
        {
            return;
        }

        GameState state = GameState.FromUnityBoard();

        if (state == null)
        {
            return;
        }

        _cachedLegalMoves =
            _moveGenerator.GenerateLegalMoves(
                state,
                agentPlayer
            );

        _cachedGameState = state;
        _cachedHash = state.Hash;
        _legalCacheValid = true;

        for (int i = 0; i < ACTION_COUNT; i++)
        {
            _legalActionTable[i] = false;
            _cachedActionMoves[i] = default;
        }

        for (int i = 0; i < _cachedLegalMoves.Count; i++)
        {
            StateMove move = _cachedLegalMoves[i];

            int action = EncodeMove(move);

            if (action < 0 || action >= ACTION_COUNT)
            {
                continue;
            }

            _legalActionTable[action] = true;
            _cachedActionMoves[action] = move;
        }

        EnsurePositionHistoryInitialized(state);
    }

    private void EnsurePositionHistoryInitialized(GameState state)
    {
        if (state == null)
        {
            return;
        }

        if (_positionVisitCounts.Count != 0)
        {
            return;
        }

        _positionVisitCounts[state.Hash] = 1;
    }

    private List<StateMove> GetCachedLegalMoves()
    {
        EnsureLegalMoveCache();
        return _cachedLegalMoves;
    }

    private GameState GetCachedGameState()
    {
        EnsureLegalMoveCache();
        return _cachedGameState;
    }

    private bool[] GetCachedLegalActionTable()
    {
        EnsureLegalMoveCache();
        return _legalActionTable;
    }

    private int EncodeMove(StateMove move)
    {
        int fromIndex =
            HexChessRules.GetCellIndex(
                move.From.X,
                move.From.Y
            );

        int toIndex =
            HexChessRules.GetCellIndex(
                move.To.X,
                move.To.Y
            );

        if (fromIndex < 0 || toIndex < 0)
        {
            return -1;
        }

        return
            fromIndex * CELL_COUNT +
            toIndex;
    }

    private bool TryGetCachedMove(
        int action,
        out StateMove move)
    {
        move = default;

        if (action < 0 || action >= ACTION_COUNT)
        {
            return false;
        }

        EnsureLegalMoveCache();

        if (!_legalActionTable[action])
        {
            return false;
        }

        move = _cachedActionMoves[action];
        return true;
    }

    private int FindFirstLegalAction()
    {
        EnsureLegalMoveCache();

        for (int i = 0; i < ACTION_COUNT; i++)
        {
            if (_legalActionTable[i])
            {
                return i;
            }
        }

        return 0;
    }

    private void TryRequestDecision()
    {
        if (!IsActionEnvironmentReady() ||
            !IsGameRunning() ||
            !IsAgentTurn())
        {
            return;
        }

        if (_decisionRequestedForTurn)
        {
            return;
        }

        EnsureLegalMoveCache();

        if (_cachedLegalMoves == null ||
            _cachedLegalMoves.Count == 0 ||
            _cachedGameState == null)
        {
            return;
        }

        _decisionSequence++;

        // Save the exact position/player BEFORE RequestDecision().
        // A late PPO callback can then be rejected safely.
        _decisionRequestedPlayer =
            BaseGameController.Current.CurrentPlayer;

        _decisionRequestedHash =
            _cachedGameState.Hash;

        // Minimax guidance is teacher/evaluation only. It never chooses
        // the PPO action and is throttled to keep training throughput high.
        EnsureMinimaxGuidance(_cachedGameState);

        _decisionRequestedForTurn = true;
        RequestDecision();
    }

    private void EnsureMinimaxGuidance(GameState state)
    {
        if (!enableMinimaxGuidance || state == null)
        {
            return;
        }

        // Model C: one Minimax teacher, fixed depth 3, every N decisions.
        int frequency =
            Mathf.Max(1, minimaxGuidanceFrequency);

        if ((_decisionSequence % frequency) != 0)
        {
            return;
        }

        // Do not search the same state twice.
        if (_cachedGuidanceHash == state.Hash)
        {
            return;
        }

        if (_guidanceAI == null)
        {
            _guidanceAI = new MinimaxAI(
                agentPlayer,
                Mathf.Max(1, minimaxGuidanceDepth),
                true,
                true,
                true,
                false,
                Mathf.Max(1, minimaxGuidanceTimeLimitMs)
            );
        }

        _cachedGuidanceMove =
            _guidanceAI.GetBestStateMoveFixedDepth(state);

        _cachedGuidanceScore =
            _guidanceAI.Statistics.BestScore;

        _cachedGuidanceHash =
            state.Hash;

        _hasGuidanceMove =
            IsValidStateMove(_cachedGuidanceMove);
    }

    private bool IsValidStateMove(StateMove move)
    {
        return
            HexChessRules.IsValidCell(move.From.X, move.From.Y) &&
            HexChessRules.IsValidCell(move.To.X, move.To.Y);
    }

    private bool HasGuidanceMove(GameState state)
    {
        return
            state != null &&
            _hasGuidanceMove &&
            _cachedGuidanceHash == state.Hash;
    }

    private StateMove GetGuidanceMove()
    {
        return _cachedGuidanceMove;
    }

    private float GetGuidanceScore()
    {
        return _cachedGuidanceScore;
    }

    private bool SameMove(
        StateMove a,
        StateMove b)
    {
        return
            a.From.X == b.From.X &&
            a.From.Y == b.From.Y &&
            a.To.X == b.To.X &&
            a.To.Y == b.To.Y;
    }

    private float EvaluateCandidateMove(
        GameState state,
        StateMove move)
    {
        if (state == null)
        {
            return 0f;
        }

        GameState copy = state.Clone();

        _moveGenerator.MakeMove(
            copy,
            move
        );

        return _evaluation.Evaluate(copy);
    }

    private bool IsImmediateReverseMove(
        GameState beforeState,
        StateMove selectedMove)
    {
        if (beforeState == null ||
            !beforeState.LastMove.HasValue)
        {
            return false;
        }

        StateMove previous =
            beforeState.LastMove.Value;

        return
            selectedMove.From.X == previous.To.X &&
            selectedMove.From.Y == previous.To.Y &&
            selectedMove.To.X == previous.From.X &&
            selectedMove.To.Y == previous.From.Y;
    }

    private void RecordPositionAfterMove(
        GameState state)
    {
        if (state == null)
        {
            return;
        }

        int previousCount = 0;

        _positionVisitCounts.TryGetValue(
            state.Hash,
            out previousCount
        );

        int newCount = previousCount + 1;

        _positionVisitCounts[state.Hash] =
            newCount;
    }

    private int GetPositionVisitCount(
        ulong hash)
    {
        if (BaseGameController.Current != null)
        {
            return BaseGameController.Current
                .GetPositionVisitCount(hash);
        }

        return 0;
    }
}
