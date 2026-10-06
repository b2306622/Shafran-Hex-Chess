using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Offline tournament runner over the project's GameState/MoveGenerator.
/// It keeps the visual board untouched while replaying independent games in
/// cloned GameState objects, so large batches do not require piece animations.
///
/// Supported participants:
/// Minimax Alpha-Beta, PPO Base, PPO + Minimax Guidance D1/D2, and
/// AlphaZero dual-head ONNX + MCTS variants, plus PPO-bootstrap A/B/C models.
/// </summary>
public sealed class AITournamentManager : MonoBehaviour
{
    [Header("Participants")]
    [SerializeField]
    private bool enableMinimax = true;

    [SerializeField]
    private bool enablePpoBase = true;

    [SerializeField]
    private bool enablePpoD1 = true;

    [SerializeField]
    private bool enablePpoD2 = true;

    [SerializeField]
    private bool enableAlphaZeroBase = true;

    [SerializeField]
    private bool enableAlphaZeroD1 = true;

    [SerializeField]
    private bool enableAlphaZeroD2 = true;

    [Header("Final A / B / C Models") ]
    [Tooltip("When enabled, run only MODEL_A_PRIME / MODEL_B_PRIME / MODEL_C_PRIME." )]
    [SerializeField]
    private bool finalThreeOnly = false;

    [SerializeField]
    private bool enableModelAPrime = true;

    [SerializeField]
    private bool enableModelBPrime = true;

    [SerializeField]
    private bool enableModelCPrime = true;

    [Header("Evaluators")]
    [SerializeField]
    private SentisPpoPolicyEvaluator ppoBaseEvaluator;

    [SerializeField]
    private SentisPpoPolicyEvaluator ppoD1Evaluator;

    [SerializeField]
    private SentisPpoPolicyEvaluator ppoD2Evaluator;

    [SerializeField]
    private SentisDualHeadPolicyValueEvaluator alphaZeroBaseEvaluator;

    [SerializeField]
    private SentisDualHeadPolicyValueEvaluator alphaZeroD1Evaluator;

    [SerializeField]
    private SentisDualHeadPolicyValueEvaluator alphaZeroD2Evaluator;

    [Header("Final A / B / C Evaluators")]
    [Tooltip("MODEL_A = PPO ChessBrain_A + heuristic value + MCTS. Assign a ShafranPPOBootstrapPolicyValueEvaluator.")]
    [SerializeField]
    private ShafranPPOBootstrapPolicyValueEvaluator modelAPrimeEvaluator;

    [Tooltip("MODEL_B = PPO ChessBrain_B + heuristic value + MCTS. Assign a ShafranPPOBootstrapPolicyValueEvaluator.")]
    [SerializeField]
    private ShafranPPOBootstrapPolicyValueEvaluator modelBPrimeEvaluator;

    [Tooltip("MODEL_C = PPO ChessBrain_C + heuristic value + MCTS. Assign a ShafranPPOBootstrapPolicyValueEvaluator.")]
    [SerializeField]
    private ShafranPPOBootstrapPolicyValueEvaluator modelCPrimeEvaluator;

    [Header("Minimax")]
    [Min(1)]
    [SerializeField]
    private int minimaxDepth = 4;

    [SerializeField]
    private bool minimaxHistory = true;

    [SerializeField]
    private bool minimaxKiller = true;

    [SerializeField]
    private bool minimaxTranspositionTable = true;

    [SerializeField]
    private bool minimaxAspiration = true;

    [Header("AlphaZero")]
    [Min(1)]
    [SerializeField]
    private int alphaZeroSimulations = 100;

    [Min(1)]
    [SerializeField]
    private int alphaZeroMaxDepth = 64;

    [Min(0.01f)]
    [SerializeField]
    private float alphaZeroCpuct = 1.5f;

    [SerializeField]
    private float alphaZeroTemperature = 0.05f;

    [Header("Tournament Opening Diversity")]
    [Tooltip("Number of opening plies sampled from the MCTS visit distribution before switching to deterministic evaluation play.")]
    [Min(0)]
    [SerializeField]
    private int openingRandomPlies = 8;

    [Tooltip("Temperature used only during the opening-diversity phase. 1.0 samples broadly from MCTS visit counts.")]
    [Range(0.05f, 1.0f)]
    [SerializeField]
    private float openingRandomTemperature = 1.0f;

    [Header("Tournament")]
    [Min(1)]
    [SerializeField]
    private int gamesPerPair = 40;

    [Min(1)]
    [SerializeField]
    private int maxPliesPerGame = 500;

    [SerializeField]
    private int randomSeed = 20260922;

    [SerializeField]
    private bool alternateColors = true;

    [SerializeField]
    private bool alternatePairOrder = true;

    [Header("Draw Rules")]
    [Min(2)]
    [SerializeField]
    private int repetitionDrawOccurrences = 3;

    [Min(1)]
    [SerializeField]
    private int noProgressDrawPlies = 100;

    [Header("Output")]
    [SerializeField]
    private string outputDirectory = "TournamentResults";

    [SerializeField]
    private bool writePerGameCsv = true;

    [SerializeField]
    private bool writeSummaryCsv = true;

    [SerializeField]
    private bool logProgress = true;

    [Header("Safety / Preflight")]
    [Tooltip("Require a real 4900-value PPO inference from A/B/C before the first game. Prevents a failed ONNX from silently becoming a uniform-policy MCTS player.")]
    [SerializeField]
    private bool requireRealPpoInferenceBeforeTournament = true;

    private Coroutine _tournamentCoroutine;

