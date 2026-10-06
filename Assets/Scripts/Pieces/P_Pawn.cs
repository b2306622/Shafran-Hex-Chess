using DG.Tweening;
using UnityEngine;

public class P_Pawn : BasePiece
{
    // =========================================================
    // CHỌN TỐT
    // =========================================================

    public override void BeSelected()
    {
        _targetedCells.Clear();

        int direction =
            Player == EPlayer.WHITE
                ? 1
                : -1;

        int file = Location.X;

        // -----------------------------------------------------
        // 1. ĐI 1 Ô
        // -----------------------------------------------------

        AddForwardMove(
            Location.X,
            Location.Y + direction
        );

        // -----------------------------------------------------
        // 2. ĐI 2 / 3 Ô Ở NƯỚC ĐẦU
        // -----------------------------------------------------

        if (!HasMoved)
        {
            int startRank =
                Player == EPlayer.WHITE
                    ? HexChessRules.GetWhitePawnStartRank(file)
                    : HexChessRules.GetBlackPawnStartRank(file);

            int maxDistance =
                HexChessRules.GetInitialPawnMoveDistance(file);

            if (Location.Y == startRank)
            {
                for (int distance = 2;
                     distance <= maxDistance;
                     distance++)
                {
                    int targetY =
                        Location.Y +
                        direction * distance;

                    if (!AddForwardMove(
                        Location.X,
                        targetY))
                    {
                        break;
                    }
                }
            }
        }

        // -----------------------------------------------------
        // 3. ĂN CHÉO
        // -----------------------------------------------------

        AddAttackMove(
            Location.X - 1,
            Location.Y + direction
        );

        AddAttackMove(
            Location.X + 1,
            Location.Y + direction
        );

        // -----------------------------------------------------
        // 4. EN PASSANT
        // -----------------------------------------------------

        AddEnPassantMove(
            Location.X - 1,
            Location.Y + direction
        );

        AddEnPassantMove(
            Location.X + 1,
            Location.Y + direction
        );

        HighlightTargetedCells();
    }

    // =========================================================
    // ĐI THẲNG
    // =========================================================

    private bool AddForwardMove(int x, int y)
    {
        Cell cell =
            ChessBoard.Current.GetCell(x, y);

        if (cell == null)
            return false;

        if (cell.CurrentPiece != null)
            return false;

        _targetedCells.Add(cell);

        return true;
    }

    // =========================================================
    // ĂN CHÉO
    // =========================================================

    private void AddAttackMove(int x, int y)
    {
        Cell cell =
            ChessBoard.Current.GetCell(x, y);

        if (cell == null)
            return;

        if (cell.CurrentPiece == null)
            return;

        if (cell.CurrentPiece.Player == Player)
            return;

        _targetedCells.Add(cell);
    }

    // =========================================================
    // KIỂM TRA EN PASSANT
    // =========================================================

    private void AddEnPassantMove(int x, int y)
    {
        Cell targetCell =
            ChessBoard.Current.GetCell(x, y);

        if (targetCell == null)
            return;

        if (targetCell.CurrentPiece != null)
            return;

        if (IsEnPassantTarget(targetCell))
        {
            _targetedCells.Add(targetCell);
        }
    }

    public bool IsEnPassantTarget(Cell targetCell)
    {
        if (targetCell == null)
            return false;

        if (targetCell.CurrentPiece != null)
            return false;

        BaseGameController game =
            BaseGameController.Current;

        if (game == null)
            return false;

        Move lastMove = game.LastMove;

        if (lastMove == null)
            return false;

        if (!(lastMove.MovedPiece is P_Pawn))
            return false;

        if (lastMove.MovedPiece.Player == Player)
            return false;

        int moveDistance =
            Mathf.Abs(
                lastMove.From.Y -
                lastMove.To.Y
            );

        // Shafran:
        // chỉ multi-step mới tạo quyền en passant
        if (moveDistance <= 1)
            return false;

        // Quân tốt vừa đi phải kết thúc cạnh tốt hiện tại
        if (lastMove.To.X != targetCell.Location.X)
            return false;

        if (lastMove.To.Y != Location.Y)
            return false;

        int direction =
            Player == EPlayer.WHITE
                ? 1
                : -1;

        if (targetCell.Location.Y !=
            Location.Y + direction)
        {
            return false;
        }

        Cell adjacentCell =
            ChessBoard.Current.GetCell(
                lastMove.To.X,
                lastMove.To.Y
            );

        if (adjacentCell == null)
            return false;

        if (!(adjacentCell.CurrentPiece is P_Pawn enemyPawn))
            return false;

        return enemyPawn.Player != Player;
    }

    // =========================================================
    // EN PASSANT ATTACK
    // =========================================================

