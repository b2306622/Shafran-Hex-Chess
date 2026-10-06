using System;
using System.Collections.Generic;
using UnityEngine;

public class HeuristicPolicyValueEvaluator : MonoBehaviour, IPolicyValueEvaluator
{
    [Header("Prior")]
    [SerializeField]
    private float temperature = 1.50f;

    [SerializeField]
    private float captureWeight = 1.65f;

    [SerializeField]
    private float promotionWeight = 2.85f;

    [SerializeField]
    private float centerWeight = 0.18f;

    [SerializeField]
    private float endgameCaptureBonus = 1.55f;

    [SerializeField]
    private float endgameCenterDamping = 0.45f;

    [SerializeField]
    private float endgamePressureWeight = 1.05f;

    [SerializeField]
    private float checkMoveWeight = 1.30f;

    [SerializeField]
    private float tacticalSafetyWeight = 1.05f;

    [Header("Value")]
    [SerializeField]
    private float valueScale = 12.0f;

    private MoveGenerator _moveGenerator;

    private void Awake()
    {
        _moveGenerator = new MoveGenerator();
    }

    /// <summary>
    /// Fast value-only path used by PPO bootstrap MCTS.
    /// It deliberately skips the expensive per-legal-move heuristic prior
    /// calculation (which clones the position and checks attacks for every move).
    /// PPO already supplies the policy prior; this method only provides a
    /// lightweight material/terminal value for MCTS backup.
    /// </summary>
    public float EvaluateValueOnly(
        GameState state,
        EPlayer perspective,
        IReadOnlyList<StateMove> legalMoves)
    {
        if (state == null)
            return 0f;

        if (HexChessRules.TryGetMaterialTerminalResult(
                state,
                out EPlayer? materialWinner,
                out bool materialDraw))
        {
            if (materialDraw)
                return 0f;

            return Mathf.Clamp(
                materialWinner == perspective ? 1f : -1f,
                -1f,
                1f);
        }

        if (legalMoves == null || legalMoves.Count == 0)
        {
            return Mathf.Clamp(
                GetTerminalAwareValue(state, perspective),
                -1f,
                1f);
        }

        return Mathf.Clamp(
            GetMaterialValue(state, perspective),
            -1f,
            1f);
    }

    public PolicyValueOutput Evaluate(
        GameState state,
        EPlayer perspective,
        IReadOnlyList<StateMove> legalMoves)
    {
        float[] policy = new float[AlphaZeroActionEncoder.ActionCount];

        if (state == null)
            return new PolicyValueOutput(policy, 0f);

        if (HexChessRules.TryGetMaterialTerminalResult(
                state,
                out EPlayer? materialWinner,
                out bool materialDraw))
        {
            float terminalValue =
                materialDraw
                    ? 0f
                    : materialWinner == perspective
                        ? 1f
                        : -1f;

            return new PolicyValueOutput(
                policy,
                terminalValue
            );
        }

        if (legalMoves == null || legalMoves.Count == 0)
            return new PolicyValueOutput(policy, GetTerminalAwareValue(state, perspective));

        float openingPhase;
        float middlegamePhase;
        float endgamePhase;

        HexChessRules.GetStrategicPhase(
            state,
            perspective,
            out openingPhase,
            out middlegamePhase,
            out endgamePhase
        );

        float effectiveCaptureWeight =
            openingPhase * captureWeight * 1.00f +
            middlegamePhase * captureWeight * 1.25f +
            endgamePhase * captureWeight * 1.40f;

        float effectivePromotionWeight =
            openingPhase * promotionWeight * 0.90f +
            middlegamePhase * promotionWeight * 1.10f +
            endgamePhase * promotionWeight * 1.35f;

        float effectiveCenterWeight =
            openingPhase * centerWeight * 1.10f +
            middlegamePhase * centerWeight +
            endgamePhase * centerWeight * endgameCenterDamping;

        float effectivePressureWeight =
            openingPhase * endgamePressureWeight * 0.35f +
            middlegamePhase * endgamePressureWeight * 1.15f +
            endgamePhase * endgamePressureWeight * 1.45f;

        float effectiveCheckWeight =
            openingPhase * checkMoveWeight * 0.35f +
            middlegamePhase * checkMoveWeight * 1.20f +
            endgamePhase * checkMoveWeight * 1.10f;

        float materialLead =
            HexChessRules.CalculateNonKingMaterial(
                state,
                perspective
            ) -
            HexChessRules.CalculateNonKingMaterial(
                state,
                HexChessRules.GetOpponent(perspective)
            );

        float leadGate =
            Mathf.InverseLerp(
                2.5f,
                6.5f,
                materialLead
            );

        float conversionPrior =
            leadGate *
            (0.30f * middlegamePhase +
             0.65f * endgamePhase);

        float[] scores = new float[legalMoves.Count];
        float maxScore = float.NegativeInfinity;

        for (int i = 0; i < legalMoves.Count; i++)
        {
            StateMove move = legalMoves[i];
            float score = 0f;

            if (move.IsCapture)
            {
                score +=
                    effectiveCaptureWeight *
                    Mathf.Max(0.1f, move.CapturedPiece.Value / 100f);

                if (endgamePhase > 0f)
                {
                    score +=
                        endgameCaptureBonus *
                        endgamePhase;
                }
            }

            if (move.IsPromotion)
            {
                score +=
                    effectivePromotionWeight;
            }

            if (conversionPrior > 0f &&
                move.IsCapture)
            {
                score +=
                    conversionPrior *
                    Mathf.Max(
                        0.25f,
                        move.CapturedPiece.Value / 1000f
                    );
            }

            score +=
                effectiveCenterWeight *
                GetCenterHeuristic(move.To);

            score +=
                effectivePressureWeight *
                GetMoveKingPressureHeuristic(
                    state,
                    move,
                    perspective
                );

            GameState child =
                state.Clone();

            _moveGenerator.MakeMove(
                child,
                move
            );

            EPlayer opponent =
                HexChessRules.GetOpponent(
                    perspective
                );

            if (_moveGenerator.IsInCheck(
                    child,
                    opponent))
            {
                score +=
                    effectiveCheckWeight;
            }

            // Avoid simple tactical blunders: if the moved piece lands on
            // an attacked square, reduce its policy prior unless the move
            // has just captured something valuable.
            if (move.MovingPiece.Type != StatePieceType.KING &&
                _moveGenerator.IsSquareAttacked(
                    child,
                    move.To.X,
                    move.To.Y,
                    opponent))
            {
                float victimValue =
                    move.IsCapture
                        ? move.CapturedPiece.Value / 1000f
                        : 0f;

                score -=
                    tacticalSafetyWeight *
                    Mathf.Max(0f, 1.0f - victimValue * 0.20f);
            }

            scores[i] = score;
            maxScore = Mathf.Max(maxScore, score);
        }

        float sum = 0f;
        float safeTemperature = Mathf.Max(0.05f, temperature);

        for (int i = 0; i < legalMoves.Count; i++)
        {
            float probability =
                Mathf.Exp((scores[i] - maxScore) / safeTemperature);

            int action = AlphaZeroActionEncoder.Encode(legalMoves[i]);
            if (action < 0)
                continue;

            policy[action] = probability;
            sum += probability;
        }

        if (sum <= 1e-8f)
        {
            float uniform = 1f / legalMoves.Count;
            for (int i = 0; i < legalMoves.Count; i++)
            {
                int action = AlphaZeroActionEncoder.Encode(legalMoves[i]);
                if (action >= 0)
                    policy[action] = uniform;
            }
        }
        else
        {
            for (int i = 0; i < policy.Length; i++)
                policy[i] /= sum;
        }

        float value = GetMaterialValue(state, perspective);
        return new PolicyValueOutput(policy, value);
    }

