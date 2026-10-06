using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public abstract class BasePiece : MonoBehaviour
{
    protected readonly List<Cell> _targetedCells =
        new List<Cell>(16);

    protected Cell _currentCell;

    [SerializeField]
    private Vector3 offsetPosition;

    [SerializeField]
    protected EPlayer _player;

    public PieceInfo Info { get; private set; }

    public EPlayer Player
    {
        get => _player;
        protected set => _player = value;
    }

    public CLocation Location { get; private set; }

    public bool HasMoved { get; set; }

    // =========================================================
    // KHỞI TẠO
    // =========================================================

    public void SetInfo(
        PieceInfo info,
        Cell newCell,
        bool animateFromWhiteKing = true)
    {
        Info = info;

        if (newCell == null)
            return;

        _currentCell = newCell;

        newCell.SetPiece(this);

        Location = newCell.Location;

        Vector3 targetPosition =
            newCell.transform.position + offsetPosition;

        Vector3 spawnPosition = targetPosition;

        if (animateFromWhiteKing &&
            ChessBoard.Current != null)
        {
            Cell whiteKing =
                ChessBoard.Current.GetCell(4, 1);

            if (whiteKing != null)
            {
                spawnPosition =
                    whiteKing.transform.position +
                    offsetPosition;
            }
        }

        bool trainingMode =
            BaseGameController.Current != null &&
            BaseGameController.Current.TrainingMode;

        // Training reset should be deterministic and immediate.  The
        // individual move animation is still kept for visual debugging.
        if (trainingMode)
        {
            transform.position = targetPosition;
            transform.DOKill(false);
        }
        else
        {
            transform.position = spawnPosition;
            transform.DOKill(false);

            if (animateFromWhiteKing &&
                spawnPosition != targetPosition)
            {
                transform
                    .DOMove(targetPosition, 0.8f)
                    .SetEase(Ease.OutBack)
                    .SetUpdate(true);
            }
        }
    }

    // =========================================================
    // THAY ĐỔI VỊ TRÍ
    // =========================================================

    protected void SetNewLocation(Cell newCell)
    {
        if (newCell == null)
            return;

        if (_currentCell != null &&
            _currentCell != newCell)
        {
            _currentCell.SetPiece(null);
        }

        _currentCell = newCell;

        newCell.SetPiece(this);

        Location = newCell.Location;

        Vector3 targetPosition =
            ChessBoard.Current.CalculatePosition(
                Location.X,
                Location.Y
            );

        targetPosition += offsetPosition;

        bool trainingMode =
            BaseGameController.Current != null &&
            BaseGameController.Current.TrainingMode;

        // Do not block the ML-Agents environment on animation.  The board
        // state changes immediately, while DOTween only visualizes the move.
        // Unscaled time keeps the animation visible even when the trainer
        // changes Time.timeScale.
        float duration = trainingMode ? 0.10f : 0.35f;

        transform.DOKill(false);

        if (duration <= 0.001f)
        {
            transform.position = targetPosition;
            return;
        }

        transform
            .DOMove(targetPosition, duration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    // =========================================================
    // DI CHUYỂN
    // =========================================================

    public virtual void Move(Cell targetedCell)
    {
        if (targetedCell == null)
            return;

        if (targetedCell.CurrentPiece != null)
            return;

        CLocation from = Location;
        CLocation to = targetedCell.Location;

        SetNewLocation(targetedCell);

        HasMoved = true;

        BeUnselected();

        Move move = new Move(
            from,
            to,
            this
        );

        BaseGameController.Current?.RegisterMove(move);

        BaseGameController.Current?.SwitchTurn();
    }

    // =========================================================
    // ĂN QUÂN
    // =========================================================

    public virtual void Attack(Cell targetedCell)
    {
        if (targetedCell == null)
            return;

        BasePiece capturedPiece =
            targetedCell.CurrentPiece;

        if (capturedPiece == null)
            return;

        if (capturedPiece.Player == Player)
            return;

        CLocation from = Location;
        CLocation to = targetedCell.Location;

        bool capturedKing =
            capturedPiece is P_King;

        capturedPiece.BeAttackedBy(this);

        SetNewLocation(targetedCell);

        HasMoved = true;

        BeUnselected();

        Move move = new Move(
            from,
            to,
            this,
            capturedPiece
        );

        // Register the capture first so the winning King-capture move is
        // included in the authoritative game-ply counter and training log.
        BaseGameController.Current?.RegisterMove(move);

        if (capturedKing)
        {
            BaseGameController.Current?.GameOver(
                Player
            );
            return;
        }

        BaseGameController.Current?.SwitchTurn();
    }

    // =========================================================
    // BỊ ĂN
    // =========================================================

    public virtual void BeAttackedBy(BasePiece enemy)
    {
        transform.DOKill();

        if (_currentCell != null)
        {
            _currentCell.SetPiece(null);
        }

        Destroy(gameObject);
    }

    // =========================================================
    // CHỌN / BỎ CHỌN
    // =========================================================

    public abstract void BeSelected();

    public void BeUnselected()
    {
        for (int i = 0; i < _targetedCells.Count; i++)
        {
            Cell cell = _targetedCells[i];

            if (cell != null)
            {
                cell.SetCellState(ECellState.NORMAL);
            }
        }

        _targetedCells.Clear();
    }

    protected void HighlightTargetedCells()
    {
        for (int i = 0; i < _targetedCells.Count; i++)
        {
            Cell cell = _targetedCells[i];

            if (cell != null)
            {
                cell.SetCellState(ECellState.TARGETED);
            }
        }
    }

    // =========================================================
    // THÊM MỘT NƯỚC ĐI
    // =========================================================

    protected bool TryAddMove(int x, int y)
    {
        Cell cell =
            ChessBoard.Current.GetCell(x, y);

        if (cell == null)
            return false;

        if (cell.CurrentPiece == null)
        {
            _targetedCells.Add(cell);
            return true;
        }

        if (cell.CurrentPiece.Player != Player)
        {
            _targetedCells.Add(cell);
        }

        return false;
    }

    // =========================================================
    // NƯỚC ĐI DẠNG TRƯỢT
    // =========================================================

    protected void AddSlidingMoves(
        Vector2Int[] directions)
    {
        foreach (Vector2Int dir in directions)
        {
            int x = Location.X + dir.x;
            int y = Location.Y + dir.y;

            while (true)
            {
                Cell cell =
                    ChessBoard.Current.GetCell(x, y);

                if (cell == null)
                    break;

                if (cell.CurrentPiece == null)
                {
                    _targetedCells.Add(cell);
                }
                else
                {
                    if (cell.CurrentPiece.Player != Player)
                    {
                        _targetedCells.Add(cell);
                    }

                    break;
                }

                x += dir.x;
                y += dir.y;
            }
        }
    }
}