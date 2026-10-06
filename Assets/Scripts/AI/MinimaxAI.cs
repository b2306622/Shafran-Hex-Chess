using System.Collections.Generic;
using UnityEngine;

public class MinimaxAI
{
    // =========================================================
    // SETTINGS
    // =========================================================

    private readonly EPlayer _aiPlayer;
    private readonly int _maxDepth;

    // Training-time search budget: keeps Unity responsive without collapsing search depth.
    private readonly int _searchTimeLimitMs;
    private System.Diagnostics.Stopwatch _searchStopwatch;
    private bool _searchTimedOut;

    // =========================================================
    // ABLATION CONFIGURATION
    //
    // BASELINE
    //   History    = false
    //   Killer     = false
    //   TT         = false
    //   Aspiration = false
    //
    // + HISTORY
    //   History    = true
    //   Killer     = false
    //   TT         = false
    //   Aspiration = false
    //
    // + KILLER
    //   History    = true
    //   Killer     = true
    //   TT         = false
    //   Aspiration = false
    //
    // + TT
    //   History    = true
    //   Killer     = true
    //   TT         = true
    //   Aspiration = false
    //
    // + ASPIRATION
    //   History    = true
    //   Killer     = true
    //   TT         = true
    //   Aspiration = true
    // =========================================================

    private readonly bool _enableHistory;
    private readonly bool _enableKiller;
    private readonly bool _enableTT;
    private readonly bool _enableAspiration;

    // =========================================================
    // SEARCH SETTINGS
    // =========================================================

    private const int MAX_QUIESCENCE_DEPTH = 3;

    private const int MAX_SEARCH_PLY = 64;

    private const int KILLER_MOVE_SLOTS = 2;

    private const int HISTORY_MAX = 1000000;

    // =========================================================
    // ASPIRATION WINDOW
    // =========================================================

    private const float ASPIRATION_INITIAL_WINDOW = 150f;

    private const int MAX_ASPIRATION_RESEARCHES = 2;

    // =========================================================
    // AI SYSTEMS
    // =========================================================

    private readonly MoveGenerator _moveGenerator;

    private readonly Evaluation _evaluation;

    private readonly TranspositionTable _transpositionTable;

    private readonly SearchStatistics _statistics;

    // =========================================================
    // KILLER MOVES
    // =========================================================

    private readonly StateMove?[,] _killerMoves;

    // =========================================================
    // HISTORY HEURISTIC
    // =========================================================

    private readonly Dictionary<long, int> _history;

    // =========================================================
    // PRINCIPAL VARIATION
    // =========================================================

    private readonly List<StateMove> _principalVariation;

    // =========================================================
    // PUBLIC
    // =========================================================

    public SearchStatistics Statistics
    {
        get
        {
            return _statistics;
        }
    }

    public bool AspirationEnabled
    {
        get
        {
            return _enableAspiration;
        }
    }

    public bool HistoryEnabled
    {
        get
        {
            return _enableHistory;
        }
    }

    public bool KillerEnabled
    {
        get
        {
            return _enableKiller;
        }
    }

    public bool TTEnabled
    {
        get
        {
            return _enableTT;
        }
    }

    // =========================================================
    // MAIN CONSTRUCTOR
    // =========================================================

    public MinimaxAI(
        EPlayer aiPlayer,
        int maxDepth,
        bool enableHistory,
        bool enableKiller,
        bool enableTT,
        bool enableAspiration,
        int searchTimeLimitMs = 0)
    {
        _aiPlayer =
            aiPlayer;

        _maxDepth =
            Mathf.Max(
                1,
                maxDepth
            );

        _enableHistory =
            enableHistory;

        _enableKiller =
            enableKiller;

        _enableTT =
            enableTT;

        _enableAspiration =
            enableAspiration;

        _searchTimeLimitMs =
            Mathf.Max(0, searchTimeLimitMs);

        _searchStopwatch =
            new System.Diagnostics.Stopwatch();

        _moveGenerator =
            new MoveGenerator();

        _evaluation =
            new Evaluation(
                _aiPlayer
            );

        // TT vẫn được tạo ở mọi configuration,
        // nhưng chỉ được đọc/ghi khi _enableTT == true.
        // Guidance searches are intentionally shallow. A smaller TT avoids
        // unnecessary memory pressure and GC while keeping normal deeper
        // Minimax searches unchanged.
        int transpositionCapacity =
            _maxDepth <= 2
                ? 20000
                : 200000;

        _transpositionTable =
            new TranspositionTable(
                transpositionCapacity
            );

        _statistics =
            new SearchStatistics();

        _killerMoves =
            new StateMove?[
                MAX_SEARCH_PLY,
                KILLER_MOVE_SLOTS
            ];

        _history =
            new Dictionary<long, int>(
                4096
            );

        _principalVariation =
            new List<StateMove>(
                MAX_SEARCH_PLY
            );
    }

    // Training constructor: same search heuristics, but with a bounded wall-clock budget.
    public MinimaxAI(
        EPlayer aiPlayer,
        int maxDepth,
        int searchTimeLimitMs)
        : this(
            aiPlayer,
            maxDepth,
            true,
            true,
            true,
            true,
            searchTimeLimitMs)
    {
    }

    // =========================================================
    // COMPATIBILITY CONSTRUCTOR
    //
    // Giữ tương thích với code cũ:
    // History + Killer + TT + Aspiration
    // =========================================================

