using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EAIDifficulty
{
    EASY = 2,
    MEDIUM = 3,
    HARD = 4
}

public class AIGameController : BaseGameController
{
    [Header("AI Settings")]

    [SerializeField]
    private EPlayer aiPlayerSide =
        EPlayer.BLACK;

    [SerializeField]
    private EAIDifficulty difficulty =
        EAIDifficulty.MEDIUM;

    [Header("AI Delay")]

    [SerializeField]
    private float aiMoveDelay =
        0.3f;

    [Header("Minimax Search Budget")]
    [Min(0)]
    [SerializeField]
    private int minimaxSearchTimeLimitMs = 100;

    private MinimaxAI _minimaxAI;

    private Coroutine _aiCoroutine;

    private bool _aiThinking;

    public EPlayer AIPlayerSide =>
        aiPlayerSide;

    public EAIDifficulty Difficulty =>
        difficulty;

    // =========================================================
    // START
    // =========================================================

    protected override void Start()
    {
        base.Start();

        InitAI();

        ScheduleAIMoveIfNeeded();
    }

    // =========================================================
    // INIT AI
    // =========================================================

    private void InitAI()
    {
        int depth =
            Mathf.Max(
                1,
                (int)difficulty
            );

        _minimaxAI =
            new MinimaxAI(
                aiPlayerSide,
                depth,
                minimaxSearchTimeLimitMs
            );
    }

    // =========================================================
    // SET DIFFICULTY
    // =========================================================

    public void SetDifficulty(
        int level)
    {
        level =
            Mathf.Clamp(
                level,
                1,
                6
            );

        difficulty =
            (EAIDifficulty)level;

        InitAI();
    }

    public void SetDifficulty(
        EAIDifficulty newDifficulty)
    {
        difficulty =
            newDifficulty;

        InitAI();
    }

    // =========================================================
    // RESET
    // =========================================================

    public override void ResetGameState()
    {
        if (_aiCoroutine != null)
        {
            StopCoroutine(
                _aiCoroutine
            );

            _aiCoroutine =
                null;
        }

        _aiThinking =
            false;

        base.ResetGameState();

        InitAI();

        ScheduleAIMoveIfNeeded();
    }

    // =========================================================
    // SWITCH TURN
    // =========================================================

    public override void SwitchTurn()
    {
        base.SwitchTurn();

        ScheduleAIMoveIfNeeded();
    }

    // =========================================================
    // SCHEDULE AI
    // =========================================================

    private bool HasTwoMLAgents()
    {
        ChessAgent[] agents =
            FindObjectsByType<ChessAgent>();

        int activeAgents = 0;

        if (agents != null)
        {
            for (int i = 0; i < agents.Length; i++)
            {
                ChessAgent agent = agents[i];

                if (agent != null &&
                    agent.isActiveAndEnabled)
                {
                    activeAgents++;
                }
            }
        }

        return activeAgents >= 2;
    }

    /// <summary>
    /// Configure AIGameController as the external Alpha-Beta opponent for
    /// Model B (PPO vs Minimax).
    /// </summary>
    public void ConfigureTrainingOpponent(
        EPlayer opponentSide,
        int depth)
    {
        aiPlayerSide = opponentSide;
        difficulty =
            (EAIDifficulty)Mathf.Clamp(depth, 1, 4);

        UseThinkTimer = false;
        TrainingMode = true;
        UseMaxGamePlies = true;

        InitAI();
    }

    private void ScheduleAIMoveIfNeeded()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        // Training scene: two ML-Agents own both sides. Never let this
        // legacy Minimax controller make a third, competing move.
        if (HasTwoMLAgents())
        {
            return;
        }

        if (GameState !=
            EGameState.PLAYING)
        {
            return;
        }

        if (CurrentPlayer !=
            aiPlayerSide)
        {
            return;
        }

        if (_aiThinking)
        {
            return;
        }

        if (_aiCoroutine != null)
        {
            return;
        }

