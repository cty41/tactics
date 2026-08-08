using Godot;
using Tactics.Core.Board;
using Tactics.Core.Combat;
using Tactics.Core.Content;
using Tactics.Core.Presentation;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>
/// Validates the first vertical slice without making presentation state part of Core.
/// </summary>
public sealed record PoisonSpearSliceValidation(
    ActionResult Action,
    PresentationExecutionPlan Presentation,
    int CatalogEntryCount);

public static class PoisonSpearSliceValidator
{
    public static PoisonSpearSliceValidation Validate(GodotResourceCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        GodotContentSnapshot snapshot = GodotContentSnapshot.Compile(catalog);

        var skill = snapshot.Get<PoisonSpearSkillResource>(new ContentId("skill.poison-spear.lv1"));
        var presentation = snapshot.Get<PoisonSpearPresentationResource>(new ContentId("presentation.poison-spear.lv1"));
        var fixture = snapshot.Get<PoisonSpearFixtureResource>(new ContentId("encounter.poison-spear.10x10"));
        var projectileScene = snapshot.Get<PackedScene>(new ContentId("projectile.poison-spear"));
        var impactScene = snapshot.Get<PackedScene>(new ContentId("impact.poison-spear"));

        if (fixture.BoardWidth != BoardSpec.Width || fixture.BoardHeight != BoardSpec.Height)
            throw new InvalidOperationException("Poison Spear fixture is not the fixed 10x10 board.");
        if (skill.Presentation is null || skill.Presentation.ContentIdValue != presentation.ContentIdValue)
            throw new InvalidOperationException("Poison Spear skill does not reference its cataloged presentation.");
        if (!string.Equals(presentation.ProjectileScenePath, CatalogPath(projectileScene), StringComparison.Ordinal))
            throw new InvalidOperationException("Poison Spear presentation projectile path is not cataloged.");
        if (!string.Equals(presentation.ImpactScenePath, CatalogPath(impactScene), StringComparison.Ordinal))
            throw new InvalidOperationException("Poison Spear presentation impact path is not cataloged.");

        ValidateSceneScript<PoisonSpearProjectile>(projectileScene, "projectile.poison-spear");
        ValidateSceneScript<PoisonSpearImpact>(impactScene, "impact.poison-spear");
        PresentationExecutionPlan plan = presentation.BuildExecutionPlan();

        var cells = new Dictionary<GridPoint, CellState>();
        for (int x = 0; x < BoardSpec.Width; x++)
        {
            for (int y = 0; y < BoardSpec.Height; y++)
                cells[new GridPoint(x, y)] = new CellState();
        }

        ActionResult action = new PoisonSpearResolver().Resolve(
            new BoardSnapshot(cells),
            new UnitState(new ContentId("unit.caster"), new GridPoint(fixture.CasterCell.X, fixture.CasterCell.Y), 3, 5),
            new UnitState(new ContentId("unit.target"), new GridPoint(fixture.TargetCell.X, fixture.TargetCell.Y), 3, 4),
            new PoisonSpearDefinition(skill.ContentId, skill.Range, skill.Damage, skill.PoisonTurns));
        if (!action.Succeeded)
            throw new InvalidOperationException($"Poison Spear validation failed: {action.FailureReason}");

        return new PoisonSpearSliceValidation(action, plan, catalog.Entries.Length);
    }

    private static void ValidateSceneScript<T>(PackedScene scene, string contentId) where T : Node
    {
        Node instance = scene.Instantiate();
        try
        {
            if (instance is not T)
                throw new InvalidOperationException($"Catalog entry '{contentId}' does not instantiate {typeof(T).Name}.");
        }
        finally
        {
            instance.Free();
        }
    }

    private static string CatalogPath(PackedScene scene)
    {
        // PackedScene.ResourcePath is the stable Godot identity used by the catalog.
        return scene.ResourcePath;
    }
}
