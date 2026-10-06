using System.Collections.Generic;

public sealed class AlphaZeroSearch
{
    private readonly MCTS _mcts;

    public AlphaZeroSearch(
        IPolicyValueEvaluator evaluator,
        AlphaZeroConfig config = null)
    {
        _mcts = new MCTS(
            evaluator,
            config
        );
    }

    public MCTSSearchResult Search(GameState state)
    {
        return _mcts.Search(state);
    }

    /// <summary>
    /// Explicit-history overload used by offline/self-play generation.
    /// This prevents the dataset exporter from depending on the active
    /// BaseGameController's live history.
    /// </summary>
    public MCTSSearchResult Search(
        GameState state,
        IReadOnlyDictionary<ulong, int> gameHistory,
        int rootNoProgressPlies = 0)
    {
        return _mcts.Search(
            state,
            gameHistory,
            rootNoProgressPlies
        );
    }


    /// <summary>
    /// Select a move from an MCTS result.
    /// Low temperature uses the MCTS best move deterministically.
    /// Higher temperature samples from root visit counts, which is suitable
    /// for AlphaZero-style self-play.
    /// </summary>
    public static StateMove ChooseMove(
        MCTSSearchResult result,
        IReadOnlyList<StateMove> legalMoves,
        float temperature)
    {
        return ChooseMove(result, legalMoves, temperature, null);
    }

    public static StateMove ChooseMove(
        MCTSSearchResult result,
        IReadOnlyList<StateMove> legalMoves,
        float temperature,
        System.Random rng)
    {
        if (legalMoves == null || legalMoves.Count == 0)
            return default;

        if (ReferenceEquals(result, null))
            return legalMoves[0];

        // Evaluation / late-game mode: deterministic MCTS best move.
        if (temperature <= 0.05f)
        {
            if (ContainsMove(result.BestMove, legalMoves))
                return result.BestMove;

            if (result.TopCandidates != null)
            {
                for (int i = 0; i < result.TopCandidates.Length; i++)
                {
                    StateMove candidate = result.TopCandidates[i].Move;
                    if (ContainsMove(candidate, legalMoves))
                        return candidate;
                }
            }

            return legalMoves[0];
        }

        // Self-play mode: sample from the full root visit distribution.
        // result.Policy is indexed by the same 4900-action encoder used by MCTS.
        if (result.Policy != null && result.Policy.Length == AlphaZeroActionEncoder.ActionCount)
        {
            float exponent = 1f / UnityEngine.Mathf.Max(0.05f, temperature);
            float total = 0f;

            for (int i = 0; i < legalMoves.Count; i++)
            {
                int action = AlphaZeroActionEncoder.Encode(legalMoves[i]);
                if (action < 0 || action >= result.Policy.Length)
                    continue;

                float visitsProbability = UnityEngine.Mathf.Max(0f, result.Policy[action]);
                float weight = UnityEngine.Mathf.Pow(
                    UnityEngine.Mathf.Max(1e-6f, visitsProbability),
                    exponent);

                if (!float.IsNaN(weight) && !float.IsInfinity(weight))
                    total += weight;
            }

            if (total > 1e-8f &&
                !float.IsNaN(total) &&
                !float.IsInfinity(total))
            {
                float sample = NextRandom01(rng) * total;

                for (int i = 0; i < legalMoves.Count; i++)
                {
                    int action = AlphaZeroActionEncoder.Encode(legalMoves[i]);
                    if (action < 0 || action >= result.Policy.Length)
                        continue;

                    float visitsProbability = UnityEngine.Mathf.Max(0f, result.Policy[action]);
                    float weight = UnityEngine.Mathf.Pow(
                        UnityEngine.Mathf.Max(1e-6f, visitsProbability),
                        exponent);

                    if (float.IsNaN(weight) || float.IsInfinity(weight))
                        continue;

                    sample -= weight;
                    if (sample <= 0f)
                        return legalMoves[i];
                }
            }
        }

        // Fallback for older MCTS results that only expose TopCandidates.
        if (result.TopCandidates != null && result.TopCandidates.Length > 0)
        {
            float exponent = 1f / UnityEngine.Mathf.Max(0.05f, temperature);
            float total = 0f;

            for (int i = 0; i < result.TopCandidates.Length; i++)
            {
                StateMove move = result.TopCandidates[i].Move;
                if (!ContainsMove(move, legalMoves))
                    continue;

                float visits = UnityEngine.Mathf.Max(0f, result.TopCandidates[i].Visits);
                float weight = UnityEngine.Mathf.Pow(
                    UnityEngine.Mathf.Max(1e-6f, visits),
                    exponent);
                total += weight;
            }

            if (total > 1e-8f &&
                !float.IsNaN(total) &&
                !float.IsInfinity(total))
            {
                float sample = NextRandom01(rng) * total;

                for (int i = 0; i < result.TopCandidates.Length; i++)
                {
                    StateMove move = result.TopCandidates[i].Move;
                    if (!ContainsMove(move, legalMoves))
                        continue;

                    float visits = UnityEngine.Mathf.Max(0f, result.TopCandidates[i].Visits);
                    float weight = UnityEngine.Mathf.Pow(
                        UnityEngine.Mathf.Max(1e-6f, visits),
                        exponent);

                    sample -= weight;
                    if (sample <= 0f)
                        return move;
                }
            }
        }

        return ContainsMove(result.BestMove, legalMoves)
            ? result.BestMove
            : legalMoves[NextRandomIndex(rng, legalMoves.Count)];
    }

