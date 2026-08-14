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
    public void ValidatedCheckpointCatalogProducesStableCanonicalV5Hashes()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["inventory-store-ready-v1"] = "70ff70d78706879dfe6168d4b3d8663eaeea084f5d8a5fcf2fa963661bf438a0",
            ["defeat-no-summon-v1"] = "855ba3ba5fc8cbeb5fe05073e94b6b20b84d32f8d917be505bd4569f41777a8a",
            ["numbers-mana-v1"] = "b1ab312c5e80aa63fc5ebddcceb21458784da6c9f46f10c29b2d7b32794e61f6",
            ["numbers-miss-v1"] = "ea583c2f9e509adfa426ad34dab653bda44496fc3426037d159c91a94bb7854a",
            ["reload-pending-battle-v1"] = "a7ef2a784163a5c8a58b5cbfeb4d90a7ab088b2e3055777260b7e72f196fc3b3"
        };
        foreach ((string id, string hash) in expected)
        {
            ValidatedGodotRunCheckpoint checkpoint = GodotGameplayCheckpointCatalog.Create(id);
            AssertThat(checkpoint.Verify()).IsTrue();
            AssertThat(checkpoint.SemanticHash).IsEqual(hash);
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task AcceptanceSpecsWriteStructuredBatchReport()
    {
        (string Plan, string Checkpoint)[] scenarios =
        [
            ("inventory-battle-projection", "inventory-store-ready-v1"),
            ("defeated-terminal", "defeat-no-summon-v1"),
            ("presentation-numbers", "numbers-mana-v1"),
            ("presentation-miss", "numbers-miss-v1"),
            ("reload-cleanup", "reload-pending-battle-v1")
        ];
        var executions = new List<GodotGameplayReportScenario>();
        foreach ((string planName, string _) in scenarios)
        {
            GodotGameplayScenarioPlan plan = LoadCompiledPlan(planName);
            GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(plan);
            executions.Add(GodotGameplayReportScenario.From(plan, result));
        }
        GodotGameplaySpecReport report = GodotGameplaySpecReport.Create(executions);
        string output = GodotGameplaySpecReportWriter.Write(report);

        AssertThat(File.Exists(output)).IsTrue();
        AssertThat(report.Scenarios.Count).IsEqual(5);
        AssertThat(report.Passed).IsEqual(5);
        AssertThat(report.Failed).IsEqual(0);
        AssertThat(report.Scenarios.Select(value => value.ScenarioName).Distinct().Count()).IsEqual(5);
        var expectedIdentities = scenarios.ToDictionary(value => LoadCompiledPlan(value.Plan).ScenarioName,
            value => value.Checkpoint, StringComparer.Ordinal);
        AssertThat(report.Scenarios.All(value => expectedIdentities.TryGetValue(value.ScenarioName, out string? checkpointId) &&
            checkpointId == value.CheckpointId)).IsTrue();
        AssertThat(report.Scenarios.Select(value => value.CheckpointId).Order().ToArray())
            .ContainsExactly(scenarios.Select(value => value.Checkpoint).Order().ToArray());
        AssertThat(report.Scenarios.All(value => value.ProductionSaveUnchanged &&
            value.ProductionSaveBefore == value.ProductionSaveAfter && value.RemainingTemporaryNodes == 0)).IsTrue();
    }

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

        if (!result.Succeeded)
            throw new InvalidOperationException($"Scenario {result.ScenarioName} failed: {result.ErrorCode}\n{string.Join(System.Environment.NewLine,
                result.Trace.Select(entry => $"{entry.Ordinal}:{entry.Phase}:{entry.Kind}:{entry.Succeeded}:{entry.Diagnostic}"))}");

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
        ValidatedGodotRunCheckpoint actual = GodotGameplayCheckpointCatalog.Create("reload-pending-battle-v1");
        GodotGameplayScenarioPlan plan = CheckpointPlan(actual) with
        {
            Checkpoint = new GodotGameplayCheckpoint(actual.Id, "validated_checkpoint", new string('0', 64), actual.Path)
        };
        try
        {
            await new GodotGameplayRuntimeRunner().ExecuteAsync(plan);
            AssertThat(false).IsTrue();
        }
        catch (InvalidDataException exception)
        {
            AssertThat(exception.Message).Contains("checkpoint metadata");
        }
    }

    [TestCase]
    public void PlanContractRejectsCheckpointSetupMetadataTampering()
    {
        ValidatedGodotRunCheckpoint checkpoint = GodotGameplayCheckpointCatalog.Create("reload-pending-battle-v1");
        GodotGameplayScenarioPlan canonical = CheckpointPlan(checkpoint);
        canonical.ValidateContract();
        GodotGameplayPlanStep tampered = canonical.SetupActions.Single(step => step.Kind == "loadValidatedCheckpoint") with
        {
            Parameters = Parameters(("id", "defeat-no-summon-v1"), ("path", checkpoint.Path),
                ("semanticHash", checkpoint.SemanticHash))
        };

        AssertThrown(() => (canonical with { SetupActions = [tampered] }).ValidateContract())
            .IsInstanceOf<InvalidDataException>();
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task ValidatedCheckpointRunsInIsolationAndCleansTheScene()
    {
        ValidatedGodotRunCheckpoint checkpoint = GodotGameplayCheckpointCatalog.Create("reload-pending-battle-v1");

        GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(
            CheckpointPlan(checkpoint));

        if (!result.Succeeded)
            throw new InvalidOperationException($"Scenario {result.ScenarioName} failed: {result.ErrorCode}\n{string.Join(System.Environment.NewLine,
                result.Trace.Select(entry => $"{entry.Ordinal}:{entry.Phase}:{entry.Kind}:{entry.Succeeded}:{entry.Diagnostic}"))}");

        AssertThat(result.Succeeded).IsTrue();
        AssertThat(result.ProductionSaveUnchanged).IsTrue();
        AssertThat(result.RemainingTemporaryNodes).IsEqual(0);
        AssertThat(result.Trace.Count).IsEqual(2);
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
        ValidatedGodotRunCheckpoint checkpoint = GodotGameplayCheckpointCatalog.Create("reload-pending-battle-v1");
        var pause = new GodotGameplayPlanStep("setPresentationPaused", "UI", null,
            Parameters(("paused", true)));
        var speed = new GodotGameplayPlanStep("setPresentationSpeed", "UI", null,
            Parameters(("speed", 1f)));
        var rightClick = new GodotGameplayPlanStep("rightClickPointerTarget", "PlayerInput", "CurrentPlayer",
            Parameters(("targetKind", "BattleUnit")));
        var resume = new GodotGameplayPlanStep("setPresentationPaused", "UI", null,
            Parameters(("paused", false)));
        GodotGameplayScenarioPlan plan = Plan("Runner.BattleControls",
            [LoadCheckpointStep(checkpoint)],
            [pause, speed, rightClick, resume], [BoolAssertion("productionSaveUnchanged", "Map", true)]) with
        {
            Checkpoint = new GodotGameplayCheckpoint(checkpoint.Id, "validated_checkpoint", checkpoint.SemanticHash, checkpoint.Path)
        };

        GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(plan);

        if (!result.Succeeded)
            Console.WriteLine($"Scenario {result.ScenarioName} failed: {result.ErrorCode}\n{string.Join(System.Environment.NewLine, result.Trace.Select(entry => $"{entry.Ordinal}:{entry.Phase}:{entry.Kind}:{entry.Succeeded}:{entry.Diagnostic}"))}");

        AssertThat(result.ErrorCode).IsNull();
        AssertThat(result.Succeeded).IsTrue();
        AssertThat(result.Trace.Count).IsEqual(6);
        AssertThat(result.RemainingTemporaryNodes).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task PlayBattleThroughInputHonorsMaximumActionsAndUsesBattleUi()
    {
        ValidatedGodotRunCheckpoint checkpoint = GodotGameplayCheckpointCatalog.Create("reload-pending-battle-v1");
        var play = new GodotGameplayPlanStep("playBattleThroughInput", "PlayerInput", null,
            Parameters(("maximumActions", 1)));
        GodotGameplayScenarioPlan plan = Plan("Runner.BattleActionLimit",
            [LoadCheckpointStep(checkpoint)], [play], []) with
        {
            Checkpoint = new GodotGameplayCheckpoint(checkpoint.Id, "validated_checkpoint", checkpoint.SemanticHash, checkpoint.Path)
        };

        GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(plan);

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

        AssertThat(step.ErrorCode).IsEqual("step.timeout:1:waitForPlayerObservable");
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

    private static GodotGameplayScenarioPlan LoadCompiledPlan(string planName)
    {
        string path = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "Tests",
            "gameplay-specs", "godot", planName + ".plan.json"));
        return GodotGameplayScenarioPlan.Parse(File.ReadAllText(path));
    }

    private static GodotGameplayScenarioPlan CheckpointPlan(ValidatedGodotRunCheckpoint checkpoint)
    {
        GodotGameplayPlanStep load = LoadCheckpointStep(checkpoint);
        return Plan("Runner.Checkpoint", [load], [], [BoolAssertion("productionSaveUnchanged", "Map", true)]) with
        {
            Checkpoint = new GodotGameplayCheckpoint(checkpoint.Id, "validated_checkpoint", checkpoint.SemanticHash, checkpoint.Path)
        };
    }

    private static GodotGameplayPlanStep LoadCheckpointStep(ValidatedGodotRunCheckpoint checkpoint) =>
        new("loadValidatedCheckpoint", "Map", null,
            Parameters(("id", checkpoint.Id), ("path", checkpoint.Path), ("semanticHash", checkpoint.SemanticHash)));

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

}
