using UnityEngine;

public partial class ChessAgent
{
    private void ApplyInvalidMoveReward()
    {
        AddShapingReward(invalidMoveReward);
    }

    private void ApplyLegalMoveReward()
    {
        if (!Mathf.Approximately(legalMoveReward, 0f))
            AddShapingReward(legalMoveReward);
    }

    private void ApplyCaptureReward(StatePiece capturedPiece)
    {
        if (!capturedPiece.IsValid)
            return;

        float normalizedValue = Mathf.Clamp01(
            capturedPiece.Value / 1000f
        );

        AddShapingReward(
            normalizedValue * captureRewardScale
        );
    }

    // Terminal rewards are additive so previous shaping rewards are preserved.
    private void AddTrackedTerminalReward(float reward)
    {
        AddReward(reward);
        _episodeRewardAccumulator += reward;
    }

    private void ApplyWinReward()
    {
        AddTrackedTerminalReward(
            CalculateWinReward()
        );
    }

    private void ApplyLossReward()
    {
        AddTrackedTerminalReward(lossReward);
    }

    private void ApplyDrawReward()
    {
        BaseGameController controller =
            BaseGameController.Current;

        EDrawReason reason =
            controller != null &&
            controller.DrawReason.HasValue
                ? controller.DrawReason.Value
                : EDrawReason.Normal;

        // Keep ordinary/non-training draws neutral. Repetition gets a
        // dedicated penalty because the objective is to finish the game
        // rather than repeatedly cycle between positions.
        float reward =
            reason == EDrawReason.Repetition
                ? repetitionDrawReward
                : reason == EDrawReason.MaxPlies
                    ? maxPlyDrawReward
                    : normalDrawReward;

        // If this agent was materially ahead when a draw occurred, make the
        // draw more costly. This targets the bad behavior we care about:
        // throwing away a winning advantage through repetition, stalemate,
        // insufficient material, or a long non-progress game. The side that
        // is behind is not hit by this extra conversion penalty.
        GameState state =
            GameState.FromUnityBoard();

        if (state != null)
        {
            EPlayer opponent =
                HexChessRules.GetOpponent(
                    agentPlayer
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

            if (materialLead >= drawMaterialLeadThreshold)
            {
                // Fixed extra penalty for giving up a clear lead, plus a
                // smooth lead-dependent component capped so it cannot dwarf
                // the terminal win/loss signal.
                reward +=
                    drawWhileAheadPenalty;

                float excessLead =
                    materialLead -
                    drawMaterialLeadThreshold;

                float leadPenalty =
                    Mathf.Max(
                        drawMaterialLeadPenaltyCap,
                        excessLead *
                        drawMaterialLeadPenaltyPerPoint
                    );

                reward += leadPenalty;
            }
        }

        AddTrackedTerminalReward(reward);
    }
}
