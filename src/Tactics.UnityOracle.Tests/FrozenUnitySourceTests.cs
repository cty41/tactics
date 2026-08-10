using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using Tactics.Common.Battle;
using Tactics.Common.Cells;
using Tactics.Common.Pathfinding.Algorithms;
using Tactics.Common.Units;
using Tactics.Core.Presentation;
using Tactics.Core.Turns;
using Tactics.Core.Units;
using CoreRuntimeScope = Tactics.Core.Runtime.BattleRuntimeScope;
using FrozenPresentation = Tactics.Common.Skills.Graph;
using FrozenRuntimeScope = Tactics.Common.Battle.Runtime.BattleRuntimeScope;
using FrozenUnitDerivedStatRules = Tactics.Common.Units.UnitDerivedStatRules;

namespace Tactics.UnityOracle.Tests;

/// <summary>
/// Executes selected engine-neutral source files from the immutable Unity final snapshot.
/// </summary>
/// <remarks>
/// This project is migration evidence only. It must never become a production dependency or
/// substitute its minimal compile-time stubs for the real Unity runtime adapters.
/// </remarks>
public sealed class FrozenUnitySourceTests
{
    private const string ExpectedGoatBodyTintShaderBlob =
        "d4da8e21404ac1b5d134b0f1455f36839900e7c2";

