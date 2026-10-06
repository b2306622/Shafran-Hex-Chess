using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Unity.InferenceEngine;
using UnityEngine;

/// <summary>
/// Sentis adapter for an ML-Agents PPO ONNX model used as the policy prior of
/// Shafran MCTS.
///
/// IMPORTANT:
/// - ML-Agents ONNX exports can expose auxiliary inputs (for example action
///   masks, previous actions or recurrent state). Older code only supplied
///   obs_0, which can make Sentis fail inside Slice/other graph nodes.
/// - This version inspects the imported model, binds the observation input,
///   and also binds additional static inputs with safe defaults.
/// - The evaluator never reports success from a fallback policy. A uniform
///   legal-move policy is only a safety return value; LastInferenceSucceeded
///   remains false when the neural model failed.
/// - The PPO model must expose a policy tensor with 4900 float values somewhere
///   in its model outputs. If it exports only the selected discrete action,
///   that model is NOT sufficient for MCTS policy-prior use and must be
///   re-exported with action probabilities/logits exposed.
/// </summary>
public sealed class SentisPpoPolicyEvaluator : MonoBehaviour, IPolicyEvaluator
{
    [Header("Model")]
    [SerializeField]
    private ModelAsset modelAsset;

    [SerializeField]
    private string resourcesModelPath = "AI/ChessPPO";

    [Header("Observation Input")]
    [Tooltip("Leave empty to auto-detect the float input whose size matches 986.")]
    [SerializeField]
    private string inputName = "obs_0";

    [Header("Policy Output")]
    [Tooltip("Leave empty to auto-discover a float output containing exactly 4900 values.")]
    [SerializeField]
    private string policyOutputName = "policy_probabilities";

    [Tooltip("Used only if the named/auto-discovered output cannot be resolved. This is the POSITION in model.outputs, not the internal output.index.")]
    [SerializeField]
    private int policyOutputIndex = 0;

    [Tooltip("When enabled, the evaluator detects probability-vs-logit output from the output name/value distribution. Disable only when you know the export format.")]
    [SerializeField]
    private bool autoDetectPolicyOutputType = false;

    [Tooltip("Used when Auto Detect is disabled. True = logits, False = probabilities.")]
    [SerializeField]
    private bool policyIsLogits = false;

    [Header("Additional ONNX Inputs")]
    [Tooltip("Automatically bind all additional fixed-shape model inputs. Action-mask inputs are filled with ones; previous-action inputs are zeroed; sequence length is set to one; other numeric inputs are zeroed.")]
    [SerializeField]
    private bool autoBindAdditionalInputs = true;

    [Tooltip("Do not silently continue with a uniform policy after a neural inference error. Recommended ON for tournament/benchmark runs.")]
    [SerializeField]
    private bool failHardOnInferenceError = false;

    [Header("Backend")]
    [SerializeField]
    private bool useGpu = false;

    [Header("Performance")]
    [Tooltip("Cache successful legal-policy evaluations by GameState hash across MCTS roots. Keeps memory bounded and avoids repeated Sentis inference for repeated positions.")]
    [SerializeField]
    private bool cacheEvaluations = true;

    [Tooltip("Maximum number of cached positions. 256 is a conservative CPU/memory setting for bootstrap dataset generation.")]
    [Min(16)]
    [SerializeField]
    private int maxCachedPositions = 256;

    [Header("Diagnostics")]
    [SerializeField]
    private bool logModelContractOnInitialize = true;

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
    private bool _lastInferenceSucceeded;
    private int _lastRawOutputLength;
    private string _lastInferenceError;
    private string _resolvedPolicyOutputName;
    private int _resolvedPolicyOutputPosition = -1;

    private readonly List<Tensor> _auxiliaryInputTensors =
        new List<Tensor>(8);

    private readonly List<string> _auxiliaryInputNames =
        new List<string>(8);

    private readonly Dictionary<ulong, float[]> _policyCache =
        new Dictionary<ulong, float[]>(256);
    private readonly Queue<ulong> _policyCacheOrder =
        new Queue<ulong>(256);

