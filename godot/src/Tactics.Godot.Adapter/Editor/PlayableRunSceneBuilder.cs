using Godot;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

/// <summary>Generates the canonical main PackedScene through Godot APIs.</summary>
[Tool]
public partial class PlayableRunSceneBuilder : SceneTree
{
    private const string MainScenePath = "res://scenes/Main.tscn";
    private const string MainSceneUid = "uid://c0mlqoh7vensn";

    public override void _Initialize()
    {
        try
        {
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

    private static void RequireUid()
    {
        Error error = ResourceSaver.SetUid(MainScenePath, ResourceUid.TextToId(MainSceneUid));
        if (error != Error.Ok) throw new InvalidOperationException($"Cannot preserve Main scene UID: {error}.");
    }
}
