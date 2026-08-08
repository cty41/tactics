using Godot;
using Tactics.Core.Runtime;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>
/// Minimal runtime entry point for the migration project. Gameplay remains in Core.
/// </summary>
public partial class TacticsMigrationRoot : Node
{
    public override void _Ready()
    {
        if (OS.GetCmdlineArgs().Contains("--validate-poison-spear"))
        {
            ValidatePoisonSpear();
            GetTree().Quit();
            return;
        }
        if (OS.GetCmdlineArgs().Contains("--play-poison-spear"))
        {
            _ = PlayPoisonSpearAsync();
            return;
        }
        GD.Print("Tactics Godot migration runtime ready");
    }

    private static void ValidatePoisonSpear()
    {
        var catalog = ResourceLoader.Load<GodotResourceCatalog>("res://content/poison_spear/ContentCatalog.tres")
            ?? throw new InvalidOperationException("Poison Spear ContentCatalog is missing.");
        PoisonSpearSliceValidation validation = PoisonSpearSliceValidator.Validate(catalog);
        GD.Print($"Poison Spear validation OK: entries={validation.CatalogEntryCount}, damage={validation.Action.Damage}, poisonTurns={validation.Action.PoisonTurns}, presentationNodes={validation.Presentation.Nodes.Count}");
    }

    private async Task PlayPoisonSpearAsync()
    {
        try
        {
            var catalog = ResourceLoader.Load<GodotResourceCatalog>("res://content/poison_spear/ContentCatalog.tres")
                ?? throw new InvalidOperationException("Poison Spear ContentCatalog is missing.");
            PoisonSpearSliceValidation validation = PoisonSpearSliceValidator.Validate(catalog);
            var presentation = catalog.TryGet("presentation.poison-spear.lv1", out Resource? resource)
                ? resource as PoisonSpearPresentationResource
                : null;
            if (presentation is null)
                throw new InvalidOperationException("Poison Spear presentation is missing from the catalog.");

            var player = new PoisonSpearPresentationPlayer();
            AddChild(player);
            using var scope = new BattleRuntimeScope();
            await player.Start(new Vector2(15f, 15f), new Vector2(35f, 25f), presentation, scope);
            await scope.WhenIdleAsync();
            GD.Print($"Poison Spear presentation OK: actionDamage={validation.Action.Damage}, nodes={validation.Presentation.Nodes.Count}");
            player.QueueFree();
        }
        catch (Exception exception)
        {
            GD.PushError($"Poison Spear presentation failed: {exception}");
            GetTree().Quit(1);
            return;
        }

        GetTree().Quit();
    }
}
