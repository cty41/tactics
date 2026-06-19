using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Tactics.Common.Battle;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Controllers.GameResolvers;
using Tactics.Common.Controllers.GridStates;
using Tactics.Common.Controllers.TurnResolvers;
using Tactics.Common.Testing.Gameplay;
using Tactics.Common.Units;
using Tactics.Common.Units.Buffs;
using Tactics.Common.Utilities;
using Tactics.Controllers.TurnResolvers;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    /// <summary>
    /// Regression tests for code review findings:
    /// 1. First frozen human turn auto-ends without deadlock (freeze applied BEFORE StartGame)
    /// 2. Consecutive battles don't interfere via HumanPlayer state
    /// 3. Initialized-not-started controller is correctly completed by bindBattleController
    /// 4. Consecutive binds clear stale Units/Cells aliases
    /// 5. Resolver mismatch on already-started controller causes bind to fail
    /// 6. Host-null fallback still auto-ends the first unactionable human turn
    /// 7. Successful rebind clears LastBattleResult
    /// 8. AdvanceTurn restores DisableAiAutoPlay when EndTurn throws
    /// 9. Host-null fallback + same-frame EndBattle discards auto-EndTurn
    /// 10. SubsequentTurnResolverImpl + turnResolver='subsequent' binds successfully
    /// </summary>
    public class HumanPlayerBattleAdapterRegressionTests
    {
        private GameObject _battleRoot;
        private GameObject _cellManagerRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
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
            yield return null;
        }

        /// <summary>
        /// Scenario 1: First frozen human turn auto-ends without deadlock.
        /// The human unit is frozen BEFORE StartGame so the very first Play() call
        /// hits the no-actionable-unit path. Verifies the turn actually advances
        /// to the next player (not just "didn't timeout").
        /// </summary>
        [UnityTest]
        public IEnumerator FrozenHumanTurn_AutoEndsWithoutDeadlock()
        {
            CreateBattleScaffolding(out var bc, createUnits: true);

            // InitializeGame first — this initializes units (BuffComponent created) and players.
            bc.InitializeGame(false);
            yield return null;

            // Freeze the human unit BEFORE StartGame so the first Play() sees no actionable unit.
            var humanUnit = bc.GetUnits().FirstOrDefault(u => u.PlayerNumber == 1);
            Assert.IsNotNull(humanUnit, "Human unit should exist after InitializeGame.");

            var frozenConfig = CreateFrozenBuffConfig();
            Assert.IsNotNull(frozenConfig, "Frozen BuffConfig should be created.");
            var buff = new Buff(frozenConfig, humanUnit, 1);
            humanUnit.AddBuff(buff);
            Assert.IsFalse(humanUnit.CanAct, "Human unit must be frozen (CanAct=false) before StartGame.");

            // Disable AI auto-play so the turn stays on P2 after advancing (deterministic assertion).
            bc.DisableAiAutoPlay = true;

            // Now start the game — first human turn's Play() will schedule auto-EndTurn.
            bc.StartGame(false);
            // StartBattleAsync sets IsBattleActive=true, which HumanPlayer's guard checks
            // before firing the queued auto-EndTurn.
            _ = bc.StartBattleAsync();

            // Wait enough frames for the one-frame-delayed auto-EndTurn coroutine to fire.
            for (int i = 0; i < 15; i++)
            {
                yield return null;
            }

            // The turn must have advanced past the frozen human player.
            Assert.IsNotNull(bc.TurnContext.CurrentPlayer,
                "TurnContext.CurrentPlayer should not be null after auto-EndTurn.");
            Assert.AreNotEqual(1, bc.TurnContext.CurrentPlayer.PlayerNumber,
                "Turn should have advanced past the frozen human player (P1).");

            // The frozen buff (duration=1) should have ticked on OnTurnEnd and expired.
            Assert.IsTrue(humanUnit.CanAct,
                "Frozen buff should have expired after the human turn's OnTurnEnd ticked it (duration was 1).");
        }

        /// <summary>
        /// Scenario 2: Two consecutive battles don't interfere.
        /// Verifies that the second battle's HumanPlayer state is clean.
        /// </summary>
        [UnityTest]
        public IEnumerator ConsecutiveBattles_DontInterfere()
        {
            // First battle
            {
                CreateBattleScaffolding(out var bc, createUnits: true);
                SetUnitSpeedResolver(bc);
                bc.InitializeAndStart(false);
                yield return null;
                yield return null;

                bc.EndBattle(new GameResult());
                yield return null;
            }

            DestroyScaffolding();
            yield return null;
            yield return null;

            // Second battle — should start clean
            {
                CreateBattleScaffolding(out var bc, createUnits: true);
                SetUnitSpeedResolver(bc);
                bc.InitializeAndStart(false);
                yield return null;
                yield return null;

                Assert.IsNotNull(bc.TurnContext.CurrentPlayer,
                    "Second battle should have a current player.");
                Assert.Pass("Second battle started cleanly without interference from first.");
            }
        }

        /// <summary>
        /// Scenario 3: Initialized-not-started controller is correctly completed by bindBattleController.
        /// Creates a controller that has only had InitializeGame() called (not StartGame()),
        /// then verifies bindBattleController completes the startup via the adapter directly.
        /// </summary>
        [UnityTest]
        public IEnumerator InitializedNotStarted_ControllerCompletesStartup()
        {
            CreateBattleScaffolding(out var bc, createUnits: true);
            SetUnitSpeedResolver(bc);

            // Only call InitializeGame, NOT StartGame
            bc.InitializeGame(false);
            yield return null;

            // Verify GridState is set but TurnContext.CurrentPlayer is null
            Assert.IsNotNull(bc.GridState, "GridState should be set after InitializeGame.");
            Assert.IsNull(bc.TurnContext.CurrentPlayer, "CurrentPlayer should be null before StartGame.");

            // Use the adapter directly to complete startup via bindBattleController
            var adapter = new BattleGameplayStepAdapter();
            var context = new GameplayRuntimeContext();
            var action = new ExecutableScenarioAction { Kind = "bindBattleController", Adapter = "Battle" };

            var task = adapter.ExecuteAsync(context, action);
            yield return new WaitUntil(() => task.IsCompleted);

            var result = task.Result;
            Assert.IsTrue(result.Passed,
                $"bindBattleController should complete startup from initialized-not-started state. Details: {result.Message}");
            Assert.IsNotNull(bc.TurnContext.CurrentPlayer,
                "CurrentPlayer should be non-null after adapter completed startup.");

            context.Dispose();
        }

        /// <summary>
        /// Scenario 4: Consecutive binds clear stale Units/Cells aliases.
        /// Verifies that BindBattleController clears context.Units/Cells before
        /// registering new aliases, so old battle aliases don't leak.
        /// </summary>
        [UnityTest]
        public IEnumerator ConsecutiveBinds_ClearStaleAliases()
        {
            CreateBattleScaffolding(out var bc, createUnits: true);
            SetUnitSpeedResolver(bc);
            bc.InitializeAndStart(false);
            yield return null;
            yield return null;

            var adapter = new BattleGameplayStepAdapter();
            var context = new GameplayRuntimeContext();
            var action = new ExecutableScenarioAction { Kind = "bindBattleController", Adapter = "Battle" };

            // First bind — populates context.Units with p1_0, p2_0, etc.
            var task1 = adapter.ExecuteAsync(context, action);
            yield return new WaitUntil(() => task1.IsCompleted);
            Assert.IsTrue(task1.Result.Passed, "First bind should succeed.");

            int firstUnitCount = context.Units.Count;
            int firstCellCount = context.Cells.Count;
            Assert.Greater(firstUnitCount, 0, "First bind should register unit aliases.");

            // Inject a stale battle-pattern alias (simulates an old battle's unit with a player
            // number not present in the current battle). This should be cleared on rebind.
            context.Units["p99_0"] = bc.GetUnits().First();
            // Inject a non-battle alias (e.g. registered by Skill adapter's createUnit).
            // This must survive rebind since it doesn't match the battle alias pattern.
            context.Units["skill_unit_0"] = bc.GetUnits().First();

            // Second bind — should clear stale battle aliases but preserve non-battle aliases.
            var task2 = adapter.ExecuteAsync(context, action);
            yield return new WaitUntil(() => task2.IsCompleted);
            Assert.IsTrue(task2.Result.Passed, "Second bind should succeed.");

            Assert.IsFalse(context.Units.ContainsKey("p99_0"),
                "Stale battle-pattern alias should have been cleared on rebind.");
            Assert.IsTrue(context.Units.ContainsKey("skill_unit_0"),
                "Non-battle alias registered by other adapters must survive rebind.");
            Assert.AreEqual(firstUnitCount + 1, context.Units.Count,
                "Unit alias count: first bind count + 1 preserved non-battle alias.");
            Assert.AreEqual(firstCellCount, context.Cells.Count,
                "Cell alias count after second bind should match first bind (not accumulate).");

            context.Dispose();
        }

        /// <summary>
        /// Scenario 5: Resolver mismatch on an already-started controller causes bind to fail.
        /// A controller already started with UnitSpeedTurnResolver cannot be re-bound with
        /// SubsequentTurnResolver — the adapter must reject this rather than silently succeed.
        /// This validates the adapter's ready-state guard on the State 3 path.
        /// </summary>
        [UnityTest]
        public IEnumerator ResolverMismatch_OnAlreadyStarted_BindFails()
        {
            CreateBattleScaffolding(out var bc, createUnits: true);
            // Start with UnitSpeed so the first bind (default action) succeeds. Then manually switch
            // the already-started controller to SubsequentTurnResolverImpl so the second bind
            // requests UnitSpeed and fails without destroying the existing context state.
            SetUnitSpeedResolver(bc);
            bc.InitializeAndStart(false);
            yield return null;
            yield return null;

            // Verify the controller is in State 3 (already ready)
            Assert.IsNotNull(bc.GridState, "GridState should be set.");
            Assert.IsNotNull(bc.TurnContext.CurrentPlayer, "CurrentPlayer should be non-null after InitializeAndStart.");

            var adapter = new BattleGameplayStepAdapter();
            var context = new GameplayRuntimeContext();
            var firstBindAction = new ExecutableScenarioAction { Kind = "bindBattleController", Adapter = "Battle" };

            var firstTask = adapter.ExecuteAsync(context, firstBindAction);
            yield return new WaitUntil(() => firstTask.IsCompleted);
            Assert.IsTrue(firstTask.Result.Passed, "First bind with matching resolver should succeed.");

            int boundUnitCount = context.Units.Count;
            int boundCellCount = context.Cells.Count;
            Assert.IsTrue(context.Units.ContainsKey("p1_0"), "Expected battle alias p1_0 after successful bind.");
            Assert.IsNotNull(context.BattleEndedHandler, "Successful bind should install BattleEnded handler.");

            bc.TurnResolver = new SubsequentTurnResolverImpl();

            var failingAction = new ExecutableScenarioAction
            {
                Kind = "bindBattleController",
                Adapter = "Battle"
            };

            var task = adapter.ExecuteAsync(context, failingAction);
            yield return new WaitUntil(() => task.IsCompleted);

            var result = task.Result;
            Assert.IsFalse(result.Passed,
                "bindBattleController should FAIL when trying to change resolver on an already-started controller.");
            Assert.IsTrue(result.Message.Contains("resolver"),
                $"Failure message should mention resolver mismatch. Actual: {result.Message}");
            // After normalization, the controller's SubsequentTurnResolverImpl should still be intact
            // (the adapter should NOT have overwritten it on failure)
            var currentResolverKind = bc.TurnResolver;
            Assert.IsTrue(currentResolverKind is SubsequentTurnResolverImpl,
                "Failed rebind must not rewrite the controller's current resolver.");
            Assert.AreEqual(boundUnitCount, context.Units.Count,
                "Failed rebind must not drop existing unit aliases from the context.");
            Assert.AreEqual(boundCellCount, context.Cells.Count,
                "Failed rebind must not drop existing cell aliases from the context.");
            Assert.IsTrue(context.Units.ContainsKey("p1_0"),
                "Failed rebind must preserve old battle aliases.");
            Assert.IsNotNull(context.BattleEndedHandler,
                "Failed rebind must preserve the old BattleEnded subscription.");

            var expectedResult = new GameResult();
            bc.EndBattle(expectedResult);
            Assert.AreEqual(expectedResult, context.LastBattleResult,
                "Old BattleEnded subscription must still update LastBattleResult after failed rebind.");

            context.Dispose();
        }

        [UnityTest]
        public IEnumerator HostNullFallback_AutoEndsFirstUnactionableHumanTurn()
        {
            CreateBattleScaffolding(out var bc, createUnits: true);
            bc.InitializeGame(false);
            yield return null;

            var humanUnit = bc.GetUnits().FirstOrDefault(u => u.PlayerNumber == 1);
            Assert.IsNotNull(humanUnit, "Human unit should exist after InitializeGame.");

            var frozenConfig = CreateFrozenBuffConfig();
            humanUnit.AddBuff(new Buff(frozenConfig, humanUnit, 1));
            Assert.IsFalse(humanUnit.CanAct, "Human unit must be frozen before StartGame.");

            var runtimePlayersField = typeof(BattleController).GetField("_runtimePlayers", BindingFlags.Instance | BindingFlags.NonPublic);
            var runtimePlayers = runtimePlayersField?.GetValue(bc) as System.Collections.IEnumerable;
            Assert.IsNotNull(runtimePlayers, "Runtime players should exist after InitializeGame.");

            object humanPlayer = null;
            foreach (var player in runtimePlayers)
            {
                var playerNumberProp = player.GetType().GetProperty("PlayerNumber");
                if ((int)playerNumberProp.GetValue(player) == 1)
                {
                    humanPlayer = player;
                    break;
                }
            }

            Assert.IsNotNull(humanPlayer, "Human player instance should exist.");
            var hostField = humanPlayer.GetType().GetField("_host", BindingFlags.Instance | BindingFlags.NonPublic);
            hostField?.SetValue(humanPlayer, null);

            bc.DisableAiAutoPlay = true;
            bc.StartGame(false);
            _ = bc.StartBattleAsync();

            Assert.IsTrue(bc.IsBattleActive, "Battle should be active after StartBattleAsync.");
            Assert.IsNotNull(BattleController.Instance, "BattleController.Instance should be set.");

            for (int i = 0; i < 15; i++)
            {
                yield return null;
            }

            Assert.IsNotNull(bc.TurnContext.CurrentPlayer,
                "TurnContext.CurrentPlayer should not be null after host-null fallback auto-end.");
            Assert.AreNotEqual(1, bc.TurnContext.CurrentPlayer.PlayerNumber,
                "Host-null fallback should still advance past the first frozen human turn.");
        }

        [UnityTest]
        public IEnumerator SuccessfulRebind_ClearsLastBattleResult()
        {
            CreateBattleScaffolding(out var bc, createUnits: true);
            SetUnitSpeedResolver(bc);
            bc.InitializeAndStart(false);
            yield return null;
            yield return null;

            var adapter = new BattleGameplayStepAdapter();
            var context = new GameplayRuntimeContext();
            var action = new ExecutableScenarioAction { Kind = "bindBattleController", Adapter = "Battle" };

            var firstTask = adapter.ExecuteAsync(context, action);
            yield return new WaitUntil(() => firstTask.IsCompleted);
            Assert.IsTrue(firstTask.Result.Passed, "First bind should succeed.");

            var oldResult = new GameResult();
            bc.EndBattle(oldResult);
            Assert.AreEqual(oldResult, context.LastBattleResult,
                "BattleEnded handler should record the first battle result.");

            var secondTask = adapter.ExecuteAsync(context, action);
            yield return new WaitUntil(() => secondTask.IsCompleted);
            Assert.IsTrue(secondTask.Result.Passed, "Second bind should succeed.");
            Assert.IsNull(context.LastBattleResult,
                "Successful rebind must clear LastBattleResult so the new battle does not inherit the old result.");

            context.Dispose();
        }

        [UnityTest]
        public IEnumerator AdvanceTurn_RestoresDisableAiAutoPlay_WhenEndTurnThrows()
        {
            CreateBattleScaffolding(out var bc, createUnits: true);
            SetUnitSpeedResolver(bc);
            bc.InitializeAndStart(false);
            yield return null;
            yield return null;

            var adapter = new BattleGameplayStepAdapter();
            var context = new GameplayRuntimeContext();
            var bindAction = new ExecutableScenarioAction { Kind = "bindBattleController", Adapter = "Battle" };
            var bindTask = adapter.ExecuteAsync(context, bindAction);
            yield return new WaitUntil(() => bindTask.IsCompleted);
            Assert.IsTrue(bindTask.Result.Passed, "Bind should succeed before testing AdvanceTurn failure recovery.");

            bc.DisableAiAutoPlay = true;
            bc.GridState = new ThrowingGridState();

            var advanceAction = new ExecutableScenarioAction { Kind = "advanceTurn", Adapter = "Battle" };
            var advanceTask = adapter.ExecuteAsync(context, advanceAction);
            yield return new WaitUntil(() => advanceTask.IsCompleted);

            Assert.IsFalse(advanceTask.Result.Passed, "AdvanceTurn should fail when GridState.EndTurn throws.");
            Assert.IsTrue(bc.DisableAiAutoPlay,
                "AdvanceTurn must restore DisableAiAutoPlay to its previous value when EndTurn throws.");

            context.Dispose();
        }

        [UnityTest]
        public IEnumerator HostNullFallback_SameFrameEndBattle_DoesNotEndTurn()
        {
            CreateBattleScaffolding(out var bc, createUnits: true);
            bc.InitializeGame(false);
            yield return null;

            var humanUnit = bc.GetUnits().FirstOrDefault(u => u.PlayerNumber == 1);
            Assert.IsNotNull(humanUnit, "Human unit should exist after InitializeGame.");

            var frozenConfig = CreateFrozenBuffConfig();
            humanUnit.AddBuff(new Buff(frozenConfig, humanUnit, 1));
            Assert.IsFalse(humanUnit.CanAct, "Human unit must be frozen before StartGame.");

            // Force _host to null so fallback path is used
            var runtimePlayersField = typeof(BattleController).GetField("_runtimePlayers", BindingFlags.Instance | BindingFlags.NonPublic);
            object humanPlayer = null;
            foreach (var player in (System.Collections.IEnumerable)runtimePlayersField.GetValue(bc))
            {
                var pn = player.GetType().GetProperty("PlayerNumber");
                if ((int)pn.GetValue(player) == 1) { humanPlayer = player; break; }
            }
            Assert.IsNotNull(humanPlayer, "Human player instance should exist.");
            humanPlayer.GetType().GetField("_host", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(humanPlayer, null);

            bc.DisableAiAutoPlay = true;
            bc.StartGame(false);
            _ = bc.StartBattleAsync();

            // Same frame: end the battle BEFORE the fallback's NextFrameAsync fires
            bc.EndBattle(new GameResult());

            // Wait for the fallback's NextFrameAsync to fire
            for (int i = 0; i < 15; i++) yield return null;

            // The turn must NOT have advanced — battle ended, auto-EndTurn should be discarded
            // If the guard failed, the turn would have advanced to P2 (PlayerNumber != 1)
            Assert.AreEqual(1, bc.TurnContext.CurrentPlayer.PlayerNumber,
                "After same-frame EndBattle, the fallback auto-EndTurn must be discarded — turn should NOT advance.");
        }

        [UnityTest]
        public IEnumerator SubsequentImplWithSubsequentParam_BindSucceeds()
        {
            CreateBattleScaffolding(out var bc, createUnits: true);
            // Start with SubsequentTurnResolverImpl (the Impl, not the wrapper)
            bc.TurnResolver = new SubsequentTurnResolverImpl();
            bc.InitializeAndStart(false);
            yield return null;
            yield return null;

            var adapter = new BattleGameplayStepAdapter();
            var context = new GameplayRuntimeContext();
            // Request "subsequent" — adapter creates SubsequentTurnResolver (wrapper)
            // This should be treated as equivalent to the Impl already on the controller
            var action = new ExecutableScenarioAction { Kind = "bindBattleController", Adapter = "Battle" };
            // Use reflection to set Parameters["turnResolver"] = "subsequent"
            // (test assembly doesn't reference Newtonsoft.Json directly)
            SetActionParameter(action, "turnResolver", "subsequent");

            var task = adapter.ExecuteAsync(context, action);
            yield return new WaitUntil(() => task.IsCompleted);

            Assert.IsTrue(task.Result.Passed,
                $"SubsequentTurnResolverImpl + turnResolver='subsequent' should be treated as compatible. Details: {task.Result.Message}");
            Assert.IsTrue(context.Units.Count > 0, "Bind should register unit aliases.");
            Assert.IsNotNull(context.BattleEndedHandler, "Bind should install BattleEnded handler.");

            context.Dispose();
        }

        #region Helpers

        private void CreateBattleScaffolding(out BattleController bc, bool createUnits)
        {
            _battleRoot = new GameObject("TestBattleController_Regression");
            bc = _battleRoot.AddComponent<BattleController>();

            // Disable auto-start (Awake will be re-invoked manually after fields are set)
            var controllerType = typeof(BattleController);
            var startFlag = controllerType.GetField("_startImmediatelly", BindingFlags.Instance | BindingFlags.NonPublic);
            startFlag?.SetValue(bc, false);

            // Create cell manager + 2x2 grid
            _cellManagerRoot = new GameObject("TestCellManager_Regression");
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

            // Set _cellManager field
            var cellMgrField = controllerType.GetField("_cellManager", BindingFlags.Instance | BindingFlags.NonPublic);
            cellMgrField?.SetValue(bc, cellMgr);

            // Re-invoke Awake to register singleton and wire _controller dependencies
            var awake = controllerType.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            awake?.Invoke(bc, null);

            // Clear BeforeUnitManagerInitialize callback (prevent SpawnEncounterUnits from running)
            var gridControllerField = controllerType.GetField("_controller", BindingFlags.Instance | BindingFlags.NonPublic);
            var gridController = gridControllerField?.GetValue(bc);
            if (gridController != null)
            {
                var beforeInitProp = gridController.GetType().GetProperty("BeforeUnitManagerInitialize");
                beforeInitProp?.SetValue(gridController, null);
            }

            // Configure players: 1 Human + 1 AI
            bc.SetPlayers(1, 1);

            if (createUnits)
            {
                // Create unit container if not already set
                var unitField = controllerType.GetField("_unitContainer", BindingFlags.Instance | BindingFlags.NonPublic);
                Transform unitContainer = (Transform)unitField?.GetValue(bc);
                if (unitContainer == null)
                {
                    var containerGo = new GameObject("UnitContainer");
                    containerGo.transform.SetParent(_battleRoot.transform);
                    unitField?.SetValue(bc, containerGo.transform);
                    unitContainer = containerGo.transform;
                }

                // P1 unit (Human)
                var unit1Go = new GameObject("TestUnit_P1");
                unit1Go.transform.SetParent(unitContainer);
                var unit1 = unit1Go.AddComponent<Unit>();
                unit1.PlayerNumber = 1;
                unit1.CurrentCell = FindCell(_cellManagerRoot, 0, 0);

                // P2 unit (AI)
                var unit2Go = new GameObject("TestUnit_P2");
                unit2Go.transform.SetParent(unitContainer);
                var unit2 = unit2Go.AddComponent<Unit>();
                unit2.PlayerNumber = 2;
                unit2.CurrentCell = FindCell(_cellManagerRoot, 1, 0);
            }
        }

        private void DestroyScaffolding()
        {
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
        }

        private static void SetUnitSpeedResolver(BattleController bc)
        {
            bc.TurnResolver = new UnitSpeedTurnResolver();
        }

        /// <summary>
        /// Creates a minimal BuffConfig with _canAct=false (frozen) using reflection.
        /// Does not use Resources.Load — constructs entirely in memory.
        /// </summary>
        private static BuffConfig CreateFrozenBuffConfig()
        {
            var config = ScriptableObject.CreateInstance<BuffConfig>();
            var configType = typeof(BuffConfig);

            var nameField = configType.GetField("_buffName", BindingFlags.NonPublic | BindingFlags.Instance);
            nameField?.SetValue(config, "TestFrozen");

            var canActField = configType.GetField("_canAct", BindingFlags.NonPublic | BindingFlags.Instance);
            canActField?.SetValue(config, false);

            var durationField = configType.GetField("_defaultDuration", BindingFlags.NonPublic | BindingFlags.Instance);
            durationField?.SetValue(config, 1);

            return config;
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

        private static void SetActionParameter(ExecutableScenarioAction action, string key, string value)
        {
            // Build a JSON object { key: value } and assign to action.Parameters via reflection,
            // avoiding a direct Newtonsoft.Json reference in the test assembly.
            var parameters = action.GetType().GetProperty("Parameters")?.GetValue(action);
            var parseMethod = parameters?.GetType().GetMethod("Parse", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(string) }, null);
            var newParams = parseMethod?.Invoke(null, new object[] { $"{{\"{key}\":\"{value}\"}}" });
            action.GetType().GetProperty("Parameters")?.SetValue(action, newParams);
        }

        private sealed class ThrowingGridState : GridState
        {
            public override void EndTurn(GridController gridController, bool isNetworkInvoked = false)
            {
                throw new InvalidOperationException("ThrowingGridState forced EndTurn failure for regression testing.");
            }
        }

        #endregion
    }
}