    // ML-Agents PPO exports expose an action_masks input.
    // MCTS uses the exact legal-action mask for the current Shafran position.
    private Tensor<float> _actionMaskTensor;
    private float[] _actionMaskBuffer;
    private string _actionMaskInputName;

    public bool IsReady =>
        _initialized &&
        _worker != null &&
        _inputTensor != null;

    public const int ExpectedObservationSize = AlphaZeroStateEncoder.ObservationSize;
    public const int ExpectedActionCount = AlphaZeroActionEncoder.ActionCount;

    public string ResourcesModelPath => resourcesModelPath;
    public string ModelAssetName => modelAsset != null ? modelAsset.name : "<null>";
    public bool LastInferenceSucceeded => _lastInferenceSucceeded;
    public int LastRawOutputLength => _lastRawOutputLength;
    public string LastInferenceError => _lastInferenceError;
    public string ResolvedPolicyOutputName => _resolvedPolicyOutputName;
    public int ResolvedPolicyOutputPosition => _resolvedPolicyOutputPosition;

    public string ModelContractSummary => BuildModelContractSummary();

    private void Awake()
    {
        TryInitialize();
    }

    private void OnEnable()
    {
        if (!IsReady)
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

    [ContextMenu("Reinitialize PPO Policy Evaluator")]
    public bool TryInitialize()
    {
        DisposeRuntime();
        ResetDiagnostics();
        ClearPolicyCache();

        if (modelAsset == null && !string.IsNullOrWhiteSpace(resourcesModelPath))
            modelAsset = Resources.Load<ModelAsset>(resourcesModelPath);

        if (modelAsset == null)
        {
            Debug.LogWarning(
                "[PPO Policy] No ModelAsset assigned and Resources model was not found at: " +
                resourcesModelPath,
                this);
            return false;
        }

        try
        {
            _runtimeModel = ModelLoader.Load(modelAsset);

            BackendType backend =
                useGpu && SystemInfo.supportsComputeShaders
                    ? BackendType.GPUCompute
                    : BackendType.CPU;

            _worker = new Worker(_runtimeModel, backend);

            PrepareObservationInput();

            if (autoBindAdditionalInputs)
                PrepareAdditionalInputs();

            _initialized = true;
            _loggedFirstInference = false;

            if (logModelContractOnInitialize)
            {
                ShafranDebug.Log(
                    "[PPO Policy] Model contract | " +
                    $"Asset={ModelAssetName} | ResourcePath={resourcesModelPath}\n" +
                    BuildModelContractSummary(),
                    this);
            }

            ShafranDebug.Log(
                "[PPO Policy] Initialized | " +
                $"Backend={backend} | ObsInput={_resolvedObservationInputName}[{ExpectedObservationSize}] | " +
                $"ConfiguredOutput={(string.IsNullOrWhiteSpace(policyOutputName) ? "AUTO" : policyOutputName)} | " +
                $"AuxInputs={_auxiliaryInputNames.Count}",
                this);

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError("[PPO Policy] Initialization failed.\n" + exception, this);
            DisposeRuntime();
            return false;
        }
    }

    private string _resolvedObservationInputName;

    public float[] EvaluatePolicy(
        GameState state,
        EPlayer perspective,
        IReadOnlyList<StateMove> legalMoves)
    {
        float[] fallback = BuildUniformPolicy(legalMoves);

        if (!IsReady || state == null || legalMoves == null || legalMoves.Count == 0)
            return fallback;

        if (cacheEvaluations &&
            _policyCache.TryGetValue(state.Hash, out float[] cachedPolicy) &&
            cachedPolicy != null &&
            cachedPolicy.Length == ExpectedActionCount)
        {
            _lastInferenceSucceeded = true;
            _lastInferenceError = null;
            _lastRawOutputLength = ExpectedActionCount;
            return cachedPolicy;
        }

        try
        {
            AlphaZeroStateEncoder.EncodeTo(state, perspective, _inputBuffer);
            _inputTensor.Upload(_inputBuffer);
            _worker.SetInput(_resolvedObservationInputName, _inputTensor);
            BindAdditionalInputs(legalMoves);

            _worker.Schedule();

            float[] raw = ReadPolicyOutput();

            if (raw == null || raw.Length != ExpectedActionCount)
            {
                throw new InvalidOperationException(
                    "PPO policy output contract mismatch. " +
                    $"Expected exactly {ExpectedActionCount} float values but received {raw?.Length ?? 0}. " +
                    "The ONNX may expose only a selected discrete action instead of policy logits/probabilities.");
            }

            _lastRawOutputLength = raw.Length;
            float[] legalPolicy = BuildLegalPolicy(raw, legalMoves);

            _lastInferenceSucceeded = true;
            _lastInferenceError = null;

            if (cacheEvaluations)
                CachePolicy(state.Hash, legalPolicy);

            if (logFirstSuccessfulInference && !_loggedFirstInference)
            {
                _loggedFirstInference = true;
                ShafranDebug.Log(
                    "[PPO Policy] First REAL inference OK | " +
                    $"Output={raw.Length} | OutputName={_resolvedPolicyOutputName} | " +
                    $"LegalMoves={legalMoves.Count} | " +
                    $"OutputMode={(DetectPolicyIsLogits(raw) ? "Logits" : "Probabilities")}",
                    this);
            }

            return legalPolicy;
        }
        catch (Exception exception)
        {
            _lastInferenceSucceeded = false;
            _lastInferenceError = exception.ToString();
            _lastRawOutputLength = 0;

            if (logInferenceFailures)
            {
                Debug.LogError(
                    "[PPO Policy] REAL inference failed. " +
                    "No neural output will be reported as valid.\n" +
                    $"Asset={ModelAssetName}\n" +
                    $"Contract={BuildModelContractSummary()}\n" +
                    exception,
                    this);
            }

            if (failHardOnInferenceError)
                throw;

            return fallback;
        }
    }

    private void PrepareObservationInput()
    {
        List<Model.Input> inputs = _runtimeModel.inputs;
        if (inputs == null || inputs.Count == 0)
            throw new InvalidOperationException("ONNX model has no inputs.");

        Model.Input selected = default;
        bool found = false;

        if (!string.IsNullOrWhiteSpace(inputName))
        {
            for (int i = 0; i < inputs.Count; i++)
            {
                if (string.Equals(inputs[i].name, inputName, StringComparison.Ordinal))
                {
                    selected = inputs[i];
                    found = true;
                    break;
                }
            }
        }

        if (!found)
        {
            for (int i = 0; i < inputs.Count; i++)
            {
                if (string.Equals(inputs[i].name, "obs_0", StringComparison.OrdinalIgnoreCase))
                {
                    selected = inputs[i];
                    found = true;
                    break;
                }
            }
        }

        if (!found)
        {
            for (int i = 0; i < inputs.Count; i++)
            {
                if (TryGetStaticTensorShape(inputs[i].shape, out TensorShape shape) &&
                    shape.length == ExpectedObservationSize)
                {
                    selected = inputs[i];
                    found = true;
                    break;
                }
            }
        }

        if (!found)
        {
            throw new InvalidOperationException(
                "Could not locate the 986-element observation input. " +
                "Inspect the model contract log and set Observation Input > Input Name to the correct ONNX input.");
        }

        if (selected.dataType != DataType.Float)
        {
            throw new InvalidOperationException(
                $"Observation input '{selected.name}' has data type {selected.dataType}; expected Float.");
        }

        if (!TryGetStaticTensorShape(selected.shape, out TensorShape shapeResult))
        {
            throw new InvalidOperationException(
                $"Observation input '{selected.name}' has a dynamic/unresolved shape: {selected.shape}. " +
                "Set its batch dimension to 1 in the ONNX/Sentis import settings or configure a compatible export.");
        }

        if (shapeResult.length != ExpectedObservationSize)
        {
            throw new InvalidOperationException(
                $"Observation input '{selected.name}' shape={shapeResult} has {shapeResult.length} elements, " +
                $"but Shafran expects {ExpectedObservationSize}.");
        }

        _resolvedObservationInputName = selected.name;
        _inputBuffer = new float[ExpectedObservationSize];
        _inputTensor = new Tensor<float>(shapeResult, _inputBuffer);
    }

    private void PrepareAdditionalInputs()
    {
        List<Model.Input> inputs = _runtimeModel.inputs;
        if (inputs == null)
            return;

        for (int i = 0; i < inputs.Count; i++)
        {
            Model.Input input = inputs[i];

            if (string.Equals(input.name, _resolvedObservationInputName, StringComparison.Ordinal))
                continue;

            if (!TryGetStaticTensorShape(input.shape, out TensorShape shape))
            {
                Debug.LogWarning(
                    $"[PPO Policy] Auxiliary input '{input.name}' has unresolved dynamic shape {input.shape}. " +
                    "It will not be bound automatically; inference may fail for recurrent/dynamic exports.",
                    this);
                continue;
            }

            try
            {
                string lower = (input.name ?? string.Empty).ToLowerInvariant();

                bool isMask =
                    lower.Contains("mask") ||
                    lower.Contains("valid_action") ||
                    lower.Contains("action_mask");

                if (isMask &&
                    input.dataType == DataType.Float &&
                    shape.length == ExpectedActionCount)
                {
                    _actionMaskBuffer = new float[shape.length];

                    // Safe initial state. EvaluatePolicy replaces this with
                    // the exact legal-action mask before every inference.
                    for (int k = 0; k < _actionMaskBuffer.Length; k++)
                        _actionMaskBuffer[k] = 1f;

                    _actionMaskTensor = new Tensor<float>(
                        shape,
                        _actionMaskBuffer);

                    _actionMaskInputName = input.name;

                    _auxiliaryInputTensors.Add(_actionMaskTensor);
                    _auxiliaryInputNames.Add(input.name);
                    continue;
                }

                Tensor tensor = CreateAuxiliaryTensor(input, shape);
                _auxiliaryInputTensors.Add(tensor);
                _auxiliaryInputNames.Add(input.name);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Could not prepare auxiliary ONNX input '{input.name}' type={input.dataType} shape={shape}.\n" +
                    exception,
                    exception);
            }
        }
    }

