using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AlphaZeroGameController : BaseGameController
{
    [Header("AlphaZero")]
    [SerializeField]
    private EPlayer aiWhiteSide = EPlayer.WHITE;

    [SerializeField]
    private EPlayer aiBlackSide = EPlayer.BLACK;

    [SerializeField]
    private int simulations = 50;

    [SerializeField]
    private int maxSearchDepth = 64;

    [SerializeField]
    private float cpuct = 1.5f;

    [SerializeField]
    private float playTemperature = 0.05f;

    [SerializeField]
    private float moveDelay = 0.05f;

    [Header("Policy / Value Evaluator")]
    [SerializeField]
    private MonoBehaviour policyValueEvaluatorComponent;

    [Header("Debug")]
    [SerializeField]
    private bool debugLogs = true;

    private IPolicyValueEvaluator _evaluator;
    private AlphaZeroSearch _search;

    private Coroutine _moveCoroutine;

    private bool _thinking;
    private bool _initialized;

    // =========================================================
    // START
    // =========================================================

    protected override void Start()
    {
        base.Start();

        if (debugLogs)
        {
            ShafranDebug.Log(
                $"[AlphaZeroController] Start. " +
                $"GameState={GameState}, " +
                $"CurrentPlayer={CurrentPlayer}, " +
                $"ChessBoard={(ChessBoard.Current != null ? "EXISTS" : "NULL")}",
                this
            );
        }

        InitializeSearch();

        StartCoroutine(BootAndPlay());
    }

    // =========================================================
    // INITIALIZE SEARCH
    // =========================================================

    private void InitializeSearch()
    {
        // For the PPO -> Dual-Head -> MCTS pipeline, prefer the Sentis neural
        // evaluator automatically when the Inspector field has not been assigned.
        if (policyValueEvaluatorComponent == null)
        {
            // Bootstrap scenes may only have the PPO teacher. Prefer it so a
            // PPO policy model is never accidentally assigned to the dual-head
            // evaluator. The dual-head evaluator remains the fallback for the
            // final AlphaZero_A/B/C inference scene.
            ShafranPPOBootstrapPolicyValueEvaluator bootstrap =
                GetComponent<ShafranPPOBootstrapPolicyValueEvaluator>();

            if (bootstrap == null)
                bootstrap =
                    FindAnyObjectByType<ShafranPPOBootstrapPolicyValueEvaluator>();

            if (bootstrap != null)
            {
                policyValueEvaluatorComponent = bootstrap;
            }
            else
            {
                SentisDualHeadPolicyValueEvaluator neuralEvaluator =
                    GetComponent<SentisDualHeadPolicyValueEvaluator>();

                if (neuralEvaluator == null)
                    neuralEvaluator =
                        FindAnyObjectByType<SentisDualHeadPolicyValueEvaluator>();

                policyValueEvaluatorComponent = neuralEvaluator;
            }
        }

        if (policyValueEvaluatorComponent == null)
        {
            Debug.LogError(
                "[AlphaZeroController] Policy / Value Evaluator is NULL.\n" +
                "Add SentisDualHeadPolicyValueEvaluator and assign the Dual-Head ONNX model.",
                this
            );

            _search = null;
            _initialized = false;
            return;
        }

        _evaluator =
            policyValueEvaluatorComponent
                as IPolicyValueEvaluator;

        if (_evaluator == null)
        {
            Debug.LogError(
                "[AlphaZeroController] Assigned component does not implement " +
                "IPolicyValueEvaluator.",
                this
            );

            _search = null;
            _initialized = false;
            return;
        }

        SentisDualHeadPolicyValueEvaluator sentisEvaluator =
            _evaluator as SentisDualHeadPolicyValueEvaluator;

        if (sentisEvaluator != null && !sentisEvaluator.IsReady)
        {
            Debug.LogError(
                "[AlphaZeroController] Neural evaluator is not ready. " +
                "Check the Dual-Head ONNX ModelAsset/Resources path and Unity Inference Engine.",
                this
            );

            _search = null;
            _initialized = false;
            return;
        }

        ShafranPPOBootstrapPolicyValueEvaluator bootstrapEvaluator =
            _evaluator as ShafranPPOBootstrapPolicyValueEvaluator;

        if (bootstrapEvaluator != null && !bootstrapEvaluator.IsReady)
        {
            Debug.LogError(
                "[AlphaZeroController] PPO bootstrap evaluator is not ready. " +
                "Check the PPO Teacher ONNX and Heuristic evaluator assignments.",
                this
            );

            _search = null;
            _initialized = false;
            return;
        }

        AlphaZeroConfig config =
            new AlphaZeroConfig
            {
                simulations =
                    Mathf.Max(
                        1,
                        simulations
                    ),

                maxDepth =
                    Mathf.Max(
                        1,
                        maxSearchDepth
                    ),

                cpuct =
                    Mathf.Max(
                        0.01f,
                        cpuct
                    ),

                policyTemperature = 1f,

                rootDirichletEpsilon = 0f,

                rootDirichletAlpha = 0.30f
            };

        _search =
            new AlphaZeroSearch(
                _evaluator,
                config
            );

        _initialized = true;

        if (debugLogs)
        {
            ShafranDebug.Log(
                "[AlphaZeroController] Search initialized.\n" +
                $"Evaluator = {_evaluator.GetType().Name} | PPO-policy bootstrap / Dual-Head -> MCTS\n" +
                $"Simulations = {config.SafeSimulations}\n" +
                $"MaxDepth = {config.SafeMaxDepth}\n" +
                $"CPUCT = {config.SafeCpuct}",
                this
            );
        }
    }

    // =========================================================
    // BOOT
    // =========================================================

    private IEnumerator BootAndPlay()
    {
        if (debugLogs)
        {
            ShafranDebug.Log(
                "[AlphaZeroController] Waiting for ChessBoard..."
            );
        }

        // -----------------------------------------------------
        // Wait until ChessBoard exists and cells are generated.
        // Do NOT use WaitForSeconds here.
        // This works even if Time.timeScale == 0.
        // -----------------------------------------------------

        float timeout = 10f;
        float elapsed = 0f;

        while (true)
        {
            bool boardReady =
                ChessBoard.Current != null &&
                ChessBoard.Current.Cells != null &&
                ChessBoard.Current.AllCells != null &&
                ChessBoard.Current.AllCells.Count > 0;

            if (boardReady)
                break;

            elapsed += Time.unscaledDeltaTime;

            if (elapsed >= timeout)
            {
                Debug.LogError(
                    "[AlphaZeroController] ChessBoard was not initialized " +
                    $"within {timeout} seconds.",
                    this
                );

                yield break;
            }

            yield return null;
        }

        // -----------------------------------------------------
        // Wait one additional frame so all pieces are created.
        // -----------------------------------------------------

        yield return null;

        if (debugLogs)
        {
            ShafranDebug.Log(
                $"[AlphaZeroController] ChessBoard READY. " +
                $"Cells={ChessBoard.Current.AllCells.Count}",
                this
            );
        }

        // -----------------------------------------------------
        // Check GameState
        // -----------------------------------------------------

        if (GameState != EGameState.PLAYING)
        {
            Debug.LogWarning(
                $"[AlphaZeroController] Game is not PLAYING. " +
                $"Current state={GameState}",
                this
            );

            yield break;
        }

        // -----------------------------------------------------
        // Search must exist
        // -----------------------------------------------------

        if (!_initialized || _search == null)
        {
            Debug.LogError(
                "[AlphaZeroController] Search is not initialized.",
                this
            );

            yield break;
        }

        if (debugLogs)
        {
            ShafranDebug.Log(
                $"[AlphaZeroController] Starting AI turn. " +
                $"Player={CurrentPlayer}",
                this
            );
        }

        ScheduleMoveIfNeeded();
    }

    // =========================================================
    // TURN
    // =========================================================

    public override void SwitchTurn()
    {
        base.SwitchTurn();

        if (debugLogs &&
            GameState == EGameState.PLAYING)
        {
            ShafranDebug.Log(
                $"[AlphaZeroController] Turn switched → {CurrentPlayer}",
                this
            );
        }

        ScheduleMoveIfNeeded();
    }

    // =========================================================
    // RESET
    // =========================================================

    public override void ResetGameState()
    {
        StopThinking();

        base.ResetGameState();

        if (!_initialized)
        {
            InitializeSearch();
        }

        StartCoroutine(
            DelayedSchedule()
        );
    }

    private IEnumerator DelayedSchedule()
    {
        yield return null;

        ScheduleMoveIfNeeded();
    }

    // =========================================================
    // SHOULD AI MOVE?
    // =========================================================

    private bool IsAITurn()
    {
        if (CurrentPlayer == aiWhiteSide &&
            CurrentPlayer == EPlayer.WHITE)
        {
            return true;
        }

        if (CurrentPlayer == aiBlackSide &&
            CurrentPlayer == EPlayer.BLACK)
        {
            return true;
        }

        return false;
    }

    // =========================================================
    // SCHEDULE
    // =========================================================

    private void ScheduleMoveIfNeeded()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (GameState != EGameState.PLAYING)
        {
            if (debugLogs)
            {
                ShafranDebug.Log(
                    $"[AlphaZeroController] No move: GameState={GameState}",
                    this
                );
            }

            return;
        }

        if (_thinking)
        {
            return;
        }

        if (_search == null)
        {
            Debug.LogError(
                "[AlphaZeroController] No AlphaZeroSearch.",
                this
            );

            return;
        }

        if (!IsAITurn())
        {
            if (debugLogs)
            {
                ShafranDebug.Log(
                    $"[AlphaZeroController] Not AI turn. " +
                    $"CurrentPlayer={CurrentPlayer}, " +
                    $"AI White={aiWhiteSide}, " +
                    $"AI Black={aiBlackSide}",
                    this
                );
            }

            return;
        }

        if (_moveCoroutine != null)
        {
            return;
        }

        _moveCoroutine =
            StartCoroutine(
                ExecuteMoveRoutine()
            );
    }

    // =========================================================
    // AI MOVE
    // =========================================================

    private IEnumerator ExecuteMoveRoutine()
    {
        _thinking = true;

        if (debugLogs)
        {
            ShafranDebug.Log(
                $"[AlphaZeroController] AI THINKING → {CurrentPlayer}",
                this
            );
        }

        // -----------------------------------------------------
        // Delay theo unscaled time.
        // Không bị kẹt khi Time.timeScale = 0.
        // -----------------------------------------------------

        float delayElapsed = 0f;

        while (delayElapsed < moveDelay)
        {
            delayElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // -----------------------------------------------------
        // Validate board
        // -----------------------------------------------------

        if (GameState != EGameState.PLAYING)
        {
            Debug.LogWarning(
                "[AlphaZeroController] Game ended before AI move.",
                this
            );

            FinishThinking();
            yield break;
        }

        if (ChessBoard.Current == null)
        {
            Debug.LogError(
                "[AlphaZeroController] ChessBoard.Current is NULL.",
                this
            );

            FinishThinking();
            yield break;
        }

        // -----------------------------------------------------
        // Convert Unity board -> GameState
        // -----------------------------------------------------

        global::GameState state =
            global::GameState.FromUnityBoard();

        if (state == null)
        {
            Debug.LogError(
                "[AlphaZeroController] GameState.FromUnityBoard() returned NULL.",
                this
            );

            FinishThinking();
            yield break;
        }

        // -----------------------------------------------------
        // Make sure side-to-move matches controller
        // -----------------------------------------------------

        if (state.SideToMove != CurrentPlayer)
        {
            Debug.LogWarning(
                "[AlphaZeroController] SideToMove mismatch.\n" +
                $"Controller={CurrentPlayer}\n" +
                $"GameState={state.SideToMove}",
                this
            );
        }

        // -----------------------------------------------------
        // V10 insufficient-material terminal rule
        // -----------------------------------------------------

        if (HexChessRules.TryGetMaterialTerminalResult(
                state,
                out EPlayer? materialWinner,
                out bool materialDraw))
        {
            if (materialDraw)
            {
                ShafranDebug.Log(
                    "[AlphaZeroController] Insufficient-material draw.",
                    this
                );
                DrawGame(EDrawReason.InsufficientMaterial);
            }
            else if (materialWinner.HasValue)
            {
                ShafranDebug.Log(
                    $"[AlphaZeroController] Material elimination. " +
                    $"Winner={materialWinner.Value}",
                    this
                );
                GameOver(materialWinner.Value);
            }

            FinishThinking();
            yield break;
        }

        // -----------------------------------------------------
        // Legal moves
        // -----------------------------------------------------

        MoveGenerator moveGenerator =
            new MoveGenerator();

        List<StateMove> legalMoves =
            moveGenerator.GenerateLegalMoves(
                state,
                state.SideToMove
            );

        if (legalMoves == null ||
            legalMoves.Count == 0)
        {
            bool inCheck =
                moveGenerator.IsInCheck(
                    state,
                    state.SideToMove
                );

            if (inCheck)
            {
                EPlayer loser = CurrentPlayer;
                EPlayer winner = HexChessRules.GetOpponent(loser);

                Debug.LogWarning(
                    $"[AlphaZeroController] Checkmate. " +
                    $"Loser={loser}, Winner={winner}",
                    this
                );

                GameOver(winner);
            }
            else
            {
                ShafranDebug.Log(
                    $"[AlphaZeroController] Stalemate. " +
                    $"No legal moves for {CurrentPlayer}.",
                    this
                );

                DrawGame(EDrawReason.Stalemate);
            }

            FinishThinking();
            yield break;
        }

        if (debugLogs)
        {
            ShafranDebug.Log(
                $"[AlphaZeroController] " +
                $"Player={CurrentPlayer}, " +
                $"LegalMoves={legalMoves.Count}, " +
                $"Simulations={simulations}",
                this
            );
        }

        // -----------------------------------------------------
        // MCTS
        // -----------------------------------------------------

        MCTSSearchResult result;

        try
        {
            result =
                _search.Search(state);
        }
        catch (System.Exception exception)
        {
            Debug.LogError(
                "[AlphaZeroController] MCTS failed:\n" +
                exception,
                this
            );

            FinishThinking();
            yield break;
        }

        // -----------------------------------------------------
        // Choose move
        // -----------------------------------------------------

        StateMove selectedMove =
            AlphaZeroSearch.ChooseMove(
                result,
                legalMoves,
                playTemperature
            );

        if (debugLogs)
        {
            ShafranDebug.Log(
                $"[AlphaZeroController] " +
                $"MCTS BEST = " +
                $"{result.BestMove.From.X},{result.BestMove.From.Y}" +
                $" -> " +
                $"{result.BestMove.To.X},{result.BestMove.To.Y}",
                this
            );

            ShafranDebug.Log(
                $"[AlphaZeroController] " +
                $"SELECTED MOVE = " +
                $"{selectedMove.From.X},{selectedMove.From.Y}" +
                $" -> " +
                $"{selectedMove.To.X},{selectedMove.To.Y}",
                this
            );
        }

        // -----------------------------------------------------
        // Execute
        // -----------------------------------------------------

        EPlayer playerBeforeMove =
            CurrentPlayer;

        bool executed =
            ExecuteUnityMove(
                selectedMove
            );

        if (!executed)
        {
            Debug.LogError(
                "[AlphaZeroController] Failed to execute selected move.",
                this
            );

            FinishThinking();
            yield break;
        }

        // -----------------------------------------------------
        // Safety:
        // BasePiece.Move / Attack normally calls SwitchTurn().
        //
        // If it didn't, force the turn here.
        // -----------------------------------------------------

        if (GameState == EGameState.PLAYING &&
            CurrentPlayer == playerBeforeMove)
        {
            Debug.LogWarning(
                "[AlphaZeroController] Piece move did not switch turn. " +
                "Forcing SwitchTurn().",
                this
            );

            SwitchTurn();
        }

        FinishThinking();
    }

    // =========================================================
    // EXECUTE UNITY MOVE
    // =========================================================

    private bool ExecuteUnityMove(
        StateMove move)
    {
        if (ChessBoard.Current == null)
        {
            return false;
        }

        Cell fromCell =
            ChessBoard.Current.GetCell(
                move.From.X,
                move.From.Y
            );

        Cell toCell =
            ChessBoard.Current.GetCell(
                move.To.X,
                move.To.Y
            );

        if (fromCell == null)
        {
            Debug.LogError(
                $"[AlphaZeroController] FROM cell is NULL: " +
                $"({move.From.X},{move.From.Y})",
                this
            );

            return false;
        }

        if (toCell == null)
        {
            Debug.LogError(
                $"[AlphaZeroController] TO cell is NULL: " +
                $"({move.To.X},{move.To.Y})",
                this
            );

            return false;
        }

        if (fromCell.CurrentPiece == null)
        {
            Debug.LogError(
                $"[AlphaZeroController] No piece at FROM cell: " +
                $"({move.From.X},{move.From.Y})",
                this
            );

            return false;
        }

        BasePiece piece =
            fromCell.CurrentPiece;

        // -----------------------------------------------------
        // Safety check
        // -----------------------------------------------------

        if (piece.Player != CurrentPlayer)
        {
            Debug.LogError(
                "[AlphaZeroController] Selected piece belongs to " +
                $"{piece.Player}, but current player is {CurrentPlayer}.",
                this
            );

            return false;
        }

        // -----------------------------------------------------
        // En Passant
        // -----------------------------------------------------

        if (move.IsEnPassant &&
            piece is P_Pawn pawn)
        {
            pawn.EnPassantAttack(
                toCell
            );

            return true;
        }

        // -----------------------------------------------------
        // Capture
        // -----------------------------------------------------

        if (move.IsCapture)
        {
            piece.Attack(
                toCell
            );

            return true;
        }

        // -----------------------------------------------------
        // Normal move
        // -----------------------------------------------------

        piece.Move(
            toCell
        );

        return true;
    }

    // =========================================================
    // FINISH
    // =========================================================

    private void FinishThinking()
    {
        _thinking = false;
        _moveCoroutine = null;

        if (GameState != EGameState.PLAYING)
            return;

        if (!IsAITurn())
            return;

        StartCoroutine(ScheduleNextTurn());
    }
    // =========================================================
    // STOP
    // =========================================================

    private void StopThinking()
    {
        if (_moveCoroutine != null)
        {
            StopCoroutine(
                _moveCoroutine
            );

            _moveCoroutine = null;
        }

        _thinking = false;
    }

    // =========================================================
    // DESTROY
    // =========================================================

    protected override void OnDestroy()
    {
        StopThinking();

        base.OnDestroy();
    }

    private IEnumerator ScheduleNextTurn()
    {
        // Đợi 1 frame để BasePiece/Board hoàn tất cập nhật trạng thái.
        yield return null;

        if (GameState != EGameState.PLAYING)
            yield break;

        if (_thinking)
            yield break;

        ScheduleMoveIfNeeded();
    }
}