    [Serializable]
    private sealed class ParticipantRuntime
    {
        public AIPlayerDescriptor descriptor;
        public IPolicyEvaluator ppo;
        public IPolicyValueEvaluator alphaZero;
    }

    private sealed class MoveDecision
    {
        public StateMove move;
        public double milliseconds;
        public long nodes;
        public int mctsSimulations;
        public bool hasMcts;
    }

    private readonly List<AITournamentMetrics> _summaries =
        new List<AITournamentMetrics>();

    [ContextMenu("Run A / B / C AlphaZero Tournament")]
    public void RunFinalABCTournament()
    {
        finalThreeOnly = true;
        enableModelAPrime = true;
        enableModelBPrime = true;
        enableModelCPrime = true;

        Debug.Log(
            $"[AI Tournament] START requested: A/B/C AlphaZero | GamesPerPair={gamesPerPair} | " +
            $"Simulations={alphaZeroSimulations} | MaxDepth={alphaZeroMaxDepth} | " +
            $"OpeningRandomPlies={openingRandomPlies} | OpeningTemp={openingRandomTemperature:F2} | " +
            $"MainTemp={alphaZeroTemperature:F2} | MaxPlies={maxPliesPerGame}",
            this);

        RunFullTournament();
    }

    [ContextMenu("Run Full AI Tournament")]
    public void RunFullTournament()
    {
        if (_tournamentCoroutine != null)
        {
            Debug.LogWarning("[AI Tournament] Tournament is already running.", this);
            return;
        }

        Debug.Log("[AI Tournament] Coroutine starting...", this);
        _tournamentCoroutine = StartCoroutine(RunTournamentRoutine());
    }

    public void StopTournament()
    {
        if (_tournamentCoroutine == null)
            return;

        StopCoroutine(_tournamentCoroutine);
        _tournamentCoroutine = null;
        Debug.LogWarning("[AI Tournament] Tournament stopped.", this);
    }

