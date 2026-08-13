using Godot;
using Tactics.Core.Units;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Resolves actor input from visible pixels before falling back to board projection.</summary>
public static class GodotUnitPointerResolver
{
    private const float AlphaThreshold = 0.1f;

    public static UnitInstanceId? Resolve(IReadOnlyDictionary<UnitInstanceId, GodotUnitActor> actors, Vector2 parentPoint) =>
        actors.Where(pair => GodotObject.IsInstanceValid(pair.Value) && pair.Value.ContainsOpaquePoint(parentPoint, AlphaThreshold))
            .OrderByDescending(pair => pair.Value.ZIndex)
            .ThenByDescending(pair => pair.Key.Value, StringComparer.Ordinal)
            .Select(pair => (UnitInstanceId?)pair.Key)
            .FirstOrDefault();
}