    private static readonly IReadOnlyDictionary<string, string> ExpectedBlobIds =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Assets/Tactics/Scripts/Common/pathfinding/algorithms/DijkstraPathfinding.cs"] = "6c7230e9f7bdb6e87c686b037c07ee23b88c30bd",
            ["Assets/Tactics/Scripts/Common/pathfinding/algorithms/PathfindingAlgorithm.cs"] = "4e3fde3f6e4909c2e03c85b8d15a77ccffe58ca9",
            ["Assets/Tactics/Scripts/Common/pathfinding/dataStructures/HeapPriorityQueue.cs"] = "84ac36a60f294f2cbdaa93105ae01c48b209c3f9",
            ["Assets/Tactics/Scripts/Common/pathfinding/dataStructures/IPriorityQueue.cs"] = "21ac3290a5ea3eae827285e7cee2a0417e37e0d1",
            ["Assets/Tactics/Scripts/Common/pathfinding/dataStructures/PriorityQueueItem.cs"] = "3dad83ed21a81c50dc457f933e0952a167dc941b",
            ["Assets/Tactics/Scripts/Common/Battle/BattleInitiativeService.cs"] = "a060663b709f316d02cf9d0f2581d3b1d7132114",
            ["Assets/Tactics/Scripts/Common/Battle/Runtime/IBattleRuntimeScope.cs"] = "c3dfdc5f556ffbc56ab60e18fc0ef5521b0c83a6",
            ["Assets/Tactics/Scripts/Common/Battle/Runtime/BattleRuntimeScope.cs"] = "544f8ba928da4d831589e4fc7d5208623b17861f",
            ["Assets/Tactics/Scripts/Common/Skills/Graph/PresentationExecutionPlan.cs"] = "6ef526ac91cdc0a1efb06066be7b3591289d41e1",
            ["Assets/Tactics/Scripts/Common/Battle/AmazonBattleState.cs"] = "edc31ec7cddede0fb2b4a6676fa3dfdad0851966",
            ["Assets/Tactics/Scripts/Common/Skills/Graph/Executors/AmazonSkillNodeExecutor.cs"] = "78e29de56e49926a2f5542d5491deea2cc2bef71",
            ["Assets/Tactics/Scripts/Common/Units/Buffs/Buff.cs"] = "84d04546bda0ab8121439a2285fa5065be642cc2",
            ["Assets/Tactics/Scripts/Common/Units/Buffs/BuffBehavior.cs"] = "31a2739f51ab47c1a24e41240083db6dfbcb9053",
            ["Assets/Tactics/Scripts/Common/Units/Buffs/BuffComponent.cs"] = "bc93aee09577d3bd9e1dda94c062c5c006383d61",
            ["Assets/Tactics/Scripts/Common/Units/abilities/SkillGraphAbilityImpl.cs"] = "54a463c642c59d1550c0454a9388c3f68c7e89b1",
            ["Assets/Tactics/Scripts/Common/Roster/CharacterDefinition.cs"] = "4f0d131a1f809c920f01cf137087dc0dc63d312a",
            ["Assets/Tactics/Scripts/Common/Roster/PlayerAdventureStateStore.cs"] = "8a3a14a1392bb57562fb6ae286eb79508ce6fe75",
            ["Assets/Tactics/Scripts/Common/Units/Unit.cs"] = "5a9776a61dd698b439dd19845a5979bce73d419a",
            ["Assets/Tactics/Scripts/Common/Units/UnitDerivedStatRules.cs"] = "4da3c885cfd9df5ed4128f8ef446815e760a2b0a",
            ["Assets/Tactics/Scripts/Common/Units/FourDirectionSpriteVisual.cs"] = "9528f7c17f3a782b16c4ceaa9b6b97e25893acfb",
            ["Assets/Tactics/Scripts/Common/Battle/EncounterConfig.cs"] = "850f23e53869c04c8ff28adbd85c1d4f12da9bae"
        };

    [Test]
    public void LinkedSources_MatchUnityFinalGitBlobs()
    {
        string repositoryRoot = FindRepositoryRoot();

        Assert.Multiple(() =>
        {
            foreach ((string path, string expectedBlobId) in ExpectedBlobIds)
            {
                string actualBlobId = ComputeGitBlobId(Path.Combine(repositoryRoot, path));
                Assert.That(actualBlobId, Is.EqualTo(expectedBlobId), path);
            }
        });
    }

    [Test]
    public void PoisonSpearLv1_SourceContractMatchesExportedCoreSemantics()
    {
        string repositoryRoot = FindRepositoryRoot();
        string executor = ReadFrozenSource(repositoryRoot,
            "Assets/Tactics/Scripts/Common/Skills/Graph/Executors/AmazonSkillNodeExecutor.cs");
        string battleState = ReadFrozenSource(repositoryRoot,
            "Assets/Tactics/Scripts/Common/Battle/AmazonBattleState.cs");
        string buff = ReadFrozenSource(repositoryRoot,
            "Assets/Tactics/Scripts/Common/Units/Buffs/Buff.cs");
        string buffBehavior = ReadFrozenSource(repositoryRoot,
            "Assets/Tactics/Scripts/Common/Units/Buffs/BuffBehavior.cs");
        string buffComponent = ReadFrozenSource(repositoryRoot,
            "Assets/Tactics/Scripts/Common/Units/Buffs/BuffComponent.cs");
        string ability = ReadFrozenSource(repositoryRoot,
            "Assets/Tactics/Scripts/Common/Units/abilities/SkillGraphAbilityImpl.cs");

        Assert.Multiple(() =>
        {
            Assert.That(executor, Does.Contain("state.FindDropCell(caster, target?.CurrentCell, 3)"));
            Assert.That(executor, Does.Contain("record.Level >= 2 ? 10f : 8f"));
            Assert.That(executor, Does.Contain("affected.AddBuff(new Buff(record.PoisonBuff, caster, 3))"));
            Assert.That(executor, Does.Contain("state.IsSpearHeld(caster)"));
            Assert.That(executor, Does.Contain("state.DropSpear(caster, dropCell)"));
            Assert.That(battleState, Does.Contain("FindDropCell(IUnit owner, ICell targetCell, int radius = 3)"));
            Assert.That(buff, Does.Contain("BuffEffectType.Poison => 3"));
            Assert.That(buff, Does.Contain("RemainingTurns--;"));
            Assert.That(buffBehavior, Does.Contain("BuffEffectType.Poison => 2f"));
            Assert.That(buffComponent, Does.Contain("BuffEffectType.Poison => BuffRefreshStrategy.AddDuration"));
            Assert.That(buffComponent, Does.Contain("existing.RemainingTurns += buff.RemainingTurns"));
            Assert.That(ability, Does.Contain("_owner.Mana < _config.ManaCost"));
            Assert.That(ability, Does.Contain("_owner.Mana -= _config.ManaCost"));
            Assert.That(ability, Does.Contain("FindDropCell(_owner, cell, 3)"));
        });
    }

    [Test]
    public void UnitDerivedStats_FrozenFormulaAndSourceSemanticsMatchGolden()
    {
        string repositoryRoot = FindRepositoryRoot();
        string characterDefinition = ReadFrozenSource(repositoryRoot,
            "Assets/Tactics/Scripts/Common/Roster/CharacterDefinition.cs");
        string playerState = ReadFrozenSource(repositoryRoot,
            "Assets/Tactics/Scripts/Common/Roster/PlayerAdventureStateStore.cs");
        string unit = ReadFrozenSource(repositoryRoot,
            "Assets/Tactics/Scripts/Common/Units/Unit.cs");
        string encounter = ReadFrozenSource(repositoryRoot,
            "Assets/Tactics/Scripts/Common/Battle/EncounterConfig.cs");
        using JsonDocument golden = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(repositoryRoot, "Tests", "golden", "unit-batch-v1.json")));

        Assert.Multiple(() =>
        {
            Assert.That(characterDefinition, Does.Contain("MaxHp => System.Math.Max(1, Constitution * 4)"));
            Assert.That(characterDefinition, Does.Contain("MaxMp => System.Math.Max(0, Charisma * 3)"));
            Assert.That(playerState, Does.Contain("CreatePureRunCharacter(\"pure_run_mage\""));
            Assert.That(playerState, Does.Contain("CreatePureRunCharacter(\"pure_run_necromancer\""));
            Assert.That(playerState, Does.Contain("CreatePureRunCharacter(\"pure_run_amazon\""));
            Assert.That(unit, Does.Contain("MaxHealth = Mathf.Max(1, Constitution * 4)"));
            Assert.That(unit, Does.Contain("MaxMana = Mathf.Max(0, Charisma * 3)"));
            Assert.That(unit, Does.Contain("Initiative = Speed * 2"));
            Assert.That(encounter, Does.Contain("PureRunGoatElitePoisonCaster.prefab"));
        });

        foreach (JsonElement formulaCase in golden.RootElement.GetProperty("formulaCases").EnumerateArray())
        {
            float speed = formulaCase.GetProperty("speed").GetSingle();
            Assert.Multiple(() =>
            {
                Assert.That(
                    FrozenUnitDerivedStatRules.CalculateMovement(speed),
                    Is.EqualTo(formulaCase.GetProperty("moveRange").GetSingle()),
                    $"speed={speed}");
                Assert.That(
                    speed * 2f,
                    Is.EqualTo(formulaCase.GetProperty("initiative").GetSingle()),
                    $"speed={speed}");
            });
        }

        foreach (JsonElement definition in golden.RootElement.GetProperty("units").EnumerateArray())
        {
            JsonElement attributes = definition.GetProperty("attributes");
            JsonElement derived = definition.GetProperty("derived");
            float speed = definition.GetProperty("speed").GetSingle();
            string contentId = definition.GetProperty("contentId").GetString()!;
            Assert.Multiple(() =>
            {
                Assert.That(
                    derived.GetProperty("maxHealth").GetInt32(),
                    Is.EqualTo(Math.Max(1, attributes.GetProperty("constitution").GetInt32() * 4)),
                    contentId);
                Assert.That(
                    derived.GetProperty("maxMana").GetInt32(),
                    Is.EqualTo(Math.Max(0, attributes.GetProperty("charisma").GetInt32() * 3)),
                    contentId);
                Assert.That(
                    derived.GetProperty("startingMana").GetInt32(),
                    Is.EqualTo(attributes.GetProperty("charisma").GetInt32()),
                    contentId);
                Assert.That(
                    derived.GetProperty("moveRange").GetSingle(),
                    Is.EqualTo(FrozenUnitDerivedStatRules.CalculateMovement(speed)),
                    contentId);
                Assert.That(derived.GetProperty("initiative").GetSingle(), Is.EqualTo(speed * 2f), contentId);
            });
        }
    }

    [Test]
    public void UnitPresentation_FrozenDirectionAndGoatTintContractsMatchGolden()
    {
        string repositoryRoot = FindRepositoryRoot();
        string directionSource = ReadFrozenSource(repositoryRoot,
            "Assets/Tactics/Scripts/Common/Units/FourDirectionSpriteVisual.cs").ReplaceLineEndings("\n");
        string shaderSource = ReadFrozenSource(repositoryRoot,
            "Assets/Tactics/Arts/PureRun/Shaders/GoatBodyTint.shader");
        using JsonDocument golden = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(repositoryRoot, "Tests", "golden", "unit-batch-v1.json")));
        JsonElement tintContract = golden.RootElement.GetProperty("tintContract");

        Assert.Multiple(() =>
        {
            Assert.That(directionSource, Does.Contain(
                "case FacingDirection.East:\n                    // Unity's isometric +X axis points up-right on screen.\n                    targetRenderer.sprite = upLeft;\n                    targetRenderer.flipX = true;"));
            Assert.That(directionSource, Does.Contain(
                "case FacingDirection.West:\n                    targetRenderer.sprite = downRight;\n                    targetRenderer.flipX = true;"));
            Assert.That(directionSource, Does.Contain(
                "case FacingDirection.North:\n                    targetRenderer.sprite = upLeft;\n                    targetRenderer.flipX = false;"));
            Assert.That(directionSource, Does.Contain(
                "case FacingDirection.South:\n                    targetRenderer.sprite = downRight;\n                    targetRenderer.flipX = false;"));
            Assert.That(shaderSource, Does.Contain(
                "return 1.0h - smoothstep(0.10h, 0.28h, sourceDistance);"));
            Assert.That(shaderSource, Does.Contain(
                "half3 recoloredBody = _BodyTint.rgb * (sourceLuminance / baseLuminance);"));
            Assert.That(shaderSource, Does.Contain(
                "half3 finalRgb = lerp(source.rgb, recoloredBody, mask);"));
            Assert.That(tintContract.GetProperty("unityShaderGitBlobSha1").GetString(),
                Is.EqualTo(ExpectedGoatBodyTintShaderBlob));
            Assert.That(tintContract.GetProperty("maskSmoothstep").EnumerateArray()
                .Select(item => item.GetSingle()), Is.EqualTo(new[] { 0.10f, 0.28f }));
            Assert.That(tintContract.GetProperty("luminanceWeights").EnumerateArray()
                .Select(item => item.GetSingle()), Is.EqualTo(new[] { 0.299f, 0.587f, 0.114f }));
        });

        JsonElement[] units = golden.RootElement.GetProperty("units").EnumerateArray().ToArray();
        Assert.That(units.Where(unit => unit.GetProperty("familyId").GetString() == "goat")
            .All(unit => unit.GetProperty("visual").GetProperty("tintMode").GetString() ==
                "goat-body-mask-v1"), Is.True);
        Assert.That(units.Where(unit => unit.GetProperty("familyId").GetString() != "goat")
            .All(unit => unit.GetProperty("visual").GetProperty("tintMode").GetString() ==
                "multiply"), Is.True);
    }

    [Test]
    public void RuntimeDijkstra_UsesFrozenNeighbourAndHeapTieBreak()
    {
        OracleGraph graph = OracleGraph.Create(
            width: 3,
            height: 3,
            blocked: new[] { new OraclePoint(1, 0) });
        OracleCell origin = graph.CellAt(0, 0);
        OracleCell destination = graph.CellAt(2, 0);
        var algorithm = new DijkstraPathfinding();

        (Dictionary<ICell, ICell> cameFrom, _) = algorithm.FindAllPaths(graph.Edges, origin);
        IReadOnlyList<OraclePoint> path = algorithm
            .ReconstructPath(origin, destination, cameFrom, new List<ICell>())
            .Cast<OracleCell>()
            .Select(cell => cell.Point)
            .ToArray();

        Assert.That(path, Is.EqualTo(new[]
        {
            new OraclePoint(0, 1),
            new OraclePoint(1, 1),
            new OraclePoint(2, 1),
            new OraclePoint(2, 0)
        }));
    }

    [Test]
    public void RuntimeInitiative_UsesInitiativeThenPlayerThenUnitId()
    {
        var fastest = new OracleUnit("fastest", initiative: 10, playerNumber: 1, unitId: 5);
        var enemyLater = new OracleUnit("enemy-later", initiative: 8, playerNumber: 1, unitId: 2);
        var friendly = new OracleUnit("friendly", initiative: 8, playerNumber: 0, unitId: 9);
        var enemyEarlier = new OracleUnit("enemy-earlier", initiative: 8, playerNumber: 1, unitId: 1);
        var service = new BattleInitiativeService();

        service.StartRound(new IUnit[] { enemyLater, fastest, friendly, enemyEarlier });

        Assert.That(
            service.GetCurrentRoundOrder().Cast<OracleUnit>().Select(unit => unit.Name),
            Is.EqualTo(new[] { "fastest", "friendly", "enemy-earlier", "enemy-later" }));
    }

    [Test]
    public void RuntimeInitiative_ReordersOnlyRemainingPartitionAfterChange()
    {
        JsonElement vector = LoadGoldenRoot().GetProperty("initiativeRoundCases")[0];
        var units = vector.GetProperty("entries").EnumerateArray()
            .Select(entry => new OracleUnit(
                entry.GetProperty("instanceId").GetString(),
                entry.GetProperty("initiative").GetSingle(),
                entry.GetProperty("playerNumber").GetInt32(),
                entry.GetProperty("spawnOrdinal").GetInt32()))
            .ToDictionary(unit => unit.Name, StringComparer.Ordinal);
        var service = new BattleInitiativeService();

        service.StartRound(units.Values);
        OracleUnit first = (OracleUnit)service.TakeNext(units.Values);
        JsonElement change = vector.GetProperty("changes")[0];
        OracleUnit changed = units[change.GetProperty("instanceId").GetString()];
        changed.Initiative = change.GetProperty("initiative").GetSingle();
        service.NotifyInitiativeChanged(changed);

        string[] expected = vector.GetProperty("expectedOrderAfterChange").EnumerateArray()
            .Select(value => value.GetString())
            .ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(first.Name, Is.EqualTo(vector.GetProperty("expectedFirst").GetString()));
            Assert.That(
                service.GetCurrentRoundOrder().Cast<OracleUnit>().Select(unit => unit.Name),
                Is.EqualTo(expected));
        });

        OracleUnit current = units[vector.GetProperty("currentInitiativeChange").GetProperty("instanceId").GetString()];
        current.Initiative = vector.GetProperty("currentInitiativeChange").GetProperty("initiative").GetSingle();
        service.NotifyInitiativeChanged(current);
        Assert.That(
            service.GetCurrentRoundOrder().Cast<OracleUnit>().Select(unit => unit.Name),
            Is.EqualTo(expected),
            "Changing the current unit must not reinsert it into the remaining partition.");

        OracleUnit second = (OracleUnit)service.TakeNext(units.Values);
        Assert.Multiple(() =>
        {
            Assert.That(second.Name, Is.EqualTo(vector.GetProperty("expectedSecond").GetString()));
            Assert.That(
                service.Acted.Cast<OracleUnit>().Select(unit => unit.Name),
                Is.EquivalentTo(vector.GetProperty("expectedActedAfterSecond").EnumerateArray()
                    .Select(value => value.GetString())));
            Assert.That(
                service.Remaining.Cast<OracleUnit>().Select(unit => unit.Name),
                Is.EqualTo(vector.GetProperty("expectedRemainingAfterSecond").EnumerateArray()
                    .Select(value => value.GetString())));
        });
    }

    [Test]
    public async Task RuntimeScope_CoreMatchesFrozenOwnershipAndFaultContract()
    {
        JsonElement vector = LoadGoldenRoot().GetProperty("runtimeScopeCases")[0];
        ScopeContractResult frozen = await ExerciseScopeContractAsync(new FrozenScopeAdapter(new FrozenRuntimeScope()));
        ScopeContractResult core = await ExerciseScopeContractAsync(new CoreScopeAdapter(new CoreRuntimeScope()));

        Assert.Multiple(() =>
        {
            Assert.That(core.AcceptedNull, Is.EqualTo(frozen.AcceptedNull));
            Assert.That(core.AcceptedCompleted, Is.EqualTo(frozen.AcceptedCompleted));
            Assert.That(core.AcceptedFault, Is.EqualTo(frozen.AcceptedFault));
            Assert.That(core.AcceptedAfterCancel, Is.EqualTo(frozen.AcceptedAfterCancel));
            Assert.That(core.IsCancelling, Is.EqualTo(frozen.IsCancelling));
            Assert.That(core.FaultMessages, Is.EqualTo(frozen.FaultMessages));
            Assert.That(core.AcceptedNull, Is.EqualTo(vector.GetProperty("expectedAcceptedNull").GetBoolean()));
            Assert.That(core.AcceptedCompleted, Is.EqualTo(vector.GetProperty("expectedAcceptedCompleted").GetBoolean()));
            Assert.That(core.AcceptedAfterCancel, Is.EqualTo(vector.GetProperty("expectedAcceptedAfterCancel").GetBoolean()));
            Assert.That(core.FaultMessages, Is.EqualTo(new[] { vector.GetProperty("faultMessage").GetString() }));
        });
    }

    [Test]
    public async Task RuntimeScope_CoreMatchesFrozenReentrantDisposeDrain()
    {
        bool expected = LoadGoldenRoot().GetProperty("runtimeScopeCases")[0]
            .GetProperty("expectedReentrantDisposeDrain")
            .GetBoolean();
        Assert.That(await ExerciseReentrantDisposeAsync(new FrozenScopeAdapter(new FrozenRuntimeScope())), Is.EqualTo(expected));
        Assert.That(await ExerciseReentrantDisposeAsync(new CoreScopeAdapter(new CoreRuntimeScope())), Is.EqualTo(expected));
    }

    [Test]
    public async Task RuntimeScope_TimeoutContainsCancellationCallbackException()
    {
        _ = Tactics.Runtime.Utilities.TLog.DrainErrors();
        using var scope = new FrozenRuntimeScope(TimeSpan.FromMilliseconds(10));
        using CancellationTokenRegistration registration = scope.Token.Register(
            () => throw new InvalidOperationException("phase-1c-timeout-callback"));

        string[] errors = Array.Empty<string>();
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (errors.Length == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
            errors = Tactics.Runtime.Utilities.TLog.DrainErrors();
        }

        Assert.That(errors.Single(), Does.Contain("phase-1c-timeout-callback"));
    }

    [Test]
    public void PresentationCompiler_CoreMatchesFrozenForkJoinTree()
    {
        JsonElement vector = LoadGoldenRoot().GetProperty("presentationCases")[0];
        List<FrozenPresentation.PresentationNodeRecord> frozenNodes = vector.GetProperty("nodes")
            .EnumerateArray()
            .Select(CreateFrozenPresentationNode)
            .ToList();
        List<FrozenPresentation.PresentationEdgeRecord> frozenEdges = vector.GetProperty("edges")
            .EnumerateArray()
            .Select(edge => new FrozenPresentation.PresentationEdgeRecord(
                edge.GetProperty("source").GetString(),
                edge.GetProperty("target").GetString()))
            .ToList();
        var frozenGraph = new FrozenPresentation.BattlePresentationGraph(frozenNodes, frozenEdges);
        FrozenPresentation.PresentationExecutionPlan frozenPlan =
            FrozenPresentation.PresentationExecutionPlanCompiler.Compile(
                frozenGraph,
                FrozenPresentation.PresentationCueKind.Action);

        var coreGraph = new PresentationGraphDefinition(
            vector.GetProperty("schemaVersion").GetInt32(),
            vector.GetProperty("nodes").EnumerateArray().Select(CreateCorePresentationNode),
            vector.GetProperty("edges").EnumerateArray().Select((edge, index) => new PresentationGraphEdge(
                edge.GetProperty("source").GetString()!,
                edge.GetProperty("target").GetString()!,
                index)));
        PresentationExecutionPlan corePlan = PresentationGraphCompiler.Compile(
            coreGraph,
            vector.GetProperty("cueId").GetString()!);

        string expected = vector.GetProperty("expectedSnapshot").GetString();
        Assert.Multiple(() =>
        {
            Assert.That(SnapshotFrozen(frozenPlan.Root), Is.EqualTo(expected));
            Assert.That(SnapshotCore(corePlan, corePlan.RootNodeId), Is.EqualTo(expected));
        });
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo current = new(TestContext.CurrentContext.TestDirectory);
        while (current != null && !File.Exists(Path.Combine(current.FullName, "global.json")))
            current = current.Parent;

        return current?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root from the test output directory.");
    }

    private static JsonElement LoadGoldenRoot()
    {
        string path = Path.Combine(FindRepositoryRoot(), "Tests", "golden", "10x10-core-vectors.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    private static string ComputeGitBlobId(string path)
    {
        byte[] content = File.ReadAllBytes(path);
        byte[] header = Encoding.UTF8.GetBytes($"blob {content.Length}\0");
        byte[] payload = new byte[header.Length + content.Length];
        Buffer.BlockCopy(header, 0, payload, 0, header.Length);
        Buffer.BlockCopy(content, 0, payload, header.Length, content.Length);
        return Convert.ToHexString(SHA1.HashData(payload)).ToLowerInvariant();
    }

    private static string ReadFrozenSource(string repositoryRoot, string relativePath) =>
        File.ReadAllText(Path.Combine(repositoryRoot, relativePath));

    private readonly record struct OraclePoint(int X, int Y);

    private sealed class OracleCell : ICell
    {
        public OracleCell(OraclePoint point) => Point = point;

        public OraclePoint Point { get; }
    }

    private sealed class OracleGraph
    {
        private static readonly OraclePoint[] Directions =
        {
            new(1, 0),
            new(-1, 0),
            new(0, 1),
            new(0, -1)
        };

        private readonly IReadOnlyDictionary<OraclePoint, OracleCell> _cells;

        private OracleGraph(
            IReadOnlyDictionary<OraclePoint, OracleCell> cells,
            Dictionary<ICell, Dictionary<ICell, float>> edges)
        {
            _cells = cells;
            Edges = edges;
        }

        public Dictionary<ICell, Dictionary<ICell, float>> Edges { get; }

        public OracleCell CellAt(int x, int y) => _cells[new OraclePoint(x, y)];

        public static OracleGraph Create(int width, int height, IEnumerable<OraclePoint> blocked)
        {
            HashSet<OraclePoint> blockedSet = blocked.ToHashSet();
            var cells = new Dictionary<OraclePoint, OracleCell>();
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var point = new OraclePoint(x, y);
                    if (!blockedSet.Contains(point))
                        cells.Add(point, new OracleCell(point));
                }
            }

            var edges = new Dictionary<ICell, Dictionary<ICell, float>>();
            foreach ((OraclePoint point, OracleCell cell) in cells)
            {
                var neighbours = new Dictionary<ICell, float>();
                foreach (OraclePoint direction in Directions)
                {
                    var neighbourPoint = new OraclePoint(point.X + direction.X, point.Y + direction.Y);
                    if (cells.TryGetValue(neighbourPoint, out OracleCell neighbour))
                        neighbours.Add(neighbour, 1f);
                }

                edges.Add(cell, neighbours);
            }

            return new OracleGraph(cells, edges);
        }
    }

    private sealed class OracleUnit : IUnit
    {
        public OracleUnit(string name, float initiative, int playerNumber, int unitId)
        {
            Name = name;
            Initiative = initiative;
            PlayerNumber = playerNumber;
            UnitID = unitId;
        }

        public string Name { get; }
        public float Health { get; set; } = 1f;
        public float Initiative { get; set; }
        public int PlayerNumber { get; }
        public int UnitID { get; }
    }

    private static async Task<ScopeContractResult> ExerciseScopeContractAsync(IRuntimeScopeAdapter scope)
    {
        using (scope)
        {
            bool acceptedNull = scope.TryTrack(null);
            bool acceptedCompleted = scope.TryTrack(Task.CompletedTask);
            bool acceptedFault = scope.TryTrack(Task.FromException(new InvalidOperationException("phase-1c-fault")));
            string[] faults;
            try
            {
                await scope.WhenIdleAsync();
                faults = Array.Empty<string>();
            }
            catch (AggregateException exception)
            {
                faults = exception.InnerExceptions.Select(item => item.Message).ToArray();
            }

            scope.Cancel();
            bool acceptedAfterCancel = scope.TryTrack(Task.CompletedTask);
            return new ScopeContractResult(
                acceptedNull,
                acceptedCompleted,
                acceptedFault,
                acceptedAfterCancel,
                scope.IsCancelling,
                faults);
        }
    }

    private static async Task<bool> ExerciseReentrantDisposeAsync(IRuntimeScopeAdapter scope)
    {
        using (scope)
        {
            var tracked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            scope.Track(tracked.Task);
            using CancellationTokenRegistration registration = scope.Token.Register(scope.Dispose);
            scope.Cancel();
            tracked.SetResult();
            await scope.WhenIdleAsync();
            scope.Dispose();
            return true;
        }
    }

    private static FrozenPresentation.PresentationNodeRecord CreateFrozenPresentationNode(JsonElement node)
    {
        string id = node.GetProperty("id").GetString();
        string kind = node.GetProperty("kind").GetString();
        FrozenPresentation.PresentationNodeRecord result = kind switch
        {
            "entry" => new FrozenPresentation.PresentationEntryNodeRecord
            {
                Cue = FrozenPresentation.PresentationCueKind.Action
            },
            "finish" => new FrozenPresentation.PresentationFinishNodeRecord(),
            "fork" => new FrozenPresentation.PresentationForkNodeRecord
            {
                JoinNodeId = node.GetProperty("joinNodeId").GetString()
            },
            "join" => new FrozenPresentation.PresentationJoinNodeRecord(),
            "leaf" => new FrozenPresentation.PresentationLeafNodeRecord
            {
                NodeTypeId = node.GetProperty("nodeTypeId").GetString()
            },
            _ => throw new InvalidOperationException($"Unsupported frozen presentation node kind '{kind}'.")
        };
        result.NodeId = id;
        result.Enabled = !node.TryGetProperty("enabled", out JsonElement enabled) || enabled.GetBoolean();
        return result;
    }

    private static PresentationGraphNode CreateCorePresentationNode(JsonElement node)
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
                _ => throw new InvalidOperationException($"Unsupported Core presentation node kind '{kind}'.")
            },
            !node.TryGetProperty("enabled", out JsonElement enabled) || enabled.GetBoolean(),
            kind == "entry" ? node.GetProperty("cueId").GetString() : null,
            kind == "fork" ? node.GetProperty("joinNodeId").GetString() : null);
    }

    private static string SnapshotFrozen(FrozenPresentation.PresentationPlanStep step) => step switch
    {
        FrozenPresentation.PresentationLeafStep leaf => $"L({leaf.NodeId})",
        FrozenPresentation.PresentationSequenceStep sequence =>
            $"S[{string.Join(",", sequence.Children.Select(SnapshotFrozen))}]",
        FrozenPresentation.PresentationParallelStep parallel =>
            $"P({parallel.ForkNodeId}->{parallel.JoinNodeId})[{string.Join("|", parallel.Branches.Select(SnapshotFrozen))}]",
        _ => throw new InvalidOperationException($"Unsupported frozen presentation step '{step.GetType().Name}'.")
    };

    private static string SnapshotCore(PresentationExecutionPlan plan, string nodeId)
    {
        PresentationNode node = plan.Nodes[nodeId];
        return node.Kind switch
        {
            PresentationNodeKind.Leaf => $"L({node.NodeId})",
            PresentationNodeKind.Sequence =>
                $"S[{string.Join(",", node.Children.Select(child => SnapshotCore(plan, child)))}]",
            PresentationNodeKind.Parallel =>
                $"P({node.ForkNodeId}->{node.JoinNodeId})[{string.Join("|", node.Children.Select(child => SnapshotCore(plan, child)))}]",
            _ => throw new InvalidOperationException($"Unsupported Core presentation node kind '{node.Kind}'.")
        };
    }

    private interface IRuntimeScopeAdapter : IDisposable
    {
        CancellationToken Token { get; }
        bool IsCancelling { get; }
        void Track(Task task);
        bool TryTrack(Task task);
        Task WhenIdleAsync();
        void Cancel();
    }

    private sealed class FrozenScopeAdapter(FrozenRuntimeScope scope) : IRuntimeScopeAdapter
    {
        public CancellationToken Token => scope.Token;
        public bool IsCancelling => scope.IsCancelling;
        public void Track(Task task) => scope.Track(task);
        public bool TryTrack(Task task) => scope.TryTrack(task);
        public Task WhenIdleAsync() => scope.WhenIdleAsync();
        public void Cancel() => scope.Cancel();
        public void Dispose() => scope.Dispose();
    }

    private sealed class CoreScopeAdapter(CoreRuntimeScope scope) : IRuntimeScopeAdapter
    {
        public CancellationToken Token => scope.Token;
        public bool IsCancelling => scope.IsCancelling;
        public void Track(Task task) => scope.Track(task);
        public bool TryTrack(Task task) => scope.TryTrack(task);
        public Task WhenIdleAsync() => scope.WhenIdleAsync();
        public void Cancel() => scope.Cancel();
        public void Dispose() => scope.Dispose();
    }

    private sealed record ScopeContractResult(
        bool AcceptedNull,
        bool AcceptedCompleted,
        bool AcceptedFault,
        bool AcceptedAfterCancel,
        bool IsCancelling,
        string[] FaultMessages);
}
