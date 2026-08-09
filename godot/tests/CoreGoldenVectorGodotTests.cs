using GdUnit4;
using System.Text.Json;
using Tactics.Application.Presentation;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Combat;
using Tactics.Core.Content;
using Tactics.Core.Pathfinding;
using Tactics.Core.Presentation;
using Tactics.Core.Turns;
using Tactics.Core.Units;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests;

[TestSuite]
public class CoreGoldenVectorGodotTests
{
    [TestCase]
    public void SharedGoldenVectorIsConsumedWithoutGodotRuntime()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Golden", "10x10-core-vectors.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        string decisionsPath = Path.Combine(AppContext.BaseDirectory, "Golden", "contract-decisions.json");
        using JsonDocument decisions = JsonDocument.Parse(File.ReadAllText(decisionsPath));
        IReadOnlyDictionary<string, JsonElement> contracts = decisions.RootElement.GetProperty("contracts")
            .EnumerateArray()
            .ToDictionary(item => item.GetProperty("id").GetString()!, StringComparer.Ordinal);
        JsonElement boardVector = document.RootElement.GetProperty("board");
        JsonElement pathVector = document.RootElement.GetProperty("pathQueries")[0];
        var cells = Enumerable.Range(0, BoardSpec.Width)
            .SelectMany(x => Enumerable.Range(0, BoardSpec.Height)
                .Select(y => new KeyValuePair<GridPoint, CellState>(new GridPoint(x, y), new CellState())))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (JsonElement blocker in pathVector.GetProperty("movementBlockers").EnumerateArray())
            cells[new GridPoint(blocker[0].GetInt32(), blocker[1].GetInt32())] = new CellState(blocksMovement: true);

        IReadOnlyList<GridPoint> pathResult = new DeterministicDijkstraPathfinder().FindPath(
            new BoardSnapshot(cells),
            Point(pathVector.GetProperty("origin")),
            Point(pathVector.GetProperty("destination")));
        GridPoint[] expected = pathVector.GetProperty("expectedPath").EnumerateArray().Select(Point).ToArray();
        JsonElement initiativeVector = document.RootElement.GetProperty("initiativeCases")[0];
        IReadOnlyList<InitiativeEntry> initiative = InitiativeOrder.Sort(
            initiativeVector.GetProperty("entries").EnumerateArray().Select(entry => new InitiativeEntry(
                new UnitInstanceId(entry.GetProperty("instanceId").GetString()!),
                entry.GetProperty("initiative").GetSingle(),
                entry.GetProperty("playerNumber").GetInt32(),
                entry.GetProperty("spawnOrdinal").GetInt32())));
        string[] expectedInitiative = initiativeVector.GetProperty("expectedOrder").EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();
        JsonElement roundVector = document.RootElement.GetProperty("initiativeRoundCases")[0];
        var roundEntries = roundVector.GetProperty("entries").EnumerateArray().ToDictionary(
            entry => entry.GetProperty("instanceId").GetString()!,
            entry => Initiative(entry),
            StringComparer.Ordinal);
        InitiativeTakeNextResult first = InitiativeRoundState.StartRound(roundEntries.Values)
            .TakeNext(roundEntries.Values);
        JsonElement initiativeChange = roundVector.GetProperty("changes")[0];
        string changedId = initiativeChange.GetProperty("instanceId").GetString()!;
        roundEntries[changedId] = roundEntries[changedId] with
        {
            Initiative = initiativeChange.GetProperty("initiative").GetSingle()
        };
        InitiativeRoundState changedRound = first.State.NotifyInitiativeChanged(roundEntries[changedId]);
        InitiativeTakeNextResult second = changedRound.TakeNext(roundEntries.Values);