    public MinimaxAI(
        EPlayer aiPlayer,
        int maxDepth)
        : this(
            aiPlayer,
            maxDepth,
            true,
            true,
            true,
            true)
    {
    }

    // =========================================================
    // COMPATIBILITY CONSTRUCTOR
    //
    // Tham số bool cũ được hiểu là enableAspiration.
    // =========================================================

    public MinimaxAI(
        EPlayer aiPlayer,
        int maxDepth,
        bool enableAspiration)
        : this(
            aiPlayer,
            maxDepth,
            true,
            true,
            true,
            enableAspiration)
    {
    }

    // =========================================================
    // GET BEST STATE MOVE - FIXED DEPTH
    //
    // Used by PPO guidance only. Skips iterative deepening and final PV
    // reconstruction so the teacher signal stays cheap.
    // =========================================================

    public StateMove GetBestStateMoveFixedDepth(
        GameState state)
    {
        _statistics.Start();

        _searchTimedOut = false;
        _searchStopwatch.Restart();

        _statistics.SetDepth(
            _maxDepth
        );

        _principalVariation.Clear();

        if (state == null)
        {
            _statistics.SetResult(
                default(StateMove),
                0f
            );

            _statistics.SetPrincipalVariation(
                _principalVariation
            );

            _statistics.Stop();
            _searchStopwatch.Stop();
            return default(StateMove);
        }

        List<StateMove> rootMoves =
            _moveGenerator.GenerateLegalMoves(
                state,
                state.SideToMove
            );

        if (
            rootMoves == null ||
            rootMoves.Count == 0)
        {
            _statistics.SetResult(
                default(StateMove),
                0f
            );

            _statistics.SetPrincipalVariation(
                _principalVariation
            );

            _statistics.Stop();
            _searchStopwatch.Stop();
            return default(StateMove);
        }

        ResetSearchHeuristics();

        if (_enableTT)
        {
            _transpositionTable.NewSearch();
        }

        StateMove bestMove =
            rootMoves[0];

        float bestScore =
            float.NegativeInfinity;

        List<StateMove> principalVariation;

        bool completed =
            SearchRoot(
                state,
                rootMoves,
                _maxDepth,
                float.NegativeInfinity,
                float.PositiveInfinity,
                out bestMove,
                out bestScore,
                out principalVariation
            );

        if (completed)
        {
            _principalVariation.Clear();

            if (
                principalVariation != null &&
                principalVariation.Count > 0)
            {
                _principalVariation.AddRange(
                    principalVariation
                );
            }

            _statistics.SetCompletedDepth(
                _maxDepth
            );
        }
        else
        {
            bestMove = rootMoves[0];
            bestScore = 0f;
        }

        _statistics.SetResult(
            bestMove,
            bestScore
        );

        _statistics.SetPrincipalVariation(
            _principalVariation
        );

        _statistics.Stop();
        _searchStopwatch.Stop();

        return bestMove;
    }

    // =========================================================
    // GET BEST STATE MOVE
    // =========================================================

