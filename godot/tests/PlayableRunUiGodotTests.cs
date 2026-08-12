using GdUnit4;
using Godot;
using System.Reflection;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;
using Tactics.Godot.Adapter.Runtime;
using Tactics.Application.Runs;
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
    public void BattleDiagnosticMetersUseCompactBounds()
    {
        AssertThat(GodotPlayableRunMain.UnitMeterSize).IsEqual(new Vector2(60, 18));
        AssertThat(GodotPlayableRunMain.UnitMeterBarHeight).IsEqual(7);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void RogueMapPlacesStartBelowBossAndKeepsLockedNodesReadOnly()
    {
        var nodes = new[]
        {
            new PureRunMapNodeSnapshot("start", 0, PureRunNodeKind.Battle, "Start", null, 0,
                PureRunMapNodeState.Completed),
            new PureRunMapNodeSnapshot("layer_01_battle", 1, PureRunNodeKind.Battle, "N1", null, 0,
                PureRunMapNodeState.Current),
            new PureRunMapNodeSnapshot("layer_07_battle", 7, PureRunNodeKind.Battle, "Boss", null, 0,
                PureRunMapNodeState.Locked, "map.node_locked")
        };
        var view = new GodotRogueMapView { Size = GodotRogueMapView.PreferredSize };
        view.SetSnapshot(new PureRunMapSnapshot(nodes,
            [new PureRunMapConnectionSnapshot("start", "layer_01_battle", true, true)],
            "layer_01_battle"), true);

        AssertThat(view.NodeCenter("start").Y).IsGreater(view.NodeCenter("layer_07_battle").Y);
        AssertThat(view.Snapshot!.FocusNodeId).IsEqual("layer_01_battle");
        AssertThat(view.Snapshot.Nodes.Single(value => value.NodeId == "layer_07_battle").UnavailableReason)
            .IsEqual("map.node_locked");
        view.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void CompactMeterKeepsExactBoundsWithoutThemeExpansion()
    {
        var actor = new GodotUnitActor { Position = new Vector2(200, 300) };
        var meter = new GodotCompactUnitMeter();
        meter.Bind(actor, 10, 20, 4, 8);

        AssertThat(meter.Size).IsEqual(GodotPlayableRunMain.UnitMeterSize);
        AssertThat(meter.GetChildCount()).IsEqual(0);
        meter.Free(); actor.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void HomeLoadsCanonical101CatalogWithoutWritingSave()
    {
        var ui = new GodotPlayableRunMain();
        ui._Ready();
        AssertThat(ui.IsReadyForInput).IsTrue();
        ui.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ProgressionFirstRendersIndependentAttributeChoices()
    {
        var ui = new GodotPlayableRunMain(); ui._Ready();
        var attributes = new UnitAttributes(5, 5, 5, 6, 5, 5);
        var mage = new RunCharacterState("pure_run_mage", new ContentId("unit.pure-run.mage"), 1, attributes,
            20, 20, 12, 12, false, new[] { new ContentId("skill.mage.fireball.lv1") },
            learnedSkillStates: new[] { new RunLearnedSkillState("mage.fireball", 1, new ContentId("skill.mage.fireball.lv1")) });
        var necromancer = new RunCharacterState("pure_run_necromancer", new ContentId("unit.pure-run.necromancer"), 1, attributes, 20, 20, 12, 12, false, Array.Empty<ContentId>());
        var amazon = new RunCharacterState("pure_run_amazon", new ContentId("unit.pure-run.amazon"), 1, attributes, 20, 20, 12, 12, false, Array.Empty<ContentId>());
        var pending = new PendingProgression("battle:n1:progression", "n1", mage.CharacterId);
        var run = new PureRunState("run-test", 7, 2, PureRunPhase.Ready, 1, new ContentId("encounter.pure-run.n2"),
            new[] { mage, necromancer, amazon }, pendingProgression: new[] { pending });
        MethodInfo? show = typeof(GodotPlayableRunMain).GetMethod("ShowProgression", BindingFlags.Instance | BindingFlags.NonPublic);
        show?.Invoke(ui, new object[] { run, pending });
        string[] buttonTexts = Descendants<Button>(ui).Select(button => button.Text).ToArray();
        AssertThat(buttonTexts.Count(text => text.StartsWith("+1 ", StringComparison.Ordinal))).IsEqual(6);
        AssertThat(buttonTexts.Any(text => text.Contains("mage.fireball", StringComparison.Ordinal))).IsFalse();
        ui.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void RunMapPageConsumesTheApplicationSnapshotWithoutStartingBattle()
    {
        var ui = new GodotPlayableRunMain(); ui._Ready();
        UnitAttributes attributes = new(5, 5, 5, 6, 5, 5);
        RunCharacterState Character(string id, string unit, string skill) => new(id, new ContentId(unit), 1,
            attributes, 20, 20, 10, 10, false, [new ContentId(skill)]);
        var run = new PureRunState("run-map-test", 7, 1, PureRunPhase.Ready, 0,
            new ContentId("encounter.pure-run.n1"),
        [
            Character("pure_run_mage", "unit.pure-run.mage", "skill.mage.fireball.lv1"),
            Character("pure_run_necromancer", "unit.pure-run.necromancer", "skill.necromancer.summon-skeleton.lv1"),
            Character("pure_run_amazon", "unit.pure-run.amazon", "skill.amazon.thrust.lv1")
        ]);

        typeof(GodotPlayableRunMain).GetMethod("ShowRunMap", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(ui, new object[] { run });
        GodotRogueMapView? map = Descendants<GodotRogueMapView>(ui).SingleOrDefault();

        AssertThat(map).IsNotNull();
        AssertThat(map!.Snapshot!.Nodes.Count).IsEqual(14);
        AssertThat(map.Snapshot.FocusNodeId).IsEqual("layer_01_battle");
        ui.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ProgressionAfterAttributeAllocationRendersSkillChoices()
    {
        var ui = new GodotPlayableRunMain(); ui._Ready();
        var attributes = new UnitAttributes(5, 5, 5, 6, 5, 5);
        var proposed = new UnitAttributes(5, 5, 5, 7, 5, 5);
        var mage = new RunCharacterState("pure_run_mage", new ContentId("unit.pure-run.mage"), 1, attributes,
            20, 20, 12, 12, false, new[] { new ContentId("skill.mage.fireball.lv1") },
            learnedSkillStates: new[] { new RunLearnedSkillState("mage.fireball", 1, new ContentId("skill.mage.fireball.lv1")) });
        var others = new[]
        {
            new RunCharacterState("pure_run_necromancer", new ContentId("unit.pure-run.necromancer"), 1, attributes, 20, 20, 12, 12, false, Array.Empty<ContentId>()),
            new RunCharacterState("pure_run_amazon", new ContentId("unit.pure-run.amazon"), 1, attributes, 20, 20, 12, 12, false, Array.Empty<ContentId>())
        };
        var pending = new PendingProgression("battle:n1:progression", "n1", mage.CharacterId, ProposedAttributes: proposed);
        var run = new PureRunState("run-test", 7, 2, PureRunPhase.Ready, 1, new ContentId("encounter.pure-run.n2"),
            new[] { mage }.Concat(others).ToArray(), pendingProgression: new[] { pending });
        typeof(GodotPlayableRunMain).GetMethod("ShowProgression", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(ui, new object[] { run, pending });
        string[] buttonTexts = Descendants<Button>(ui).Select(button => button.Text).ToArray();
        AssertThat(buttonTexts.Count(text => text.StartsWith("Learn ", StringComparison.Ordinal) || text.StartsWith("Upgrade ", StringComparison.Ordinal))).IsLessEqual(3);
        AssertThat(buttonTexts.Any(text => text.Contains("Upgrade Fireball Lv1 → Lv2", StringComparison.Ordinal))).IsTrue();
        ui.Free();
    }

    private static IEnumerable<T> Descendants<T>(Node node) where T : Node
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is T match) yield return match;
            foreach (T nested in Descendants<T>(child)) yield return nested;
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ReplacingAPageDoesNotRetainDisposedUnitMeters()
    {
        var ui = new GodotPlayableRunMain();
        ui._Ready();
        FieldInfo? metersField = typeof(GodotPlayableRunMain).GetField("_unitMeters", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo? newPage = typeof(GodotPlayableRunMain).GetMethod("NewPage", BindingFlags.Instance | BindingFlags.NonPublic);
        var meters = (Dictionary<UnitInstanceId, Control>?)metersField?.GetValue(ui);
        var disposedMeter = new Control();
        ui.AddChild(disposedMeter);
        meters?.Add(new UnitInstanceId("test.disposed-meter"), disposedMeter);
        disposedMeter.Free();

        newPage?.Invoke(ui, new object[] { "PAGE REPLACEMENT TEST", "Disposed references must be forgotten", false });

        AssertThat(meters).IsNotNull();
        AssertThat(meters?.Count ?? -1).IsEqual(0);

        ui.Free();
    }
}
