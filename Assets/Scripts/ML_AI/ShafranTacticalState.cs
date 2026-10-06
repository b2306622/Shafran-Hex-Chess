using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared Shafran tactical-state cache used by PPO observations and reward.
/// It keeps the observation contract at 986 values while replacing the
/// old per-cell HasMoved scalar with a compact tactical safety signal.
///
/// "Legal attack/defense" is based on legal moves, not only geometric
/// attacks, so pinned pieces are not incorrectly treated as valid defenders.
/// </summary>
public static class ShafranTacticalState
{
    private const int CELL_COUNT = HexChessRules.CELL_COUNT;

    private static readonly MoveGenerator Generator =
        new MoveGenerator();

    private static ulong _cachedHash = ulong.MaxValue;
    private static bool _pseudoReady;
    private static bool _whiteLegalCapturesReady;
    private static bool _blackLegalCapturesReady;

    private static readonly int[] WhitePseudo =
        new int[CELL_COUNT];
    private static readonly int[] BlackPseudo =
        new int[CELL_COUNT];

    private static readonly int[] WhiteLegalCaptures =
        new int[CELL_COUNT];
    private static readonly int[] BlackLegalCaptures =
        new int[CELL_COUNT];

    private static readonly int[] WhiteLegalDefenders =
        new int[CELL_COUNT];
    private static readonly int[] BlackLegalDefenders =
        new int[CELL_COUNT];

    private static readonly bool[] WhiteDefenseKnown =
        new bool[CELL_COUNT];
    private static readonly bool[] BlackDefenseKnown =
        new bool[CELL_COUNT];

    private static void EnsurePseudo(GameState state)
    {
        if (state == null)
            return;

        if (_cachedHash == state.Hash && _pseudoReady)
            return;

        _cachedHash = state.Hash;
        _pseudoReady = true;
        _whiteLegalCapturesReady = false;
        _blackLegalCapturesReady = false;

        System.Array.Clear(WhitePseudo, 0, WhitePseudo.Length);
        System.Array.Clear(BlackPseudo, 0, BlackPseudo.Length);
        System.Array.Clear(WhiteLegalCaptures, 0, WhiteLegalCaptures.Length);
        System.Array.Clear(BlackLegalCaptures, 0, BlackLegalCaptures.Length);
        System.Array.Clear(WhiteLegalDefenders, 0, WhiteLegalDefenders.Length);
        System.Array.Clear(BlackLegalDefenders, 0, BlackLegalDefenders.Length);
        System.Array.Clear(WhiteDefenseKnown, 0, WhiteDefenseKnown.Length);
        System.Array.Clear(BlackDefenseKnown, 0, BlackDefenseKnown.Length);

        BuildPseudoAttackMap(state, EPlayer.WHITE, WhitePseudo);
        BuildPseudoAttackMap(state, EPlayer.BLACK, BlackPseudo);
    }

    private static void EnsureLegalCaptures(GameState state, EPlayer player)
    {
        EnsurePseudo(state);
        if (state == null)
            return;

        if (player == EPlayer.WHITE)
        {
            if (_whiteLegalCapturesReady)
                return;

            BuildLegalCaptureMap(
                Generator.GenerateLegalMoves(state, EPlayer.WHITE),
                WhiteLegalCaptures
            );
            _whiteLegalCapturesReady = true;
        }
        else
        {
            if (_blackLegalCapturesReady)
                return;

            BuildLegalCaptureMap(
                Generator.GenerateLegalMoves(state, EPlayer.BLACK),
                BlackLegalCaptures
            );
            _blackLegalCapturesReady = true;
        }
    }

    private static void BuildLegalCaptureMap(
        List<StateMove> moves,
        int[] map)
    {
        for (int i = 0; i < moves.Count; i++)
        {
            StateMove move = moves[i];
            if (!move.IsCapture)
                continue;

            int index = HexChessRules.GetCellIndex(
                move.To.X,
                move.To.Y
            );

            if (index >= 0 && index < CELL_COUNT)
                map[index]++;
        }
    }

