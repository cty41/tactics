using GdUnit4;
using Godot;
using Tactics.Godot.Adapter.Editor;
using Tactics.Core.Runs;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests;

[TestSuite]
public class EditorReloadLifecycleGodotTests
{
    [TestCase]
    [RequireGodotRuntime]
    public void BridgeShutdownStopsItsServerAndDeletesItsDescriptor()
    {
        var bridge = new TacticsAuthoringEditorBridge();
        bridge.ConfigureForLifecycleTest();
        bridge._EnterTree();
        string descriptor = Path.Combine(ProjectSettings.GlobalizePath("res://.godot"),
            $"tactics-authoring-session-{System.Environment.ProcessId}.json");

        AssertThat(File.Exists(descriptor)).IsTrue();
        AuthoringBridgeShutdownResult first = bridge.ShutdownForReload(TimeSpan.FromSeconds(2));
        AuthoringBridgeShutdownResult second = bridge.ShutdownForReload(TimeSpan.FromSeconds(2));

        AssertThat(first.Completed).IsTrue();
        AssertThat(second.Completed).IsTrue();
        AssertThat(File.Exists(descriptor)).IsFalse();
        bridge.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void WorkbenchShutdownImmediatelyFreesItsControlTree()
    {
        var workbench = new TacticsContentWorkbench();
        workbench.ConfigureForLifecycleTest();
        workbench._Ready();
        AssertThat(workbench.GetChildCount()).IsGreater(0);

        workbench.ShutdownForReload();
        workbench.ShutdownForReload();

        AssertThat(workbench.GetChildCount()).IsEqual(0);
        workbench.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void WorkspaceCoordinatorStopsWhenReloadHasNotRestoredPrivateControlFields()
    {
        var coordinator = new AuthoringWorkspaceCoordinator();
        coordinator.SimulateReloadFieldLossForTest();

        coordinator._Process(0);

        AssertThat(coordinator.IsProcessing()).IsFalse();
        coordinator.ShutdownForReload();
        coordinator.ShutdownForReload();
        coordinator.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void MapGraphUsesCompactUnityStyleSemanticNodes()
    {
        AssertThat((int)CircularMapGraphNode.Diameter).IsEqual(44);
        AssertThat(PureRunMapWorkbench.Glyph(PureRunNodeKind.Battle)).IsEqual("E");
        AssertThat(PureRunMapWorkbench.Glyph(PureRunNodeKind.Elite)).IsEqual("X");
        AssertThat(PureRunMapWorkbench.Glyph(PureRunNodeKind.Boss)).IsEqual("B");
        AssertThat(PureRunMapWorkbench.Glyph(PureRunNodeKind.Mystery)).IsEqual("?");

        var node = new CircularMapGraphNode();
        node.Configure("S", new Color("33b34d"), "start");
        AssertThat((int)node.CustomMinimumSize.X).IsEqual(44);
        AssertThat((int)node.CustomMinimumSize.Y).IsEqual(44);
        AssertThat(node.TooltipText).IsEqual("start");
        AssertThat(node.GetChildCount()).IsEqual(1);
        AssertThat(((Label)node.GetChild(0)).Text).IsEqual("S");
        node.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void WorkbenchThemeDerivesReadablePanelsFromEditorTextColor()
    {
        var control = new Control();
        WorkbenchThemeTokens tokens = WorkbenchThemeTokens.Resolve(control);
        AssertThat(tokens.Panel).IsNotEqual(tokens.Background);
        AssertThat(tokens.Selection).IsNotEqual(tokens.Error);
        AssertThat(tokens.Success).IsNotEqual(tokens.Warning);
        control.Free();
    }

    [TestCase]
    public void WorkbenchNavigationExposesOnlyTheThreeAuthoringSurfaces()
    {
        AssertThat(TacticsContentWorkbench.TopLevelTabNames.ToArray())
            .ContainsExactly("Map", "Event", "Skill / Presentation");
    }
}