    private IEnumerator RunTournamentRoutine()
    {
        Debug.Log("[AI Tournament] ROUTINE ENTERED", this);
        EnsureEvaluatorReferences();

        List<AIPlayerDescriptor> participants = BuildParticipants();
        Debug.Log($"[AI Tournament] Participants built = {participants.Count}", this);
        for (int p = 0; p < participants.Count; p++)
            Debug.Log($"[AI Tournament] Participant[{p}] = {participants[p].displayName}", this);

        if (participants.Count < 2)
        {
            Debug.LogError(
                "[AI Tournament] Need at least two valid participants. " +
                "For MODEL_A/B/C assign ShafranPPOBootstrapPolicyValueEvaluator components.",
                this);
            _tournamentCoroutine = null;
            yield break;
        }

        GameState initialState = CaptureInitialState();
        if (initialState == null)
        {
            _tournamentCoroutine = null;
            yield break;
        }

        if (requireRealPpoInferenceBeforeTournament && !PreflightPpoModels(participants, initialState))
        {
            Debug.LogError(
                "[AI Tournament] PRE-FLIGHT FAILED. Tournament was NOT started because at least one PPO A/B/C model did not produce a real 4900-value neural policy. Fix the ONNX contract/inference first.",
                this);
            _tournamentCoroutine = null;
            yield break;
        }

        string directory = ResolveOutputDirectory();
        Directory.CreateDirectory(directory);

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string perGamePath = Path.Combine(directory, "AITournament_Games_" + timestamp + ".csv");
        string summaryPath = Path.Combine(directory, "AITournament_Summary_" + timestamp + ".csv");
        string overallPath = Path.Combine(directory, "AITournament_Overall_" + timestamp + ".csv");

        using (StreamWriter perGameWriter = writePerGameCsv
                   ? new StreamWriter(perGamePath, false, Encoding.UTF8)
                   : null)
        using (StreamWriter summaryWriter = writeSummaryCsv
                   ? new StreamWriter(summaryPath, false, Encoding.UTF8)
                   : null)
        using (StreamWriter overallWriter = writeSummaryCsv
                   ? new StreamWriter(overallPath, false, Encoding.UTF8)
                   : null)
        {
            if (perGameWriter != null)
            {
                perGameWriter.WriteLine(
                    "Pair,Game,White,Black,Winner,DrawReason,Plies,Captures,Checks,Promotions," +
                    "WhiteThinkMs,BlackThinkMs,WhiteNodes,BlackNodes,MctsSimulations,MctsSearchMs");
            }

            bool aborted = false;

            for (int i = 0; i < participants.Count && !aborted; i++)
            {
                for (int j = i + 1; j < participants.Count && !aborted; j++)
                {
                    ParticipantRuntime a = CreateRuntime(participants[i]);
                    ParticipantRuntime b = CreateRuntime(participants[j]);
                    if (a == null || b == null)
                        continue;

                    AITournamentMetrics summary =
                        new AITournamentMetrics(participants[i].displayName, participants[j].displayName);
                    _summaries.Add(summary);

                    int totalGames = Mathf.Max(1, gamesPerPair);

                    for (int game = 0; game < totalGames; game++)
                    {
                        bool aWhite = alternateColors
                            ? ((game & 1) == 0)
                            : true;

                        if (alternatePairOrder && ((game / 2) & 1) == 1)
                            aWhite = !aWhite;

                        ParticipantRuntime white = aWhite ? a : b;
                        ParticipantRuntime black = aWhite ? b : a;

                        Debug.Log(
                            $"[AI Tournament] GAME START | {participants[i].displayName} vs {participants[j].displayName} | " +
                            $"Game {game + 1}/{totalGames} | " +
                            $"White={white.descriptor.displayName} | Black={black.descriptor.displayName} | " +
                            $"Seed={randomSeed + i * 100000 + j * 1000 + game}",
                            this);

                        System.Random rng = new System.Random(
                            randomSeed + i * 100000 + j * 1000 + game);

                        // Let Unity render the GAME START log before entering the synchronous MCTS search.
                        yield return null;

                        GameSimulationResult result;
                        try
                        {
                            result = SimulateGame(initialState, white, black, rng);
                        }
                        catch (Exception exception)
                        {
                            aborted = true;
                            Debug.LogError(
                                "[AI Tournament] GAME ABORTED because an AI inference/search failed. " +
                                "No fake fallback result will be recorded.\n" +
                                $"Pair={participants[i].displayName} vs {participants[j].displayName} | Game={game + 1}\n" +
                                exception,
                                this);
                            break;
                        }

                        summary.AddGame(
                            result.winner,
                            result.plies,
                            result.captures,
                            result.checks,
                            result.promotions);

                        summary.totalWhiteSearchMs += result.whiteThinkMs;
                        summary.totalBlackSearchMs += result.blackThinkMs;
                        summary.totalWhiteNodes += result.whiteNodes;
                        summary.totalBlackNodes += result.blackNodes;
                        summary.totalMctsSimulations += result.mctsSimulations;
                        summary.totalMctsSearchMs += result.mctsSearchMs;

                        if (perGameWriter != null)
                        {
                            perGameWriter.WriteLine(
                                string.Join(",",
                                    Escape(participants[i].displayName + " vs " + participants[j].displayName),
                                    game + 1,
                                    Escape(white.descriptor.displayName),
                                    Escape(black.descriptor.displayName),
                                    result.winner.HasValue ? result.winner.Value.ToString() : "DRAW",
                                    result.drawReason,
                                    result.plies,
                                    result.captures,
                                    result.checks,
                                    result.promotions,
                                    F(result.whiteThinkMs),
                                    F(result.blackThinkMs),
                                    result.whiteNodes,
                                    result.blackNodes,
                                    result.mctsSimulations,
                                    F(result.mctsSearchMs)));
                            perGameWriter.Flush();
                        }

                        if (logProgress)
                        {
                            string outcome = result.winner.HasValue
                                ? result.winner.Value.ToString()
                                : "DRAW";

                            Debug.Log(
                                "[AI Tournament] " +
                                $"{participants[i].displayName} vs {participants[j].displayName} | " +
                                $"Game {game + 1}/{totalGames} | " +
                                $"Winner={outcome} | " +
                                $"Reason={(result.winner.HasValue ? "Win" : result.drawReason)} | " +
                                $"Ply={result.plies}",
                                this);
                        }

                        // Yield every game so the Unity editor/player remains responsive.
                        yield return null;
                    }
                }
            }

            if (summaryWriter != null)
            {
                summaryWriter.WriteLine(
                    "White,Black,Games,WhiteWins,BlackWins,Draws,WhiteWinRate,BlackWinRate,DrawRate," +
                    "AveragePlies,AverageCaptures,AverageChecks,AveragePromotions,ScoreRate," +
                    "WhiteWins<=100,WhiteWins<=150,WhiteWins<=200,WhiteWins<=300," +
                    "BlackWins<=100,BlackWins<=150,BlackWins<=200,BlackWins<=300,TotalWhiteSearchMs," +
                    "TotalBlackSearchMs,TotalWhiteNodes,TotalBlackNodes,TotalMctsSimulations,TotalMctsSearchMs");

                for (int i = 0; i < _summaries.Count; i++)
                {
                    AITournamentMetrics m = _summaries[i];
                    summaryWriter.WriteLine(
                        string.Join(",",
                            Escape(m.playerWhite),
                            Escape(m.playerBlack),
                            m.games,
                            m.whiteWins,
                            m.blackWins,
                            m.draws,
                            F(m.WhiteWinRate),
                            F(m.BlackWinRate),
                            F(m.DrawRate),
                            F(m.AveragePlies),
                            F(m.AverageCaptures),
                            F(m.AverageChecks),
                            F(m.AveragePromotions),
                            F(m.ScoreRate),
                            m.whiteWinsUnder100,
                            m.whiteWinsUnder150,
                            m.whiteWinsUnder200,
                            m.whiteWinsUnder300,
                            m.blackWinsUnder100,
                            m.blackWinsUnder150,
                            m.blackWinsUnder200,
                            m.blackWinsUnder300,
                            F(m.totalWhiteSearchMs),
                            F(m.totalBlackSearchMs),
                            m.totalWhiteNodes,
                            m.totalBlackNodes,
                            m.totalMctsSimulations,
                            F(m.totalMctsSearchMs)));
                }

                summaryWriter.Flush();
            }

            if (overallWriter != null)
            {
                overallWriter.WriteLine(
                    "Participant,Games,Wins,Losses,Draws,WinRate,LossRate,DrawRate,ScoreRate," +
                    "AveragePlies,AverageCaptures,AverageChecks,AveragePromotions," +
                    "Wins<=100,Wins<=150,Wins<=200,Wins<=300");

                Dictionary<string, OverallStats> overall = BuildOverallStats();
                foreach (KeyValuePair<string, OverallStats> pair in overall)
                {
                    OverallStats s = pair.Value;
                    overallWriter.WriteLine(
                        string.Join(",",
                            Escape(pair.Key),
                            s.games,
                            s.wins,
                            s.losses,
                            s.draws,
                            F(s.Games > 0 ? (double)s.wins / s.games : 0.0),
                            F(s.Games > 0 ? (double)s.losses / s.games : 0.0),
                            F(s.Games > 0 ? (double)s.draws / s.games : 0.0),
                            F(s.Games > 0 ? (double)(s.wins + 0.5 * s.draws) / s.games : 0.0),
                            F(s.games > 0 ? (double)s.totalPlies / s.games : 0.0),
                            F(s.games > 0 ? (double)s.totalCaptures / s.games : 0.0),
                            F(s.games > 0 ? (double)s.totalChecks / s.games : 0.0),
                            F(s.games > 0 ? (double)s.totalPromotions / s.games : 0.0),
                            s.winsUnder100,
                            s.winsUnder150,
                            s.winsUnder200,
                            s.winsUnder300));
                }
                overallWriter.Flush();
            }
        }

        _tournamentCoroutine = null;

        LogOverallRanking();

        Debug.Log(
            "[AI Tournament] COMPLETE\n" +
            $"Games={ComputeTotalGames()} | " +
            $"Output={directory}",
            this);
    }

