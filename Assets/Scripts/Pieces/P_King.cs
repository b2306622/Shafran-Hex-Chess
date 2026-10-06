using UnityEngine;

public class P_King : BasePiece
{
    // =========================================================
    // CHỌN VUA
    // =========================================================

    public override void BeSelected()
    {
        _targetedCells.Clear();

        EPlayer enemy =
            HexChessRules.GetOpponent(Player);

        // -----------------------------------------------------
        // VUA ĐI 1 Ô THEO 12 HƯỚNG
        // -----------------------------------------------------

        foreach (Vector2Int dir in
                 HexChessRules.QueenDirections)
        {
            Cell cell =
                ChessBoard.Current.GetCell(
                    Location.X + dir.x,
                    Location.Y + dir.y
                );

            if (cell == null)
                continue;

            if (cell.CurrentPiece != null &&
                cell.CurrentPiece.Player == Player)
            {
                continue;
            }

            if (ChessBoard.Current.IsSquareAttacked(
                cell.Location.X,
                cell.Location.Y,
                enemy))
            {
                continue;
            }

            _targetedCells.Add(cell);
        }

        // -----------------------------------------------------
        // NHẬP THÀNH
        // -----------------------------------------------------

        CheckCastling(enemy);

        HighlightTargetedCells();
    }

    // =========================================================
    // KIỂM TRA NHẬP THÀNH
    // =========================================================

    private void CheckCastling(EPlayer enemy)
    {
        if (HasMoved)
            return;

        // Không được nhập thành khi đang bị chiếu
        if (ChessBoard.Current.IsSquareAttacked(
            Location.X,
            Location.Y,
            enemy))
        {
            return;
        }

        foreach (Vector2Int dir in
                 HexChessRules.RookDirections)
        {
            // Nhập thành ngắn
            if (CanCastle(
                dir,
                2,
                3,
                enemy))
            {
                AddCastleTarget(dir, 2);
            }

            // Nhập thành dài
            if (CanCastle(
                dir,
                3,
                2,
                enemy))
            {
                AddCastleTarget(dir, 3);
            }
        }
    }

    private void AddCastleTarget(
        Vector2Int dir,
        int kingDistance)
    {
        Cell target =
            ChessBoard.Current.GetCell(
                Location.X + dir.x * kingDistance,
                Location.Y + dir.y * kingDistance
            );

        if (target == null)
            return;

        if (target.CurrentPiece != null)
            return;

        if (!_targetedCells.Contains(target))
        {
            _targetedCells.Add(target);
        }
    }

    // =========================================================
    // ĐIỀU KIỆN NHẬP THÀNH
    // =========================================================

    private bool CanCastle(
        Vector2Int dir,
        int kingDistance,
        int rookDistance,
        EPlayer enemy)
    {
        P_Rook rook =
            FindCastlingRook(dir);

        if (rook == null)
            return false;

        if (rook.HasMoved)
            return false;

        // Xe phải cách Vua đúng 4 ô
        if (GetRookDistance(dir) != 4)
            return false;

        // -----------------------------------------------------
        // Kiểm tra đường Vua
        // -----------------------------------------------------

        for (int i = 1;
             i <= kingDistance;
             i++)
        {
            Cell cell =
                ChessBoard.Current.GetCell(
                    Location.X + dir.x * i,
                    Location.Y + dir.y * i
                );

            if (cell == null)
                return false;

            if (cell.CurrentPiece != null)
                return false;

            if (ChessBoard.Current.IsSquareAttacked(
                cell.Location.X,
                cell.Location.Y,
                enemy))
            {
                return false;
            }
        }

        // -----------------------------------------------------
        // Ô Xe sẽ tới
        // -----------------------------------------------------

        Cell rookTarget =
            ChessBoard.Current.GetCell(
                Location.X + dir.x * rookDistance,
                Location.Y + dir.y * rookDistance
            );

        if (rookTarget == null)
            return false;

        if (rookTarget.CurrentPiece != null)
            return false;

        return true;
    }

    // =========================================================
    // TÌM XE
    // =========================================================

