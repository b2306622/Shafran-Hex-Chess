using System;
using UnityEngine;

[Serializable]
public sealed class AIPlayerDescriptor
{
    public string displayName;
    public AIPlayerKind kind;

    [Header("Minimax")]
    [Min(1)]
    public int minimaxDepth = 4;

    public bool minimaxHistory = true;
    public bool minimaxKiller = true;
    public bool minimaxTranspositionTable = true;
    public bool minimaxAspiration = true;

    [Header("Neural Evaluators")]
    public MonoBehaviour ppoPolicyEvaluator;
    public MonoBehaviour alphaZeroPolicyValueEvaluator;

    /// <summary>
    /// For MODEL_A/B/C this is the PPO-bootstrap evaluator:
    /// PPO policy prior + heuristic value + MCTS.
    /// </summary>
    public MonoBehaviour ppoBootstrapPolicyValueEvaluator;

    [Header("AlphaZero")]
    [Min(1)]
    public int alphaZeroSimulations = 100;
    [Min(1)]
    public int alphaZeroMaxDepth = 64;
    [Min(0.01f)]
    public float alphaZeroCpuct = 1.5f;

    public bool IsPpo =>
        kind == AIPlayerKind.PPO_BASE ||
        kind == AIPlayerKind.PPO_MINIMAX_D1 ||
        kind == AIPlayerKind.PPO_MINIMAX_D2;

    public bool IsAlphaZero =>
        kind == AIPlayerKind.ALPHAZERO_BASE ||
        kind == AIPlayerKind.ALPHAZERO_FROM_PPO_D1 ||
        kind == AIPlayerKind.ALPHAZERO_FROM_PPO_D2 ||
        kind == AIPlayerKind.MODEL_A_PRIME ||
        kind == AIPlayerKind.MODEL_B_PRIME ||
        kind == AIPlayerKind.MODEL_C_PRIME;
}
