using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum EDrawReason
{
    Normal,
    MaxPlies,
    Repetition,
    FiftyMove,
    InsufficientMaterial,
    Stalemate
}

public class BaseGameController : MonoBehaviour
{
    public static BaseGameController Current;

    public const float TIME_TO_THINK = 30f;

    // =========================================================
    // UI
    // =========================================================

    [Header("UI")]
    public TMP_Text txt_time;
    public TMP_Text txt_current_player;

    // =========================================================
    // DEFERRED MAX-PLY DRAW
    // =========================================================

    private void LateUpdate()
    {
        // Fallback only. ChessAgent.Update() resolves the pending MaxPly
        // draw immediately after the final move's normal terminal checks.
        // This LateUpdate path protects non-ML/manual game modes and any
        // scene where no ChessAgent owns the final move.
        TryResolvePendingMaxPlyDraw();
    }

    /// <summary>
    /// Resolves the artificial MaxPly horizon only after all normal terminal
    /// rules for the final move have had a chance to run.
    ///
    /// Returns true when this call actually changed the game to GAME_OVER.
    /// The method is intentionally public so ChessAgent can call it from
    /// Update(), before BaseGameController.LateUpdate().
    /// </summary>
    public bool TryResolvePendingMaxPlyDraw()
    {
        if (!_maxPlyDrawPending)
        {
            return false;
        }

        if (GameState != EGameState.PLAYING)
        {
            _maxPlyDrawPending = false;
            return false;
        }

        if (!useMaxGamePlies ||
            _gamePlyCount < MaxGamePlies)
        {
            _maxPlyDrawPending = false;
            return false;
        }

        _maxPlyDrawPending = false;
        DrawGame(EDrawReason.MaxPlies);
        return true;
    }

    // =========================================================
    // TIMER
    // =========================================================

    [Header("Timer")]
    [SerializeField]
    private bool useThinkTimer = true;

    // =========================================================
    // TRAINING
    // =========================================================

    [Header("Training")]
    [SerializeField]
    private bool useMaxGamePlies = true;

    [SerializeField]
    private int maxGamePlies = 500;

    [Header("Self-Play / Training")]
    [SerializeField]
    private bool trainingMode = false;

    [SerializeField]
    private int repetitionDrawCount = 3;

    // =========================================================
    // REPETITION
    // =========================================================

    private readonly Dictionary<ulong, int> _positionVisitCounts =
        new Dictionary<ulong, int>(512);

    // Number of consecutive non-progress plies in the real game.
    // A progress move is a pawn move, capture, en-passant, or promotion.
    private int _noProgressPlyCount;

    // Defer the artificial max-ply draw until terminal rules have been
    // evaluated for the move that reached the horizon.
    private bool _maxPlyDrawPending;

    // =========================================================
    // INTERNAL STATE
    // =========================================================

    private float _time;
    private bool _timeOutIsCalled;

    private EGameState _gameState;

    private Coroutine _timerCoroutine;

    // IMPORTANT:
    // This is the ONLY source of truth for game ply.
    private int _gamePlyCount;

    private bool _trainingResultRecorded;

    // =========================================================
    // PUBLIC STATE
    // =========================================================

    public EPlayer CurrentPlayer { get; private set; }

    public EPlayer? Winner { get; private set; }

    public EDrawReason? DrawReason { get; private set; }

    public Move LastMove { get; private set; }

    public EGameState GameState
    {
        get => _gameState;
        set => _gameState = value;
    }

    public float CurrentThinkTime => _time;

    public float RemainingThinkTime =>
        Mathf.Max(
            0f,
            TIME_TO_THINK - _time
        );

    // =========================================================
    // GAME PLY
    // =========================================================

    public int GamePlyCount =>
        _gamePlyCount;

    public int MaxGamePlies
    {
        get => Mathf.Max(1, maxGamePlies);

        set =>
            maxGamePlies =
                Mathf.Max(1, value);
    }

