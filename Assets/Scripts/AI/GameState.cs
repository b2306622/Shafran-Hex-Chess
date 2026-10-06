using System; 
using System.Collections.Generic; 
using UnityEngine; 
 
public class GameState 
{ 
    public const int FILE_COUNT = 
        HexChessRules.FILE_COUNT; 
 
    public const int MAX_RANK = 
        HexChessRules.MAX_RANK; 
 
    private readonly StatePiece?[,] _board; 
 
    // ========================================================= 
    // HASH 
    // ========================================================= 
 
    private ulong _hash; 
 
    public ulong Hash 
    { 
        get 
        { 
            return _hash; 
        } 
    } 
 
    // ========================================================= 
    // SIDE TO MOVE 
    // ========================================================= 
 
    private EPlayer _sideToMove; 
 
    public EPlayer SideToMove 
    { 
        get 
        { 
            return _sideToMove; 
        } 
        set 
        { 
            if (_sideToMove == value) 
            { 
                return; 
            } 
 
            // Remove old side key. 
            _hash ^= ZobristHash.GetSideKey( 
                _sideToMove 
            ); 
 
            _sideToMove = value; 
 
            // Add new side key. 
            _hash ^= ZobristHash.GetSideKey( 
                _sideToMove 
            ); 
        } 
    } 
 
    // ========================================================= 
    // LAST MOVE 
    // ========================================================= 
 
    public StateMove? LastMove 
    { 
        get; 
        set; 
    } 
 
    // ========================================================= 
    // EN PASSANT 
    // ========================================================= 
 
    private CLocation _enPassantTarget; 
 
    private bool _hasEnPassantTarget; 
 
    public CLocation EnPassantTarget 
    { 
        get 
        { 
            return _enPassantTarget; 
        } 
        set 
        { 
            // Nếu EP đang active, 
            // bỏ hash của target cũ trước. 
            if (_hasEnPassantTarget) 
            { 
                _hash ^= ZobristHash.GetEnPassantKey( 
                    _enPassantTarget 
                ); 
            } 
 
            _enPassantTarget = value; 
 
            // Nếu EP vẫn active, 
            // thêm hash target mới. 
            if (_hasEnPassantTarget) 
            { 
                _hash ^= ZobristHash.GetEnPassantKey( 
                    _enPassantTarget 
                ); 
            } 
        } 
    } 
 
    public bool HasEnPassantTarget 
    { 
        get 
        { 
            return _hasEnPassantTarget; 
        } 
        set 
        { 
            if (_hasEnPassantTarget == value) 
            { 
                return; 
            } 
 
            if (_hasEnPassantTarget) 
            { 
                // Tắt EP -> remove hash. 
                _hash ^= ZobristHash.GetEnPassantKey( 
                    _enPassantTarget 
                ); 
            } 
 
            _hasEnPassantTarget = value; 
 
            if (_hasEnPassantTarget) 
            { 
                // Bật EP -> add hash. 
                _hash ^= ZobristHash.GetEnPassantKey( 
                    _enPassantTarget 
                ); 
            } 
        } 
    } 
 
    // ========================================================= 
    // CONSTRUCTOR 
    // ========================================================= 
 
    public GameState() 
    { 
        _board = 
            new StatePiece?[ 
                FILE_COUNT, 
                MAX_RANK + 1 
            ]; 
 
        _sideToMove = 
            EPlayer.WHITE; 
 
        LastMove = null; 
 
        _enPassantTarget = 
            default; 
 
        _hasEnPassantTarget = 
            false; 
 
        _hash = 
            ZobristHash.GetSideKey( 
                EPlayer.WHITE 
            ); 
    } 
 
    // ========================================================= 
    // BOARD 
    // ========================================================= 
 
    public StatePiece?[,] Board 
    { 
        get 
        { 
            return _board; 
        } 
    } 
 
    // ========================================================= 
    // GET PIECE 
    // ========================================================= 
 
    public StatePiece? GetPiece( 
        int x, 
        int y) 
    { 
        if (!HexChessRules.IsValidCell( 
                x, 
                y)) 
        { 
            return null; 
        } 
 
        return _board[ 
            x, 
            y 
        ]; 
    } 
 
    // ========================================================= 
    // SET PIECE 
    // ========================================================= 
    // 
    // 7.5.8: 
    // cập nhật Zobrist hash incremental. 
    // 
    // Không cần Compute() lại toàn bộ board. 
    // 
    // ========================================================= 
 
    public void SetPiece( 
        int x, 
        int y, 
        StatePiece? piece) 
    { 
        if (!HexChessRules.IsValidCell( 
                x, 
                y)) 
        { 
            return; 
        } 
 
        StatePiece? oldPiece = 
            _board[ 
                x, 
                y 
            ]; 
 
        // Remove old piece from hash. 
        if (oldPiece.HasValue) 
        { 
            _hash ^= 
                ZobristHash.GetPieceKey( 
                    x, 
                    y, 
                    oldPiece.Value 
                ); 
        } 
 
        // Set board. 
        _board[ 
            x, 
            y 
        ] = piece; 
 
        // Add new piece to hash. 
        if (piece.HasValue) 
        { 
            _hash ^= 
                ZobristHash.GetPieceKey( 
                    x, 
                    y, 
                    piece.Value 
                ); 
        } 
    } 
 
