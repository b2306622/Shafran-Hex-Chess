public class P_Queen : BasePiece
{
    public override void BeSelected()
    {
        _targetedCells.Clear();

        AddSlidingMoves(
            HexChessRules.QueenDirections
        );

        HighlightTargetedCells();
    }
}