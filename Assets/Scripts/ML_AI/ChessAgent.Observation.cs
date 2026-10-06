using Unity.MLAgents.Sensors;
using UnityEngine;

public partial class ChessAgent
{
    public override void CollectObservations(
        VectorSensor sensor)
    {
        GameState state =
            _cachedGameState != null &&
            _legalCacheValid &&
            BaseGameController.Current != null &&
            BaseGameController.Current.CurrentPlayer == agentPlayer
                ? _cachedGameState
                : GameState.FromUnityBoard();

        AddBoardObservations(
            sensor,
            state
        );
        AddLastMoveObservations(sensor);
        AddTurnObservation(sensor);
        AddAgentSideObservation(sensor);
    }

    private void AddBoardObservations(
        VectorSensor sensor,
        GameState state
    )
    {
        if (_observationSafetyBuffer == null ||
            _observationSafetyBuffer.Length < CELL_COUNT)
        {
            return;
        }

        float[] safetyMap = _observationSafetyBuffer;

        if (state != null)
        {
            ShafranTacticalState.FillSquareSafetyFastMap(
                state,
                agentPlayer,
                safetyMap
            );
        }

        int positionIndex = 0;

        foreach (Vector2Int position in HexChessRules.CellCoordinates)
        {
            Cell cell =
                ChessBoard.Current != null
                    ? ChessBoard.Current.GetCell(
                        position.x,
                        position.y)
                    : null;

            BasePiece piece =
                cell != null
                    ? cell.CurrentPiece
                    : null;

            AddPieceStateObservation(
                sensor,
                piece
            );

            sensor.AddObservation(
                positionIndex < safetyMap.Length
                    ? safetyMap[positionIndex]
                    : 0f
            );

            positionIndex++;
        }
    }

    private void AddPieceStateObservation(
        VectorSensor sensor,
        BasePiece piece)
    {
        int stateIndex =
            GetRelativePieceStateIndex(piece);

        for (int i = 0; i < PIECE_STATE_COUNT; i++)
        {
            float value =
                i == stateIndex
                    ? 1f
                    : 0f;

            // Pack HasMoved into the same channel without increasing
            // observation size. This is deliberately simple so the policy
            // can still distinguish a fresh piece from a developed one.
            if (i == stateIndex &&
                piece != null &&
                piece.HasMoved)
            {
                value = 0.5f;
            }

            sensor.AddObservation(value);
        }
    }

    private int GetRelativePieceStateIndex(
        BasePiece piece)
    {
        if (piece == null)
        {
            return 0;
        }

        int baseIndex;

        if (piece is P_Pawn)
            baseIndex = 1;
        else if (piece is P_Knight)
            baseIndex = 2;
        else if (piece is P_Bishop)
            baseIndex = 3;
        else if (piece is P_Rook)
            baseIndex = 4;
        else if (piece is P_Queen)
            baseIndex = 5;
        else if (piece is P_King)
            baseIndex = 6;
        else
            return 0;

        return piece.Player == agentPlayer
            ? baseIndex
            : baseIndex + 6;
    }

    private void AddLastMoveObservations(
        VectorSensor sensor)
    {
        Move lastMove =
            BaseGameController.Current != null
                ? BaseGameController.Current.LastMove
                : null;

        if (lastMove == null)
        {
            sensor.AddObservation(-1f);
            sensor.AddObservation(-1f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            return;
        }

        int fromIndex =
            HexChessRules.GetCellIndex(
                lastMove.From.X,
                lastMove.From.Y
            );

        int toIndex =
            HexChessRules.GetCellIndex(
                lastMove.To.X,
                lastMove.To.Y
            );

        float maxIndex =
            Mathf.Max(
                1f,
                CELL_COUNT - 1
            );

        sensor.AddObservation(
            fromIndex / maxIndex
        );

        sensor.AddObservation(
            toIndex / maxIndex
        );

        bool multiStepPawn =
            lastMove.MovedPiece is P_Pawn &&
            Mathf.Abs(
                lastMove.From.Y -
                lastMove.To.Y
            ) > 1;

        sensor.AddObservation(
            multiStepPawn ? 1f : 0f
        );

        sensor.AddObservation(
            lastMove.CapturedPiece != null
                ? 1f
                : 0f
        );
    }

    private void AddTurnObservation(
        VectorSensor sensor)
    {
        bool isMyTurn =
            BaseGameController.Current != null &&
            BaseGameController.Current.CurrentPlayer == agentPlayer;

        sensor.AddObservation(
            isMyTurn ? 1f : 0f
        );
    }

    private void AddAgentSideObservation(
        VectorSensor sensor)
    {
        sensor.AddObservation(
            agentPlayer == EPlayer.WHITE
                ? 1f
                : -1f
        );
    }
}
