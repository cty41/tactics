using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.AssetPipeline;
using Tactics.Common.AI.MonsterAI;
using Tactics.Common.Battle;
using Tactics.Common.Cells;
using Tactics.Common.Testing.Gameplay;
using Tactics.Common.Units;
using Tactics.Common.Units.Classes;
using Tactics.Common.Utilities;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public class GameplayRuntimeBattleWithRealConfigs
    {
        private GameObject _battleRoot;
        private GameObject _cellManagerRoot;
        private GameAssetManager _assetManager;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

            // Initialize GameAssetManager
            var initTask = TestGameAssetHelper.EnsureInitialized();
            yield return new WaitUntil(() => initTask.IsCompleted);
            _assetManager = initTask.Result;
            Assume.That(_assetManager, Is.Not.Null, "GameAssetManager should be initialized.");
            Assume.That(_assetManager.IsInitialized, Is.True, "GameAssetManager should be initialized.");

            var controllerType = ResolveBattleControllerType();
            Assume.That(controllerType, Is.Not.Null, "BattleController type should exist.");

            _battleRoot = new GameObject("TestBattleControllerReal");
            var bc = (MonoBehaviour)_battleRoot.AddComponent(controllerType);

            var startFlag = controllerType.GetField("_startImmediatelly", BindingFlags.Instance | BindingFlags.NonPublic);
            startFlag?.SetValue(bc, false);

            // 4x4 grid
            _cellManagerRoot = new GameObject("TestCellManagerReal");
            var cellMgr = _cellManagerRoot.AddComponent<RegularCellManager>();
            for (int x = 0; x < 4; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    var cellGo = new GameObject($"Cell_{x}_{y}");
                    cellGo.transform.SetParent(_cellManagerRoot.transform);
                    var square = cellGo.AddComponent<Square>();
                    square.GridCoordinates = new Vector2IntImpl(x, y);
                    square.WorldPosition = new Vector3Impl(x, y, 0);
                    square.MovementCost = 1f;
                }
            }

            var cellMgrField = controllerType.GetField("_cellManager", BindingFlags.Instance | BindingFlags.NonPublic);
            cellMgrField?.SetValue(bc, cellMgr);

            var awake = controllerType.GetMethod("Awake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            awake?.Invoke(bc, null);

            var gridControllerField = controllerType.GetField("_controller", BindingFlags.Instance | BindingFlags.NonPublic);
            var gridController = gridControllerField?.GetValue(bc);
            if (gridController != null)
            {
                var beforeInitProp = gridController.GetType().GetProperty("BeforeUnitManagerInitialize");
                beforeInitProp?.SetValue(gridController, null);
            }

            // Set all players as AI for automated testing
            controllerType.GetMethod("SetPlayers", BindingFlags.Instance | BindingFlags.Public)?.Invoke(bc, new object[] { 0, 2 });

            var unitContainer = _battleRoot.transform;
            var unitField = controllerType.GetField("_unitContainer", BindingFlags.Instance | BindingFlags.NonPublic);
            if (unitField?.GetValue(bc) == null)
            {
                var containerGo = new GameObject("UnitContainer");
                containerGo.transform.SetParent(_battleRoot.transform);
                unitField?.SetValue(bc, containerGo.transform);
                unitContainer = containerGo.transform;
            }
            else
            {
                unitContainer = (Transform)unitField.GetValue(bc);
            }

            // Load real configs
            var brainAsset = TestUnitFactory.LoadBasicMeleeBrain();
            Assume.That(brainAsset, Is.Not.Null, "BasicMeleeBrain should load.");

            // Create units with real configs
            var unit1 = TestUnitFactory.CreateBarbarian(unitContainer, "Barbarian_P1", 1, FindCell(_cellManagerRoot, 0, 0), brainAsset);
            var unit2 = TestUnitFactory.CreateBarbarian(unitContainer, "Barbarian_P2", 2, FindCell(_cellManagerRoot, 3, 0), brainAsset);

            // Initialize and start battle
            // 设置 resolver 与 Test1.unity 一致（UnitSpeedTurnResolver）
            var resolverType = Type.GetType("Tactics.Controllers.TurnResolvers.UnitSpeedTurnResolver, com.tactics");
            if (resolverType != null)
            {
                var resolver = Activator.CreateInstance(resolverType);
                var resolverProp = controllerType.GetProperty("TurnResolver", BindingFlags.Instance | BindingFlags.Public);
                resolverProp?.SetValue(bc, resolver);
            }

            var initMethod = controllerType.GetMethod("InitializeAndStart", BindingFlags.Instance | BindingFlags.Public);
            initMethod?.Invoke(bc, new object[] { false });

            // Wait for AI players to execute
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

            if (_cellManagerRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(_cellManagerRoot);
                _cellManagerRoot = null;
            }

            if (_battleRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(_battleRoot);
                _battleRoot = null;
            }

            TestGameAssetHelper.Cleanup();

            yield return null;
        }

        [UnityTest]
        public IEnumerator GameAssetManager_CanLoadRealConfigs()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            // Verify GameAssetManager can load real game configs
            Assert.IsNotNull(_assetManager, "GameAssetManager should exist.");
            Assert.IsTrue(_assetManager.IsInitialized, "GameAssetManager should be initialized.");

            // Load RoleConfig
            var barbarianConfig = _assetManager.Load<RoleConfig>("Assets/Tactics/Battle/Classes/Barbarian.asset");
            Assert.IsNotNull(barbarianConfig, "Barbarian RoleConfig should load.");
            Assert.AreEqual(RoleType.Barbarian, barbarianConfig.RoleType, "Should be Barbarian role.");
            Assert.That(barbarianConfig.Abilities.Count, Is.GreaterThan(0), "Barbarian should have abilities.");

            // Load AiBrainAsset
            var brainAsset = _assetManager.Load<AiBrainAsset>("Assets/Tactics/AI/BasicMeleeBrain.asset");
            Assert.IsNotNull(brainAsset, "BasicMeleeBrain should load.");
            Assert.IsTrue(brainAsset.IsValid(), "AiBrainAsset should be valid.");

            // Verify battle controller exists
            var controller = BattleController.Instance;
            Assert.IsNotNull(controller, "BattleController.Instance should exist.");

            // Verify units are registered with abilities
            var units = controller.GetUnits().ToList();
            Assert.That(units.Count, Is.GreaterThanOrEqualTo(2), "Should have at least 2 units.");

            var p1Unit = units.FirstOrDefault(u => u.PlayerNumber == 1);
            var p2Unit = units.FirstOrDefault(u => u.PlayerNumber == 2);
            Assert.IsNotNull(p1Unit, "P1 unit should exist.");
            Assert.IsNotNull(p2Unit, "P2 unit should exist.");

            // Verify units have abilities
            var p1Abilities = p1Unit.GetBaseAbilities().ToList();
            Assert.That(p1Abilities.Count, Is.GreaterThan(0), "P1 unit should have abilities.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBattleWithRealSkillGraph()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteBattlePlan(GetPlanPath("battle-assets", "battle-with-real-skill-graph.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "battleIsActive" && assertion.Passed), Is.True, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBattleWithRealFireball()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteBattlePlan(GetPlanPath("battle-assets", "battle-with-real-fireball.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "battleIsActive" && assertion.Passed), Is.True, details);
        }

        private static string GetPlanPath(string fileName)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tests", "gameplay-specs", fileName));
        }

        private static string GetPlanPath(string subDir, string fileName)
        {
            // Use compiled/ directory for batch-compile output
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tests", "gameplay-specs", "compiled", fileName));
        }

        private static async Task<GameplayTestResult> ExecuteBattlePlan(string planPath)
        {
            Assert.IsTrue(File.Exists(planPath), $"Plan file not found: {planPath}");
            Assert.IsNotNull(GetBattleControllerInstance(), "BattleController.Instance should be injected by SetUp.");
            var plan = ExecutableScenarioPlanLoader.FromFile(planPath);
            var runner = new GameplayRuntimeRunner(new IGameplayStepAdapter[]
            {
                new SkillGameplayStepAdapter(),
                new BattleGameplayStepAdapter()
            });
            return await runner.ExecuteAsync(plan);
        }

        private static object GetBattleControllerInstance()
        {
            var controllerType = ResolveBattleControllerType();
            if (controllerType == null)
                return null;

            var property = controllerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            return property?.GetValue(null);
        }

        private static Type ResolveBattleControllerType()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly =>
                {
                    try { return assembly.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .FirstOrDefault(type => type.FullName == "Tactics.Common.Battle.BattleController");
        }

        private static ICell FindCell(GameObject cellManagerRoot, int x, int y)
        {
            var squares = cellManagerRoot.GetComponentsInChildren<Square>();
            foreach (var square in squares)
            {
                if (square.GridCoordinates.x == x && square.GridCoordinates.y == y)
                    return square;
            }
            return null;
        }

        private static IEnumerator WaitForTask(Task task)
        {
            while (!task.IsCompleted)
                yield return null;
            if (task.IsFaulted)
                Assert.Fail($"Task failed: {task.Exception}");
        }
    }
}
