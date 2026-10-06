using UnityEngine;

public static class AlphaZeroActionEncoder
{
    public const int CellCount = HexChessRules.CELL_COUNT;
    public const int ActionCount = CellCount * CellCount;

    public static int Encode(StateMove move)
    {
        int from = HexChessRules.GetCellIndex(move.From.X, move.From.Y);
        int to = HexChessRules.GetCellIndex(move.To.X, move.To.Y);

        if (from < 0 || to < 0)
            return -1;

        return from * CellCount + to;
    }

    public static bool TryDecode(int action, out CLocation from, out CLocation to)
    {
        from = default;
        to = default;

        if (action < 0 || action >= ActionCount)
            return false;

        int fromIndex = action / CellCount;
        int toIndex = action % CellCount;

        Vector2Int fromCell = HexChessRules.GetCellCoordinates(fromIndex);
        Vector2Int toCell = HexChessRules.GetCellCoordinates(toIndex);

        if (!HexChessRules.IsValidCell(fromCell.x, fromCell.y) ||
            !HexChessRules.IsValidCell(toCell.x, toCell.y))
        {
            return false;
        }

        from = new CLocation(fromCell.x, fromCell.y);
        to = new CLocation(toCell.x, toCell.y);
        return true;
    }
}
