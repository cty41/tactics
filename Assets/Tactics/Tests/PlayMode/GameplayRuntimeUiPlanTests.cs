using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics;
using Tactics.AssetPipeline;
using Tactics.Common.Battle;
using Tactics.Common.Cells;
using Tactics.Common.Testing.Gameplay;
using Tactics.Common.Units;
using Tactics.Common.Utilities;
using Tactics.Roguelike;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public class GameplayRuntimeUiPlanTests
    {
        private GameObject _battleRoot;
        private GameObject _cellManagerRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            PureRunSessionStore.Clear();
            RoguelikeMapRuntimeState.ClearAll();
            BattleSettlementFlow.Instance.Unsubscribe();
            BattleSettlementCoordinator.Instance.Reset();

            DestroyCachedUiInstances();
            yield return null;

            // Initialize GameAssetManager for real asset loading
            var initTask = TestGameAssetHelper.EnsureInitialized();
            yield return new WaitUntil(() => initTask.IsCompleted);
            Assume.That(initTask.Result, Is.Not.Null, "GameAssetManager should be initialized.");

            // Create BattleController first (BattleUIController.FindFirstObjectByType needs it)
            var controllerType = ResolveBattleControllerType();
            Assume.That(controllerType, Is.Not.Null, "BattleController type should exist.");

            _battleRoot = new GameObject("TestBattleControllerUi");
            _battleRoot.SetActive(false);
            var bc = (MonoBehaviour)_battleRoot.AddComponent(controllerType);

            var startFlag = controllerType.GetField("_startImmediatelly", BindingFlags.Instance | BindingFlags.NonPublic);
            startFlag?.SetValue(bc, false);

            // 2x2 grid
            _cellManagerRoot = new GameObject("TestCellManagerUi");
            var cellMgr = _cellManagerRoot.AddComponent<RegularCellManager>();
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
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

            _battleRoot.SetActive(true);
            RoguelikeBattleReturnHandler.Instance.UnregisterController((BattleController)bc);
            bc.enabled = false;
            ((BattleController)bc).DisableAiAutoPlay = true;

            var gridControllerField = controllerType.GetField("_controller", BindingFlags.Instance | BindingFlags.NonPublic);
            var gridController = gridControllerField?.GetValue(bc);
            if (gridController != null)
            {
                var beforeInitProp = gridController.GetType().GetProperty("BeforeUnitManagerInitialize");
                beforeInitProp?.SetValue(gridController, null);
            }

            // 1 Human + 1 AI
            controllerType.GetMethod("SetPlayers", BindingFlags.Instance | BindingFlags.Public)?.Invoke(bc, new object[] { 1, 1 });

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

            // Create units
            var unit1Go = new GameObject("Unit_P1");
            unit1Go.transform.SetParent(unitContainer);
            var unit1 = unit1Go.AddComponent<Unit>();
            unit1.PlayerNumber = 1;
            unit1.Speed = 20f;
            unit1.CurrentCell = FindCell(_cellManagerRoot, 0, 0);
            unit1.CurrentCell.CurrentUnits.Add(unit1);
            unit1.CurrentCell.IsTaken = true;

            var unit2Go = new GameObject("Unit_P2");
            unit2Go.transform.SetParent(unitContainer);
            var unit2 = unit2Go.AddComponent<Unit>();
            unit2.PlayerNumber = 2;
            unit2.Speed = 10f;
            unit2.CurrentCell = FindCell(_cellManagerRoot, 1, 0);
            unit2.CurrentCell.CurrentUnits.Add(unit2);
            unit2.CurrentCell.IsTaken = true;

            // Initialize and start battle (registers units with UnitManager)
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

            // Show Battle UI through real asset pipeline
            var showTask = UIManager.Instance.ShowAsync(UIManager.UIId.Battle);
            yield return new WaitUntil(() => showTask.IsCompleted);

            // Wait for BattleUIController.WireButtonsDelayed coroutine (1 frame delay)
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

            PureRunSessionStore.Clear();
            RoguelikeMapRuntimeState.ClearAll();
            BattleSettlementFlow.Instance.Unsubscribe();
            BattleSettlementCoordinator.Instance.Reset();
            DestroyCachedUiInstances();
            yield return null;
            TestGameAssetHelper.Cleanup();

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

            yield return null;
        }

        private static void DestroyCachedUiInstances()
        {
            if (UIManager.Instance == null)
                return;

            UIManager.Instance.Destroy(UIManager.UIId.Battle);
            UIManager.Instance.Destroy(UIManager.UIId.Inventory);
            UIManager.Instance.Destroy(UIManager.UIId.LevelUp);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesUiElementVisibility()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteUiPlan(GetPlanPath("ui", "ui-element-visibility.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesUiMapBattleIntegration()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteUiPlan(GetPlanPath("ui", "ui-map-battle-integration.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBuffIconCountDisplay()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteUiPlan(GetPlanPath("ui", "buff-icon-count.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesInventoryItemPopover()
        {
            var task = ExecuteUiPlan(GetPlanPath("ui", "inventory-item-popover.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBattleConsumableSlot()
        {
            var task = ExecuteUiPlan(GetPlanPath("ui", "battle-consumable-slot.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesLevelUpMixedCandidateConfirmation()
        {
            var task = ExecuteUiPlan(GetPlanPath("compiled", "levelup-mixed-candidate-confirmation.plan.json"));
            yield return WaitForTask(task);
            AssertPlanPassed(task.Result);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesInventoryReadonlySkillTooltip()
        {
            var task = ExecuteUiPlan(GetPlanPath("compiled", "inventory-readonly-skill-tooltip.plan.json"));
            yield return WaitForTask(task);
            AssertPlanPassed(task.Result);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBattleUiTwoRowLayout()
        {
            var task = ExecuteUiPlan(GetPlanPath("compiled", "battle-ui-two-row-layout.plan.json"));
            yield return WaitForTask(task);
            AssertPlanPassed(task.Result);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBattleDisabledAbilityReason()
        {
            var task = ExecuteUiPlan(GetPlanPath("compiled", "battle-disabled-ability-reason.plan.json"));
            yield return WaitForTask(task);
            AssertPlanPassed(task.Result);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesAmazonMultiStabUiFlow()
        {
            var task = ExecuteUiPlan(GetPlanPath("compiled", "amazon-multi-stab-ui-flow.plan.json"));
            yield return WaitForTask(task);
            AssertPlanPassed(task.Result);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesPureRunRealPlayerRoute()
        {
            var task = ExecuteUiPlan(GetPlanPath("compiled", "pure-run-real-player-route.plan.json"));
            yield return WaitForTask(task);
            AssertPlanPassed(task.Result);
        }

        private static void AssertPlanPassed(GameplayTestResult result)
        {
            var stepTrace = string.Join("; ", result.ExecutedSteps.Select(step => $"{step.Kind}: {step.Message}"));
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, " +
                          $"Diagnostics=[{string.Join("; ", result.Diagnostics)}], StepTrace=[{stepTrace}]";
            Assert.IsTrue(result.Passed, details);
        }

        private static string GetPlanPath(string subDir, string fileName)
        {
            // Use compiled/ directory for batch-compile output
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tests", "gameplay-specs", "compiled", fileName));
        }

        private static async Task<GameplayTestResult> ExecuteUiPlan(string planPath)
        {
            Assert.IsTrue(File.Exists(planPath), $"Plan file not found: {planPath}");
            var plan = ExecutableScenarioPlanLoader.FromFile(planPath);
            var runner = new GameplayRuntimeRunner(new IGameplayStepAdapter[]
            {
                new SkillGameplayStepAdapter(),
                new BattleGameplayStepAdapter(),
                new MapGameplayStepAdapter(),
                new UiGameplayStepAdapter()
            });
            return await runner.ExecuteAsync(plan);
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
