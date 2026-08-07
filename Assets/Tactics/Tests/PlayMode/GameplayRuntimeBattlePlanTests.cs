using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.AssetPipeline;
using Tactics.Common.Battle;
using Tactics.Common.Cells;
using Tactics.Common.Controllers.GameResolvers;
using Tactics.Common.Testing.Gameplay;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Common.Utilities;
using Tactics.Roguelike;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public class GameplayRuntimeBattlePlanTests
    {
        private GameObject _battleRoot;
        private GameObject _cellManagerRoot;
        private bool _originalIgnoreFailingMessages;
        private bool _suiteOriginalIgnoreFailingMessages;

        [OneTimeSetUp]
        public void CaptureSuiteLogAssertState()
        {
            _suiteOriginalIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = false;
        }

        [OneTimeTearDown]
        public void RestoreSuiteLogAssertState()
        {
            LogAssert.ignoreFailingMessages = _suiteOriginalIgnoreFailingMessages;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _originalIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            // 忽略前一个测试残留的 AIPlayer 异步错误（async void Play() 跨帧执行）
            LogAssert.ignoreFailingMessages = true;

            var assetTask = TestGameAssetHelper.EnsureInitialized();
            yield return new WaitUntil(() => assetTask.IsCompleted);
            Assume.That(assetTask.Result, Is.Not.Null, "GameAssetManager should be initialized.");

            var controllerType = ResolveBattleControllerType();
            Assume.That(controllerType, Is.Not.Null, "BattleController type should exist.");

            _battleRoot = new GameObject("TestBattleController");
            _battleRoot.SetActive(false);
            var bc = (MonoBehaviour)_battleRoot.AddComponent(controllerType);

            // 禁用 Start() 协程（依赖 GameAssetManager）
            var startFlag = controllerType.GetField("_startImmediatelly", BindingFlags.Instance | BindingFlags.NonPublic);
            startFlag?.SetValue(bc, false);

            // 创建 RegularCellManager + 4x2 网格，覆盖近战、远程与接敌路径。
            _cellManagerRoot = new GameObject("TestCellManager");
            var cellMgr = _cellManagerRoot.AddComponent<RegularCellManager>();
            for (int x = 0; x < 4; x++)
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

            // Activate only after serialized dependencies are assigned so Awake runs once.
            _battleRoot.SetActive(true);
            RoguelikeBattleReturnHandler.Instance.UnregisterController((BattleController)bc);
            bc.enabled = false;
            ((BattleController)bc).DisableAiAutoPlay = true;

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
            unit1.CurrentCell.CurrentUnits.Add(unit1);
            unit1.CurrentCell.IsTaken = true;

            // 创建 P2 单位（AI）
            var unit2Go = new GameObject("TestUnit_P2");
            unit2Go.transform.SetParent(unitContainer);
            var unit2 = unit2Go.AddComponent<Unit>();
            unit2.PlayerNumber = 2;
            unit2.CurrentCell = FindCell(_cellManagerRoot, 1, 0);
            unit2.CurrentCell.CurrentUnits.Add(unit2);
            unit2.CurrentCell.IsTaken = true;

            var meleeAttack = GameAssetManager.Instance.Load<SkillGraphAbilityConfig>(
                "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/MeleeAttack_Graph_Ability.asset");
            Assume.That(meleeAttack, Is.Not.Null, "Melee attack gameplay fixture is required.");
            unit1.ApplyAbilityConfigs(new AbilityConfig[] { meleeAttack });
            unit2.ApplyAbilityConfigs(new AbilityConfig[] { meleeAttack });

            // 设置 resolver 与 Test1.unity 一致（UnitSpeedTurnResolver），
            // 然后初始化战斗。这样 bindBattleController 检测到 resolver 匹配就不会 re-init。
            var resolverType = Type.GetType("Tactics.Controllers.TurnResolvers.UnitSpeedTurnResolver, com.tactics");
            if (resolverType != null)
            {
                var resolver = Activator.CreateInstance(resolverType);
                var resolverProp = controllerType.GetProperty("TurnResolver", BindingFlags.Instance | BindingFlags.Public);
                resolverProp?.SetValue(bc, resolver);
            }

            var initMethod = controllerType.GetMethod("InitializeAndStart", BindingFlags.Instance | BindingFlags.Public);
            initMethod?.Invoke(bc, new object[] { false });

            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var failures = new List<Exception>();
            BattleController battleController = null;

            // 取消 RoguelikeBattleReturnHandler 的订阅，防止访问已销毁对象
            if (_battleRoot != null)
            {
                battleController = _battleRoot.GetComponent<BattleController>();
                if (battleController != null)
                {
                    try
                    {
                        // 取消 BattleEnded 事件订阅
                        var handlerType = ResolveRoguelikeBattleReturnHandlerType();
                        if (handlerType != null)
                        {
                            var instanceProp = handlerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                            var instance = instanceProp?.GetValue(null);
                            if (instance != null)
                            {
                                var unregisterMethod = handlerType.GetMethod("UnregisterController", BindingFlags.Public | BindingFlags.Instance);
                                unregisterMethod?.Invoke(instance, new object[] { battleController });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        failures.Add(new InvalidOperationException("Unregister BattleController failed.", ex));
                    }
                }
            }

            Task runtimeTeardownTask = null;
            if (battleController != null)
            {
                try
                {
                    runtimeTeardownTask = battleController.IsBattleActive
                        ? battleController.EndBattleAsync(new GameResult())
                        : battleController.TeardownRuntimeScopeAsync();
                }
                catch (Exception ex)
                {
                    failures.Add(new InvalidOperationException("Starting BattleController teardown failed.", ex));
                }
            }

            if (runtimeTeardownTask != null)
            {
                yield return new WaitUntil(() => runtimeTeardownTask.IsCompleted);
                if (runtimeTeardownTask.IsFaulted)
                    failures.Add(runtimeTeardownTask.Exception.Flatten());
            }
            if (battleController?.RuntimeScopeTeardownException != null)
                failures.Add(battleController.RuntimeScopeTeardownException);

            try
            {
                if (_cellManagerRoot != null)
                    UnityEngine.Object.DestroyImmediate(_cellManagerRoot);
            }
            catch (Exception ex)
            {
                failures.Add(new InvalidOperationException("Destroying test cell manager failed.", ex));
            }
            finally
            {
                _cellManagerRoot = null;
            }

            try
            {
                if (_battleRoot != null)
                    UnityEngine.Object.DestroyImmediate(_battleRoot);
            }
            catch (Exception ex)
            {
                failures.Add(new InvalidOperationException("Destroying test BattleController failed.", ex));
            }
            finally
            {
                _battleRoot = null;
            }

            try
            {
                TestGameAssetHelper.Cleanup();
            }
            catch (Exception ex)
            {
                failures.Add(new InvalidOperationException("Cleaning test assets failed.", ex));
            }

            yield return null;
            LogAssert.ignoreFailingMessages = _originalIgnoreFailingMessages;
            if (failures.Count > 0)
                Assert.Fail(string.Join("\n", failures.Select(failure => failure.ToString())));
        }

        [UnityTest]
        [Order(-1000)]
        public IEnumerator CleanupProbe_FirstRunCapturesSuiteLogAssertState()
        {
            AssertCapturedLogAssertStateIsFalse();
            yield return null;
        }

        [UnityTest]
        [Order(-999)]
        public IEnumerator CleanupProbe_PreviousTeardownRestoredSuiteLogAssertState()
        {
            AssertCapturedLogAssertStateIsFalse();
            yield return null;
        }

        private void AssertCapturedLogAssertStateIsFalse()
        {
            var capturedState = GetType().GetField(
                "_originalIgnoreFailingMessages",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(capturedState, Is.Not.Null,
                "The fixture must capture the entering LogAssert state before changing it.");
            Assert.That((bool)capturedState.GetValue(this), Is.False,
                "Each SetUp must observe the false value restored by the previous TearDown.");
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBattleAdvanceTurnPlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
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

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "battleIsActive" && assertion.Passed), Is.True, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_BattleEndActionWaitsForControllerScopeDrain()
        {
            var plan = ExecutableScenarioPlanLoader.FromFile(GetPlanPath("battle-end-result.plan.json"));
            var gate = new GateStepAdapter();
            plan.RequiredAdapters.Add(gate.AdapterName);
            plan.RuntimeActions.Insert(0, new ExecutableScenarioAction
            {
                Adapter = gate.AdapterName,
                Kind = GateStepAdapter.ActionKind
            });

            var runner = new GameplayRuntimeRunner(new IGameplayStepAdapter[]
            {
                new BattleGameplayStepAdapter(),
                gate
            });
            Task<GameplayTestResult> runnerTask = runner.ExecuteAsync(plan);
            yield return new WaitUntil(() => gate.Entered.Task.IsCompleted);

            var controller = _battleRoot.GetComponent<BattleController>();
            Assert.That(controller.RuntimeScope, Is.Not.Null,
                "bindBattleController must establish the production battle runtime scope before the next action runs.");
            var trackedCompletion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            controller.RuntimeScope.Track(trackedCompletion.Task);

            gate.Release();
            yield return null;

            Assert.That(runnerTask.IsCompleted, Is.False,
                "endBattleWithResult must not let the gameplay runner return before the controller scope drains.");
            trackedCompletion.TrySetResult(true);
            yield return WaitForTask(runnerTask);

            Assert.That(runnerTask.Result.Passed, Is.True, string.Join("; ", runnerTask.Result.Diagnostics));
            Assert.That(controller.RuntimeScope, Is.Null,
                "The controller runtime scope must be released before the gameplay runner returns.");
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBattleFullCombatVictoryPlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteBattlePlan(GetPlanPath("battle-full-combat-victory.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitAliveEquals" && assertion.Passed), Is.True, details);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "battleResultEquals" && assertion.Passed), Is.True, details);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "battleIsActive" && assertion.Passed), Is.True, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBattleHealPlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteBattlePlan(GetPlanPath("battle-heal.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "battleIsActive" && assertion.Passed), Is.True, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBattleMoveUnitPlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteBattlePlan(GetPlanPath("battle-move-unit.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitPositionEquals" && assertion.Passed), Is.True, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBattleFireballAoEPlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteBattlePlan(GetPlanPath("battle-fireball-aoe.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "battleIsActive" && assertion.Passed), Is.True, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBattleMultiRoundPlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteBattlePlan(GetPlanPath("battle-multi-round.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "currentRoundEquals" && assertion.Passed), Is.True, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBattleBuffIgnitePlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteBattlePlan(GetPlanPath("battle-buff-ignite.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitHasBuff" && assertion.Passed), Is.True, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBattleAIAttackPlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteBattlePlan(GetPlanPath("battle-ai-attack.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBattleAIDecisionLogPlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteBattlePlan(GetPlanPath("battle-ai-decision-log.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "aiSelectedIntentTypeEquals" && assertion.Passed), Is.True, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBattleFrozenRestrictionPlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteBattlePlan(GetPlanPath("battle-frozen-restriction.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitHasBuff" && assertion.Passed), Is.True, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBattleFrozenBuffExpiryPlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteBattlePlan(GetPlanPath("battle-frozen-expiry.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBattleDotDamagePlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteBattlePlan(GetPlanPath("battle-dot-damage.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBattleBuffExpiryPlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteBattlePlan(GetPlanPath("battle-buff-expiry.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBattleMultiBuffPlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteBattlePlan(GetPlanPath("battle-multi-buff.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitHasBuff" && assertion.Passed), Is.True, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBattle3v3CombatPlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteBattlePlan(GetPlanPath("battle-3v3-combat.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitAliveEquals" && assertion.Passed), Is.True, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBattleAIBasicAttackExecutesDamagePlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteBattlePlan(GetPlanPath("battle-ai-basic-attack-executes-damage.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
            Assert.That(result.Assertions.Any(a => a.Kind == "aiWasNoOpEquals" && a.Passed), Is.True, details);
            Assert.That(result.Assertions.Any(a => a.Kind == "targetHealthChangedSinceStep" && a.Passed), Is.True, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBattleAIEngageIsNotNoOpPlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteBattlePlan(GetPlanPath("battle-ai-engage-is-not-noop.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
            Assert.That(result.Assertions.Any(a => a.Kind == "unitPositionChangedSinceStep" && a.Passed), Is.True, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBattleAIFreezeExpiryResumePlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteBattlePlan(GetPlanPath("battle-ai-freeze-expiry-resume.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
            Assert.That(result.Assertions.Any(a => a.Kind == "unitCanAct" && a.Passed), Is.True, details);
            Assert.That(result.Assertions.Any(a => a.Kind == "aiWasNoOpEquals" && a.Passed), Is.True, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBattleAIAbilityNameMappingPlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteBattlePlan(GetPlanPath("battle-ai-ability-name-mapping.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
            Assert.That(result.Assertions.Any(a => a.Kind == "aiWasNoOpEquals" && a.Passed), Is.True, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBattleAIRangedBasicAttackDamagePlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteBattlePlan(GetPlanPath("battle-ai-ranged-basic-attack-damage.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
            Assert.That(result.Assertions.Any(a => a.Kind == "targetHealthChangedSinceStep" && a.Passed), Is.True, details);
            Assert.That(result.Assertions.Any(a => a.Kind == "aiWasNoOpEquals" && a.Passed), Is.True, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBattleAIEngageThenAttackPlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecuteBattlePlan(GetPlanPath("battle-ai-engage-then-attack.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, Diagnostics=[{string.Join("; ", result.Diagnostics)}]";
            Assert.IsTrue(result.Passed, details);
            Assert.That(result.Assertions.Any(a => a.Kind == "unitPositionChangedSinceStep" && a.Passed), Is.True, details);
            Assert.That(result.Assertions.Any(a => a.Kind == "targetHealthChangedSinceStep" && a.Passed), Is.True, details);
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

        private static Type ResolveRoguelikeBattleReturnHandlerType()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly =>
                {
                    try { return assembly.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .FirstOrDefault(type => type.FullName == "Tactics.RoguelikeMap.RoguelikeBattleReturnHandler");
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

        private sealed class GateStepAdapter : IGameplayStepAdapter
        {
            public const string ActionKind = "waitForBattleDrainProbe";
            private readonly TaskCompletionSource<bool> _release = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

            public string AdapterName => "Gate";
            public TaskCompletionSource<bool> Entered { get; } = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

            public bool CanExecute(ExecutableScenarioAction action)
            {
                return action.Kind == ActionKind;
            }

            public async Task<GameplayStepResult> ExecuteAsync(
                GameplayRuntimeContext context,
                ExecutableScenarioAction action)
            {
                Entered.TrySetResult(true);
                await _release.Task;
                return GameplayStepResult.Pass(AdapterName, action.Kind, "Gate released.");
            }

            public bool CanAssert(ExecutableScenarioAssertion assertion)
            {
                return false;
            }

            public Task<GameplayAssertionResult> AssertAsync(
                GameplayRuntimeContext context,
                ExecutableScenarioAssertion assertion)
            {
                return Task.FromResult<GameplayAssertionResult>(null);
            }

            public ProbeSnapshot CaptureProbe(GameplayRuntimeContext context, GameplayProbeRequest request)
            {
                return null;
            }

            public void Release()
            {
                _release.TrySetResult(true);
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
