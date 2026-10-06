using System;

[Serializable]
public struct CLocation
{
    public int X;
    public int Y;

    public CLocation(int x, int y)
    {
        X = x;
        Y = y;
    }

    public override string ToString()
    {
        return $"[{X}, {Y}]";
    }

    public override bool Equals(object obj)
    {
        if (!(obj is CLocation))
            return false;

        CLocation other = (CLocation)obj;
        return X == other.X && Y == other.Y;
    }

    public override int GetHashCode()
    {
        return (X * 397) ^ Y;
    }

    public static bool operator ==(CLocation a, CLocation b)
    {
        return a.X == b.X && a.Y == b.Y;
    }

    public static bool operator !=(CLocation a, CLocation b)
    {
        return !(a == b);
    }
}