using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.Common.Battle;
using Tactics.Common.Cells;
using Tactics.Common.Testing.Gameplay;
using Tactics.Common.Units;
using Tactics.Common.Utilities;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public class GameplayRuntimeMapPlanTests
    {
        private GameObject _battleRoot;
        private GameObject _cellManagerRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

            var controllerType = ResolveBattleControllerType();
            Assume.That(controllerType, Is.Not.Null, "BattleController type should exist.");

            _battleRoot = new GameObject("TestBattleControllerMap");
            var bc = (MonoBehaviour)_battleRoot.AddComponent(controllerType);

            var startFlag = controllerType.GetField("_startImmediatelly", BindingFlags.Instance | BindingFlags.NonPublic);
            startFlag?.SetValue(bc, false);

            // 2x2 grid
            _cellManagerRoot = new GameObject("TestCellManagerMap");
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

            var awake = controllerType.GetMethod("Awake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            awake?.Invoke(bc, null);

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
            unit1.CurrentCell = FindCell(_cellManagerRoot, 0, 0);

            var unit2Go = new GameObject("Unit_P2");
            unit2Go.transform.SetParent(unitContainer);
            var unit2 = unit2Go.AddComponent<Unit>();
            unit2.PlayerNumber = 2;
            unit2.CurrentCell = FindCell(_cellManagerRoot, 1, 0);

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

            yield return null;
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesMapEnterNode()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteMapPlan(GetPlanPath("map", "map-enter-node.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesMapBattleNode()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteMapPlan(GetPlanPath("map", "map-battle-node.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesMapEventNode()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteMapPlan(GetPlanPath("map", "map-event-node.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesEventResultItemInventory()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteMapPlan(GetPlanPath("map", "event-result-item-inventory.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesEventResultSelfHeal()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteMapPlan(GetPlanPath("map", "event-result-self-heal.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesEventResultAllHeal()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteMapPlan(GetPlanPath("map", "event-result-all-heal.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesEventResultRandomAllyDamage()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteMapPlan(GetPlanPath("map", "event-result-random-ally-damage.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesEventResultSelfBuff()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteMapPlan(GetPlanPath("map", "event-result-self-buff.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesEventResultAllBuff()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteMapPlan(GetPlanPath("map", "event-result-all-buff.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesEventResultAllDebuff()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteMapPlan(GetPlanPath("map", "event-result-all-debuff.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesEventResultPersistenceWriteback()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteMapPlan(GetPlanPath("map", "event-result-persistence-writeback.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesMapBattleResultWriteback()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteMapPlan(GetPlanPath("map", "battle-result-writeback.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesMapRestSiteRepairWriteback()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteMapPlan(GetPlanPath("map", "restsite-repair-writeback.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesMapShopGoldConversion()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteMapPlan(GetPlanPath("map", "shop-gold-conversion.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesTask1MinLoop2Rounds()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteMapPlan(GetPlanPath("map", "task1-min-loop-2rounds.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesMapBattleDeathWriteback()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteMapPlan(GetPlanPath("map", "battle-death-writeback.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesMapBattleGrowthWriteback()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteMapPlan(GetPlanPath("map", "battle-growth-writeback.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesMapRestSiteSkipsDead()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteMapPlan(GetPlanPath("map", "restsite-skips-dead.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesMapBattleDeathEquipmentRetained()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteMapPlan(GetPlanPath("map", "battle-death-equipment-retained.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesMapBattleDeathConsumableRetained()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteMapPlan(GetPlanPath("map", "battle-death-consumable-retained.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesMapShopStaffMageIntelligence()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteMapPlan(GetPlanPath("map", "shop-staff-mage-intelligence.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesMapShopBowHunterAgility()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteMapPlan(GetPlanPath("map", "shop-bow-hunter-agility.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        private static string GetPlanPath(string subDir, string fileName)
        {
            // Use compiled/ directory for batch-compile output
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tests", "gameplay-specs", "compiled", fileName));
        }

        private static async Task<GameplayTestResult> ExecuteMapPlan(string planPath)
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