    private P_Rook FindCastlingRook(Vector2Int dir)
    {
        for (int distance = 1;
             distance <= 4;
             distance++)
        {
            Cell cell =
                ChessBoard.Current.GetCell(
                    Location.X + dir.x * distance,
                    Location.Y + dir.y * distance
                );

            if (cell == null)
                return null;

            if (cell.CurrentPiece == null)
                continue;

            if (cell.CurrentPiece is P_Rook rook &&
                rook.Player == Player)
            {
                return distance == 4
                    ? rook
                    : null;
            }

            return null;
        }

        return null;
    }

    // =========================================================
    // KHOẢNG CÁCH XE
    // =========================================================

    private int GetRookDistance(Vector2Int dir)
    {
        for (int distance = 1;
             distance <= 4;
             distance++)
        {
            Cell cell =
                ChessBoard.Current.GetCell(
                    Location.X + dir.x * distance,
                    Location.Y + dir.y * distance
                );

            if (cell == null)
                return -1;

            if (cell.CurrentPiece == null)
                continue;

            if (cell.CurrentPiece is P_Rook rook &&
                rook.Player == Player)
            {
                return distance;
            }

            return -1;
        }

        return -1;
    }

    // =========================================================
    // DI CHUYỂN
    // =========================================================

    public override void Move(Cell targetedCell)
    {
        if (targetedCell == null)
            return;

        Vector2Int delta =
            new Vector2Int(
                targetedCell.Location.X - Location.X,
                targetedCell.Location.Y - Location.Y
            );

        if (TryGetCastlingData(
            targetedCell,
            delta,
            out P_Rook rook,
            out Cell rookTarget,
            out int kingDistance))
        {
            CLocation rookFrom =
                rook.Location;

            CLocation rookTo =
                rookTarget.Location;

            // Xe đi trước, nhưng không đổi lượt
            rook.CastlingMove(rookTarget);

            // Vua đi
            base.Move(targetedCell);

            // Cập nhật Move cuối thành nhập thành
            if (BaseGameController.Current != null &&
                BaseGameController.Current.LastMove != null)
            {
                Move lastMove =
                    BaseGameController.Current.LastMove;

                lastMove.IsCastling = true;
                lastMove.RookPiece = rook;
                lastMove.RookFrom = rookFrom;
                lastMove.RookTo = rookTo;
            }

            return;
        }

        base.Move(targetedCell);
    }

    // =========================================================
    // XÁC ĐỊNH NHẬP THÀNH
    // =========================================================

    private bool TryGetCastlingData(
        Cell targetCell,
        Vector2Int delta,
        out P_Rook rook,
        out Cell rookTarget,
        out int kingDistance)
    {
        rook = null;
        rookTarget = null;
        kingDistance = 0;

        foreach (Vector2Int dir in
                 HexChessRules.RookDirections)
        {
            if (delta == dir * 2)
            {
                kingDistance = 2;
            }
            else if (delta == dir * 3)
            {
                kingDistance = 3;
            }
            else
            {
                continue;
            }

            int rookDistance =
                5 - kingDistance;

            rook =
                FindCastlingRook(dir);

            if (rook == null ||
                rook.HasMoved)
            {
                return false;
            }

            rookTarget =
                ChessBoard.Current.GetCell(
                    Location.X + dir.x * rookDistance,
                    Location.Y + dir.y * rookDistance
                );

            if (rookTarget == null ||
                rookTarget.CurrentPiece != null)
            {
                return false;
            }

            EPlayer enemy =
                HexChessRules.GetOpponent(Player);

            if (!CanCastle(
                dir,
                kingDistance,
                rookDistance,
                enemy))
            {
                return false;
            }

            return true;
        }

        return false;
    }

    // =========================================================
    // VUA BỊ ĂN
    // =========================================================

    public override void BeAttackedBy(BasePiece enemy)
    {
        // BasePiece.Attack() is the single authoritative place that
        // registers the capture and then closes the game. Keeping this
        // method focused on removing the captured King prevents the final
        // winning ply from being dropped by RegisterMove().
        base.BeAttackedBy(enemy);
    }
}