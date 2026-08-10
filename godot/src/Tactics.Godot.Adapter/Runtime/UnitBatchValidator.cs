using Godot;
using Tactics.Application.Units;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Units;

namespace Tactics.Godot.Adapter.Runtime;

public sealed record UnitBatchValidation(
    int CatalogEntryCount,
    int UnitCount,
    IReadOnlyList<BattleUnitState> SpawnedStates);

/// <summary>
/// Validates the final Unit Catalog, Resource, PackedScene, and Core factory boundary.
/// </summary>
public static class UnitBatchValidator
{
    public static UnitBatchValidation Validate(GodotResourceCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        GodotCatalogCompilation compilation = GodotCatalogCompiler.Compile(catalog);
        if (catalog.Entries.Length != 13 || compilation.Snapshot.Entries.Count != 13)
            throw new InvalidOperationException("Pure Run Unit Catalog must contain 12 Units and one actor scene.");

        GodotResourceEntry actorEntry = catalog.Entries.Single(
            entry => entry.ContentIdValue == "packed-scene.unit-actor");
        if (actorEntry.ResourceTypeIdValue != "packed-scene")
            throw new InvalidOperationException("Pure Run Unit actor has the wrong resource type.");
        PackedScene actorScene = compilation.Resources.Get<PackedScene>(
            new ContentId(actorEntry.ContentIdValue));
        Node actorInstance = actorScene.Instantiate();
        if (actorInstance is not GodotUnitActor)
        {
            actorInstance.Free();
            throw new InvalidOperationException("Pure Run Unit actor scene has the wrong root type.");
        }
        actorInstance.Free();

        GodotResourceEntry[] unitEntries = catalog.Entries
            .Where(entry => entry.ResourceTypeIdValue == "unit")
            .OrderBy(entry => entry.ContentIdValue, StringComparer.Ordinal)
            .ToArray();
        if (unitEntries.Length != 12)
            throw new InvalidOperationException("Pure Run Unit Catalog must contain exactly 12 Unit entries.");
        var spawned = new List<BattleUnitState>();
        for (int index = 0; index < unitEntries.Length; index++)
        {
            UnitDefinitionResource definition = compilation.Resources.Get<UnitDefinitionResource>(
                new ContentId(unitEntries[index].ContentIdValue));
            BattleUnitState state = GodotUnitFactory.CreateBattleState(
                definition,
                new UnitInstanceId($"validation.unit.{index}"),
                new GridPoint(index % 4, index / 4),
                index < 3 ? 0 : 1,
                index);
            if (state.Unit.DefinitionId.Value != definition.ContentIdValue ||
                state.Unit.InstanceId.Value == definition.ContentIdValue ||
                state.CurrentHealth != definition.MaxHealth ||
                state.CurrentMana != definition.StartingMana)
            {
                throw new InvalidOperationException(
                    $"Unit factory state differs from '{definition.ContentIdValue}'.");
            }

            GodotUnitActor actor = GodotUnitFactory.InstantiateActor(definition);
            Texture2D originalShadow = actor.Shadow!.Texture!;
            actor.SetFacing(GodotUnitFacing.South);
            if (actor.Body!.Texture != definition.DownRightTexture || actor.Body.FlipH)
                throw new InvalidOperationException("Unit actor south-facing texture contract failed.");
            actor.SetFacing(GodotUnitFacing.North);
            if (actor.Body.Texture != definition.UpLeftTexture || actor.Body.FlipH)
                throw new InvalidOperationException("Unit actor north-facing texture contract failed.");
            actor.SetFacing(GodotUnitFacing.East);
            if (actor.Body.Texture != definition.UpLeftTexture || !actor.Body.FlipH)
                throw new InvalidOperationException("Unit actor east-facing texture contract failed.");
            actor.SetFacing(GodotUnitFacing.West);
            if (actor.Body.Texture != definition.DownRightTexture || !actor.Body.FlipH ||
                actor.Shadow.FlipH || actor.Shadow.Texture != originalShadow)
                throw new InvalidOperationException("Unit actor mirrors Shadow or fails to mirror west-facing Body.");
            if (definition.BodyTintModeValue == UnitBodyTintModes.GoatBodyMaskV1)
            {
                if (actor.Body.Material != definition.BodyTintMaterial ||
                    !actor.Body.Modulate.IsEqualApprox(Colors.White))
                {
                    throw new InvalidOperationException("Goat Unit actor did not apply its body-mask material.");
                }
            }
            else if (actor.Body.Material is not null ||
                !actor.Body.Modulate.IsEqualApprox(definition.BodyTint))
            {
                throw new InvalidOperationException("Multiply-tinted Unit actor has an invalid material contract.");
            }
            actor.SetDeathVisual(true);
            if (definition.CanProduceCorpse && actor.Body.Texture != definition.DeathTexture)
                throw new InvalidOperationException("Unit actor death texture contract failed.");
            actor.Free();
            spawned.Add(state);
        }

        return new UnitBatchValidation(catalog.Entries.Length, unitEntries.Length, spawned);
    }
}
