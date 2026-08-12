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
        var batch=ResourceLoader.Load<GodotResourceCatalog>("res://content/ai_encounters/ContentCatalog.tres");var global=ResourceLoader.Load<GodotResourceCatalog>("res://content/ContentCatalog.tres");AssertThat(batch).IsNotNull();AssertThat(global).IsNotNull();if(batch is null||global is null)return;AiEncounterBatchValidation result=AiEncounterBatchValidator.Validate(batch,global);AssertThat(result.BatchCount).IsEqual(15);AssertThat(result.GlobalCount).IsEqual(74);AssertThat(result.Skills).IsEqual(4);AssertThat(result.Ai).IsEqual(6);AssertThat(result.Layouts).IsEqual(2);AssertThat(result.Encounters).IsEqual(3);
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
        AiFixtureTurnResult result = fixture.ExecuteSingleTurn();
        AssertThat(result.ActorId).IsEqual("fixture.enemy.0");
        AssertThat(result.GlobalStep).IsEqual(1);
        AssertThat(result.CandidateCount).IsGreater(0);
        fixture.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void FixtureLoadsEverySkillAndAiAcrossRepeatedInitialization()
    {
        for (int iteration = 0; iteration < 3; iteration++)
        {
            var fixture = new GodotAiEncounterFixture();
            fixture._Ready();
            AssertThat(fixture.ExecuteSingleTurn().CandidateCount).IsGreater(0);
            fixture.Free();
        }
    }

    [TestCase(0, 3)]
    [TestCase(1, 3)]
    [TestCase(2, 4)]
    [TestCase(3, 1)]
    [TestCase(4, 1)]
    [RequireGodotRuntime]
    public void AutoRoundExecutesEveryAiActorExactlyOnce(int scenario, int expectedTurns)
    {
        var fixture = new GodotAiEncounterFixture();
        fixture._Ready();
        fixture.SelectScenario(scenario);
        AiFixtureRoundResult result = fixture.ExecuteCurrentRound();
        AssertThat(result.Turns.Count).IsEqual(expectedTurns);
        AssertThat(result.RoundAfter).IsEqual(result.RoundBefore + 1);
        AssertThat(result.HitCommandLimit).IsFalse();
        fixture.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ResetReplaysTheSameRoundAndEliteSingleMatchesRound()
    {
        var fixture = new GodotAiEncounterFixture();
        fixture._Ready();
        AiFixtureRoundResult first = fixture.ExecuteCurrentRound();
        fixture.ResetCurrentScenario();
        AiFixtureRoundResult replay = fixture.ExecuteCurrentRound();
        AssertThat(replay.StateFingerprint).IsEqual(first.StateFingerprint);
        AssertThat(string.Join('|', replay.Turns.Select(value => value.Events))).IsEqual(string.Join('|', first.Turns.Select(value => value.Events)));

        fixture.SelectScenario(3);
        AiFixtureTurnResult eliteSingle = fixture.ExecuteSingleTurn();
        fixture.ResetCurrentScenario();
        AiFixtureRoundResult eliteRound = fixture.ExecuteCurrentRound();
        AssertThat(eliteRound.Turns.Count).IsEqual(1);
        AssertThat(eliteRound.StateFingerprint).IsEqual(eliteSingle.StateFingerprint);
        fixture.Free();
    }
}
