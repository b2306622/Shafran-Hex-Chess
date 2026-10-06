public class P_Bishop : BasePiece
{
    public override void BeSelected()
    {
        _targetedCells.Clear();

        AddSlidingMoves(
            HexChessRules.BishopDirections
        );

        HighlightTargetedCells();
    }
}