    public StateMove GetBestStateMove(
        GameState state)
    {
        _statistics.Start();

        _searchTimedOut = false;
        _searchStopwatch.Restart();

        _statistics.SetDepth(
            _maxDepth
        );

        _principalVariation.Clear();

        if (state == null)
        {
            _statistics.SetResult(
                default(StateMove),
                0f
            );

            _statistics.SetPrincipalVariation(
                _principalVariation
            );

            _statistics.Stop();

            return default(StateMove);
        }

        List<StateMove> rootMoves =
            _moveGenerator.GenerateLegalMoves(
                state,
                state.SideToMove
            );

        if (
            rootMoves == null ||
            rootMoves.Count == 0)
        {
            _statistics.SetResult(
                default(StateMove),
                0f
            );

            _statistics.SetPrincipalVariation(
                _principalVariation
            );

            _statistics.Stop();

            return default(StateMove);
        }

        // =====================================================
        // RESET SEARCH HEURISTICS
        // =====================================================

        ResetSearchHeuristics();

        if (_enableTT)
        {
            _transpositionTable.NewSearch();
        }

        // =====================================================
        // FALLBACK
        // =====================================================

        StateMove bestCompletedMove =
            rootMoves[0];

        float bestCompletedScore =
            float.NegativeInfinity;

        int completedDepth = 0;

        // =====================================================
        // ITERATIVE DEEPENING
        // =====================================================

        for (
            int currentDepth = 1;
            currentDepth <= _maxDepth;
            currentDepth++)
        {
            StateMove currentBestMove =
                bestCompletedMove;

            float currentBestScore =
                bestCompletedScore;

            List<StateMove> currentPV =
                new List<StateMove>();

            bool completed = false;

            // =================================================
            // FULL WINDOW
            //
            // Depth 1 luôn dùng full window.
            // Baseline cũng luôn dùng full window.
            // =================================================

            if (
                currentDepth == 1 ||
                completedDepth == 0 ||
                !_enableAspiration)
            {
                completed =
                    SearchRoot(
                        state,
                        rootMoves,
                        currentDepth,
                        float.NegativeInfinity,
                        float.PositiveInfinity,
                        out currentBestMove,
                        out currentBestScore,
                        out currentPV
                    );
            }
            else
            {
                // =================================================
                // ASPIRATION WINDOW
                // =================================================

                float center =
                    bestCompletedScore;

                float alpha =
                    center -
                    ASPIRATION_INITIAL_WINDOW;

                float beta =
                    center +
                    ASPIRATION_INITIAL_WINDOW;

                int researchCount = 0;

                bool hadReSearch = false;

                while (true)
                {
                    _statistics.AddAspirationAttempt();

                    completed =
                        SearchRoot(
                            state,
                            rootMoves,
                            currentDepth,
                            alpha,
                            beta,
                            out currentBestMove,
                            out currentBestScore,
                            out currentPV
                        );

                    if (!completed)
                    {
                        break;
                    }

                    // =================================================
                    // SUCCESS
                    // =================================================

                    if (
                        currentBestScore > alpha &&
                        currentBestScore < beta)
                    {
                        break;
                    }

                    // =================================================
                    // FAIL LOW
                    // =================================================

                    if (currentBestScore <= alpha)
                    {
                        _statistics.AddAspirationFailLow();

                        researchCount++;

                        if (
                            researchCount >
                            MAX_ASPIRATION_RESEARCHES)
                        {
                            completed = false;
                            break;
                        }

                        _statistics.AddAspirationReSearch();

                        hadReSearch = true;

                        float distance =
                            beta - alpha;

                        alpha =
                            currentBestScore -
                            distance;

                        continue;
                    }

                    // =================================================
                    // FAIL HIGH
                    // =================================================

                    if (currentBestScore >= beta)
                    {
                        _statistics.AddAspirationFailHigh();

                        researchCount++;

                        if (
                            researchCount >
                            MAX_ASPIRATION_RESEARCHES)
                        {
                            completed = false;
                            break;
                        }

                        _statistics.AddAspirationReSearch();

                        hadReSearch = true;

                        float distance =
                            beta - alpha;

                        beta =
                            currentBestScore +
                            distance;

                        continue;
                    }

                    break;
                }

                if (hadReSearch)
                {
                    _statistics.MarkSearchWithAspirationReSearch();
                }

                // =================================================
                // FULL WINDOW FALLBACK
                // =================================================

                if (!completed)
                {
                    completed =
                        SearchRoot(
                            state,
                            rootMoves,
                            currentDepth,
                            float.NegativeInfinity,
                            float.PositiveInfinity,
                            out currentBestMove,
                            out currentBestScore,
                            out currentPV
                        );
                }
            }

            // =====================================================
            // ITERATION FAILED
            // =====================================================

            if (!completed)
            {
                break;
            }

            // =====================================================
            // SAVE COMPLETED RESULT
            // =====================================================

            bestCompletedMove =
                currentBestMove;

            bestCompletedScore =
                currentBestScore;

            completedDepth =
                currentDepth;

            // =====================================================
            // SAVE PV
            // =====================================================

            _principalVariation.Clear();

            if (
                currentPV != null &&
                currentPV.Count > 0)
            {
                _principalVariation.AddRange(
                    currentPV
                );
            }

            // =====================================================
            // BEST MOVE FIRST
            // =====================================================

            rootMoves.Remove(
                currentBestMove
            );

            rootMoves.Insert(
                0,
                currentBestMove
            );

            // =====================================================
            // STATISTICS
            // =====================================================

            _statistics.SetCompletedDepth(
                completedDepth
            );

            _statistics.SetPrincipalVariation(
                _principalVariation
            );
        }

        // =====================================================
        // REBUILD PV FROM TT
        //
        // Chỉ được làm khi TT thực sự bật.
        // =====================================================

        if (_enableTT)
        {
            List<StateMove> finalPV =
                BuildPrincipalVariation(
                    state,
                    completedDepth
                );

            if (
                finalPV != null &&
                finalPV.Count > 0)
            {
                _principalVariation.Clear();

                _principalVariation.AddRange(
                    finalPV
                );
            }
        }

        // =====================================================
        // FINAL RESULT
        // =====================================================

        _statistics.SetResult(
            bestCompletedMove,
            bestCompletedScore
        );

        _statistics.SetPrincipalVariation(
            _principalVariation
        );

        _statistics.Stop();
        _searchStopwatch.Stop();

        return bestCompletedMove;
    }

    // =========================================================
    // GET UNITY MOVE
    // =========================================================

    public Move GetBestMove()
    {
        GameState state =
            GameState.FromUnityBoard();

        if (state == null)
        {
            _statistics.Start();

            _statistics.SetDepth(
                _maxDepth
            );

            _statistics.SetResult(
                default(StateMove),
                0f
            );

            _statistics.SetPrincipalVariation(
                new List<StateMove>()
            );

            _statistics.Stop();

            return null;
        }

        StateMove bestStateMove =
            GetBestStateMove(
                state
            );

        // Statistics are still collected internally, but printing them for every
        // Minimax move severely slows PPO training in the Unity Editor.

        List<StateMove> legalMoves =
            _moveGenerator.GenerateLegalMoves(
                state,
                state.SideToMove
            );

        if (
            legalMoves == null ||
            legalMoves.Count == 0)
        {
            return null;
        }

        bool validMove = false;

        foreach (
            StateMove legalMove in legalMoves)
        {
            if (
                AreSameMove(
                    bestStateMove,
                    legalMove
                ))
            {
                validMove = true;
                break;
            }
        }

        if (!validMove)
        {
            Debug.LogError(
                "[MinimaxAI] Best StateMove is illegal."
            );

            Debug.LogError(
                $"Move: {FormatMove(bestStateMove)}"
            );

            return null;
        }

        return ConvertToUnityMove(
            bestStateMove
        );
    }

    // =========================================================
    // ROOT SEARCH
    // =========================================================

