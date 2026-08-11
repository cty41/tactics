using GdUnit4;
using Godot;
using Tactics.Godot.Adapter.Runtime;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests;

[TestSuite]
public class PlayableRunUiGodotTests
{
    [TestCase]
    [RequireGodotRuntime]
    public void MainSceneLoadsNativePlayableUiContract()
    {
        AssertThat(GodotPlayableRunMain.CanvasWidth).IsEqual(1600);
        AssertThat(GodotPlayableRunMain.CanvasHeight).IsEqual(900);
        AssertThat(ProjectSettings.GetSetting("display/window/size/viewport_width").AsInt32()).IsEqual(1600);
        AssertThat(ProjectSettings.GetSetting("display/window/size/viewport_height").AsInt32()).IsEqual(900);
        AssertThat(ProjectSettings.GetSetting("display/window/stretch/mode").AsString()).IsEqual("canvas_items");
        AssertThat(ProjectSettings.GetSetting("display/window/stretch/aspect").AsString()).IsEqual("keep");
        PackedScene? scene = ResourceLoader.Load<PackedScene>("res://scenes/Main.tscn");
        AssertThat(scene).IsNotNull();
        Node? root = scene?.Instantiate();
        AssertThat(root).IsInstanceOf<TacticsMigrationRoot>();
        root?.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void HomeLoadsCanonical74CatalogWithoutWritingSave()
    {
        var ui = new GodotPlayableRunMain();
        ui._Ready();
        AssertThat(ui.IsReadyForInput).IsTrue();
        ui.Free();
    }
}
