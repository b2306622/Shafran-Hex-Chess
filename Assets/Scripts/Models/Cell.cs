using UnityEngine;

public class Cell : MonoBehaviour
{
    private Transform _cellSelectedObj;

    private Renderer _renderer;

    private ECellColor _color;
    private ECellState _state;

    private float _size = 1f;

    public CLocation Location { get; private set; }

    public BasePiece CurrentPiece { get; private set; }

    public float SIZE => _size;

    // =========================================================
    // STATE
    // =========================================================

    public ECellState State
    {
        get => _state;

        private set
        {
            _state = value;

            if (_cellSelectedObj != null)
            {
                _cellSelectedObj.gameObject.SetActive(
                    _state != ECellState.NORMAL
                );
            }

            if (_state == ECellState.SELECTED)
            {
                CurrentPiece?.BeSelected();
            }
        }
    }

    // =========================================================
    // COLOR
    // =========================================================

    public ECellColor Color
    {
        get => _color;

        set
        {
            _color = value;

            if (_renderer == null)
                return;

            ResourcesControllers resources =
                ResourcesControllers.Instance;

            if (resources == null)
                return;

            switch (_color)
            {
                case ECellColor.BLACK:
                    _renderer.sharedMaterial =
                        resources.BlackCellMaterial;
                    break;

                case ECellColor.WHITE:
                    _renderer.sharedMaterial =
                        resources.WhiteCellMaterial;
                    break;

                case ECellColor.GRAY:
                    _renderer.sharedMaterial =
                        resources.GrayCellMaterial;
                    break;
            }
        }
    }

    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();

        if (_renderer != null)
        {
            _size = _renderer.bounds.size.x;
        }

        if (transform.childCount > 1)
        {
            _cellSelectedObj =
                transform.GetChild(1);
        }
    }

    // =========================================================
    // SETTERS
    // =========================================================

    public void SetLocation(CLocation location)
    {
        Location = location;
    }

    public void SetCellState(ECellState cellState)
    {
        State = cellState;
    }

    public void SetPiece(BasePiece piece)
    {
        CurrentPiece = piece;
    }

    // =========================================================
    // DI CHUYỂN
    // =========================================================

    public void MakeAMove(Cell targetedCell)
    {
        if (CurrentPiece == null ||
            targetedCell == null)
        {
            return;
        }

        // En passant
        if (CurrentPiece is P_Pawn pawn &&
            pawn.IsEnPassantTarget(targetedCell))
        {
            pawn.EnPassantAttack(targetedCell);
            return;
        }

        CurrentPiece.Move(targetedCell);

        State = ECellState.NORMAL;
    }
}