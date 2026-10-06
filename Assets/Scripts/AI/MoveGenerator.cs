using System.Collections.Generic; 
using UnityEngine; 
 
public class MoveGenerator 
{ 
    // ========================================================= 
    // LEGAL MOVES 
    // ========================================================= 
 
    public List<StateMove> GenerateLegalMoves( 
        GameState state, 
        EPlayer player) 
    { 
        List<StateMove> pseudoMoves = 
            GeneratePseudoLegalMoves( 
                state, 
                player 
            ); 
 
        List<StateMove> legalMoves = 
            new List<StateMove>( 
                pseudoMoves.Count 
            ); 
 
        foreach ( 
            StateMove move in pseudoMoves) 
        { 
            if (IsLegalMove( 
                    state, 
                    player, 
                    move 
                )) 
            { 
                legalMoves.Add( 
                    move 
                ); 
            } 
        } 
 
        return legalMoves; 
    } 
 
    // ========================================================= 
    // LEGAL CAPTURE MOVES 
    // 7.5.3 OPTIMIZATION 
    // ========================================================= 
 
    public List<StateMove> GenerateLegalCaptureMoves( 
        GameState state, 
        EPlayer player) 
    { 
        List<StateMove> pseudoMoves = 
            GeneratePseudoLegalMoves( 
                state, 
                player 
            ); 
 
        List<StateMove> legalCaptures = 
            new List<StateMove>(); 
 
        foreach ( 
            StateMove move in pseudoMoves) 
        { 
            if (!move.IsCapture) 
            { 
                continue; 
            } 
 
            if (IsLegalMove( 
                    state, 
                    player, 
                    move 
                )) 
            { 
                legalCaptures.Add( 
                    move 
                ); 
            } 
        } 
 
        return legalCaptures; 
    } 
 
    // ========================================================= 
    // OLD API 
    // Giữ lại để không phá code cũ. 
    // ========================================================= 
 
    public List<StateMove> GenerateCaptureMoves( 
        GameState state, 
        EPlayer player) 
    { 
        return GenerateLegalCaptureMoves( 
            state, 
            player 
        ); 
    } 
 
    // ========================================================= 
    // LEGAL MOVE CHECK 
    // ========================================================= 
 
    private bool IsLegalMove( 
        GameState state, 
        EPlayer player, 
        StateMove move) 
    { 
        GameState copy = 
            state.Clone(); 
 
        MakeMove( 
            copy, 
            move 
        ); 
 
        return !IsInCheck( 
            copy, 
            player 
        ); 
    } 
 
    // ========================================================= 
    // PSEUDO LEGAL MOVES 
    // ========================================================= 
 
    public List<StateMove> GeneratePseudoLegalMoves( 
        GameState state, 
        EPlayer player) 
    { 
        List<StateMove> result = 
            new List<StateMove>(64); 
 
        foreach ( 
            Vector2Int position in 
            HexChessRules.CellCoordinates) 
        { 
            StatePiece? piece = 
                state.GetPiece( 
                    position.x, 
                    position.y 
                ); 
 
            if (!piece.HasValue) 
            { 
                continue; 
            } 
 
            if (piece.Value.Player != 
                player) 
            { 
                continue; 
            } 
 
            switch (piece.Value.Type) 
            { 
                case StatePieceType.PAWN: 
 
                    GeneratePawnMoves( 
                        state, 
                        position, 
                        piece.Value, 
                        result 
                    ); 
 
                    break; 
 
                case StatePieceType.KNIGHT: 
 
                    GenerateKnightMoves( 
                        state, 
                        position, 
                        piece.Value, 
                        result 
                    ); 
 
                    break; 
 
                case StatePieceType.BISHOP: 
 
                    GenerateSlidingMoves( 
                        state, 
                        position, 
                        piece.Value, 
                        HexChessRules.BishopDirections, 
                        result 
                    ); 
 
                    break; 
 
                case StatePieceType.ROOK: 
 
                    GenerateSlidingMoves( 
                        state, 
                        position, 
                        piece.Value, 
                        HexChessRules.RookDirections, 
                        result 
                    ); 
 
                    break; 
 
                case StatePieceType.QUEEN: 
 
                    GenerateSlidingMoves( 
                        state, 
                        position, 
                        piece.Value, 
                        HexChessRules.QueenDirections, 
                        result 
                    ); 
 
                    break; 
 
                case StatePieceType.KING: 
 
                    GenerateKingMoves( 
                        state, 
                        position, 
                        piece.Value, 
                        result 
                    ); 
 
                    break; 
            } 
        } 
 
        return result; 
    } 
 
