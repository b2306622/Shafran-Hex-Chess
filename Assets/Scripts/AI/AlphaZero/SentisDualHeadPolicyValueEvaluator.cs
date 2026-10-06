using System;
using System.Collections.Generic;
using Unity.InferenceEngine;
using UnityEngine;

/// <summary>
/// Neural policy + value evaluator for AlphaZero-style MCTS.
/// This evaluator is used after PPO/distillation training has produced a
/// dedicated dual-head AlphaZero network.
///
/// Expected ONNX contract:
///   input  : obs_0   [batch, 986]
///   output : policy  [batch, 4900]  (RAW LOGITS)
///   output : value   [batch, 1]     (RAW SCALAR; tanh applied here)
///
/// This component intentionally does NOT silently accept arbitrary ML-Agents
/// PPO ONNX files. The model must expose the dual-head contract above.
/// </summary>
public sealed class SentisDualHeadPolicyValueEvaluator
    : MonoBehaviour, IPolicyValueEvaluator
{
    [Header("Model")]
    [SerializeField]
    private ModelAsset modelAsset;

    [Tooltip("Optional Resources path without .onnx. Used when ModelAsset is not assigned in Inspector.")]
    [SerializeField]
    private string resourcesModelPath = "AI/ChessBrain_DualHead";

    [SerializeField]
    private string inputName = "obs_0";

    [Header("Outputs")]
    [SerializeField]
    private string policyOutputName = "policy";

    [SerializeField]
    private string valueOutputName = "value";

    [Header("Contract")]
    [Tooltip("When true, raw policy output is converted with a numerically stable softmax over LEGAL moves only.")]
    [SerializeField]
    private bool policyIsLogits = true;

    [Tooltip("The training network exports an unconstrained scalar. Apply tanh before returning to MCTS.")]
    [SerializeField]
    private bool valueNeedsTanh = true;

    [Tooltip("Keep false for strict contract checking. Enable only when you deliberately know the output index.")]
    [SerializeField]
    private bool allowIndexFallback = false;

    [SerializeField]
    private int policyOutputIndex = 0;

    [SerializeField]
    private int valueOutputIndex = 1;

    [Header("Backend")]
    [SerializeField]
    private bool useGpu = false;

    [Header("Diagnostics")]
    [SerializeField]
    private bool logFirstSuccessfulInference = true;

    [SerializeField]
    private bool logInferenceFailures = true;

    private Worker _worker;
    private Model _runtimeModel;
    private Tensor<float> _inputTensor;
    private float[] _inputBuffer;
    private bool _initialized;
    private bool _loggedFirstInference;

    public bool IsReady =>
        _initialized &&
        _worker != null &&
        _inputTensor != null;

    private void Awake()
    {
        TryInitialize();
    }

    private void OnEnable()
    {
        if (!IsReady && modelAsset != null)
            TryInitialize();
    }

    private void OnDisable()
    {
        DisposeRuntime();
    }

    private void OnDestroy()
    {
        DisposeRuntime();
    }

    [ContextMenu("Reinitialize Neural Evaluator")]
    public bool TryInitialize()
    {
        DisposeRuntime();

        if (modelAsset == null && !string.IsNullOrWhiteSpace(resourcesModelPath))
        {
            modelAsset = Resources.Load<ModelAsset>(resourcesModelPath);
        }

        if (modelAsset == null)
        {
            Debug.LogWarning(
                "[AlphaZero NN] No ModelAsset assigned and Resources model was not found at: " +
                resourcesModelPath
            );
            return false;
        }

        try
        {
            _runtimeModel = ModelLoader.Load(modelAsset);

            BackendType backend =
                useGpu && SystemInfo.supportsComputeShaders
                    ? BackendType.GPUCompute
                    : BackendType.CPU;

            _worker = new Worker(
                _runtimeModel,
                backend
            );

            _inputBuffer =
                new float[AlphaZeroStateEncoder.ObservationSize];

            _inputTensor =
                new Tensor<float>(
                    new TensorShape(
                        1,
                        AlphaZeroStateEncoder.ObservationSize
                    ),
                    _inputBuffer
                );

            _initialized = true;
            _loggedFirstInference = false;

            ShafranDebug.Log(
                "[AlphaZero NN] Initialized | " +
                $"Backend={backend} | " +
                $"Input={inputName}[{AlphaZeroStateEncoder.ObservationSize}] | " +
                $"Policy={policyOutputName}[{AlphaZeroActionEncoder.ActionCount}] | " +
                $"Value={valueOutputName}[1]"
            );

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[AlphaZero NN] Initialization failed. " +
                "Make sure this is a dual-head ONNX exported by the supplied trainer, " +
                "not an ML-Agents PPO .onnx file.\n" +
                exception
            );

            DisposeRuntime();
            return false;
        }
    }

    public PolicyValueOutput Evaluate(
        GameState state,
        EPlayer perspective,
        IReadOnlyList<StateMove> legalMoves)
    {
        if (!IsReady || state == null)
            return BuildUniformFallback(legalMoves, 0f);

        if (legalMoves == null || legalMoves.Count == 0)
            return new PolicyValueOutput(
                new float[AlphaZeroActionEncoder.ActionCount],
                0f
            );

        try
        {
            AlphaZeroStateEncoder.EncodeTo(
                state,
                perspective,
                _inputBuffer
            );

            _inputTensor.Upload(_inputBuffer);
            _worker.SetInput(inputName, _inputTensor);
            _worker.Schedule();

            Tensor policyTensor = GetOutput(
                policyOutputName,
                policyOutputIndex
            );

            Tensor valueTensor = GetOutput(
                valueOutputName,
                valueOutputIndex
            );

            if (policyTensor == null || valueTensor == null)
            {
                throw new InvalidOperationException(
                    "Dual-head outputs were not found. " +
                    $"Expected named outputs '{policyOutputName}' and '{valueOutputName}'."
                );
            }

            Tensor<float> policyFloat =
                policyTensor as Tensor<float>;

            Tensor<float> valueFloat =
                valueTensor as Tensor<float>;

            if (policyFloat == null || valueFloat == null)
            {
                throw new InvalidOperationException(
                    "The model outputs are not Tensor<float>. " +
                    "Export the network as FP32 outputs."
                );
            }

            float[] policyRaw =
                policyFloat.DownloadToArray();

            float[] valueRaw =
                valueFloat.DownloadToArray();

            if (policyRaw == null ||
                policyRaw.Length != AlphaZeroActionEncoder.ActionCount)
            {
                throw new InvalidOperationException(
                    "Policy output size mismatch. " +
                    $"Expected={AlphaZeroActionEncoder.ActionCount}, " +
                    $"Actual={policyRaw?.Length ?? 0}."
                );
            }

            if (valueRaw == null || valueRaw.Length == 0)
            {
                throw new InvalidOperationException(
                    "Value output is empty. Expected one scalar."
                );
            }

            float value = valueRaw[0];

            if (valueNeedsTanh)
                value = (float)Math.Tanh(value);

            value = Mathf.Clamp(value, -1f, 1f);

            float[] legalPolicy =
                BuildLegalPolicy(
                    policyRaw,
                    legalMoves
                );

            if (logFirstSuccessfulInference && !_loggedFirstInference)
            {
                _loggedFirstInference = true;
                ShafranDebug.Log(
                    "[AlphaZero NN] First inference OK | " +
                    $"PolicyRaw={policyRaw.Length} | " +
                    $"Value={value:F4} | " +
                    $"LegalMoves={legalMoves.Count}"
                );
            }

            return new PolicyValueOutput(
                legalPolicy,
                value
            );
        }
        catch (Exception exception)
        {
            if (logInferenceFailures)
            {
                Debug.LogError(
                    "[AlphaZero NN] Inference failed. " +
                    "The assigned ONNX probably does not match the required " +
                    "986 -> (4900 policy, 1 value) contract.\n" +
                    exception
                );
            }

            return BuildUniformFallback(
                legalMoves,
                0f
            );
        }
    }

    private Tensor GetOutput(string name, int index)
    {
        if (_worker == null)
            return null;

        if (!string.IsNullOrWhiteSpace(name))
        {
            try
            {
                Tensor named = _worker.PeekOutput(name);
                if (named != null)
                    return named;
            }
            catch
            {
                // Optional index fallback handled below.
            }
        }

        if (!allowIndexFallback)
            return null;

        try
        {
            return _worker.PeekOutput(index);
        }
        catch
        {
            return null;
        }
    }

    private float[] BuildLegalPolicy(
        float[] raw,
        IReadOnlyList<StateMove> legalMoves)
    {
        float[] policy =
            new float[AlphaZeroActionEncoder.ActionCount];

        if (raw == null || legalMoves == null || legalMoves.Count == 0)
            return policy;

        float maxLogit = float.NegativeInfinity;

        for (int i = 0; i < legalMoves.Count; i++)
        {
            int action =
                AlphaZeroActionEncoder.Encode(legalMoves[i]);

            if (action < 0 || action >= raw.Length)
                continue;

            if (raw[action] > maxLogit)
                maxLogit = raw[action];
        }

        if (policyIsLogits)
        {
            if (float.IsNegativeInfinity(maxLogit))
                return BuildUniformPolicy(legalMoves);
        }

        float sum = 0f;

        for (int i = 0; i < legalMoves.Count; i++)
        {
            int action =
                AlphaZeroActionEncoder.Encode(legalMoves[i]);

            if (action < 0 || action >= raw.Length)
                continue;

            float probability;

            if (policyIsLogits)
            {
                float logit =
                    Mathf.Clamp(
                        raw[action] - maxLogit,
                        -30f,
                        30f
                    );

                probability = Mathf.Exp(logit);
            }
            else
            {
                probability = Mathf.Max(0f, raw[action]);
            }

            if (float.IsNaN(probability) ||
                float.IsInfinity(probability))
            {
                probability = 0f;
            }

            policy[action] = probability;
            sum += probability;
        }

        if (sum <= 1e-8f ||
            float.IsNaN(sum) ||
            float.IsInfinity(sum))
        {
            return BuildUniformPolicy(legalMoves);
        }

        for (int i = 0; i < policy.Length; i++)
            policy[i] /= sum;

        return policy;
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
            int action =
                AlphaZeroActionEncoder.Encode(legalMoves[i]);

            if (action >= 0 && action < policy.Length)
                valid++;
        }

        if (valid == 0)
            return policy;

        float uniform = 1f / valid;

        for (int i = 0; i < legalMoves.Count; i++)
        {
            int action =
                AlphaZeroActionEncoder.Encode(legalMoves[i]);

            if (action >= 0 && action < policy.Length)
                policy[action] = uniform;
        }

        return policy;
    }

    private static PolicyValueOutput BuildUniformFallback(
        IReadOnlyList<StateMove> legalMoves,
        float value)
    {
        return new PolicyValueOutput(
            BuildUniformPolicy(legalMoves),
            value
        );
    }

    private void DisposeRuntime()
    {
        if (_worker != null)
        {
            try
            {
                _worker.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[AlphaZero NN] Worker.Dispose failed:\n" +
                    exception
                );
            }

            _worker = null;
        }

        if (_inputTensor != null)
        {
            try
            {
                _inputTensor.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[AlphaZero NN] Input tensor Dispose failed:\n" +
                    exception
                );
            }

            _inputTensor = null;
        }

        _runtimeModel = null;
        _inputBuffer = null;
        _initialized = false;
    }
}
