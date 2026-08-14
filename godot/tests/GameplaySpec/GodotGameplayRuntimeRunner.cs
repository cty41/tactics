using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using Tactics.Application.Battle;
using Tactics.Application.Runs;
using Tactics.Godot.Adapter.Runtime;
using Tactics.Core.Board;

namespace Tactics.Godot.Tests.GameplaySpec;

public interface IGodotGameplayDecisionSource
{
    Task ExecuteAsync(GodotGameplayRuntimeContext context, CancellationToken cancellationToken);
}

public sealed class ScriptedDecisionSource(IEnumerable<string> buttonTexts) : IGodotGameplayDecisionSource
{
    public async Task ExecuteAsync(GodotGameplayRuntimeContext context, CancellationToken cancellationToken)
    {
        foreach (string text in buttonTexts) await context.ClickButtonAsync(text, cancellationToken);
    }
}

public sealed class EndTurnOnlyDecisionSource(int? maximumActions = null) : IGodotGameplayDecisionSource
{
    public async Task ExecuteAsync(GodotGameplayRuntimeContext context, CancellationToken cancellationToken)
    {
        int actions = 0;
        int limit = Math.Min(maximumActions ?? context.Plan.Watchdog.BattleRoundLimit, context.Plan.Watchdog.BattleRoundLimit);
        while (!context.IsTerminal && actions < limit)
        {
            if (context.IsTerminalPending || !context.CanSubmitPlayerInput)
            {
                await context.WaitForAutomaticProgressAsync(cancellationToken);
                continue;
            }
            await context.PressKeyAsync(Key.Enter, cancellationToken);
            actions++;
        }
        if (!context.IsTerminal) throw new GodotGameplayScenarioException(GodotGameplayFailureKind.NoProgress, "battle_round_limit");
    }
}

public sealed class ProductionInputDecisionSource(int maximumActions) : IGodotGameplayDecisionSource
{
    public async Task ExecuteAsync(GodotGameplayRuntimeContext context, CancellationToken cancellationToken)
    {
        int actions = 0;
        while (!context.IsTerminal && actions < maximumActions)
        {
            if (context.IsTerminalPending || !context.CanSubmitPlayerInput)
            {
                await context.WaitForAutomaticProgressAsync(cancellationToken);
                continue;
            }
            BattleUiSnapshot snapshot = context.Main.CaptureTestProbe().BattleSnapshot!;
            actions++;
            bool usedSkill = await TryUseFirstSkillAsync(context, snapshot, cancellationToken);
            if (!usedSkill && snapshot.MoveAvailability.IsAvailable && snapshot.LegalMoveCells.Count > 0)
            {
                await context.ClickPointerAsync("Move", Parameters(("targetKind", "UiElement")), cancellationToken);
                GridPoint cell = snapshot.LegalMoveCells.OrderBy(value => value.Y).ThenBy(value => value.X).First();
                await context.ClickPointerAsync($"{cell.X},{cell.Y}", Parameters(("targetKind", "BattleCell")), cancellationToken);
                await context.WaitUntilPlayerReadyOrTerminalAsync(cancellationToken);
                if (!context.IsTerminal && !context.IsTerminalPending)
                    await TryUseFirstSkillAsync(context, context.Main.CaptureTestProbe().BattleSnapshot!, cancellationToken);
            }
            await context.WaitUntilPlayerReadyOrTerminalAsync(cancellationToken);
            if (!context.IsTerminal && !context.IsTerminalPending && context.CanSubmitPlayerInput)
                await context.PressKeyAsync(Key.Enter, cancellationToken);
        }
        if (!context.IsTerminal)
            throw new GodotGameplayScenarioException(GodotGameplayFailureKind.NoProgress, "battle_action_limit");
    }

    private static async Task<bool> TryUseFirstSkillAsync(GodotGameplayRuntimeContext context,
        BattleUiSnapshot snapshot, CancellationToken cancellationToken)
    {
        BattleUiSkillAvailability? availability = snapshot.SkillAvailability?.FirstOrDefault(value => value.IsAvailable &&
                snapshot.LegalTargets.Any(target => target.SkillId == value.SkillId));
        if (availability is null) return false;
        await context.ClickPointerAsync("SkillAction_" + availability.SkillId.Value.Replace('.', '_'),
            Parameters(("targetKind", "UiElement")), cancellationToken);
        BattleUiTarget target = context.Main.CaptureTestProbe().BattleSnapshot!.LegalTargets
            .First(value => value.SkillId == availability.SkillId);
        await context.ClickPointerAsync($"{target.Cell.X},{target.Cell.Y}",
            Parameters(("targetKind", "BattleCell")), cancellationToken);
        return true;
    }

    private static Dictionary<string, JsonElement> Parameters(params (string Key, object Value)[] values) =>
        values.ToDictionary(value => value.Key, value => JsonSerializer.SerializeToElement(value.Value), StringComparer.Ordinal);
}

