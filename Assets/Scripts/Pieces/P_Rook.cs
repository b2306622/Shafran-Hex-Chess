using UnityEngine;

public class P_Rook : BasePiece
{
    public override void BeSelected()
    {
        _targetedCells.Clear();

        AddSlidingMoves(
            HexChessRules.RookDirections
        );

        HighlightTargetedCells();
    }

    // =========================================================
    // NHẬP THÀNH
    // Xe di chuyển nhưng KHÔNG đổi lượt
    // =========================================================

    public void CastlingMove(Cell targetCell)
    {
        if (targetCell == null)
            return;

        if (_currentCell != null)
        {
            _currentCell.SetCellState(
                ECellState.NORMAL
            );
        }

        SetNewLocation(targetCell);

        HasMoved = true;
    }
}