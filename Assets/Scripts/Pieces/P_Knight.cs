using UnityEngine;

public class P_Knight : BasePiece
{
    public override void BeSelected()
    {
        _targetedCells.Clear();

        foreach (Vector2Int move in HexChessRules.KnightMoves)
        {
            int x = Location.X + move.x;
            int y = Location.Y + move.y;

            Cell cell =
                ChessBoard.Current.GetCell(x, y);

            if (cell == null)
                continue;

            if (cell.CurrentPiece == null ||
                cell.CurrentPiece.Player != Player)
            {
                _targetedCells.Add(cell);
            }
        }

        HighlightTargetedCells();
    }
}