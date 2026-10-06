using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class ChessBoard : MonoBehaviour
{
    public static ChessBoard Current { get; private set; }

    [Header("Board Setup")]
    [SerializeField]
    private GameObject cellPrefab;

    [SerializeField]
    private LayerMask cellLayerMask = 0;

    [SerializeField]
    private Transform cellContainer;

    [SerializeField]
    private Transform pieceContainer;

    [Header("Positioning")]
    [SerializeField]
    private Vector3 boardOrigin = Vector3.zero;

    private Cell[][] _cells;

    private float _cellSize = -1f;

    private readonly List<BasePiece> _pieces =
        new List<BasePiece>();

    private readonly List<Cell> _flatCellList =
        new List<Cell>(HexChessRules.CELL_COUNT);

    private Cell _currentSelectedCell;

    public Cell[][] Cells => _cells;

    public IReadOnlyList<Cell> AllCells =>
        _flatCellList;

    public Transform PieceContainer =>
        pieceContainer != null
            ? pieceContainer
            : transform;

    public float CellSize
    {
        get
        {
            if (_cellSize < 0f)
            {
                if (cellPrefab == null)
                    return 1f;

                Cell cell =
                    cellPrefab.GetComponent<Cell>();

                _cellSize =
                    cell != null
                        ? cell.SIZE
                        : 1f;
            }

            return _cellSize;
        }
    }

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Awake()
    {
        if (Current != null &&
            Current != this)
        {
            Destroy(gameObject);
            return;
        }

        Current = this;
    }

    private void Start()
    {
        InitChessBoard();
        InitChessPieces();
    }

    private void Update()
    {
        if (BaseGameController.Current == null)
            return;

        if (BaseGameController.Current.GameState !=
            EGameState.PLAYING)
        {
            return;
        }

        CheckUserInput();
    }

    // =========================================================
    // GET CELL
    // =========================================================

    public Cell GetCell(int x, int y)
    {
        if (_cells == null)
            return null;

        if (!HexChessRules.IsValidCell(x, y))
            return null;

        return _cells[x][y];
    }

    public int GetCellIndex(int x, int y)
    {
        return HexChessRules.GetCellIndex(x, y);
    }

    // =========================================================
    // POSITION
    // =========================================================

    public Vector3 CalculatePosition(int x, int y)
    {
        float size = CellSize;

        float px =
            (x - 4) *
            size *
            0.8660254f;

        float pz =
            (y - 1 -
             (x - 4) * 0.5f) *
            size;

        return boardOrigin +
               new Vector3(
                   px,
                   0f,
                   pz
               );
    }

    [System.Obsolete("Use CalculatePosition instead.")]
    public Vector3 CanculatePosition(int x, int y)
    {
        return CalculatePosition(x, y);
    }

    // =========================================================
    // INIT BOARD
    // =========================================================

    public void InitChessBoard()
    {
        ClearContainer(cellContainer);

        _flatCellList.Clear();

        _cells = new Cell[HexChessRules.FILE_COUNT][];

        for (int x = 0;
             x < HexChessRules.FILE_COUNT;
             x++)
        {
            _cells[x] =
                new Cell[HexChessRules.MAX_RANK + 1];
        }

        Transform parent =
            cellContainer != null
                ? cellContainer
                : transform;

        for (int x = 0;
             x < HexChessRules.FILE_COUNT;
             x++)
        {
            int minRank =
                HexChessRules.GetMinRank(x);

            int maxRank =
                HexChessRules.GetMaxRank(x);

            for (int y = minRank;
                 y <= maxRank;
                 y++)
            {
                GameObject cellGO =
                    Instantiate(
                        cellPrefab,
                        CalculatePosition(x, y),
                        Quaternion.Euler(
                            0f,
                            30f,
                            0f
                        ),
                        parent
                    );

                cellGO.name =
                    $"HexCell_{(char)('a' + x)}{y}";

                Cell cell =
                    cellGO.GetComponent<Cell>();

                if (cell == null)
                {
                    Debug.LogError(
                        $"Prefab {cellPrefab.name} does not contain Cell."
                    );

                    Destroy(cellGO);
                    continue;
                }

                cell.SetLocation(
                    new CLocation(x, y)
                );

                int colorIndex =
                    HexChessRules.GetCellColorIndex(
                        x,
                        y
                    );

                cell.Color =
                    colorIndex switch
                    {
                        0 => ECellColor.BLACK,
                        1 => ECellColor.WHITE,
                        _ => ECellColor.GRAY
                    };

                _cells[x][y] = cell;
                _flatCellList.Add(cell);
            }
        }
    }

    // =========================================================
    // INIT PIECES
    // =========================================================

    [ContextMenu("InitChessPieces")]
    public void InitChessPieces()
    {
        ClearContainer(pieceContainer);

        _pieces.Clear();

        List<PieceInfo> listInfo =
            new List<PieceInfo>(36);

        // =====================================================
        // WHITE
        // =====================================================

        Add(
            listInfo,
            "W_ROOK_1",
            "Pieces/W_ROOK",
            0,
            1
        );

        Add(
            listInfo,
            "W_ROOK_2",
            "Pieces/W_ROOK",
            8,
            5
        );

        Add(
            listInfo,
            "W_KNIGHT_1",
            "Pieces/W_KNIGHT",
            1,
            1
        );

        Add(
            listInfo,
            "W_KNIGHT_2",
            "Pieces/W_KNIGHT",
            6,
            3
        );

        Add(
            listInfo,
            "W_BISHOP_1",
            "Pieces/W_BISHOP",
            2,
            1
        );

        Add(
            listInfo,
            "W_BISHOP_2",
            "Pieces/W_BISHOP",
            5,
            2
        );

        Add(
            listInfo,
            "W_BISHOP_3",
            "Pieces/W_BISHOP",
            7,
            4
        );

        Add(
            listInfo,
            "W_QUEEN_1",
            "Pieces/W_QUEEN",
            3,
            1
        );

        Add(
            listInfo,
            "W_KING_1",
            "Pieces/W_KING",
            4,
            1
        );

        for (int i = 0; i < 9; i++)
        {
            AddPawn(
                listInfo,
                EPlayer.WHITE,
                i
            );
        }

        // =====================================================
        // BLACK
        // =====================================================

        Add(
            listInfo,
            "B_ROOK_1",
            "Pieces/B_ROOK",
            0,
            6
        );

        Add(
            listInfo,
            "B_ROOK_2",
            "Pieces/B_ROOK",
            8,
            10
        );

        Add(
            listInfo,
            "B_KNIGHT_1",
            "Pieces/B_KNIGHT",
            2,
            8
        );

        Add(
            listInfo,
            "B_KNIGHT_2",
            "Pieces/B_KNIGHT",
            7,
            10
        );

        Add(
            listInfo,
            "B_BISHOP_1",
            "Pieces/B_BISHOP",
            1,
            7
        );

        Add(
            listInfo,
            "B_BISHOP_2",
            "Pieces/B_BISHOP",
            3,
            9
        );

        Add(
            listInfo,
            "B_BISHOP_3",
            "Pieces/B_BISHOP",
            6,
            10
        );

        Add(
            listInfo,
            "B_QUEEN_1",
            "Pieces/B_QUEEN",
            5,
            10
        );

        Add(
            listInfo,
            "B_KING_1",
            "Pieces/B_KING",
            4,
            10
        );

        for (int i = 0; i < 9; i++)
        {
            AddPawn(
                listInfo,
                EPlayer.BLACK,
                i
            );
        }

        Transform parent =
            pieceContainer != null
                ? pieceContainer
                : transform;

        foreach (PieceInfo info in listInfo)
        {
            GameObject prefab =
                ResourcesControllers.Instance
                    .GetGameObject(info.Path);

            if (prefab == null)
                continue;

            GameObject pieceGO =
                Instantiate(
                    prefab,
                    parent
                );

            pieceGO.name = info.Name;

            BasePiece piece =
                pieceGO.GetComponent<BasePiece>();

            if (piece == null)
            {
                Debug.LogError(
                    $"Prefab {info.Path} does not contain BasePiece."
                );

                Destroy(pieceGO);
                continue;
            }

            Cell cell =
                GetCell(
                    info.X,
                    info.Y
                );

            if (cell == null)
            {
                Debug.LogError(
                    $"Cell target not found: {info.X},{info.Y}"
                );

                Destroy(pieceGO);
                continue;
            }

            piece.SetInfo(
                info,
                cell
            );

            _pieces.Add(piece);
        }
    }

    private void Add(
        List<PieceInfo> list,
        string name,
        string path,
        int x,
        int y)
    {
        list.Add(
            new PieceInfo
            {
                Name = name,
                Path = path,
                X = x,
                Y = y
            }
        );
    }

    private void AddPawn(
        List<PieceInfo> list,
        EPlayer player,
        int file)
    {
        bool isWhite =
            player == EPlayer.WHITE;

        int rank =
            isWhite
                ? HexChessRules.GetWhitePawnStartRank(file)
                : HexChessRules.GetBlackPawnStartRank(file);

        string prefix =
            isWhite ? "W" : "B";

        Add(
            list,
            $"{prefix}_PAWN_{file + 1}",
            $"Pieces/{prefix}_PAWN",
            file,
            rank
        );
    }

    // =========================================================
    // INPUT
    // =========================================================

    private void CheckUserInput()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        Camera mainCamera =
            Camera.main;

        if (mainCamera == null)
            return;

        Ray ray =
            mainCamera.ScreenPointToRay(
                Input.mousePosition
            );

        if (!Physics.Raycast(
            ray,
            out RaycastHit hit,
            1000f,
            cellLayerMask.value))
        {
            return;
        }

        Cell cell =
            hit.collider.GetComponent<Cell>();

        if (cell == null)
            return;

        switch (cell.State)
        {
            case ECellState.NORMAL:
                HandleNormalCellClick(cell);
                break;

            case ECellState.TARGETED:
                HandleTargetedCellClick(cell);
                break;
        }
    }

    private void HandleNormalCellClick(Cell cell)
    {
        EPlayer currentPlayer =
            BaseGameController.Current.CurrentPlayer;

        if (cell.CurrentPiece == null)
            return;

        if (cell.CurrentPiece.Player != currentPlayer)
            return;

        DeselectCurrentCell();

        _currentSelectedCell = cell;

        _currentSelectedCell.SetCellState(
            ECellState.SELECTED
        );
    }

    private void HandleTargetedCellClick(Cell cell)
    {
        if (_currentSelectedCell == null)
            return;

        EPlayer currentPlayer =
            BaseGameController.Current.CurrentPlayer;

        if (cell.CurrentPiece == null)
        {
            _currentSelectedCell.MakeAMove(cell);
        }
        else if (cell.CurrentPiece.Player != currentPlayer)
        {
            _currentSelectedCell.CurrentPiece
                .Attack(cell);
        }

        DeselectCurrentCell();
    }

    private void DeselectCurrentCell()
    {
        if (_currentSelectedCell == null)
            return;

        _currentSelectedCell.SetCellState(
            ECellState.NORMAL
        );

        if (_currentSelectedCell.CurrentPiece != null)
        {
            _currentSelectedCell.CurrentPiece
                .BeUnselected();
        }

        _currentSelectedCell = null;
    }

    // =========================================================
    // ATTACK CHECK
    // =========================================================

    public bool IsSquareAttacked(
        int targetX,
        int targetY,
        EPlayer byPlayer)
    {
        if (GetCell(targetX, targetY) == null)
            return false;

        foreach (Cell cell in _flatCellList)
        {
            BasePiece piece =
                cell.CurrentPiece;

            if (piece == null)
                continue;

            if (piece.Player != byPlayer)
                continue;

            int px = piece.Location.X;
            int py = piece.Location.Y;

            if (piece is P_Pawn &&
                IsPawnAttack(
                    px,
                    py,
                    targetX,
                    targetY,
                    byPlayer))
            {
                return true;
            }

            if (piece is P_King &&
                IsKingAttack(
                    px,
                    py,
                    targetX,
                    targetY))
            {
                return true;
            }

            if (piece is P_Knight &&
                IsKnightAttack(
                    px,
                    py,
                    targetX,
                    targetY))
            {
                return true;
            }

            if (piece is P_Rook &&
                IsSlidingAttack(
                    px,
                    py,
                    targetX,
                    targetY,
                    HexChessRules.RookDirections))
            {
                return true;
            }

            if (piece is P_Bishop &&
                IsSlidingAttack(
                    px,
                    py,
                    targetX,
                    targetY,
                    HexChessRules.BishopDirections))
            {
                return true;
            }

            if (piece is P_Queen &&
                IsSlidingAttack(
                    px,
                    py,
                    targetX,
                    targetY,
                    HexChessRules.QueenDirections))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsSquareAttacked(
        int targetX,
        int targetY)
    {
        EPlayer current =
            BaseGameController.Current.CurrentPlayer;

        EPlayer enemy =
            HexChessRules.GetOpponent(current);

        return IsSquareAttacked(
            targetX,
            targetY,
            enemy
        );
    }

    private bool IsPawnAttack(
        int px,
        int py,
        int tx,
        int ty,
        EPlayer player)
    {
        int dir =
            player == EPlayer.WHITE
                ? 1
                : -1;

        return
            (tx == px - 1 &&
             ty == py + dir) ||

            (tx == px + 1 &&
             ty == py + dir);
    }

    private bool IsKingAttack(
        int px,
        int py,
        int tx,
        int ty)
    {
        int dx = tx - px;
        int dy = ty - py;

        foreach (Vector2Int dir in
                 HexChessRules.QueenDirections)
        {
            if (dx == dir.x &&
                dy == dir.y)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsKnightAttack(
        int px,
        int py,
        int tx,
        int ty)
    {
        int dx = tx - px;
        int dy = ty - py;

        foreach (Vector2Int move in
                 HexChessRules.KnightMoves)
        {
            if (dx == move.x &&
                dy == move.y)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsSlidingAttack(
        int px,
        int py,
        int tx,
        int ty,
        Vector2Int[] directions)
    {
        foreach (Vector2Int dir in directions)
        {
            int x = px + dir.x;
            int y = py + dir.y;

            while (HexChessRules.IsValidCell(x, y))
            {
                if (x == tx && y == ty)
                    return true;

                Cell cell = GetCell(x, y);

                if (cell != null &&
                    cell.CurrentPiece != null)
                {
                    break;
                }

                x += dir.x;
                y += dir.y;
            }
        }

        return false;
    }

    // =========================================================
    // LEGAL MOVE
    // =========================================================

    public bool IsLegalMove(
        Cell fromCell,
        Cell targetCell)
    {
        if (fromCell == null ||
            targetCell == null)
        {
            return false;
        }

        if (fromCell.CurrentPiece == null)
            return false;

        BasePiece piece =
            fromCell.CurrentPiece;

        if (targetCell.CurrentPiece != null &&
            targetCell.CurrentPiece.Player ==
            piece.Player)
        {
            return false;
        }

        piece.BeSelected();

        bool targeted =
            targetCell.State ==
            ECellState.TARGETED;

        piece.BeUnselected();

        if (!targeted)
            return false;

        return !WouldLeaveKingInCheck(
            fromCell,
            targetCell
        );
    }

    // =========================================================
    // KING SAFETY
    // =========================================================

    public bool WouldLeaveKingInCheck(
        Cell fromCell,
        Cell targetCell)
    {
        if (fromCell == null ||
            targetCell == null ||
            fromCell.CurrentPiece == null)
        {
            return true;
        }

        BasePiece[,] board =
            CreateVirtualBoard();

        int fromX =
            fromCell.Location.X;

        int fromY =
            fromCell.Location.Y;

        int toX =
            targetCell.Location.X;

        int toY =
            targetCell.Location.Y;

        BasePiece movingPiece =
            board[fromX, fromY];

        if (movingPiece == null)
            return true;

        BasePiece capturedPiece =
            board[toX, toY];

        board[fromX, fromY] = null;
        board[toX, toY] = movingPiece;

        // -----------------------------------------------------
        // EN PASSANT
        // -----------------------------------------------------

        if (movingPiece is P_Pawn &&
            capturedPiece == null &&
            IsCurrentEnPassantMove(
                fromCell,
                targetCell))
        {
            int capturedX =
                targetCell.Location.X;

            int capturedY =
                fromCell.Location.Y;

            board[capturedX, capturedY] =
                null;
        }

        // -----------------------------------------------------
        // CASTLING
        // -----------------------------------------------------

        if (movingPiece is P_King)
        {
            Vector2Int delta =
                new Vector2Int(
                    toX - fromX,
                    toY - fromY
                );

            if (HexChessRules.TryGetDirection(
                delta,
                HexChessRules.RookDirections,
                out Vector2Int dir,
                out int kingDistance))
            {
                if (kingDistance == 2 ||
                    kingDistance == 3)
                {
                    int rookFromX =
                        fromX + dir.x * 4;

                    int rookFromY =
                        fromY + dir.y * 4;

                    int rookToDistance =
                        5 - kingDistance;

                    int rookToX =
                        fromX +
                        dir.x *
                        rookToDistance;

                    int rookToY =
                        fromY +
                        dir.y *
                        rookToDistance;

                    BasePiece rook =
                        board[
                            rookFromX,
                            rookFromY
                        ];

                    if (rook is P_Rook)
                    {
                        board[
                            rookFromX,
                            rookFromY
                        ] = null;

                        board[
                            rookToX,
                            rookToY
                        ] = rook;
                    }
                }
            }
        }

        Vector2Int kingPosition =
            FindKingPosition(
                board,
                movingPiece.Player
            );

        if (kingPosition.x < 0)
            return true;

        return IsSquareAttackedOnBoard(
            board,
            kingPosition.x,
            kingPosition.y,
            HexChessRules.GetOpponent(
                movingPiece.Player
            )
        );
    }

    private bool IsCurrentEnPassantMove(
        Cell fromCell,
        Cell targetCell)
    {
        if (!(fromCell.CurrentPiece is P_Pawn pawn))
            return false;

        return pawn.IsEnPassantTarget(
            targetCell
        );
    }

    // =========================================================
    // VIRTUAL BOARD
    // =========================================================

    private BasePiece[,] CreateVirtualBoard()
    {
        BasePiece[,] board =
            new BasePiece[
                HexChessRules.FILE_COUNT,
                HexChessRules.MAX_RANK + 1
            ];

        foreach (Cell cell in _flatCellList)
        {
            if (cell == null)
                continue;

            if (cell.CurrentPiece == null)
                continue;

            int x = cell.Location.X;
            int y = cell.Location.Y;

            board[x, y] =
                cell.CurrentPiece;
        }

        return board;
    }

    // =========================================================
    // FIND KING
    // =========================================================

    private Vector2Int FindKingPosition(
        BasePiece[,] board,
        EPlayer player)
    {
        foreach (Vector2Int position in
                 HexChessRules.CellCoordinates)
        {
            BasePiece piece =
                board[
                    position.x,
                    position.y
                ];

            if (piece != null &&
                piece.Player == player &&
                piece is P_King)
            {
                return position;
            }
        }

        return new Vector2Int(-1, -1);
    }

    // =========================================================
    // ATTACK ON VIRTUAL BOARD
    // =========================================================

    private bool IsSquareAttackedOnBoard(
        BasePiece[,] board,
        int targetX,
        int targetY,
        EPlayer attackingPlayer)
    {
        foreach (Vector2Int position in
                 HexChessRules.CellCoordinates)
        {
            BasePiece piece =
                board[
                    position.x,
                    position.y
                ];

            if (piece == null)
                continue;

            if (piece.Player != attackingPlayer)
                continue;

            if (PieceCanAttackSquare(
                board,
                piece,
                position.x,
                position.y,
                targetX,
                targetY))
            {
                return true;
            }
        }

        return false;
    }

    private bool PieceCanAttackSquare(
        BasePiece[,] board,
        BasePiece piece,
        int px,
        int py,
        int targetX,
        int targetY)
    {
        int dx = targetX - px;
        int dy = targetY - py;

        // Pawn
        if (piece is P_Pawn)
        {
            int direction =
                piece.Player == EPlayer.WHITE
                    ? 1
                    : -1;

            return
                dy == direction &&
                Mathf.Abs(dx) == 1;
        }

        // Knight
        if (piece is P_Knight)
        {
            foreach (Vector2Int move in
                     HexChessRules.KnightMoves)
            {
                if (dx == move.x &&
                    dy == move.y)
                {
                    return true;
                }
            }

            return false;
        }

        // King
        if (piece is P_King)
        {
            foreach (Vector2Int dir in
                     HexChessRules.QueenDirections)
            {
                if (dx == dir.x &&
                    dy == dir.y)
                {
                    return true;
                }
            }

            return false;
        }

        // Rook
        if (piece is P_Rook)
        {
            return IsSlidingAttackOnBoard(
                board,
                px,
                py,
                targetX,
                targetY,
                HexChessRules.RookDirections
            );
        }

        // Bishop
        if (piece is P_Bishop)
        {
            return IsSlidingAttackOnBoard(
                board,
                px,
                py,
                targetX,
                targetY,
                HexChessRules.BishopDirections
            );
        }

        // Queen
        if (piece is P_Queen)
        {
            return IsSlidingAttackOnBoard(
                board,
                px,
                py,
                targetX,
                targetY,
                HexChessRules.QueenDirections
            );
        }

        return false;
    }

    private bool IsSlidingAttackOnBoard(
        BasePiece[,] board,
        int px,
        int py,
        int tx,
        int ty,
        Vector2Int[] directions)
    {
        foreach (Vector2Int dir in directions)
        {
            int x = px + dir.x;
            int y = py + dir.y;

            while (HexChessRules.IsValidCell(x, y))
            {
                if (x == tx && y == ty)
                    return true;

                if (board[x, y] != null)
                    break;

                x += dir.x;
                y += dir.y;
            }
        }

        return false;
    }

    // =========================================================
    // RESET
    // =========================================================

    public void ResetBoard()
    {
        ClearContainer(pieceContainer);
        ClearContainer(cellContainer);

        _pieces.Clear();
        _flatCellList.Clear();

        _currentSelectedCell = null;

        InitChessBoard();
        InitChessPieces();
    }

    private void ClearContainer(
        Transform container)
    {
        if (container == null)
            return;

        for (int i = container.childCount - 1;
             i >= 0;
             i--)
        {
            Transform child =
                container.GetChild(i);

            if (child != null)
            {
                child.DOKill();
                Destroy(child.gameObject);
            }
        }
    }
}