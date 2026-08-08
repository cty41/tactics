using System;

namespace Tactics.Core.Board;

/// <summary>
/// Engine-independent local board coordinate.
/// </summary>
public readonly struct GridPoint : IEquatable<GridPoint>, IComparable<GridPoint>
{
    public GridPoint(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int X { get; }
    public int Y { get; }

    public bool Equals(GridPoint other) => X == other.X && Y == other.Y;

    public override bool Equals(object? obj) => obj is GridPoint other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public int CompareTo(GridPoint other)
    {
        int yComparison = Y.CompareTo(other.Y);
        return yComparison != 0 ? yComparison : X.CompareTo(other.X);
    }

    public override string ToString() => $"({X},{Y})";

    public static bool operator ==(GridPoint left, GridPoint right) => left.Equals(right);

    public static bool operator !=(GridPoint left, GridPoint right) => !left.Equals(right);
}
