public enum StatePieceType
{
    NONE,
    PAWN,
    KNIGHT,
    BISHOP,
    ROOK,
    QUEEN,
    KING
}

public struct StatePiece
{
    public StatePieceType Type;
    public EPlayer Player;
    public bool HasMoved;

    public StatePiece(
        StatePieceType type,
        EPlayer player,
        bool hasMoved = false)
    {
        Type = type;
        Player = player;
        HasMoved = hasMoved;
    }

    public bool IsValid =>
        Type != StatePieceType.NONE;

    public int Value
    {
        get
        {
            switch (Type)
            {
                case StatePieceType.PAWN:
                    return 100;

                case StatePieceType.KNIGHT:
                    return 350;

                case StatePieceType.BISHOP:
                    return 350;

                case StatePieceType.ROOK:
                    return 500;

                case StatePieceType.QUEEN:
                    return 1000;

                case StatePieceType.KING:
                    return 20000;

                default:
                    return 0;
            }
        }
    }
}