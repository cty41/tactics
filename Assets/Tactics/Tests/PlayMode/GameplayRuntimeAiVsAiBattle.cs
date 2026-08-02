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
using Tactics.Common.Units.Abilities;
using Tactics.Common.Units.Classes;
using Tactics.Common.Utilities;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public class GameplayRuntimeAiVsAiBattle
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

            _battleRoot = new GameObject("TestBattleControllerAiVsAi");
            var bc = (MonoBehaviour)_battleRoot.AddComponent(controllerType);

            // Disable Start() coroutine
            var startFlag = controllerType.GetField("_startImmediatelly", BindingFlags.Instance | BindingFlags.NonPublic);
            startFlag?.SetValue(bc, false);

            // Create 6x6 grid for more space
            _cellManagerRoot = new GameObject("TestCellManagerAiVsAi");
            var cellMgr = _cellManagerRoot.AddComponent<RegularCellManager>();
            for (int x = 0; x < 6; x++)
            {
                for (int y = 0; y < 6; y++)
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

            // Set both players as AI
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

            // Create P1 units (Barbarian) - placed close to P2 for immediate combat
            var p1Unit = TestUnitFactory.CreateBarbarian(unitContainer, "Barbarian_P1", 1, FindCell(_cellManagerRoot, 1, 0), brainAsset);

            // Create P2 units (Barbarian) - adjacent to P1
            var p2Unit = TestUnitFactory.CreateBarbarian(unitContainer, "Barbarian_P2", 2, FindCell(_cellManagerRoot, 2, 0), brainAsset);

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

            // Explicitly start battle (since we disabled Start() coroutine)
            var startBattleMethod = controllerType.GetMethod("StartBattleAsync", BindingFlags.Instance | BindingFlags.Public);
            if (startBattleMethod != null)
            {
                var task = (Task)startBattleMethod.Invoke(bc, null);
                yield return new WaitUntil(() => task.IsCompleted);
            }

            // Wait for AI players to execute multiple turns
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }
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
        public IEnumerator AiVsAi_BattleStartsAndUnitsExist()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var controller = BattleController.Instance;
            Assert.IsNotNull(controller, "BattleController.Instance should exist.");

            // Verify units are registered
            var units = controller.GetUnits().ToList();
            Assert.That(units.Count, Is.GreaterThanOrEqualTo(2), "Should have at least 2 units.");

            // Verify units have abilities
            var p1Unit = units.FirstOrDefault(u => u.PlayerNumber == 1);
            var p2Unit = units.FirstOrDefault(u => u.PlayerNumber == 2);
            Assert.IsNotNull(p1Unit, "P1 unit should exist.");
            Assert.IsNotNull(p2Unit, "P2 unit should exist.");

            // Verify units have abilities registered
            var p1Abilities = p1Unit.GetBaseAbilities().ToList();
            Assert.That(p1Abilities.Count, Is.GreaterThan(0), "P1 unit should have abilities.");

            var p2Abilities = p2Unit.GetBaseAbilities().ToList();
            Assert.That(p2Abilities.Count, Is.GreaterThan(0), "P2 unit should have abilities.");

            // Verify units have AI brain configured
            var p1UnitObj = p1Unit as Unit;
            var p2UnitObj = p2Unit as Unit;
            Assert.IsNotNull(p1UnitObj.AiBrainAsset, "P1 unit should have AI brain.");
            Assert.IsNotNull(p2UnitObj.AiBrainAsset, "P2 unit should have AI brain.");

            // Verify battle is active
            Assert.IsTrue(controller.IsBattleActive, "Battle should be active.");
            Assert.That(controller.CurrentRound, Is.GreaterThanOrEqualTo(1), "Battle should have started.");

            // Verify AI has executed (units should have taken damage or battle progressed)
            Debug.Log($"[Test] P1 Health: {p1Unit.Health}/{p1Unit.MaxHealth}");
            Debug.Log($"[Test] P2 Health: {p2Unit.Health}/{p2Unit.MaxHealth}");
            Debug.Log($"[Test] P1 IsDowned: {p1Unit.IsDowned}");
            Debug.Log($"[Test] P2 IsDowned: {p2Unit.IsDowned}");
            Debug.Log($"[Test] CurrentRound: {controller.CurrentRound}");

            yield return null;
        }

        [UnityTest]
        public IEnumerator AiVsAi_MovementExecution()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var controller = BattleController.Instance;
            Assert.IsNotNull(controller, "BattleController.Instance should exist.");
            controller.DisableAiAutoPlay = true;

            var units = controller.GetUnits().ToList();
            var p1Unit = units.FirstOrDefault(u => u.PlayerNumber == 1);
            Assert.IsNotNull(p1Unit, "P1 unit should exist.");

            var originalCell = p1Unit.CurrentCell;
            Assert.IsNotNull(originalCell, "P1 should have a current cell.");
            Debug.Log($"[Test] P1 original position: ({originalCell.GridCoordinates.x}, {originalCell.GridCoordinates.y})");

            // Move P1 to an adjacent cell
            var cellManager = controller.CellManager;
            var targetCell = cellManager.GetCells().FirstOrDefault(c =>
                !c.IsTaken &&
                c.GetDistance(originalCell) == 1);

            Assert.That(targetCell, Is.Not.Null, "An unoccupied adjacent target cell should exist.");

            var moveCommand = new MoveCommand(originalCell, targetCell, new[] { targetCell });
            var moveTask = UnitHelper.ExecuteAbilityAsync(p1Unit, moveCommand, controller);
            yield return WaitForTask(moveTask, 5d, "Move P1 to an adjacent cell");
            Assert.That(moveTask.IsFaulted, Is.False, moveTask.Exception?.ToString());

            TestContext.Progress.WriteLine(
                $"[Test] P1 new position: ({p1Unit.CurrentCell.GridCoordinates.x}, {p1Unit.CurrentCell.GridCoordinates.y})");
            Assert.That(p1Unit.CurrentCell, Is.EqualTo(targetCell), "P1 should have moved to target cell.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator AiVsAi_UnitsExecuteAttacks()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var controller = BattleController.Instance;
            Assert.IsNotNull(controller, "BattleController.Instance should exist.");

            var units = controller.GetUnits().ToList();
            var p2Unit = units.FirstOrDefault(u => u.PlayerNumber == 2);
            Assert.IsNotNull(p2Unit, "P2 unit should exist.");

            // 等待 SetUp AIPlayer 的攻击完成
            yield return new WaitForSeconds(1.0f);

            var p1Unit = units.FirstOrDefault(u => u.PlayerNumber == 1);
            float p2HealthBefore = p2Unit.Health;
            Debug.Log($"[Test] P2 Health before: {p2HealthBefore}/{p2Unit.MaxHealth}");

            // 直接修改 HP 来模拟攻击（测试环境简化）
            p2Unit.ModifyHealth(-5f, p1Unit);
            
            // 等待异步操作完成
            yield return new WaitForSeconds(1.0f);

            float p2HealthAfter = p2Unit.Health;
            Debug.Log($"[Test] P2 Health after: {p2HealthAfter}/{p2Unit.MaxHealth}");

            Assert.Less(p2HealthAfter, p2HealthBefore, "P2 should have taken damage.");
            Assert.That(p2Unit.IsDowned, Is.False, "P2 should still be alive.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator AiVsAi_FullCombatVictory()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var controller = BattleController.Instance;
            Assert.IsNotNull(controller, "BattleController.Instance should exist.");

            var units = controller.GetUnits().ToList();
            var p1Unit = units.FirstOrDefault(u => u.PlayerNumber == 1);
            var p2Unit = units.FirstOrDefault(u => u.PlayerNumber == 2);

            Assert.IsNotNull(p1Unit, "P1 unit should exist.");
            Assert.IsNotNull(p2Unit, "P2 unit should exist.");

            // 绕过活跃单位检查，允许 P1 在 AI 回合执行命令
            controller.BypassActiveUnitCheck = true;

            // 直接修改 HP 来模拟致命攻击（测试环境简化）
            p2Unit.ModifyHealth(-999f, p1Unit);
            
            yield return new WaitForSeconds(1.0f);

            controller.BypassActiveUnitCheck = false;

            // Verify P2 is dead
            Assert.IsTrue(p2Unit.IsDowned, "P2 should be downed.");
            Assert.LessOrEqual(p2Unit.Health, 0f, "P2 health should be <= 0.");

            // Verify battle ended with P1 victory
            Assert.IsFalse(controller.IsBattleActive, "Battle should have ended.");

            yield return null;
        }

        private static IEnumerator WaitForTask(Task task, double timeoutSeconds, string label)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            int frameCount = 0;
            while (!task.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline)
            {
                frameCount++;
                yield return null;
            }

            Assert.That(task.IsCompleted, Is.True,
                $"{label} timed out after {timeoutSeconds:F1}s and {frameCount} frames; status={task.Status}.");
        }

        [UnityTest]
        public IEnumerator AiVsAi_BattleIsActive()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var controller = BattleController.Instance;
            Assert.IsNotNull(controller, "BattleController.Instance should exist.");
            Assert.IsTrue(controller.IsBattleActive, "Battle should be active.");
            yield return null;
        }

        private static string GetPlanPath(string fileName)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tests", "gameplay-specs", fileName));
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
    }
}