    private Tensor CreateAuxiliaryTensor(Model.Input input, TensorShape shape)
    {
        string lower = (input.name ?? string.Empty).ToLowerInvariant();
        int length = Mathf.Max(1, shape.length);

        bool isMask =
            lower.Contains("mask") ||
            lower.Contains("valid_action") ||
            lower.Contains("action_mask");

        bool isSequence =
            lower.Contains("sequence") ||
            lower.Contains("seq_len") ||
            lower.Contains("sequence_length");

        if (input.dataType == DataType.Float)
        {
            float[] values = new float[length];

            if (isMask)
            {
                for (int i = 0; i < values.Length; i++)
                    values[i] = 1f;
            }
            else if (isSequence)
            {
                values[0] = 1f;
            }

            return new Tensor<float>(shape, values);
        }

        if (input.dataType == DataType.Int)
        {
            int[] values = new int[length];

            if (isMask)
            {
                for (int i = 0; i < values.Length; i++)
                    values[i] = 1;
            }
            else if (isSequence)
            {
                values[0] = 1;
            }

            return new Tensor<int>(shape, values);
        }

        throw new NotSupportedException(
            $"Automatic auxiliary input binding supports Float and Int only, but '{input.name}' uses {input.dataType}.");
    }

    private void BindAdditionalInputs(
        IReadOnlyList<StateMove> legalMoves)
    {
        if (!autoBindAdditionalInputs || _auxiliaryInputTensors.Count == 0)
            return;

        if (_actionMaskTensor != null &&
            _actionMaskBuffer != null &&
            !string.IsNullOrWhiteSpace(_actionMaskInputName))
        {
            // ML-Agents action mask convention:
            //   1 = action is available
            //   0 = action is masked/illegal.
            for (int i = 0; i < _actionMaskBuffer.Length; i++)
                _actionMaskBuffer[i] = 0f;

            if (legalMoves != null)
            {
                for (int i = 0; i < legalMoves.Count; i++)
                {
                    int action = AlphaZeroActionEncoder.Encode(legalMoves[i]);

                    if (action >= 0 && action < _actionMaskBuffer.Length)
                        _actionMaskBuffer[action] = 1f;
                }
            }

            _actionMaskTensor.Upload(_actionMaskBuffer);
        }

        for (int i = 0; i < _auxiliaryInputTensors.Count; i++)
        {
            if (_auxiliaryInputTensors[i] == null)
                continue;

            _worker.SetInput(
                _auxiliaryInputNames[i],
                _auxiliaryInputTensors[i]);
        }
    }

