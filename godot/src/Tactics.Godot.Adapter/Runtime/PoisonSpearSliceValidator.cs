using Godot;
using Tactics.Core.Board;
using Tactics.Core.Combat;
using Tactics.Core.Content;
using Tactics.Core.Presentation;
using Tactics.Core.Units;

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
        GodotCatalogCompilation compilation = GodotCatalogCompiler.Compile(catalog);
        GodotResourceRegistry resources = compilation.Resources;

        var skill = resources.Get<PoisonSpearSkillResource>(new ContentId("skill.poison-spear.lv1"));
        var poison = resources.Get<PoisonBuffResource>(new ContentId("buff.poison"));
        var presentation = resources.Get<PoisonSpearPresentationResource>(new ContentId("presentation.poison-spear.lv1"));
        var fixture = resources.Get<PoisonSpearFixtureResource>(new ContentId("encounter.poison-spear.10x10"));
        var projectileScene = resources.Get<PackedScene>(new ContentId("projectile.poison-spear"));
        var impactScene = resources.Get<PackedScene>(new ContentId("impact.poison-spear"));

        if (fixture.BoardWidth != BoardSpec.Width || fixture.BoardHeight != BoardSpec.Height)
            throw new InvalidOperationException("Poison Spear fixture is not the fixed 10x10 board.");
        if (skill.Presentation is null || skill.Presentation.ContentIdValue != presentation.ContentIdValue)
            throw new InvalidOperationException("Poison Spear skill does not reference its cataloged presentation.");
        if (skill.Poison is null || skill.Poison.ContentIdValue != poison.ContentIdValue)
            throw new InvalidOperationException("Poison Spear skill does not reference its cataloged Poison status.");
        if (skill.Range != 5 || skill.ManaCost != 6 || skill.Damage != 8 ||
            poison.DefaultDuration != 3 || poison.DamagePerTurn != 2 ||
            !skill.DropsSpearOnCompletion || skill.DropSearchRadius != 3 ||
            poison.RefreshStrategy != "AddDuration" || poison.TriggerTiming != "TurnStart")
        {
            throw new InvalidOperationException("Poison Spear resource values do not match the frozen Unity export.");
        }
        if (!string.Equals(presentation.ProjectileScenePath, CatalogPath(projectileScene), StringComparison.Ordinal))
            throw new InvalidOperationException("Poison Spear presentation projectile path is not cataloged.");
        if (!string.Equals(presentation.ImpactScenePath, CatalogPath(impactScene), StringComparison.Ordinal))
            throw new InvalidOperationException("Poison Spear presentation impact path is not cataloged.");

        ValidateSceneScript<PoisonSpearProjectile>(projectileScene, "projectile.poison-spear");
        ValidateSceneScript<PoisonSpearImpact>(impactScene, "impact.poison-spear");
        PresentationExecutionPlan plan = presentation.BuildExecutionPlan();
        presentation.ValidateAuthoringGraph();
#if TOOLS
        Tactics.Godot.Adapter.Editor.PoisonSpearPresentationEditorService.ValidateStoredRevision(presentation);
#endif

        var cells = new Dictionary<GridPoint, CellState>();
        for (int x = 0; x < BoardSpec.Width; x++)
        {
            for (int y = 0; y < BoardSpec.Height; y++)
                cells[new GridPoint(x, y)] = new CellState();
        }

        ActionResult action = new PoisonSpearResolver().Resolve(
            new BoardSnapshot(cells),
            new UnitState(
                new UnitInstanceId("party.caster.0"),
                new ContentId("unit.caster"),
                new GridPoint(fixture.CasterCell.X, fixture.CasterCell.Y),
                3,
                10,
                0,
                0),
            new UnitState(
                new UnitInstanceId("enemy.target.0"),
                new ContentId("unit.target"),
                new GridPoint(fixture.TargetCell.X, fixture.TargetCell.Y),
                3,
                8,
                1,
                1),
            new PoisonSpearDefinition(
                skill.ContentId,
                skill.Range,
                skill.Damage,
                skill.PoisonTurns,
                poison.ContentId,
                skill.PoisonDamagePerTurn,
                skill.ManaCost,
                skill.DropSearchRadius));
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
