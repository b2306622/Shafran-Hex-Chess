using System;
using UnityEngine;

[Serializable]
public class AlphaZeroConfig
{
    [Header("MCTS")]
    [Min(1)]
    public int simulations = 200;

    [Min(1)]
    public int maxDepth = 64;

    [Min(0.01f)]
    public float cpuct = 1.50f;

    [Min(0.01f)]
    public float policyTemperature = 1.0f;

    [Range(0f, 1f)]
    public float rootDirichletEpsilon = 0.0f;

    [Min(0.01f)]
    public float rootDirichletAlpha = 0.30f;

    [Header("Inference")]
    public bool useGpu = false;

    [Header("Search")]
    public bool reuseRoot = false;

    [Header("Draw-Aware Search")]
    public bool enableRepetitionAwareness = true;

    [Min(2)]
    public int repetitionCutoffOccurrences = 3;

    public bool enableNoProgressAwareness = true;

    // The real game clock can already be above the search threshold.
    // Do not let that make every root child an immediate draw cutoff.
    // When false, no-progress is counted from the current MCTS root.
    public bool inheritGameNoProgressIntoSearch = false;

    [Min(1)]
    public int noProgressPlyThreshold = 100;

    [Range(-1f, 1f)]
    public float repetitionCutoffValue = 0f;

    [Range(-1f, 1f)]
    public float noProgressCutoffValue = 0f;

    [Header("Diagnostics")]
    public bool enableDiagnostics = true;

    [Min(1)]
    public int diagnosticsCandidateCount = 5;

    public int SafeSimulations => Mathf.Max(1, simulations);
    public int SafeMaxDepth => Mathf.Max(1, maxDepth);
    public float SafeCpuct => Mathf.Max(0.01f, cpuct);
    public float SafeTemperature => Mathf.Max(0.01f, policyTemperature);
    public bool EnableRepetitionAwareness => enableRepetitionAwareness;
    public int SafeRepetitionCutoffOccurrences =>
        Mathf.Max(2, repetitionCutoffOccurrences);
    public bool EnableNoProgressAwareness => enableNoProgressAwareness;
    public bool InheritGameNoProgressIntoSearch =>
        inheritGameNoProgressIntoSearch;
    public int SafeNoProgressPlyThreshold =>
        Mathf.Clamp(noProgressPlyThreshold, 1, SafeMaxDepth);
    public float SafeRepetitionCutoffValue =>
        Mathf.Clamp(repetitionCutoffValue, -1f, 1f);
    public float SafeNoProgressCutoffValue =>
        Mathf.Clamp(noProgressCutoffValue, -1f, 1f);
    public bool EnableDiagnostics => enableDiagnostics;
    public int SafeDiagnosticsCandidateCount =>
        Mathf.Clamp(diagnosticsCandidateCount, 1, 10);
}
