using Godot;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

/// <summary>Generates the canonical main PackedScene through Godot APIs.</summary>
[Tool]
public partial class PlayableRunSceneBuilder : SceneTree
{
    private const string MainScenePath = "res://scenes/Main.tscn";
    private const string MainSceneUid = "uid://c0mlqoh7vensn";
    private const string BalancePath = "res://content/ui/PlayableLv1BalanceProfile.tres";
    private const string EnemySpeedPath = "res://content/ui/PlayableEnemySpeedProfile.tres";

    public override void _Initialize()
    {
        try
        {
            SaveBalanceProfile();
            SaveEnemySpeedProfile();
            if (ResourceLoader.Load<PackedScene>(MainScenePath, string.Empty, ResourceLoader.CacheMode.Ignore) is PackedScene existing)
            {
                RequireUid();
                Node instance = existing.Instantiate();
                bool valid = instance is TacticsMigrationRoot;
                instance.Free();
                if (!valid) throw new InvalidOperationException("Existing Main scene has the wrong root type.");
                GD.Print("Playable Run Main scene already matches the canonical PackedScene contract.");
                Quit();
                return;
            }
            var root = new TacticsMigrationRoot { Name = "TacticsMigrationRoot" };
            var packed = new PackedScene();
            Error pack = packed.Pack(root);
            if (pack != Error.Ok) throw new InvalidOperationException($"Cannot pack Main scene: {pack}.");
            Error save = ResourceSaver.Save(packed, MainScenePath);
            if (save != Error.Ok) throw new InvalidOperationException($"Cannot save Main scene: {save}.");
            RequireUid();
            root.Free();
            GD.Print("Playable Run Main scene generated through PackedScene/ResourceSaver.");
            Quit();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            Quit(1);
        }
    }

    private static void SaveBalanceProfile()
    {
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://content/ui"));
        var profile = new PlayableLv1BalanceProfileResource
        {
            SkillContentIds = new[] { "skill.mage.fireball.lv1", "skill.mage.ice-bolt.lv1", "skill.mage.lightning.lv1", "skill.necromancer.summon-skeleton.lv1", "skill.necromancer.amplify-damage.lv1", "skill.necromancer.bone-spear.lv1", "skill.amazon.thrust.lv1", "skill.poison-spear.lv1", "skill.basic.magic", "skill.basic.melee" },
            SkillManaCosts = new[] { 5, 4, 5, 3, 3, 5, 2, 5, 0, 0 },
            SkillDamages = new[] { 6, 8, 10, 0, 0, 8, 6, 9, 0, 0 },
            UnitContentIds = new[] { "unit.pure-run.mage", "unit.pure-run.necromancer", "unit.pure-run.amazon", "unit.pure-run.skeleton-warrior" },
            UnitPhysicalAttacks = new[] { 2, 2, 5, 4 },
            UnitMagicalAttacks = new[] { 6, 5, 2, 0 }
        };
        profile.ToCoreProfile();
        Error save = ResourceSaver.Save(profile, BalancePath);
        if (save != Error.Ok) throw new InvalidOperationException($"Cannot save playable balance profile: {save}.");
    }

    private static void SaveEnemySpeedProfile()
    {
        var profile = new PlayableEnemySpeedProfileResource
        {
            UnitContentIds =
            [
                "unit.pure-run.goat-ranged", "unit.pure-run.goat-charger", "unit.pure-run.goat-support",
                "unit.pure-run.goat-aoe", "unit.pure-run.goat-elite-charger", "unit.pure-run.goat-elite-poison-caster"
            ],
            Speeds = [6f, 6f, 5f, 5f, 7f, 6f]
        };
        profile.ToCoreProfile();
        Error save = ResourceSaver.Save(profile, EnemySpeedPath);
        if (save != Error.Ok) throw new InvalidOperationException($"Cannot save enemy speed profile: {save}.");
    }

    private static void RequireUid()
    {
        Error error = ResourceSaver.SetUid(MainScenePath, ResourceUid.TextToId(MainSceneUid));
        if (error != Error.Ok) throw new InvalidOperationException($"Cannot preserve Main scene UID: {error}.");
    }
}