    // =========================================================
    // SETTINGS
    // =========================================================

    public bool UseThinkTimer
    {
        get => useThinkTimer;
        set => useThinkTimer = value;
    }

    public bool UseMaxGamePlies
    {
        get => useMaxGamePlies;
        set => useMaxGamePlies = value;
    }

    public bool TrainingMode
    {
        get => trainingMode;
        set => trainingMode = value;
    }

    // =========================================================
    // MAX PLY
    // =========================================================

    public bool HasReachedMaxGamePlies()
    {
        if (!useMaxGamePlies)
        {
            return false;
        }

        return _gamePlyCount >= MaxGamePlies;
    }

    // =========================================================
    // REPETITION
    // =========================================================

    public bool IsThreefoldRepetition()
    {
        global::GameState state =
            global::GameState.FromUnityBoard();

        if (state == null)
        {
            return false;
        }

        return _positionVisitCounts.TryGetValue(
                   state.Hash,
                   out int count
               )
               &&
               count >= Mathf.Max(
                   2,
                   repetitionDrawCount
               );
    }

    /// <summary>
    /// Returns a copy of the real game's position-history table.
    /// The AlphaZero MCTS uses this snapshot to avoid searching into
    /// repetitions that have already occurred earlier in the game.
    /// </summary>
    public Dictionary<ulong, int> GetPositionVisitCountsSnapshot()
    {
        return new Dictionary<ulong, int>(
            _positionVisitCounts
        );
    }

    /// <summary>
    /// Returns the occurrence count from the authoritative game-level
    /// repetition history. Unlike the snapshot method, this does not
    /// allocate a new Dictionary.
    /// </summary>
    public int GetPositionVisitCount(ulong hash)
    {
        return _positionVisitCounts.TryGetValue(
                   hash,
                   out int count)
            ? count
            : 0;
    }

    /// <summary>
    /// Number of consecutive plies since the last progress move in the
    /// actual Unity game. This is intentionally read-only to the search.
    /// </summary>
    public int NoProgressPlyCount =>
        Mathf.Max(0, _noProgressPlyCount);

    // =========================================================
    // UNITY
    // =========================================================

    protected virtual void Awake()
    {
        Current = this;

        GameState = EGameState.PLAYING;

        Winner = null;

        DrawReason = null;

        LastMove = null;

        CurrentPlayer = EPlayer.WHITE;

        _time = 0f;

        _timeOutIsCalled = false;

        _gamePlyCount = 0;

        _noProgressPlyCount = 0;
        _maxPlyDrawPending = false;

        _trainingResultRecorded = false;

        _positionVisitCounts.Clear();
    }

    protected virtual void Start()
    {
        ResetGameState();

        StartTimer();
    }

    private void Update()
    {
        if (GameState != EGameState.PLAYING)
        {
            return;
        }

        if (!useThinkTimer)
        {
            return;
        }

        _time += Time.deltaTime;

        if (_time >= TIME_TO_THINK &&
            !_timeOutIsCalled)
        {
            TimeIsOut();
        }
    }

    // =========================================================
    // TIMER
    // =========================================================

    private void StartTimer()
    {
        if (_timerCoroutine != null)
        {
            StopCoroutine(_timerCoroutine);
        }

        _timerCoroutine =
            StartCoroutine(
                UpdateTimeUI()
            );
    }

    private IEnumerator UpdateTimeUI()
    {
        WaitForSeconds wait =
            new WaitForSeconds(0.1f);

        while (true)
        {
            if (GameState == EGameState.PLAYING &&
                useThinkTimer)
            {
                int remainingTime =
                    Mathf.Max(
                        0,
                        Mathf.CeilToInt(
                            TIME_TO_THINK - _time
                        )
                    );

                if (txt_time != null)
                {
                    txt_time.text =
                        remainingTime.ToString();
                }
            }

            yield return wait;
        }
    }

    // =========================================================
    // TRAINING METRICS
    // =========================================================