        _aiCoroutine =
            StartCoroutine(
                ExecuteAIMoveRoutine()
            );
    }

    // =========================================================
    // AI MOVE ROUTINE
    // =========================================================

    private IEnumerator ExecuteAIMoveRoutine()
    {
        _aiThinking =
            true;

        // IMPORTANT FOR PPO vs MINIMAX TRAINING:
        // StartCoroutine() may execute the IEnumerator immediately until
        // its first yield. Without an initial yield, Minimax can therefore
        // run INSIDE ChessAgent.OnActionReceived() while BasePiece.Attack()
        // is still unwinding. That creates a re-entrant two-ply turn switch
        // and can make the Unity board/search snapshot inconsistent.
        // Always hand control back to Unity for one frame before Minimax
        // starts. This is required even when aiMoveDelay == 0.
        yield return null;

        // -----------------------------------------------------
        // AI THINKING DELAY
        // -----------------------------------------------------

        if (aiMoveDelay > 0f)
        {
            yield return new WaitForSeconds(
                aiMoveDelay
            );
        }

        // -----------------------------------------------------
        // INIT AI IF NEEDED
        // -----------------------------------------------------

        if (_minimaxAI == null)
        {
            InitAI();
        }

        // -----------------------------------------------------
        // BOARD READY
        // -----------------------------------------------------

        if (ChessBoard.Current == null ||
            ChessBoard.Current.Cells == null)
        {
            Debug.LogError(
                "[AIGameController] " +
                "ChessBoard not ready."
            );

            FinishAIThinking();

            yield break;
        }

        // -----------------------------------------------------
        // GAME STILL VALID
        // -----------------------------------------------------

        if (GameState !=
            EGameState.PLAYING)
        {
            FinishAIThinking();

            yield break;
        }

        if (CurrentPlayer !=
            aiPlayerSide)
        {
            FinishAIThinking();

            yield break;
        }

        // -----------------------------------------------------
        // TERMINAL CHECK FOR THE MINIMAX SIDE
        // -----------------------------------------------------
        // Model B has only ONE active PPO ChessAgent. When PPO moves into
        // a checkmate/stalemate position against the Minimax side, the
        // ChessAgent is not the side-to-move and therefore its own
        // CheckMLAgentTerminalState() must not be expected to close the game.
        // Resolve that terminal state here before asking Minimax for a move.
        if (TryResolveAIOpponentTerminalState())
        {
            FinishAIThinking();
            yield break;
        }

        // -----------------------------------------------------
        // SEARCH
        // -----------------------------------------------------

        Move bestMove =
            _minimaxAI.GetBestMove();

        // -----------------------------------------------------
        // NO MOVE
        // -----------------------------------------------------

        if (bestMove == null)
        {
            Debug.LogWarning(
                "[AIGameController] " +
                "AI không tìm thấy nước đi."
            );

            FinishAIThinking();

            yield break;
        }

        // -----------------------------------------------------
        // TARGET CELL
        // -----------------------------------------------------

        Cell targetCell =
            ChessBoard.Current.GetCell(
                bestMove.To.X,
                bestMove.To.Y
            );

        if (targetCell == null)
        {
            Debug.LogError(
                "[AIGameController] " +
                "Không tìm thấy target Cell."
            );

            FinishAIThinking();

            yield break;
        }

        // -----------------------------------------------------
        // MOVE
        // -----------------------------------------------------

        if (bestMove.IsEnPassant &&
            bestMove.MovedPiece is P_Pawn pawn)
        {
            pawn.EnPassantAttack(
                targetCell
            );
        }
        else if (
            bestMove.CapturedPiece != null)
        {
            bestMove.MovedPiece.Attack(
                targetCell
            );
        }
        else
        {
            bestMove.MovedPiece.Move(
                targetCell
            );
        }

        // -----------------------------------------------------
        // FINISH
        // -----------------------------------------------------

        FinishAIThinking();
    }

    // =========================================================
    // MINIMAX-SIDE TERMINAL CHECK
    // =========================================================

    private bool TryResolveAIOpponentTerminalState()
    {
        if (GameState != EGameState.PLAYING)
        {
            return true;
        }

        global::GameState state =
        global::GameState.FromUnityBoard();

        if (state == null)
        {
            return false;
        }

        // Material draw is terminal for the search as well.
        EPlayer? materialWinner;
        bool materialDraw;

        if (HexChessRules.TryGetMaterialTerminalResult(
                state,
                out materialWinner,
                out materialDraw) &&
            materialDraw)
        {
            DrawGame(EDrawReason.InsufficientMaterial);

            ShafranDebug.Log(
                "[AIGameController] Minimax terminal | " +
                "Reason=InsufficientMaterial | Side=" +
                aiPlayerSide +
                " | Ply=" +
                GamePlyCount,
                this);

            return true;
        }

        MoveGenerator generator =
            new MoveGenerator();

        List<StateMove> legalMoves =
            generator.GenerateLegalMoves(
                state,
                aiPlayerSide
            );

        if (legalMoves != null &&
            legalMoves.Count > 0)
        {
            return false;
        }

        bool inCheck =
            generator.IsInCheck(
                state,
                aiPlayerSide
            );

        EPlayer winner =
            HexChessRules.GetOpponent(
                aiPlayerSide
            );

        if (inCheck)
        {
            GameOver(winner);

            ShafranDebug.Log(
                "[AIGameController] Minimax terminal | " +
                "CHECKMATE | Loser=" +
                aiPlayerSide +
                " | Winner=" +
                winner +
                " | Ply=" +
                GamePlyCount,
                this);
        }
        else
        {
            DrawGame(EDrawReason.Stalemate);

            ShafranDebug.Log(
                "[AIGameController] Minimax terminal | " +
                "STALEMATE | Side=" +
                aiPlayerSide +
                " | Ply=" +
                GamePlyCount,
                this);
        }

        return true;
    }

    // =========================================================
    // FINISH THINKING
    // =========================================================

    private void FinishAIThinking()
    {
        _aiThinking =
            false;

        _aiCoroutine =
            null;
    }

    // =========================================================
    // DISABLE / ENABLE SAFETY
    // =========================================================

    private void OnDisable()
    {
        if (_aiCoroutine != null)
        {
            StopCoroutine(
                _aiCoroutine
            );

            _aiCoroutine =
                null;
        }

        _aiThinking =
            false;
    }
}