    // ========================================================= 
    // PAWN 
    // ========================================================= 
 
    private void GeneratePawnMoves( 
        GameState state, 
        Vector2Int from, 
        StatePiece pawn, 
        List<StateMove> result) 
    { 
        int direction = 
            pawn.Player == EPlayer.WHITE 
                ? 1 
                : -1; 
 
        int maxDistance = 1; 
 
        if (!pawn.HasMoved) 
        { 
            int startRank = 
                pawn.Player == EPlayer.WHITE 
                    ? HexChessRules.GetWhitePawnStartRank( 
                        from.x) 
                    : HexChessRules.GetBlackPawnStartRank( 
                        from.x); 
 
            if (from.y == startRank) 
            { 
                maxDistance = 
                    HexChessRules.GetInitialPawnMoveDistance( 
                        from.x); 
            } 
        } 
 
        // ----------------------------------------------------- 
        // FORWARD 
        // ----------------------------------------------------- 
 
        for (int distance = 1; 
             distance <= maxDistance; 
             distance++) 
        { 
            int x = 
                from.x; 
 
            int y = 
                from.y + 
                direction * 
                distance; 
 
            if (!HexChessRules.IsValidCell( 
                    x, 
                    y)) 
            { 
                break; 
            } 
 
            if (state.GetPiece( 
                    x, 
                    y 
                ).HasValue) 
            { 
                break; 
            } 
 
            AddMove( 
                state, 
                from, 
                new Vector2Int(x, y), 
                pawn, 
                result 
            ); 
        } 
 
        // ----------------------------------------------------- 
        // CAPTURE 
        // ----------------------------------------------------- 
 
        AddPawnCapture( 
            state, 
            from, 
            pawn, 
            from.x - 1, 
            from.y + direction, 
            result 
        ); 
 
        AddPawnCapture( 
            state, 
            from, 
            pawn, 
            from.x + 1, 
            from.y + direction, 
            result 
        ); 
 
        // ----------------------------------------------------- 
        // EN PASSANT 
        // ----------------------------------------------------- 
 
        AddPawnEnPassant( 
            state, 
            from, 
            pawn, 
            from.x - 1, 
            from.y + direction, 
            result 
        ); 
 
        AddPawnEnPassant( 
            state, 
            from, 
            pawn, 
            from.x + 1, 
            from.y + direction, 
            result 
        ); 
    } 
 
    private void AddPawnCapture( 
        GameState state, 
        Vector2Int from, 
        StatePiece pawn, 
        int x, 
        int y, 
        List<StateMove> result) 
    { 
        if (!HexChessRules.IsValidCell( 
                x, 
                y)) 
        { 
            return; 
        } 
 
        StatePiece? target = 
            state.GetPiece( 
                x, 
                y 
            ); 
 
        if (!target.HasValue) 
        { 
            return; 
        } 
 
        if (target.Value.Player == 
            pawn.Player) 
        { 
            return; 
        } 
 
        AddMove( 
            state, 
            from, 
            new Vector2Int( 
                x, 
                y 
            ), 
            pawn, 
            result 
        ); 
    } 
 
    private void AddPawnEnPassant( 
        GameState state, 
        Vector2Int from, 
        StatePiece pawn, 
        int x, 
        int y, 
        List<StateMove> result) 
    { 
        if (!state.HasEnPassantTarget) 
        { 
            return; 
        } 
 
        if (!HexChessRules.IsValidCell( 
                x, 
                y)) 
        { 
            return; 
        } 
 
        if (state.EnPassantTarget.X != x || 
            state.EnPassantTarget.Y != y) 
        { 
            return; 
        } 
 
        // EP target phải trống. 
        if (state.GetPiece( 
                x, 
                y 
            ).HasValue) 
        { 
            return; 
        } 
 
        int capturedX = 
            x; 
 
        int capturedY = 
            from.y; 
 
        StatePiece? captured = 
            state.GetPiece( 
                capturedX, 
                capturedY 
            ); 
 
        if (!captured.HasValue) 
        { 
            return; 
        } 
 
        if (captured.Value.Type != 
            StatePieceType.PAWN) 
        { 
            return; 
        } 
 
        if (captured.Value.Player == 
            pawn.Player) 
        { 
            return; 
        } 
 
        StateMove move = 
            new StateMove( 
                new CLocation( 
                    from.x, 
                    from.y 
                ), 
                new CLocation( 
                    x, 
                    y 
                ), 
                pawn 
            ); 
 
        move.IsCapture = 
            true; 
 
        move.IsEnPassant = 
            true; 
 
        move.CapturedPiece = 
            captured.Value; 
 
        move.CapturedLocation = 
            new CLocation( 
                capturedX, 
                capturedY 
            ); 
 
        result.Add( 
            move 
        ); 
    } 
 
