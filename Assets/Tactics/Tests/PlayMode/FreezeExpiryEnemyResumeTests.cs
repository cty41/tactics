using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.Common;
using Tactics.Common.Battle;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Controllers.GridStates;
using Tactics.Common.Controllers.TurnResolvers;
using Tactics.Common.Players;
using Tactics.Common.Testing.Gameplay;
using Tactics.Common.Units;
using Tactics.Common.Units.Buffs;
using Tactics.Common.Utilities;
using Tactics.Controllers.TurnResolvers;
using Tactics.Runtime.Utilities;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    /// <summary>
    /// Reproduction test for the bug: after freeze expires on an enemy AI unit,
    /// the enemy does not move until the caster (P1) moves. Uses real RoleConfig
    /// units with AI brains to verify AI behavior before and after freeze.
    /// </summary>
    public class FreezeExpiryEnemyResumeTests
    {
        private GameObject _battleRoot;
        private GameObject _cellManagerRoot;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            bool runtimeTeardownCompleted = true;
            bool runtimeTeardownFaulted = false;
            Exception runtimeTeardownException = null;
            if (_battleRoot != null)
            {
                var battleController = _battleRoot.GetComponent<BattleController>();
                if (battleController != null)
                {
                    Task teardownTask = battleController.TeardownRuntimeScopeAsync();
                    for (int frame = 0; frame < 60 && !teardownTask.IsCompleted; frame++)
                        yield return null;

                    runtimeTeardownCompleted = teardownTask.IsCompleted;
                    runtimeTeardownFaulted = teardownTask.IsFaulted;
                    runtimeTeardownException = battleController.RuntimeScopeTeardownException;
                }
            }

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
            GameTimeService.ForceResume();
            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Normal);
            TestGameAssetHelper.Cleanup();
            yield return null;
            LogAssert.ignoreFailingMessages = false;

            Assert.That(runtimeTeardownCompleted, Is.True,
                "Freeze fixture runtime teardown did not complete within 60 frames.");
            Assert.That(runtimeTeardownFaulted, Is.False,
                "Freeze fixture runtime teardown task faulted.");
            Assert.That(runtimeTeardownException, Is.Null,
                "Freeze fixture runtime teardown observed a tracked failure.");
            Assert.That(BattleController.Instance, Is.Null,
                "Freeze fixture leaked the BattleController singleton.");
        }

        /// <summary>
        /// Phase 1: verify P2 AI acts normally (moves/attacks) without freeze.
        /// Phase 2: freeze P2, wait for expiry, verify P2 acts again.
        /// </summary>
        [UnityTest]
        public IEnumerator FreezeExpires_EnemyAIResumesWithoutCasterAction()
        {
            // Initialize GameAssetManager for RoleConfig loading
            var initTask = TestGameAssetHelper.EnsureInitialized();
            float initializationDeadline = Time.realtimeSinceStartup + 10f;
            while (!initTask.IsCompleted && Time.realtimeSinceStartup < initializationDeadline)
                yield return null;
            Assert.That(initTask.IsCompleted, Is.True,
                "GameAssetManager initialization exceeded the 10 second realtime deadline.");
            Assert.That(initTask.IsFaulted, Is.False, initTask.Exception?.ToString());
            Assume.That(initTask.Result, Is.Not.Null,
                "GameAssetManager must initialize for RoleConfig loading.");

            CreateBattleScaffolding(out var bc, out Unit p1Unit, out Unit p2Unit);
            Assert.IsNotNull(p1Unit, "P1 unit must be created with RoleConfig.");
            Assert.IsNotNull(p2Unit, "P2 unit must be created with RoleConfig.");

            bool p1PerformedAction = false;
            bool p2PerformedAction = false;
            p1Unit.BasicAbilityUsed += _ => p1PerformedAction = true;
            p1Unit.AbilityUsed += _ => p1PerformedAction = true;
            p2Unit.BasicAbilityUsed += _ => p2PerformedAction = true;
            p2Unit.AbilityUsed += _ => p2PerformedAction = true;

            // UnitSpeedTurnResolver matches real game
            bc.TurnResolver = new UnitSpeedTurnResolver();

            // Initialize and start — both AI auto-play enabled
            bc.InitializeAndStart(false);
            _ = bc.StartBattleAsync();
            Assert.IsTrue(bc.IsBattleActive, "Battle should be active after StartBattleAsync.");

            // Record initial positions
            var p1InitialCell = p1Unit.CurrentCell;
            var p2InitialCell = p2Unit.CurrentCell;
            TLog.Info($"[TEST] Start: P1@{p1InitialCell?.GridCoordinates} HP={p1Unit.Health}/{p1Unit.MaxHealth}, P2@{p2InitialCell?.GridCoordinates} HP={p2Unit.Health}/{p2Unit.MaxHealth}");

            // === Phase 1: verify both AIs act without freeze ===
            bool p1Acted = false, p2Acted = false;
            float phaseOneDeadline = Time.realtimeSinceStartup + 4f;
            for (int i = 0; Time.realtimeSinceStartup < phaseOneDeadline; i++)
            {
                yield return null;

                if (!p1Acted && (p1PerformedAction || HasUnitActed(p1Unit, p1InitialCell, bc)))
                {
                    p1Acted = true;
                    TLog.Info($"[TEST] Phase1: P1 acted at frame {i+1}. Pos={p1Unit.CurrentCell?.GridCoordinates}, HP={p1Unit.Health}");
                }
                if (!p2Acted && (p2PerformedAction || HasUnitActed(p2Unit, p2InitialCell, bc)))
                {
                    p2Acted = true;
                    TLog.Info($"[TEST] Phase1: P2 acted at frame {i+1}. Pos={p2Unit.CurrentCell?.GridCoordinates}, HP={p2Unit.Health}");
                }

                if (p1Acted && p2Acted)
                    break;
                if (!bc.IsBattleActive)
                    break;
            }

            TLog.Info($"[TEST] Phase1 result: P1Acted={p1Acted}, P2Acted={p2Acted}");

            // Both AIs should act without freeze — this validates the test setup
            Assume.That(p2Acted, Is.True,
                $"P2 AI should act without freeze (test setup validation). " +
                $"If this fails, the AI brain/RoleConfig setup is wrong, not the freeze bug. " +
                $"P2.Pos={p2Unit.CurrentCell?.GridCoordinates}, P2.HP={p2Unit.Health}/{p2Unit.MaxHealth}");

            // Record P2 state after phase 1 (may have moved)
            var p2Phase1Cell = p2Unit.CurrentCell;
            float p2Phase1Hp = p2Unit.Health;
            TLog.Info($"[TEST] P2 after Phase1: Pos={p2Phase1Cell?.GridCoordinates}, HP={p2Phase1Hp}");

            // === Phase 2: freeze P2, wait for expiry, verify P2 acts again ===
            p2PerformedAction = false;
            var frozenConfig = CreateFrozenBuffConfig(duration: 2);
            var freezeBuff = new Buff(frozenConfig, p2Unit, 2);
            p2Unit.AddBuff(freezeBuff);
            Assert.IsFalse(p2Unit.CanAct, "P2 must be frozen after adding Freeze buff.");
            TLog.Info($"[TEST] Freeze applied. P2.CanAct={p2Unit.CanAct}");

            // Wait for freeze to expire and P2 to act again
            bool p2ActedAfterFreeze = false;
            bool p2ActionableTurnObserved = false;
            int freezeFrame = 0;
            int lastPlayer = -1;
            var turnLog = new List<string>();

            float phaseTwoDeadline = Time.realtimeSinceStartup + 8f;
            for (int i = 0; Time.realtimeSinceStartup < phaseTwoDeadline; i++)
            {
                yield return null;
                freezeFrame = i + 1;

                int currentPlayer = bc.TurnContext.CurrentPlayer?.PlayerNumber ?? -1;
                if (currentPlayer == 2 && p2Unit.CanAct)
                    p2ActionableTurnObserved = true;
                if (currentPlayer != lastPlayer)
                {
                    string entry = $"F{freezeFrame}:P{currentPlayer},CanAct={p2Unit.CanAct},Buffs={p2Unit.BuffComponent?.GetActiveBuffs().Count ?? 0}";
                    turnLog.Add(entry);
                    lastPlayer = currentPlayer;
                }

                if ((p2ActionableTurnObserved && currentPlayer != 2) ||
                    p2PerformedAction ||
                    HasUnitActed(p2Unit, p2Phase1Cell, bc))
                {
                    p2ActedAfterFreeze = true;
                    TLog.Info($"[TEST] Phase2: P2 acted after freeze at frame {freezeFrame}! Pos={p2Unit.CurrentCell?.GridCoordinates}, HP={p2Unit.Health}");
                    break;
                }

                if (!bc.IsBattleActive)
                    break;
            }

            TLog.Info($"[TEST] Phase2 result: P2ActedAfterFreeze={p2ActedAfterFreeze}, frames={freezeFrame}");
            TLog.Info($"[TEST] TurnLog({turnLog.Count}): {string.Join(" | ", turnLog.Take(15))}");
            TLog.Info($"[TEST] Final: P2.CanAct={p2Unit.CanAct}, P2.Buffs={p2Unit.BuffComponent?.GetActiveBuffs().Count}, P2.Pos={p2Unit.CurrentCell?.GridCoordinates}, P2.HP={p2Unit.Health}/{p2Unit.MaxHealth}");

            Assert.IsTrue(p2ActedAfterFreeze,
                $"P2 enemy should act after freeze expired (same as before freeze). " +
                $"Phase1 verified P2 acted. Phase2: waited {freezeFrame} frames, {turnLog.Count} turns. " +
                $"P2.CanAct={p2Unit.CanAct}, P2.Buffs={p2Unit.BuffComponent?.GetActiveBuffs().Count}, " +
                $"P2.Pos={p2Unit.CurrentCell?.GridCoordinates}, P2.HP={p2Unit.Health}. " +
                $"TurnLog: {string.Join(" | ", turnLog.Take(15))}");
        }

        /// <summary>
        /// P1=Human scenario: user casts freeze on P2 enemy, then ends turn without moving.
        /// Drives P1 turns by calling EndTurn (simulating player ending turn without moving).
        /// Verifies that P2 AI acts after freeze expires on its own turn.
        /// </summary>
        [UnityTest]
        public IEnumerator HumanCaster_EndsTurn_EnemyAIResumesAfterFreeze()
        {
            LogAssert.ignoreFailingMessages = true;

            var initTask = TestGameAssetHelper.EnsureInitialized();
            yield return new WaitUntil(() => initTask.IsCompleted);
            Assume.That(initTask.Result, Is.Not.Null, "GameAssetManager must initialize.");

            CreateBattleScaffolding(out var bc, out Unit p1Unit, out Unit p2Unit, humanPlayer1: true);
            Assert.IsNotNull(p1Unit, "P1 unit must be created.");
            Assert.IsNotNull(p2Unit, "P2 unit must be created.");

            bc.TurnResolver = new UnitSpeedTurnResolver();
            bc.InitializeAndStart(false);
            _ = bc.StartBattleAsync();
            Assert.IsTrue(bc.IsBattleActive, "Battle should be active.");

            // Wait for initial setup
            for (int i = 0; i < 5; i++) yield return null;

            // Verify P2 AI acts before freeze (Phase 0: baseline)
            var p2InitialCell = p2Unit.CurrentCell;
            bool p2ActedBeforeFreeze = false;
            bool p2PerformedAction = false;
            p2Unit.BasicAbilityUsed += _ => p2PerformedAction = true;
            p2Unit.AbilityUsed += _ => p2PerformedAction = true;
            float baselineDeadline = Time.realtimeSinceStartup + 4f;
            for (int i = 0; Time.realtimeSinceStartup < baselineDeadline; i++)
            {
                yield return null;
                // Drive P1's turn: if it's P1's turn, call EndTurn (simulating "no move, just end turn")
                if (bc.TurnContext.CurrentPlayer?.PlayerNumber == 1 && bc.GridState is GridStateAwaitInput)
                {
                    bc.EndTurn();
                }
                if (p2PerformedAction || HasUnitActed(p2Unit, p2InitialCell, bc))
                {
                    p2ActedBeforeFreeze = true;
                    TLog.Info($"[TEST] P2 acted BEFORE freeze at frame {i+1}. Pos={p2Unit.CurrentCell?.GridCoordinates}");
                    break;
                }
                if (!bc.IsBattleActive) break;
            }

            Assume.That(p2ActedBeforeFreeze, Is.True,
                $"P2 AI must act before freeze (baseline). If this fails, AI setup is wrong. " +
                $"P2.Pos={p2Unit.CurrentCell?.GridCoordinates}");

            var p2PreFreezeCell = p2Unit.CurrentCell;
            float p1PreFreezeHealth = p1Unit.Health;
            p2PerformedAction = false;
            TLog.Info($"[TEST] P2 baseline established at {p2PreFreezeCell?.GridCoordinates}");

            // Freeze P2
            var frozenConfig = CreateFrozenBuffConfig(duration: 2);
            p2Unit.AddBuff(new Buff(frozenConfig, p2Unit, 2));
            Assert.IsFalse(p2Unit.CanAct, "P2 must be frozen.");
            TLog.Info($"[TEST] Freeze applied. P2.CanAct={p2Unit.CanAct}");

            // Phase 1: drive turns, P1 ends turn each cycle, P2 frozen → skip
            // Wait for freeze to expire, then verify P2 acts again
            bool p2ActedAfterFreeze = false;
            bool p2ActionableTurnObserved = false;
            int frame = 0;
            var turnLog = new List<string>();
            int lastPlayer = -1;

            float resumeDeadline = Time.realtimeSinceStartup + 8f;
            for (int i = 0; Time.realtimeSinceStartup < resumeDeadline; i++)
            {
                yield return null;
                frame = i + 1;

                int currentPlayer = bc.TurnContext.CurrentPlayer?.PlayerNumber ?? -1;
                if (currentPlayer == 2 && p2Unit.CanAct)
                    p2ActionableTurnObserved = true;

                // Drive P1's turn: end without moving
                if (currentPlayer == 1 && bc.GridState is GridStateAwaitInput)
                {
                    bc.EndTurn();
                }

                if (currentPlayer != lastPlayer)
                {
                    string entry = $"F{frame}:P{currentPlayer},CanAct={p2Unit.CanAct},Buffs={p2Unit.BuffComponent?.GetActiveBuffs().Count ?? 0}";
                    turnLog.Add(entry);
                    lastPlayer = currentPlayer;
                }

                if ((p2ActionableTurnObserved && currentPlayer != 2) ||
                    p2PerformedAction ||
                    HasUnitActed(p2Unit, p2PreFreezeCell, bc) ||
                    p1Unit.Health < p1PreFreezeHealth)
                {
                    p2ActedAfterFreeze = true;
                    TLog.Info($"[TEST] P2 acted AFTER freeze at frame {frame}! Pos={p2Unit.CurrentCell?.GridCoordinates}");
                    break;
                }
                if (!bc.IsBattleActive) break;
            }

            TLog.Info($"[TEST] TurnLog({turnLog.Count}): {string.Join(" | ", turnLog.Take(20))}");
            TLog.Info($"[TEST] Final: P2.CanAct={p2Unit.CanAct}, P2.Buffs={p2Unit.BuffComponent?.GetActiveBuffs().Count}, P2.Pos={p2Unit.CurrentCell?.GridCoordinates}");

            Assert.IsTrue(p2ActedAfterFreeze,
                $"P2 enemy should act after freeze expires, even when P1 just ends turn without moving. " +
                $"Waited {frame} frames, {turnLog.Count} turns. " +
                $"P2.CanAct={p2Unit.CanAct}, P2.Buffs={p2Unit.BuffComponent?.GetActiveBuffs().Count}, " +
                $"P2.Pos={p2Unit.CurrentCell?.GridCoordinates}. " +
                $"TurnLog: {string.Join(" | ", turnLog.Take(20))}");
        }

        [UnityTest]
        public IEnumerator FrozenAIUnit_WaitsBeforeSkip()
        {
            LogAssert.ignoreFailingMessages = true;

            var initTask = TestGameAssetHelper.EnsureInitialized();
            yield return new WaitUntil(() => initTask.IsCompleted);
            Assume.That(initTask.Result, Is.Not.Null, "GameAssetManager must initialize.");

            CreateBattleScaffolding(out var bc, out Unit p1Unit, out Unit p2Unit);
            bc.TurnResolver = new UnitSpeedTurnResolver();

            var frozenConfig = CreateFrozenBuffConfig(duration: 1);
            p1Unit.AddBuff(new Buff(frozenConfig, p1Unit, 1));
            Assert.IsFalse(p1Unit.CanAct, "P1 AI unit must be frozen before battle start.");

            bc.InitializeAndStart(false);
            _ = bc.StartBattleAsync();
            Assert.IsTrue(bc.IsBattleActive, "Battle should be active.");

            yield return new WaitForSecondsRealtime(0.1f);

            Assert.IsNotNull(bc.TurnContext.CurrentPlayer);
            Assert.AreEqual(1, bc.TurnContext.CurrentPlayer.PlayerNumber,
                "Frozen AI unit should still hold the turn during the 1-second visibility delay.");

            float skipDeadline = Time.realtimeSinceStartup + TurnSkipHelper.FrozenSkipDelaySeconds + 2f;
            while (bc.TurnContext.CurrentPlayer?.PlayerNumber == 1 && Time.realtimeSinceStartup < skipDeadline)
                yield return null;

            Assert.IsNotNull(bc.TurnContext.CurrentPlayer);
            Assert.AreNotEqual(1, bc.TurnContext.CurrentPlayer.PlayerNumber,
                "Frozen AI unit should be skipped after the 1-second delay.");
        }

        #region Helpers

        private void CreateBattleScaffolding(out BattleController bc, out Unit p1Unit, out Unit p2Unit, bool humanPlayer1 = false)
        {
            _battleRoot = new GameObject("TestBattleController_FreezeTest");
            _battleRoot.SetActive(false);
            bc = _battleRoot.AddComponent<BattleController>();

            var controllerType = typeof(BattleController);
            var startFlag = controllerType.GetField("_startImmediatelly", BindingFlags.Instance | BindingFlags.NonPublic);
            startFlag?.SetValue(bc, false);

            // 4x4 grid
            _cellManagerRoot = new GameObject("TestCellManager_FreezeTest");
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

            _battleRoot.SetActive(true);

            var gridControllerField = controllerType.GetField("_controller", BindingFlags.Instance | BindingFlags.NonPublic);
            var gridController = gridControllerField?.GetValue(bc);
            if (gridController != null)
            {
                var beforeInitProp = gridController.GetType().GetProperty("BeforeUnitManagerInitialize");
                beforeInitProp?.SetValue(gridController, null);
            }

            // P1=Human, P2=AI or both AI
            bc.SetPlayers(humanPlayer1 ? 1 : 0, humanPlayer1 ? 1 : 2);
            foreach (var aiPlayer in ((IPlayerManager)bc).GetPlayers().OfType<AIPlayer>())
            {
                aiPlayer.TurnStartDelay = 0;
                aiPlayer.UnitDelay = 0;
            }

            // Create unit container
            var unitField = controllerType.GetField("_unitContainer", BindingFlags.Instance | BindingFlags.NonPublic);
            Transform unitContainer = (Transform)unitField?.GetValue(bc);
            if (unitContainer == null)
            {
                var containerGo = new GameObject("UnitContainer");
                containerGo.transform.SetParent(_battleRoot.transform);
                unitField?.SetValue(bc, containerGo.transform);
                unitContainer = containerGo.transform;
            }

            var attackBrain = AiBrainTestHelper.CreateAttackBrain();

            p1Unit = TestUnitFactory.CreateBarbarian(unitContainer, "P1_Fighter", 1, FindCell(_cellManagerRoot, 0, 0), humanPlayer1 ? null : attackBrain);
            p1Unit.Constitution = 250;
            p1Unit.Initiative = 10f;
            p1Unit.MovementAnimationSpeed = 1000f;

            p2Unit = TestUnitFactory.CreateBarbarian(unitContainer, "P2_Fighter", 2, FindCell(_cellManagerRoot, 1, 0), attackBrain);
            p2Unit.Constitution = 250;
            p2Unit.Initiative = 10f;
            p2Unit.MovementAnimationSpeed = 1000f;
        }

        private static bool HasUnitActed(Unit unit, ICell referenceCell, BattleController bc)
        {
            if (unit == null || referenceCell == null) return false;

            var currentCell = unit.CurrentCell;
            if (currentCell != null &&
                (currentCell.GridCoordinates.x != referenceCell.GridCoordinates.x ||
                 currentCell.GridCoordinates.y != referenceCell.GridCoordinates.y))
            {
                return true;
            }

            if (Math.Abs(unit.Health - unit.MaxHealth) > 0.01f)
            {
                return true;
            }

            return false;
        }

        private static BuffConfig CreateFrozenBuffConfig(int duration)
        {
            var config = ScriptableObject.CreateInstance<BuffConfig>();
            var configType = typeof(BuffConfig);

            var nameField = configType.GetField("_buffName", BindingFlags.NonPublic | BindingFlags.Instance);
            nameField?.SetValue(config, "TestFrozen");

            var canActField = configType.GetField("_canAct", BindingFlags.NonPublic | BindingFlags.Instance);
            canActField?.SetValue(config, false);

            var durationField = configType.GetField("_defaultDuration", BindingFlags.NonPublic | BindingFlags.Instance);
            durationField?.SetValue(config, duration);

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

        #endregion
    }
}