    public void EnPassantAttack(Cell targetCell)
    {
        if (!IsEnPassantTarget(targetCell))
            return;

        int capturedX =
            targetCell.Location.X;

        int capturedY =
            Location.Y;

        Cell capturedCell =
            ChessBoard.Current.GetCell(
                capturedX,
                capturedY
            );

        if (capturedCell == null)
            return;

        BasePiece capturedPiece =
            capturedCell.CurrentPiece;

        if (capturedPiece == null)
            return;

        CLocation from = Location;
        CLocation to = targetCell.Location;

        capturedPiece.BeAttackedBy(this);

        SetNewLocation(targetCell);

        HasMoved = true;

        BeUnselected();

        Move move = new Move(
            from,
            to,
            this,
            capturedPiece
        );

        move.IsEnPassant = true;
        move.CapturedLocation =
            capturedCell.Location;

        BaseGameController.Current?.RegisterMove(move);

        BaseGameController.Current?.SwitchTurn();

        CheckPromotion();
    }

    // =========================================================
    // DI CHUYỂN BÌNH THƯỜNG
    // =========================================================

    public override void Move(Cell targetedCell)
    {
        base.Move(targetedCell);

        CheckPromotion();
    }

    public override void Attack(Cell targetedCell)
    {
        base.Attack(targetedCell);

        CheckPromotion();
    }

    // =========================================================
    // PHONG CẤP
    // =========================================================

    private void CheckPromotion()
    {
        if (HexChessRules.IsPromotionRank(
            Player,
            Location.X,
            Location.Y))
        {
            PromoteToQueen();
        }
    }

    private void PromoteToQueen()
    {
        transform.DOKill();

        string queenPath =
            Player == EPlayer.WHITE
                ? "Pieces/W_QUEEN"
                : "Pieces/B_QUEEN";

        GameObject prefab =
            ResourcesControllers.Instance
                .GetGameObject(queenPath);

        if (prefab == null)
        {
            Debug.LogError(
                $"Cannot promote pawn. Missing prefab: {queenPath}"
            );

            return;
        }

        Transform parent =
            ChessBoard.Current.PieceContainer;

        GameObject queenGO =
            Object.Instantiate(
                prefab,
                parent
            );

        queenGO.name =
            Player == EPlayer.WHITE
                ? "W_QUEEN_PROMOTED"
                : "B_QUEEN_PROMOTED";

        P_Queen newQueen =
            queenGO.GetComponent<P_Queen>();

        if (newQueen == null)
        {
            Debug.LogError(
                "[P_Pawn] Promoted prefab does not contain P_Queen."
            );

            Object.Destroy(queenGO);
            return;
        }

        PieceInfo info =
            new PieceInfo
            {
                Name = queenGO.name,
                Path = queenPath,
                X = Location.X,
                Y = Location.Y
            };

        newQueen.SetInfo(
            info,
            _currentCell,
            false
        );

        newQueen.HasMoved = true;

        int episodeId = ChessAgent.DebugEpisodeId;
        int ply = BaseGameController.Current != null
            ? BaseGameController.Current.GamePlyCount
            : -1;

        bool cellHasQueen =
            _currentCell != null &&
            _currentCell.CurrentPiece is P_Queen;

        // Verify the promotion immediately after SetInfo(): both the Unity
        // cell and the AI GameState must see the promoted Queen.
        GameState verifyState = GameState.FromUnityBoard();
        StatePiece? promotedStatePiece =
            verifyState?.GetPiece(Location.X, Location.Y);

        bool stateHasPromotedQueen =
            promotedStatePiece.HasValue &&
            promotedStatePiece.Value.IsValid &&
            promotedStatePiece.Value.Type == StatePieceType.QUEEN &&
            promotedStatePiece.Value.Player == Player;

        int whiteQueens = 0;
        int blackQueens = 0;

        if (verifyState != null)
        {
            foreach (Vector2Int position in HexChessRules.CellCoordinates)
            {
                StatePiece? piece =
                    verifyState.Board[position.x, position.y];

                if (!piece.HasValue ||
                    !piece.Value.IsValid ||
                    piece.Value.Type != StatePieceType.QUEEN)
                {
                    continue;
                }

                if (piece.Value.Player == EPlayer.WHITE)
                    whiteQueens++;
                else if (piece.Value.Player == EPlayer.BLACK)
                    blackQueens++;
            }
        }

        ShafranDebug.Log(
            $"[SHAFRAN PROMOTION] Episode={episodeId} | " +
            $"Ply={ply} | {Player} Pawn -> QUEEN | " +
            $"Location=({Location.X},{Location.Y}) | " +
            $"Object={queenGO.name} | " +
            $"UnityCellQueen={cellHasQueen} | " +
            $"GameStateQueen={stateHasPromotedQueen} | " +
            $"WHITE_Q={whiteQueens} | BLACK_Q={blackQueens}"
        );

        Object.Destroy(gameObject);
    }
}