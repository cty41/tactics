using System.Text.Json;
using NUnit.Framework;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Combat;
using Tactics.Core.Content;
using Tactics.Core.Pathfinding;
using Tactics.Core.Presentation;
using Tactics.Core.Randomness;
using Tactics.Core.Runtime;
using Tactics.Core.Turns;
using Tactics.Core.Units;

namespace Tactics.Core.Tests;

public sealed class GoldenVectorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Test]
    public void SharedGoldenVector_ReplaysCurrentCoreContracts()
    {
        GoldenDocument golden = LoadGolden();
        Assert.Multiple(() =>
        {
            Assert.That(golden.SchemaVersion, Is.EqualTo(6));
            Assert.That(golden.SourceOracle.UnityCommit, Is.EqualTo("168d1934"));
            Assert.That(golden.Board.Width, Is.EqualTo(BoardSpec.Width));
            Assert.That(golden.Board.Height, Is.EqualTo(BoardSpec.Height));
        });

        foreach (PathVector vector in golden.PathQueries)
        {
            Assert.That(vector.Algorithm, Is.EqualTo("deterministic-dijkstra-unity-final-v1"), vector.Id);
            IReadOnlyList<GridPoint> actual = new DeterministicDijkstraPathfinder().FindPath(
                CreateBoard(movementBlockers: vector.MovementBlockers),
                Point(vector.Origin),
                Point(vector.Destination));
            Assert.That(actual, Is.EqualTo(vector.ExpectedPath.Select(Point)), vector.Id);
        }

        foreach (LineOfSightVector vector in golden.LineOfSightQueries)
        {
            bool actual = new SupercoverLineOfSight().HasLineOfSight(
                CreateBoard(lineOfSightBlockers: vector.LineOfSightBlockers),
                Point(vector.Origin),
                Point(vector.Target));
            Assert.That(actual, Is.EqualTo(vector.ExpectedVisible), vector.Id);
        }

        foreach (MovementVector vector in golden.MovementCases)
        {
            var movement = new MovementActionState(vector.MoveRange);
            foreach (MovementAttempt attempt in vector.Attempts)
                Assert.That(movement.TryUseMove(attempt.PathLength), Is.EqualTo(attempt.ExpectedAccepted), vector.Id);
            movement.PrepareForTurn();
            Assert.That(movement.TryUseMove(vector.PostResetPathLength), Is.EqualTo(vector.ExpectedPostResetAccepted), vector.Id);
        }

        foreach (InitiativeVector vector in golden.InitiativeCases)
        {
            IReadOnlyList<InitiativeEntry> actual = InitiativeOrder.Sort(vector.Entries.Select(entry =>
                new InitiativeEntry(
                    new UnitInstanceId(entry.InstanceId),
                    entry.Initiative,
                    entry.PlayerNumber,
                    entry.SpawnOrdinal)));
            Assert.That(actual.Select(entry => entry.UnitId.Value), Is.EqualTo(vector.ExpectedOrder), vector.Id);
        }

        foreach (InitiativeRoundVector vector in golden.InitiativeRoundCases)
        {
            var entries = vector.Entries.ToDictionary(
                entry => entry.InstanceId,
                CreateInitiativeEntry,
                StringComparer.Ordinal);
            InitiativeRoundState round = InitiativeRoundState.StartRound(entries.Values);
            InitiativeTakeNextResult first = round.TakeNext(entries.Values);
            Assert.That(first.Current?.UnitId.Value, Is.EqualTo(vector.ExpectedFirst), vector.Id);

            InitiativeChange changed = vector.Changes.Single();
            entries[changed.InstanceId] = entries[changed.InstanceId] with { Initiative = changed.Initiative };
            round = first.State.NotifyInitiativeChanged(entries[changed.InstanceId]);
            Assert.That(
                round.GetCurrentRoundOrder().Select(entry => entry.UnitId.Value),
                Is.EqualTo(vector.ExpectedOrderAfterChange),
                vector.Id);

            InitiativeChange currentChange = vector.CurrentInitiativeChange;
            entries[currentChange.InstanceId] = entries[currentChange.InstanceId] with
            {
                Initiative = currentChange.Initiative
            };
            round = round.NotifyInitiativeChanged(entries[currentChange.InstanceId]);
            Assert.That(
                round.GetCurrentRoundOrder().Select(entry => entry.UnitId.Value),
                Is.EqualTo(vector.ExpectedOrderAfterChange),
                vector.Id);

            InitiativeTakeNextResult second = round.TakeNext(entries.Values);
            Assert.Multiple(() =>
            {
                Assert.That(second.Current?.UnitId.Value, Is.EqualTo(vector.ExpectedSecond), vector.Id);
                Assert.That(second.State.Acted.Select(id => id.Value), Is.EquivalentTo(vector.ExpectedActedAfterSecond), vector.Id);
                Assert.That(second.State.Remaining.Select(entry => entry.UnitId.Value), Is.EqualTo(vector.ExpectedRemainingAfterSecond), vector.Id);
            });
        }

        foreach (PresentationVector vector in golden.PresentationCases)
        {
            var graph = new PresentationGraphDefinition(
                vector.SchemaVersion,
                vector.Nodes.Select(CreatePresentationNode),
                vector.Edges.Select((edge, index) => new PresentationGraphEdge(edge.Source, edge.Target, index)));
            PresentationExecutionPlan plan = PresentationGraphCompiler.Compile(graph, vector.CueId);
            Assert.That(Snapshot(plan, plan.RootNodeId), Is.EqualTo(vector.ExpectedSnapshot), vector.Id);
        }

        foreach (ActionVector vector in golden.ActionCases)
        {
            ActionResult actual = new PoisonSpearResolver().Resolve(
                CreateBoard(lineOfSightBlockers: vector.LineOfSightBlockers),
                Unit(vector.Caster),
                Unit(vector.Target),
                new PoisonSpearDefinition(
                    new ContentId(vector.Skill.ContentId),
                     vector.Skill.Range,
                     vector.Skill.Damage,
                     vector.Skill.PoisonTurns,
                     poisonDamagePerTurn: vector.Skill.PoisonDamagePerTurn,
                     manaCost: vector.Skill.ManaCost,
                     dropSearchRadius: vector.Skill.DropSearchRadius));
            Assert.That(actual, Is.EqualTo(new ActionResult(
                vector.Expected.Succeeded,
                vector.Expected.Damage,
                vector.Expected.PoisonTurns,
                vector.Expected.FailureReason)), vector.Id);
        }

        foreach (RandomVector vector in golden.RandomCases)
        {
            Assert.That(vector.Algorithm, Is.EqualTo(DeterministicRandom.AlgorithmId), vector.Id);
            var random = new DeterministicRandom(ulong.Parse(vector.Seed));
            foreach (RandomOperation operation in vector.Operations)
            {
                if (operation.Type == "uint64")
                    Assert.That(random.NextUInt64(), Is.EqualTo(ulong.Parse(operation.Expected)), vector.Id);
                else if (operation.Type == "bounded-int")
                    Assert.That(random.NextInt(operation.ExclusiveUpperBound), Is.EqualTo(int.Parse(operation.Expected)), vector.Id);
                else
                    Assert.Fail($"{vector.Id}: unsupported random operation '{operation.Type}'.");
            }

            Assert.That(random.State, Is.EqualTo(ulong.Parse(vector.ExpectedFinalState)), vector.Id);
        }

        foreach (BattleScenarioVector vector in golden.BattleScenarios)
            ReplayBattleScenario(vector);
    }

    [Test]
    public void OracleMatrix_BindsPoisonSpearToRealExportAndFrozenRuntimeSources()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Golden", "oracle-matrix.json");
        using JsonDocument matrix = JsonDocument.Parse(File.ReadAllText(path));
        string[] statuses = matrix.RootElement.GetProperty("contracts")
            .EnumerateArray()
            .Select(item => item.GetProperty("status").GetString() ?? string.Empty)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(statuses, Does.Contain("unity_final_passed"));
            Assert.That(statuses, Does.Contain("unity_final_linked_source_oracle"));
            Assert.That(statuses, Does.Not.Contain("missing_dedicated_unity_oracle"));
            Assert.That(statuses, Does.Contain("unity_final_asset_export_and_linked_source_oracle"));
            Assert.That(statuses, Does.Not.Contain("real_asset_export_and_oracle_pending"));
        });
    }

    [Test]
    public void ContractDecisions_DeclareVersionedTransitionAndDeterministicRngReplacement()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Golden", "contract-decisions.json");
        using JsonDocument decisions = JsonDocument.Parse(File.ReadAllText(path));
        IReadOnlyDictionary<string, JsonElement> contracts = decisions.RootElement.GetProperty("contracts")
            .EnumerateArray()
            .ToDictionary(item => item.GetProperty("id").GetString()!, StringComparer.Ordinal);
        JsonElement battle = contracts["battle.command-event-transition"];
        JsonElement random = contracts["random.splitmix64-v1"];

        Assert.Multiple(() =>
        {
            Assert.That(decisions.RootElement.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(1));
            Assert.That(battle.GetProperty("resolution").GetString(), Is.EqualTo("versioned_migration_contract"));
            Assert.That(battle.GetProperty("runtimeContractId").GetString(), Is.EqualTo(BattleTransitionService.ContractId));
            Assert.That(random.GetProperty("resolution").GetString(), Is.EqualTo("deterministic_replacement_contract"));
            Assert.That(random.GetProperty("runtimeContractId").GetString(), Is.EqualTo(DeterministicRandom.AlgorithmId));
        });
    }

    [Test]
    public async Task RuntimeScopeGolden_ReplaysFrozenOwnershipFaultAndReentrantDispose()
    {
        RuntimeScopeVector vector = LoadGolden().RuntimeScopeCases.Single();
        using (var scope = new BattleRuntimeScope())
        {
            bool acceptedNull = scope.TryTrack(null);
            bool acceptedCompleted = scope.TryTrack(Task.CompletedTask);
            scope.Track(Task.FromException(new InvalidOperationException(vector.FaultMessage)));
            AggregateException fault = Assert.ThrowsAsync<AggregateException>(
                async () => await scope.WhenIdleAsync())!;
            scope.Cancel();

            Assert.Multiple(() =>
            {
                Assert.That(acceptedNull, Is.EqualTo(vector.ExpectedAcceptedNull), vector.Id);
                Assert.That(acceptedCompleted, Is.EqualTo(vector.ExpectedAcceptedCompleted), vector.Id);
                Assert.That(scope.TryTrack(Task.CompletedTask), Is.EqualTo(vector.ExpectedAcceptedAfterCancel), vector.Id);
                Assert.That(fault.InnerExceptions.Select(item => item.Message), Is.EqualTo(new[] { vector.FaultMessage }), vector.Id);
            });
        }

        var reentrant = new BattleRuntimeScope();
        var tracked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        reentrant.Track(tracked.Task);
        using CancellationTokenRegistration registration = reentrant.Token.Register(reentrant.Dispose);
        reentrant.Cancel();
        tracked.SetResult();
        await reentrant.WhenIdleAsync();
        reentrant.Dispose();
        Assert.That(vector.ExpectedReentrantDisposeDrain, Is.True, vector.Id);
    }

    private static GoldenDocument LoadGolden()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Golden", "10x10-core-vectors.json");
        return JsonSerializer.Deserialize<GoldenDocument>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidOperationException("Golden vector JSON is empty.");
    }

    private static BoardSnapshot CreateBoard(int[][]? movementBlockers = null, int[][]? lineOfSightBlockers = null)
    {
        var movement = (movementBlockers ?? Array.Empty<int[]>()).Select(Point).ToHashSet();
        var sight = (lineOfSightBlockers ?? Array.Empty<int[]>()).Select(Point).ToHashSet();
        var cells = new Dictionary<GridPoint, CellState>();
        for (int x = 0; x < BoardSpec.Width; x++)
        {
            for (int y = 0; y < BoardSpec.Height; y++)
            {
                var point = new GridPoint(x, y);
                cells[point] = new CellState(blocksMovement: movement.Contains(point), blocksLineOfSight: sight.Contains(point));
            }
        }

        return new BoardSnapshot(cells);
    }

    private static GridPoint Point(int[] value) => new(value[0], value[1]);

    private static InitiativeEntry CreateInitiativeEntry(InitiativeInput value) => new(
        new UnitInstanceId(value.InstanceId),
        value.Initiative,
        value.PlayerNumber,
        value.SpawnOrdinal);

    private static PresentationGraphNode CreatePresentationNode(PresentationNodeVector value) => new(
        value.Id,
        value.NodeTypeId,
        value.Kind switch
        {
            "entry" => PresentationGraphNodeKind.Entry,
            "finish" => PresentationGraphNodeKind.Finish,
            "fork" => PresentationGraphNodeKind.Fork,
            "join" => PresentationGraphNodeKind.Join,
            "leaf" => PresentationGraphNodeKind.Leaf,
            _ => throw new InvalidOperationException($"Unsupported Golden presentation node kind '{value.Kind}'.")
        },
        value.Enabled,
        value.CueId,
        value.JoinNodeId);

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

    private static void ReplayBattleScenario(BattleScenarioVector vector)
    {
        var units = vector.Units.Select(unit => new BattleUnitState(
            Unit(unit),
            unit.MaxHealth,
            unit.CurrentHealth,
            unit.HasMovedThisTurn,
            maxMana: unit.MaxMana,
            currentMana: unit.CurrentMana,
            statuses: unit.Statuses.ToDictionary(
                status => new ContentId(status.ContentId),
                status => new BattleStatusState(
                    new ContentId(status.ContentId),
                    new UnitInstanceId(string.IsNullOrWhiteSpace(status.SourceId)
                        ? unit.InstanceId
                        : status.SourceId),
                    status.RemainingTurns,
                    status.DamagePerTurn))));
        var state = new BattleState(
            CreateBoard(vector.MovementBlockers, vector.LineOfSightBlockers),
            units,
            vector.TurnOrder.Select(item => new UnitInstanceId(item)).ToArray(),
            vector.Round,
            vector.ActiveIndex,
            ulong.Parse(vector.RandomState));
        var service = new BattleTransitionService();

        foreach (BattleCommandVector commandVector in vector.Commands)
        {
            BattleTransition transition = service.Apply(state, CreateCommand(commandVector));
            Assert.That(transition.Succeeded, Is.EqualTo(commandVector.ExpectedSucceeded), vector.Id);
            AssertEvents(transition.Events, commandVector.ExpectedEvents, vector.Id);
            state = transition.State;
        }

        ExpectedBattleState expected = vector.ExpectedFinalState;
        Assert.Multiple(() =>
        {
            Assert.That(state.RandomState, Is.EqualTo(ulong.Parse(expected.RandomState)), vector.Id);
            Assert.That(state.Round, Is.EqualTo(expected.Round), vector.Id);
            Assert.That(state.ActiveIndex, Is.EqualTo(expected.ActiveIndex), vector.Id);
            Assert.That(state.ActiveUnitId, Is.EqualTo(new UnitInstanceId(expected.ActiveUnitId)), vector.Id);
            Assert.That(
                state.DroppedSpears.OrderBy(item => item.Key.Value)
                    .Select(item => $"{item.Key.Value}:{item.Value.X},{item.Value.Y}"),
                Is.EqualTo(expected.DroppedSpears.OrderBy(item => item.OwnerId)
                    .Select(item => $"{item.OwnerId}:{item.Cell[0]},{item.Cell[1]}")),
                vector.Id);
        });

        foreach (ExpectedBattleUnit expectedUnit in expected.Units)
        {
            BattleUnitState actual = state.Units[new UnitInstanceId(expectedUnit.InstanceId)];
            Assert.Multiple(() =>
            {
                Assert.That(actual.Unit.InstanceId, Is.EqualTo(new UnitInstanceId(expectedUnit.InstanceId)), vector.Id);
                Assert.That(actual.Unit.DefinitionId, Is.EqualTo(new ContentId(expectedUnit.DefinitionId)), vector.Id);
                Assert.That(actual.Unit.Position, Is.EqualTo(Point(expectedUnit.Cell)), vector.Id);
                Assert.That(actual.MaxHealth, Is.EqualTo(expectedUnit.MaxHealth), vector.Id);
                Assert.That(actual.CurrentHealth, Is.EqualTo(expectedUnit.CurrentHealth), vector.Id);
                Assert.That(actual.MaxMana, Is.EqualTo(expectedUnit.MaxMana), vector.Id);
                Assert.That(actual.CurrentMana, Is.EqualTo(expectedUnit.CurrentMana), vector.Id);
                Assert.That(actual.HasMovedThisTurn, Is.EqualTo(expectedUnit.HasMovedThisTurn), vector.Id);
                Assert.That(
                    actual.Statuses.Values.OrderBy(item => item.ContentId.Value)
                        .Select(item => $"{item.ContentId.Value}:{item.SourceId.Value}:{item.RemainingTurns}:{item.DamagePerTurn}"),
                    Is.EqualTo(expectedUnit.Statuses.OrderBy(item => item.ContentId)
                        .Select(item => $"{item.ContentId}:{item.SourceId}:{item.RemainingTurns}:{item.DamagePerTurn}")),
                    vector.Id);
            });
        }
    }

    private static BattleCommand CreateCommand(BattleCommandVector vector)
    {
        var actorId = new UnitInstanceId(vector.ActorId);
        return vector.Type switch
        {
            "move" => new MoveUnitCommand(actorId, Point(vector.Destination)),
            "use-poison-spear" => new UsePoisonSpearCommand(
                actorId,
                new UnitInstanceId(vector.TargetId),
                new PoisonSpearDefinition(
                    new ContentId(vector.Skill.ContentId),
                    vector.Skill.Range,
                     vector.Skill.Damage,
                     vector.Skill.PoisonTurns,
                     new ContentId(vector.Skill.PoisonStatusId),
                     vector.Skill.PoisonDamagePerTurn,
                     vector.Skill.ManaCost,
                     vector.Skill.DropSearchRadius)),
            "end-turn" => new EndTurnCommand(actorId),
            _ => throw new InvalidOperationException($"Unsupported Golden command type '{vector.Type}'.")
        };
    }

    private static void AssertEvents(
        IReadOnlyList<BattleEvent> actualEvents,
        IReadOnlyList<ExpectedBattleEvent> expectedEvents,
        string scenarioId)
    {
        Assert.That(actualEvents, Has.Count.EqualTo(expectedEvents.Count), scenarioId);
        for (int index = 0; index < expectedEvents.Count; index++)
        {
            ExpectedBattleEvent expected = expectedEvents[index];
            switch (actualEvents[index])
            {
                case UnitMovedEvent moved when expected.Type == "unit-moved":
                    Assert.Multiple(() =>
                    {
                        Assert.That(moved.UnitId, Is.EqualTo(new UnitInstanceId(expected.UnitId)), scenarioId);
                        Assert.That(moved.Origin, Is.EqualTo(Point(expected.Origin)), scenarioId);
                        Assert.That(moved.Destination, Is.EqualTo(Point(expected.Destination)), scenarioId);
                        Assert.That(moved.Path, Is.EqualTo(expected.Path.Select(Point)), scenarioId);
                    });
                    break;
                case SkillUsedEvent used when expected.Type == "skill-used":
                    Assert.Multiple(() =>
                    {
                        Assert.That(used.ActorId, Is.EqualTo(new UnitInstanceId(expected.ActorId)), scenarioId);
                        Assert.That(used.TargetId, Is.EqualTo(new UnitInstanceId(expected.TargetId)), scenarioId);
                        Assert.That(used.SkillId, Is.EqualTo(new ContentId(expected.SkillId)), scenarioId);
                    });
                    break;
                case ManaSpentEvent mana when expected.Type == "mana-spent":
                    Assert.Multiple(() =>
                    {
                        Assert.That(mana.UnitId, Is.EqualTo(new UnitInstanceId(expected.UnitId)), scenarioId);
                        Assert.That(mana.SkillId, Is.EqualTo(new ContentId(expected.SkillId)), scenarioId);
                        Assert.That(mana.Amount, Is.EqualTo(expected.Amount), scenarioId);
                        Assert.That(mana.RemainingMana, Is.EqualTo(expected.RemainingMana), scenarioId);
                    });
                    break;
                case DamageAppliedEvent damage when expected.Type == "damage-applied":
                    Assert.Multiple(() =>
                    {
                        Assert.That(damage.SourceId, Is.EqualTo(new UnitInstanceId(expected.ActorId)), scenarioId);
                        Assert.That(damage.TargetId, Is.EqualTo(new UnitInstanceId(expected.TargetId)), scenarioId);
                        Assert.That(damage.SkillId, Is.EqualTo(new ContentId(expected.SkillId)), scenarioId);
                        Assert.That(damage.Amount, Is.EqualTo(expected.Amount), scenarioId);
                        Assert.That(damage.RemainingHealth, Is.EqualTo(expected.RemainingHealth), scenarioId);
                    });
                    break;
                case StatusAppliedEvent status when expected.Type == "status-applied":
                    Assert.Multiple(() =>
                    {
                        Assert.That(status.SourceId, Is.EqualTo(new UnitInstanceId(expected.ActorId)), scenarioId);
                        Assert.That(status.TargetId, Is.EqualTo(new UnitInstanceId(expected.TargetId)), scenarioId);
                        Assert.That(status.StatusId, Is.EqualTo(new ContentId(expected.StatusId)), scenarioId);
                        Assert.That(status.RemainingTurns, Is.EqualTo(expected.RemainingTurns), scenarioId);
                    });
                    break;
                case StatusTickedEvent tick when expected.Type == "status-ticked":
                    Assert.Multiple(() =>
                    {
                        Assert.That(tick.SourceId, Is.EqualTo(new UnitInstanceId(expected.ActorId)), scenarioId);
                        Assert.That(tick.TargetId, Is.EqualTo(new UnitInstanceId(expected.TargetId)), scenarioId);
                        Assert.That(tick.StatusId, Is.EqualTo(new ContentId(expected.StatusId)), scenarioId);
                        Assert.That(tick.Amount, Is.EqualTo(expected.Amount), scenarioId);
                        Assert.That(tick.RemainingHealth, Is.EqualTo(expected.RemainingHealth), scenarioId);
                    });
                    break;
                case SpearDroppedEvent spear when expected.Type == "spear-dropped":
                    Assert.Multiple(() =>
                    {
                        Assert.That(spear.OwnerId, Is.EqualTo(new UnitInstanceId(expected.UnitId)), scenarioId);
                        Assert.That(spear.Cell, Is.EqualTo(Point(expected.Cell)), scenarioId);
                    });
                    break;
                case TurnAdvancedEvent turn when expected.Type == "turn-advanced":
                    Assert.Multiple(() =>
                    {
                        Assert.That(turn.PreviousUnitId, Is.EqualTo(new UnitInstanceId(expected.ActorId)), scenarioId);
                        Assert.That(turn.ActiveUnitId, Is.EqualTo(new UnitInstanceId(expected.ActiveUnitId)), scenarioId);
                        Assert.That(turn.Round, Is.EqualTo(expected.Round), scenarioId);
                    });
                    break;
                default:
                    Assert.Fail($"{scenarioId}: event {index} was {actualEvents[index].GetType().Name}, expected {expected.Type}.");
                    break;
            }
        }
    }

    private static UnitState Unit(UnitVector value) => new(
        new UnitInstanceId(value.InstanceId),
        new ContentId(value.DefinitionId),
        Point(value.Cell),
        value.MoveRange,
        value.Initiative,
        value.PlayerNumber,
        value.SpawnOrdinal);

    private sealed class GoldenDocument
    {
        public int SchemaVersion { get; init; }
        public SourceOracle SourceOracle { get; init; } = new();
        public BoardVector Board { get; init; } = new();
        public PathVector[] PathQueries { get; init; } = Array.Empty<PathVector>();
        public LineOfSightVector[] LineOfSightQueries { get; init; } = Array.Empty<LineOfSightVector>();
        public MovementVector[] MovementCases { get; init; } = Array.Empty<MovementVector>();
        public InitiativeVector[] InitiativeCases { get; init; } = Array.Empty<InitiativeVector>();
        public InitiativeRoundVector[] InitiativeRoundCases { get; init; } = Array.Empty<InitiativeRoundVector>();
        public RuntimeScopeVector[] RuntimeScopeCases { get; init; } = Array.Empty<RuntimeScopeVector>();
        public PresentationVector[] PresentationCases { get; init; } = Array.Empty<PresentationVector>();
        public RandomVector[] RandomCases { get; init; } = Array.Empty<RandomVector>();
        public ActionVector[] ActionCases { get; init; } = Array.Empty<ActionVector>();
        public BattleScenarioVector[] BattleScenarios { get; init; } = Array.Empty<BattleScenarioVector>();
    }

    private sealed class SourceOracle { public string UnityCommit { get; init; } = string.Empty; }
    private sealed class BoardVector { public int Width { get; init; } public int Height { get; init; } }
    private sealed class PathVector { public string Id { get; init; } = string.Empty; public int[] Origin { get; init; } = []; public int[] Destination { get; init; } = []; public int[][] MovementBlockers { get; init; } = []; public string Algorithm { get; init; } = string.Empty; public int[][] ExpectedPath { get; init; } = []; }
    private sealed class LineOfSightVector { public string Id { get; init; } = string.Empty; public int[] Origin { get; init; } = []; public int[] Target { get; init; } = []; public int[][] LineOfSightBlockers { get; init; } = []; public bool ExpectedVisible { get; init; } }
    private sealed class MovementVector { public string Id { get; init; } = string.Empty; public int MoveRange { get; init; } public MovementAttempt[] Attempts { get; init; } = []; public int PostResetPathLength { get; init; } public bool ExpectedPostResetAccepted { get; init; } }
    private sealed class MovementAttempt { public int PathLength { get; init; } public bool ExpectedAccepted { get; init; } }
    private sealed class InitiativeVector { public string Id { get; init; } = string.Empty; public InitiativeInput[] Entries { get; init; } = []; public string[] ExpectedOrder { get; init; } = []; }
    private sealed class InitiativeInput { public string InstanceId { get; init; } = string.Empty; public string DefinitionId { get; init; } = string.Empty; public float Initiative { get; init; } public int PlayerNumber { get; init; } public int SpawnOrdinal { get; init; } }
    private sealed class InitiativeRoundVector { public string Id { get; init; } = string.Empty; public InitiativeInput[] Entries { get; init; } = []; public string ExpectedFirst { get; init; } = string.Empty; public InitiativeChange[] Changes { get; init; } = []; public InitiativeChange CurrentInitiativeChange { get; init; } = new(); public string[] ExpectedOrderAfterChange { get; init; } = []; public string ExpectedSecond { get; init; } = string.Empty; public string[] ExpectedActedAfterSecond { get; init; } = []; public string[] ExpectedRemainingAfterSecond { get; init; } = []; }
    private sealed class InitiativeChange { public string InstanceId { get; init; } = string.Empty; public float Initiative { get; init; } }
    private sealed class RuntimeScopeVector { public string Id { get; init; } = string.Empty; public string FaultMessage { get; init; } = string.Empty; public bool ExpectedAcceptedNull { get; init; } public bool ExpectedAcceptedCompleted { get; init; } public bool ExpectedAcceptedAfterCancel { get; init; } public bool ExpectedReentrantDisposeDrain { get; init; } }
    private sealed class PresentationVector { public string Id { get; init; } = string.Empty; public int SchemaVersion { get; init; } public string CueId { get; init; } = string.Empty; public PresentationNodeVector[] Nodes { get; init; } = []; public PresentationEdgeVector[] Edges { get; init; } = []; public string ExpectedSnapshot { get; init; } = string.Empty; }
    private sealed class PresentationNodeVector { public string Id { get; init; } = string.Empty; public string NodeTypeId { get; init; } = string.Empty; public string Kind { get; init; } = string.Empty; public bool Enabled { get; init; } = true; public string? CueId { get; init; } public string? JoinNodeId { get; init; } }
    private sealed class PresentationEdgeVector { public string Source { get; init; } = string.Empty; public string Target { get; init; } = string.Empty; }
    private sealed class ActionVector { public string Id { get; init; } = string.Empty; public UnitVector Caster { get; init; } = new(); public UnitVector Target { get; init; } = new(); public SkillVector Skill { get; init; } = new(); public int[][] LineOfSightBlockers { get; init; } = []; public ActionExpected Expected { get; init; } = new(); }
    private sealed class UnitVector { public string InstanceId { get; init; } = string.Empty; public string DefinitionId { get; init; } = string.Empty; public int[] Cell { get; init; } = []; public int MoveRange { get; init; } public float Initiative { get; init; } public int PlayerNumber { get; init; } public int SpawnOrdinal { get; init; } public int MaxHealth { get; init; } public int CurrentHealth { get; init; } public int MaxMana { get; init; } public int CurrentMana { get; init; } public bool HasMovedThisTurn { get; init; } public StatusVector[] Statuses { get; init; } = []; }
    private sealed class SkillVector { public string ContentId { get; init; } = string.Empty; public int Range { get; init; } public int Damage { get; init; } public int PoisonTurns { get; init; } public int PoisonDamagePerTurn { get; init; } = 2; public int ManaCost { get; init; } public int DropSearchRadius { get; init; } = 3; public string PoisonStatusId { get; init; } = "buff.poison"; }
    private sealed class ActionExpected { public bool Succeeded { get; init; } public int Damage { get; init; } public int PoisonTurns { get; init; } public string FailureReason { get; init; } = string.Empty; }
    private sealed class RandomVector { public string Id { get; init; } = string.Empty; public string Algorithm { get; init; } = string.Empty; public string Seed { get; init; } = string.Empty; public RandomOperation[] Operations { get; init; } = []; public string ExpectedFinalState { get; init; } = string.Empty; }
    private sealed class RandomOperation { public string Type { get; init; } = string.Empty; public int ExclusiveUpperBound { get; init; } public string Expected { get; init; } = string.Empty; }
    private sealed class BattleScenarioVector { public string Id { get; init; } = string.Empty; public string RandomState { get; init; } = string.Empty; public int Round { get; init; } public int ActiveIndex { get; init; } public string[] TurnOrder { get; init; } = []; public int[][] MovementBlockers { get; init; } = []; public int[][] LineOfSightBlockers { get; init; } = []; public UnitVector[] Units { get; init; } = []; public BattleCommandVector[] Commands { get; init; } = []; public ExpectedBattleState ExpectedFinalState { get; init; } = new(); }
    private sealed class BattleCommandVector { public string Type { get; init; } = string.Empty; public string ActorId { get; init; } = string.Empty; public string TargetId { get; init; } = string.Empty; public int[] Destination { get; init; } = []; public SkillVector Skill { get; init; } = new(); public bool ExpectedSucceeded { get; init; } public ExpectedBattleEvent[] ExpectedEvents { get; init; } = []; }
    private sealed class ExpectedBattleEvent { public string Type { get; init; } = string.Empty; public string ActorId { get; init; } = string.Empty; public string UnitId { get; init; } = string.Empty; public string TargetId { get; init; } = string.Empty; public string SkillId { get; init; } = string.Empty; public string StatusId { get; init; } = string.Empty; public string ActiveUnitId { get; init; } = string.Empty; public int[] Cell { get; init; } = []; public int[] Origin { get; init; } = []; public int[] Destination { get; init; } = []; public int[][] Path { get; init; } = []; public int Amount { get; init; } public int RemainingHealth { get; init; } public int RemainingMana { get; init; } public int RemainingTurns { get; init; } public int Round { get; init; } }
    private sealed class ExpectedBattleState { public string RandomState { get; init; } = string.Empty; public int Round { get; init; } public int ActiveIndex { get; init; } public string ActiveUnitId { get; init; } = string.Empty; public DroppedSpearVector[] DroppedSpears { get; init; } = []; public ExpectedBattleUnit[] Units { get; init; } = []; }
    private sealed class ExpectedBattleUnit { public string InstanceId { get; init; } = string.Empty; public string DefinitionId { get; init; } = string.Empty; public int[] Cell { get; init; } = []; public int MaxHealth { get; init; } public int CurrentHealth { get; init; } public int MaxMana { get; init; } public int CurrentMana { get; init; } public bool HasMovedThisTurn { get; init; } public StatusVector[] Statuses { get; init; } = []; }
    private sealed class StatusVector { public string ContentId { get; init; } = string.Empty; public string SourceId { get; init; } = string.Empty; public int RemainingTurns { get; init; } public int DamagePerTurn { get; init; } }
    private sealed class DroppedSpearVector { public string OwnerId { get; init; } = string.Empty; public int[] Cell { get; init; } = []; }
}