public sealed class GodotGameplayRuntimeRunner
{
    public async Task<GodotGameplayScenarioResult> ExecuteAsync(GodotGameplayScenarioPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        plan.ValidateContract();
        ValidatedGodotRunCheckpoint? checkpoint = plan.Checkpoint is null
            ? null
            : GodotGameplayCheckpointCatalog.Create(plan.Checkpoint.Id);
        if (plan.Checkpoint is not null &&
            (plan.Checkpoint.Id != checkpoint!.Id || plan.Checkpoint.Path != checkpoint.Path ||
             !checkpoint.Verify() || !string.Equals(plan.Checkpoint.SemanticHash, checkpoint.SemanticHash, StringComparison.Ordinal)))
            throw new InvalidDataException("The catalog checkpoint does not match the compiled plan.");
        var trace = new List<GodotGameplayTraceEntry>();
        string before = ProductionSaveEvidence();
        string attemptId = Guid.NewGuid().ToString("N");
        var isolatedStore = new GodotGameplayIsolatedRunStore(plan.ScenarioName, attemptId, checkpoint?.Snapshot);
        TacticsMigrationRoot? activeRoot = null;
        GodotGameplayRuntimeContext? context = null;
        GodotGameplayFailureKind? failure = null;
        string? error = null;
        int remainingTemporaryNodes = 0;
        using var scenarioTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        scenarioTimeout.CancelAfter(plan.Watchdog.ScenarioTimeoutMs);
        try
        {
            PackedScene scene = ResourceLoader.Load<PackedScene>("res://scenes/Main.tscn")
                ?? throw new InvalidOperationException("Main.tscn is missing.");
            activeRoot = scene.Instantiate<TacticsMigrationRoot>();
            activeRoot.ConfigureTestContext(new GodotPlayableRunTestContext(isolatedStore, 7,
                plan.Checkpoint?.Id ?? "no-checkpoint", true, 4f));
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(activeRoot);
            await activeRoot.ToSignal(activeRoot.GetTree(), SceneTree.SignalName.ProcessFrame);
            GodotPlayableRunMain main = activeRoot.PlayableRun ?? throw new InvalidOperationException("Main did not create the playable run UI.");
            context = new GodotGameplayRuntimeContext(plan, activeRoot, main, isolatedStore, before);
            int ordinal = 0;
            foreach (GodotGameplayPlanStep step in plan.SetupActions)
                await ExecuteStepAsync(context, step, "setup", ++ordinal, trace, scenarioTimeout.Token);
            foreach (GodotGameplayPlanStep step in plan.RuntimeActions)
                await ExecuteStepAsync(context, step, "action", ++ordinal, trace, scenarioTimeout.Token);
            foreach (GodotGameplayPlanAssertion assertion in plan.AssertionPlans)
                await ExecuteAssertionAsync(context, assertion, ++ordinal, trace, scenarioTimeout.Token);
        }
        catch (OperationCanceledException)
        {
            failure = GodotGameplayFailureKind.Timeout;
            error = "scenario.timeout";
        }
        catch (GodotGameplayScenarioException exception)
        {
            failure = exception.Kind;
            error = exception.Message;
        }
        catch (Exception exception)
        {
            failure = GodotGameplayFailureKind.Action;
            error = exception.GetType().Name + ":" + exception.Message;
        }
        finally
        {
            activeRoot = context?.Root ?? activeRoot;
            if (activeRoot is not null && GodotObject.IsInstanceValid(activeRoot))
            {
                SceneTree tree = (SceneTree)Engine.GetMainLoop();
                activeRoot.QueueFree();
                for (int frame = 0; frame < 5 && GodotObject.IsInstanceValid(activeRoot); frame++)
                    await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                remainingTemporaryNodes = GodotObject.IsInstanceValid(activeRoot) ? 1 : 0;
                if (remainingTemporaryNodes > 0) { failure ??= GodotGameplayFailureKind.Cleanup; error ??= "scene_cleanup_incomplete"; }
            }
            try { isolatedStore.Cleanup(); }
            catch (Exception cleanupError) { failure ??= GodotGameplayFailureKind.Cleanup; error ??= "isolation_cleanup:" + cleanupError.Message; }
        }
        bool productionUnchanged = before == ProductionSaveEvidence();
        if (!productionUnchanged && failure is null) { failure = GodotGameplayFailureKind.Cleanup; error = "production_save_changed"; }
        return new GodotGameplayScenarioResult(plan.ScenarioName, failure is null, failure, error, trace,
            productionUnchanged, remainingTemporaryNodes);
    }

