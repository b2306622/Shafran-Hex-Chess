using UnityEngine;

public partial class ChessAgent
{
    public override void OnEpisodeBegin()
    {
        ResetRuntimeState();

        if (resetBoardOnlyForWhiteAgent &&
            agentPlayer != EPlayer.WHITE)
        {
            return;
        }

        ResetChessEnvironment();
    }

    private void ResetChessEnvironment()
    {
        // WHITE performs the actual environment reset, so this increments
        // exactly once per self-play game. Diagnostic only.
        _sharedDebugEpisodeId++;

        // Reset the visible board FIRST so the controller never hashes the
        // terminal board as the starting position of the next episode.
        if (ChessBoard.Current != null)
        {
            ChessBoard.Current.ResetBoard();
        }

        if (BaseGameController.Current != null)
        {
            BaseGameController.Current.UseThinkTimer = false;
            BaseGameController.Current.UseMaxGamePlies = true;

            // Force the training environment to use the configured
            // 500-ply cap even when an older scene still contains 250.
            BaseGameController.Current.MaxGamePlies =
                Mathf.Max(1, trainingMaxGamePlies);

            // Self-play uses game-state changes immediately; move animation
            // is visual-only and does not delay the environment step.
            BaseGameController.Current.TrainingMode = true;
            BaseGameController.Current.ResetGameState();
            BaseGameController.Current.ResetPositionHistoryFromCurrentBoard();
        }

        InvalidateLegalMoveCache();

        if (ChessBoard.Current != null)
        {
            GameState initial =
                GameState.FromUnityBoard();

            if (initial != null)
            {
                _positionVisitCounts.Clear();
                _positionVisitCounts[initial.Hash] = 1;
            }
        }

        _turnObserved = false;
        _decisionRequestedForTurn = false;
        _decisionRequestedHash = ulong.MaxValue;
        _decisionRequestedPlayer = EPlayer.WHITE;
        _terminalStateDirty = true;
    }

    private void ResolveGameResult()
    {
        if (_episodeResultProcessed)
        {
            return;
        }

        if (BaseGameController.Current == null)
        {
            return;
        }

        if (BaseGameController.Current.GameState != EGameState.GAME_OVER)
        {
            return;
        }

        _episodeResultProcessed = true;

        // Never allow a decision generated for the finished position
        // to survive into the next episode.
        _decisionRequestedForTurn = false;
        _decisionRequestedHash = ulong.MaxValue;
        _decisionRequestedPlayer = EPlayer.WHITE;
        InvalidateLegalMoveCache();

        EPlayer? winner =
            BaseGameController.Current.Winner;

        if (!winner.HasValue)
        {
            ApplyDrawReward();

            EndEpisode();
            return;
        }

        if (winner.Value == agentPlayer)
        {
            ApplyWinReward();
        }
        else
        {
            ApplyLossReward();
        }

        EndEpisode();
    }
}