    private bool SearchRoot(
        GameState state,
        List<StateMove> rootMoves,
        int depth,
        float alpha,
        float beta,
        out StateMove bestMove,
        out float bestScore,
        out List<StateMove> principalVariation)
    {
        bestMove =
            rootMoves != null &&
            rootMoves.Count > 0
                ? rootMoves[0]
                : default(StateMove);

        bestScore =
            float.NegativeInfinity;

        principalVariation =
            new List<StateMove>();

        if (
            state == null ||
            rootMoves == null ||
            rootMoves.Count == 0)
        {
            return false;
        }

        if (HexChessRules.TryGetMaterialTerminalResult(
                state,
                out EPlayer? materialWinner,
                out bool materialDraw))
        {
            return false;
        }

        float originalAlpha =
            alpha;

        float originalBeta =
            beta;

        ulong rootHash =
            state.Hash;

        // =====================================================
        // TT BEST MOVE
        // =====================================================

        StateMove? ttBestMove =
            null;

        if (_enableTT)
        {
            TTEntry rootEntry;

            if (
                _transpositionTable.TryGet(
                    rootHash,
                    out rootEntry))
            {
                ttBestMove =
                    rootEntry.BestMove;
            }
        }

        // =====================================================
        // MOVE ORDERING
        // =====================================================

        OrderMoves(
            state,
            rootMoves,
            ttBestMove,
            0
        );

        // =====================================================
        // SEARCH ROOT MOVES
        // =====================================================

        foreach (
            StateMove move in rootMoves)
        {
            GameState child =
                state.Clone();

            _moveGenerator.MakeMove(
                child,
                move
            );

            List<StateMove> childPV;

            float score =
                Search(
                    child,
                    depth - 1,
                    alpha,
                    beta,
                    1,
                    out childPV
                );

            // =================================================
            // ROOT MAX
            // =================================================

            if (score > bestScore)
            {
                bestScore =
                    score;

                bestMove =
                    move;

                principalVariation.Clear();

                principalVariation.Add(
                    move
                );

                if (
                    childPV != null &&
                    childPV.Count > 0)
                {
                    principalVariation.AddRange(
                        childPV
                    );
                }
            }

            if (score > alpha)
            {
                alpha =
                    score;
            }

            // =================================================
            // ROOT CUTOFF
            // =================================================

            if (alpha >= beta)
            {
                _statistics.AddCutoff();

                if (
                    !move.IsCapture &&
                    !move.IsPromotion &&
                    !move.IsCastling &&
                    !move.IsEnPassant)
                {
                    if (_enableKiller)
                    {
                        AddKillerMove(
                            0,
                            move
                        );
                    }

                    if (_enableHistory)
                    {
                        AddHistory(
                            move,
                            depth
                        );
                    }
                }

                break;
            }
        }

        // =====================================================
        // TT FLAG
        // =====================================================

        if (_enableTT)
        {
            TTFlag flag;

            if (bestScore <= originalAlpha)
            {
                flag =
                    TTFlag.UPPER_BOUND;
            }
            else if (bestScore >= originalBeta)
            {
                flag =
                    TTFlag.LOWER_BOUND;
            }
            else
            {
                flag =
                    TTFlag.EXACT;
            }

            // =================================================
            // STORE TT
            // =================================================

            _transpositionTable.Store(
                rootHash,
                new TTEntry(
                    depth,
                    bestScore,
                    flag,
                    bestMove
                )
            );
        }

        return true;
    }

    // =========================================================
    // SEARCH
    // =========================================================