    private float GetTerminalAwareValue(GameState state, EPlayer perspective)
    {
        List<StateMove> moves = _moveGenerator.GenerateLegalMoves(
            state,
            state.SideToMove
        );

        if (moves.Count != 0)
            return GetMaterialValue(state, perspective);

        bool inCheck = _moveGenerator.IsInCheck(state, state.SideToMove);

        if (!inCheck)
            return 0f;

        return state.SideToMove == perspective ? -1f : 1f;
    }


    private float GetEndgamePhase(
        GameState state,
        EPlayer perspective)
    {
        float opening;
        float middlegame;
        float endgame;

        HexChessRules.GetStrategicPhase(
            state,
            perspective,
            out opening,
            out middlegame,
            out endgame
        );

        return endgame;
    }

    private float GetMoveKingPressureHeuristic(
        GameState state,
        StateMove move,
        EPlayer perspective)
    {
        EPlayer opponent =
            HexChessRules.GetOpponent(perspective);

        Vector2Int opponentKing = FindKing(
            state,
            opponent
        );

        if (opponentKing.x < 0)
            return 0f;

        Vector2Int destination =
            new Vector2Int(
                move.To.X,
                move.To.Y
            );

        int distance =
            Mathf.Abs(destination.x - opponentKing.x) +
            Mathf.Abs(destination.y - opponentKing.y);

        float pressure =
            1f / (1f + distance);

        // Captures are already rewarded separately. Here we add a modest
        // bonus for moving toward/constraining the opposing king.
        if (distance <= 2)
            pressure += 0.25f;

        return Mathf.Clamp01(pressure);
    }

    private Vector2Int FindKing(
        GameState state,
        EPlayer player)
    {
        foreach (Vector2Int position in HexChessRules.CellCoordinates)
        {
            StatePiece? piece =
                state.GetPiece(
                    position.x,
                    position.y
                );

            if (piece.HasValue &&
                piece.Value.IsValid &&
                piece.Value.Player == player &&
                piece.Value.Type == StatePieceType.KING)
            {
                return position;
            }
        }

        return new Vector2Int(-1, -1);
    }

    private float GetMaterialValue(GameState state, EPlayer perspective)
    {
        float score = 0f;

        foreach (Vector2Int position in HexChessRules.CellCoordinates)
        {
            StatePiece? piece = state.GetPiece(position.x, position.y);
            if (!piece.HasValue)
                continue;

            float value = piece.Value.Value / 100f;
            score += piece.Value.Player == perspective ? value : -value;
        }

        float normalized = score / Mathf.Max(1f, valueScale);
        return (float)Math.Tanh(normalized);
    }

    private float GetCenterHeuristic(CLocation location)
    {
        float dx = location.X - 4f;
        float dy = location.Y - 5.5f;
        return 1f / (1f + dx * dx + dy * dy);
    }
}