        JsonElement presentationVector = document.RootElement.GetProperty("presentationCases")[0];
        var presentationGraph = new PresentationGraphDefinition(
            presentationVector.GetProperty("schemaVersion").GetInt32(),
            presentationVector.GetProperty("nodes").EnumerateArray().Select(CreatePresentationGraphNode),
            presentationVector.GetProperty("edges").EnumerateArray().Select((edge, index) => new PresentationGraphEdge(
                edge.GetProperty("source").GetString()!,
                edge.GetProperty("target").GetString()!,
                index)));
        PresentationExecutionPlan presentationPlan = PresentationGraphCompiler.Compile(
            presentationGraph,
            presentationVector.GetProperty("cueId").GetString()!);

        AssertThat(boardVector.GetProperty("width").GetInt32()).IsEqual(BoardSpec.Width);
        AssertThat(boardVector.GetProperty("height").GetInt32()).IsEqual(BoardSpec.Height);
        AssertThat(pathResult.ToArray()).ContainsExactly(expected);
        AssertThat(initiative.Select(entry => entry.UnitId.Value).ToArray()).ContainsExactly(expectedInitiative);
        AssertThat(first.Current?.UnitId.Value).IsEqual(roundVector.GetProperty("expectedFirst").GetString()!);
        AssertThat(changedRound.GetCurrentRoundOrder().Select(entry => entry.UnitId.Value).ToArray())
            .ContainsExactly(roundVector.GetProperty("expectedOrderAfterChange").EnumerateArray()
                .Select(value => value.GetString()!).ToArray());
        AssertThat(second.Current?.UnitId.Value).IsEqual(roundVector.GetProperty("expectedSecond").GetString()!);
        AssertThat(second.State.Remaining.Select(entry => entry.UnitId.Value).ToArray())
            .ContainsExactly(roundVector.GetProperty("expectedRemainingAfterSecond").EnumerateArray()
                .Select(value => value.GetString()!).ToArray());
        AssertThat(Snapshot(presentationPlan, presentationPlan.RootNodeId))
            .IsEqual(presentationVector.GetProperty("expectedSnapshot").GetString()!);
        AssertThat(contracts["battle.command-event-transition"].GetProperty("runtimeContractId").GetString()!)
            .IsEqual(BattleTransitionService.ContractId);
        AssertThat(contracts["random.splitmix64-v1"].GetProperty("runtimeContractId").GetString()!)
            .IsEqual(Tactics.Core.Randomness.DeterministicRandom.AlgorithmId);
    }

    [TestCase]
    public void BattleTransitionGoldenIsReplayedByGodotTestHost()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Golden", "10x10-core-vectors.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement scenario = document.RootElement.GetProperty("battleScenarios")[0];
        var units = scenario.GetProperty("units").EnumerateArray()
            .Select(unit => new BattleUnitState(
                new UnitState(
                    new UnitInstanceId(unit.GetProperty("instanceId").GetString()!),
                    new ContentId(unit.GetProperty("definitionId").GetString()!),
                    Point(unit.GetProperty("cell")),
                    unit.GetProperty("moveRange").GetInt32(),
                    unit.GetProperty("initiative").GetSingle(),
                    unit.GetProperty("playerNumber").GetInt32(),
                    unit.GetProperty("spawnOrdinal").GetInt32()),
                unit.GetProperty("maxHealth").GetInt32(),
                unit.GetProperty("currentHealth").GetInt32(),
                unit.GetProperty("hasMovedThisTurn").GetBoolean(),
                maxMana: unit.GetProperty("maxMana").GetInt32(),
                currentMana: unit.GetProperty("currentMana").GetInt32(),
                statuses: unit.GetProperty("statuses").EnumerateArray().ToDictionary(
                    status => new ContentId(status.GetProperty("contentId").GetString()!),
                    status => new BattleStatusState(
                        new ContentId(status.GetProperty("contentId").GetString()!),
                        new UnitInstanceId(status.GetProperty("sourceId").GetString()!),
                        status.GetProperty("remainingTurns").GetInt32(),
                        status.GetProperty("damagePerTurn").GetInt32()))))
            .ToArray();
        var state = new BattleState(
            CreateBoard(scenario),
            units,
            scenario.GetProperty("turnOrder").EnumerateArray()
                .Select(value => new UnitInstanceId(value.GetString()!))
                .ToArray(),
            scenario.GetProperty("round").GetInt32(),
            scenario.GetProperty("activeIndex").GetInt32(),
            ulong.Parse(scenario.GetProperty("randomState").GetString()!));
        var service = new BattleTransitionService();

        foreach (JsonElement commandVector in scenario.GetProperty("commands").EnumerateArray())
        {
            BattleTransition transition = service.Apply(state, CreateCommand(commandVector));
            string[] expectedEventTypes = commandVector.GetProperty("expectedEvents").EnumerateArray()
                .Select(item => item.GetProperty("type").GetString()!)
                .ToArray();

            AssertThat(transition.Succeeded).IsEqual(commandVector.GetProperty("expectedSucceeded").GetBoolean());
            AssertThat(transition.Events.Select(EventType).ToArray()).ContainsExactly(expectedEventTypes);
            state = transition.State;
        }

        JsonElement expected = scenario.GetProperty("expectedFinalState");
        JsonElement expectedTarget = expected.GetProperty("units").EnumerateArray()
            .Single(unit => unit.GetProperty("instanceId").GetString() == "enemy.target.0");
        BattleUnitState target = state.Units[new UnitInstanceId("enemy.target.0")];
        JsonElement expectedSpear = expected.GetProperty("droppedSpears")[0];
        AssertThat(document.RootElement.GetProperty("schemaVersion").GetInt32()).IsEqual(6);
        AssertThat(state.ActiveUnitId.Value).IsEqual(expected.GetProperty("activeUnitId").GetString()!);
        AssertThat(target.CurrentHealth).IsEqual(expectedTarget.GetProperty("currentHealth").GetInt32());
        AssertThat(target.StatusDurations[new ContentId("buff.poison")]).IsEqual(3);
        AssertThat(state.DroppedSpears[new UnitInstanceId(expectedSpear.GetProperty("ownerId").GetString()!)])
            .IsEqual(Point(expectedSpear.GetProperty("cell")));
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
        AssertThat(validation.CatalogEntryCount).IsEqual(6);
        AssertThat(validation.Action.Succeeded).IsTrue();
        AssertThat(validation.Action.Damage).IsEqual(8);
        AssertThat(validation.Action.PoisonTurns).IsEqual(3);
        AssertThat(validation.Presentation.RootNodeId).IsEqual("__poison_spear.runtime");
        AssertThat(validation.Presentation.Nodes.Count).IsEqual(3);
        var presentation = global::Godot.ResourceLoader.Load<Tactics.Godot.Adapter.Runtime.PoisonSpearPresentationResource>(
            "res://content/poison_spear/PoisonSpearPresentationLv1.tres");
        AssertThat(presentation).IsNotNull();
        AssertThat(presentation?.AuthoringNodeIds.Length ?? 0).IsEqual(6);
        AssertThat(presentation?.AuthoringNodePositions.Length ?? 0).IsEqual(6);
        AssertThat(presentation?.EdgeIds.Length ?? 0).IsEqual(4);
        if (presentation is not null)
        {
            AssertThat(presentation.AuthoringNodePositions[0]).IsEqual(new global::Godot.Vector2(0f, 20f));
            AssertThat(presentation.AuthoringNodePositions[5]).IsEqual(new global::Godot.Vector2(570f, 220f));
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void PresentationChangeSet_UsesRevisionUpdatesPlanAndRollsBackFailedSave()
    {
        Tactics.Godot.Adapter.Runtime.PoisonSpearPresentationResource presentation =
            CreatePresentationResourceForMutation();
        string originalRevision =
            Tactics.Godot.Adapter.Editor.PoisonSpearPresentationEditorService.SynchronizeRevision(presentation);
        PresentationGraphDocument source =
            Tactics.Godot.Adapter.Editor.PoisonSpearPresentationEditorService.Read(presentation);
        var disableAction = new PresentationGraphChangeSet(
            "test.disable-action",
            source.Revision,
            new[] { new SetPresentationNodeEnabledOperation("action", false) });
        PresentationGraphMutationResult disabled =
            Tactics.Godot.Adapter.Editor.PoisonSpearPresentationEditorService.Apply(
                presentation,
                disableAction);
        var moveAction = new PresentationGraphChangeSet(
            "test.move-action",
            disabled.Document.Revision,
            new[]
            {
                new SetPresentationNodePositionOperation(
                    "action",
                    new PresentationNodePosition(320f, 40f))
            });
        PresentationGraphMutationResult moved =
            Tactics.Godot.Adapter.Editor.PoisonSpearPresentationEditorService.Apply(
                presentation,
                moveAction);
        var stale = new PresentationGraphChangeSet(
            "test.stale",
            originalRevision,
            new[] { new SetPresentationNodeEnabledOperation("projectile", false) });
        PresentationGraphMutationResult rejected =
            Tactics.Godot.Adapter.Editor.PoisonSpearPresentationEditorService.Apply(presentation, stale);

        string testDirectory = "res://.godot/tactics-migration-tests";
        string testPath = $"{testDirectory}/presentation-change-set.tres";
        string absoluteDirectory = global::Godot.ProjectSettings.GlobalizePath(testDirectory);
        string absolutePath = global::Godot.ProjectSettings.GlobalizePath(testPath);
        Directory.CreateDirectory(absoluteDirectory);
        global::Godot.Error initialSave = global::Godot.ResourceSaver.Save(presentation, testPath);
        long expectedUid = global::Godot.ResourceUid.CreateIdForPath(testPath);
        if (!global::Godot.ResourceUid.HasId(expectedUid))
            global::Godot.ResourceUid.AddId(expectedUid, testPath);
        global::Godot.Error initialUidSave =
            global::Godot.ResourceSaver.SetUid(testPath, expectedUid);
        Tactics.Godot.Adapter.Editor.PoisonSpearPresentationEditorService.SaveWithRollback(
            presentation,
            testPath);
        bool successfulSavePreservedUid = File.ReadAllText(absolutePath).Contains(
            global::Godot.ResourceUid.IdToText(expectedUid),
            StringComparison.Ordinal);
        var successfullyReloaded = global::Godot.ResourceLoader.Load<
            Tactics.Godot.Adapter.Runtime.PoisonSpearPresentationResource>(
                testPath,
                string.Empty,
                global::Godot.ResourceLoader.CacheMode.Ignore);
        byte[] beforeRollback = File.ReadAllBytes(absolutePath);
        bool rollbackTriggered = false;
        try
        {
            Tactics.Godot.Adapter.Editor.PoisonSpearPresentationEditorService.SaveWithRollback(
                presentation,
                testPath,
                _ => false);
        }
        catch (InvalidOperationException)
        {
            rollbackTriggered = true;
        }
        byte[] afterRollback = File.ReadAllBytes(absolutePath);
        File.Delete(absolutePath);

        AssertThat(initialSave).IsEqual(global::Godot.Error.Ok);
        AssertThat(initialUidSave).IsEqual(global::Godot.Error.Ok);
        AssertThat(successfulSavePreservedUid).IsTrue();
        AssertThat(disabled.Succeeded).IsTrue();
        AssertThat(disabled.Changed).IsTrue();
        AssertThat(disabled.Document.Revision).IsNotEqual(originalRevision);
        AssertThat(disabled.Document.Nodes.Select(node => node.Position).ToArray())
            .ContainsExactly(
                new PresentationNodePosition(0f, 0f),
                new PresentationNodePosition(280f, 0f),
                new PresentationNodePosition(560f, 0f),
                new PresentationNodePosition(840f, 0f));
        AssertThat(moved.Succeeded).IsTrue();
        AssertThat(moved.Document.Nodes.Single(node => node.NodeId == "action").Position)
            .IsEqual(new PresentationNodePosition(320f, 40f));
        AssertThat(successfullyReloaded?.AuthoringNodePositions[1] ?? global::Godot.Vector2.Zero)
            .IsEqual(new global::Godot.Vector2(320f, 40f));
        AssertThat(presentation.BuildExecutionPlan().Nodes.Count).IsEqual(2);
        AssertThat(rejected.Succeeded).IsFalse();
        AssertThat(rejected.Diagnostics.Single().Code).IsEqual("presentation.revision_conflict");
        AssertThat(rollbackTriggered).IsTrue();
        AssertThat(afterRollback).ContainsExactly(beforeRollback);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void PresentationPreviewFrame_ConfiguresCenteredAspectRatioFit()
    {
        global::Godot.AspectRatioContainer frame =
            Tactics.Godot.Adapter.Editor.TacticsGraphWorkbench.CreatePreviewFrame();
        float expectedRatio = 640f / 180f;
        AssertThat(frame.StretchMode)
            .IsEqual(global::Godot.AspectRatioContainer.StretchModeEnum.Fit);
        AssertThat(global::Godot.Mathf.IsEqualApprox(frame.Ratio, expectedRatio)).IsTrue();
        AssertThat(frame.AlignmentHorizontal)
            .IsEqual(global::Godot.AspectRatioContainer.AlignmentMode.Center);
        AssertThat(frame.AlignmentVertical)
            .IsEqual(global::Godot.AspectRatioContainer.AlignmentMode.Center);
        AssertThat(frame.ClipContents).IsTrue();
        frame.Free();
    }

    private static BattleCommand CreateCommand(JsonElement vector)
    {
        var actorId = new UnitInstanceId(vector.GetProperty("actorId").GetString()!);
        string type = vector.GetProperty("type").GetString()!;
        if (type == "move")
            return new MoveUnitCommand(actorId, Point(vector.GetProperty("destination")));
        if (type == "end-turn")
            return new EndTurnCommand(actorId);
        if (type == "use-poison-spear")
        {
            JsonElement skill = vector.GetProperty("skill");
            return new UsePoisonSpearCommand(
                actorId,
                new UnitInstanceId(vector.GetProperty("targetId").GetString()!),
                new PoisonSpearDefinition(
                    new ContentId(skill.GetProperty("contentId").GetString()!),
                    skill.GetProperty("range").GetInt32(),
                     skill.GetProperty("damage").GetInt32(),
                     skill.GetProperty("poisonTurns").GetInt32(),
                     new ContentId(skill.GetProperty("poisonStatusId").GetString()!),
                     skill.GetProperty("poisonDamagePerTurn").GetInt32(),
                     skill.GetProperty("manaCost").GetInt32(),
                     skill.GetProperty("dropSearchRadius").GetInt32()));
        }

        throw new InvalidOperationException($"Unsupported Golden command '{type}'.");
    }

    private static Tactics.Godot.Adapter.Runtime.PoisonSpearPresentationResource
        CreatePresentationResourceForMutation()
    {
        return new Tactics.Godot.Adapter.Runtime.PoisonSpearPresentationResource
        {
            ContentIdValue = "presentation.poison-spear.lv1",
            SchemaVersion = 1,
            NodeIds = new[] { "__poison_spear.runtime", "action", "projectile" },
            NodeTypes = new[] { "sequence", "unit.tween.ranged", "projectile.flight-impact" },
            NodeChildren = new[] { "action,projectile", string.Empty, string.Empty },
            PlanRootNodeId = "__poison_spear.runtime",
            AuthoringNodeIds = new[] { "entry", "action", "projectile", "finish" },
            AuthoringNodeTypes = new[]
            {
                "PresentationEntryNodeRecord",
                "PresentationUnitTweenNodeRecord",
                "PresentationProjectileNodeRecord",
                "PresentationFinishNodeRecord"
            },
            AuthoringNodeKinds = new[] { "entry", "leaf", "leaf", "finish" },
            AuthoringNodeCues = new[] { "Action", string.Empty, string.Empty, string.Empty },
            AuthoringNodeEnabled = new[] { 1, 1, 1, 1 },
            AuthoringNodePositions = new[]
            {
                new global::Godot.Vector2(0f, 0f),
                new global::Godot.Vector2(280f, 0f),
                new global::Godot.Vector2(560f, 0f),
                new global::Godot.Vector2(840f, 0f)
            },
            EdgeIds = new[] { "edge.1", "edge.2", "edge.3" },
            EdgeSources = new[] { "entry", "action", "projectile" },
            EdgeTargets = new[] { "action", "projectile", "finish" }
        };
    }

    private static BoardSnapshot CreateBoard(JsonElement scenario)
    {
        var movementBlockers = scenario.GetProperty("movementBlockers").EnumerateArray()
            .Select(Point)
            .ToHashSet();
        var lineOfSightBlockers = scenario.GetProperty("lineOfSightBlockers").EnumerateArray()
            .Select(Point)
            .ToHashSet();
        var cells = new Dictionary<GridPoint, CellState>();
        for (int x = 0; x < BoardSpec.Width; x++)
        {
            for (int y = 0; y < BoardSpec.Height; y++)
            {
                var point = new GridPoint(x, y);
                cells[point] = new CellState(
                    blocksMovement: movementBlockers.Contains(point),
                    blocksLineOfSight: lineOfSightBlockers.Contains(point));
            }
        }

        return new BoardSnapshot(cells);
    }

    private static string EventType(BattleEvent battleEvent) => battleEvent switch
    {
        UnitMovedEvent => "unit-moved",
        SkillUsedEvent => "skill-used",
        ManaSpentEvent => "mana-spent",
        DamageAppliedEvent => "damage-applied",
        StatusAppliedEvent => "status-applied",
        StatusTickedEvent => "status-ticked",
        StatusDurationChangedEvent => "status-duration-changed",
        StatusExpiredEvent => "status-expired",
        SpearDroppedEvent => "spear-dropped",
        UnitDefeatedEvent => "unit-defeated",
        TurnAdvancedEvent => "turn-advanced",
        CommandRejectedEvent => "command-rejected",
        _ => throw new InvalidOperationException($"Unsupported event type {battleEvent.GetType().Name}.")
    };

    private static GridPoint Point(JsonElement value) => new(value[0].GetInt32(), value[1].GetInt32());

    private static InitiativeEntry Initiative(JsonElement entry) => new(
        new UnitInstanceId(entry.GetProperty("instanceId").GetString()!),
        entry.GetProperty("initiative").GetSingle(),
        entry.GetProperty("playerNumber").GetInt32(),
        entry.GetProperty("spawnOrdinal").GetInt32());

    private static PresentationGraphNode CreatePresentationGraphNode(JsonElement node)
    {
        string kind = node.GetProperty("kind").GetString()!;
        return new PresentationGraphNode(
            node.GetProperty("id").GetString()!,
            node.GetProperty("nodeTypeId").GetString()!,
            kind switch
            {
                "entry" => PresentationGraphNodeKind.Entry,
                "finish" => PresentationGraphNodeKind.Finish,
                "fork" => PresentationGraphNodeKind.Fork,
                "join" => PresentationGraphNodeKind.Join,
                "leaf" => PresentationGraphNodeKind.Leaf,
                _ => throw new InvalidOperationException($"Unsupported Golden presentation node kind '{kind}'.")
            },
            !node.TryGetProperty("enabled", out JsonElement enabled) || enabled.GetBoolean(),
            kind == "entry" ? node.GetProperty("cueId").GetString() : null,
            kind == "fork" ? node.GetProperty("joinNodeId").GetString() : null);
    }

    private static string Snapshot(PresentationExecutionPlan plan, string nodeId)
    {
        PresentationNode node = plan.Nodes[nodeId];
        return node.Kind switch
        {
            PresentationNodeKind.Leaf => $"L({node.NodeId})",
            PresentationNodeKind.Sequence =>
                $"S[{string.Join(",", node.Children.Select(child => Snapshot(plan, child)))}]",
            PresentationNodeKind.Parallel =>
                $"P({node.ForkNodeId}->{node.JoinNodeId})[{string.Join("|", node.Children.Select(child => Snapshot(plan, child)))}]",
            _ => throw new InvalidOperationException($"Unsupported presentation node kind '{node.Kind}'.")
        };
    }
}
