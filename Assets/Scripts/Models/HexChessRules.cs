using System.Collections.Generic;
using UnityEngine;

public static class HexChessRules
{
    public const int FILE_COUNT = 9;
    public const int MAX_RANK = 10;
    public const int CELL_COUNT = 70;

    // =========================================================
    // 1. KIỂM TRA Ô HỢP LỆ
    // =========================================================

    public static bool IsValidCell(int x, int y)
    {
        if (x < 0 || x >= FILE_COUNT)
            return false;

        if (y < 1 || y > MAX_RANK)
            return false;

        int minRank = Mathf.Max(1, x - 3);
        int maxRank = Mathf.Min(10, x + 6);

        return y >= minRank && y <= maxRank;
    }

    public static int GetMinRank(int file)
    {
        return Mathf.Max(1, file - 3);
    }

    public static int GetMaxRank(int file)
    {
        return Mathf.Min(10, file + 6);
    }

    // =========================================================
    // 2. 6 HƯỚNG XE
    // =========================================================

    public static readonly Vector2Int[] RookDirections =
    {
        new Vector2Int( 0,  1),
        new Vector2Int( 1,  1),
        new Vector2Int( 1,  0),
        new Vector2Int( 0, -1),
        new Vector2Int(-1, -1),
        new Vector2Int(-1,  0)
    };

    // =========================================================
    // 3. 6 HƯỚNG TƯỢNG
    // =========================================================

    public static readonly Vector2Int[] BishopDirections =
    {
        new Vector2Int( 1, -1),
        new Vector2Int(-1,  1),
        new Vector2Int( 1,  2),
        new Vector2Int(-1, -2),
        new Vector2Int( 2,  1),
        new Vector2Int(-2, -1)
    };

    // =========================================================
    // 4. 12 HƯỚNG HẬU / VUA
    // =========================================================

    public static readonly Vector2Int[] QueenDirections =
    {
        // Xe
        new Vector2Int( 0,  1),
        new Vector2Int( 1,  1),
        new Vector2Int( 1,  0),
        new Vector2Int( 0, -1),
        new Vector2Int(-1, -1),
        new Vector2Int(-1,  0),

        // Tượng
        new Vector2Int( 1, -1),
        new Vector2Int(-1,  1),
        new Vector2Int( 1,  2),
        new Vector2Int(-1, -2),
        new Vector2Int( 2,  1),
        new Vector2Int(-2, -1)
    };

    // =========================================================
    // 5. 12 NƯỚC MÃ
    // =========================================================

    public static readonly Vector2Int[] KnightMoves;

    // =========================================================
    // 6. DANH SÁCH 70 Ô HỢP LỆ
    // =========================================================

    public static readonly Vector2Int[] CellCoordinates;

    private static readonly int[,] _cellIndexMap =
        new int[FILE_COUNT, MAX_RANK + 1];

    static HexChessRules()
    {
        // Khởi tạo map = -1
        for (int x = 0; x < FILE_COUNT; x++)
        {
            for (int y = 0; y <= MAX_RANK; y++)
            {
                _cellIndexMap[x, y] = -1;
            }
        }

        List<Vector2Int> cells = new List<Vector2Int>(CELL_COUNT);

        for (int x = 0; x < FILE_COUNT; x++)
        {
            int minRank = GetMinRank(x);
            int maxRank = GetMaxRank(x);

            for (int y = minRank; y <= maxRank; y++)
            {
                _cellIndexMap[x, y] = cells.Count;
                cells.Add(new Vector2Int(x, y));
            }
        }

        CellCoordinates = cells.ToArray();

        // Sinh 12 nước Mã một lần duy nhất
        List<Vector2Int> knightMoves = new List<Vector2Int>(12);

        for (int i = 0; i < RookDirections.Length; i++)
        {
            Vector2Int dir = RookDirections[i];

            Vector2Int side1 =
                RookDirections[(i + 1) % 6];

            Vector2Int side2 =
                RookDirections[(i + 5) % 6];

            Vector2Int move1 = dir * 2 + side1;
            Vector2Int move2 = dir * 2 + side2;

            if (!knightMoves.Contains(move1))
                knightMoves.Add(move1);

            if (!knightMoves.Contains(move2))
                knightMoves.Add(move2);
        }

        KnightMoves = knightMoves.ToArray();
    }

