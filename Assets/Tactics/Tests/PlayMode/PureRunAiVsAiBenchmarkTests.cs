using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.AssetPipeline;
using Tactics.Common.AI.MonsterAI;
using Tactics.Common.Battle;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Controllers.GameResolvers;
using Tactics.Common.Players;
using Tactics.Common.Testing.Gameplay;
using Tactics.Common.Units;
using Tactics.Common.Utilities;
using Tactics.Cells;
using Tactics.Roguelike;
using Tactics.Roster;
using Tactics.Units;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace Tactics.Tests.PlayMode
{
    /// <summary>
    /// Runs repeatable, unattended Pure Run balance probes against production encounter recipes.
    /// The player party uses one explicitly recorded generic AI policy, so results are an AI proxy
    /// and never a substitute for the manual playtest protocol.
    /// </summary>
    public sealed class PureRunAiVsAiBenchmarkTests
    {
        private const int PartySeed = 20260804;
        private const double BattleTimeoutSeconds = 15d;
        private static readonly int[] SimulationSeeds = { 101, 202, 303 };
        private static readonly string[] RecipeIds = { "N1", "N2", "N3", "N4", "N5", "N6" };

        private GameObject _battleRoot;
        private GameObject _cellRoot;
        private BattleController _controller;
        private Tile _runtimeTile;
        private readonly List<string> _loadedPartyBrainPaths = new();
        private UnityEngine.Random.State _randomState;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _randomState = UnityEngine.Random.state;
            GameTimeService.ForceResume();
            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Quadruple);
            var initializeTask = TestGameAssetHelper.EnsureInitialized();
            yield return WaitForTask(initializeTask, 20d, "Initialize GameAssetManager");
            Assert.That(initializeTask.Result, Is.Not.Null);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                if (_controller != null)
                {
                    var aiDrainTask = CancelAndDrainAiPlayersAsync();
                    yield return WaitForTask(aiDrainTask, 10d, "Drain benchmark AI players");
                    var teardownTask = _controller.TeardownRuntimeScopeAsync();
                    yield return WaitForTask(teardownTask, 10d, "Drain benchmark runtime scope");
                }
            }
            finally
            {
                DestroyCurrentBattle();
                UnityEngine.Random.state = _randomState;
                GameTimeService.ForceResume();
                GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Normal);
                TestGameAssetHelper.Cleanup();
            }

            yield return null;
        }

        [UnityTest]
        [Timeout(600000)]
        [Explicit("Long-running Pure Run balance benchmark; run directly for an auditable CSV artifact.")]
        public IEnumerator N1ToN6_ThreeSeeds_RecordMetricsAndEvaluateRoundMedian()
        {
            var rows = new List<BenchmarkRow>();
            string artifactPath = Path.Combine(
                Application.temporaryCachePath,
                "pure-run-ai-vs-ai-baseline.csv");

            foreach (string recipeId in RecipeIds)
            {
                foreach (int simulationSeed in SimulationSeeds)
                {
                    BenchmarkRow row = null;
                    yield return RunBattle(recipeId, simulationSeed, result => row = result);
                    Assert.That(row, Is.Not.Null, $"{recipeId}/{simulationSeed} did not produce a benchmark row.");
                    rows.Add(row);
                    WriteCsvArtifact(artifactPath, rows);
                    DestroyCurrentBattle();
                    yield return null;
                }
            }

            Assert.That(rows, Has.Count.EqualTo(18));
            foreach (string recipeId in RecipeIds)
            {
                var recipeRounds = rows.Where(row => row.RecipeId == recipeId)
                    .Select(row => row.CurrentRound)
                    .OrderBy(round => round)
                    .ToArray();
                Assert.That(recipeRounds, Has.Length.EqualTo(3));
                TestContext.Progress.WriteLine(
                    $"BENCHMARK {recipeId}: median_round={recipeRounds[1]}, rounds={string.Join("/", recipeRounds)}");
            }

            int overallMedian = Median(rows.Select(row => row.CurrentRound));
            TestContext.Progress.WriteLine($"BENCHMARK artifact={artifactPath}");
            TestContext.Progress.WriteLine($"BENCHMARK overall_median_round={overallMedian}");
            Assert.That(rows, Has.None.Matches<BenchmarkRow>(row => row.TimedOut),
                "Natural AI-vs-AI benchmark runs must complete without synthetic finishers.");
            Assert.That(overallMedian, Is.InRange(4, 6),
                "AI proxy overall median must remain inside the approved 4-6 round tuning band.");
        }

        private IEnumerator RunBattle(string recipeId, int simulationSeed, Action<BenchmarkRow> completed)
        {
            UnityEngine.Random.InitState(simulationSeed);
            var resolved = EncounterResolver.Resolve(recipeId, simulationSeed);
            var partyState = PlayerAdventureStateStore.CreatePureRunState(PartySeed);
            var gridController = CreateIsolatedTenByTenBattle(resolved.ToEncounterConfig(), partyState);

            _controller.InitializeGame();
            var initialUnits = _controller.GetUnits().Where(unit => unit != null).ToList();
            var partyUnits = initialUnits.Where(unit => unit.PlayerNumber == 1).ToList();
            Assert.That(partyUnits, Has.Count.EqualTo(3), "Benchmark policy requires the fixed three-member Pure Run party.");

            ApplyPartyBenchmarkBrains(partyUnits);

            _controller.SetPlayers(0, 2);
            foreach (var aiPlayer in ((IPlayerManager)_controller).GetPlayers().OfType<AIPlayer>())
            {
                aiPlayer.TurnStartDelay = 0;
                aiPlayer.UnitDelay = 0;
            }

            _controller.DisableAiAutoPlay = true;
            _controller.StartGame();
            var initialMetrics = CountInitialAttackableTargets(gridController, initialUnits);

            var partyIds = partyUnits.Select(unit => unit.UnitID).ToHashSet();
            int playerCasualties = 0;
            void OnUnitRemoved(IUnit unit)
            {
                if (unit != null && partyIds.Remove(unit.UnitID))
                    playerCasualties++;
            }

            GameResult? result = null;
            _controller.UnitRemoved += OnUnitRemoved;
            _controller.GameEnded += battleResult => result = battleResult;
            _controller.DisableAiAutoPlay = false;
            gridController.TurnContext.CurrentPlayer.Play(gridController);

            double deadline = Time.realtimeSinceStartupAsDouble + BattleTimeoutSeconds;
            while (!result.HasValue && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;

            _controller.UnitRemoved -= OnUnitRemoved;
            bool timedOut = !result.HasValue;
            string resultLabel = timedOut
                ? "timeout"
                : result.Value.Winners.Any(player => player != null && player.PlayerNumber == 1)
                    ? "player_win"
                    : "enemy_win";

            completed(new BenchmarkRow
            {
                RecipeId = recipeId,
                LayoutId = resolved.Layout.LayoutId,
                PartySeed = PartySeed,
                SimulationSeed = simulationSeed,
                Result = resultLabel,
                CurrentRound = _controller.CurrentRound,
                PlayerCasualties = playerCasualties,
                PlayerInitialDistinctTargets = initialMetrics.PlayerDistinctTargets,
                EnemyInitialDistinctTargets = initialMetrics.EnemyDistinctTargets,
                PlayerInitialActorTargetPairs = initialMetrics.PlayerActorTargetPairs,
                EnemyInitialActorTargetPairs = initialMetrics.EnemyActorTargetPairs,
                TimedOut = timedOut
            });

            var aiDrainTask = CancelAndDrainAiPlayersAsync();
            yield return WaitForTask(aiDrainTask, 10d, $"Drain AI players for {recipeId}/{simulationSeed}");
            var teardownTask = _controller.TeardownRuntimeScopeAsync();
            yield return WaitForTask(teardownTask, 10d, $"Drain {recipeId}/{simulationSeed}");
        }

        private Task CancelAndDrainAiPlayersAsync()
        {
            if (_controller == null)
                return Task.CompletedTask;

            return Task.WhenAll(
                ((IPlayerManager)_controller).GetPlayers()
                    .OfType<AIPlayer>()
                    .Select(aiPlayer => aiPlayer.CancelAndDrainAsync()));
        }

        private void ApplyPartyBenchmarkBrains(IReadOnlyCollection<IUnit> partyUnits)
        {
            var policyByCharacterId = new Dictionary<string, string>
            {
                ["pure_run_mage"] = "Assets/Tactics/AI/Encounters/AOEBrain.asset",
                ["pure_run_necromancer"] = "Assets/Tactics/AI/Encounters/SupportBrain.asset",
                ["pure_run_amazon"] = "Assets/Tactics/AI/Encounters/RangedBrain.asset"
            };

            foreach (var partyUnit in partyUnits.OfType<Unit>())
            {
                var link = partyUnit.GetComponent<RosterCharacterLink>();
                Assert.That(link, Is.Not.Null, $"{partyUnit.name} must preserve its production roster identity.");
                Assert.That(policyByCharacterId.TryGetValue(link.CharacterId, out string brainPath), Is.True,
                    $"No benchmark AI policy is recorded for '{link.CharacterId}'.");
                var brain = GameAssetManager.Instance.Load<AiBrainAsset>(brainPath);
                Assert.That(brain, Is.Not.Null, $"Benchmark brain must load from '{brainPath}'.");
                partyUnit.ApplyAiBrain(brain);
                _loadedPartyBrainPaths.Add(brainPath);
            }
        }

        private GridController CreateIsolatedTenByTenBattle(
            EncounterConfig encounter,
            PlayerAdventureState partyState)
        {
            _cellRoot = new GameObject("PureRunBenchmarkGrid");
            _cellRoot.AddComponent<Grid>();
            var gridLayerObject = new GameObject("GridLayer");
            gridLayerObject.transform.SetParent(_cellRoot.transform, false);
            var gridLayer = gridLayerObject.AddComponent<Tilemap>();
            gridLayerObject.AddComponent<TilemapRenderer>();
            _runtimeTile = ScriptableObject.CreateInstance<Tile>();
            for (int x = 0; x < BattleBoardSpec.Width; x++)
            {
                for (int y = 0; y < BattleBoardSpec.Height; y++)
                    gridLayer.SetTile(new Vector3Int(x, y, 0), _runtimeTile);
            }

            var obstacleLayerObject = new GameObject("ObstacleLayer");
            obstacleLayerObject.transform.SetParent(_cellRoot.transform, false);
            var obstacleLayer = obstacleLayerObject.AddComponent<Tilemap>();
            obstacleLayerObject.AddComponent<TilemapRenderer>();
            var cellManager = _cellRoot.AddComponent<TilemapCellManager>();
            SetRequiredPrivateField(cellManager, "_gridLayer", gridLayer);
            SetRequiredPrivateField(cellManager, "_obstacleLayer", obstacleLayer);
            cellManager.Initialize(null);
            cellManager.enabled = false;

            _battleRoot = new GameObject($"PureRunBenchmark_{encounter.EncounterId}");
            _battleRoot.SetActive(false);
            _controller = _battleRoot.AddComponent<BattleController>();
            var unitContainer = new GameObject("UnitContainer");
            unitContainer.transform.SetParent(_battleRoot.transform);
            SetRequiredPrivateField(_controller, "_cellManager", cellManager);
            SetRequiredPrivateField(_controller, "_unitContainer", unitContainer.transform);
            SetRequiredPrivateField(_controller, "_startImmediatelly", false);
            SetRequiredPrivateField(_controller, "_humanPlayerNumber", 1);
            SetRequiredPrivateField(_controller, "_useTestSetup", false);
            SetRequiredPrivateField(_controller, "_partyStateLoaderOverrideForTests",
                new Func<PlayerAdventureState>(() => partyState));
            SetRequiredPrivateField(_controller, "_encounterLoaderOverrideForTests",
                new Func<EncounterConfig>(() => encounter));
            _battleRoot.SetActive(true);
            RoguelikeBattleReturnHandler.Instance.UnregisterController(_controller);

            return (GridController)GetRequiredPrivateField(_controller, "_controller");
        }

        private static InitialTargetMetrics CountInitialAttackableTargets(
            GridController gridController,
            IReadOnlyCollection<IUnit> initialUnits)
        {
            var playerTargets = new HashSet<int>();
            var enemyTargets = new HashSet<int>();
            var playerPairs = new HashSet<(int Actor, int Target)>();
            var enemyPairs = new HashSet<(int Actor, int Target)>();

            foreach (var actor in initialUnits.OfType<Unit>().Where(unit => unit.AiBrainAsset != null))
            {
                var context = AiContextBuilder.Build(actor, gridController, actor.AiBrainAsset);
                var candidates = IntentGenerator.Generate(context);
                RuleFilter.Filter(candidates, context);
                foreach (var candidate in candidates.Where(candidate =>
                             candidate.PassedRules &&
                             candidate.Target != null &&
                             candidate.Target.PlayerNumber != actor.PlayerNumber &&
                             (candidate.Action == ActionType.Attack || candidate.Action == ActionType.UseAbility) &&
                             (candidate.Destination == null || ReferenceEquals(candidate.Destination, actor.CurrentCell))))
                {
                    if (actor.PlayerNumber == 1)
                    {
                        playerTargets.Add(candidate.Target.UnitID);
                        playerPairs.Add((actor.UnitID, candidate.Target.UnitID));
                    }
                    else
                    {
                        enemyTargets.Add(candidate.Target.UnitID);
                        enemyPairs.Add((actor.UnitID, candidate.Target.UnitID));
                    }
                }
            }

            return new InitialTargetMetrics(
                playerTargets.Count,
                enemyTargets.Count,
                playerPairs.Count,
                enemyPairs.Count);
        }

        private void DestroyCurrentBattle()
        {
            if (_controller != null)
                RoguelikeBattleReturnHandler.Instance.UnregisterController(_controller);
            if (_battleRoot != null)
                UnityEngine.Object.DestroyImmediate(_battleRoot);
            if (_cellRoot != null)
                UnityEngine.Object.DestroyImmediate(_cellRoot);
            if (_runtimeTile != null)
                UnityEngine.Object.DestroyImmediate(_runtimeTile);
            foreach (string brainPath in _loadedPartyBrainPaths)
                GameAssetManager.Instance?.Release(brainPath);
            _loadedPartyBrainPaths.Clear();
            _controller = null;
            _battleRoot = null;
            _cellRoot = null;
            _runtimeTile = null;
        }

        private static IEnumerator WaitForTask(Task task, double timeoutSeconds, string operation)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            while (!task.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;
            Assert.That(task.IsCompleted, Is.True, $"Timed out while waiting to: {operation}.");
            if (task.IsFaulted)
                throw task.Exception?.GetBaseException() ?? new InvalidOperationException($"Failed to: {operation}.");
            Assert.That(task.IsCanceled, Is.False, $"Canceled while waiting to: {operation}.");
        }

        private static int Median(IEnumerable<int> values)
        {
            int[] sorted = values.OrderBy(value => value).ToArray();
            Assert.That(sorted.Length, Is.GreaterThan(0));
            return sorted[(sorted.Length - 1) / 2];
        }

        private static void WriteCsvArtifact(string path, IEnumerable<BenchmarkRow> rows)
        {
            var lines = new List<string>
            {
                "recipe_id,layout_id,party_seed,simulation_seed,result,current_round,player_casualties,player_initial_distinct_targets,enemy_initial_distinct_targets,player_initial_actor_target_pairs,enemy_initial_actor_target_pairs,party_brain_id,manual_operation,timed_out"
            };
            lines.AddRange(rows.Select(row => string.Join(",",
                row.RecipeId,
                row.LayoutId,
                row.PartySeed,
                row.SimulationSeed,
                row.Result,
                row.CurrentRound,
                row.PlayerCasualties,
                row.PlayerInitialDistinctTargets,
                row.EnemyInitialDistinctTargets,
                row.PlayerInitialActorTargetPairs,
                row.EnemyInitialActorTargetPairs,
                "Mage=AOEBrain;Necromancer=SupportBrain;Amazon=RangedBrain",
                "false",
                row.TimedOut ? "true" : "false")));
            File.WriteAllLines(path, lines);
        }

        private static void SetRequiredPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing required private seam '{fieldName}'.");
            field.SetValue(target, value);
        }

        private static object GetRequiredPrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing required private field '{fieldName}'.");
            return field.GetValue(target);
        }

        private readonly struct InitialTargetMetrics
        {
            public InitialTargetMetrics(
                int playerDistinctTargets,
                int enemyDistinctTargets,
                int playerActorTargetPairs,
                int enemyActorTargetPairs)
            {
                PlayerDistinctTargets = playerDistinctTargets;
                EnemyDistinctTargets = enemyDistinctTargets;
                PlayerActorTargetPairs = playerActorTargetPairs;
                EnemyActorTargetPairs = enemyActorTargetPairs;
            }

            public int PlayerDistinctTargets { get; }
            public int EnemyDistinctTargets { get; }
            public int PlayerActorTargetPairs { get; }
            public int EnemyActorTargetPairs { get; }
        }

        private sealed class BenchmarkRow
        {
            public string RecipeId { get; set; }
            public string LayoutId { get; set; }
            public int PartySeed { get; set; }
            public int SimulationSeed { get; set; }
            public string Result { get; set; }
            public int CurrentRound { get; set; }
            public int PlayerCasualties { get; set; }
            public int PlayerInitialDistinctTargets { get; set; }
            public int EnemyInitialDistinctTargets { get; set; }
            public int PlayerInitialActorTargetPairs { get; set; }
            public int EnemyInitialActorTargetPairs { get; set; }
            public bool TimedOut { get; set; }
        }
    }
}
