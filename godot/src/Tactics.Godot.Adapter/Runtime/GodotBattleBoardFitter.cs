using Godot;
using Tactics.Core.Board;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Fits the complete canonical board bounds into a HUD-safe viewport.</summary>
public static class GodotBattleBoardFitter
{
    public static Rect2 BoardBounds()
    {
        Vector2[] points = Enumerable.Range(0, IsometricBattleBoardLayout.GridSize)
            .SelectMany(y => Enumerable.Range(0, IsometricBattleBoardLayout.GridSize)
                .SelectMany(x => IsometricBattleBoardLayout.Diamond(new GridPoint(x, y))))
            .ToArray();
        float minX = points.Min(value => value.X), minY = points.Min(value => value.Y);
        float maxX = points.Max(value => value.X), maxY = points.Max(value => value.Y);
        return new Rect2(minX, minY, maxX - minX, maxY - minY);
    }

    public static Transform2D Fit(Rect2 bounds, Rect2 safeArea)
    {
        if (bounds.Size.X <= 0 || bounds.Size.Y <= 0 || safeArea.Size.X <= 0 || safeArea.Size.Y <= 0)
            throw new ArgumentOutOfRangeException(nameof(bounds));
        float scale = MathF.Min(safeArea.Size.X / bounds.Size.X, safeArea.Size.Y / bounds.Size.Y);
        Vector2 offset = safeArea.GetCenter() - bounds.GetCenter() * scale;
        return new Transform2D(0f, new Vector2(scale, scale), 0f, offset);
    }

    public static Rect2 TransformBounds(Rect2 bounds, Transform2D transform)
    {
        Vector2[] corners =
        [
            transform * bounds.Position,
            transform * new Vector2(bounds.End.X, bounds.Position.Y),
            transform * bounds.End,
            transform * new Vector2(bounds.Position.X, bounds.End.Y)
        ];
        float minX = corners.Min(value => value.X), minY = corners.Min(value => value.Y);
        float maxX = corners.Max(value => value.X), maxY = corners.Max(value => value.Y);
        return new Rect2(minX, minY, maxX - minX, maxY - minY);
    }
}
