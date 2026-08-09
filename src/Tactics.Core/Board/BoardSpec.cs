namespace Tactics.Core.Board;

public static class BoardSpec
{
    public const int Width = 10;
    public const int Height = 10;
    public const int CellCount = Width * Height;

    public static bool Contains(GridPoint point) =>
        point.X >= 0 && point.X < Width && point.Y >= 0 && point.Y < Height;
}
