using Godot;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Units;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>
/// Creates Core battle state and presentation actors from one generated Unit resource.
/// </summary>
public static class GodotUnitFactory
{
    public static BattleUnitState CreateBattleState(
        UnitDefinitionResource resource,
        UnitInstanceId instanceId,
        GridPoint position,
        int playerNumber,
        int spawnOrdinal)
    {
        ArgumentNullException.ThrowIfNull(resource);
        UnitDefinition definition = resource.ToCoreDefinition();
        return definition.CreateBattleState(instanceId, position, playerNumber, spawnOrdinal);
    }

    public static GodotUnitActor InstantiateActor(UnitDefinitionResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        resource.ValidateVisualContract();
        Node instance = resource.ActorScene!.Instantiate();
        if (instance is not GodotUnitActor actor)
        {
            instance.Free();
            throw new InvalidOperationException(
                $"Actor scene for '{resource.ContentIdValue}' does not instantiate {nameof(GodotUnitActor)}.");
        }
        actor.Configure(resource);
        return actor;
    }
}