    private void RecordTrainingResultIfNeeded()
    {
        if (_trainingResultRecorded ||
            GameState != EGameState.GAME_OVER)
        {
            return;
        }

        _trainingResultRecorded = true;

    }

    // =========================================================
    // REGISTER SUCCESSFUL MOVE
    // =========================================================
    //
    // This method must be called exactly ONCE after a move
    // has actually been executed successfully.
    //
    // It is the ONLY place where GamePlyCount increments.
    //
    // =========================================================

    public void RegisterMove(Move move)
    {
        if (move == null)
        {
            return;
        }

        if (GameState != EGameState.PLAYING)
        {
            return;
        }

        LastMove = move;

        // =====================================================
        // EXACTLY ONE PLY
        // =====================================================

        _gamePlyCount++;

        // Keep a game-level no-progress clock so MCTS can start its
        // simulation from the real game's current history.
        bool progressMove =
            move.MovedPiece is P_Pawn ||
            move.CapturedPiece != null ||
            move.IsEnPassant ||
            move.IsPromotion;

        if (progressMove)
        {
            _noProgressPlyCount = 0;
        }
        else
        {
            _noProgressPlyCount++;
        }


        // =====================================================
        // MAX PLY
        // =====================================================
        //
        // Max-ply draw is deliberately resolved here.
        // ChessAgent.ResolveGameResult() will later distinguish
        // this from an ordinary draw.
        //
        // =====================================================

        if (useMaxGamePlies &&
            _gamePlyCount >= MaxGamePlies &&
            GameState == EGameState.PLAYING)
        {
            // Defer the artificial horizon draw. The just-played move may
            // have produced checkmate, stalemate, insufficient material,
            // or a captured-King win, and those terminal rules must win
            // over MaxPlies.
            _maxPlyDrawPending = true;
        }

        RecordTrainingResultIfNeeded();
    }

    // =========================================================
    // TIMER RESET
    // =========================================================

    protected virtual void ResetTimer()
    {
        _time = 0f;

        _timeOutIsCalled = false;

        UpdateTimeUIImmediately();
    }

    private void UpdateTimeUIImmediately()
    {
        if (txt_time == null)
        {
            return;
        }

        if (!useThinkTimer)
        {
            txt_time.text = "∞";
            return;
        }

        int remainingTime =
            Mathf.Max(
                0,
                Mathf.CeilToInt(
                    TIME_TO_THINK - _time
                )
            );

        txt_time.text =
            remainingTime.ToString();
    }

    // =========================================================
    // TIME OUT
    // =========================================================

    private void TimeIsOut()
    {
        if (_timeOutIsCalled ||
            GameState != EGameState.PLAYING)
        {
            return;
        }

        _timeOutIsCalled = true;

        EPlayer winner =
            CurrentPlayer == EPlayer.WHITE
                ? EPlayer.BLACK
                : EPlayer.WHITE;

        GameOver(winner);

        RecordTrainingResultIfNeeded();
    }

    // =========================================================
    // REPETITION
    // =========================================================

    private void RecordCurrentPositionAndCheckRepetition()
    {
        if (GameState != EGameState.PLAYING)
        {
            return;
        }

        global::GameState state =
            global::GameState.FromUnityBoard();

        if (state == null)
        {
            return;
        }

        if (_positionVisitCounts.TryGetValue(
                state.Hash,
                out int visitCount))
        {
            visitCount++;

            _positionVisitCounts[state.Hash] =
                visitCount;
        }
        else
        {
            visitCount = 1;

            _positionVisitCounts[state.Hash] =
                1;
        }

        if (visitCount >=
            Mathf.Max(
                2,
                repetitionDrawCount
            ))
        {
            DrawGame(EDrawReason.Repetition);
        }
    }

    // =========================================================
    // SWITCH TURN
    // =========================================================