    private static float NextRandom01(System.Random rng)
    {
        return rng != null
            ? (float)rng.NextDouble()
            : UnityEngine.Random.value;
    }

    private static int NextRandomIndex(System.Random rng, int count)
    {
        if (count <= 1)
            return 0;

        return rng != null
            ? rng.Next(count)
            : UnityEngine.Random.Range(0, count);
    }

    private static bool ContainsMove(
        StateMove move,
        IReadOnlyList<StateMove> legalMoves)
    {
        int action = AlphaZeroActionEncoder.Encode(move);
        if (action < 0 || action >= AlphaZeroActionEncoder.ActionCount)
            return false;

        for (int i = 0; i < legalMoves.Count; i++)
        {
            if (AlphaZeroActionEncoder.Encode(legalMoves[i]) == action)
                return true;
        }

        return false;
    }

    public static StateMove ChooseMoveFromPolicy(
        GameState state,
        IReadOnlyList<StateMove> legalMoves,
        float[] policy,
        float temperature)
    {
        return ChooseMoveFromPolicy(state, legalMoves, policy, temperature, null);
    }

    public static StateMove ChooseMoveFromPolicy(
        GameState state,
        IReadOnlyList<StateMove> legalMoves,
        float[] policy,
        float temperature,
        System.Random rng)
    {
        if (legalMoves == null || legalMoves.Count == 0)
            return default;

        if (temperature <= 0.05f)
        {
            StateMove best = legalMoves[0];
            float bestValue = float.NegativeInfinity;

            for (int i = 0; i < legalMoves.Count; i++)
            {
                int action =
                    AlphaZeroActionEncoder.Encode(legalMoves[i]);

                float value =
                    action >= 0 &&
                    policy != null &&
                    action < policy.Length
                        ? policy[action]
                        : 0f;

                if (value > bestValue)
                {
                    bestValue = value;
                    best = legalMoves[i];
                }
            }

            return best;
        }

        float[] weights =
            new float[legalMoves.Count];

        float sum = 0f;

        for (int i = 0; i < legalMoves.Count; i++)
        {
            int action =
                AlphaZeroActionEncoder.Encode(legalMoves[i]);

            float probability =
                action >= 0 &&
                policy != null &&
                action < policy.Length
                    ? UnityEngine.Mathf.Max(0f, policy[action])
                    : 0f;

            float weight =
                UnityEngine.Mathf.Pow(
                    probability,
                    1f / UnityEngine.Mathf.Max(0.05f, temperature)
                );

            weights[i] = weight;
            sum += weight;
        }

        if (sum <= 1e-8f)
            return legalMoves[NextRandomIndex(rng, legalMoves.Count)];

        float sample = NextRandom01(rng) * sum;

        for (int i = 0; i < weights.Length; i++)
        {
            sample -= weights[i];
            if (sample <= 0f)
                return legalMoves[i];
        }

        return legalMoves[legalMoves.Count - 1];
    }
}
