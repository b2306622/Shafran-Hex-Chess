using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime validator for the three PPO ONNX branches.
///
/// A model is only considered valid when a REAL Sentis inference succeeds and
/// returns exactly 4900 policy values. Fallback policy arrays do not count.
/// </summary>
public sealed class ShafranPPOModelSetValidator : MonoBehaviour
{
    [SerializeField] private SentisPpoPolicyEvaluator modelA;
    [SerializeField] private SentisPpoPolicyEvaluator modelB;
    [SerializeField] private SentisPpoPolicyEvaluator modelC;
    [SerializeField] private bool logOnStart = true;

    private void Start()
    {
        if (logOnStart)
            ValidateAll();
    }

    [ContextMenu("Validate A / B / C PPO Models")]
    public void ValidateAll()
    {
        if (ChessBoard.Current == null)
        {
            Debug.LogError(
                "[PPO Model Validator] ChessBoard.Current is null. " +
                "Open the Shafran board scene before validating inference.",
                this);
            return;
        }

        GameState state = GameState.FromUnityBoard();
        if (state == null)
        {
            Debug.LogError("[PPO Model Validator] Could not capture GameState.", this);
            return;
        }

        MoveGenerator generator = new MoveGenerator();
        List<StateMove> legalMoves =
            generator.GenerateLegalMoves(state, state.SideToMove);

        if (legalMoves == null || legalMoves.Count == 0)
        {
            Debug.LogError(
                "[PPO Model Validator] Initial state has no legal moves.",
                this);
            return;
        }

        Validate("A", modelA, state, legalMoves);
        Validate("B", modelB, state, legalMoves);
        Validate("C", modelC, state, legalMoves);
        ValidateDistinctAssignments();
    }

    private static void Validate(
        string id,
        SentisPpoPolicyEvaluator evaluator,
        GameState state,
        IReadOnlyList<StateMove> legalMoves)
    {
        if (evaluator == null)
        {
            Debug.LogError(
                $"[PPO Model Validator] Model {id}: evaluator is NULL.");
            return;
        }

        Debug.Log(
            $"[PPO Model Validator] Model {id}: Asset='{evaluator.ModelAssetName}' | " +
            $"Path='{evaluator.ResourcesModelPath}' | Ready={evaluator.IsReady}\n" +
            evaluator.ModelContractSummary);

        if (!evaluator.IsReady)
        {
            Debug.LogError(
                $"[PPO Model Validator] Model {id}: evaluator is NOT READY.");
            return;
        }

        float[] policy = evaluator.EvaluatePolicy(
            state,
            state.SideToMove,
            legalMoves);

        bool realInference = evaluator.LastInferenceSucceeded;
        bool outputOk =
            realInference &&
            evaluator.LastRawOutputLength == SentisPpoPolicyEvaluator.ExpectedActionCount &&
            policy != null &&
            policy.Length == SentisPpoPolicyEvaluator.ExpectedActionCount;

        if (outputOk)
        {
            Debug.Log(
                $"[PPO Model Validator] Model {id}: READY | Inference=OK | " +
                $"RawOutput={evaluator.LastRawOutputLength} | " +
                $"Policy={policy.Length} | Expected={SentisPpoPolicyEvaluator.ExpectedActionCount} | " +
                $"LegalMoves={legalMoves.Count} | ResolvedOutput='{evaluator.ResolvedPolicyOutputName}'.");
        }
        else
        {
            Debug.LogError(
                $"[PPO Model Validator] Model {id}: INVALID | " +
                $"Inference={(realInference ? "OK" : "FAILED")} | " +
                $"RawOutput={evaluator.LastRawOutputLength} | " +
                $"PolicyLength={policy?.Length ?? 0} | " +
                $"Expected={SentisPpoPolicyEvaluator.ExpectedActionCount} | " +
                $"Error='{evaluator.LastInferenceError}'.\n" +
                "Do NOT run the A/B/C tournament until all three models pass this check.");
        }
    }

    private void ValidateDistinctAssignments()
    {
        WarnIfDuplicate("A", modelA, "B", modelB);
        WarnIfDuplicate("A", modelA, "C", modelC);
        WarnIfDuplicate("B", modelB, "C", modelC);
    }

    private static void WarnIfDuplicate(
        string idA,
        SentisPpoPolicyEvaluator a,
        string idB,
        SentisPpoPolicyEvaluator b)
    {
        if (a == null || b == null)
            return;

        bool sameAsset =
            !string.Equals(a.ModelAssetName, "<null>", StringComparison.Ordinal) &&
            string.Equals(a.ModelAssetName, b.ModelAssetName, StringComparison.Ordinal);

        bool bothUseNullAsset =
            string.Equals(a.ModelAssetName, "<null>", StringComparison.Ordinal) &&
            string.Equals(b.ModelAssetName, "<null>", StringComparison.Ordinal);

        bool samePath =
            bothUseNullAsset &&
            string.Equals(a.ResourcesModelPath, b.ResourcesModelPath, StringComparison.Ordinal);

        if (sameAsset || samePath)
        {
            Debug.LogWarning(
                $"[PPO Model Validator] Possible duplicate assignment: " +
                $"Model {idA} and Model {idB} both resolve to " +
                $"Asset='{a.ModelAssetName}' / Path='{a.ResourcesModelPath}'. " +
                "Make sure A/B/C really use three different ONNX files.");
        }
    }
}