    private float Search(
        GameState state,
        int depth,
        float alpha,
        float beta,
        int ply,
        out List<StateMove> principalVariation)
    {
        if (IsSearchTimeExceeded())
        {
            principalVariation = new List<StateMove>();
            return 0f;
        }

        principalVariation =
            new List<StateMove>();

        _statistics.AddNode();

        // V10 insufficient-material draw rule. K+Q/R/etc. vs K is
        // NOT terminal; it must continue until checkmate.
        if (HexChessRules.TryGetMaterialTerminalResult(
                state,
                out EPlayer? materialWinner,
                out bool materialDraw))
        {
            _statistics.AddLeafNode();

            if (materialDraw)
            {
                return 0f;
            }

            return materialWinner == _aiPlayer
                ? 1000000f + ply
                : -1000000f - ply;
        }

        // =====================================================
        // DEPTH LIMIT
        // =====================================================

        if (depth <= 0)
        {
            return QuiescenceSearch(
                state,
                alpha,
                beta,
                MAX_QUIESCENCE_DEPTH,
                ply
            );
        }

        // =====================================================
        // HASH
        // =====================================================

        ulong hash =
            state.Hash;

        // =====================================================
        // TT LOOKUP
        // =====================================================

        StateMove? ttBestMove =
            null;

        if (_enableTT)
        {
            TTEntry entry;

            if (
                _transpositionTable.TryGet(
                    hash,
                    out entry))
            {
                _statistics.AddTTHit();

                // TT move vẫn được dùng cho move ordering
                ttBestMove =
                    entry.BestMove;

                // TT bound cutoff
                if (entry.Depth >= depth)
                {
                    switch (entry.Flag)
                    {
                        case TTFlag.EXACT:
                        {
                            principalVariation.Add(
                                entry.BestMove
                            );

                            return entry.Score;
                        }

                        case TTFlag.LOWER_BOUND:
                        {
                            if (entry.Score > alpha)
                            {
                                alpha =
                                    entry.Score;
                            }

                            break;
                        }

                        case TTFlag.UPPER_BOUND:
                        {
                            if (entry.Score < beta)
                            {
                                beta =
                                    entry.Score;
                            }

                            break;
                        }
                    }

                    if (alpha >= beta)
                    {
                        _statistics.AddCutoff();

                        return entry.Score;
                    }
                }
            }
            else
            {
                _statistics.AddTTMiss();
            }
        }

        // =====================================================
        // LEGAL MOVES
        // =====================================================

        List<StateMove> legalMoves =
            _moveGenerator.GenerateLegalMoves(
                state,
                state.SideToMove
            );

        // =====================================================
        // TERMINAL
        // =====================================================

        if (
            legalMoves == null ||
            legalMoves.Count == 0)
        {
            _statistics.AddLeafNode();

            bool inCheck =
                _moveGenerator.IsInCheck(
                    state,
                    state.SideToMove
                );

            if (inCheck)
            {
                if (
                    state.SideToMove ==
                    _aiPlayer)
                {
                    return
                        -1000000f -
                        depth;
                }

                return
                    1000000f +
                    depth;
            }

            return 0f;
        }

        // =====================================================
        // SAVE ORIGINAL WINDOW
        // =====================================================

        float originalAlpha =
            alpha;

        float originalBeta =
            beta;

        // =====================================================
        // MOVE ORDERING
        // =====================================================

        OrderMoves(
            state,
            legalMoves,
            ttBestMove,
            ply
        );

        // =====================================================
        // MAX / MIN
        // =====================================================

        bool maximizing =
            state.SideToMove ==
            _aiPlayer;

        float bestScore =
            maximizing
                ? float.NegativeInfinity
                : float.PositiveInfinity;

        StateMove bestMove =
            legalMoves[0];

        // =====================================================
        // CHILDREN
        // =====================================================

        foreach (
            StateMove move in legalMoves)
        {
            GameState child =
                state.Clone();

            _moveGenerator.MakeMove(
                child,
                move
            );

            List<StateMove> childPV;

            float score =
                Search(
                    child,
                    depth - 1,
                    alpha,
                    beta,
                    ply + 1,
                    out childPV
                );

            // =================================================
            // MAX NODE
            // =================================================

            if (maximizing)
            {
                if (score > bestScore)
                {
                    bestScore =
                        score;

                    bestMove =
                        move;

                    principalVariation.Clear();

                    principalVariation.Add(
                        move
                    );

                    if (
                        childPV != null &&
                        childPV.Count > 0)
                    {
                        principalVariation.AddRange(
                            childPV
                        );
                    }
                }

                if (score > alpha)
                {
                    alpha =
                        score;
                }
            }

            // =================================================
            // MIN NODE
            // =================================================

            else
            {
                if (score < bestScore)
                {
                    bestScore =
                        score;

                    bestMove =
                        move;

                    principalVariation.Clear();

                    principalVariation.Add(
                        move
                    );

                    if (
                        childPV != null &&
                        childPV.Count > 0)
                    {
                        principalVariation.AddRange(
                            childPV
                        );
                    }
                }

                if (score < beta)
                {
                    beta =
                        score;
                }
            }

            // =================================================
            // ALPHA-BETA CUTOFF
            // =================================================

            if (alpha >= beta)
            {
                _statistics.AddCutoff();

                if (
                    !move.IsCapture &&
                    !move.IsPromotion &&
                    !move.IsCastling &&
                    !move.IsEnPassant)
                {
                    if (_enableKiller)
                    {
                        AddKillerMove(
                            ply,
                            move
                        );
                    }

                    if (_enableHistory)
                    {
                        AddHistory(
                            move,
                            depth
                        );
                    }
                }

                break;
            }
        }

        // =====================================================
        // STORE TT
        // =====================================================

        if (_enableTT)
        {
            TTFlag flag;

            if (bestScore <= originalAlpha)
            {
                flag =
                    TTFlag.UPPER_BOUND;
            }
            else if (bestScore >= originalBeta)
            {
                flag =
                    TTFlag.LOWER_BOUND;
            }
            else
            {
                flag =
                    TTFlag.EXACT;
            }

            _transpositionTable.Store(
                hash,
                new TTEntry(
                    depth,
                    bestScore,
                    flag,
                    bestMove
                )
            );
        }

        return bestScore;
    }

    private bool IsSearchTimeExceeded()
    {
        if (_searchTimeLimitMs <= 0)
        {
            return false;
        }

        if (_searchTimedOut)
        {
            return true;
        }

        if (_searchStopwatch.ElapsedMilliseconds >= _searchTimeLimitMs)
        {
            _searchTimedOut = true;
            return true;
        }

        return false;
    }

    // =========================================================
    // QUIESCENCE SEARCH
    // =========================================================