    private static void BuildPseudoAttackMap(
        GameState state,
        EPlayer player,
        int[] map)
    {
        // PERFORMANCE: build attacks directly from Shafran movement rays.
        // The previous implementation tested every piece against every one
        // of the 70 cells, causing unnecessary work during observations.
        foreach (Vector2Int from in HexChessRules.CellCoordinates)
        {
            StatePiece? piece = state.GetPiece(from.x, from.y);
            if (!piece.HasValue ||
                !piece.Value.IsValid ||
                piece.Value.Player != player)
            {
                continue;
            }

            StatePiece value = piece.Value;

            if (value.Type == StatePieceType.PAWN)
            {
                int direction =
                    value.Player == EPlayer.WHITE ? 1 : -1;

                MarkPseudoTarget(
                    from.x + 1, from.y + direction, map);
                MarkPseudoTarget(
                    from.x - 1, from.y + direction, map);
                continue;
            }

            if (value.Type == StatePieceType.KNIGHT)
            {
                for (int i = 0; i < HexChessRules.KnightMoves.Length; i++)
                {
                    Vector2Int d = HexChessRules.KnightMoves[i];
                    MarkPseudoTarget(from.x + d.x, from.y + d.y, map);
                }
                continue;
            }

            if (value.Type == StatePieceType.KING)
            {
                for (int i = 0; i < HexChessRules.QueenDirections.Length; i++)
                {
                    Vector2Int d = HexChessRules.QueenDirections[i];
                    MarkPseudoTarget(from.x + d.x, from.y + d.y, map);
                }
                continue;
            }

            Vector2Int[] directions = null;
            if (value.Type == StatePieceType.BISHOP)
                directions = HexChessRules.BishopDirections;
            else if (value.Type == StatePieceType.ROOK)
                directions = HexChessRules.RookDirections;
            else if (value.Type == StatePieceType.QUEEN)
                directions = HexChessRules.QueenDirections;

            if (directions == null)
                continue;

            for (int i = 0; i < directions.Length; i++)
            {
                Vector2Int d = directions[i];
                int x = from.x + d.x;
                int y = from.y + d.y;

                while (HexChessRules.IsValidCell(x, y))
                {
                    int index = HexChessRules.GetCellIndex(x, y);
                    if (index >= 0 && index < map.Length)
                        map[index]++;

                    // Include the first occupied square as defended/attacked,
                    // then stop the ray.
                    if (state.GetPiece(x, y).HasValue)
                        break;

                    x += d.x;
                    y += d.y;
                }
            }
        }
    }

    private static void MarkPseudoTarget(int x, int y, int[] map)
    {
        if (!HexChessRules.IsValidCell(x, y))
            return;

        int index = HexChessRules.GetCellIndex(x, y);
        if (index >= 0 && index < map.Length)
            map[index]++;
    }

    private static bool CanGeometricAttack(
        GameState state,
        Vector2Int from,
        StatePiece piece,
        int targetX,
        int targetY)
    {
        int dx = targetX - from.x;
        int dy = targetY - from.y;

        if (piece.Type == StatePieceType.PAWN)
        {
            int direction = piece.Player == EPlayer.WHITE ? 1 : -1;
            return dy == direction && Mathf.Abs(dx) == 1;
        }

        if (piece.Type == StatePieceType.KNIGHT)
        {
            for (int i = 0; i < HexChessRules.KnightMoves.Length; i++)
            {
                if (dx == HexChessRules.KnightMoves[i].x &&
                    dy == HexChessRules.KnightMoves[i].y)
                {
                    return true;
                }
            }
            return false;
        }

        if (piece.Type == StatePieceType.KING)
        {
            for (int i = 0; i < HexChessRules.QueenDirections.Length; i++)
            {
                Vector2Int d = HexChessRules.QueenDirections[i];
                if (dx == d.x && dy == d.y)
                    return true;
            }
            return false;
        }

        Vector2Int[] directions = null;
        if (piece.Type == StatePieceType.BISHOP)
            directions = HexChessRules.BishopDirections;
        else if (piece.Type == StatePieceType.ROOK)
            directions = HexChessRules.RookDirections;
        else if (piece.Type == StatePieceType.QUEEN)
            directions = HexChessRules.QueenDirections;

        if (directions == null)
            return false;

        for (int i = 0; i < directions.Length; i++)
        {
            Vector2Int d = directions[i];
            int x = from.x + d.x;
            int y = from.y + d.y;

            while (HexChessRules.IsValidCell(x, y))
            {
                if (x == targetX && y == targetY)
                    return true;

                if (state.GetPiece(x, y).HasValue)
                    break;

                x += d.x;
                y += d.y;
            }
        }

        return false;
    }