    private float[] ReadPolicyOutput()
    {
        // 1. Configured named output is always the first choice.
        if (!string.IsNullOrWhiteSpace(policyOutputName))
        {
            for (int i = 0; i < _runtimeModel.outputs.Count; i++)
            {
                Model.Output output = _runtimeModel.outputs[i];
                if (!string.Equals(output.name, policyOutputName, StringComparison.Ordinal))
                    continue;

                float[] values = TryReadFloatOutput(i);
                if (values != null)
                {
                    _resolvedPolicyOutputPosition = i;
                    _resolvedPolicyOutputName = output.name;
                    return values;
                }
            }
        }

        // 2. Use cached auto-discovered output.
        if (_resolvedPolicyOutputPosition >= 0 &&
            _resolvedPolicyOutputPosition < _runtimeModel.outputs.Count)
        {
            float[] cached = TryReadFloatOutput(_resolvedPolicyOutputPosition);
            if (cached != null && cached.Length == ExpectedActionCount)
                return cached;
        }

        // 3. Scan every model output and choose a 4900-float tensor.
        //    Prefer names that conventionally describe policy/probability/logit outputs.
        int bestPosition = -1;
        int bestScore = int.MinValue;
        float[] bestValues = null;
        string bestName = null;

        for (int i = 0; i < _runtimeModel.outputs.Count; i++)
        {
            Model.Output output = _runtimeModel.outputs[i];
            float[] values = TryReadFloatOutput(i);
            if (values == null || values.Length != ExpectedActionCount)
                continue;

            int score = ScorePolicyOutputName(output.name);
            if (score > bestScore)
            {
                bestScore = score;
                bestPosition = i;
                bestValues = values;
                bestName = output.name;
            }
        }

        if (bestValues != null)
        {
            _resolvedPolicyOutputPosition = bestPosition;
            _resolvedPolicyOutputName = bestName;
            return bestValues;
        }

        // 4. Explicit position as last resort.
        if (policyOutputIndex >= 0 && policyOutputIndex < _runtimeModel.outputs.Count)
        {
            Model.Output output = _runtimeModel.outputs[policyOutputIndex];
            float[] values = TryReadFloatOutput(policyOutputIndex);
            if (values != null && values.Length == ExpectedActionCount)
            {
                _resolvedPolicyOutputPosition = policyOutputIndex;
                _resolvedPolicyOutputName = output.name;
                return values;
            }
        }

        throw new InvalidOperationException(
            "No model output contains exactly 4900 float values. " +
            "The model outputs are listed in the contract log. " +
            "If the ONNX exposes only a discrete selected action, it must be re-exported with action probabilities/logits exposed for MCTS.");
    }

