public class Move
{
    public CLocation From;
    public CLocation To;

    public BasePiece MovedPiece;
    public BasePiece CapturedPiece;

    // Nhập thành
    public bool IsCastling;
    public CLocation RookFrom;
    public CLocation RookTo;
    public BasePiece RookPiece;

    // Phong cấp
    public bool IsPromotion;

    // En passant
    public bool IsEnPassant;
    public CLocation CapturedLocation;

    public Move(
        CLocation from,
        CLocation to,
        BasePiece movedPiece,
        BasePiece capturedPiece = null)
    {
        From = from;
        To = to;
        MovedPiece = movedPiece;
        CapturedPiece = capturedPiece;
    }
}