    private GameSimulationResult SimulateGame(
        GameState initialState,
        ParticipantRuntime white,
        ParticipantRuntime black,
        System.Random rng)
    {
        GameState state = initialState.Clone();
        MoveGenerator moveGenerator = new MoveGenerator();
        Dictionary<ulong, int> repetition = new Dictionary<ulong, int>(512);
        repetition[state.Hash] = 1;

        int plies = 0;
        int captures = 0;
        int checks = 0;
        int promotions = 0;
        int noProgress = 0;
        EPlayer? winner = null;
        string drawReason = "";

        double whiteThinkMs = 0.0;
        double blackThinkMs = 0.0;
        long whiteNodes = 0;
        long blackNodes = 0;
        int mctsSimulations = 0;
        double mctsSearchMs = 0.0;

        while (plies < Mathf.Max(1, maxPliesPerGame))
        {
            List<StateMove> legalMoves =
                moveGenerator.GenerateLegalMoves(state, state.SideToMove);

            if (legalMoves == null || legalMoves.Count == 0)
            {
                bool inCheck = moveGenerator.IsInCheck(state, state.SideToMove);
                if (inCheck)
                    winner = HexChessRules.GetOpponent(state.SideToMove);
                else
                    drawReason = "NoLegalMoves";
                break;
            }

            ParticipantRuntime current =
                state.SideToMove == EPlayer.WHITE ? white : black;

            MoveDecision decision = ChooseMove(
                current,
                state,
                legalMoves,
                repetition,
                noProgress,
                plies,
                rng);
            StateMove selectedMove = decision.move;

            if (!ContainsAction(legalMoves, selectedMove))
                selectedMove = legalMoves[rng.Next(legalMoves.Count)];

            if (state.SideToMove == EPlayer.WHITE)
            {
                whiteThinkMs += decision.milliseconds;
                whiteNodes += decision.nodes;
            }
            else
            {
                blackThinkMs += decision.milliseconds;
                blackNodes += decision.nodes;
            }

            mctsSimulations += decision.mctsSimulations;
            mctsSearchMs += decision.hasMcts ? decision.milliseconds : 0.0;

            if (selectedMove.IsCapture || selectedMove.IsEnPassant)
                captures++;
            if (selectedMove.IsPromotion)
                promotions++;

            moveGenerator.MakeMove(state, selectedMove);
            plies++;

            // Keep tournament terminal rules identical to PPO, Minimax and
            // AlphaZero: only configured insufficient-material cases draw;
            // material advantage alone is never an automatic win.
            if (HexChessRules.TryGetMaterialTerminalResult(
                    state,
                    out EPlayer? materialWinner,
                    out bool materialDraw))
            {
                if (materialDraw)
                    drawReason = "KingVsKing";
                else if (materialWinner.HasValue)
                    winner = materialWinner.Value;

                break;
            }

            bool progress =
                selectedMove.IsCapture ||
                selectedMove.IsEnPassant ||
                selectedMove.IsPromotion ||
                selectedMove.MovingPiece.Type == StatePieceType.PAWN;

            if (progress)
                noProgress = 0;
            else
                noProgress++;

            List<StateMove> opponentMoves =
                moveGenerator.GenerateLegalMoves(state, state.SideToMove);
            if (opponentMoves == null || opponentMoves.Count == 0)
            {
                if (moveGenerator.IsInCheck(state, state.SideToMove))
                    winner = HexChessRules.GetOpponent(state.SideToMove);
                else
                    drawReason = "NoLegalMoves";
                break;
            }

            if (moveGenerator.IsInCheck(state, state.SideToMove))
                checks++;

            if (repetition.TryGetValue(state.Hash, out int count))
                repetition[state.Hash] = count + 1;
            else
                repetition[state.Hash] = 1;

            if (repetition[state.Hash] >= Mathf.Max(2, repetitionDrawOccurrences))
            {
                drawReason = "Repetition";
                break;
            }

            if (noProgress >= Mathf.Max(1, noProgressDrawPlies))
            {
                drawReason = "NoProgress";
                break;
            }
        }

        if (!winner.HasValue && string.IsNullOrEmpty(drawReason) && plies >= maxPliesPerGame)
            drawReason = "MaxPlies";

        return new GameSimulationResult(
            winner,
            drawReason,
            plies,
            captures,
            checks,
            promotions,
            whiteThinkMs,
            blackThinkMs,
            whiteNodes,
            blackNodes,
            mctsSimulations,
            mctsSearchMs);
    }

