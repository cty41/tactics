using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.Common.Cells;
using Tactics.Common.Testing.Gameplay;
using Tactics.Common.Units;
using Tactics.Common.Utilities;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public class GameplayRuntimeBattlePlanTests
    {
        private GameObject _battleRoot;
        private GameObject _cellManagerRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var controllerType = ResolveBattleControllerType();
            Assume.That(controllerType, Is.Not.Null, "BattleController type should exist.");

            _battleRoot = new GameObject("TestBattleController");
            var bc = (MonoBehaviour)_battleRoot.AddComponent(controllerType);

            // 禁用 Start() 协程（依赖 GameAssetManager）
            var startFlag = controllerType.GetField("_startImmediatelly", BindingFlags.Instance | BindingFlags.NonPublic);
            startFlag?.SetValue(bc, false);

            // 创建 RegularCellManager + 2x2 网格
            _cellManagerRoot = new GameObject("TestCellManager");
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

            // 设置 _cellManager 字段（Awake 会读取它）
            var cellMgrField = controllerType.GetField("_cellManager", BindingFlags.Instance | BindingFlags.NonPublic);
            cellMgrField?.SetValue(bc, cellMgr);

            // 手动触发 Awake()
            var awake = controllerType.GetMethod("Awake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            awake?.Invoke(bc, null);

            // 清除 BeforeUnitManagerInitialize 回调（避免 SpawnEncounterUnits 依赖 GameAssetManager）
            var gridControllerField = controllerType.GetField("_controller", BindingFlags.Instance | BindingFlags.NonPublic);
            var gridController = gridControllerField?.GetValue(bc);
            if (gridController != null)
            {
                var beforeInitProp = gridController.GetType().GetProperty("BeforeUnitManagerInitialize");
                beforeInitProp?.SetValue(gridController, null);
            }

            // 配置玩家：1 Human + 1 AI
            controllerType.GetMethod("SetPlayers", BindingFlags.Instance | BindingFlags.Public)?.Invoke(bc, new object[] { 1, 1 });

            // 创建单位
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

            // 创建 P1 单位（Human）
            var unit1Go = new GameObject("TestUnit_P1");
            unit1Go.transform.SetParent(unitContainer);
            var unit1 = unit1Go.AddComponent<Unit>();
            unit1.PlayerNumber = 1;
            unit1.CurrentCell = FindCell(_cellManagerRoot, 0, 0);

            // 创建 P2 单位（AI）
            var unit2Go = new GameObject("TestUnit_P2");
            unit2Go.transform.SetParent(unitContainer);
            var unit2 = unit2Go.AddComponent<Unit>();
            unit2.PlayerNumber = 2;
            unit2.CurrentCell = FindCell(_cellManagerRoot, 1, 0);

            // 初始化并开始战斗
            var initMethod = controllerType.GetMethod("InitializeAndStart", BindingFlags.Instance | BindingFlags.Public);
            initMethod?.Invoke(bc, new object[] { false });

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // 忽略销毁时和 AIPlayer 的错误日志
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
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBattleAdvanceTurnPlanFromFile()
        {
            var task = ExecuteBattlePlan(GetPlanPath("battle-advance-turn.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "battleIsActive" && assertion.Passed), Is.True, details);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "currentRoundEquals" && assertion.Passed), Is.True, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBattleEndResultPlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteBattlePlan(GetPlanPath("battle-end-result.plan.json"));
            yield return WaitForTask(task);
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "battleIsActive" && assertion.Passed), Is.True, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBattleFullCombatVictoryPlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteBattlePlan(GetPlanPath("battle-full-combat-victory.plan.json"));
            yield return WaitForTask(task);
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitHealthEquals" && assertion.Passed), Is.True, details);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitAliveEquals" && assertion.Passed), Is.True, details);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "battleResultEquals" && assertion.Passed), Is.True, details);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "battleIsActive" && assertion.Passed), Is.True, details);
        }

        private static string GetPlanPath(string fileName)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tests", "gameplay-specs", fileName));
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

        private static IEnumerator WaitForTask<T>(Task<T> task)
        {
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted)
            {
                var exception = task.Exception;
                if (exception?.InnerExceptions is { Count: > 0 })
                {
                    throw exception.InnerExceptions[0];
                }

                throw exception ?? new System.Exception("Task faulted.");
            }
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