    // Exact legal defender count. A candidate defender must be able to
    // capture the target on this square without leaving its own King in check.
    private static int GetLegalDefenseCountInternal(
        GameState state,
        EPlayer defender,
        int targetX,
        int targetY)
    {
        int index = HexChessRules.GetCellIndex(targetX, targetY);
        if (index < 0)
            return 0;

        int[] counts = defender == EPlayer.WHITE
            ? WhiteLegalDefenders
            : BlackLegalDefenders;
        bool[] known = defender == EPlayer.WHITE
            ? WhiteDefenseKnown
            : BlackDefenseKnown;

        if (known[index])
            return counts[index];

        known[index] = true;
        counts[index] = 0;

        StatePiece? target = state.GetPiece(targetX, targetY);
        if (!target.HasValue ||
            !target.Value.IsValid ||
            target.Value.Player != defender ||
            target.Value.Type == StatePieceType.KING)
        {
            return 0;
        }

        // Replace the defender's own piece by a harmless enemy pawn. Legal
        // move generation now answers the exact question: "Can any defender
        // legally capture a piece on this square without exposing its King?"
        GameState probe = state.Clone();
        probe.SetPiece(
            targetX,
            targetY,
            new StatePiece(
                StatePieceType.PAWN,
                HexChessRules.GetOpponent(defender)
            )
        );

        List<StateMove> moves =
            Generator.GenerateLegalMoves(
                probe,
                defender
            );

        for (int i = 0; i < moves.Count; i++)
        {
            StateMove move = moves[i];
            if (!move.IsCapture)
                continue;

            if (move.To.X == targetX &&
                move.To.Y == targetY)
            {
                counts[index]++;
            }
        }

        return counts[index];
    }

    public static int GetPseudoAttackCount(
        GameState state,
        EPlayer attacker,
        int x,
        int y)
    {
        EnsurePseudo(state);
        int index = HexChessRules.GetCellIndex(x, y);
        if (index < 0)
            return 0;

        return attacker == EPlayer.WHITE
            ? WhitePseudo[index]
            : BlackPseudo[index];
    }

    public static int GetLegalCaptureCount(
        GameState state,
        EPlayer attacker,
        int x,
        int y)
    {
        EnsureLegalCaptures(state, attacker);
        int index = HexChessRules.GetCellIndex(x, y);
        if (index < 0)
            return 0;

        return attacker == EPlayer.WHITE
            ? WhiteLegalCaptures[index]
            : BlackLegalCaptures[index];
    }

    public static int GetLegalDefenseCount(
        GameState state,
        EPlayer defender,
        int x,
        int y)
    {
        EnsurePseudo(state);
        return GetLegalDefenseCountInternal(
            state,
            defender,
            x,
            y
        );
    }