    public static int GetCellIndex(int x, int y)
    {
        if (!IsValidCell(x, y))
            return -1;

        return _cellIndexMap[x, y];
    }

    public static Vector2Int GetCellCoordinates(int index)
    {
        if (index < 0 || index >= CellCoordinates.Length)
            return new Vector2Int(-1, -1);

        return CellCoordinates[index];
    }

    // =========================================================
    // 7. TỐT - VỊ TRÍ BAN ĐẦU
    // =========================================================

    public static int GetWhitePawnStartRank(int file)
    {
        switch (file)
        {
            case 0:
            case 1:
            case 2:
            case 3:
            case 4:
                return 2;

            case 5:
                return 3;

            case 6:
                return 4;

            case 7:
                return 5;

            case 8:
                return 6;
        }

        return -1;
    }

    public static int GetBlackPawnStartRank(int file)
    {
        switch (file)
        {
            case 0:
                return 5;

            case 1:
                return 6;

            case 2:
                return 7;

            case 3:
                return 8;

            case 4:
            case 5:
            case 6:
            case 7:
            case 8:
                return 9;
        }

        return -1;
    }

    // =========================================================
    // 8. SỐ Ô TỐT ĐƯỢC ĐI Ở NƯỚC ĐẦU
    // =========================================================

    public static int GetInitialPawnMoveDistance(int file)
    {
        switch (file)
        {
            case 0:
            case 8:
                return 1;

            case 1:
            case 2:
            case 6:
            case 7:
                return 2;

            case 3:
            case 4:
            case 5:
                return 3;
        }

        return 1;
    }

    // =========================================================
    // 9. PHONG CẤP
    // =========================================================

    public static bool IsPromotionRank(
        EPlayer player,
        int x,
        int y)
    {
        if (player == EPlayer.WHITE)
            return y == GetMaxRank(x);

        return y == GetMinRank(x);
    }

    // =========================================================
    // 10. MÀU Ô
    // =========================================================

    public static int GetCellColorIndex(int x, int y)
    {
        int value = (x + y) % 3;

        if (value < 0)
            value += 3;

        return value;
    }

    // =========================================================
    // STRATEGIC PHASE DETECTION
    // =========================================================
    // Shared by PPO reward, Minimax Evaluation and AlphaZero heuristic.
    // The board starts with 34 non-king pieces, so the phase thresholds
    // are calibrated to this project's 70-cell setup rather than standard
    // 8x8 chess.
    //
    // opening       = development / center / king safety
    // middlegame    = tactics / initiative / piece coordination
    // endgame       = conversion / king activity / promotion
    // =========================================================

    public static void GetStrategicPhase(
        GameState state,
        EPlayer perspective,
        out float opening,
        out float middlegame,
        out float endgame)
    {
        opening = 0f;
        middlegame = 0f;
        endgame = 0f;

        if (state == null)
        {
            return;
        }

        EPlayer opponent =
            GetOpponent(perspective);

        int ownNonKing =
            CountNonKingPieces(
                state,
                perspective
            );

        int opponentNonKing =
            CountNonKingPieces(
                state,
                opponent
            );

        int totalNonKing =
            ownNonKing +
            opponentNonKing;

        // Shafran starts with 34 non-king pieces on the 70-cell board.
        // Keep the opening phase meaningful for the first exchanges, but
        // move into a tactical middlegame earlier than the previous V3
        // thresholds so the agents stop over-valuing quiet development.
        opening = Mathf.InverseLerp(
            20f,
            32f,
            totalNonKing
        );

        // Conversion should begin before the absolute endgame. This is
        // particularly important in Shafran because open hex-lines and the
        // wider mobility of the board can otherwise produce long technical
        // phases after one side is already clearly ahead.
        float countEndgame =
            Mathf.InverseLerp(
                16f,
                6f,
                totalNonKing
            );

        float ownMaterial =
            CalculateNonKingMaterial(
                state,
                perspective
            );

        float opponentMaterial =
            CalculateNonKingMaterial(
                state,
                opponent
            );

        float materialLead =
            ownMaterial -
            opponentMaterial;

        // A material advantage should trigger conversion thinking, but
        // not too early in the opening.
        float materialGate =
            Mathf.InverseLerp(
                24f,
                10f,
                totalNonKing
            );

        float leadEndgame =
            Mathf.InverseLerp(
                2.5f,
                6.5f,
                materialLead
            ) *
            materialGate *
            0.80f;

        endgame =
            Mathf.Clamp01(
                Mathf.Max(
                    countEndgame,
                    leadEndgame
                )
            );

        // Middlegame is the residual phase. This is deliberately explicit
        // instead of simply treating every non-endgame position as opening.
        middlegame =
            Mathf.Clamp01(
                1f -
                Mathf.Max(
                    opening,
                    endgame
                )
            );

        // Normalize overlap so opening + middlegame + endgame ~= 1.
        float total =
            opening +
            middlegame +
            endgame;

        if (total > 0.0001f)
        {
            opening /= total;
            middlegame /= total;
            endgame /= total;
        }
    }

