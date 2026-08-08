using Godot;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

public static class PoisonSpearAssetFactory
{
    public static void BuildLv1(string root = "res://content/poison_spear")
    {
        EnsureDirectory(root);

        var presentation = new PoisonSpearPresentationResource
        {
            ContentIdValue = "presentation.poison-spear.lv1",
            SchemaVersion = 1,
            ProjectileScenePath = $"{root}/PoisonSpearProjectile.tscn",
            ImpactScenePath = $"{root}/PoisonSpearImpact.tscn"
        };
        var skill = new PoisonSpearSkillResource
        {
            ContentIdValue = "skill.poison-spear.lv1",
            Range = 6,
            MoveCost = 6,
            Damage = 8,
            PoisonTurns = 3,
            Presentation = presentation
        };
        var fixture = new PoisonSpearFixtureResource
        {
            ContentIdValue = "encounter.poison-spear.10x10",
            BoardWidth = 10,
            BoardHeight = 10,
            CasterCell = new Vector2I(1, 1),
            TargetCell = new Vector2I(3, 2)
        };

        Save(skill, $"{root}/PoisonSpearSkillLv1.tres");
        Save(presentation, $"{root}/PoisonSpearPresentationLv1.tres");
        Save(fixture, $"{root}/PoisonSpear10x10Fixture.tres");

        SaveScene<PoisonSpearProjectile>($"{root}/PoisonSpearProjectile.tscn", "PoisonSpearProjectile");
        SaveScene<PoisonSpearImpact>($"{root}/PoisonSpearImpact.tscn", "PoisonSpearImpact");

        var catalog = new GodotResourceCatalog
        {
            Entries = new[]
            {
                new GodotResourceEntry { ContentIdValue = skill.ContentIdValue, ResourcePathValue = $"{root}/PoisonSpearSkillLv1.tres" },
                new GodotResourceEntry { ContentIdValue = presentation.ContentIdValue, ResourcePathValue = $"{root}/PoisonSpearPresentationLv1.tres" },
                new GodotResourceEntry { ContentIdValue = fixture.ContentIdValue, ResourcePathValue = $"{root}/PoisonSpear10x10Fixture.tres" },
                new GodotResourceEntry { ContentIdValue = "projectile.poison-spear", ResourcePathValue = $"{root}/PoisonSpearProjectile.tscn" },
                new GodotResourceEntry { ContentIdValue = "impact.poison-spear", ResourcePathValue = $"{root}/PoisonSpearImpact.tscn" }
            }
        };
        Save(catalog, $"{root}/ContentCatalog.tres");
    }

    private static void EnsureDirectory(string resourceDirectory)
    {
        string absolutePath = ProjectSettings.GlobalizePath(resourceDirectory);
        Error error = DirAccess.MakeDirRecursiveAbsolute(absolutePath);
        if (error != Error.Ok && error != Error.AlreadyExists)
            throw new InvalidOperationException($"Cannot create '{resourceDirectory}': {error}");
    }

    private static void Save(Resource resource, string path)
    {
        Error error = ResourceSaver.Save(resource, path);
        if (error != Error.Ok)
            throw new InvalidOperationException($"Cannot save '{path}': {error}");
    }

    private static void SaveScene<T>(string path, string name) where T : Node, new()
    {
        PackedScene? existing = ResourceLoader.Load<PackedScene>(path);
        if (existing is not null)
            return;

        var root = new T { Name = name };
        var packedScene = new PackedScene();
        Error packError = packedScene.Pack(root);
        root.Free();
        if (packError != Error.Ok)
            throw new InvalidOperationException($"Cannot pack '{path}': {packError}");
        Save(packedScene, path);
    }
}