    // Fast observation/heuristic safety. This deliberately uses the cached
    // Shafran geometric attack map only. Exact legal defense remains available
    // through GetSquareSafety for rare tactical decisions. Avoiding a full
    // legal-move generation per board cell is critical for PPO throughput.
    public static void FillSquareSafetyFastMap(
        GameState state,
        EPlayer perspective,
        float[] buffer
    )
    {
        if (buffer == null || buffer.Length < CELL_COUNT)
            return;

        EnsurePseudo(state);

        for (int i = 0; i < CELL_COUNT; i++)
            buffer[i] = 0f;

        if (state == null)
            return;

        EPlayer opponent = HexChessRules.GetOpponent(perspective);
        int outputIndex = 0;

        foreach (Vector2Int position in HexChessRules.CellCoordinates)
        {
            StatePiece? piece = state.GetPiece(position.x, position.y);
            int cellIndex = HexChessRules.GetCellIndex(position.x, position.y);

            if (!piece.HasValue || !piece.Value.IsValid ||
                cellIndex < 0 || cellIndex >= CELL_COUNT)
            {
                if (outputIndex < buffer.Length)
                    buffer[outputIndex++] = 0f;
                continue;
            }

            int enemyAttacks =
                opponent == EPlayer.WHITE
                    ? WhitePseudo[cellIndex]
                    : BlackPseudo[cellIndex];

            int ownDefenders =
                perspective == EPlayer.WHITE
                    ? WhitePseudo[cellIndex]
                    : BlackPseudo[cellIndex];

            if (piece.Value.Type == StatePieceType.KING)
            {
                buffer[outputIndex++] = enemyAttacks > 0 ? -1f : 0.15f;
                continue;
            }

            if (piece.Value.Player == perspective)
            {
                float balance = Mathf.Clamp(
                    (ownDefenders - enemyAttacks) / 3f,
                    -1f, 1f);
                float defenceLoad = Mathf.Clamp01(ownDefenders / 4f);
                float attackLoad = Mathf.Clamp01(enemyAttacks / 4f);

                buffer[outputIndex++] = Mathf.Clamp(
                    balance * 0.72f +
                    defenceLoad * 0.18f -
                    attackLoad * 0.18f,
                    -1f, 1f);
            }
            else
            {
                if (ownDefenders <= 0)
                {
                    buffer[outputIndex++] = -0.10f;
                    continue;
                }

                float targetBalance = Mathf.Clamp(
                    (ownDefenders - enemyAttacks) / 3f,
                    -1f, 1f);
                float captureLoad = Mathf.Clamp01(ownDefenders / 4f);
                float defenderLoad = Mathf.Clamp01(enemyAttacks / 4f);

                buffer[outputIndex++] = Mathf.Clamp(
                    targetBalance * 0.72f +
                    captureLoad * 0.18f -
                    defenderLoad * 0.18f,
                    -1f, 1f);
            }
        }
    }

    public static float GetSquareSafetyFast(
        GameState state,
        EPlayer perspective,
        int x,
        int y)
    {
        EnsurePseudo(state);

        if (state == null)
            return 0f;

        StatePiece? piece = state.GetPiece(x, y);
        if (!piece.HasValue || !piece.Value.IsValid)
            return 0f;

        EPlayer opponent = HexChessRules.GetOpponent(perspective);

        if (piece.Value.Type == StatePieceType.KING)
        {
            int attacks = GetPseudoAttackCount(
                state, opponent, x, y);
            return attacks > 0 ? -1f : 0.15f;
        }

        int enemyAttacks = GetPseudoAttackCount(
            state, opponent, x, y);
        int ownDefenders = GetPseudoAttackCount(
            state, perspective, x, y);

        if (piece.Value.Player == perspective)
        {
            float balance = Mathf.Clamp(
                (ownDefenders - enemyAttacks) / 3f,
                -1f, 1f);
            float defenceLoad = Mathf.Clamp01(ownDefenders / 4f);
            float attackLoad = Mathf.Clamp01(enemyAttacks / 4f);

            return Mathf.Clamp(
                balance * 0.72f +
                defenceLoad * 0.18f -
                attackLoad * 0.18f,
                -1f, 1f);
        }

        int ourCaptures = GetPseudoAttackCount(
            state, perspective, x, y);
        int enemyDefenders = GetPseudoAttackCount(
            state, opponent, x, y);

        if (ourCaptures <= 0)
            return -0.10f;

        float targetBalance = Mathf.Clamp(
            (ourCaptures - enemyDefenders) / 3f,
            -1f, 1f);
        float captureLoad = Mathf.Clamp01(ourCaptures / 4f);
        float defenderLoad = Mathf.Clamp01(enemyDefenders / 4f);

        return Mathf.Clamp(
            targetBalance * 0.72f +
            captureLoad * 0.18f -
            defenderLoad * 0.18f,
            -1f, 1f);
    }

