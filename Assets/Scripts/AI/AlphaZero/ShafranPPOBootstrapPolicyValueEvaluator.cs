using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Branch-specific PPO-guided MCTS evaluator for Model A/B/C.
///
/// This is the bootstrap stage before a true neural dual-head AlphaZero model:
/// PPO provides the policy prior, while the Shafran heuristic evaluator
/// provides the initial value estimate.
/// </summary>
public sealed class ShafranPPOBootstrapPolicyValueEvaluator : MonoBehaviour, IPolicyValueEvaluator
{
    [Header("Branch Identity")]
    [SerializeField]
    private string modelId = "MODEL_A";

    [Header("Evaluators")]
    [SerializeField]
    private SentisPpoPolicyEvaluator ppoPolicyEvaluator;

    [SerializeField]
    private HeuristicPolicyValueEvaluator heuristicValueEvaluator;

    [Header("Policy Blend")]
    [Range(0f, 1f)]
    [SerializeField]
    private float ppoPolicyWeight = 1.00f;

    [Range(0f, 1f)]
    [SerializeField]
    private float heuristicPolicyWeight = 0.00f;

    [Header("Reliability")]
    [Tooltip("Recommended ON for tournaments. A failed PPO inference throws instead of silently turning this player into a uniform-policy MCTS.")]
    [SerializeField]
    private bool failOnPpoInferenceFailure = true;

    public string ModelId => modelId;
    public SentisPpoPolicyEvaluator PpoPolicyEvaluator => ppoPolicyEvaluator;
    public HeuristicPolicyValueEvaluator HeuristicValueEvaluator => heuristicValueEvaluator;

    public bool IsReady =>
        ppoPolicyEvaluator != null &&
        ppoPolicyEvaluator.IsReady &&
        heuristicValueEvaluator != null;

    public bool LastPpoInferenceSucceeded =>
        ppoPolicyEvaluator != null &&
        ppoPolicyEvaluator.LastInferenceSucceeded;

    public string LastPpoInferenceError =>
        ppoPolicyEvaluator != null
            ? ppoPolicyEvaluator.LastInferenceError
            : "PPO evaluator is null.";

    public bool ValidatePolicyInference(
        GameState state,
        EPlayer perspective,
        IReadOnlyList<StateMove> legalMoves,
        out string message)
    {
        message = null;

        if (ppoPolicyEvaluator == null)
        {
            message = "PPO evaluator is null.";
            return false;
        }

        if (!ppoPolicyEvaluator.IsReady)
        {
            message =
                $"PPO evaluator is not ready. Asset={ppoPolicyEvaluator.ModelAssetName}, " +
                $"Path={ppoPolicyEvaluator.ResourcesModelPath}";
            return false;
        }

        float[] policy = ppoPolicyEvaluator.EvaluatePolicy(
            state,
            perspective,
            legalMoves);

        if (!ppoPolicyEvaluator.LastInferenceSucceeded)
        {
            message =
                $"PPO inference failed: {ppoPolicyEvaluator.LastInferenceError}";
            return false;
        }

        if (policy == null || policy.Length != AlphaZeroActionEncoder.ActionCount)
        {
            message =
                $"PPO policy length invalid: {policy?.Length ?? 0}. " +
                $"Expected {AlphaZeroActionEncoder.ActionCount}.";
            return false;
        }

        message =
            $"OK | Output={ppoPolicyEvaluator.LastRawOutputLength} | " +
            $"ResolvedOutput={ppoPolicyEvaluator.ResolvedPolicyOutputName} | " +
            $"LegalMoves={legalMoves?.Count ?? 0}";
        return true;
    }