    private float QuiescenceSearch(
        GameState state,
        float alpha,
        float beta,
        int quiescenceDepth,
        int ply)
    {
        if (IsSearchTimeExceeded())
        {
            return 0f;
        }

        _statistics.AddNode();

        if (HexChessRules.TryGetMaterialTerminalResult(
                state,
                out EPlayer? materialWinner,
                out bool materialDraw))
        {
            _statistics.AddLeafNode();

            if (materialDraw)
            {
                return 0f;
            }

            return materialWinner == _aiPlayer
                ? 1000000f + ply
                : -1000000f - ply;
        }

        // =====================================================
        // CHECK
        // =====================================================

        bool inCheck =
            _moveGenerator.IsInCheck(
                state,
                state.SideToMove
            );

        // =====================================================
        // IN CHECK
        // =====================================================

        if (inCheck)
        {
            List<StateMove> evasions =
                _moveGenerator.GenerateLegalMoves(
                    state,
                    state.SideToMove
                );

            if (
                evasions == null ||
                evasions.Count == 0)
            {
                _statistics.AddLeafNode();

                if (
                    state.SideToMove ==
                    _aiPlayer)
                {
                    return -1000000f;
                }

                return 1000000f;
            }

            if (quiescenceDepth <= 0)
            {
                _statistics.AddLeafNode();

                return _evaluation.Evaluate(
                    state
                );
            }

            OrderMoves(
                state,
                evasions,
                null,
                ply
            );

            bool maximizing =
                state.SideToMove ==
                _aiPlayer;

            float bestScore =
                maximizing
                    ? float.NegativeInfinity
                    : float.PositiveInfinity;

            foreach (
                StateMove move in evasions)
            {
                GameState child =
                    state.Clone();

                _moveGenerator.MakeMove(
                    child,
                    move
                );

                float score =
                    QuiescenceSearch(
                        child,
                        alpha,
                        beta,
                        quiescenceDepth - 1,
                        ply + 1
                    );

                if (maximizing)
                {
                    if (score > bestScore)
                    {
                        bestScore =
                            score;
                    }

                    if (score > alpha)
                    {
                        alpha =
                            score;
                    }
                }
                else
                {
                    if (score < bestScore)
                    {
                        bestScore =
                            score;
                    }

                    if (score < beta)
                    {
                        beta =
                            score;
                    }
                }

                if (alpha >= beta)
                {
                    _statistics.AddCutoff();

                    break;
                }
            }

            return bestScore;
        }

        // =====================================================
        // STAND PAT
        // =====================================================

        float standPat =
            _evaluation.Evaluate(
                state
            );

        if (quiescenceDepth <= 0)
        {
            _statistics.AddLeafNode();

            return standPat;
        }

        bool maximizingSide =
            state.SideToMove ==
            _aiPlayer;

        if (maximizingSide)
        {
            if (standPat >= beta)
            {
                _statistics.AddCutoff();

                return standPat;
            }

            if (standPat > alpha)
            {
                alpha =
                    standPat;
            }
        }
        else
        {
            if (standPat <= alpha)
            {
                _statistics.AddCutoff();

                return standPat;
            }

            if (standPat < beta)
            {
                beta =
                    standPat;
            }
        }

        // =====================================================
        // CAPTURE MOVES ONLY
        // =====================================================

        List<StateMove> captureMoves =
            _moveGenerator.GenerateLegalCaptureMoves(
                state,
                state.SideToMove
            );

        if (
            captureMoves == null ||
            captureMoves.Count == 0)
        {
            _statistics.AddLeafNode();

            return standPat;
        }

        // =====================================================
        // IMPORTANT
        //
        // Quiescence vẫn dùng basic tactical ordering:
        // Promotion / Capture / En-passant / Castling.
        //
        // History / Killer / TT chỉ ảnh hưởng nếu được bật
        // thông qua OrderMoves().
        // =====================================================

        OrderMoves(
            state,
            captureMoves,
            null,
            ply
        );

        float bestCaptureScore =
            standPat;

        foreach (
            StateMove move in captureMoves)
        {
            GameState child =
                state.Clone();

            _moveGenerator.MakeMove(
                child,
                move
            );

            float score =
                QuiescenceSearch(
                    child,
                    alpha,
                    beta,
                    quiescenceDepth - 1,
                    ply + 1
                );

            if (maximizingSide)
            {
                if (score > bestCaptureScore)
                {
                    bestCaptureScore =
                        score;
                }

                if (score > alpha)
                {
                    alpha =
                        score;
                }
            }
            else
            {
                if (score < bestCaptureScore)
                {
                    bestCaptureScore =
                        score;
                }

                if (score < beta)
                {
                    beta =
                        score;
                }
            }

            if (alpha >= beta)
            {
                _statistics.AddCutoff();

                break;
            }
        }

        return bestCaptureScore;
    }

    // =========================================================
    // BUILD PRINCIPAL VARIATION
    // =========================================================

    private List<StateMove> BuildPrincipalVariation(
        GameState rootState,
        int depth)
    {
        List<StateMove> pv =
            new List<StateMove>();

        // PV từ TT chỉ hợp lệ khi TT bật.
        if (
            !_enableTT ||
            rootState == null ||
            depth <= 0)
        {
            return pv;
        }

        GameState state =
            rootState.Clone();

        HashSet<ulong> visited =
            new HashSet<ulong>();

        for (
            int ply = 0;
            ply < depth;
            ply++)
        {
            ulong hash =
                state.Hash;

            // =================================================
            // LOOP PROTECTION
            // =================================================

            if (!visited.Add(hash))
            {
                break;
            }

            // =================================================
            // GET TT ENTRY
            // =================================================

            TTEntry entry;

            if (
                !_transpositionTable.TryGet(
                    hash,
                    out entry))
            {
                break;
            }

            StateMove candidate =
                entry.BestMove;

            // =================================================
            // LEGAL MOVES
            // =================================================

            List<StateMove> legalMoves =
                _moveGenerator.GenerateLegalMoves(
                    state,
                    state.SideToMove
                );

            if (
                legalMoves == null ||
                legalMoves.Count == 0)
            {
                break;
            }

            // =================================================
            // VALIDATE
            // =================================================

            bool valid =
                false;

            StateMove validMove =
                default(StateMove);

            foreach (
                StateMove legalMove in legalMoves)
            {
                if (
                    AreSameMove(
                        candidate,
                        legalMove
                    ))
                {
                    valid =
                        true;

                    validMove =
                        legalMove;

                    break;
                }
            }

            if (!valid)
            {
                break;
            }

            pv.Add(
                validMove
            );

            // =================================================
            // APPLY
            // =================================================

            _moveGenerator.MakeMove(
                state,
                validMove
            );
        }

        return pv;
    }

