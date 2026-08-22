namespace Tactics.Core.Battle;

/// <summary>
/// Engine-neutral rectangular board bounds used by the first 10x10 migration fixture.
/// </summary>
public readonly record struct BattleBoardBounds(int Width, int Height)
{
    public static BattleBoardBounds TenByTen => new(10, 10);

    public int CellCount => checked(Width * Height);

    public bool Contains(GridPosition position) =>
        position.X >= 0 && position.X < Width &&
        position.Y >= 0 && position.Y < Height;
}
