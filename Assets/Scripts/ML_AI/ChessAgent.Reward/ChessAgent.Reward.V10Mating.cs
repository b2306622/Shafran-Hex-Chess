using System.Collections.Generic;
using UnityEngine;

public partial class ChessAgent
{
    // =========================================================
    // V10 MATE / FORCING REWARD
    // =========================================================
    //
    // Design goal:
    //   Do NOT teach the agent to run away from the enemy King.
    //   Teach it to make the enemy King lose legal squares, then
    //   convert that restriction into check and checkmate.
    //
    // These rewards are shaping only. Checkmate remains the terminal
    // objective and receives the much larger terminal reward.
    // =========================================================

    private float CalculateV10MatingNetReward(
        GameState before,
        GameState after)
    {
        if (before == null ||
            after == null ||
            _moveGenerator == null)
        {
            return 0f;
        }

        EPlayer opponent =
            HexChessRules.GetOpponent(agentPlayer);

        int beforeMoves =
            CountKingLegalMoves(
                before,
                opponent
            );

        int afterMoves =
            CountKingLegalMoves(
                after,
                opponent
            );

        int reduction =
            Mathf.Max(
                0,
                beforeMoves - afterMoves
            );

        bool givesCheck =
            _moveGenerator.IsInCheck(
                after,
                opponent
            );

        float reward = 0f;

        // -----------------------------------------------------
        // KING MOBILITY REDUCTION
        // -----------------------------------------------------
        if (reduction > 0)
        {
            if (reduction == 1)
            {
                reward += v10MobilityReductionSmall;
            }
            else if (reduction == 2)
            {
                reward += v10MobilityReductionMedium;
            }
            else if (reduction == 3)
            {
                reward += v10MobilityReductionStrong;
            }
            else
            {
                // Four or more lost escape squares is a critical collapse.
                reward += v10MobilityReductionCritical;
            }
        }

        // The final escape squares matter disproportionately.
        if (afterMoves <= 2)
        {
            reward +=
                v10MobilityReductionMedium;
        }

        if (afterMoves <= 1)
        {
            reward +=
                v10KingOneEscapeBonus;
        }

        // -----------------------------------------------------
        // CHECK
        // -----------------------------------------------------
        if (givesCheck)
        {
            reward += v10CheckBonus;
        }

        // -----------------------------------------------------
        // MATE-IN-2 SIGNAL
        //
        // If, after this move, the opponent still has legal moves
        // but we have a legal continuation that checkmates, this move
        // has created an immediate mating threat.
        //
        // We only run the deeper test in tactical/endgame positions to
        // keep PPO throughput reasonable.
        // -----------------------------------------------------
        int opponentNonKing =
            CountNonKingPiecesForReward(
                after,
                opponent
            );

        if (afterMoves > 0 &&
            afterMoves <= 4 &&
            opponentNonKing <= 2)
        {
            if (HasForcedMateInTwoV9(
                    after,
                    opponent))
            {
                reward +=
                    v10MateThreatBonus;
            }
        }

        // Once the opponent has only a King, the network must stop
        // thinking about "more material" and focus almost entirely on
        // restricting the King.
        if (opponentNonKing == 0 &&
            afterMoves <= 2)
        {
            reward +=
                0.020f;
        }

        return Mathf.Clamp(
            reward,
            -0.05f,
            0.12f
        );
    }

    // =========================================================
    // V10 FORCING MOVE
    // =========================================================
    //
    // Reward combinations that force the opponent to respond:
    //   - check + mobility reduction
    //   - valuable capture + mobility reduction
    //   - large mobility collapse
    //
    // A quiet move gets no bonus from this function.
    // =========================================================

    private float CalculateV10ForcingMoveReward(
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

        EPlayer opponent =
            HexChessRules.GetOpponent(agentPlayer);

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
            Mathf.Max(
                0,
                beforeKingMoves - afterKingMoves
            );

        bool check =
            _moveGenerator.IsInCheck(
                after,
                opponent
            );

        float reward = 0f;

        // A check that also removes escape squares is much more
        // meaningful than a random check.
        if (check && reduction >= 1)
        {
            reward += 0.020f;
        }

        if (reduction >= 2)
        {
            reward += 0.015f;
        }

        // A high-value capture that simultaneously restricts the King
        // is a forcing tactical move, not merely a material gain.
        if (selectedMove.IsCapture &&
            selectedMove.CapturedPiece.IsValid)
        {
            float capturedValue =
                GetMaterialValue(
                    selectedMove.CapturedPiece
                );

            if (capturedValue >= 7.0f &&
                reduction >= 1)
            {
                reward +=
                    v10ForcingCaptureBonus;
            }
        }

        return Mathf.Clamp(
            reward,
            -0.05f,
            0.06f
        );
    }

    // =========================================================
    // IMMEDIATE MATE THREAT DETECTOR
    // =========================================================

    private bool HasImmediateMateThreat(
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

        if (opponentReplies.Count <= 0)
        {
            return false;
        }

        List<StateMove> ourMoves =
            _moveGenerator.GenerateLegalMoves(
                state,
                agentPlayer
            );

        // Safety cap for PPO throughput. The strongest tactical moves
        // are normally found well before this limit because legal moves
        // are generated deterministically.
        int limit =
            Mathf.Min(
                ourMoves.Count,
                96
            );

        for (int i = 0; i < limit; i++)
        {
            GameState candidate =
                state.Clone();

            _moveGenerator.MakeMove(
                candidate,
                ourMoves[i]
            );

            if (!_moveGenerator.IsInCheck(
                    candidate,
                    opponent))
            {
                continue;
            }

            List<StateMove> repliesAfterCheck =
                _moveGenerator.GenerateLegalMoves(
                    candidate,
                    opponent
                );

            if (repliesAfterCheck.Count == 0)
            {
                return true;
            }
        }

        return false;
    }

    // =========================================================
    // SHAFRAN THREE-COLOR BISHOP ACTIVITY
    // =========================================================
    //
    // The board has three cell colors. This is a small positional
    // signal only; it must never outweigh tactical/checkmate signals.
    // =========================================================

    private float CalculateV10BishopColorReward(
        GameState before,
        GameState after)
    {
        int beforeCoverage =
            CountBishopColorCoverage(
                before,
                agentPlayer
            );

        int afterCoverage =
            CountBishopColorCoverage(
                after,
                agentPlayer
            );

        if (afterCoverage <= beforeCoverage)
        {
            return 0f;
        }

        float reward =
            (afterCoverage - beforeCoverage) *
            v10BishopColorBonus;

        if (afterCoverage == 3 &&
            beforeCoverage < 3)
        {
            reward +=
                v10ThreeColorBishopBonus;
        }

        return Mathf.Clamp(
            reward,
            0f,
            0.04f
        );
    }

    private int CountBishopColorCoverage(
        GameState state,
        EPlayer player)
    {
        if (state == null)
        {
            return 0;
        }

        bool[] colors =
        {
            false,
            false,
            false
        };

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
                piece.Value.Player != player ||
                piece.Value.Type != StatePieceType.BISHOP)
            {
                continue;
            }

            int color =
                HexChessRules.GetCellColorIndex(
                    position.x,
                    position.y
                );

            if (color >= 0 &&
                color < 3)
            {
                colors[color] = true;
            }
        }

        int count = 0;

        for (int i = 0; i < colors.Length; i++)
        {
            if (colors[i])
            {
                count++;
            }
        }

        return count;
    }
}