    private MoveDecision ChooseMove(
        ParticipantRuntime player,
        GameState state,
        IReadOnlyList<StateMove> legalMoves,
        IReadOnlyDictionary<ulong, int> repetition,
        int noProgress,
        int plyNumber,
        System.Random rng)
    {
        if (player == null || player.descriptor == null)
            return new MoveDecision { move = legalMoves[0] };

        if (player.descriptor.kind == AIPlayerKind.MINIMAX_ALPHA_BETA)
        {
            MinimaxAI ai = new MinimaxAI(
                state.SideToMove,
                Mathf.Max(1, player.descriptor.minimaxDepth),
                player.descriptor.minimaxHistory,
                player.descriptor.minimaxKiller,
                player.descriptor.minimaxTranspositionTable,
                player.descriptor.minimaxAspiration);

            float start = Time.realtimeSinceStartup;
            StateMove move = ai.GetBestStateMove(state);
            double elapsed = (Time.realtimeSinceStartup - start) * 1000.0;

            return new MoveDecision
            {
                move = move,
                milliseconds = elapsed,
                nodes = ai.Statistics.Nodes
            };
        }

        if (player.descriptor.IsPpo)
        {
            if (player.ppo == null)
                return new MoveDecision { move = legalMoves[0] };

            float start = Time.realtimeSinceStartup;
            float[] policy = player.ppo.EvaluatePolicy(
                state,
                state.SideToMove,
                legalMoves);

            StateMove move = AlphaZeroSearch.ChooseMoveFromPolicy(
                state,
                legalMoves,
                policy,
                GetTournamentTemperature(plyNumber),
                rng);

            return new MoveDecision
            {
                move = move,
                milliseconds = (Time.realtimeSinceStartup - start) * 1000.0
            };
        }

        if (player.descriptor.IsAlphaZero)
        {
            if (player.alphaZero == null)
                return new MoveDecision { move = legalMoves[0] };

            AlphaZeroConfig config = new AlphaZeroConfig
            {
                simulations = Mathf.Max(1, player.descriptor.alphaZeroSimulations),
                maxDepth = Mathf.Max(1, player.descriptor.alphaZeroMaxDepth),
                cpuct = Mathf.Max(0.01f, player.descriptor.alphaZeroCpuct),
                policyTemperature = 1f,
                rootDirichletEpsilon = 0f,
                rootDirichletAlpha = 0.30f,
                enableRepetitionAwareness = true,
                repetitionCutoffOccurrences = Mathf.Max(2, repetitionDrawOccurrences),
                enableNoProgressAwareness = true,
                noProgressPlyThreshold = Mathf.Max(1, noProgressDrawPlies),
                inheritGameNoProgressIntoSearch = false,
                enableDiagnostics = false
            };

            AlphaZeroSearch search = new AlphaZeroSearch(player.alphaZero, config);
            MCTSSearchResult result = search.Search(state, repetition, noProgress);

            return new MoveDecision
            {
                move = AlphaZeroSearch.ChooseMove(
                    result,
                    legalMoves,
                    GetTournamentTemperature(plyNumber),
                    rng),
                milliseconds = result.SearchTimeMilliseconds,
                nodes = 0,
                mctsSimulations = result.CompletedSimulations,
                hasMcts = true
            };
        }

        return new MoveDecision { move = legalMoves[0] };
    }


    private float GetTournamentTemperature(int plyNumber)
    {
        if (openingRandomPlies > 0 && plyNumber < openingRandomPlies)
            return Mathf.Clamp(openingRandomTemperature, 0.05f, 1.0f);

        return Mathf.Max(0.01f, alphaZeroTemperature);
    }

    private GameState CaptureInitialState()
    {
        if (ChessBoard.Current == null)
        {
            Debug.LogError("[AI Tournament] ChessBoard.Current is null.", this);
            return null;
        }

        MonoBehaviour liveController = BaseGameController.Current as MonoBehaviour;
        bool restore = liveController != null && liveController.enabled;

        try
        {
            if (liveController != null)
                liveController.enabled = false;

            ChessBoard.Current.ResetBoard();
            if (BaseGameController.Current != null)
                BaseGameController.Current.ResetGameState();

            GameState initial = GameState.FromUnityBoard();
            if (initial == null)
                return null;

            MoveGenerator generator = new MoveGenerator();
            List<StateMove> legal = generator.GenerateLegalMoves(initial, initial.SideToMove);
            if (legal == null || legal.Count == 0)
            {
                Debug.LogError("[AI Tournament] Initial position has no legal moves.", this);
                return null;
            }

            return initial;
        }
        finally
        {
            if (liveController != null)
                liveController.enabled = restore;
        }
    }