    // ========================================================= 
    // KNIGHT 
    // ========================================================= 
 
    private void GenerateKnightMoves( 
        GameState state, 
        Vector2Int from, 
        StatePiece piece, 
        List<StateMove> result) 
    { 
        foreach ( 
            Vector2Int offset in 
            HexChessRules.KnightMoves) 
        { 
            int x = 
                from.x + 
                offset.x; 
 
            int y = 
                from.y + 
                offset.y; 
 
            if (!HexChessRules.IsValidCell( 
                    x, 
                    y)) 
            { 
                continue; 
            } 
 
            StatePiece? target = 
                state.GetPiece( 
                    x, 
                    y 
                ); 
 
            if (target.HasValue && 
                target.Value.Player == 
                piece.Player) 
            { 
                continue; 
            } 
 
            AddMove( 
                state, 
                from, 
                new Vector2Int( 
                    x, 
                    y 
                ), 
                piece, 
                result 
            ); 
        } 
    } 
 
    // ========================================================= 
    // SLIDING 
    // ========================================================= 
 
    private void GenerateSlidingMoves( 
        GameState state, 
        Vector2Int from, 
        StatePiece piece, 
        Vector2Int[] directions, 
        List<StateMove> result) 
    { 
        foreach ( 
            Vector2Int dir in directions) 
        { 
            int x = 
                from.x + 
                dir.x; 
 
            int y = 
                from.y + 
                dir.y; 
 
            while ( 
                HexChessRules.IsValidCell( 
                    x, 
                    y)) 
            { 
                StatePiece? target = 
                    state.GetPiece( 
                        x, 
                        y 
                    ); 
 
                if (!target.HasValue) 
                { 
                    AddMove( 
                        state, 
                        from, 
                        new Vector2Int( 
                            x, 
                            y 
                        ), 
                        piece, 
                        result 
                    ); 
                } 
                else 
                { 
                    if (target.Value.Player != 
                        piece.Player) 
                    { 
                        AddMove( 
                            state, 
                            from, 
                            new Vector2Int( 
                                x, 
                                y 
                            ), 
                            piece, 
                            result 
                        ); 
                    } 
 
                    break; 
                } 
 
                x += 
                    dir.x; 
 
                y += 
                    dir.y; 
            } 
        } 
    } 
 
    // ========================================================= 
    // KING 
    // ========================================================= 
 
    private void GenerateKingMoves( 
        GameState state, 
        Vector2Int from, 
        StatePiece king, 
        List<StateMove> result) 
    { 
        foreach ( 
            Vector2Int dir in 
            HexChessRules.QueenDirections) 
        { 
            int x = 
                from.x + 
                dir.x; 
 
            int y = 
                from.y + 
                dir.y; 
 
            if (!HexChessRules.IsValidCell( 
                    x, 
                    y)) 
            { 
                continue; 
            } 
 
            StatePiece? target = 
                state.GetPiece( 
                    x, 
                    y 
                ); 
 
            if (target.HasValue && 
                target.Value.Player == 
                king.Player) 
            { 
                continue; 
            } 
 
            AddMove( 
                state, 
                from, 
                new Vector2Int( 
                    x, 
                    y 
                ), 
                king, 
                result 
            ); 
        } 
 
        if (king.HasMoved) 
        { 
            return; 
        } 
 
        if (IsInCheck( 
                state, 
                king.Player)) 
        { 
            return; 
        } 
 
        foreach ( 
            Vector2Int dir in 
            HexChessRules.RookDirections) 
        { 
            TryAddCastling( 
                state, 
                from, 
                king, 
                dir, 
                2, 
                3, 
                result 
            ); 
 
            TryAddCastling( 
                state, 
                from, 
                king, 
                dir, 
                3, 
                2, 
                result 
            ); 
        } 
    } 
 
