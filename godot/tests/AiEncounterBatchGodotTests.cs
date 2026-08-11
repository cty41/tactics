using GdUnit4;
using Godot;
using Tactics.Godot.Adapter.Runtime;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests;

[TestSuite]
public class AiEncounterBatchGodotTests
{
    [TestCase]
    [RequireGodotRuntime]
    public void GeneratedBatchBuildsCanonical73Catalog()
    {
        var batch=ResourceLoader.Load<GodotResourceCatalog>("res://content/ai_encounters/ContentCatalog.tres");var global=ResourceLoader.Load<GodotResourceCatalog>("res://content/ContentCatalog.tres");AssertThat(batch).IsNotNull();AssertThat(global).IsNotNull();if(batch is null||global is null)return;AiEncounterBatchValidation result=AiEncounterBatchValidator.Validate(batch,global);AssertThat(result.BatchCount).IsEqual(15);AssertThat(result.GlobalCount).IsEqual(73);AssertThat(result.Skills).IsEqual(4);AssertThat(result.Ai).IsEqual(6);AssertThat(result.Layouts).IsEqual(2);AssertThat(result.Encounters).IsEqual(3);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void FixtureUsesNative1600x900Contract()
    {
        AssertThat(GodotAiEncounterFixture.CanvasWidth).IsEqual(1600);AssertThat(GodotAiEncounterFixture.CanvasHeight).IsEqual(900);var scene=ResourceLoader.Load<PackedScene>("res://content/ai_encounters/AiEncounterFixture.tscn");AssertThat(scene).IsNotNull();Node? instance=scene?.Instantiate();AssertThat(instance).IsInstanceOf<GodotAiEncounterFixture>();instance?.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void FixtureExecutesRealDeterministicAiTurn()
    {
        var fixture = new GodotAiEncounterFixture();
        fixture._Ready();
        string result = fixture.ExecuteStep();
        AssertThat(result).Contains("actor=fixture.enemy.0");
        AssertThat(result).Contains("selected=");
        AssertThat(result).Contains("Commands/events:");
        fixture.Free();
    }
}