    private bool PreflightPpoModels(
        IReadOnlyList<AIPlayerDescriptor> participants,
        GameState initialState)
    {
        if (participants == null || initialState == null)
            return false;

        MoveGenerator generator = new MoveGenerator();
        List<StateMove> legalMoves =
            generator.GenerateLegalMoves(initialState, initialState.SideToMove);

        if (legalMoves == null || legalMoves.Count == 0)
        {
            Debug.LogError(
                "[AI Tournament] PPO preflight cannot run because the initial position has no legal moves.",
                this);
            return false;
        }

        bool allOk = true;

        for (int i = 0; i < participants.Count; i++)
        {
            AIPlayerDescriptor participant = participants[i];
            if (participant == null || !participant.IsAlphaZero)
                continue;

            ShafranPPOBootstrapPolicyValueEvaluator bootstrap =
                participant.ppoBootstrapPolicyValueEvaluator as
                ShafranPPOBootstrapPolicyValueEvaluator;

            if (bootstrap == null)
                continue;

            if (!bootstrap.IsReady)
            {
                Debug.LogError(
                    $"[AI Tournament] PRE-FLIGHT {participant.displayName}: NOT READY. " +
                    "Assign the correct bootstrap component, PPO evaluator and heuristic evaluator.",
                    this);
                allOk = false;
                continue;
            }

            bool ok = bootstrap.ValidatePolicyInference(
                initialState,
                initialState.SideToMove,
                legalMoves,
                out string message);

            if (ok)
            {
                Debug.Log(
                    $"[AI Tournament] PRE-FLIGHT {participant.displayName}: OK | {message}",
                    this);
            }
            else
            {
                Debug.LogError(
                    $"[AI Tournament] PRE-FLIGHT {participant.displayName}: FAILED | {message}",
                    this);
                allOk = false;
            }
        }

        return allOk;
    }

    private void EnsureEvaluatorReferences()
    {
        if (ppoBaseEvaluator == null)
            ppoBaseEvaluator = FindAnyObjectByType<SentisPpoPolicyEvaluator>();

        if (alphaZeroBaseEvaluator == null)
            alphaZeroBaseEvaluator = FindAnyObjectByType<SentisDualHeadPolicyValueEvaluator>();
    }

    private List<AIPlayerDescriptor> BuildParticipants()
    {
        if (finalThreeOnly)
        {
            List<AIPlayerDescriptor> finalParticipants =
                new List<AIPlayerDescriptor>(3);

            if (enableModelAPrime && modelAPrimeEvaluator != null)
            {
                finalParticipants.Add(new AIPlayerDescriptor
                {
                    displayName = "AlphaZero_A_PPO_MCTS",
                    kind = AIPlayerKind.MODEL_A_PRIME,
                    ppoBootstrapPolicyValueEvaluator = modelAPrimeEvaluator,
                alphaZeroPolicyValueEvaluator = modelAPrimeEvaluator,
                    alphaZeroSimulations = alphaZeroSimulations,
                    alphaZeroMaxDepth = alphaZeroMaxDepth,
                    alphaZeroCpuct = alphaZeroCpuct
                });
            }

            if (enableModelBPrime && modelBPrimeEvaluator != null)
            {
                finalParticipants.Add(new AIPlayerDescriptor
                {
                    displayName = "AlphaZero_B_PPO_MCTS",
                    kind = AIPlayerKind.MODEL_B_PRIME,
                    ppoBootstrapPolicyValueEvaluator = modelBPrimeEvaluator,
                alphaZeroPolicyValueEvaluator = modelBPrimeEvaluator,
                    alphaZeroSimulations = alphaZeroSimulations,
                    alphaZeroMaxDepth = alphaZeroMaxDepth,
                    alphaZeroCpuct = alphaZeroCpuct
                });
            }

            if (enableModelCPrime && modelCPrimeEvaluator != null)
            {
                finalParticipants.Add(new AIPlayerDescriptor
                {
                    displayName = "AlphaZero_C_PPO_MCTS",
                    kind = AIPlayerKind.MODEL_C_PRIME,
                    ppoBootstrapPolicyValueEvaluator = modelCPrimeEvaluator,
                alphaZeroPolicyValueEvaluator = modelCPrimeEvaluator,
                    alphaZeroSimulations = alphaZeroSimulations,
                    alphaZeroMaxDepth = alphaZeroMaxDepth,
                    alphaZeroCpuct = alphaZeroCpuct
                });
            }

            return finalParticipants;
        }

        List<AIPlayerDescriptor> result = new List<AIPlayerDescriptor>(10);

        if (enableMinimax)
            result.Add(new AIPlayerDescriptor
            {
                displayName = "Minimax_AlphaBeta",
                kind = AIPlayerKind.MINIMAX_ALPHA_BETA,
                minimaxDepth = minimaxDepth,
                minimaxHistory = minimaxHistory,
                minimaxKiller = minimaxKiller,
                minimaxTranspositionTable = minimaxTranspositionTable,
                minimaxAspiration = minimaxAspiration
            });

        if (enablePpoBase && ppoBaseEvaluator != null)
            result.Add(new AIPlayerDescriptor
            {
                displayName = "PPO_Base",
                kind = AIPlayerKind.PPO_BASE,
                ppoPolicyEvaluator = ppoBaseEvaluator
            });

        if (enablePpoD1 && ppoD1Evaluator != null)
            result.Add(new AIPlayerDescriptor
            {
                displayName = "PPO_Minimax_D1",
                kind = AIPlayerKind.PPO_MINIMAX_D1,
                ppoPolicyEvaluator = ppoD1Evaluator
            });

        if (enablePpoD2 && ppoD2Evaluator != null)
            result.Add(new AIPlayerDescriptor
            {
                displayName = "PPO_Minimax_D2",
                kind = AIPlayerKind.PPO_MINIMAX_D2,
                ppoPolicyEvaluator = ppoD2Evaluator
            });

        if (enableAlphaZeroBase && alphaZeroBaseEvaluator != null)
            result.Add(new AIPlayerDescriptor
            {
                displayName = "AlphaZero_Base",
                kind = AIPlayerKind.ALPHAZERO_BASE,
                alphaZeroPolicyValueEvaluator = alphaZeroBaseEvaluator,
                alphaZeroSimulations = alphaZeroSimulations,
                alphaZeroMaxDepth = alphaZeroMaxDepth,
                alphaZeroCpuct = alphaZeroCpuct
            });

        if (enableAlphaZeroD1 && alphaZeroD1Evaluator != null)
            result.Add(new AIPlayerDescriptor
            {
                displayName = "AlphaZero_From_PPO_D1",
                kind = AIPlayerKind.ALPHAZERO_FROM_PPO_D1,
                alphaZeroPolicyValueEvaluator = alphaZeroD1Evaluator,
                alphaZeroSimulations = alphaZeroSimulations,
                alphaZeroMaxDepth = alphaZeroMaxDepth,
                alphaZeroCpuct = alphaZeroCpuct
            });

        if (enableAlphaZeroD2 && alphaZeroD2Evaluator != null)
            result.Add(new AIPlayerDescriptor
            {
                displayName = "AlphaZero_From_PPO_D2",
                kind = AIPlayerKind.ALPHAZERO_FROM_PPO_D2,
                alphaZeroPolicyValueEvaluator = alphaZeroD2Evaluator,
                alphaZeroSimulations = alphaZeroSimulations,
                alphaZeroMaxDepth = alphaZeroMaxDepth,
                alphaZeroCpuct = alphaZeroCpuct
            });

        if (enableModelAPrime && modelAPrimeEvaluator != null)
            result.Add(new AIPlayerDescriptor
            {
                displayName = "AlphaZero_A_PPO_MCTS",
                kind = AIPlayerKind.MODEL_A_PRIME,
                ppoBootstrapPolicyValueEvaluator = modelAPrimeEvaluator,
                alphaZeroPolicyValueEvaluator = modelAPrimeEvaluator,
                alphaZeroSimulations = alphaZeroSimulations,
                alphaZeroMaxDepth = alphaZeroMaxDepth,
                alphaZeroCpuct = alphaZeroCpuct
            });

        if (enableModelBPrime && modelBPrimeEvaluator != null)
            result.Add(new AIPlayerDescriptor
            {
                displayName = "AlphaZero_B_PPO_MCTS",
                kind = AIPlayerKind.MODEL_B_PRIME,
                ppoBootstrapPolicyValueEvaluator = modelBPrimeEvaluator,
                alphaZeroPolicyValueEvaluator = modelBPrimeEvaluator,
                alphaZeroSimulations = alphaZeroSimulations,
                alphaZeroMaxDepth = alphaZeroMaxDepth,
                alphaZeroCpuct = alphaZeroCpuct
            });

        if (enableModelCPrime && modelCPrimeEvaluator != null)
            result.Add(new AIPlayerDescriptor
            {
                displayName = "AlphaZero_C_PPO_MCTS",
                kind = AIPlayerKind.MODEL_C_PRIME,
                ppoBootstrapPolicyValueEvaluator = modelCPrimeEvaluator,
                alphaZeroPolicyValueEvaluator = modelCPrimeEvaluator,
                alphaZeroSimulations = alphaZeroSimulations,
                alphaZeroMaxDepth = alphaZeroMaxDepth,
                alphaZeroCpuct = alphaZeroCpuct
            });

        return result;
    }