    public static float CalculateNonKingMaterial(
        GameState state,
        EPlayer player)
    {
        if (state == null)
        {
            return 0f;
        }

        float total = 0f;

        foreach (Vector2Int position in CellCoordinates)
        {
            StatePiece? piece =
                state.GetPiece(
                    position.x,
                    position.y
                );

            if (!piece.HasValue ||
                !piece.Value.IsValid ||
                piece.Value.Player != player ||
                piece.Value.Type == StatePieceType.KING)
            {
                continue;
            }

            total +=
                piece.Value.Value /
                1000f;
        }

        return total;
    }

    // =========================================================
    // 10B. KHOẢNG CÁCH HEX CHÍNH XÁC
    // =========================================================
    // Board coordinates use six rook-neighbor directions:
    //   (0,1), (1,1), (1,0) and their opposites.
    // Mapping to axial/cube coordinates gives the exact graph distance:
    //   max(|dx|, |dy|, |dy - dx|).
    // =========================================================

    public static int HexDistance(
        int x1,
        int y1,
        int x2,
        int y2)
    {
        int dx = Mathf.Abs(x2 - x1);
        int dy = Mathf.Abs(y2 - y1);
        int dxy = Mathf.Abs((y2 - y1) - (x2 - x1));

        return Mathf.Max(
            dx,
            Mathf.Max(dy, dxy)
        );
    }

    // =========================================================
    // 11. ĐỐI THỦ
    // =========================================================

    public static EPlayer GetOpponent(EPlayer player)
    {
        return player == EPlayer.WHITE
            ? EPlayer.BLACK
            : EPlayer.WHITE;
    }

    // =========================================================
    // 12. SHAFRAN MATERIAL / INSUFFICIENT-MATERIAL RULES
    // =========================================================
    //
    // V10 does NOT use "only King remaining" as a win condition.
    // A position such as K+Q vs K or K+R vs K remains playable until
    // checkmate.
    //
    // Automatic draw cases configured for this project:
    //   K vs K
    //   K+B vs K
    //   K+N vs K
    //   K+2B vs K
    //
    // This helper is shared by PPO / Minimax / AlphaZero so the terminal
    // definition stays consistent across all AI implementations.
    // =========================================================