    private static async Task ExecuteStepAsync(GodotGameplayRuntimeContext context, GodotGameplayPlanStep step,
        string phase, int ordinal, List<GodotGameplayTraceEntry> trace, CancellationToken scenarioToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(scenarioToken);
        timeout.CancelAfter(context.Plan.Watchdog.StepTimeoutMs);
        ulong started = Time.GetTicksMsec();
        try
        {
            switch (step.Kind)
            {
                case "loadValidatedCheckpoint": await context.ValidateCheckpointAsync(timeout.Token); break;
                case "initializePlayerInput": await context.WaitFramesAsync(1, timeout.Token); break;
                case "movePointerToTarget": await context.MovePointerAsync(PointerLocator(step), step.Parameters, timeout.Token); break;
                case "clickPointerTarget": await context.ClickPointerAsync(PointerLocator(step), step.Parameters, timeout.Token); break;
                case "rightClickPointerTarget": await context.RightClickAsync(PointerLocator(step), step.Parameters, timeout.Token); break;
                case "pressInputKey": await context.PressKeyAsync(ParseKey(RequiredString(step.Parameters, "key")), timeout.Token); break;
                case "waitForPlayerObservable": await context.WaitObservableAsync(RequiredString(step.Parameters, "observable"), step.Target, step.Parameters, timeout.Token); break;
                case "waitForFrames": await context.WaitFramesAsync(RequiredInt(step.Parameters, "count", 1), timeout.Token); break;
                case "playBattleThroughInput": await new ProductionInputDecisionSource(RequiredInt(step.Parameters, "maximumActions", 100)).ExecuteAsync(context, timeout.Token); break;
                case "restartGodotMain": await context.RestartMainAsync(timeout.Token); break;
                case "setPresentationPaused": await context.SetPausedAsync(step.Parameters["paused"].GetBoolean(), timeout.Token); break;
                case "setPresentationSpeed": await context.SetSpeedAsync((float)step.Parameters["speed"].GetDouble(), timeout.Token); break;
                case "endTurnOnlyUntilTerminal": await new EndTurnOnlyDecisionSource().ExecuteAsync(context, timeout.Token); break;
                case "endTurnUntilPresentationNumber": await context.EndTurnUntilPresentationNumberAsync(
                    RequiredString(step.Parameters, "kind"), RequiredInt(step.Parameters, "maximumActions", 100), timeout.Token); break;
                default: throw new GodotGameplayScenarioException(GodotGameplayFailureKind.Contract, "unsupported_action:" + step.Kind);
            }
            trace.Add(new GodotGameplayTraceEntry(ordinal, phase, step.Kind, true, context.StateHash(), (long)(Time.GetTicksMsec() - started), null));
        }
        catch (OperationCanceledException) when (!scenarioToken.IsCancellationRequested)
        {
            string diagnostic = $"step.timeout:{ordinal}:{step.Kind}";
            trace.Add(new GodotGameplayTraceEntry(ordinal, phase, step.Kind, false, context.StateHash(), (long)(Time.GetTicksMsec() - started), diagnostic));
            throw new GodotGameplayScenarioException(GodotGameplayFailureKind.Timeout, diagnostic);
        }
        catch (Exception exception)
        {
            trace.Add(new GodotGameplayTraceEntry(ordinal, phase, step.Kind, false, context.StateHash(), (long)(Time.GetTicksMsec() - started), exception.Message));
            throw;
        }
    }