    // ========================================================= 
    // CASTLING 
    // ========================================================= 
 
    private void TryAddCastling( 
        GameState state, 
        Vector2Int from, 
        StatePiece king, 
        Vector2Int dir, 
        int kingDistance, 
        int rookDistance, 
        List<StateMove> result) 
    { 
        int rookX = 
            from.x + 
            dir.x * 
            4; 
 
        int rookY = 
            from.y + 
            dir.y * 
            4; 
 
        if (!HexChessRules.IsValidCell( 
                rookX, 
                rookY)) 
        { 
            return; 
        } 
 
        StatePiece? rook = 
            state.GetPiece( 
                rookX, 
                rookY 
            ); 
 
        if (!rook.HasValue) 
        { 
            return; 
        } 
 
        if (rook.Value.Type != 
            StatePieceType.ROOK) 
        { 
            return; 
        } 
 
        if (rook.Value.Player != 
            king.Player) 
        { 
            return; 
        } 
 
        if (rook.Value.HasMoved) 
        { 
            return; 
        } 
 
        // ----------------------------------------------------- 
        // BETWEEN KING AND ROOK 
        // ----------------------------------------------------- 
 
        for (int i = 1; 
             i <= 3; 
             i++) 
        { 
            int x = 
                from.x + 
                dir.x * 
                i; 
 
            int y = 
                from.y + 
                dir.y * 
                i; 
 
            if (!HexChessRules.IsValidCell( 
                    x, 
                    y)) 
            { 
                return; 
            } 
 
            if (state.GetPiece( 
                    x, 
                    y 
                ).HasValue) 
            { 
                return; 
            } 
        } 
 
        // ----------------------------------------------------- 
        // KING PATH 
        // ----------------------------------------------------- 
 
        for (int i = 1; 
             i <= kingDistance; 
             i++) 
        { 
            int x = 
                from.x + 
                dir.x * 
                i; 
 
            int y = 
                from.y + 
                dir.y * 
                i; 
 
            GameState test = 
                state.Clone(); 
 
            test.SetPiece( 
                from.x, 
                from.y, 
                null 
            ); 
 
            test.SetPiece( 
                x, 
                y, 
                king 
            ); 
 
            if (IsSquareAttacked( 
                    test, 
                    x, 
                    y, 
                    HexChessRules.GetOpponent( 
                        king.Player))) 
            { 
                return; 
            } 
        } 
 
        int kingX = 
            from.x + 
            dir.x * 
            kingDistance; 
 
        int kingY = 
            from.y + 
            dir.y * 
            kingDistance; 
 
        int rookTargetX = 
            from.x + 
            dir.x * 
            rookDistance; 
 
        int rookTargetY = 
            from.y + 
            dir.y * 
            rookDistance; 
 
        if (!HexChessRules.IsValidCell( 
                kingX, 
                kingY)) 
        { 
            return; 
        } 
 
        if (!HexChessRules.IsValidCell( 
                rookTargetX, 
                rookTargetY)) 
        { 
            return; 
        } 
 
        if (state.GetPiece( 
                kingX, 
                kingY 
            ).HasValue) 
        { 
            return; 
        } 
 
        if (state.GetPiece( 
                rookTargetX, 
                rookTargetY 
            ).HasValue) 
        { 
            return; 
        } 
 
        StateMove move = 
            new StateMove( 
                new CLocation( 
                    from.x, 
                    from.y 
                ), 
                new CLocation( 
                    kingX, 
                    kingY 
                ), 
                king 
            ); 
 
        move.IsCastling = 
            true; 
 
        move.RookFrom = 
            new CLocation( 
                rookX, 
                rookY 
            ); 
 
        move.RookTo = 
            new CLocation( 
                rookTargetX, 
                rookTargetY 
            ); 
 
        move.RookPiece = 
            rook.Value; 
 
        result.Add( 
            move 
        ); 
    } 
 
    // ========================================================= 
    // ADD MOVE 
    // ========================================================= 
 