    public static int CountNonKingPieces(
        GameState state,
        EPlayer player)
    {
        if (state == null)
        {
            return 0;
        }

        int count = 0;

        foreach (
            Vector2Int position
            in CellCoordinates)
        {
            StatePiece? piece =
                state.GetPiece(
                    position.x,
                    position.y
                );

            if (!piece.HasValue ||
                !piece.Value.IsValid ||
                piece.Value.Player != player ||
                piece.Value.Type == StatePieceType.KING)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    public static bool HasKing(
        GameState state,
        EPlayer player)
    {
        if (state == null)
        {
            return false;
        }

        foreach (
            Vector2Int position
            in CellCoordinates)
        {
            StatePiece? piece =
                state.GetPiece(
                    position.x,
                    position.y
                );

            if (piece.HasValue &&
                piece.Value.IsValid &&
                piece.Value.Player == player &&
                piece.Value.Type == StatePieceType.KING)
            {
                return true;
            }
        }

        return false;
    }

    // =========================================================
    // INSUFFICIENT MATERIAL
    // =========================================================

    public static bool IsInsufficientMaterial(
        GameState state,
        out bool draw)
    {
        draw = false;

        if (state == null ||
            !HasKing(state, EPlayer.WHITE) ||
            !HasKing(state, EPlayer.BLACK))
        {
            return false;
        }

        bool whiteOnlyKing =
            HasOnlyKing(
                state,
                EPlayer.WHITE
            );

        bool blackOnlyKing =
            HasOnlyKing(
                state,
                EPlayer.BLACK
            );

        // K vs K
        if (whiteOnlyKing &&
            blackOnlyKing)
        {
            draw = true;
            return true;
        }

        // K+B vs K, K+N vs K, K+2B vs K
        if (whiteOnlyKing &&
            IsInsufficientMinorOnlySide(
                state,
                EPlayer.BLACK))
        {
            draw = true;
            return true;
        }

        if (blackOnlyKing &&
            IsInsufficientMinorOnlySide(
                state,
                EPlayer.WHITE))
        {
            draw = true;
            return true;
        }

        return false;
    }

    private static bool HasOnlyKing(
        GameState state,
        EPlayer player)
    {
        return CountNonKingPieces(
            state,
            player
        ) == 0;
    }

    private static bool IsInsufficientMinorOnlySide(
        GameState state,
        EPlayer player)
    {
        int nonKingCount =
            CountNonKingPieces(
                state,
                player
            );

        // Exactly B, N, or BB.
        if (nonKingCount < 1 ||
            nonKingCount > 2)
        {
            return false;
        }

        int bishopCount = 0;
        int knightCount = 0;

        foreach (
            Vector2Int position
            in CellCoordinates)
        {
            StatePiece? piece =
                state.GetPiece(
                    position.x,
                    position.y
                );

            if (!piece.HasValue ||
                !piece.Value.IsValid ||
                piece.Value.Player != player)
            {
                continue;
            }

            switch (piece.Value.Type)
            {
                case StatePieceType.BISHOP:
                    bishopCount++;
                    break;

                case StatePieceType.KNIGHT:
                    knightCount++;
                    break;

                case StatePieceType.KING:
                    break;

                default:
                    return false;
            }
        }

        // K+B vs K
        if (bishopCount == 1 &&
            knightCount == 0)
        {
            return true;
        }

        // K+N vs K
        if (bishopCount == 0 &&
            knightCount == 1)
        {
            return true;
        }

        // K+2B vs K
        if (bishopCount == 2 &&
            knightCount == 0)
        {
            return true;
        }

        return false;
    }

    // Backward-compatible API. It now implements the V10 insufficient-
    // material rule instead of the old "material elimination = win" rule.
    public static bool TryGetMaterialTerminalResult(
        GameState state,
        out EPlayer? winner,
        out bool draw)
    {
        winner = null;
        draw = false;

        if (IsInsufficientMaterial(
                state,
                out draw))
        {
            return true;
        }

        return false;
    }

    // =========================================================
    // 13. KIỂM TRA DELTA CÓ THEO MỘT HƯỚNG HỢP LỆ
    // =========================================================

    public static bool TryGetDirection(
        Vector2Int delta,
        Vector2Int[] directions,
        out Vector2Int direction,
        out int distance)
    {
        foreach (Vector2Int dir in directions)
        {
            int currentX = 0;
            int currentY = 0;

            for (int step = 1; step <= 10; step++)
            {
                currentX += dir.x;
                currentY += dir.y;

                if (currentX == delta.x &&
                    currentY == delta.y)
                {
                    direction = dir;
                    distance = step;
                    return true;
                }

                // Nếu vượt quá khoảng cách hữu ích
                if (Mathf.Abs(currentX) > 10 ||
                    Mathf.Abs(currentY) > 10)
                {
                    break;
                }
            }
        }

        direction = Vector2Int.zero;
        distance = 0;

        return false;
    }
}