    private ParticipantRuntime CreateRuntime(AIPlayerDescriptor descriptor)
    {
        if (descriptor == null)
            return null;

        IPolicyEvaluator ppo =
            descriptor.ppoPolicyEvaluator as IPolicyEvaluator;

        IPolicyValueEvaluator alphaZero =
            descriptor.alphaZeroPolicyValueEvaluator as IPolicyValueEvaluator;

        // Final A/B/C models use PPO policy + heuristic value + MCTS.
        if (descriptor.ppoBootstrapPolicyValueEvaluator != null)
        {
            ShafranPPOBootstrapPolicyValueEvaluator bootstrapComponent =
                descriptor.ppoBootstrapPolicyValueEvaluator as
                ShafranPPOBootstrapPolicyValueEvaluator;

            if (bootstrapComponent != null)
            {
                if (!bootstrapComponent.IsReady)
                {
                    Debug.LogError(
                        $"[AI Tournament] {descriptor.displayName} is NOT READY. " +
                        "Check its PPO evaluator, ONNX model, and heuristic evaluator.",
                        this);
                    return null;
                }

                alphaZero = bootstrapComponent;
            }
            else
            {
                IPolicyValueEvaluator bootstrap =
                    descriptor.ppoBootstrapPolicyValueEvaluator as IPolicyValueEvaluator;

                if (bootstrap != null)
                    alphaZero = bootstrap;
            }
        }

        return new ParticipantRuntime
        {
            descriptor = descriptor,
            ppo = ppo,
            alphaZero = alphaZero
        };
    }

    private static bool ContainsAction(
        IReadOnlyList<StateMove> moves,
        StateMove target)
    {
        int targetAction = AlphaZeroActionEncoder.Encode(target);
        if (targetAction < 0)
            return false;

        for (int i = 0; i < moves.Count; i++)
        {
            if (AlphaZeroActionEncoder.Encode(moves[i]) == targetAction)
                return true;
        }

        return false;
    }

    private string ResolveOutputDirectory()
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
            return Application.persistentDataPath;

        if (Path.IsPathRooted(outputDirectory))
            return outputDirectory;

