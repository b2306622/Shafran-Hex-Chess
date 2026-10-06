using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class SearchStatistics
{
    // =========================================================
    // BASIC SEARCH
    // =========================================================

    private int _depth;
    private int _completedDepth;

    private long _nodes;
    private long _leafNodes;
    private long _ttHits;
    private long _ttMisses;
    private long _cutoffs;

    private double _searchTimeSeconds;

    private float _bestScore;
    private StateMove _bestMove;

    private DateTime _startTime;

    // =========================================================
    // SEARCH CONFIGURATION
    // =========================================================

    private bool _historyEnabled;
    private bool _killerEnabled;
    private bool _ttEnabled;
    private bool _aspirationEnabled;

    // =========================================================
    // HISTORY
    // =========================================================

    private long _historyOrderingHits;

    // =========================================================
    // KILLER
    // =========================================================

    private long _killerMoveHits;

    private long _killer1Hits;

    private long _killer2Hits;

    // =========================================================
    // TT ORDERING
    // =========================================================

    private long _ttOrderingHits;

    // =========================================================
    // ASPIRATION
    // =========================================================

    private int _aspirationAttempts;
    private int _aspirationFailHigh;
    private int _aspirationFailLow;
    private int _aspirationReSearches;

    private int _searchesWithAspirationReSearch;

    // =========================================================
    // PRINCIPAL VARIATION
    // =========================================================

    private readonly List<StateMove> _principalVariation =
        new List<StateMove>();

    // =========================================================
    // CONFIGURATION PROPERTIES
    // =========================================================

    public bool HistoryEnabled
    {
        get
        {
            return _historyEnabled;
        }
    }

    public bool KillerEnabled
    {
        get
        {
            return _killerEnabled;
        }
    }

    public bool TTEnabled
    {
        get
        {
            return _ttEnabled;
        }
    }

    public bool AspirationEnabled
    {
        get
        {
            return _aspirationEnabled;
        }
    }

    public string ConfigurationName
    {
        get
        {
            if (!_historyEnabled &&
                !_killerEnabled &&
                !_ttEnabled &&
                !_aspirationEnabled)
            {
                return "Baseline";
            }

            if (_historyEnabled &&
                !_killerEnabled &&
                !_ttEnabled &&
                !_aspirationEnabled)
            {
                return "+History";
            }

            if (_historyEnabled &&
                _killerEnabled &&
                !_ttEnabled &&
                !_aspirationEnabled)
            {
                return "+Killer";
            }

            if (_historyEnabled &&
                _killerEnabled &&
                _ttEnabled &&
                !_aspirationEnabled)
            {
                return "+TT";
            }

            if (_historyEnabled &&
                _killerEnabled &&
                _ttEnabled &&
                _aspirationEnabled)
            {
                return "+Aspiration";
            }

            return "Custom";
        }
    }

    // =========================================================
    // BASIC PROPERTIES
    // =========================================================

    public int Depth
    {
        get
        {
            return _depth;
        }
    }

    public int CompletedDepth
    {
        get
        {
            return _completedDepth;
        }
    }

    public long Nodes
    {
        get
        {
            return _nodes;
        }
    }

    public long LeafNodes
    {
        get
        {
            return _leafNodes;
        }
    }

    public long TT_Hits
    {
        get
        {
            return _ttHits;
        }
    }

    public long TT_Misses
    {
        get
        {
            return _ttMisses;
        }
    }

    public long Cutoffs
    {
        get
        {
            return _cutoffs;
        }
    }

    public double SearchTimeSeconds
    {
        get
        {
            return _searchTimeSeconds;
        }
    }

    public float BestScore
    {
        get
        {
            return _bestScore;
        }
    }

    public StateMove BestMove
    {
        get
        {
            return _bestMove;
        }
    }

    public IReadOnlyList<StateMove> PrincipalVariation
    {
        get
        {
            return _principalVariation;
        }
    }

    public int PrincipalVariationLength
    {
        get
        {
            return _principalVariation.Count;
        }
    }

    // =========================================================
    // HISTORY STATISTICS
    // =========================================================

    public long HistoryOrderingHits
    {
        get
        {
            return _historyOrderingHits;
        }
    }

    // =========================================================
    // KILLER STATISTICS
    // =========================================================

    public long KillerMoveHits
    {
        get
        {
            return _killerMoveHits;
        }
    }

    public long Killer1Hits
    {
        get
        {
            return _killer1Hits;
        }
    }

    public long Killer2Hits
    {
        get
        {
            return _killer2Hits;
        }
    }

    // =========================================================
    // TT ORDERING
    // =========================================================

    public long TTOrderingHits
    {
        get
        {
            return _ttOrderingHits;
        }
    }

    // =========================================================
    // ASPIRATION
    // =========================================================

    public int AspirationAttempts
    {
        get
        {
            return _aspirationAttempts;
        }
    }

    public int AspirationFailHigh
    {
        get
        {
            return _aspirationFailHigh;
        }
    }

    public int AspirationFailLow
    {
        get
        {
            return _aspirationFailLow;
        }
    }

    public int AspirationReSearches
    {
        get
        {
            return _aspirationReSearches;
        }
    }

    public int SearchesWithAspirationReSearch
    {
        get
        {
            return _searchesWithAspirationReSearch;
        }
    }

    public double AspirationReSearchRate
    {
        get
        {
            if (_aspirationAttempts <= 0)
            {
                return 0.0;
            }

            return
                (double)_aspirationReSearches /
                _aspirationAttempts *
                100.0;
        }
    }

    public double AspirationSuccessRate
    {
        get
        {
            if (_aspirationAttempts <= 0)
            {
                return 0.0;
            }

            int successfulAttempts =
                _aspirationAttempts -
                _aspirationReSearches;

            if (successfulAttempts < 0)
            {
                successfulAttempts = 0;
            }

            return
                (double)successfulAttempts /
                _aspirationAttempts *
                100.0;
        }
    }

    // =========================================================
    // DERIVED METRICS
    // =========================================================

    public double NodesPerSecond
    {
        get
        {
            if (_searchTimeSeconds <= 0.0)
            {
                return _nodes;
            }

            return
                _nodes /
                _searchTimeSeconds;
        }
    }

    public double TTHitRate
    {
        get
        {
            long total =
                _ttHits +
                _ttMisses;

            if (total <= 0)
            {
                return 0.0;
            }

            return
                (double)_ttHits /
                total *
                100.0;
        }
    }

    public double CutoffRate
    {
        get
        {
            if (_nodes <= 0)
            {
                return 0.0;
            }

            return
                (double)_cutoffs /
                _nodes *
                100.0;
        }
    }

    // =========================================================
    // START
    // =========================================================

    public void Start()
    {
        Reset();

        _startTime =
            DateTime.UtcNow;
    }

    // =========================================================
    // STOP
    // =========================================================

    public void Stop()
    {
        _searchTimeSeconds =
            (
                DateTime.UtcNow -
                _startTime
            ).TotalSeconds;
    }

    // =========================================================
    // RESET
    // =========================================================

    public void Reset()
    {
        _depth = 0;
        _completedDepth = 0;

        _nodes = 0;
        _leafNodes = 0;
        _ttHits = 0;
        _ttMisses = 0;
        _cutoffs = 0;

        _searchTimeSeconds = 0.0;

        _bestScore = 0f;
        _bestMove = default(StateMove);

        _historyOrderingHits = 0;

        _killerMoveHits = 0;
        _killer1Hits = 0;
        _killer2Hits = 0;

        _ttOrderingHits = 0;

        _aspirationAttempts = 0;
        _aspirationFailHigh = 0;
        _aspirationFailLow = 0;
        _aspirationReSearches = 0;
        _searchesWithAspirationReSearch = 0;

        _principalVariation.Clear();
    }

    // =========================================================
    // CONFIGURATION
    // =========================================================

    public void SetConfiguration(
        bool historyEnabled,
        bool killerEnabled,
        bool ttEnabled,
        bool aspirationEnabled)
    {
        _historyEnabled = historyEnabled;
        _killerEnabled = killerEnabled;
        _ttEnabled = ttEnabled;
        _aspirationEnabled = aspirationEnabled;
    }

    // =========================================================
    // DEPTH
    // =========================================================

    public void SetDepth(
        int depth)
    {
        _depth = depth;
    }

    public void SetCompletedDepth(
        int depth)
    {
        _completedDepth = depth;
    }

    // =========================================================
    // COUNTERS
    // =========================================================

    public void AddNode()
    {
        _nodes++;
    }

    public void AddLeafNode()
    {
        _leafNodes++;
    }

    public void AddTTHit()
    {
        _ttHits++;
    }

    public void AddTTMiss()
    {
        _ttMisses++;
    }

    public void AddTTOrderingHit()
    {
        _ttOrderingHits++;
    }

    public void AddCutoff()
    {
        _cutoffs++;
    }

    // =========================================================
    // HISTORY
    // =========================================================

    public void AddHistoryOrderingHit()
    {
        _historyOrderingHits++;
    }

    // =========================================================
    // KILLER
    // =========================================================

    public void AddKillerMoveHit(
        int slot)
    {
        _killerMoveHits++;

        if (slot == 0)
        {
            _killer1Hits++;
        }
        else if (slot == 1)
        {
            _killer2Hits++;
        }
    }

    // =========================================================
    // ASPIRATION
    // =========================================================

    public void AddAspirationAttempt()
    {
        _aspirationAttempts++;
    }

    public void AddAspirationFailHigh()
    {
        _aspirationFailHigh++;
    }

    public void AddAspirationFailLow()
    {
        _aspirationFailLow++;
    }

    public void AddAspirationReSearch()
    {
        _aspirationReSearches++;
    }

    public void MarkSearchWithAspirationReSearch()
    {
        _searchesWithAspirationReSearch++;
    }

    // =========================================================
    // PV
    // =========================================================

    public void SetPrincipalVariation(
        List<StateMove> pv)
    {
        _principalVariation.Clear();

        if (pv == null)
        {
            return;
        }

        for (
            int i = 0;
            i < pv.Count;
            i++)
        {
            _principalVariation.Add(
                pv[i]
            );
        }
    }

    // =========================================================
    // RESULT
    // =========================================================

    public void SetResult(
        StateMove bestMove,
        float bestScore)
    {
        _bestMove = bestMove;
        _bestScore = bestScore;
    }

    // =========================================================
    // PV FORMAT
    // =========================================================

    public string GetPrincipalVariationString()
    {
        if (_principalVariation.Count == 0)
        {
            return "NONE";
        }

        StringBuilder builder =
            new StringBuilder();

        for (
            int i = 0;
            i < _principalVariation.Count;
            i++)
        {
            if (i > 0)
            {
                builder.Append(" | ");
            }

            StateMove move =
                _principalVariation[i];

            builder.Append(
                $"({move.From.X},{move.From.Y})" +
                $"->" +
                $"({move.To.X},{move.To.Y})"
            );
        }

        return builder.ToString();
    }

    // =========================================================
    // PRINT
    // =========================================================

    public void Print()
    {
        StringBuilder report =
            new StringBuilder();
    }
}