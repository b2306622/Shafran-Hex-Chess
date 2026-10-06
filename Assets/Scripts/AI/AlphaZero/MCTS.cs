using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class MCTS
{
    private readonly MoveGenerator _moveGenerator;
    private readonly IPolicyValueEvaluator _evaluator;
    private readonly AlphaZeroConfig _config;
    private readonly Dictionary<ulong, PolicyValueOutput> _evaluationCache =
        new Dictionary<ulong, PolicyValueOutput>(4096);

    // Search-local draw awareness diagnostics.
    private int _repetitionCutoffs;
    private int _noProgressCutoffs;

    public MCTS(
        IPolicyValueEvaluator evaluator,
        AlphaZeroConfig config = null)
    {
        _moveGenerator = new MoveGenerator();
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        _config = config ?? new AlphaZeroConfig();
    }

    public MCTSSearchResult Search(GameState rootState)
    {
        if (rootState == null)
            throw new ArgumentNullException(nameof(rootState));

        // When the search is running inside the Unity game, automatically
        // inherit the REAL game's repetition history and no-progress clock.
        // Standalone benchmark calls still work because the history can be null.
        Dictionary<ulong, int> gameHistory = null;
        int rootNoProgressPlies = 0;

        if (BaseGameController.Current != null)
        {
            gameHistory =
                BaseGameController.Current
                    .GetPositionVisitCountsSnapshot();

            rootNoProgressPlies =
                BaseGameController.Current.NoProgressPlyCount;
        }

        return Search(
            rootState,
            gameHistory,
            rootNoProgressPlies);
    }

    /// <summary>
    /// Full-history-aware MCTS entry point.
    ///
    /// gameHistory contains position-hash occurrence counts from the actual
    /// game before this search starts. The search adds its own simulated path
    /// occurrences on top of those counts.
    /// </summary>
    public MCTSSearchResult Search(
        GameState rootState,
        IReadOnlyDictionary<ulong, int> gameHistory,
        int rootNoProgressPlies = 0)
    {
        if (rootState == null)
            throw new ArgumentNullException(nameof(rootState));

        _evaluationCache.Clear();
        _repetitionCutoffs = 0;
        _noProgressCutoffs = 0;

        // The search must use the same V10 insufficient-material rule
        // as the real Unity game. K+Q/R/etc. vs K remains searchable
        // until checkmate.
        if (HexChessRules.TryGetMaterialTerminalResult(
                rootState,
                out EPlayer? materialWinner,
                out bool materialDraw))
        {
            return MCTSSearchResult.Empty(rootState);
        }

        List<StateMove> rootLegalMoves =
            _moveGenerator.GenerateLegalMoves(
                rootState,
                rootState.SideToMove);

        if (rootLegalMoves == null || rootLegalMoves.Count == 0)
            return MCTSSearchResult.Empty(rootState);

        int rootHistoryOccurrences =
            GetHistoryOccurrence(
                gameHistory,
                rootState.Hash);

        // The current root position always exists once in the real game, even
        // if this search is executed outside a BaseGameController.
        rootHistoryOccurrences =
            Mathf.Max(1, rootHistoryOccurrences);

        int historyPositionCount =
            gameHistory != null
                ? gameHistory.Count
                : 0;

        int actualRootNoProgressPlies =
            Mathf.Clamp(
                rootNoProgressPlies,
                0,
                _config.SafeMaxDepth);

        // IMPORTANT:
        // RootNoProg is the real game's clock. It is diagnostic state, not
        // automatically a search-terminal condition. If the actual game has
        // already exceeded the heuristic search threshold, feeding that value
        // directly into every simulation would make every quiet child an
        // immediate No-Progress cutoff (e.g. RootNoProg=36, threshold=32).
        // By default the MCTS no-progress clock starts at 0 for each search.
        // It can be inherited explicitly through config when desired.
        int simulationStartNoProgressPlies =
            _config.InheritGameNoProgressIntoSearch
                ? actualRootNoProgressPlies
                : 0;

        MCTSNode root = new MCTSNode(
            null,
            default,
            1f,
            rootState.Clone());

        PolicyValueOutput rootEvaluation = EvaluateNode(
            rootState,
            rootState.SideToMove,
            rootLegalMoves);

        ExpandNode(
            root,
            rootLegalMoves,
            rootEvaluation.policy);

        if (_config.rootDirichletEpsilon > 0f)
            ApplyRootDirichletNoise(root);

        float searchStartTime = Time.realtimeSinceStartup;
        int completedSimulations = 0;

        for (int simulation = 0;
             simulation < _config.SafeSimulations;
             simulation++)
        {
            RunSimulation(
                root,
                gameHistory,
                rootHistoryOccurrences,
                simulationStartNoProgressPlies);

            completedSimulations++;
        }

        float elapsedMilliseconds =
            (Time.realtimeSinceStartup - searchStartTime) * 1000f;

        MCTSSearchResult result = BuildResult(
            root,
            rootLegalMoves,
            elapsedMilliseconds,
            completedSimulations,
            _repetitionCutoffs,
            _noProgressCutoffs,
            rootHistoryOccurrences,
            historyPositionCount,
            actualRootNoProgressPlies);

        if (_config.EnableDiagnostics)
            LogDiagnostics(rootState, rootLegalMoves.Count, result);

        return result;
    }

    private static Dictionary<ulong, int> CreateSimulationHistory(
        IReadOnlyDictionary<ulong, int> gameHistory,
        int capacity)
    {
        Dictionary<ulong, int> result =
            new Dictionary<ulong, int>(
                Mathf.Max(4, capacity));

        if (gameHistory == null)
            return result;

        foreach (KeyValuePair<ulong, int> pair in gameHistory)
        {
            if (pair.Value > 0)
                result[pair.Key] = pair.Value;
        }

        return result;
    }

    private static int GetHistoryOccurrence(
        IReadOnlyDictionary<ulong, int> history,
        ulong hash)
    {
        if (history == null)
            return 0;

        return history.TryGetValue(
                   hash,
                   out int count)
            ? Mathf.Max(0, count)
            : 0;
    }

    private void RunSimulation(
        MCTSNode root,
        IReadOnlyDictionary<ulong, int> gameHistory,
        int rootHistoryOccurrences,
        int rootNoProgressPlies)
    {
        List<MCTSNode> path =
            new List<MCTSNode>(_config.SafeMaxDepth + 1);

        // Tracks position occurrences from BOTH the actual game history and
        // the currently simulated path. GameState.Hash includes board pieces,
        // side to move, and en-passant state.
        Dictionary<ulong, int> positionOccurrences =
            CreateSimulationHistory(
                gameHistory,
                _config.SafeMaxDepth + 2);

        MCTSNode node = root;
        path.Add(node);

        // Ensure the root is represented exactly at least once even when the
        // search is called without an external game history.
        positionOccurrences[root.State.Hash] =
            Mathf.Max(
                rootHistoryOccurrences,
                GetHistoryOccurrence(
                    positionOccurrences,
                    root.State.Hash));

        int depth = 0;
        int noProgressPlies =
            Mathf.Clamp(
                rootNoProgressPlies,
                0,
                _config.SafeMaxDepth);

        while (true)
        {
            if (HexChessRules.TryGetMaterialTerminalResult(
                    node.State,
                    out EPlayer? materialWinner,
                    out bool materialDraw))
            {
                Backup(
                    path,
                    GetTerminalValue(node.State)
                );
                return;
            }

            if (depth >= _config.SafeMaxDepth)
            {
                float cutoffValue = EvaluateValue(node.State);
                Backup(path, cutoffValue);
                return;
            }

            if (!node.IsExpanded)
            {
                List<StateMove> legalMoves =
                    _moveGenerator.GenerateLegalMoves(
                        node.State,
                        node.State.SideToMove);

                if (legalMoves == null || legalMoves.Count == 0)
                {
                    float value = GetTerminalValue(node.State);
                    Backup(path, value);
                    return;
                }

                PolicyValueOutput evaluation = EvaluateNode(
                    node.State,
                    node.State.SideToMove,
                    legalMoves);

                ExpandNode(
                    node,
                    legalMoves,
                    evaluation.policy);

                Backup(path, evaluation.value);
                return;
            }

            if (node.Children.Count == 0)
            {
                Backup(path, GetTerminalValue(node.State));
                return;
            }

            MCTSNode child = SelectChild(node);

            GameState childState = child.State;
            if (childState == null)
            {
                childState = node.State.Clone();
                _moveGenerator.MakeMove(
                    childState,
                    child.MoveFromParent);
                child.SetState(childState);
            }

            depth++;

            // -----------------------------------------------
            // Search-local repetition cutoff.
            // -----------------------------------------------
            ulong childHash = childState.Hash;
            int previousOccurrences = 0;

            if (positionOccurrences.TryGetValue(
                    childHash,
                    out int existingOccurrences))
            {
                previousOccurrences = existingOccurrences;
            }

            int occurrences = previousOccurrences + 1;
            positionOccurrences[childHash] = occurrences;

            if (_config.EnableRepetitionAwareness &&
                occurrences >= _config.SafeRepetitionCutoffOccurrences)
            {
                path.Add(child);
                _repetitionCutoffs++;
                Backup(
                    path,
                    _config.SafeRepetitionCutoffValue);
                return;
            }

            // -----------------------------------------------
            // Search-local no-progress cutoff.
            // A progress move is a pawn move, capture, en-passant
            // capture, or promotion. Quiet piece shuffles accumulate.
            // -----------------------------------------------
            if (IsProgressMove(child.MoveFromParent))
                noProgressPlies = 0;
            else
                noProgressPlies++;

            if (_config.EnableNoProgressAwareness &&
                noProgressPlies >= _config.SafeNoProgressPlyThreshold)
            {
                path.Add(child);
                _noProgressCutoffs++;
                Backup(
                    path,
                    _config.SafeNoProgressCutoffValue);
                return;
            }

            node = child;
            path.Add(node);
        }
    }

    private static bool IsProgressMove(StateMove move)
    {
        return move.IsCapture ||
               move.IsEnPassant ||
               move.IsPromotion ||
               move.MovingPiece.Type == StatePieceType.PAWN;
    }

    private void ExpandNode(
        MCTSNode node,
        List<StateMove> legalMoves,
        float[] policy = null)
    {
        if (node.IsExpanded)
            return;

        float uniform = 1f / Mathf.Max(1, legalMoves.Count);
        float totalPrior = 0f;

        for (int i = 0; i < legalMoves.Count; i++)
        {
            StateMove move = legalMoves[i];
            int action = AlphaZeroActionEncoder.Encode(move);

            float prior =
                policy != null &&
                action >= 0 &&
                action < policy.Length
                    ? Mathf.Max(0f, policy[action])
                    : uniform;

            MCTSNode child = new MCTSNode(
                node,
                move,
                prior);

            node.Children.Add(child);
            totalPrior += prior;
        }

        if (totalPrior <= 1e-8f)
        {
            for (int i = 0; i < node.Children.Count; i++)
                node.Children[i].Prior = uniform;
        }
        else
        {
            float inverseTotal = 1f / totalPrior;
            for (int i = 0; i < node.Children.Count; i++)
                node.Children[i].Prior *= inverseTotal;
        }

        node.IsExpanded = true;
    }

    private MCTSNode SelectChild(MCTSNode node)
    {
        MCTSNode best = null;
        float bestScore = float.NegativeInfinity;

        float sqrtParentVisits = Mathf.Sqrt(
            Mathf.Max(1, node.VisitCount));

        for (int i = 0; i < node.Children.Count; i++)
        {
            MCTSNode child = node.Children[i];

            float exploration =
                _config.SafeCpuct *
                child.Prior *
                sqrtParentVisits /
                (1f + child.VisitCount);

            float selectionQ = -child.Q;
            // child.Q is stored from the child node's side-to-move perspective.
            // The parent must evaluate that child from its own perspective, so negate Q.
            float score = selectionQ + exploration;

            if (score > bestScore)
            {
                bestScore = score;
                best = child;
            }
        }

        return best;
    }

    private PolicyValueOutput EvaluateNode(
        GameState state,
        EPlayer perspective,
        IReadOnlyList<StateMove> legalMoves)
    {
        if (_evaluationCache.TryGetValue(
                state.Hash,
                out PolicyValueOutput cached))
        {
            return cached;
        }

        PolicyValueOutput result = _evaluator.Evaluate(
            state,
            perspective,
            legalMoves);

        if (result.policy == null ||
            result.policy.Length != AlphaZeroActionEncoder.ActionCount)
        {
            result.policy = BuildUniformPolicy(legalMoves);
        }

        result.value = Mathf.Clamp(result.value, -1f, 1f);
        _evaluationCache[state.Hash] = result;
        return result;
    }

    private float EvaluateValue(GameState state)
    {
        List<StateMove> legalMoves =
            _moveGenerator.GenerateLegalMoves(
                state,
                state.SideToMove);

        if (legalMoves == null || legalMoves.Count == 0)
            return GetTerminalValue(state);

        return EvaluateNode(
            state,
            state.SideToMove,
            legalMoves).value;
    }

    private void Backup(
        List<MCTSNode> path,
        float value)
    {
        float currentValue = value;

        for (int i = path.Count - 1; i >= 0; i--)
        {
            MCTSNode node = path[i];
            node.Update(currentValue);
            currentValue = -currentValue;
        }
    }

    private float GetTerminalValue(GameState state)
    {
        if (HexChessRules.TryGetMaterialTerminalResult(
                state,
                out EPlayer? materialWinner,
                out bool materialDraw))
        {
            if (materialDraw)
            {
                return 0f;
            }

            return materialWinner == state.SideToMove
                ? -1f
                : 1f;
        }

        bool inCheck = _moveGenerator.IsInCheck(
            state,
            state.SideToMove);

        return inCheck ? -1f : 0f;
    }

    private float[] BuildUniformPolicy(
        IReadOnlyList<StateMove> legalMoves)
    {
        float[] policy =
            new float[AlphaZeroActionEncoder.ActionCount];

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

    private void ApplyRootDirichletNoise(MCTSNode root)
    {
        if (root.Children.Count == 0)
            return;

        float[] noise = SampleDirichlet(
            root.Children.Count,
            Mathf.Max(0.01f, _config.rootDirichletAlpha));

        float epsilon = Mathf.Clamp01(
            _config.rootDirichletEpsilon);

        for (int i = 0; i < root.Children.Count; i++)
        {
            root.Children[i].Prior =
                (1f - epsilon) * root.Children[i].Prior +
                epsilon * noise[i];
        }
    }

    private static float[] SampleDirichlet(
        int count,
        float alpha)
    {
        float[] values = new float[count];
        float sum = 0f;

        for (int i = 0; i < count; i++)
        {
            float gamma = SampleGamma(alpha);
            values[i] = gamma;
            sum += gamma;
        }

        if (sum <= 1e-8f)
        {
            float uniform = 1f / Mathf.Max(1, count);
            for (int i = 0; i < count; i++)
                values[i] = uniform;
            return values;
        }

        float inverse = 1f / sum;
        for (int i = 0; i < count; i++)
            values[i] *= inverse;

        return values;
    }

    private static float SampleGamma(float shape)
    {
        if (shape < 1f)
        {
            float u = Mathf.Max(1e-7f, UnityEngine.Random.value);
            return SampleGamma(shape + 1f) *
                   Mathf.Pow(u, 1f / shape);
        }

        float d = shape - 1f / 3f;
        float c = 1f / Mathf.Sqrt(9f * d);

        while (true)
        {
            float x = SampleStandardNormal();
            float v = 1f + c * x;

            if (v <= 0f)
                continue;

            v *= v * v;
            float u = UnityEngine.Random.value;

            if (u < 1f - 0.0331f * x * x * x * x)
                return d * v;

            if (Mathf.Log(Mathf.Max(1e-7f, u)) <
                0.5f * x * x +
                d * (1f - v + Mathf.Log(v)))
            {
                return d * v;
            }
        }
    }

    private static float SampleStandardNormal()
    {
        float u1 = Mathf.Max(1e-7f, UnityEngine.Random.value);
        float u2 = UnityEngine.Random.value;

        return Mathf.Sqrt(-2f * Mathf.Log(u1)) *
               Mathf.Cos(2f * Mathf.PI * u2);
    }

    private MCTSSearchResult BuildResult(
        MCTSNode root,
        IReadOnlyList<StateMove> rootLegalMoves,
        float elapsedMilliseconds,
        int completedSimulations,
        int repetitionCutoffs,
        int noProgressCutoffs,
        int rootHistoryOccurrences,
        int historyPositionCount,
        int initialNoProgressPlies)
    {
        StateMove bestMove = root.Children[0].MoveFromParent;
        MCTSNode bestChild = root.Children[0];
        int bestVisits = -1;

        int[] actions = new int[root.Children.Count];
        float[] policy = new float[AlphaZeroActionEncoder.ActionCount];
        float visitSum = 0f;

        for (int i = 0; i < root.Children.Count; i++)
        {
            MCTSNode child = root.Children[i];

            if (child.VisitCount > bestVisits)
            {
                bestVisits = child.VisitCount;
                bestChild = child;
                bestMove = child.MoveFromParent;
            }

            int action = AlphaZeroActionEncoder.Encode(
                child.MoveFromParent);

            actions[i] = action;
            visitSum += child.VisitCount;
        }

        if (visitSum <= 0f)
        {
            float uniform = 1f / Mathf.Max(1, root.Children.Count);

            for (int i = 0; i < root.Children.Count; i++)
            {
                int action = actions[i];
                if (action >= 0 && action < policy.Length)
                    policy[action] = uniform;
            }
        }
        else
        {
            float inverseVisitSum = 1f / visitSum;

            for (int i = 0; i < root.Children.Count; i++)
            {
                int action = actions[i];
                if (action >= 0 && action < policy.Length)
                    policy[action] =
                        root.Children[i].VisitCount * inverseVisitSum;
            }
        }

        MCTSRootCandidate[] ranked =
            BuildRankedCandidates(root, _config.SafeDiagnosticsCandidateCount);

        float entropy =
            CalculateRootPolicyEntropy(root, visitSum);

        float bestU =
            CalculateExplorationBonus(root, bestChild);

        return new MCTSSearchResult(
            bestMove,
            policy,
            root.VisitCount,
            root.Q,
            bestChild.VisitCount,
            -bestChild.Q,
            bestChild.Prior,
            bestU,
            root.Children.Count,
            entropy,
            elapsedMilliseconds,
            completedSimulations,
            repetitionCutoffs,
            noProgressCutoffs,
            rootHistoryOccurrences,
            historyPositionCount,
            initialNoProgressPlies,
            ranked);
    }

    private MCTSRootCandidate[] BuildRankedCandidates(
        MCTSNode root,
        int maxCount)
    {
        List<MCTSRootCandidate> candidates =
            new List<MCTSRootCandidate>(root.Children.Count);

        float sqrtParentVisits = Mathf.Sqrt(
            Mathf.Max(1, root.VisitCount));

        for (int i = 0; i < root.Children.Count; i++)
        {
            MCTSNode child = root.Children[i];

            float u =
                _config.SafeCpuct *
                child.Prior *
                sqrtParentVisits /
                (1f + child.VisitCount);

            candidates.Add(
                new MCTSRootCandidate(
                    child.MoveFromParent,
                    AlphaZeroActionEncoder.Encode(child.MoveFromParent),
                    child.VisitCount,
                    -child.Q,
                    child.Prior,
                    u,
                    -child.Q + u));
        }

        candidates.Sort(
            (a, b) =>
            {
                int visitsCompare =
                    b.Visits.CompareTo(a.Visits);

                if (visitsCompare != 0)
                    return visitsCompare;

                return b.Score.CompareTo(a.Score);
            });

        int count = Mathf.Min(
            Mathf.Max(0, maxCount),
            candidates.Count);

        if (count <= 0)
            return Array.Empty<MCTSRootCandidate>();

        MCTSRootCandidate[] result =
            new MCTSRootCandidate[count];

        for (int i = 0; i < count; i++)
            result[i] = candidates[i];

        return result;
    }

    private float CalculateRootPolicyEntropy(
        MCTSNode root,
        float visitSum)
    {
        if (visitSum <= 0f)
            return 0f;

        float entropy = 0f;

        for (int i = 0; i < root.Children.Count; i++)
        {
            float probability =
                root.Children[i].VisitCount / visitSum;

            if (probability > 1e-8f)
            {
                entropy -=
                    probability *
                    Mathf.Log(probability);
            }
        }

        return entropy;
    }

    // U is perspective-independent. Q is handled separately by negation at parent selection.
    private float CalculateExplorationBonus(
        MCTSNode root,
        MCTSNode child)
    {
        float sqrtParentVisits = Mathf.Sqrt(
            Mathf.Max(1, root.VisitCount));

        return
            _config.SafeCpuct *
            child.Prior *
            sqrtParentVisits /
            (1f + child.VisitCount);
    }

    private void LogDiagnostics(
        GameState rootState,
        int legalMoveCount,
        MCTSSearchResult result)
    {
        ShafranDebug.Log(
            "[MCTS DIAGNOSTIC] " +
            $"Player={rootState.SideToMove} | " +
            $"LegalMoves={legalMoveCount} | " +
            $"Simulations={result.CompletedSimulations} | " +
            $"RootVisits={result.RootVisits} | " +
            $"RootValue={result.RootValue:F4} | " +
            $"RootChildren={result.RootChildren} | " +
            $"Entropy={result.RootPolicyEntropy:F4} | " +
            $"BestVisits={result.BestMoveVisits} | " +
            $"BestQ(RootPerspective)={result.BestMoveQ:F4} | " +
            $"BestP={result.BestMovePrior:F4} | " +
            $"BestU={result.BestMoveU:F4} | " +
            $"RepCutoffs={result.RepetitionCutoffs} | " +
            $"NoProgCutoffs={result.NoProgressCutoffs} | " +
            $"RootHistory={result.RootHistoryOccurrences} | " +
            $"HistoryPositions={result.HistoryPositionCount} | " +
            $"RootNoProg={result.InitialNoProgressPlies} | " +
            $"SearchMs={result.SearchTimeMilliseconds:F2}");

        if (result.TopCandidates == null)
            return;

        for (int i = 0; i < result.TopCandidates.Length; i++)
        {
            MCTSRootCandidate candidate = result.TopCandidates[i];

            ShafranDebug.Log(
                "[MCTS ROOT] " +
                $"#{i + 1} " +
                $"Move={candidate.Move.From.X},{candidate.Move.From.Y}" +
                $"->{candidate.Move.To.X},{candidate.Move.To.Y} | " +
                $"Visits={candidate.Visits} | " +
                $"Q(RootPerspective)={candidate.Q:F4} | " +
                $"P={candidate.Prior:F4} | " +
                $"U={candidate.U:F4} | " +
                $"Score={candidate.Score:F4}");
        }
    }
}

public readonly struct MCTSRootCandidate
{
    public readonly StateMove Move;
    public readonly int Action;
    public readonly int Visits;
    public readonly float Q;
    public readonly float Prior;
    public readonly float U;
    public readonly float Score;

    public MCTSRootCandidate(
        StateMove move,
        int action,
        int visits,
        float q,
        float prior,
        float u,
        float score)
    {
        Move = move;
        Action = action;
        Visits = visits;
        Q = q;
        Prior = prior;
        U = u;
        Score = score;
    }
}

public readonly struct MCTSSearchResult
{
    public readonly StateMove BestMove;
    public readonly float[] Policy;
    public readonly int RootVisits;
    public readonly float RootValue;
    public readonly int BestMoveVisits;
    public readonly float BestMoveQ;
    public readonly float BestMovePrior;
    public readonly float BestMoveU;
    public readonly int RootChildren;
    public readonly float RootPolicyEntropy;
    public readonly float SearchTimeMilliseconds;
    public readonly int CompletedSimulations;
    public readonly int RepetitionCutoffs;
    public readonly int NoProgressCutoffs;
    public readonly int RootHistoryOccurrences;
    public readonly int HistoryPositionCount;
    public readonly int InitialNoProgressPlies;
    public readonly MCTSRootCandidate[] TopCandidates;

    public MCTSSearchResult(
        StateMove bestMove,
        float[] policy,
        int rootVisits,
        float rootValue,
        int bestMoveVisits,
        float bestMoveQ,
        float bestMovePrior,
        float bestMoveU,
        int rootChildren,
        float rootPolicyEntropy,
        float searchTimeMilliseconds,
        int completedSimulations,
        int repetitionCutoffs,
        int noProgressCutoffs,
        int rootHistoryOccurrences,
        int historyPositionCount,
        int initialNoProgressPlies,
        MCTSRootCandidate[] topCandidates)
    {
        BestMove = bestMove;
        Policy = policy;
        RootVisits = rootVisits;
        RootValue = rootValue;
        BestMoveVisits = bestMoveVisits;
        BestMoveQ = bestMoveQ;
        BestMovePrior = bestMovePrior;
        BestMoveU = bestMoveU;
        RootChildren = rootChildren;
        RootPolicyEntropy = rootPolicyEntropy;
        SearchTimeMilliseconds = searchTimeMilliseconds;
        CompletedSimulations = completedSimulations;
        RepetitionCutoffs = repetitionCutoffs;
        NoProgressCutoffs = noProgressCutoffs;
        RootHistoryOccurrences = rootHistoryOccurrences;
        HistoryPositionCount = historyPositionCount;
        InitialNoProgressPlies = initialNoProgressPlies;
        TopCandidates = topCandidates ??
                         Array.Empty<MCTSRootCandidate>();
    }

    public static MCTSSearchResult Empty(GameState state)
    {
        return new MCTSSearchResult(
            default,
            new float[AlphaZeroActionEncoder.ActionCount],
            0,
            0f,
            0,
            0f,
            0f,
            0f,
            0,
            0f,
            0f,
            0,
            0,
            0,
            1,
            0,
            0,
            Array.Empty<MCTSRootCandidate>());
    }
}
