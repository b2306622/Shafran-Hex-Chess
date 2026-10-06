using System.Collections.Generic;

public enum TTFlag
{
    EXACT,
    LOWER_BOUND,
    UPPER_BOUND
}

public struct TTEntry
{
    public int Depth;
    public float Score;
    public TTFlag Flag;
    public StateMove BestMove;

    // Generation giúp ưu tiên entry mới.
    public int Generation;

    public TTEntry(
        int depth,
        float score,
        TTFlag flag,
        StateMove bestMove,
        int generation = 0)
    {
        Depth = depth;
        Score = score;
        Flag = flag;
        BestMove = bestMove;
        Generation = generation;
    }
}

public class TranspositionTable
{
    private readonly Dictionary<ulong, TTEntry>
        _table;

    private readonly int _maxEntries;

    private int _generation;

    public int Count =>
        _table.Count;

    public int Generation =>
        _generation;

    public TranspositionTable(
        int maxEntries = 100000)
    {
        _maxEntries =
            maxEntries < 1
                ? 1
                : maxEntries;

        _table =
            new Dictionary<ulong, TTEntry>(
                _maxEntries
            );

        _generation = 0;
    }

    public void NewSearch()
    {
        _generation++;
    }

    public bool TryGet(
        ulong hash,
        out TTEntry entry)
    {
        return _table.TryGetValue(
            hash,
            out entry
        );
    }

    public void Store(
        ulong hash,
        TTEntry entry)
    {
        entry.Generation =
            _generation;

        if (_table.TryGetValue(
                hash,
                out TTEntry existing))
        {
            // Giữ entry sâu hơn.
            if (existing.Depth > entry.Depth &&
                existing.Generation == entry.Generation)
            {
                return;
            }

            // Cùng depth nhưng entry EXACT mới hơn.
            if (existing.Depth == entry.Depth &&
                existing.Flag == TTFlag.EXACT &&
                entry.Flag != TTFlag.EXACT &&
                existing.Generation >= entry.Generation)
            {
                return;
            }

            _table[hash] =
                entry;

            return;
        }

        if (_table.Count >= _maxEntries)
        {
            ClearOldestApproximation();
        }

        _table[hash] =
            entry;
    }

    public void Clear()
    {
        _table.Clear();
    }

    private void ClearOldestApproximation()
    {
        int removeCount =
            _table.Count / 10;

        if (removeCount < 1)
        {
            removeCount = 1;
        }

        List<ulong> keys =
            new List<ulong>(
                _table.Keys
            );

        int removed = 0;

        // Ưu tiên xóa entry cũ.
        for (int i = 0;
             i < keys.Count &&
             removed < removeCount;
             i++)
        {
            TTEntry entry =
                _table[keys[i]];

            if (entry.Generation <
                _generation)
            {
                _table.Remove(
                    keys[i]
                );

                removed++;
            }
        }

        // Nếu chưa đủ thì xóa approximation.
        for (int i = 0;
             i < keys.Count &&
             removed < removeCount;
             i++)
        {
            if (_table.Remove(
                    keys[i]))
            {
                removed++;
            }
        }
    }
}