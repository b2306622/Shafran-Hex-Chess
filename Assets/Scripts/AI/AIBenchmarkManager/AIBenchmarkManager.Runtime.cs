using System.Collections;
using UnityEngine;

public partial class AIBenchmarkManager
{
    protected IEnumerator WaitForChessBoardReady()
    {
        float timeout = 10f;
        float elapsed = 0f;

        while (_benchmarkRunning)
        {
            if (
                ChessBoard.Current != null &&
                ChessBoard.Current.Cells != null)
            {
                GameState testState =
                    GameState.FromUnityBoard();

                if (
                    IsInitialStateValid(
                        testState
                    ))
                {
                    yield break;
                }
            }

            elapsed +=
                Time.deltaTime;

            if (elapsed >= timeout)
            {
                Debug.LogError(
                    "[AIBenchmark] Timeout waiting for ChessBoard."
                );

                yield break;
            }

            yield return null;
        }
    }

    protected void DisableGameController()
    {
        _gameController = null;

        if (BaseGameController.Current != null)
        {
            _gameController =
                BaseGameController.Current;

            _gameControllerWasEnabled =
                _gameController.enabled;

            _gameController.enabled = false;
        }

        _aiGameController = null;

        AIGameController aiController =
            FindAnyObjectByType<AIGameController>();

        if (aiController != null)
        {
            _aiGameController =
                aiController;

            _aiGameControllerWasEnabled =
                _aiGameController.enabled;

            _aiGameController.enabled = false;
        }
    }

    protected void RestoreGameController()
    {
        if (_gameController != null)
        {
            _gameController.enabled =
                _gameControllerWasEnabled;
        }

        _gameController = null;

        if (_aiGameController != null)
        {
            _aiGameController.enabled =
                _aiGameControllerWasEnabled;
        }

        _aiGameController = null;
    }
}
