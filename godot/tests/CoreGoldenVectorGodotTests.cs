using GdUnit4;
using Tactics.Core.Board;
using Tactics.Core.Content;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests;

[TestSuite]
public class CoreGoldenVectorGodotTests
{
    [TestCase]
    public void CoreCanBeConsumedWithoutGodotRuntime()
    {
        var unit = new UnitState(new ContentId("unit.godot.test"), new GridPoint(0, 0), 3, 1);
        AssertThat(unit.Position).IsEqual(new GridPoint(0, 0));
        AssertThat(BoardSpec.Contains(unit.Position)).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GodotRuntimeMarkerIsExplicit()
    {
        var node = new global::Godot.Node();
        AssertThat(node).IsNotNull();
        node.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void PoisonSpearCatalogCompilesToCorePlanAndScenes()
    {
        var catalog = global::Godot.ResourceLoader.Load<Tactics.Godot.Adapter.Runtime.GodotResourceCatalog>(
            "res://content/poison_spear/ContentCatalog.tres");
        AssertThat(catalog).IsNotNull();
        if (catalog is null)
            return;

        var validation = Tactics.Godot.Adapter.Runtime.PoisonSpearSliceValidator.Validate(catalog);
        AssertThat(validation.CatalogEntryCount).IsEqual(5);
        AssertThat(validation.Action.Succeeded).IsTrue();
        AssertThat(validation.Action.Damage).IsEqual(8);
        AssertThat(validation.Action.PoisonTurns).IsEqual(3);
        AssertThat(validation.Presentation.RootNodeId).IsEqual("poison-spear.sequence");
        AssertThat(validation.Presentation.Nodes.Count).IsEqual(3);
    }
}