    // =========================================================
    // MOVE ORDERING
    // =========================================================

    private void OrderMoves(
        GameState state,
        List<StateMove> moves,
        StateMove? ttBestMove,
        int ply)
    {
        if (
            moves == null ||
            moves.Count <= 1)
        {
            return;
        }

        moves.Sort(
            (a, b) =>
            {
                int scoreA =
                    GetMoveOrderingScore(
                        state,
                        a,
                        ttBestMove,
                        ply
                    );

                int scoreB =
                    GetMoveOrderingScore(
                        state,
                        b,
                        ttBestMove,
                        ply
                    );

                return
                    scoreB.CompareTo(
                        scoreA
                    );
            }
        );
    }

    // =========================================================
    // MOVE ORDERING SCORE
    // =========================================================

    private int GetMoveOrderingScore(
        GameState state,
        StateMove move,
        StateMove? ttBestMove,
        int ply)
    {
        int score =
            0;

        // =====================================================
        // BASIC MOVE ORDERING
        //
        // Có mặt ở TẤT CẢ cấu hình.
        //
        // Đây không phải History/Killer/TT.
        // =====================================================

        // -----------------------------------------------------
        // PROMOTION
        // -----------------------------------------------------

        if (move.IsPromotion)
        {
            score +=
                900000;

            score +=
                GetPieceValue(
                    move.PromotionType
                );
        }

        // -----------------------------------------------------
        // CAPTURE MVV-LVA
        // -----------------------------------------------------

        if (move.IsCapture)
        {
            int capturedValue =
                GetPieceValue(
                    move.CapturedPiece.Type
                );

            int attackerValue =
                GetPieceValue(
                    move.MovingPiece.Type
                );

            score +=
                500000 +
                capturedValue * 100 -
                attackerValue;
        }

        // -----------------------------------------------------
        // CHECKING MOVE
        // -----------------------------------------------------
        // Checks are searched before quiet moves. Evaluation still decides
        // whether the check is actually good, so this only improves search
        // ordering/pruning rather than forcing checks.

        if (IsCheckingMove(state, move))
        {
            score += 250000;
        }

        // -----------------------------------------------------
        // EN PASSANT
        // -----------------------------------------------------

        if (move.IsEnPassant)
        {
            score +=
                400000;
        }

        // -----------------------------------------------------
        // CASTLING
        // -----------------------------------------------------

        if (move.IsCastling)
        {
            score +=
                10000;
        }

        // =====================================================
        // TRANSPOSITION TABLE
        //
        // Chỉ khi TT bật.
        // =====================================================

        if (
            _enableTT &&
            ttBestMove.HasValue &&
            AreSameMove(
                move,
                ttBestMove.Value
            ))
        {
            score +=
                10000000;
        }

        // =====================================================
        // KILLER #1
        //
        // Chỉ khi Killer bật.
        // =====================================================

        if (_enableKiller)
        {
            if (
                IsKillerMove(
                    ply,
                    move,
                    0))
            {
                score +=
                    80000;
            }

            // =================================================
            // KILLER #2
            // =================================================

            if (
                IsKillerMove(
                    ply,
                    move,
                    1))
            {
                score +=
                    70000;
            }
        }

        // =====================================================
        // HISTORY
        //
        // Chỉ khi History bật.
        // =====================================================

        if (_enableHistory)
        {
            score +=
                GetHistory(
                    move
                );
        }

        return score;
    }

    private bool IsCheckingMove(
        GameState state,
        StateMove move)
    {
        if (state == null)
        {
            return false;
        }

        GameState child =
            state.Clone();

        _moveGenerator.MakeMove(
            child,
            move
        );

        return _moveGenerator.IsInCheck(
            child,
            HexChessRules.GetOpponent(
                state.SideToMove
            )
        );
    }

    // =========================================================
    // KILLER MOVE
    // =========================================================

    private void AddKillerMove(
        int ply,
        StateMove move)
    {
        if (!_enableKiller)
        {
            return;
        }

        if (
            ply < 0 ||
            ply >= MAX_SEARCH_PLY)
        {
            return;
        }

        if (
            IsKillerMove(
                ply,
                move,
                0))
        {
            return;
        }

        _killerMoves[
            ply,
            1
        ] =
            _killerMoves[
                ply,
                0
            ];

        _killerMoves[
            ply,
            0
        ] =
            move;
    }

    private bool IsKillerMove(
        int ply,
        StateMove move,
        int slot)
    {
        if (!_enableKiller)
        {
            return false;
        }

        if (
            ply < 0 ||
            ply >= MAX_SEARCH_PLY ||
            slot < 0 ||
            slot >= KILLER_MOVE_SLOTS)
        {
            return false;
        }

        if (
            !_killerMoves[
                ply,
                slot
            ].HasValue)
        {
            return false;
        }

        return AreSameMove(
            move,
            _killerMoves[
                ply,
                slot
            ].Value
        );
    }

