public struct StateMove
{
    public CLocation From;
    public CLocation To;

    public StatePiece MovingPiece;

    public bool IsCapture;
    public StatePiece CapturedPiece;
    public CLocation CapturedLocation;

    public bool IsPromotion;
    public StatePieceType PromotionType;

    public bool IsCastling;
    public CLocation RookFrom;
    public CLocation RookTo;
    public StatePiece RookPiece;

    public bool IsEnPassant;

    public StateMove(
        CLocation from,
        CLocation to,
        StatePiece movingPiece)
    {
        From = from;
        To = to;

        MovingPiece = movingPiece;

        IsCapture = false;
        CapturedPiece = default;
        CapturedLocation = default;

        IsPromotion = false;
        PromotionType = StatePieceType.NONE;

        IsCastling = false;
        RookFrom = default;
        RookTo = default;
        RookPiece = default;

        IsEnPassant = false;
    }
}