        return Path.Combine(Application.persistentDataPath, outputDirectory);
    }

    private void LogOverallRanking()
    {
        Dictionary<string, OverallStats> overall = BuildOverallStats();
        List<KeyValuePair<string, OverallStats>> ranking =
            new List<KeyValuePair<string, OverallStats>>(overall);

        ranking.Sort((left, right) =>
        {
            double leftScore = left.Value.games > 0
                ? (double)(left.Value.wins + 0.5 * left.Value.draws) / left.Value.games
                : 0.0;
            double rightScore = right.Value.games > 0
                ? (double)(right.Value.wins + 0.5 * right.Value.draws) / right.Value.games
                : 0.0;

            int scoreCompare = rightScore.CompareTo(leftScore);
            if (scoreCompare != 0)
                return scoreCompare;

            double leftPly = left.Value.games > 0
                ? (double)left.Value.totalPlies / left.Value.games
                : double.MaxValue;
            double rightPly = right.Value.games > 0
                ? (double)right.Value.totalPlies / right.Value.games
                : double.MaxValue;
            return leftPly.CompareTo(rightPly);
        });

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("[AI Tournament] OVERALL RANKING");

        for (int i = 0; i < ranking.Count; i++)
        {
            OverallStats s = ranking[i].Value;
            double score = s.games > 0
                ? (double)(s.wins + 0.5 * s.draws) / s.games
                : 0.0;
            double avgPly = s.games > 0
                ? (double)s.totalPlies / s.games
                : 0.0;

            sb.Append(i + 1)
                .Append(". ")
                .Append(ranking[i].Key)
                .Append(" | Score=")
                .Append(F(score))
                .Append(" | W=")
                .Append(s.wins)
                .Append(" D=")
                .Append(s.draws)
                .Append(" L=")
                .Append(s.losses)
                .Append(" | AvgPly=")
                .Append(F(avgPly))
                .Append(" | Win<=100=")
                .Append(s.winsUnder100)
                .Append(" | Win<=200=")
                .Append(s.winsUnder200)
                .AppendLine();
        }

        Debug.Log(sb.ToString(), this);
    }

    private int ComputeTotalGames()
    {
        int count = 0;
        for (int i = 0; i < _summaries.Count; i++)
            count += _summaries[i].games;
        return count;
    }

    private Dictionary<string, OverallStats> BuildOverallStats()
    {
        Dictionary<string, OverallStats> result =
            new Dictionary<string, OverallStats>(StringComparer.Ordinal);

        for (int i = 0; i < _summaries.Count; i++)
        {
            AITournamentMetrics m = _summaries[i];

            OverallStats white = GetOrCreateOverall(result, m.playerWhite);
            white.games += m.games;
            white.wins += m.whiteWins;
            white.losses += m.blackWins;
            white.draws += m.draws;
            white.totalPlies += m.totalPlies;
            white.totalCaptures += m.totalCaptures;
            white.totalChecks += m.totalChecks;
            white.totalPromotions += m.totalPromotions;
            white.winsUnder100 += m.whiteWinsUnder100;
            white.winsUnder150 += m.whiteWinsUnder150;
            white.winsUnder200 += m.whiteWinsUnder200;
            white.winsUnder300 += m.whiteWinsUnder300;

            OverallStats black = GetOrCreateOverall(result, m.playerBlack);
            black.games += m.games;
            black.wins += m.blackWins;
            black.losses += m.whiteWins;
            black.draws += m.draws;
            black.totalPlies += m.totalPlies;
            black.totalCaptures += m.totalCaptures;
            black.totalChecks += m.totalChecks;
            black.totalPromotions += m.totalPromotions;
            black.winsUnder100 += m.blackWinsUnder100;
            black.winsUnder150 += m.blackWinsUnder150;
            black.winsUnder200 += m.blackWinsUnder200;
            black.winsUnder300 += m.blackWinsUnder300;
        }

        return result;
    }

    private static OverallStats GetOrCreateOverall(
        Dictionary<string, OverallStats> dictionary,
        string name)
    {
        if (!dictionary.TryGetValue(name, out OverallStats stats))
        {
            stats = new OverallStats();
            dictionary[name] = stats;
        }
        return stats;
    }

    private static string Escape(string value)
    {
        if (value == null)
            return "";

        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            return "\"" + value.Replace("\"", "\"\"") + "\"";

        return value;
    }

    private static string F(double value)
    {
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private sealed class OverallStats
    {
        public int games;
        public int wins;
        public int losses;
        public int draws;
        public long totalPlies;
        public long totalCaptures;
        public long totalChecks;
        public long totalPromotions;
        public long winsUnder100;
        public long winsUnder150;
        public long winsUnder200;
        public long winsUnder300;

        public int Games => games;
    }

    private sealed class GameSimulationResult
    {
        public readonly EPlayer? winner;
        public readonly string drawReason;
        public readonly int plies;
        public readonly int captures;
        public readonly int checks;
        public readonly int promotions;
        public readonly double whiteThinkMs;
        public readonly double blackThinkMs;
        public readonly long whiteNodes;
        public readonly long blackNodes;
        public readonly int mctsSimulations;
        public readonly double mctsSearchMs;

        public GameSimulationResult(
            EPlayer? winner,
            string drawReason,
            int plies,
            int captures,
            int checks,
            int promotions,
            double whiteThinkMs,
            double blackThinkMs,
            long whiteNodes,
            long blackNodes,
            int mctsSimulations,
            double mctsSearchMs)
        {
            this.winner = winner;
            this.drawReason = drawReason ?? "";
            this.plies = plies;
            this.captures = captures;
            this.checks = checks;
            this.promotions = promotions;
            this.whiteThinkMs = whiteThinkMs;
            this.blackThinkMs = blackThinkMs;
            this.whiteNodes = whiteNodes;
            this.blackNodes = blackNodes;
            this.mctsSimulations = mctsSimulations;
            this.mctsSearchMs = mctsSearchMs;
        }
    }
}
