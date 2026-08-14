using System.Text.Json;
using GdUnit4;
using Godot;
using Tactics.Application.Runs;
using Tactics.Core.Runs;
using Tactics.Godot.Adapter.Runtime;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests.GameplaySpec;

[TestSuite]
public class GodotGameplayRuntimeRunnerTests
{
    [TestCase]
    [RequireGodotRuntime]
    public async Task MainSceneReceivesIsolatedStoreAndProductionQuitInput()
    {
        var assertion = new GodotGameplayPlanAssertion("runtimeHasNoErrors", "UI", null, Json(true), []);
        var plan = new GodotGameplayScenarioPlan(2, "Godot", "Runner.MainQuit", ["PlayerInput", "UI"],
            ["setup:initializePlayerInput", "action:clickPointerTarget", "assertion:runtimeHasNoErrors"],
            [new GodotGameplayPlanStep("initializePlayerInput", "PlayerInput", null, [])],
            [new GodotGameplayPlanStep("clickPointerTarget", "PlayerInput", "Quit", [])],
            [assertion], [new GodotGameplayProbeRequest(assertion.Kind, assertion.Adapter, assertion.Target, assertion.Parameters)],
            new GodotGameplaySaveIsolation("user://qa-runner", true),
            new GodotGameplayWatchdog(30000, 80, 300000, 4), null);

        GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(plan);

        AssertThat(result.ErrorCode).IsNull();
        AssertThat(result.Succeeded).IsTrue();
        AssertThat(result.ProductionSaveUnchanged).IsTrue();
        AssertThat(result.Trace.Count).IsEqual(3);
        AssertThat(result.RemainingTemporaryNodes).IsEqual(0);
    }

    [TestCase]
    public void PlanParserRejectsUnityOrUnknownSchema()
    {
        AssertThrown(() => GodotGameplayScenarioPlan.Parse("{\"schemaVersion\":1,\"runtime\":\"Unity\"}"))
            .IsInstanceOf<InvalidDataException>();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void PlanParserAcceptsTheTypeScriptCompiledGodotFixture()
    {
        string path = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "Tests",
            "gameplay-specs", "godot", "runner-home-quit.plan.json"));
        GodotGameplayScenarioPlan plan = GodotGameplayScenarioPlan.Parse(File.ReadAllText(path));