    private float[] TryReadFloatOutput(int position)
    {
        try
        {
            Tensor tensor = _worker.PeekOutput(position);
            Tensor<float> floatTensor = tensor as Tensor<float>;
            if (floatTensor != null)
                return floatTensor.DownloadToArray();

            Tensor<int> intTensor = tensor as Tensor<int>;
            if (intTensor != null)
            {
                int[] intValues = intTensor.DownloadToArray();
                if (intValues == null)
                    return null;

                float[] result = new float[intValues.Length];
                for (int i = 0; i < intValues.Length; i++)
                    result[i] = intValues[i];
                return result;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static int ScorePolicyOutputName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return 0;

        string lower = name.ToLowerInvariant();
        int score = 0;

        if (lower.Contains("policy_probabilities")) score += 220;
        if (lower.Contains("action_probs")) score += 200;
        if (lower.Contains("action_prob")) score += 190;
        if (lower.Contains("actionprob")) score += 190;
        if (lower.Contains("policy")) score += 160;
        if (lower.Contains("prob")) score += 150;
        if (lower.Contains("logit")) score += 130;
        if (lower.Contains("action")) score += 20;

        if (lower.Contains("value")) score -= 100;
        if (lower.Contains("critic")) score -= 100;

        return score;
    }

    private float[] BuildLegalPolicy(
        float[] raw,
        IReadOnlyList<StateMove> legalMoves)
    {
        float[] policy =
            new float[ExpectedActionCount];

        if (raw == null || legalMoves == null || legalMoves.Count == 0)
            return policy;

        bool treatAsLogits = DetectPolicyIsLogits(raw);
        float maxValue = float.NegativeInfinity;

        for (int i = 0; i < legalMoves.Count; i++)
        {
            int action = AlphaZeroActionEncoder.Encode(legalMoves[i]);
            if (action >= 0 && action < raw.Length)
                maxValue = Mathf.Max(maxValue, raw[action]);
        }

        float sum = 0f;

        for (int i = 0; i < legalMoves.Count; i++)
        {
            int action = AlphaZeroActionEncoder.Encode(legalMoves[i]);
            if (action < 0 || action >= raw.Length)
                continue;

            float probability;

            if (treatAsLogits)
            {
                if (float.IsNegativeInfinity(maxValue))
                    continue;

                probability = Mathf.Exp(
                    Mathf.Clamp(raw[action] - maxValue, -30f, 30f));
            }
            else
            {
                probability = Mathf.Max(0f, raw[action]);
            }

            if (float.IsNaN(probability) || float.IsInfinity(probability))
                probability = 0f;

            policy[action] = probability;
            sum += probability;
        }

        if (sum <= 1e-8f || float.IsNaN(sum) || float.IsInfinity(sum))
            return BuildUniformPolicy(legalMoves);

        for (int i = 0; i < policy.Length; i++)
            policy[i] /= sum;

        return policy;
    }

    private bool DetectPolicyIsLogits(float[] raw)
    {
        if (!autoDetectPolicyOutputType)
            return policyIsLogits;

        if (!string.IsNullOrWhiteSpace(_resolvedPolicyOutputName))
        {
            string lower = _resolvedPolicyOutputName.ToLowerInvariant();
            if (lower.Contains("prob"))
                return false;
            if (lower.Contains("logit"))
                return true;
        }

        if (raw == null || raw.Length == 0)
            return policyIsLogits;

        double nonNegativeSum = 0.0;
        bool allNonNegative = true;
        int finiteCount = 0;

        for (int i = 0; i < raw.Length; i++)
        {
            float value = raw[i];
            if (float.IsNaN(value) || float.IsInfinity(value))
                continue;

            finiteCount++;
            if (value < 0f)
                allNonNegative = false;
            else
                nonNegativeSum += value;
        }

        if (finiteCount == raw.Length && allNonNegative &&
            Math.Abs(nonNegativeSum - 1.0) <= 0.02)
        {
            return false;
        }

        return true;
    }

    private static float[] BuildUniformPolicy(IReadOnlyList<StateMove> legalMoves)
    {
        float[] policy = new float[ExpectedActionCount];
        if (legalMoves == null || legalMoves.Count == 0)
            return policy;

        int valid = 0;
        for (int i = 0; i < legalMoves.Count; i++)
        {
            int action = AlphaZeroActionEncoder.Encode(legalMoves[i]);
            if (action >= 0 && action < policy.Length)
                valid++;
        }

        if (valid == 0)
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

    private string BuildModelContractSummary()
    {
        if (_runtimeModel == null)
            return "RuntimeModel=<null>";

        StringBuilder sb = new StringBuilder(1024);
        sb.Append("Inputs=");

        if (_runtimeModel.inputs != null && _runtimeModel.inputs.Count > 0)
        {
            for (int i = 0; i < _runtimeModel.inputs.Count; i++)
            {
                if (i > 0) sb.Append(" | ");
                Model.Input input = _runtimeModel.inputs[i];
                sb.Append(i)
                    .Append(":")
                    .Append(input.name)
                    .Append(" shape=")
                    .Append(input.shape)
                    .Append(" type=")
                    .Append(input.dataType);
            }
        }
        else
        {
            sb.Append("<none>");
        }

        sb.Append("; Outputs=");

        if (_runtimeModel.outputs != null && _runtimeModel.outputs.Count > 0)
        {
            for (int i = 0; i < _runtimeModel.outputs.Count; i++)
            {
                if (i > 0) sb.Append(" | ");
                Model.Output output = _runtimeModel.outputs[i];
                sb.Append(i)
                    .Append(":")
                    .Append(output.name)
                    .Append(" internalIndex=")
                    .Append(output.index);
            }
        }
        else
        {
            sb.Append("<none>");
        }

        return sb.ToString();
    }

    private static bool TryGetStaticTensorShape(object dynamicShape, out TensorShape shape)
    {
        shape = default;
        if (dynamicShape == null)
            return false;

        string text = dynamicShape.ToString();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        MatchCollection matches = Regex.Matches(text, @"-?\d+");
        if (matches.Count == 0 || matches.Count > TensorShape.maxRank)
            return false;

        int[] dims = new int[matches.Count];
        for (int i = 0; i < matches.Count; i++)
        {
            if (!int.TryParse(
                    matches[i].Value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int value))
            {
                return false;
            }

            // Dynamic dimensions are replaced with a conservative batch/sequence
            // value of 1. The common action-mask dimension is retained when fixed.
            dims[i] = value > 0 ? value : 1;
        }

        try
        {
            shape = new TensorShape(dims);
            return shape.length > 0;
        }
        catch
        {
            return false;
        }
    }

    private void CachePolicy(ulong hash, float[] policy)
    {
        if (!cacheEvaluations || policy == null || policy.Length != ExpectedActionCount)
            return;

        int capacity = Mathf.Clamp(maxCachedPositions, 16, 4096);

        if (_policyCache.ContainsKey(hash))
        {
            _policyCache[hash] = policy;
            return;
        }

        _policyCache[hash] = policy;
        _policyCacheOrder.Enqueue(hash);

        while (_policyCache.Count > capacity && _policyCacheOrder.Count > 0)
        {
            ulong oldest = _policyCacheOrder.Dequeue();
            _policyCache.Remove(oldest);
        }
    }

    private void ClearPolicyCache()
    {
        _policyCache.Clear();
        _policyCacheOrder.Clear();
    }

    private void ResetDiagnostics()
    {
        _lastInferenceSucceeded = false;
        _lastRawOutputLength = 0;
        _lastInferenceError = null;
        _resolvedPolicyOutputName = null;
        _resolvedPolicyOutputPosition = -1;
        _resolvedObservationInputName = null;
        _actionMaskTensor = null;
        _actionMaskBuffer = null;
        _actionMaskInputName = null;
    }

    private void DisposeRuntime()
    {
        ClearPolicyCache();

        if (_worker != null)
        {
            try { _worker.Dispose(); }
            catch (Exception exception)
            {
                Debug.LogWarning("[PPO Policy] Worker.Dispose failed.\n" + exception, this);
            }
            _worker = null;
        }

        if (_inputTensor != null)
        {
            try { _inputTensor.Dispose(); }
            catch (Exception exception)
            {
                Debug.LogWarning("[PPO Policy] Input tensor Dispose failed.\n" + exception, this);
            }
            _inputTensor = null;
        }

        for (int i = 0; i < _auxiliaryInputTensors.Count; i++)
        {
            if (_auxiliaryInputTensors[i] == null)
                continue;

            try { _auxiliaryInputTensors[i].Dispose(); }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[PPO Policy] Auxiliary tensor Dispose failed.\n" + exception,
                    this);
            }
        }

        _auxiliaryInputTensors.Clear();
        _auxiliaryInputNames.Clear();
        _actionMaskTensor = null;
        _actionMaskBuffer = null;
        _actionMaskInputName = null;

        _runtimeModel = null;
        _inputBuffer = null;
        _initialized = false;
        _resolvedObservationInputName = null;
    }
}