    // =========================================================
    // HISTORY
    // =========================================================

    private long GetMoveKey(
        StateMove move)
    {
        unchecked
        {
            long key =
                17;

            key =
                key * 31 +
                move.From.X;

            key =
                key * 31 +
                move.From.Y;

            key =
                key * 31 +
                move.To.X;

            key =
                key * 31 +
                move.To.Y;

            key =
                key * 31 +
                (int)move.MovingPiece.Type;

            key =
                key * 31 +
                (int)move.MovingPiece.Player;

            return key;
        }
    }

    private int GetHistory(
        StateMove move)
    {
        if (!_enableHistory)
        {
            return 0;
        }

        long key =
            GetMoveKey(
                move
            );

        int value;

        if (
            _history.TryGetValue(
                key,
                out value))
        {
            return value;
        }

        return 0;
    }

    private void AddHistory(
        StateMove move,
        int depth)
    {
        if (!_enableHistory)
        {
            return;
        }

        long key =
            GetMoveKey(
                move
            );

        int bonus =
            depth *
            depth *
            16;

        int current;

        if (
            !_history.TryGetValue(
                key,
                out current))
        {
            current =
                0;
        }

        current +=
            bonus;

        if (
            current >
            HISTORY_MAX)
        {
            current =
                HISTORY_MAX;
        }

        _history[key] =
            current;
    }

    // =========================================================
    // RESET SEARCH HEURISTICS
    // =========================================================

    private void ResetSearchHeuristics()
    {
        // History
        _history.Clear();

        // Killer
        for (
            int ply = 0;
            ply < MAX_SEARCH_PLY;
            ply++)
        {
            for (
                int slot = 0;
                slot < KILLER_MOVE_SLOTS;
                slot++)
            {
                _killerMoves[
                    ply,
                    slot
                ] =
                    null;
            }
        }
    }

    // =========================================================
    // PIECE VALUE
    // =========================================================

    private int GetPieceValue(
        StatePieceType type)
    {
        switch (type)
        {
            case StatePieceType.PAWN:
                return 100;

            case StatePieceType.KNIGHT:
                return 350;

            case StatePieceType.BISHOP:
                return 350;

            case StatePieceType.ROOK:
                return 500;

            case StatePieceType.QUEEN:
                return 1000;

            case StatePieceType.KING:
                return 20000;

            default:
                return 0;
        }
    }

    // =========================================================
    // SAME MOVE
    // =========================================================

    private bool AreSameMove(
        StateMove a,
        StateMove b)
    {
        if (
            a.From.X != b.From.X ||
            a.From.Y != b.From.Y ||
            a.To.X != b.To.X ||
            a.To.Y != b.To.Y)
        {
            return false;
        }

        if (
            a.IsPromotion !=
            b.IsPromotion)
        {
            return false;
        }

        if (
            a.IsPromotion &&
            a.PromotionType !=
            b.PromotionType)
        {
            return false;
        }

        return true;
    }

    // =========================================================
    // FORMAT MOVE
    // =========================================================

    private string FormatMove(
        StateMove move)
    {
        return
            $"({move.From.X},{move.From.Y}) -> " +
            $"({move.To.X},{move.To.Y})";
    }

    // =========================================================
    // CONVERT TO UNITY MOVE
    // =========================================================

    private Move ConvertToUnityMove(
        StateMove stateMove)
    {
        if (ChessBoard.Current == null)
        {
            Debug.LogError(
                "[MinimaxAI] ChessBoard.Current is null."
            );

            return null;
        }

        Cell fromCell =
            ChessBoard.Current.GetCell(
                stateMove.From.X,
                stateMove.From.Y
            );

        Cell toCell =
            ChessBoard.Current.GetCell(
                stateMove.To.X,
                stateMove.To.Y
            );

        if (
            fromCell == null ||
            toCell == null)
        {
            Debug.LogError(
                "[MinimaxAI] " +
                "Không tìm thấy From/To Cell."
            );

            return null;
        }

        BasePiece movedPiece =
            fromCell.CurrentPiece;

        if (movedPiece == null)
        {
            Debug.LogError(
                "[MinimaxAI] " +
                "From Cell không có quân."
            );

            return null;
        }

        BasePiece capturedPiece =
            null;

        if (stateMove.IsCapture)
        {
            Cell capturedCell =
                ChessBoard.Current.GetCell(
                    stateMove.CapturedLocation.X,
                    stateMove.CapturedLocation.Y
                );

            if (capturedCell != null)
            {
                capturedPiece =
                    capturedCell.CurrentPiece;
            }
        }

        Move move =
            new Move(
                new CLocation(
                    stateMove.From.X,
                    stateMove.From.Y
                ),
                new CLocation(
                    stateMove.To.X,
                    stateMove.To.Y
                ),
                movedPiece,
                capturedPiece
            );

        move.IsPromotion =
            stateMove.IsPromotion;

        move.IsCastling =
            stateMove.IsCastling;

        move.IsEnPassant =
            stateMove.IsEnPassant;

        if (stateMove.IsCastling)
        {
            move.RookFrom =
                stateMove.RookFrom;

            move.RookTo =
                stateMove.RookTo;
        }

        if (stateMove.IsEnPassant)
        {
            move.CapturedLocation =
                stateMove.CapturedLocation;
        }

        return move;
    }
}