    public virtual void SwitchTurn()
    {
        if (GameState == EGameState.GAME_OVER)
        {
            return;
        }

        CurrentPlayer =
            CurrentPlayer == EPlayer.WHITE
                ? EPlayer.BLACK
                : EPlayer.WHITE;

        RecordCurrentPositionAndCheckRepetition();

        if (GameState == EGameState.GAME_OVER)
        {
            return;
        }

        ResetTimer();

        UpdatePlayerUI();
    }

    // =========================================================
    // UI
    // =========================================================

    protected virtual void UpdatePlayerUI()
    {
        if (txt_current_player == null)
        {
            return;
        }

        switch (CurrentPlayer)
        {
            case EPlayer.WHITE:

                txt_current_player.text =
                    "Lượt: TRẮNG";

                txt_current_player.color =
                    Color.white;

                break;

            case EPlayer.BLACK:

                txt_current_player.text =
                    "Lượt: ĐEN";

                txt_current_player.color =
                    new Color(
                        0.2f,
                        0.2f,
                        0.2f,
                        1f
                    );

                break;
        }
    }

    // =========================================================
    // GAME STATE
    // =========================================================

    public EGameState CheckGameState()
    {
        return GameState;
    }

    // =========================================================
    // GAME OVER
    // =========================================================

    public virtual void GameOver(
        EPlayer winPlayer)
    {
        if (GameState == EGameState.GAME_OVER)
        {
            return;
        }

        _maxPlyDrawPending = false;

        Winner = winPlayer;
        DrawReason = null;
        _trainingResultRecorded = true;

        GameState =
            EGameState.GAME_OVER;


        EPlayer loser =
            winPlayer == EPlayer.WHITE
                ? EPlayer.BLACK
                : EPlayer.WHITE;

        Debug.Log(
            $"[RESULT] WIN={winPlayer} | LOSS={loser} | Ply={_gamePlyCount}"
        );
    }

    // =========================================================
    // DRAW
    // =========================================================

    public virtual void DrawGame(
        EDrawReason reason = EDrawReason.Normal)
    {
        if (GameState == EGameState.GAME_OVER)
        {
            return;
        }

        _maxPlyDrawPending = false;

        Winner = null;
        DrawReason = reason;
        _trainingResultRecorded = true;

        GameState =
            EGameState.GAME_OVER;


        Debug.Log(
            $"[RESULT] DRAW | Reason={reason} | Ply={_gamePlyCount}"
        );
    }

    // =========================================================
    // RESET
    // =========================================================

    public virtual void ResetGameState()
    {
        GameState =
            EGameState.PLAYING;

        CurrentPlayer =
            EPlayer.WHITE;

        Winner = null;
        DrawReason = null;

        LastMove = null;

        // SINGLE SOURCE OF TRUTH
        _gamePlyCount = 0;

        _noProgressPlyCount = 0;
        _maxPlyDrawPending = false;

        _trainingResultRecorded = false;

        ResetTimer();

        UpdatePlayerUI();

        // The board itself is reset by the training agent.  Do not hash the
        // old board here, otherwise a terminal position from the previous
        // episode can leak into the new episode history.
        _positionVisitCounts.Clear();
    }

    /// <summary>
    /// Rebuilds repetition history after the Unity board has actually been
    /// reset.  This must be called after ChessBoard.ResetBoard().
    /// </summary>
    public void ResetPositionHistoryFromCurrentBoard()
    {
        _positionVisitCounts.Clear();

        global::GameState initialState =
            global::GameState.FromUnityBoard();

        if (initialState != null)
        {
            _positionVisitCounts[initialState.Hash] = 1;
        }
    }

    // =========================================================
    // DESTROY
    // =========================================================

    protected virtual void OnDestroy()
    {
        if (Current == this)
        {
            Current = null;
        }

        _positionVisitCounts.Clear();

        if (_timerCoroutine != null)
        {
            StopCoroutine(
                _timerCoroutine
            );

            _timerCoroutine = null;
        }
    }
}