    private void AddMove( 
        GameState state, 
        Vector2Int from, 
        Vector2Int to, 
        StatePiece movingPiece, 
        List<StateMove> result) 
    { 
        StateMove move = 
            new StateMove( 
                new CLocation( 
                    from.x, 
                    from.y 
                ), 
                new CLocation( 
                    to.x, 
                    to.y 
                ), 
                movingPiece 
            ); 
 
        StatePiece? target = 
            state.GetPiece( 
                to.x, 
                to.y 
            ); 
 
        if (target.HasValue) 
        { 
            move.IsCapture = 
                true; 
 
            move.CapturedPiece = 
                target.Value; 
 
            move.CapturedLocation = 
                new CLocation( 
                    to.x, 
                    to.y 
                ); 
        } 
 
        if (movingPiece.Type == 
            StatePieceType.PAWN) 
        { 
            if (HexChessRules.IsPromotionRank( 
                    movingPiece.Player, 
                    to.x, 
                    to.y)) 
            { 
                move.IsPromotion = 
                    true; 
 
                move.PromotionType = 
                    StatePieceType.QUEEN; 
            } 
        } 
 
        result.Add( 
            move 
        ); 
    } 
 
    // ========================================================= 
    // MAKE MOVE 
    // ========================================================= 
 
    public void MakeMove( 
    GameState state, 
    StateMove move) 
    { 
        if (state == null) 
        { 
            return; 
        } 
 
        // ========================================================= 
        // CLEAR OLD EN PASSANT 
        // ========================================================= 
 
        state.HasEnPassantTarget = 
            false; 
 
        // ========================================================= 
        // MOVING PIECE 
        // ========================================================= 
 
        StatePiece moving = 
            move.MovingPiece; 
 
        // ========================================================= 
        // MARK AS MOVED 
        // ========================================================= 
 
        moving.HasMoved = 
            true; 
 
        // ========================================================= 
        // REMOVE FROM SOURCE 
        // ========================================================= 
 
        state.SetPiece( 
            move.From.X, 
            move.From.Y, 
            null 
        ); 
 
        // ========================================================= 
        // EN PASSANT CAPTURE 
        // ========================================================= 
 
        if (move.IsEnPassant) 
        { 
            state.SetPiece( 
                move.CapturedLocation.X, 
                move.CapturedLocation.Y, 
                null 
            ); 
        } 
 
        // ========================================================= 
        // PROMOTION 
        // ========================================================= 
 
        if (move.IsPromotion) 
        { 
            moving.Type = 
                move.PromotionType; 
        } 
 
        // ========================================================= 
        // DESTINATION 
        // 
        // SetPiece() tự: 
        // - remove captured piece 
        // - add moving piece 
        // - update Zobrist 
        // ========================================================= 
 
        state.SetPiece( 
            move.To.X, 
            move.To.Y, 
            moving 
        ); 
 
        // ========================================================= 
        // CASTLING 
        // ========================================================= 
 
        if (move.IsCastling) 
        { 
            StatePiece rook = 
                move.RookPiece; 
 
            rook.HasMoved = 
                true; 
 
            state.SetPiece( 
                move.RookFrom.X, 
                move.RookFrom.Y, 
                null 
            ); 
 
            state.SetPiece( 
                move.RookTo.X, 
                move.RookTo.Y, 
                rook 
            ); 
        } 
 
        // ========================================================= 
        // NEW EN PASSANT TARGET 
        // ========================================================= 
 
        if ( 
            moving.Type == 
            StatePieceType.PAWN && 
            Mathf.Abs( 
                move.From.Y - 
                move.To.Y 
            ) > 1 
        ) 
        { 
            int direction = 
                moving.Player == 
                EPlayer.WHITE 
                    ? 1 
                    : -1; 
 
            state.EnPassantTarget = 
                new CLocation( 
                    move.From.X, 
                    move.From.Y + 
                    direction * 
                    Mathf.Abs( 
                        move.To.Y - 
                        move.From.Y 
                    ) / 2 
                ); 
 
            state.HasEnPassantTarget = 
                true; 
        } 
 
        // ========================================================= 
        // LAST MOVE 
        // ========================================================= 
 
        state.LastMove = 
            move; 
 
        // ========================================================= 
        // CHANGE SIDE 
        // 
        // property tự cập nhật Zobrist. 
        // ========================================================= 
 
        state.SideToMove = 
            HexChessRules.GetOpponent( 
                state.SideToMove 
            ); 
    } 
 
    // ========================================================= 
    // UNDO 
    // ========================================================= 
 
