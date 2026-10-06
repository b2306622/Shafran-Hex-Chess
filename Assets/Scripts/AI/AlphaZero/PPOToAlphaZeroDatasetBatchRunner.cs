using System.Collections;
using UnityEngine;

/// <summary>
/// Optional helper for generating the three bootstrap datasets sequentially.
/// Each branch uses an existing trained PPO ONNX evaluator and the same
/// lightweight MCTS exporter. It runs one branch at a time to avoid holding
/// multiple active Sentis searches on the frame loop.
/// </summary>
public sealed class PPOToAlphaZeroDatasetBatchRunner : MonoBehaviour
{
    [Header("Exporter")]
    [SerializeField]
    private AlphaZeroSelfPlayExporter exporter;

    [Header("PPO Bootstrap Evaluators")]
    [SerializeField]
    private ShafranPPOBootstrapPolicyValueEvaluator modelA;

    [SerializeField]
    private ShafranPPOBootstrapPolicyValueEvaluator modelB;

    [SerializeField]
    private ShafranPPOBootstrapPolicyValueEvaluator modelC;

    [Header("Dataset Names")]
    [SerializeField]
    private string fileA = "AlphaZeroSamples_A.ndjson";

    [SerializeField]
    private string fileB = "AlphaZeroSamples_B.ndjson";

    [SerializeField]
    private string fileC = "AlphaZeroSamples_C.ndjson";

    [Header("Lite Search")]
    [Min(1)]
    [SerializeField]
    private int simulations = 16;

    [Min(1)]
    [SerializeField]
    private int maxDepth = 16;

    [SerializeField]
    private bool rootNoise = false;

    [ContextMenu("Generate A -> B -> C Datasets")]
    public void GenerateABC()
    {
        StartCoroutine(GenerateSequentially());
    }

    private IEnumerator GenerateSequentially()
    {
        if (exporter == null)
        {
            Debug.LogError("[PPO->AlphaZero] Exporter is NULL.", this);
            yield break;
        }

        if (modelA == null || modelB == null || modelC == null)
        {
            Debug.LogError(
                "[PPO->AlphaZero] Assign modelA/modelB/modelC bootstrap evaluators.",
                this);
            yield break;
        }

        exporter.ConfigureSearch(simulations, maxDepth, rootNoise);

        yield return GenerateOne("A", modelA, fileA);
        yield return GenerateOne("B", modelB, fileB);
        yield return GenerateOne("C", modelC, fileC);

        Debug.Log("[PPO->AlphaZero] A/B/C dataset generation finished.", this);
    }

    private IEnumerator GenerateOne(
        string id,
        ShafranPPOBootstrapPolicyValueEvaluator evaluator,
        string fileName)
    {
        exporter.ConfigureEvaluator(evaluator);
        exporter.ConfigureOutputFile(fileName);

        Debug.Log(
            $"[PPO->AlphaZero] START Model {id} | " +
            $"File={fileName} | Sims={simulations} | Depth={maxDepth}",
            this);

        exporter.GenerateDataset();

        // GenerateDataset is intentionally synchronous. Yield once so Unity can
        // flush logs and render state before the next branch starts.
        yield return null;
    }
}