    public static float GetSquareSafety(
        GameState state,
        EPlayer perspective,
        int x,
        int y)
    {
        EnsurePseudo(state);

        StatePiece? piece = state.GetPiece(x, y);
        if (!piece.HasValue || !piece.Value.IsValid)
            return 0f;

        EPlayer opponent = HexChessRules.GetOpponent(perspective);

        if (piece.Value.Type == StatePieceType.KING)
        {
            if (piece.Value.Player == perspective)
            {
                return Generator.IsInCheck(state, perspective)
                    ? -1f
                    : 0.15f;
            }

            return 0f;
        }

        if (piece.Value.Player == perspective)
        {
            int attackers = GetLegalCaptureCount(
                state, opponent, x, y);
            int defenders = GetLegalDefenseCount(
                state, perspective, x, y);

            // Signed tactical signature: safety balance + tactical load.
            // This keeps one scalar per cell while exposing more than a
            // simple defended/undefended bit to the policy.
            float balance = Mathf.Clamp(
                (defenders - attackers) / 3f,
                -1f,
                1f);
            float defenceLoad = Mathf.Clamp01(defenders / 4f);
            float attackLoad = Mathf.Clamp01(attackers / 4f);

            return Mathf.Clamp(
                balance * 0.72f +
                defenceLoad * 0.18f -
                attackLoad * 0.18f,
                -1f,
                1f);
        }

        int ourCaptures = GetLegalCaptureCount(
            state, perspective, x, y);
        int enemyDefenders = GetLegalDefenseCount(
            state, opponent, x, y);

        if (ourCaptures <= 0)
            return -0.10f;

        float targetBalance = Mathf.Clamp(
            (ourCaptures - enemyDefenders) / 3f,
            -1f,
            1f);
        float captureLoad = Mathf.Clamp01(ourCaptures / 4f);
        float defenderLoad = Mathf.Clamp01(enemyDefenders / 4f);

        return Mathf.Clamp(
            targetBalance * 0.72f +
            captureLoad * 0.18f -
            defenderLoad * 0.18f,
            -1f,
            1f);
    }

    public static bool IsPseudoAttackingSquare(
        GameState state,
        EPlayer attacker,
        int fromX,
        int fromY,
        int targetX,
        int targetY)
    {
        EnsurePseudo(state);

        if (state == null ||
            !HexChessRules.IsValidCell(fromX, fromY) ||
            !HexChessRules.IsValidCell(targetX, targetY) ||
            fromX == targetX && fromY == targetY)
        {
            return false;
        }

        StatePiece? piece = state.GetPiece(fromX, fromY);
        if (!piece.HasValue ||
            !piece.Value.IsValid ||
            piece.Value.Player != attacker)
        {
            return false;
        }

        return CanGeometricAttack(
            state,
            new Vector2Int(fromX, fromY),
            piece.Value,
            targetX,
            targetY);
    }

    public static bool IsLegallyAttacked(
        GameState state,
        EPlayer attacker,
        int x,
        int y)
    {
        return GetLegalCaptureCount(
            state,
            attacker,
            x,
            y
        ) > 0;
    }
}
