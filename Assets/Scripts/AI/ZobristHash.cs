using System;
using UnityEngine;

public static class ZobristHash
{
    private static bool _initialized;

    // [file, rank, pieceType, player, hasMoved]
    private static ulong[,,,,] _pieceKeys;

    private static ulong[] _sideKeys;

    private static ulong[] _enPassantKeys;

    private const int PIECE_TYPE_COUNT = 7;

    private const int PLAYER_COUNT = 2;

    private const int MOVED_STATE_COUNT = 2;

    // =========================================================
    // INITIALIZE
    // =========================================================

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        System.Random random =
            new System.Random(
                20260910
            );

        _pieceKeys =
            new ulong[
                HexChessRules.FILE_COUNT,
                HexChessRules.MAX_RANK + 1,
                PIECE_TYPE_COUNT,
                PLAYER_COUNT,
                MOVED_STATE_COUNT
            ];

        _sideKeys =
            new ulong[2];

        _enPassantKeys =
            new ulong[
                HexChessRules.CELL_COUNT + 1
            ];

        // =====================================================
        // PIECE KEYS
        // =====================================================

        for (
            int x = 0;
            x < HexChessRules.FILE_COUNT;
            x++)
        {
            for (
                int y = 0;
                y <= HexChessRules.MAX_RANK;
                y++)
            {
                for (
                    int piece = 0;
                    piece < PIECE_TYPE_COUNT;
                    piece++)
                {
                    for (
                        int player = 0;
                        player < PLAYER_COUNT;
                        player++)
                    {
                        for (
                            int moved = 0;
                            moved < MOVED_STATE_COUNT;
                            moved++)
                        {
                            _pieceKeys[
                                x,
                                y,
                                piece,
                                player,
                                moved
                            ] =
                                NextUInt64(
                                    random
                                );
                        }
                    }
                }
            }
        }

        // =====================================================
        // SIDE KEYS
        // =====================================================

        for (
            int i = 0;
            i < _sideKeys.Length;
            i++)
        {
            _sideKeys[i] =
                NextUInt64(
                    random
                );
        }

        // =====================================================
        // EN PASSANT KEYS
        // =====================================================

        for (
            int i = 0;
            i < _enPassantKeys.Length;
            i++)
        {
            _enPassantKeys[i] =
                NextUInt64(
                    random
                );
        }

        _initialized =
            true;
    }

    // =========================================================
    // PIECE KEY
    // =========================================================

    public static ulong GetPieceKey(
        int x,
        int y,
        StatePiece piece)
    {
        Initialize();

        int pieceIndex =
            (int)piece.Type;

        int playerIndex =
            piece.Player ==
            EPlayer.WHITE
                ? 0
                : 1;

        int movedIndex =
            piece.HasMoved
                ? 1
                : 0;

        if (x < 0 ||
            x >= HexChessRules.FILE_COUNT)
        {
            return 0UL;
        }

        if (y < 0 ||
            y > HexChessRules.MAX_RANK)
        {
            return 0UL;
        }

        if (pieceIndex < 0 ||
            pieceIndex >= PIECE_TYPE_COUNT)
        {
            return 0UL;
        }

        if (playerIndex < 0 ||
            playerIndex >= PLAYER_COUNT)
        {
            return 0UL;
        }

        return
            _pieceKeys[
                x,
                y,
                pieceIndex,
                playerIndex,
                movedIndex
            ];
    }

    // =========================================================
    // SIDE KEY
    // =========================================================

    public static ulong GetSideKey(
        EPlayer player)
    {
        Initialize();

        int index =
            player ==
            EPlayer.WHITE
                ? 0
                : 1;

        return _sideKeys[index];
    }

    // =========================================================
    // EN PASSANT KEY
    // =========================================================

    public static ulong GetEnPassantKey(
        CLocation location)
    {
        Initialize();

        int index =
            HexChessRules.GetCellIndex(
                location.X,
                location.Y
            );

        if (index < 0 ||
            index >= _enPassantKeys.Length)
        {
            return 0UL;
        }

        return _enPassantKeys[index];
    }

    // =========================================================
    // FULL COMPUTE
    // =========================================================
    //
    // Không dùng trong Search nóng.
    // Dùng để:
    // - kiểm tra tính đúng của incremental hash
    // - repetition benchmark
    // - debug
    //
    // =========================================================

    public static ulong Compute(
        GameState state)
    {
        Initialize();

        if (state == null)
        {
            return 0UL;
        }

        ulong hash =
            0UL;

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

            hash ^=
                GetPieceKey(
                    position.x,
                    position.y,
                    piece.Value
                );
        }

        hash ^=
            GetSideKey(
                state.SideToMove
            );

        if (state.HasEnPassantTarget)
        {
            hash ^=
                GetEnPassantKey(
                    state.EnPassantTarget
                );
        }

        return hash;
    }

    // =========================================================
    // VERIFY HASH
    // =========================================================

    public static bool Verify(
        GameState state)
    {
        if (state == null)
        {
            return false;
        }

        ulong computed =
            Compute(
                state
            );

        return
            computed ==
            state.Hash;
    }

    // =========================================================
    // RANDOM UINT64
    // =========================================================

    private static ulong NextUInt64(
        System.Random random)
    {
        byte[] bytes =
            new byte[8];

        random.NextBytes(
            bytes
        );

        return BitConverter.ToUInt64(
            bytes,
            0
        );
    }
}