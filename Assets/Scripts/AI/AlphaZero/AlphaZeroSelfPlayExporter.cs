using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Generates the initial AlphaZero-style dataset.
///
/// Cycle 1 can use HeuristicPolicyValueEvaluator to bootstrap the first
/// neural network. After that, assign the neural evaluator and repeat the
/// same exporter to continue the AlphaZero self-play loop.
/// </summary>
public sealed class AlphaZeroSelfPlayExporter : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private MonoBehaviour policyValueEvaluatorComponent;

    [Header("Self-play")]
    [SerializeField]
    private int games = 20;

    [SerializeField]
    private int simulationsPerMove = 16;

    [Min(1)]
    [SerializeField]
    private int maxSearchDepth = 16;

    [SerializeField]
    private int maxPlies = 500;

    [Min(1)]
    [SerializeField]
    private int noProgressDrawPlies = 100;

    [SerializeField]
    private float trainingTemperature = 1.0f;

    [SerializeField]
    private bool addRootNoise = false;

    [Header("Output")]
    [SerializeField]
    private string outputDirectory = @"D:\CoHex\CoHexAI\Training";

    [SerializeField]
    private string outputFileName = "AlphaZeroSamples.ndjson";

    [SerializeField]
    private bool overwriteFile = true;

    /// <summary>
    /// Allows the thesis A/B/C batch setup to assign an isolated dataset name
    /// without exposing the existing serialized field or duplicating exporter logic.
    /// </summary>
    public void ConfigureOutputFile(string fileName)
    {
        if (!string.IsNullOrWhiteSpace(fileName))
            outputFileName = fileName.Trim();

        overwriteFile = true;
    }

    /// <summary>
    /// Runtime hook used by the A/B/C batch bootstrap runner.
    /// </summary>
    public void ConfigureEvaluator(MonoBehaviour evaluatorComponent)
    {
        policyValueEvaluatorComponent = evaluatorComponent;
    }

    /// <summary>
    /// Configure the lightweight bootstrap search without exposing internal
    /// serialized fields to other scripts.
    /// </summary>
    public void ConfigureSearch(int simulations, int maxDepth, bool rootNoise)
    {
        simulationsPerMove = Mathf.Max(1, simulations);
        maxSearchDepth = Mathf.Max(1, maxDepth);
        addRootNoise = rootNoise;
    }

    [Header("Dataset Start Position")]
    [SerializeField]
    private bool resetUnityBoardBeforeGeneration = true;

    [SerializeField]
    private bool disableLiveGameControllerDuringGeneration = true;

    [ContextMenu("Generate Self-Play Dataset")]
    public void GenerateDataset()
    {
        IPolicyValueEvaluator evaluator =
            policyValueEvaluatorComponent as IPolicyValueEvaluator;

        if (evaluator == null)
        {
            Debug.LogError(
                "[AlphaZero Dataset] policyValueEvaluatorComponent must implement IPolicyValueEvaluator."
            );
            return;
        }

        MonoBehaviour liveController =
            BaseGameController.Current as MonoBehaviour;

        bool restoreControllerEnabled =
            liveController != null && liveController.enabled;

        try
        {
            // The exporter is an offline simulator. Do not allow the live
            // controller to continue making Unity-board moves while the
            // dataset is being generated.
            if (disableLiveGameControllerDuringGeneration &&
                liveController != null)
            {
                liveController.enabled = false;
            }

            // IMPORTANT:
            // If the current Unity board is already in GAME_OVER, the old
            // implementation captured that terminal position and every
            // generated game ended at Ply=0, producing Samples=0.
            // Rebuild the board first so dataset generation always starts
            // from the initial position.
            if (resetUnityBoardBeforeGeneration)
            {
                if (ChessBoard.Current == null)
                {
                    Debug.LogError(
                        "[AlphaZero Dataset] ChessBoard.Current is null; cannot reset the board."
                    );
                    return;
                }

                ChessBoard.Current.ResetBoard();

                if (BaseGameController.Current != null)
                {
                    BaseGameController.Current.ResetGameState();
                }
            }

            GameState initialState =
                GameState.FromUnityBoard();

            if (initialState == null)
            {
                Debug.LogError(
                    "[AlphaZero Dataset] Cannot build initial GameState from Unity board."
                );
                return;
            }

            MoveGenerator moveGenerator =
                new MoveGenerator();

            List<StateMove> initialLegalMoves =
                moveGenerator.GenerateLegalMoves(
                    initialState,
                    initialState.SideToMove
                );

            if (initialLegalMoves == null || initialLegalMoves.Count == 0)
            {
                Debug.LogError(
                    "[AlphaZero Dataset] Initial position has 0 legal moves. " +
                    "Make sure the Unity scene contains the normal starting board, " +
                    "then run Generate Self-Play Dataset again."
                );
                return;
            }

            ShafranDebug.Log(
                "[AlphaZero Dataset] Initial position OK | " +
                $"Player={initialState.SideToMove} | " +
                $"LegalMoves={initialLegalMoves.Count} | " +
                $"Hash={initialState.Hash}"
            );

            AlphaZeroConfig config =
                new AlphaZeroConfig
                {
                    simulations = Mathf.Max(1, simulationsPerMove),
                    maxDepth = Mathf.Max(1, maxSearchDepth),
                    cpuct = 1.5f,
                    policyTemperature = Mathf.Clamp(trainingTemperature, 0.01f, 5f),
                    rootDirichletEpsilon = addRootNoise ? 0.25f : 0f,
                    rootDirichletAlpha = 0.30f,
                    enableRepetitionAwareness = true,
                    repetitionCutoffOccurrences = 3,
                    enableNoProgressAwareness = true,
                    inheritGameNoProgressIntoSearch = false,
                    noProgressPlyThreshold = Mathf.Max(1, noProgressDrawPlies)
                };

            AlphaZeroSearch search =
                new AlphaZeroSearch(
                    evaluator,
                    config
                );

            string directory =
                string.IsNullOrWhiteSpace(outputDirectory)
                    ? Application.persistentDataPath
                    : outputDirectory.Trim();

            try
            {
                Directory.CreateDirectory(directory);
            }
            catch (System.Exception exception)
            {
                Debug.LogError(
                    "[AlphaZero Dataset] Failed to create output directory: " +
                    directory + "\n" +
                    exception
                );
                return;
            }

            string path =
                Path.Combine(
                    directory,
                    outputFileName
                );

            int samplesWritten = 0;
            bool append = !overwriteFile && File.Exists(path);

            using (StreamWriter writer =
                   new StreamWriter(path, append))
            {
                for (int game = 0;
                     game < Mathf.Max(1, games);
                     game++)
                {
                    GameState state =
                        initialState.Clone();

                    List<RecordedState> history =
                        new List<RecordedState>(maxPlies);

                    Dictionary<ulong, int> repetition =
                        new Dictionary<ulong, int>(512);

                    repetition[state.Hash] = 1;

                    int ply = 0;
                    EPlayer? winner = null;
                    int noProgressPlies = 0;

                    while (ply < maxPlies)
                    {
                        List<StateMove> legalMoves =
                            moveGenerator.GenerateLegalMoves(
                                state,
                                state.SideToMove
                            );

                        if (legalMoves == null || legalMoves.Count == 0)
                        {
                            bool check =
                                moveGenerator.IsInCheck(
                                    state,
                                    state.SideToMove
                                );

                            if (check)
                            {
                                winner = HexChessRules.GetOpponent(
                                    state.SideToMove
                                );
                            }

                            break;
                        }

                        MCTSSearchResult result =
                            search.Search(
                                state,
                                repetition,
                                noProgressPlies
                            );

                        history.Add(
                            new RecordedState(
                                AlphaZeroStateEncoder.Encode(
                                    state,
                                    state.SideToMove
                                ),
                                state.SideToMove,
                                result.Policy,
                                ply
                            )
                        );

                        StateMove selectedMove =
                            AlphaZeroSearch.ChooseMove(
                                result,
                                legalMoves,
                                trainingTemperature
                            );

                        bool progressMove =
                            selectedMove.IsCapture ||
                            selectedMove.IsEnPassant ||
                            selectedMove.IsPromotion ||
                            selectedMove.MovingPiece.Type == StatePieceType.PAWN;

                        moveGenerator.MakeMove(
                            state,
                            selectedMove
                        );

                        ply++;

                        if (progressMove)
                        {
                            noProgressPlies = 0;
                        }
                        else
                        {
                            noProgressPlies++;
                        }

                        // Apply the same V10 terminal rule as Unity PPO:
                        // only the configured insufficient-material cases draw.
                        if (HexChessRules.TryGetMaterialTerminalResult(
                                state,
                                out EPlayer? materialWinner,
                                out bool materialDraw))
                        {
                            if (materialWinner.HasValue)
                            {
                                winner = materialWinner.Value;
                            }

                            // materialDraw leaves winner == null.
                            break;
                        }

                        if (repetition.TryGetValue(
                                state.Hash,
                                out int count))
                        {
                            repetition[state.Hash] = count + 1;
                        }
                        else
                        {
                            repetition[state.Hash] = 1;
                        }

                        if (repetition[state.Hash] >= 3)
                            break;

                        if (noProgressPlies >= Mathf.Max(1, noProgressDrawPlies))
                            break;
                    }

                    for (int i = 0;
                         i < history.Count;
                         i++)
                    {
                        RecordedState sample = history[i];

                        float value =
                            !winner.HasValue
                                ? 0f
                                : winner.Value == sample.Perspective
                                    ? 1f
                                    : -1f;

                        BuildSparsePolicy(
                            sample.Policy,
                            out int[] actionIndices,
                            out float[] actionProbabilities
                        );

                        AlphaZeroTrainingSample output =
                            new AlphaZeroTrainingSample(
                                sample.Observation,
                                actionIndices,
                                actionProbabilities,
                                value,
                                game,
                                sample.PlyIndex
                            );

                        writer.WriteLine(
                            JsonUtility.ToJson(output)
                        );

                        samplesWritten++;
                    }

                    ShafranDebug.Log(
                        "[AlphaZero Dataset] " +
                        $"Game={game + 1}/{games} | " +
                        $"Ply={ply} | " +
                        $"Winner={winner?.ToString() ?? "DRAW"} | " +
                        $"SamplesThisGame={history.Count}"
                    );
                }
            }

            ShafranDebug.Log(
                "[AlphaZero Dataset] Written | " +
                $"Path={path} | Samples={samplesWritten}"
            );
        }
        finally
        {
            if (disableLiveGameControllerDuringGeneration &&
                liveController != null &&
                restoreControllerEnabled)
            {
                liveController.enabled = true;
            }
        }
    }

    private static void BuildSparsePolicy(
        float[] policy,
        out int[] actionIndices,
        out float[] actionProbabilities)
    {
        List<int> indices =
            new List<int>(64);
        List<float> probabilities =
            new List<float>(64);

        if (policy != null)
        {
            for (int i = 0;
                 i < policy.Length;
                 i++)
            {
                if (policy[i] <= 1e-8f)
                    continue;

                indices.Add(i);
                probabilities.Add(policy[i]);
            }
        }

        actionIndices = indices.ToArray();
        actionProbabilities = probabilities.ToArray();
    }

    private readonly struct RecordedState
    {
        public readonly float[] Observation;
        public readonly EPlayer Perspective;
        public readonly float[] Policy;
        public readonly int PlyIndex;

        public RecordedState(
            float[] observation,
            EPlayer perspective,
            float[] policy,
            int plyIndex)
        {
            Observation = observation;
            Perspective = perspective;
            Policy = policy;
            PlyIndex = plyIndex;
        }
    }
}