    private static Task ExecuteAssertionAsync(GodotGameplayRuntimeContext context, GodotGameplayPlanAssertion assertion,
        int ordinal, List<GodotGameplayTraceEntry> trace, CancellationToken token)
    {
        GodotPlayableRunProbe probe = context.Main.CaptureTestProbe();
        bool passed = assertion.Kind switch
        {
            "inventoryProjectionEnteredBattle" => context.InventoryProjectionEnteredBattle() == assertion.Expected.GetBoolean(),
            "activeRunExistsEquals" => (probe.SaveSnapshot?.ActiveRun is not null) == assertion.Expected.GetBoolean(),
            "terminalSummaryOutcomeEquals" => string.Equals(probe.SaveSnapshot?.TerminalSummary?.Outcome.ToString(), assertion.Expected.GetString(), StringComparison.Ordinal),
            "presentationNumberEquals" => probe.PresentationNumbers.Any(number =>
                string.Equals(number.Kind.ToString(), assertion.Expected.GetString(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(number.Text, assertion.Expected.GetString(), StringComparison.Ordinal)),
            "presentationNodeCountEquals" => probe.PresentationNumberCount == assertion.Expected.GetInt32(),
            "checkpointRevisionEquals" => probe.SaveSnapshot?.ActiveRun?.Checkpoint?.Revision == assertion.Expected.GetInt64(),
            "runtimeStateHashEquals" => context.StateHash() == assertion.Expected.GetString(),
            "runtimeHasNoErrors" => (probe.RuntimeErrorCount == 0) == assertion.Expected.GetBoolean(),
            "productionSaveUnchanged" => context.ProductionSaveIsUnchanged() == assertion.Expected.GetBoolean(),
            _ => throw new GodotGameplayScenarioException(GodotGameplayFailureKind.Contract, "unsupported_assertion:" + assertion.Kind)
        };
        if (!passed)
        {
            string diagnostic = assertion.Kind == "inventoryProjectionEnteredBattle"
                ? "assertion_failed:" + string.Join(";", context.Main.CaptureInventoryBattleProjectionEvidence().Select(value =>
                    $"{value.CharacterId}:equipment={value.EquipmentCount},hp={value.BaseMaxHealth}->{value.ProjectedMaxHealth}/{value.BattleMaxHealth},mp={value.BaseMaxMana}->{value.ProjectedMaxMana}/{value.BattleMaxMana},match={value.Matches}"))
                : "assertion_failed";
            trace.Add(new GodotGameplayTraceEntry(ordinal, "assertion", assertion.Kind, false, context.StateHash(), 0, diagnostic));
            throw new GodotGameplayScenarioException(GodotGameplayFailureKind.Assertion,
                assertion.Kind == "inventoryProjectionEnteredBattle" ? diagnostic : "assertion_failed:" + assertion.Kind);
        }
        trace.Add(new GodotGameplayTraceEntry(ordinal, "assertion", assertion.Kind, true, context.StateHash(), 0, null));
        return Task.CompletedTask;
    }

    private static string RequiredString(Dictionary<string, JsonElement> parameters, string key) =>
        parameters.TryGetValue(key, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()! : throw new InvalidDataException($"Missing string parameter '{key}'.");
    private static int RequiredInt(Dictionary<string, JsonElement> parameters, string key, int fallback) =>
        parameters.TryGetValue(key, out JsonElement value) ? value.GetInt32() : fallback;
    private static string PointerLocator(GodotGameplayPlanStep step) => step.Target ??
        new[] { "elementName", "nodeId", "unitId", "cell" }
            .Select(key => step.Parameters.TryGetValue(key, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ??
        throw new InvalidDataException("Pointer action requires a semantic target locator.");
    private static Key ParseKey(string key) => Enum.TryParse(key, true, out Key parsed) ? parsed : throw new InvalidDataException("Unknown key: " + key);

    private static string ProductionSaveEvidence()
    {
        static string Evidence(string path)
        {
            string absolute = ProjectSettings.GlobalizePath(path);
            if (!File.Exists(absolute)) return "missing";
            var info = new FileInfo(absolute);
            return $"{info.Length}:{info.LastWriteTimeUtc.Ticks}:{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(absolute)))}";
        }
        return Evidence(GodotRunSaveStore.DefaultPath) + "|" + Evidence(GodotRunSaveStore.DefaultPath + ".bak");
    }
}

public sealed class GodotGameplayRuntimeContext(GodotGameplayScenarioPlan plan, TacticsMigrationRoot root,
    GodotPlayableRunMain main, GodotGameplayIsolatedRunStore saveStore, string productionSaveEvidence)
{
    public GodotGameplayScenarioPlan Plan { get; } = plan;
    public TacticsMigrationRoot Root { get; private set; } = root;
    public GodotPlayableRunMain Main { get; private set; } = main;
    private GodotGameplayIsolatedRunStore SaveStore { get; } = saveStore;
    private string ProductionSaveEvidence { get; } = productionSaveEvidence;
    private string _lastHash = string.Empty;
    private int _sameHashCount;

    public async Task ClickButtonAsync(string text, CancellationToken token)
    {
        Control expected;
        Vector2 logicalPoint;
        Button[] candidates = Descendants<Button>(Main).Where(value => value.Visible && !value.Disabled).ToArray();
        Button? button = candidates.FirstOrDefault(value => string.Equals(value.Name, text, StringComparison.Ordinal)) ??
            candidates.FirstOrDefault(value => string.Equals(value.Text, text, StringComparison.OrdinalIgnoreCase)) ??
            candidates.FirstOrDefault(value => value.Text.Contains(text, StringComparison.OrdinalIgnoreCase));
        GodotRogueMapView? map = null;
        string? mapNodeId = null;
        if (button is not null) { expected = button; logicalPoint = button.GetGlobalRect().GetCenter(); }
        else if (text.StartsWith("map:", StringComparison.Ordinal))
        {
            map = Descendants<GodotRogueMapView>(Main).Single();
            mapNodeId = text[4..];
            expected = map; logicalPoint = map.GetGlobalTransform() * map.NodeCenter(text[4..]);
        }
        else throw new GodotGameplayScenarioException(GodotGameplayFailureKind.Action, "pointer_target_not_available:" + text);
        (Viewport viewport, Vector2 point, bool localCoordinates) = await ResolvePointerAsync(logicalPoint, expected, text, token);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Action buttonHandler = completion.SetResult;
        Action<string> mapHandler = nodeId => { if (nodeId == mapNodeId) completion.TrySetResult(); };
        if (button is not null) button.Pressed += buttonHandler;
        else map!.NodePressed += mapHandler;
        using CancellationTokenRegistration registration = token.Register(() => completion.TrySetCanceled(token));
        viewport.PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true }, localCoordinates);
        await WaitFramesAsync(1, token);
        viewport.PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }, localCoordinates);
        await completion.Task;
        if (button is not null && GodotObject.IsInstanceValid(button)) button.Pressed -= buttonHandler;
        else if (map is not null && GodotObject.IsInstanceValid(map)) map.NodePressed -= mapHandler;
        await WaitFramesAsync(1, token);
    }

    public async Task MovePointerAsync(string target, CancellationToken token) =>
        await MovePointerAsync(target, [], token);

    public async Task MovePointerAsync(string target, Dictionary<string, JsonElement> parameters, CancellationToken token)
    {
        string targetKind = OptionalString(parameters, "targetKind") ?? "UiElement";
        Button? button = string.Equals(targetKind, "UiElement", StringComparison.Ordinal)
            ? Descendants<Button>(Main).FirstOrDefault(value => value.Visible &&
                (value.Text.Contains(target, StringComparison.OrdinalIgnoreCase) || string.Equals(value.Name, target, StringComparison.Ordinal)))
            : null;
        if (button is not null) { await ResolvePointerAsync(button.GetGlobalRect().GetCenter(), button, target, token); return; }
        if (string.Equals(targetKind, "MapNode", StringComparison.Ordinal) || target.StartsWith("map:", StringComparison.Ordinal))
        {
            GodotRogueMapView map = Descendants<GodotRogueMapView>(Main).Single();
            string nodeId = target.StartsWith("map:", StringComparison.Ordinal) ? target[4..] : target;
            Vector2 logical = map.GetGlobalTransform() * map.NodeCenter(nodeId);
            await ResolvePointerAsync(logical, map, target, token);
            return;
        }
        if (Main.TryResolveTestBattlePointerTarget(targetKind, target, out Control? surface, out Vector2 battlePoint) && surface is not null)
        {
            await ResolvePointerAsync(battlePoint, surface, target, token);
            return;
        }
        throw new GodotGameplayScenarioException(GodotGameplayFailureKind.Action, "pointer_target_not_found:" + target);
    }

    public async Task ClickPointerAsync(string target, Dictionary<string, JsonElement> parameters, CancellationToken token)
    {
        string targetKind = OptionalString(parameters, "targetKind") ?? "UiElement";
        if (string.Equals(targetKind, "UiElement", StringComparison.Ordinal))
        {
            await ClickButtonAsync(target, token);
            return;
        }
        if (string.Equals(targetKind, "MapNode", StringComparison.Ordinal))
        {
            await ClickButtonAsync(target.StartsWith("map:", StringComparison.Ordinal) ? target : "map:" + target, token);
            return;
        }
        await MovePointerAsync(target, parameters, token);
        GodotIsometricBattleBoard board = Descendants<GodotIsometricBattleBoard>(Main).Single();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(Vector2 _) => completion.TrySetResult();
        board.PointerPressed += Handler;
        using CancellationTokenRegistration registration = token.Register(() => completion.TrySetCanceled(token));
        Viewport viewport = Main.GetViewport();
        Vector2 point = viewport.GetMousePosition();
        viewport.PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true }, false);
        await WaitFramesAsync(1, token);
        viewport.PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }, false);
        await completion.Task;
        if (GodotObject.IsInstanceValid(board)) board.PointerPressed -= Handler;
    }

    private async Task<(Viewport Viewport, Vector2 Point, bool Local)> ResolvePointerAsync(Vector2 logicalPoint,
        Control expectedControl, string identity, CancellationToken token)
    {
        Viewport viewport = Main.GetViewport();
        (Vector2 Point, bool Local)[] candidates =
        [
            (viewport.GetCanvasTransform() * logicalPoint, false),
            (logicalPoint, true),
            (logicalPoint, false)
        ];
        (Vector2 Point, bool Local)? resolved = null;
        foreach ((Vector2 candidatePoint, bool local) in candidates)
        {
            viewport.PushInput(new InputEventMouseMotion { Position = candidatePoint, GlobalPosition = candidatePoint }, local);
            await WaitFramesAsync(1, token);
            if (viewport.GuiGetHoveredControl() == expectedControl) { resolved = (candidatePoint, local); break; }
        }
        if (resolved is null) throw new GodotGameplayScenarioException(GodotGameplayFailureKind.Action,
            $"pointer_target_not_hovered:{identity}:viewport={viewport.GetVisibleRect()}");
        return (viewport, resolved.Value.Point, resolved.Value.Local);
    }

    public async Task RightClickAsync(string target, Dictionary<string, JsonElement> parameters, CancellationToken token)
    {
        await MovePointerAsync(target, parameters, token);
        BattleTargetingMode before = Main.CaptureTestProbe().BattleSnapshot?.TargetingMode ?? BattleTargetingMode.None;
        Viewport viewport = Main.GetViewport();
        Vector2 point = viewport.GetMousePosition();
        foreach (bool pressed in new[] { true, false })
        {
            viewport.PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Right, Pressed = pressed }, false);
            await WaitFramesAsync(1, token);
        }
        if (before != BattleTargetingMode.None)
            await WaitUntilAsync(() => Main.CaptureTestProbe().BattleSnapshot?.TargetingMode == BattleTargetingMode.None, token);
    }

    public async Task WaitObservableAsync(string observable, string? target,
        Dictionary<string, JsonElement> parameters, CancellationToken token)
    {
        string? locator = target ?? OptionalString(parameters, "elementName") ?? OptionalString(parameters, "uiId");
        int maximumFrames = RequiredPositiveInt(parameters, "maximumFrames", int.MaxValue);
        for (int frame = 0; frame < maximumFrames; frame++)
        {
            GodotPlayableRunProbe probe = Main.CaptureTestProbe();
            bool ready = observable switch
            {
                "mapReady" => probe.PageTitle.Contains("MAP", StringComparison.OrdinalIgnoreCase),
                "battleReady" => probe.BattleSnapshot is not null,
                "humanTurn" => probe.BattleSnapshot?.Phase == Tactics.Application.Battle.PlayableBattlePhase.PlayerTurn,
                "battleEnded" => IsTerminal,
                "uiVisible" or "uiElement" => locator is not null && IsUiVisible(locator),
                "uiHidden" => locator is not null && !IsUiVisible(locator),
                _ => false
            };
            if (ready) return;
            await WaitFramesAsync(1, token);
        }
        throw new GodotGameplayScenarioException(GodotGameplayFailureKind.NoProgress,
            $"observable_not_reached:{observable}:{locator ?? "none"}");
    }

    public async Task RestartMainAsync(CancellationToken token)
    {
        SceneTree tree = Main.GetTree();
        TacticsMigrationRoot previousRoot = Root;
        previousRoot.QueueFree();
        for (int frame = 0; frame < 120 && GodotObject.IsInstanceValid(previousRoot); frame++)
        {
            token.ThrowIfCancellationRequested();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
        if (GodotObject.IsInstanceValid(previousRoot))
            throw new GodotGameplayScenarioException(GodotGameplayFailureKind.Cleanup, "previous_main_cleanup_failed");
        PackedScene scene = ResourceLoader.Load<PackedScene>("res://scenes/Main.tscn")!;
        Root = scene.Instantiate<TacticsMigrationRoot>();
        Root.ConfigureTestContext(new GodotPlayableRunTestContext(SaveStore, 7,
            Plan.Checkpoint?.Id ?? "no-checkpoint", true, 4f));
        tree.Root.AddChild(Root);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        Main = Root.PlayableRun ?? throw new GodotGameplayScenarioException(GodotGameplayFailureKind.Action, "main_restart_failed");
        RunStoreResult loaded = SaveStore.Load();
        if (loaded.Succeeded && loaded.Snapshot is { } snapshot &&
            (snapshot.ActiveRun is not null || snapshot.TerminalSummary is not null || snapshot.PendingRunSetup is not null))
            await ClickButtonAsync("Continue", token);
        token.ThrowIfCancellationRequested();
    }

    public async Task PressKeyAsync(Key key, CancellationToken token)
    {
        GodotPlayableRunProbe before = Main.CaptureTestProbe();
        if (key is Key.Enter or Key.KpEnter && (before.PresentationLocked || before.PresentationPlaying))
            throw new GodotGameplayScenarioException(GodotGameplayFailureKind.Action, "input_locked:presentation");
        Main.GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true }, true);
        Main.GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false }, true);
        if (key is Key.Enter or Key.KpEnter)
        {
            BattleAuthorityStamp stamp = BattleAuthorityStamp.From(before.BattleSnapshot);
            await WaitUntilAsync(() => BattleAuthorityStamp.From(Main.CaptureTestProbe().BattleSnapshot) != stamp, token);
            return;
        }
        if (key == Key.Escape)
        {
            await WaitUntilAsync(() =>
            {
                GodotPlayableRunProbe after = Main.CaptureTestProbe();
                return before.CheatConsoleVisible && !after.CheatConsoleVisible ||
                    before.BattleSnapshot?.TargetingMode != BattleTargetingMode.None && after.BattleSnapshot?.TargetingMode == BattleTargetingMode.None ||
                    !before.PauseMenuVisible && after.PauseMenuVisible;
            }, token);
            return;
        }
        await WaitForStateDifferentAsync(StateHash(before), token);
    }

    public async Task SetPausedAsync(bool paused, CancellationToken token)
    {
        if (Main.CaptureTestProbe().PlaybackPaused == paused) return;
        await ClickButtonAsync("Pause/Resume", token);
        await WaitUntilAsync(() => Main.CaptureTestProbe().PlaybackPaused == paused, token);
    }

    public async Task SetSpeedAsync(float speed, CancellationToken token)
    {
        for (int index = 0; index < 4; index++)
        {
            if (Math.Abs(Main.CaptureTestProbe().PlaybackSpeed - speed) < .001f) return;
            await ClickButtonAsync("Speed ", token);
        }
        throw new GodotGameplayScenarioException(GodotGameplayFailureKind.Action, "speed_not_reached");
    }

    public async Task WaitForChangeAsync(CancellationToken token)
    {
        string before = StateHash();
        await WaitForStateDifferentAsync(before, token);
    }

    public async Task WaitForAutomaticProgressAsync(CancellationToken token)
    {
        string before = StateHash();
        await WaitFramesAsync(1, token);
        GodotPlayableRunProbe probe = Main.CaptureTestProbe();
        string after = StateHash(probe);
            if (after != before || probe.PresentationPlaying || probe.PresentationLocked || probe.AutomaticFramesPending || IsTerminalPending)
        {
            _sameHashCount = 0;
            _lastHash = after;
            return;
        }
        if (after == _lastHash) _sameHashCount++; else { _lastHash = after; _sameHashCount = 1; }
        if (_sameHashCount >= Plan.Watchdog.NoProgressLimit)
            throw new GodotGameplayScenarioException(GodotGameplayFailureKind.NoProgress, "no_progress");
    }

    public async Task WaitUntilPlayerReadyOrTerminalAsync(CancellationToken token)
    {
        while (!IsTerminal && !IsTerminalPending && !CanSubmitPlayerInput)
            await WaitForAutomaticProgressAsync(token);
    }

    public async Task EndTurnUntilPresentationNumberAsync(string kind, int maximumActions, CancellationToken token)
    {
        for (int action = 0; action < maximumActions;)
        {
            if (Main.CaptureTestProbe().PresentationNumbers.Any(number =>
                    string.Equals(number.Kind.ToString(), kind, StringComparison.OrdinalIgnoreCase)))
                return;
            if (IsTerminal)
                throw new GodotGameplayScenarioException(GodotGameplayFailureKind.NoProgress,
                    "presentation_number_not_observed:" + kind);
            if (!CanSubmitPlayerInput)
            {
                await WaitForAutomaticProgressAsync(token);
                continue;
            }
            await PressKeyAsync(Key.Enter, token);
            action++;
        }
        if (!Main.CaptureTestProbe().PresentationNumbers.Any(number =>
                string.Equals(number.Kind.ToString(), kind, StringComparison.OrdinalIgnoreCase)))
            throw new GodotGameplayScenarioException(GodotGameplayFailureKind.NoProgress,
                "presentation_number_action_limit:" + kind);
    }

    private async Task WaitForStateDifferentAsync(string before, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await WaitFramesAsync(1, token);
            string after = StateHash();
            if (after != before) return;
            GodotPlayableRunProbe probe = Main.CaptureTestProbe();
            if (probe.PresentationPlaying || probe.PresentationLocked) { _sameHashCount = 0; continue; }
            if (after == _lastHash) _sameHashCount++; else { _lastHash = after; _sameHashCount = 0; }
            if (_sameHashCount >= Plan.Watchdog.NoProgressLimit)
                throw new GodotGameplayScenarioException(GodotGameplayFailureKind.NoProgress, "no_progress");
        }
        token.ThrowIfCancellationRequested();
    }

    public async Task WaitFramesAsync(int count, CancellationToken token)
    {
        for (int index = 0; index < count; index++)
        {
            token.ThrowIfCancellationRequested();
            await Main.ToSignal(Main.GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    public string StateHash()
    {
        return StateHash(Main.CaptureTestProbe());
    }

    private static string StateHash(GodotPlayableRunProbe probe)
    {
        string value = $"{probe.PageTitle}|{probe.SaveSnapshot?.Revision}|{probe.SaveSnapshot?.ActiveRun?.Revision}|{probe.BattleSnapshot?.Round}|{probe.BattleSnapshot?.ActiveUnitId.Value}|{probe.BattleSnapshot?.Phase}|{probe.PresentationLocked}|{probe.PresentationPlaying}|{probe.AutomaticFramesPending}|{probe.PresentationNumberCount}|{probe.PlaybackPaused}|{probe.PlaybackSpeed}|{probe.QuitRequested}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    public bool IsTerminal
    {
        get
        {
            GodotPlayableRunProbe probe = Main.CaptureTestProbe();
            return probe.SaveSnapshot?.TerminalSummary is not null;
        }
    }

    public bool IsTerminalPending
    {
        get
        {
            GodotPlayableRunProbe probe = Main.CaptureTestProbe();
            return probe.BattleSnapshot?.TerminalPending == true ||
                probe.BattleSnapshot?.Phase is PlayableBattlePhase.Victory or PlayableBattlePhase.Defeat;
        }
    }

    public bool CanSubmitPlayerInput
    {
        get
        {
            GodotPlayableRunProbe probe = Main.CaptureTestProbe();
            return probe.BattleSnapshot?.Phase == PlayableBattlePhase.PlayerTurn &&
                !probe.PresentationLocked && !probe.PresentationPlaying && !probe.AutomaticFramesPending && !probe.PlaybackPaused;
        }
    }

    public bool ProductionSaveIsUnchanged() => ProductionSaveEvidence == CaptureProductionSaveEvidence();

    public bool InventoryProjectionEnteredBattle()
        => Main.ValidateInventoryProjectionEnteredBattle();

    public async Task ValidateCheckpointAsync(CancellationToken token)
    {
        RunStoreResult loaded = SaveStore.Load();
        if (!loaded.Succeeded || loaded.Snapshot is null || Plan.Checkpoint is null)
            throw new GodotGameplayScenarioException(GodotGameplayFailureKind.Contract, "validated_checkpoint_not_loaded");
        ValidatedGodotRunCheckpoint actual = ValidatedGodotRunCheckpoint.Create(Plan.Checkpoint.Id, Plan.Checkpoint.Path, loaded.Snapshot);
        if (!string.Equals(actual.SemanticHash, Plan.Checkpoint.SemanticHash, StringComparison.Ordinal))
            throw new GodotGameplayScenarioException(GodotGameplayFailureKind.Contract, "validated_checkpoint_hash_mismatch");
        if (loaded.Snapshot.ActiveRun is not null || loaded.Snapshot.TerminalSummary is not null ||
            loaded.Snapshot.PendingRunSetup is not null)
            await ClickButtonAsync("Continue", token);
    }

    private static string CaptureProductionSaveEvidence()
    {
        static string Evidence(string path)
        {
            string absolute = ProjectSettings.GlobalizePath(path);
            if (!File.Exists(absolute)) return "missing";
            var info = new FileInfo(absolute);
            return $"{info.Length}:{info.LastWriteTimeUtc.Ticks}:{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(absolute)))}";
        }
        return Evidence(GodotRunSaveStore.DefaultPath) + "|" + Evidence(GodotRunSaveStore.DefaultPath + ".bak");
    }

    private static IEnumerable<T> Descendants<T>(Node node) where T : Node
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is T match) yield return match;
            foreach (T nested in Descendants<T>(child)) yield return nested;
        }
    }

    private bool IsUiVisible(string locator) =>
        Main.CaptureTestProbe().PageTitle.Contains(locator, StringComparison.OrdinalIgnoreCase) ||
        Descendants<Control>(Main).Any(value => value.Visible &&
            (string.Equals(value.Name, locator, StringComparison.OrdinalIgnoreCase) ||
             value is Button button && button.Text.Contains(locator, StringComparison.OrdinalIgnoreCase) ||
             value is Label label && label.Text.Contains(locator, StringComparison.OrdinalIgnoreCase)));

    private async Task WaitUntilAsync(Func<bool> predicate, CancellationToken token)
    {
        while (!predicate())
        {
            token.ThrowIfCancellationRequested();
            await WaitFramesAsync(1, token);
        }
    }

    private static string? OptionalString(Dictionary<string, JsonElement> parameters, string key) =>
        parameters.TryGetValue(key, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int RequiredPositiveInt(Dictionary<string, JsonElement> parameters, string key, int fallback) =>
        parameters.TryGetValue(key, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.GetInt32() > 0
            ? value.GetInt32()
            : fallback;

    private readonly record struct BattleAuthorityStamp(int Round, string ActiveUnit, PlayableBattlePhase Phase,
        BattleTargetingMode TargetingMode, int EventCount)
    {
        public static BattleAuthorityStamp From(BattleUiSnapshot? snapshot) => snapshot is null
            ? new BattleAuthorityStamp(-1, string.Empty, PlayableBattlePhase.Faulted, BattleTargetingMode.None, -1)
            : new BattleAuthorityStamp(snapshot.Round, snapshot.ActiveUnitId.Value, snapshot.Phase,
                snapshot.TargetingMode, snapshot.RecentEvents.Count);
    }
}

public sealed class GodotGameplayScenarioException(GodotGameplayFailureKind kind, string message) : Exception(message)
{
    public GodotGameplayFailureKind Kind { get; } = kind;
}