    // ========================================================= 
    // CLONE 
    // ========================================================= 
    // 
    // 7.5.8: 
    // dùng Array.Copy thay vì foreach 70 cells. 
    // 
    // Đồng thời copy hash trực tiếp. 
    // 
    // ========================================================= 
 
    public GameState Clone() 
    { 
        GameState clone = 
            new GameState(); 
 
        Array.Copy( 
            _board, 
            clone._board, 
            _board.Length 
        ); 
 
        clone._sideToMove = 
            _sideToMove; 
 
        clone.LastMove = 
            LastMove; 
 
        clone._enPassantTarget = 
            _enPassantTarget; 
 
        clone._hasEnPassantTarget = 
            _hasEnPassantTarget; 
 
        clone._hash = 
            _hash; 
 
        return clone; 
    } 
 
    // ========================================================= 
    // TẠO GAME STATE TỪ UNITY 
    // ========================================================= 
 
    public static GameState FromUnityBoard() 
    { 
        GameState state = 
            new GameState(); 
 
        if (ChessBoard.Current == null) 
        { 
            return state; 
        } 
 
        // ===================================================== 
        // SIDE TO MOVE 
        // ===================================================== 
 
        if (BaseGameController.Current != null) 
        { 
            state.SideToMove = 
                BaseGameController.Current.CurrentPlayer; 
 
            Move lastMove = 
                BaseGameController.Current.LastMove; 
 
            if (lastMove != null) 
            { 
                state.LastMove = 
                    ConvertLastMove( 
                        lastMove 
                    ); 
 
                // ================================================= 
                // EN PASSANT 
                // ================================================= 
 
                if (lastMove.MovedPiece is P_Pawn) 
                { 
                    int distance = 
                        Mathf.Abs( 
                            lastMove.From.Y - 
                            lastMove.To.Y 
                        ); 
 
                    if (distance > 1) 
                    { 
                        int direction = 
                            lastMove.MovedPiece.Player == 
                            EPlayer.WHITE 
                                ? 1 
                                : -1; 
 
                        state.EnPassantTarget = 
                            new CLocation( 
                                lastMove.To.X, 
                                lastMove.To.Y + 
                                direction 
                            ); 
 
                        state.HasEnPassantTarget = 
                            true; 
                    } 
                } 
            } 
        } 
 
        // ===================================================== 
        // BOARD 
        // ===================================================== 
 
        foreach ( 
            Vector2Int position in 
            HexChessRules.CellCoordinates) 
        { 
            Cell cell = 
                ChessBoard.Current.GetCell( 
                    position.x, 
                    position.y 
                ); 
 
            if (cell == null || 
                cell.CurrentPiece == null) 
            { 
                continue; 
            } 
 
            BasePiece piece = 
                cell.CurrentPiece; 
 
            StatePiece statePiece = 
                new StatePiece( 
                    ConvertType(piece), 
                    piece.Player, 
                    piece.HasMoved 
                ); 
 
            state.SetPiece( 
                position.x, 
                position.y, 
                statePiece 
            ); 
        } 
 
        return state; 
    } 
 
    // ========================================================= 
    // CONVERT LAST MOVE 
    // ========================================================= 
 
    private static StateMove ConvertLastMove( 
        Move move) 
    { 
        StateMove stateMove = 
            new StateMove( 
                move.From, 
                move.To, 
                ConvertPiece( 
                    move.MovedPiece 
                ) 
            ); 
 
        if (move.CapturedPiece != null) 
        { 
            stateMove.IsCapture = 
                true; 
 
            stateMove.CapturedPiece = 
                ConvertPiece( 
                    move.CapturedPiece 
                ); 
 
            stateMove.CapturedLocation = 
                move.IsEnPassant 
                    ? move.CapturedLocation 
                    : move.To; 
        } 
 
        stateMove.IsCastling = 
            move.IsCastling; 
 
        stateMove.IsEnPassant = 
            move.IsEnPassant; 
 
        stateMove.IsPromotion = 
            move.IsPromotion; 
 
        return stateMove; 
    } 
 
    // ========================================================= 
    // CONVERT PIECE 
    // ========================================================= 
 
    private static StatePiece ConvertPiece( 
        BasePiece piece) 
    { 
        return new StatePiece( 
            ConvertType(piece), 
            piece.Player, 
            piece.HasMoved 
        ); 
    } 
 
    // ========================================================= 
    // CONVERT TYPE 
    // ========================================================= 
 
    private static StatePieceType ConvertType( 
        BasePiece piece) 
    { 
        if (piece is P_Pawn) 
        { 
            return StatePieceType.PAWN; 
        } 
 
        if (piece is P_Knight) 
        { 
            return StatePieceType.KNIGHT; 
        } 
 
        if (piece is P_Bishop) 
        { 
            return StatePieceType.BISHOP; 
        } 
 
        if (piece is P_Rook) 
        { 
            return StatePieceType.ROOK; 
        } 
 
        if (piece is P_Queen) 
        { 
            return StatePieceType.QUEEN; 
        } 
 
        if (piece is P_King) 
        { 
            return StatePieceType.KING; 
        } 
 
        return StatePieceType.NONE; 
    } 
}