        AssertThat(plan.Runtime).IsEqual("Godot");
        AssertThat(plan.ScenarioName).IsEqual("GodotGameplayRunner.RunnerHomeQuit");
        AssertThat(plan.RequiredCapabilities.Length).IsEqual(4);
    }

    [TestCase]
    public void PlanContractRejectsCapabilityAdapterAndProbeTampering()
    {
        GodotGameplayScenarioPlan canonical = HomeQuitPlan("Contract.Canonical");
        canonical.ValidateContract();

        AssertThrown(() => (canonical with { RequiredCapabilities = ["action:clickPointerTarget"] }).ValidateContract())
            .IsInstanceOf<InvalidDataException>();
        AssertThrown(() => (canonical with { RequiredAdapters = ["PlayerInput"] }).ValidateContract())
            .IsInstanceOf<InvalidDataException>();
        AssertThrown(() => (canonical with
            {
                ProbeRequests = [new GodotGameplayProbeRequest("runtimeStateHashEquals", "UI", null, [])]
            }).ValidateContract()).IsInstanceOf<InvalidDataException>();
    }

    [TestCase]
    public async Task CheckpointMismatchIsRejectedBeforeSceneExecution()
    {
        ValidatedGodotRunCheckpoint actual = ValidatedGodotRunCheckpoint.Create("checkpoint-a", "validated://checkpoint-a",
            new PureRunSaveSnapshot(0, null, null));
        GodotGameplayScenarioPlan plan = CheckpointPlan(actual) with
        {
            Checkpoint = new GodotGameplayCheckpoint("checkpoint-a", "validated_checkpoint", new string('0', 64), "fixture")
        };
        try
        {
            await new GodotGameplayRuntimeRunner().ExecuteAsync(plan, actual);
            AssertThat(false).IsTrue();
        }
        catch (InvalidDataException exception)
        {
            AssertThat(exception.Message).Contains("does not match");
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task ValidatedCheckpointRunsInIsolationAndCleansTheScene()
    {
        ValidatedGodotRunCheckpoint checkpoint = ValidatedGodotRunCheckpoint.Create("empty-v5", "validated://empty-v5",
            new PureRunSaveSnapshot(0, null, null));

        GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(
            CheckpointPlan(checkpoint), checkpoint);

        AssertThat(result.Succeeded).IsTrue();
        AssertThat(result.ProductionSaveUnchanged).IsTrue();
        AssertThat(result.RemainingTemporaryNodes).IsEqual(0);
        AssertThat(result.Trace.Count).IsEqual(3);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task FailedPointerActionWritesTraceAndStillCleansTheScene()
    {
        var step = new GodotGameplayPlanStep("clickPointerTarget", "PlayerInput", "missing-control",
            Parameters(("targetKind", "UiElement")));
        GodotGameplayScenarioPlan plan = Plan("Runner.BadPointer", [], [step], []);

        GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(plan);

        AssertThat(result.Succeeded).IsFalse();
        AssertThat(result.FailureKind).IsEqual(GodotGameplayFailureKind.Action);
        AssertThat(result.Trace.Count).IsEqual(1);
        AssertThat(result.Trace[0].Succeeded).IsFalse();
        AssertThat(result.RemainingTemporaryNodes).IsEqual(0);
        AssertThat(result.ProductionSaveUnchanged).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task MainRestartReusesTheIsolatedStoreAndProductionInput()
    {
        var restart = new GodotGameplayPlanStep("restartGodotMain", "UI", null, []);
        var quit = new GodotGameplayPlanStep("clickPointerTarget", "PlayerInput", "Quit",
            Parameters(("targetKind", "UiElement")));
        var assertion = BoolAssertion("runtimeHasNoErrors", "UI", true);
        GodotGameplayScenarioPlan plan = Plan("Runner.Restart", [], [restart, quit], [assertion]);

        GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(plan);

        AssertThat(result.Succeeded).IsTrue();
        AssertThat(result.Trace.Count).IsEqual(3);
        AssertThat(result.RemainingTemporaryNodes).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task ObservableFrameLimitProducesNoProgressTrace()
    {
        var wait = new GodotGameplayPlanStep("waitForPlayerObservable", "PlayerInput", null,
            Parameters(("observable", "battleReady"), ("maximumFrames", 1)));
        GodotGameplayScenarioPlan plan = Plan("Runner.NoProgress", [], [wait], []);

        GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(plan);

        AssertThat(result.Succeeded).IsFalse();
        AssertThat(result.FailureKind).IsEqual(GodotGameplayFailureKind.NoProgress);
        AssertThat(result.Trace.Count).IsEqual(1);
        AssertThat(result.Trace[0].Diagnostic).Contains("observable_not_reached");
        AssertThat(result.RemainingTemporaryNodes).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task PauseSpeedAndRightClickUseTheirRequestedProductionSemantics()
    {
        ValidatedGodotRunCheckpoint checkpoint = BattleCheckpoint("battle-controls");
        var pause = new GodotGameplayPlanStep("setPresentationPaused", "UI", null,
            Parameters(("paused", true)));
        var speed = new GodotGameplayPlanStep("setPresentationSpeed", "UI", null,
            Parameters(("speed", 1f)));
        var rightClick = new GodotGameplayPlanStep("rightClickPointerTarget", "PlayerInput", "CurrentPlayer",
            Parameters(("targetKind", "BattleUnit")));
        var resume = new GodotGameplayPlanStep("setPresentationPaused", "UI", null,
            Parameters(("paused", false)));
        GodotGameplayScenarioPlan plan = Plan("Runner.BattleControls",
            [new GodotGameplayPlanStep("loadValidatedCheckpoint", "Map", null, [])],
            [pause, speed, rightClick, resume], [BoolAssertion("productionSaveUnchanged", "Map", true)]) with
        {
            Checkpoint = new GodotGameplayCheckpoint(checkpoint.Id, "validated_checkpoint", checkpoint.SemanticHash, checkpoint.Path)
        };

        GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(plan, checkpoint);

        AssertThat(result.ErrorCode).IsNull();
        AssertThat(result.Succeeded).IsTrue();
        AssertThat(result.Trace.Count).IsEqual(6);
        AssertThat(result.RemainingTemporaryNodes).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task PlayBattleThroughInputHonorsMaximumActionsAndUsesBattleUi()
    {
        ValidatedGodotRunCheckpoint checkpoint = BattleCheckpoint("battle-action-limit");
        var play = new GodotGameplayPlanStep("playBattleThroughInput", "PlayerInput", null,
            Parameters(("maximumActions", 1)));
        GodotGameplayScenarioPlan plan = Plan("Runner.BattleActionLimit",
            [new GodotGameplayPlanStep("loadValidatedCheckpoint", "Map", null, [])], [play], []) with
        {
            Checkpoint = new GodotGameplayCheckpoint(checkpoint.Id, "validated_checkpoint", checkpoint.SemanticHash, checkpoint.Path)
        };

        GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(plan, checkpoint);

        AssertThat(result.Succeeded).IsFalse();
        AssertThat(result.ErrorCode).IsEqual("battle_action_limit");
        AssertThat(result.FailureKind).IsEqual(GodotGameplayFailureKind.NoProgress);
        AssertThat(result.Trace.Last().Succeeded).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task StepAndScenarioTimeoutsAreClassifiedSeparately()
    {
        var wait = new GodotGameplayPlanStep("waitForPlayerObservable", "PlayerInput", null,
            Parameters(("observable", "battleReady"), ("maximumFrames", 100000)));
        GodotGameplayScenarioPlan stepPlan = Plan("Runner.StepTimeout", [], [wait], []) with
        {
            Watchdog = new GodotGameplayWatchdog(20, 80, 300000, 4)
        };
        GodotGameplayScenarioPlan scenarioPlan = stepPlan with
        {
            ScenarioName = "Runner.ScenarioTimeout",
            Watchdog = new GodotGameplayWatchdog(30000, 80, 20, 4)
        };

        GodotGameplayScenarioResult step = await new GodotGameplayRuntimeRunner().ExecuteAsync(stepPlan);
        GodotGameplayScenarioResult scenario = await new GodotGameplayRuntimeRunner().ExecuteAsync(scenarioPlan);

        AssertThat(step.ErrorCode).IsEqual("step.timeout:waitForPlayerObservable");
        AssertThat(step.Trace.Single().Succeeded).IsFalse();
        AssertThat(scenario.ErrorCode).IsEqual("scenario.timeout");
        AssertThat(scenario.Trace.Single().Succeeded).IsFalse();
    }

    private static GodotGameplayScenarioPlan HomeQuitPlan(string name)
    {
        var setup = new GodotGameplayPlanStep("initializePlayerInput", "PlayerInput", null, []);
        var quit = new GodotGameplayPlanStep("clickPointerTarget", "PlayerInput", "Quit",
            Parameters(("targetKind", "UiElement")));
        return Plan(name, [setup], [quit], [BoolAssertion("runtimeHasNoErrors", "UI", true)]);
    }

    private static GodotGameplayScenarioPlan CheckpointPlan(ValidatedGodotRunCheckpoint checkpoint)
    {
        var load = new GodotGameplayPlanStep("loadValidatedCheckpoint", "Map", null, []);
        var quit = new GodotGameplayPlanStep("clickPointerTarget", "PlayerInput", "Quit",
            Parameters(("targetKind", "UiElement")));
        return Plan("Runner.Checkpoint", [load], [quit], [BoolAssertion("productionSaveUnchanged", "Map", true)]) with
        {
            Checkpoint = new GodotGameplayCheckpoint(checkpoint.Id, "validated_checkpoint", checkpoint.SemanticHash, checkpoint.Path)
        };
    }

    private static ValidatedGodotRunCheckpoint BattleCheckpoint(string id)
    {
        PureRunDefinitionResource resource = ResourceLoader.Load<PureRunDefinitionResource>(
            "res://content/runs/PureRunThreeEncounterV1.tres", string.Empty, ResourceLoader.CacheMode.Ignore)!;
        PureRunDefinition definition = resource.ToCoreDefinition();
        var store = new MemoryRunStore();
        var service = new PureRunSessionService(definition, store);
        AssertThat(service.BeginNewRunSetup(7).Succeeded).IsTrue();
        foreach (PureRunPartyTemplate member in definition.Party)
            AssertThat(service.ChooseStartingSkill(member.CharacterId, member.StartingSkillContentId).Succeeded).IsTrue();
        AssertThat(service.BeginEncounter().Succeeded).IsTrue();
        return ValidatedGodotRunCheckpoint.Create(id, "validated://" + id, store.Snapshot!);
    }

    private static GodotGameplayScenarioPlan Plan(string name, GodotGameplayPlanStep[] setup,
        GodotGameplayPlanStep[] actions, GodotGameplayPlanAssertion[] assertions)
    {
        string[] adapters = setup.Concat(actions).Select(step => step.Adapter)
            .Concat(assertions.Select(assertion => assertion.Adapter)).Distinct().Order().ToArray();
        string[] capabilities = setup.Select(step => "setup:" + step.Kind)
            .Concat(actions.Select(step => "action:" + step.Kind))
            .Concat(assertions.Select(assertion => "assertion:" + assertion.Kind)).Distinct().Order().ToArray();
        GodotGameplayProbeRequest[] probes = assertions.Select(assertion =>
            new GodotGameplayProbeRequest(assertion.Kind, assertion.Adapter, assertion.Target, assertion.Parameters)).ToArray();
        return new GodotGameplayScenarioPlan(2, "Godot", name, adapters, capabilities, setup, actions,
            assertions, probes, new GodotGameplaySaveIsolation("user://qa-runner", true),
            new GodotGameplayWatchdog(30000, 80, 300000, 4), null);
    }

    private static GodotGameplayPlanAssertion BoolAssertion(string kind, string adapter, bool value) =>
        new(kind, adapter, null, Json(value), []);

    private static Dictionary<string, JsonElement> Parameters(params (string Key, object Value)[] values) =>
        values.ToDictionary(value => value.Key, value => JsonSerializer.SerializeToElement(value.Value), StringComparer.Ordinal);

    private static JsonElement Json(bool value) => JsonDocument.Parse(value ? "true" : "false").RootElement.Clone();

    private sealed class MemoryRunStore : IRunSaveStore
    {
        public PureRunSaveSnapshot? Snapshot { get; private set; } = new(0, null, null);
        public RunStoreResult Load() => new(true, null, Snapshot);
        public RunStoreResult Save(PureRunSaveSnapshot snapshot, long expectedRevision)
        {
            if (Snapshot?.Revision != expectedRevision) return new RunStoreResult(false, "save.stale_revision", Snapshot);
            Snapshot = snapshot;
            return new RunStoreResult(true, null, Snapshot);
        }
    }

}