    public PolicyValueOutput Evaluate(
        GameState state,
        EPlayer perspective,
        IReadOnlyList<StateMove> legalMoves)
    {
        int actionCount = AlphaZeroActionEncoder.ActionCount;
        float[] resultPolicy = new float[actionCount];

        if (state == null || legalMoves == null || legalMoves.Count == 0)
            return new PolicyValueOutput(resultPolicy, 0f);

        float ppoWeight = Mathf.Max(0f, ppoPolicyWeight);
        float heuristicWeight = Mathf.Max(0f, heuristicPolicyWeight);
        float totalWeight = ppoWeight + heuristicWeight;

        if (totalWeight <= 1e-6f)
        {
            ppoWeight = 1f;
            heuristicWeight = 0f;
            totalWeight = 1f;
        }

        if (ppoPolicyEvaluator == null)
        {
            if (failOnPpoInferenceFailure)
                throw new InvalidOperationException(
                    $"[{modelId}] PPO evaluator is null.");

            return new PolicyValueOutput(
                BuildUniformPolicy(legalMoves),
                0f);
        }

        float[] ppoPolicy =
            ppoPolicyEvaluator.EvaluatePolicy(
                state,
                perspective,
                legalMoves);

        if (!ppoPolicyEvaluator.LastInferenceSucceeded)
        {
            if (failOnPpoInferenceFailure)
            {
                throw new InvalidOperationException(
                    $"[{modelId}] PPO inference failed. " +
                    ppoPolicyEvaluator.LastInferenceError);
            }
        }

        float[] heuristicPolicy = null;
        float heuristicValue = 0f;

        if (heuristicValueEvaluator != null)
        {
            // The PPO bootstrap already provides the policy prior.
            // Do NOT build the expensive heuristic policy on every MCTS leaf
            // unless the user explicitly assigns a non-zero heuristic weight.
            if (heuristicWeight > 1e-6f)
            {
                PolicyValueOutput heuristicOutput =
                    heuristicValueEvaluator.Evaluate(
                        state,
                        perspective,
                        legalMoves);

                heuristicPolicy = heuristicOutput.policy;
                heuristicValue = heuristicOutput.value;
            }
            else
            {
                heuristicValue =
                    heuristicValueEvaluator.EvaluateValueOnly(
                        state,
                        perspective,
                        legalMoves);
            }
        }

        for (int i = 0; i < legalMoves.Count; i++)
        {
            int action =
                AlphaZeroActionEncoder.Encode(
                    legalMoves[i]);

            if (action < 0 || action >= actionCount)
                continue;

            float ppo =
                ppoPolicy != null && action < ppoPolicy.Length
                    ? Mathf.Max(0f, ppoPolicy[action])
                    : 0f;

            float heuristic =
                heuristicPolicy != null && action < heuristicPolicy.Length
                    ? Mathf.Max(0f, heuristicPolicy[action])
                    : 0f;

            resultPolicy[action] =
                (ppo * ppoWeight +
                 heuristic * heuristicWeight) /
                totalWeight;
        }

        NormalizeLegalPolicy(resultPolicy, legalMoves);

        return new PolicyValueOutput(
            resultPolicy,
            Mathf.Clamp(heuristicValue, -1f, 1f));
    }

    private static void NormalizeLegalPolicy(
        float[] policy,
        IReadOnlyList<StateMove> legalMoves)
    {
        float sum = 0f;

        for (int i = 0; i < legalMoves.Count; i++)
        {
            int action =
                AlphaZeroActionEncoder.Encode(
                    legalMoves[i]);

            if (action < 0 || action >= policy.Length)
                continue;

            sum += Mathf.Max(0f, policy[action]);
        }

        if (sum <= 1e-8f)
        {
            float uniform = 1f / Mathf.Max(1, legalMoves.Count);

            for (int i = 0; i < legalMoves.Count; i++)
            {
                int action =
                    AlphaZeroActionEncoder.Encode(
                        legalMoves[i]);

                if (action >= 0 && action < policy.Length)
                    policy[action] = uniform;
            }

            return;
        }

        for (int i = 0; i < legalMoves.Count; i++)
        {
            int action =
                AlphaZeroActionEncoder.Encode(
                    legalMoves[i]);

            if (action >= 0 && action < policy.Length)
                policy[action] /= sum;
        }
    }

    private static float[] BuildUniformPolicy(
        IReadOnlyList<StateMove> legalMoves)
    {
        float[] policy =
            new float[AlphaZeroActionEncoder.ActionCount];

        if (legalMoves == null || legalMoves.Count == 0)
            return policy;

        int valid = 0;
        for (int i = 0; i < legalMoves.Count; i++)
        {
            int action = AlphaZeroActionEncoder.Encode(legalMoves[i]);
            if (action >= 0 && action < policy.Length)
                valid++;
        }

        if (valid <= 0)
            return policy;

        float uniform = 1f / valid;

        for (int i = 0; i < legalMoves.Count; i++)
        {
            int action = AlphaZeroActionEncoder.Encode(legalMoves[i]);
            if (action >= 0 && action < policy.Length)
                policy[action] = uniform;
        }

        return policy;
    }
}
