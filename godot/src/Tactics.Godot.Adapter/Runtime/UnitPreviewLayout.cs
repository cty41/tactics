using Godot;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>
/// Defines the native canvas shared by generated Unit visual fixtures and captures.
/// </summary>
internal static class UnitPreviewLayout
{
    internal const int CanvasWidth = 1600;
    internal const int CanvasHeight = 900;
    internal const string CanvasContract = "native-1600x900-v1";

    internal static readonly Vector2I CanvasSize = new(CanvasWidth, CanvasHeight);
    internal static readonly Rect2 CanvasRect = new(
        Vector2.Zero,
        new Vector2(CanvasWidth, CanvasHeight));
}