    public void UndoMove( 
        GameState state, 
        StateMove move) 
    { 
        Debug.LogWarning( 
            "[MoveGenerator] UndoMove is not used by Minimax " + 
            "Search uses Clone() states." 
        ); 
    } 
 
    // ========================================================= 
    // CHECK 
    // ========================================================= 
 
    public bool IsInCheck( 
        GameState state, 
        EPlayer player) 
    { 
        Vector2Int king = 
            FindKing( 
                state, 
                player 
            ); 
 
        if (king.x < 0) 
        { 
            return true; 
        } 
 
        return IsSquareAttacked( 
            state, 
            king.x, 
            king.y, 
            HexChessRules.GetOpponent( 
                player 
            ) 
        ); 
    } 
 
    private Vector2Int FindKing( 
        GameState state, 
        EPlayer player) 
    { 
        foreach ( 
            Vector2Int position in 
            HexChessRules.CellCoordinates) 
        { 
            StatePiece? piece = 
                state.GetPiece( 
                    position.x, 
                    position.y 
                ); 
 
            if (!piece.HasValue) 
            { 
                continue; 
            } 
 
            if (piece.Value.Player == 
                    player && 
                piece.Value.Type == 
                    StatePieceType.KING) 
            { 
                return position; 
            } 
        } 
 
        return new Vector2Int( 
            -1, 
            -1 
        ); 
    } 
 
    // ========================================================= 
    // SQUARE ATTACKED 
    // ========================================================= 
 
    public bool IsSquareAttacked( 
        GameState state, 
        int targetX, 
        int targetY, 
        EPlayer attacker) 
    { 
        foreach ( 
            Vector2Int position in 
            HexChessRules.CellCoordinates) 
        { 
            StatePiece? piece = 
                state.GetPiece( 
                    position.x, 
                    position.y 
                ); 
 
            if (!piece.HasValue) 
            { 
                continue; 
            } 
 
            if (piece.Value.Player != attacker) 
            { 
                continue; 
            } 
 
            if (CanPieceAttack(state, position, piece.Value, targetX, targetY)) 
            { 
                return true; 
            } 
        } 
 
        return false; 
    } 
 
    // CAN PIECE ATTACK 
 
    private bool CanPieceAttack(GameState state, Vector2Int from,  
    StatePiece piece, int targetX, int targetY) 
    { 
        int dx = targetX - from.x; 
        int dy = targetY - from.y; 
 
        // PAWN 
 
        if (piece.Type == StatePieceType.PAWN) 
        { 
            int dir = piece.Player == EPlayer.WHITE ? 1 : -1; 
            return dy == dir && Mathf.Abs(dx) == 1; 
        } 
 
        // KNIGHT 
 
        if (piece.Type == StatePieceType.KNIGHT) 
        { 
            foreach (Vector2Int move in HexChessRules.KnightMoves) 
            { 
                if (dx == move.x && 
                    dy == move.y) 
                { 
                    return true; 
                } 
            } 
 
            return false; 
        } 
 
        // KING 
 
        if (piece.Type == 
            StatePieceType.KING) 
        { 
            foreach (Vector2Int dir in HexChessRules.QueenDirections) 
            { 
                if (dx == dir.x && dy == dir.y) 
                { 
                    return true; 
                } 
            } 
 
            return false; 
        } 
 
        // SLIDING 
 
        Vector2Int[] directions = null; 
 
        switch (piece.Type) 
        { 
            case StatePieceType.BISHOP: 
                directions = HexChessRules.BishopDirections; 
                break; 
 
            case StatePieceType.ROOK: 
                directions = HexChessRules.RookDirections; 
                break; 
 
            case StatePieceType.QUEEN: 
                directions = HexChessRules.QueenDirections; 
                break; 
        } 
 
        if (directions == null) 
        { 
            return false; 
        } 
 
        foreach (Vector2Int dir in directions) 
        { 
            int x = from.x + dir.x; 
            int y = from.y + dir.y; 
 
            while (HexChessRules.IsValidCell(x,y)) 
            { 
                if (x == targetX && y == targetY) 
                { 
                    return true; 
                } 
 
                if (state.GetPiece(x,y).HasValue) 
                { 
                    break; 
                } 
 
                x += dir.x; 
                y += dir.y; 
            } 
        } 
 
        return false; 
    } 
}
