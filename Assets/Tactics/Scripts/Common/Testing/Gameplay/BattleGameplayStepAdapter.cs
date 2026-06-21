using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Tactics.Common.AI.MonsterAI;
using Tactics.Common.Battle;
using Tactics.Common.Cells;
using Tactics.Common.Controllers.GameResolvers;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Common.Units.Buffs;
using Tactics.AssetPipeline;
using Tactics.Common.Controllers;
using Tactics.Common.Controllers.TurnResolvers;
using Tactics.Controllers.TurnResolvers;
using UnityEngine;

namespace Tactics.Common.Testing.Gameplay
{
    public sealed class BattleGameplayStepAdapter : IGameplayStepAdapter
    {
        private const string BattleAdapterName = "Battle";
        private static readonly Dictionary<int, Type> _appliedResolverTypes = new();

        public string AdapterName => BattleAdapterName;

        public bool CanExecute(ExecutableScenarioAction action)
        {
            return action.Kind is "bindBattleController"
                or "advanceTurn"
                or "endBattleWithResult"
                or "executeBattleSkillGraph"
                or "moveUnit"
                or "setUnitState"
                or "addBuff"
                or "executeAI"
                or "createAiBrain"
                or "useRealAssets"
                or "spawnCorpse"
                or "killUnit";
        }

        public async Task<GameplayStepResult> ExecuteAsync(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            try
            {
                switch (action.Kind)
                {
                    case "bindBattleController":
                        return BindBattleController(context, action);
                    case "advanceTurn":
                        return AdvanceTurn(context, action);
                    case "endBattleWithResult":
                        return EndBattleWithResult(context, action);
                    case "executeBattleSkillGraph":
                        return await ExecuteBattleSkillGraph(context, action);
                    case "moveUnit":
                        return MoveUnit(context, action);
                    case "setUnitState":
                        return SetUnitState(context, action);
                    case "addBuff":
                        return AddBuff(context, action);
                    case "executeAI":
                        return await ExecuteAI(context, action);
                    case "createAiBrain":
                        return CreateAiBrain(context, action);
                    case "useRealAssets":
                        return await UseRealAssets(context, action);
                    case "spawnCorpse":
                        return SpawnCorpse(context, action);
                    case "killUnit":
                        return KillUnit(context, action);
                    default:
                        return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Unsupported Battle action '{action.Kind}'.");
                }
            }
            catch (Exception ex)
            {
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, ex.Message);
            }
        }

        public bool CanAssert(ExecutableScenarioAssertion assertion)
        {
            return assertion.Kind is "battleIsActive"
                or "currentRoundEquals"
                or "unitAliveEquals"
                or "unitHealthEquals"
                or "battleResultEquals"
                or "unitPositionEquals"
                or "unitManaEquals"
                or "unitHasBuff"
                or "unitDoesNotHaveBuff"
                or "unitBuffDurationEquals"
                or "playerNumberEquals"
                or "unitMaxHealthEquals"
                or "unitCountEquals"
                or "unitCanAct"
                or "aiSelectedIntentTypeEquals"
                or "aiCandidateCountEquals"
                or "aiRuleFilteredCountEquals"
                or "aiUsedAbilityEquals"
                or "aiWasNoOpEquals"
                or "unitPositionChangedSinceStep"
                or "targetHealthChangedSinceStep"
                or "decisionLogContains"
                or "cellIsBlocked"
                or "unitIsCorpse"
                or "unitOwnerEquals";
        }

        public Task<GameplayAssertionResult> AssertAsync(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            try
            {
                GameplayAssertionResult result = assertion.Kind switch
                {
                    "battleIsActive" => AssertBattleIsActive(context, assertion),
                    "currentRoundEquals" => AssertCurrentRoundEquals(context, assertion),
                    "unitAliveEquals" => AssertUnitAliveEquals(context, assertion),
                    "unitHealthEquals" => AssertUnitHealthEquals(context, assertion),
                    "battleResultEquals" => AssertBattleResultEquals(context, assertion),
                    "unitPositionEquals" => AssertUnitPositionEquals(context, assertion),
                    "unitManaEquals" => AssertUnitManaEquals(context, assertion),
                    "unitHasBuff" => AssertUnitHasBuff(context, assertion),
                    "unitDoesNotHaveBuff" => AssertUnitDoesNotHaveBuff(context, assertion),
                    "unitBuffDurationEquals" => AssertUnitBuffDurationEquals(context, assertion),
                    "playerNumberEquals" => AssertPlayerNumberEquals(context, assertion),
                    "unitMaxHealthEquals" => AssertUnitMaxHealthEquals(context, assertion),
                    "unitCountEquals" => AssertUnitCountEquals(context, assertion),
                    "unitCanAct" => AssertUnitCanAct(context, assertion),
                    "aiSelectedIntentTypeEquals" => AssertAiSelectedIntentTypeEquals(context, assertion),
                    "aiCandidateCountEquals" => AssertAiCandidateCountEquals(context, assertion),
                    "aiRuleFilteredCountEquals" => AssertAiRuleFilteredCountEquals(context, assertion),
                    "aiUsedAbilityEquals" => AssertAiUsedAbilityEquals(context, assertion),
                    "aiWasNoOpEquals" => AssertAiWasNoOpEquals(context, assertion),
                    "unitPositionChangedSinceStep" => AssertUnitPositionChangedSinceStep(context, assertion),
                    "targetHealthChangedSinceStep" => AssertTargetHealthChangedSinceStep(context, assertion),
                    "decisionLogContains" => AssertDecisionLogContains(context, assertion),
                    "cellIsBlocked" => AssertCellIsBlocked(context, assertion),
                    "unitIsCorpse" => AssertUnitIsCorpse(context, assertion),
                    "unitOwnerEquals" => AssertUnitOwnerEquals(context, assertion),
                    _ => GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Unsupported Battle assertion '{assertion.Kind}'.")
                };

                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                return Task.FromResult(GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, ex.Message));
            }
        }

        public ProbeSnapshot CaptureProbe(GameplayRuntimeContext context, GameplayProbeRequest request)
        {
            var data = new JObject();
            var controller = context.BattleController;
            if (controller != null)
            {
                data["isBattleActive"] = controller.IsBattleActive;
                data["currentRound"] = controller.CurrentRound;
            }

            if (context.LastBattleResult.HasValue)
            {
                var result = context.LastBattleResult.Value;
                data["hasResult"] = true;
                data["winnerCount"] = result.Winners?.Count() ?? 0;
                data["loserCount"] = result.Losers?.Count() ?? 0;
            }

            if (!string.IsNullOrWhiteSpace(request.Target) && context.Units.TryGetValue(request.Target, out var unit))
            {
                data["unit"] = request.Target;
                data["health"] = unit.Health;
                data["maxHealth"] = unit.MaxHealth;
                data["playerNumber"] = unit.PlayerNumber;
                data["isDowned"] = unit.IsDowned;
                data["mana"] = unit.Mana;
                data["maxMana"] = unit.MaxMana;
                data["canAct"] = unit.CanAct;
                data["activeBuffs"] = new JArray(unit.GetActiveBuffs().Select(b => b.BuffName));
            }

            if (context.LastAiDecisionLog != null)
            {
                var entries = context.LastAiDecisionLog.GetEntries();
                var finalSelection = entries.LastOrDefault(e => e.Type == AiDecisionLog.LogType.FinalSelection);
                if (finalSelection != null)
                {
                    data["aiSelectedIntent"] = finalSelection.Message;
                }
                data["aiRuleFilteredCount"] = entries.Count(e => e.Type == AiDecisionLog.LogType.RuleFiltered);
            }

            // AI execution snapshot fields
            var snap = context.LastAiSnapshot;
            if (snap != null)
            {
                data["aiSelectedIntentType"] = snap.SelectedIntentType ?? "";
                data["aiSelectedActionType"] = snap.SelectedActionType ?? "";
                data["aiSelectedAbilityName"] = snap.SelectedAbilityName ?? "";
                data["aiSelectedScore"] = snap.SelectedScore;
                data["aiActorPosition"] = $"{snap.ActorPositionAfter.x},{snap.ActorPositionAfter.y}";
                data["aiTargetPosition"] = snap.TargetAlias != null ? $"{snap.TargetPositionAfter.x},{snap.TargetPositionAfter.y}" : "";
                data["aiDidMove"] = snap.DidMove;
                data["aiDidDamageTarget"] = snap.DidDamageTarget;
                data["aiDidHealTarget"] = snap.DidHealTarget;
                data["aiWasNoOp"] = snap.WasNoOp;
                data["aiFailureReason"] = snap.FailureReason ?? "";
            }

            return new ProbeSnapshot
            {
                Adapter = BattleAdapterName,
                Kind = request.Kind,
                Target = request.Target,
                Data = data
            };
        }

        private static GameplayStepResult BindBattleController(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            var controller = BattleController.Instance;
            if (controller == null)
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "BattleController.Instance is not available.");

            // Resolve requested turn resolver type (default: UnitSpeedTurnResolver to match Test1.unity)
            var resolverParam = action.Parameters?["turnResolver"]?.ToString()?.ToLowerInvariant();
            var requestedResolver = resolverParam == "subsequent"
                ? (ITurnResolver)new SubsequentTurnResolver()
                : new UnitSpeedTurnResolver();
            var requestedType = requestedResolver.GetType();

            // Four-state initialization check:
            // 1. Uninitialized: GridState == null
            // 2. Initialized-not-started: GridState != null, TurnContext.CurrentPlayer == null
            // 3. Ready: TurnContext.CurrentPlayer != null
            // 4. Half-initialized anomaly: GridState != null but StartGame partially failed
            if (controller.GridState == null)
            {
                // State 1: Uninitialized — set resolver and do full InitializeAndStart
                controller.TurnResolver = requestedResolver;
                if (!EnsureBattleInitialized(controller))
                    return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "BattleController initialization failed during bind.");
                if (!IsBattleControllerReady(controller))
                    return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "BattleController is in a half-initialized anomaly state after InitializeAndStart (GridState set but CurrentPlayer missing).");
            }
            else if (controller.TurnContext.CurrentPlayer == null)
            {
                // State 2: Initialized but not started (InitializeGame ran, StartGame did not).
                // Set resolver and call StartGame() to complete startup.
                controller.TurnResolver = requestedResolver;
                controller.StartGame();
                // StartGame() does not throw on failure — it early-returns. We must verify
                // the controller actually entered a ready state before treating bind as success.
                if (!IsBattleControllerReady(controller))
                    return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "BattleController is in a half-initialized anomaly state after StartGame (GridState set but CurrentPlayer missing).");
                _appliedResolverTypes[controller.GetInstanceID()] = requestedType;
                if (!controller.IsBattleActive)
                    _ = controller.StartBattleAsync();
            }
            else
            {
                // State 3: Already ready — resolver semantic kind must match
                var currentKind = NormalizeResolverKind(controller.TurnResolver);
                var requestedKind = NormalizeResolverKind(requestedResolver);
                if (currentKind != requestedKind)
                    return GameplayStepResult.Fail(BattleAdapterName, action.Kind,
                        $"Cannot change turn resolver on an already-started BattleController. Current={currentKind}, Requested={requestedKind}. Set the resolver in test SetUp before InitializeAndStart().");
                if (!IsBattleControllerReady(controller))
                    return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "BattleController is in a half-initialized anomaly state (CurrentPlayer was non-null at branch entry but ready check failed).");
                _appliedResolverTypes[controller.GetInstanceID()] = requestedType;
                if (!controller.IsBattleActive)
                    _ = controller.StartBattleAsync();
            }

            var units = controller.GetUnits().ToList();
            var unitAliases = new Dictionary<string, IUnit>(StringComparer.OrdinalIgnoreCase);
            var playerCounters = new Dictionary<int, int>();
            foreach (var unit in units)
            {
                int playerNum = unit.PlayerNumber;
                if (!playerCounters.ContainsKey(playerNum))
                    playerCounters[playerNum] = 0;
                int index = playerCounters[playerNum]++;
                string alias = $"p{playerNum}_{index}";
                unitAliases[alias] = unit;
            }

            var cellAliases = new Dictionary<string, ICell>(StringComparer.OrdinalIgnoreCase);
            if (controller.CellManager != null)
            {
                foreach (var cell in controller.CellManager.GetCells())
                {
                    var coords = cell.GridCoordinates;
                    string alias = $"cell_{coords.x}_{coords.y}";
                    cellAliases[alias] = cell;
                }
            }

            // Commit only after all validation succeeded. If bind fails earlier, keep the
            // existing context intact: old subscription, old aliases, and old BattleController.
            bool isRebind = context.SubscribedBattleController != null;
            CleanupBattleEndedSubscription(context);
            if (isRebind)
            {
                RemoveBattleAliases(context.Units, @"^p\d+_\d+$");
                RemoveBattleAliases(context.Cells, @"^cell_");
            }

            context.BattleController = controller;
            context.LastBattleResult = null;

            // Subscribe BattleEnded with a named handler so it can be unsubscribed
            Action<GameResult> onBattleEnded = result => { context.LastBattleResult = result; };
            controller.BattleEnded += onBattleEnded;
            context.SubscribedBattleController = controller;
            context.BattleEndedHandler = onBattleEnded;

            foreach (var pair in unitAliases)
            {
                context.Units[pair.Key] = pair.Value;
            }

            foreach (var pair in cellAliases)
            {
                context.Cells[pair.Key] = pair.Value;
            }

            return GameplayStepResult.Pass(BattleAdapterName, action.Kind, $"Bound {units.Count} units ({string.Join(", ", context.Units.Keys)}), {context.Cells.Count} cells.");
        }

        private static GameplayStepResult AdvanceTurn(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            var controller = RequireBattleController(context, action.Kind);
            if (!EnsureBattleInitialized(controller))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "BattleController initialization failed. Ensure the test scene provides CellManager and players.");
            // 禁用 AI 自动 Play，防止 AI 自动推进回合
            var previousDisableAiAutoPlay = controller.DisableAiAutoPlay;
            controller.DisableAiAutoPlay = true;
            try
            {
                controller.EndTurn();
            }
            finally
            {
                controller.DisableAiAutoPlay = previousDisableAiAutoPlay;
            }
            return GameplayStepResult.Pass(BattleAdapterName, action.Kind, $"Advanced turn. CurrentRound={controller.CurrentRound}");
        }

        private static GameplayStepResult EndBattleWithResult(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            var controller = RequireBattleController(context, action.Kind);
            var result = new GameResult();
            controller.EndBattle(result);
            context.LastBattleResult = result;
            return GameplayStepResult.Pass(BattleAdapterName, action.Kind, "Battle ended.");
        }

        /// <summary>
        /// 通过 SkillGraph 执行技能（新系统入口）。
        /// 从 context.SkillGraphs 中查找图，通过 SkillGraphRunner 执行。
        /// </summary>
        private static async Task<GameplayStepResult> ExecuteBattleSkillGraph(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            var controller = RequireBattleController(context, action.Kind);
            if (!EnsureBattleInitialized(controller))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "BattleController not initialized.");

            string graphAlias = action.Parameters["graphAlias"]?.ToString();
            if (string.IsNullOrWhiteSpace(graphAlias))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "executeBattleSkillGraph requires graphAlias.");

            if (!context.SkillGraphs.TryGetValue(graphAlias, out var graphAsset))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"SkillGraph alias '{graphAlias}' not found.");

            string casterAlias = action.Parameters["casterAlias"]?.ToString();
            if (string.IsNullOrWhiteSpace(casterAlias))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "executeBattleSkillGraph requires casterAlias.");
            if (!context.Units.TryGetValue(casterAlias, out var caster))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Caster alias '{casterAlias}' not found.");

            string targetAlias = action.Parameters["targetAlias"]?.ToString();
            IUnit primaryTarget = null;
            if (!string.IsNullOrWhiteSpace(targetAlias))
            {
                if (!context.Units.TryGetValue(targetAlias, out primaryTarget))
                    return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Target alias '{targetAlias}' not found.");
            }

            string targetPointAlias = action.Parameters["targetPointAlias"]?.ToString();
            ICell targetPoint = null;
            if (!string.IsNullOrWhiteSpace(targetPointAlias))
            {
                if (!context.Cells.TryGetValue(targetPointAlias, out targetPoint))
                    return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Target point alias '{targetPointAlias}' not found.");
            }

            var runtimeDef = SkillGraphRuntimeDefinition.FromAsset(graphAsset);
            var skillContext = new SkillExecutionContext(caster, graphAsset, runtimeDef, controller)
            {
                PrimaryTarget = primaryTarget,
                TargetPoint = targetPoint,
                RuntimeScope = context.RuntimeScope
            };

            var runner = new SkillGraphRunner();
            var result = await runner.Execute(skillContext);

            if (result == SkillGraphExecutionState.Completed)
                return GameplayStepResult.Pass(BattleAdapterName, action.Kind, $"Executed SkillGraph '{graphAlias}' on {casterAlias}.");

            return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"SkillGraph '{graphAlias}' execution failed: {skillContext.LastError}");
        }

        private static GameplayStepResult MoveUnit(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            string unitAlias = action.Parameters["unitAlias"]?.ToString();
            if (string.IsNullOrWhiteSpace(unitAlias))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "moveUnit requires unitAlias.");
            if (!context.Units.TryGetValue(unitAlias, out var unit))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Unit alias '{unitAlias}' not found.");

            ICell destCell = null;
            var cellAlias = action.Parameters["cellAlias"]?.ToString();
            if (!string.IsNullOrWhiteSpace(cellAlias))
            {
                if (!context.Cells.TryGetValue(cellAlias, out destCell))
                    return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Cell alias '{cellAlias}' not found.");
            }
            else
            {
                int x = action.Parameters["x"]?.ToObject<int>() ?? 0;
                int y = action.Parameters["y"]?.ToObject<int>() ?? 0;
                var key = $"cell_{x}_{y}";
                if (!context.Cells.TryGetValue(key, out destCell))
                    return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Cell at ({x},{y}) not found.");
            }

            var mono = unit as MonoBehaviour;
            if (mono != null)
                mono.transform.position = new Vector3(destCell.GridCoordinates.x, destCell.GridCoordinates.y, 0);

            // Remove from old cell
            if (unit.CurrentCell != null)
            {
                unit.CurrentCell.CurrentUnits.Remove(unit);
                unit.CurrentCell.IsTaken = unit.CurrentCell.CurrentUnits.Count > 0;
            }

            // Check if destination is blocked (e.g., by corpse)
            if (destCell.IsTaken)
            {
                // Restore old cell
                if (unit.CurrentCell != null)
                {
                    unit.CurrentCell.CurrentUnits.Add(unit);
                    unit.CurrentCell.IsTaken = true;
                }
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Cell ({destCell.GridCoordinates.x},{destCell.GridCoordinates.y}) is blocked.");
            }

            unit.CurrentCell = destCell;
            if (!destCell.CurrentUnits.Contains(unit))
                destCell.CurrentUnits.Add(unit);
            destCell.IsTaken = true;

            return GameplayStepResult.Pass(BattleAdapterName, action.Kind, $"Moved {unitAlias} to ({destCell.GridCoordinates.x},{destCell.GridCoordinates.y}).");
        }

        private static GameplayStepResult SetUnitState(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            string unitAlias = action.Parameters["unitAlias"]?.ToString();
            if (string.IsNullOrWhiteSpace(unitAlias))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "setUnitState requires unitAlias.");
            if (!context.Units.TryGetValue(unitAlias, out var unit))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Unit alias '{unitAlias}' not found.");

            var mono = unit as Unit;

            var health = action.Parameters["health"];
            if (health != null) unit.Health = health.ToObject<float>();

            var maxHealth = action.Parameters["maxHealth"];
            if (maxHealth != null && mono != null) mono.MaxHealth = maxHealth.ToObject<float>();

            var mana = action.Parameters["mana"];
            if (mana != null) unit.Mana = mana.ToObject<float>();

            var playerNumber = action.Parameters["playerNumber"];
            if (playerNumber != null) unit.PlayerNumber = playerNumber.ToObject<int>();

            return GameplayStepResult.Pass(BattleAdapterName, action.Kind, $"Set state for {unitAlias}.");
        }

        private static GameplayStepResult AddBuff(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            string unitAlias = action.Parameters["unitAlias"]?.ToString();
            if (string.IsNullOrWhiteSpace(unitAlias))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "addBuff requires unitAlias.");
            if (!context.Units.TryGetValue(unitAlias, out var unit))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Unit alias '{unitAlias}' not found.");

            string buffName = action.Parameters["buffName"]?.ToString();
            if (string.IsNullOrWhiteSpace(buffName))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "addBuff requires buffName.");

            int duration = action.Parameters["duration"]?.ToObject<int>() ?? 3;

            string configPath = action.Parameters["configPath"]?.ToString();
            BuffConfig config;

            if (!string.IsNullOrWhiteSpace(configPath))
            {
                config = GameAssetManager.Instance?.Load<BuffConfig>(configPath);
                if (config == null)
                    return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Failed to load BuffConfig from '{configPath}'.");
            }
            else
            {
                config = ScriptableObject.CreateInstance<BuffConfig>();
                var nameField = typeof(BuffConfig).GetField("_buffName", BindingFlags.NonPublic | BindingFlags.Instance);
                nameField?.SetValue(config, buffName);
                var durationField = typeof(BuffConfig).GetField("_defaultDuration", BindingFlags.NonPublic | BindingFlags.Instance);
                durationField?.SetValue(config, duration);
            }

            var buff = new Buff(config, unit, duration);
            unit.AddBuff(buff);

            return GameplayStepResult.Pass(BattleAdapterName, action.Kind, $"Added buff '{buffName}' to {unitAlias} for {duration} turns.");
        }

        private static async Task<GameplayStepResult> ExecuteAI(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            var controller = RequireBattleController(context, action.Kind);
            if (!EnsureBattleInitialized(controller))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "BattleController not initialized.");

            string unitAlias = action.Parameters["unitAlias"]?.ToString();
            if (string.IsNullOrWhiteSpace(unitAlias))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "executeAI requires unitAlias.");
            if (!context.Units.TryGetValue(unitAlias, out var unit))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Unit alias '{unitAlias}' not found.");

            string brainAlias = action.Parameters["brainAssetAlias"]?.ToString() ?? "default_brain";
            if (!context.AiBrainAssets.TryGetValue(brainAlias, out var brainAsset))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Brain asset alias '{brainAlias}' not found. Use createAiBrain first.");

            // Record pre-execution snapshot
            var snapshot = new AiExecutionSnapshot
            {
                ActorAlias = unitAlias,
                ActorUnitId = unit.UnitID,
                ActorPositionBefore = unit.CurrentCell?.GridCoordinates ?? default,
                ActorHealthBefore = unit.Health,
                ActorManaBefore = unit.Mana,
            };

            // Resolve target alias from context.Units (first enemy unit as default target)
            string targetAlias = action.Parameters["targetAlias"]?.ToString();
            if (!string.IsNullOrWhiteSpace(targetAlias) && context.Units.TryGetValue(targetAlias, out var targetUnit))
            {
                snapshot.TargetAlias = targetAlias;
                snapshot.TargetUnitId = targetUnit.UnitID;
                snapshot.TargetPositionBefore = targetUnit.CurrentCell?.GridCoordinates ?? default;
                snapshot.TargetHealthBefore = targetUnit.Health;
            }

            try
            {
                // 绕过活跃单位检查，允许 AI 直接执行命令
                controller.BypassActiveUnitCheck = true;
                var decisionLog = await AI.MonsterAI.AiBrainRunner.ExecuteWithLog(unit, controller, brainAsset);
                controller.BypassActiveUnitCheck = false;
                context.LastAiDecisionLog = decisionLog;

                // Record post-execution snapshot
                snapshot.ActorPositionAfter = unit.CurrentCell?.GridCoordinates ?? default;
                snapshot.ActorHealthAfter = unit.Health;
                snapshot.ActorManaAfter = unit.Mana;

                if (snapshot.TargetAlias != null && context.Units.TryGetValue(snapshot.TargetAlias, out var targetAfter))
                {
                    snapshot.TargetPositionAfter = targetAfter.CurrentCell?.GridCoordinates ?? default;
                    snapshot.TargetHealthAfter = targetAfter.Health;
                }

                // Extract selected intent from decision log
                if (decisionLog != null)
                {
                    var entries = decisionLog.GetEntries();
                    var finalSelection = entries.LastOrDefault(e => e.Type == AiDecisionLog.LogType.FinalSelection);
                    if (finalSelection != null)
                    {
                        // Parse "Selected: Engage (Score: 18.51)" format
                        var msg = finalSelection.Message;
                        var colonIdx = msg.IndexOf(':');
                        var parenIdx = msg.IndexOf('(');
                        if (colonIdx >= 0 && parenIdx > colonIdx)
                            snapshot.SelectedIntentType = msg.Substring(colonIdx + 1, parenIdx - colonIdx - 1).Trim();
                        if (float.TryParse(System.Text.RegularExpressions.Regex.Match(msg, @"Score:\s*([\d.]+)").Groups[1].Value, out var score))
                            snapshot.SelectedScore = score;
                    }

                    // Extract ability name and action type from ExecutionResult log
                    var execResult = entries.LastOrDefault(e => e.Type == AiDecisionLog.LogType.ExecutionResult);
                    if (execResult != null)
                    {
                        // Parse "Executed: Ability='Melee Attack', ActionType=Attack, Target=Unit_0" format
                        var abilityMatch = System.Text.RegularExpressions.Regex.Match(execResult.Message, @"Ability='([^']*)'");
                        if (abilityMatch.Success)
                            snapshot.SelectedAbilityName = abilityMatch.Groups[1].Value;

                        var actionMatch = System.Text.RegularExpressions.Regex.Match(execResult.Message, @"ActionType=(\w+)");
                        if (actionMatch.Success)
                            snapshot.SelectedActionType = actionMatch.Groups[1].Value;
                    }
                }

                // Compute effect flags
                snapshot.DidMove = snapshot.ActorPositionBefore.x != snapshot.ActorPositionAfter.x
                                || snapshot.ActorPositionBefore.y != snapshot.ActorPositionAfter.y;
                snapshot.DidDamageTarget = snapshot.TargetAlias != null
                                        && snapshot.TargetHealthAfter < snapshot.TargetHealthBefore;
                snapshot.DidHealTarget = snapshot.TargetAlias != null
                                      && snapshot.TargetHealthAfter > snapshot.TargetHealthBefore;

                // Determine if this was a no-op
                if (snapshot.SelectedIntentType == null)
                {
                    snapshot.WasNoOp = true;
                    snapshot.FailureReason = "Failed to parse selected intent type from decision log.";
                }
                else
                {
                    bool intentIsHoldPosition = string.Equals(snapshot.SelectedIntentType, "HoldPosition", System.StringComparison.OrdinalIgnoreCase);
                    snapshot.WasNoOp = !intentIsHoldPosition && !snapshot.DidMove && !snapshot.DidDamageTarget && !snapshot.DidHealTarget;

                    if (snapshot.WasNoOp)
                        snapshot.FailureReason = "AI selected a non-HoldPosition intent but produced no observable effect (no move, no damage, no heal).";
                }

                // Store snapshots
                context.PreviousAiSnapshot = context.LastAiSnapshot;
                context.LastAiSnapshot = snapshot;

                return GameplayStepResult.Pass(BattleAdapterName, action.Kind, $"Executed AI for {unitAlias} using brain '{brainAlias}'. Intent={snapshot.SelectedIntentType}, DidMove={snapshot.DidMove}, DidDamage={snapshot.DidDamageTarget}, WasNoOp={snapshot.WasNoOp}");
            }
            catch (Exception ex)
            {
                controller.BypassActiveUnitCheck = false;
                snapshot.FailureReason = $"AI execution exception: {ex.Message}";
                context.PreviousAiSnapshot = context.LastAiSnapshot;
                context.LastAiSnapshot = snapshot;
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"AI execution failed: {ex.Message}");
            }
        }

        private static GameplayStepResult CreateAiBrain(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            string brainAlias = action.Parameters["brainAssetAlias"]?.ToString();
            if (string.IsNullOrWhiteSpace(brainAlias))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "createAiBrain requires brainAssetAlias.");

            string brainType = action.Parameters["brainType"]?.ToString()?.ToLower() ?? "attack";
            AiBrainAsset brainAsset;
            switch (brainType)
            {
                case "attack":
                    brainAsset = AiBrainTestHelper.CreateAttackBrain();
                    break;
                case "heal":
                    brainAsset = AiBrainTestHelper.CreateHealBrain();
                    break;
                default:
                    return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Unsupported brainType '{brainType}'.");
            }

            context.AiBrainAssets[brainAlias] = brainAsset;
            return GameplayStepResult.Pass(BattleAdapterName, action.Kind, $"Created AI brain '{brainAlias}' of type '{brainType}'.");
        }

        private static async Task<GameplayStepResult> UseRealAssets(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            try
            {
                var mgr = await TestGameAssetHelper.EnsureInitialized();
                if (mgr == null)
                    return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "Failed to initialize GameAssetManager.", "Asset");

                context.UseRealAssets = true;
                return GameplayStepResult.Pass(BattleAdapterName, action.Kind, "Real asset mode enabled.");
            }
            catch (Exception ex)
            {
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Failed to enable real asset mode: {ex.Message}", "Asset");
            }
        }

        private static GameplayAssertionResult AssertBattleIsActive(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            var controller = RequireBattleController(context, assertion.Kind);
            bool expected = assertion.Expected?.ToObject<bool>() ?? true;
            bool actual = controller.IsBattleActive;
            return actual == expected
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"IsBattleActive={actual}")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected IsBattleActive={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertCurrentRoundEquals(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            var controller = RequireBattleController(context, assertion.Kind);
            int expected = assertion.Expected?.ToObject<int>() ?? 0;
            int actual = controller.CurrentRound;
            return actual == expected
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"CurrentRound={actual}")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected CurrentRound={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertUnitAliveEquals(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (string.IsNullOrWhiteSpace(assertion.Target))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "unitAliveEquals requires a target unit alias.");
            if (!context.Units.TryGetValue(assertion.Target, out var unit))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Unit alias '{assertion.Target}' does not exist.");

            bool expected = assertion.Expected?.ToObject<bool>() ?? true;
            bool actual = !unit.IsDowned;
            return actual == expected
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"{assertion.Target}.IsAlive={actual}")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected {assertion.Target}.IsAlive={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertUnitHealthEquals(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (string.IsNullOrWhiteSpace(assertion.Target))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "unitHealthEquals requires a target unit alias.");
            if (!context.Units.TryGetValue(assertion.Target, out var unit))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Unit alias '{assertion.Target}' does not exist.");

            float expected = assertion.Expected?.ToObject<float>() ?? 0f;
            return Math.Abs(unit.Health - expected) < 0.001f
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"{assertion.Target}.Health={unit.Health}")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected {assertion.Target}.Health={expected}, actual={unit.Health}.");
        }

        private static GameplayAssertionResult AssertBattleResultEquals(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (!context.LastBattleResult.HasValue)
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "No battle result recorded. Execute endBattleWithResult first.");

            var result = context.LastBattleResult.Value;
            string expectedWinner = assertion.Parameters["winnerPlayerNumber"]?.ToString();
            bool hasWinner = result.Winners?.Any() ?? false;

            if (expectedWinner != null)
            {
                int winnerPlayerNumber = int.Parse(expectedWinner);
                bool actual = result.Winners?.Any(p => p.PlayerNumber == winnerPlayerNumber) ?? false;
                return actual
                    ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"Winner includes Player {winnerPlayerNumber}.")
                    : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected winner Player {winnerPlayerNumber}, but not found.");
            }

            return hasWinner
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"Battle has winner(s).")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "Battle has no winner.");
        }

        private static GameplayAssertionResult AssertUnitPositionEquals(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (string.IsNullOrWhiteSpace(assertion.Target))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "unitPositionEquals requires a target unit alias.");
            if (!context.Units.TryGetValue(assertion.Target, out var unit))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Unit alias '{assertion.Target}' does not exist.");

            var expected = assertion.Expected;
            if (expected == null || expected.Type != JTokenType.Object)
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "unitPositionEquals requires expected { x, y } coordinates.");

            int expectedX = expected["x"]?.ToObject<int>() ?? 0;
            int expectedY = expected["y"]?.ToObject<int>() ?? 0;

            if (unit.CurrentCell == null)
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"{assertion.Target} has no cell assigned.");

            int actualX = unit.CurrentCell.GridCoordinates.x;
            int actualY = unit.CurrentCell.GridCoordinates.y;

            return actualX == expectedX && actualY == expectedY
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"{assertion.Target}.Position=({actualX},{actualY})")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected {assertion.Target}.Position=({expectedX},{expectedY}), actual=({actualX},{actualY}).");
        }

        private static GameplayAssertionResult AssertUnitManaEquals(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (string.IsNullOrWhiteSpace(assertion.Target))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "unitManaEquals requires a target unit alias.");
            if (!context.Units.TryGetValue(assertion.Target, out var unit))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Unit alias '{assertion.Target}' does not exist.");
            float expected = assertion.Expected?.ToObject<float>() ?? 0f;
            return Math.Abs(unit.Mana - expected) < 0.001f
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"{assertion.Target}.Mana={unit.Mana}")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected {assertion.Target}.Mana={expected}, actual={unit.Mana}.");
        }

        private static GameplayAssertionResult AssertUnitHasBuff(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (string.IsNullOrWhiteSpace(assertion.Target))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "unitHasBuff requires a target unit alias.");
            if (!context.Units.TryGetValue(assertion.Target, out var unit))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Unit alias '{assertion.Target}' does not exist.");
            string expected = assertion.Expected?.ToString() ?? assertion.Parameters["buffName"]?.ToString();
            if (string.IsNullOrWhiteSpace(expected))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "unitHasBuff requires expected buffName.");
            bool hasBuff = unit.GetActiveBuffs().Any(b => b.BuffName == expected);
            return hasBuff
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"{assertion.Target} has buff '{expected}'.")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected {assertion.Target} to have buff '{expected}', but not found.");
        }

        private static GameplayAssertionResult AssertUnitDoesNotHaveBuff(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (string.IsNullOrWhiteSpace(assertion.Target))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "unitDoesNotHaveBuff requires a target unit alias.");
            if (!context.Units.TryGetValue(assertion.Target, out var unit))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Unit alias '{assertion.Target}' does not exist.");
            string buffName = assertion.Expected?.ToString() ?? assertion.Parameters["buffName"]?.ToString();
            if (string.IsNullOrWhiteSpace(buffName))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "unitDoesNotHaveBuff requires expected buffName.");
            bool hasBuff = unit.GetActiveBuffs().Any(b => b.BuffName == buffName);
            return !hasBuff
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"{assertion.Target} does not have buff '{buffName}'.")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected {assertion.Target} to NOT have buff '{buffName}', but it was found.");
        }

        private static GameplayAssertionResult AssertUnitBuffDurationEquals(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (string.IsNullOrWhiteSpace(assertion.Target))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "unitBuffDurationEquals requires a target unit alias.");
            if (!context.Units.TryGetValue(assertion.Target, out var unit))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Unit alias '{assertion.Target}' does not exist.");
            string buffName = assertion.Parameters["buffName"]?.ToString();
            if (string.IsNullOrWhiteSpace(buffName))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "unitBuffDurationEquals requires buffName in parameters.");
            int expected = assertion.Expected?.ToObject<int>() ?? 0;
            var buff = unit.GetActiveBuffs().FirstOrDefault(b => b.BuffName == buffName);
            if (buff == null)
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Buff '{buffName}' not found on {assertion.Target}.");
            int actual = buff.RemainingTurns;
            return actual == expected
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"{assertion.Target}.Buff('{buffName}').Duration={actual}")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected {assertion.Target}.Buff('{buffName}').Duration={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertPlayerNumberEquals(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (string.IsNullOrWhiteSpace(assertion.Target))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "playerNumberEquals requires a target unit alias.");
            if (!context.Units.TryGetValue(assertion.Target, out var unit))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Unit alias '{assertion.Target}' does not exist.");
            int expected = assertion.Expected?.ToObject<int>() ?? 0;
            int actual = unit.PlayerNumber;
            return actual == expected
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"{assertion.Target}.PlayerNumber={actual}")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected {assertion.Target}.PlayerNumber={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertUnitMaxHealthEquals(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (string.IsNullOrWhiteSpace(assertion.Target))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "unitMaxHealthEquals requires a target unit alias.");
            if (!context.Units.TryGetValue(assertion.Target, out var unit))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Unit alias '{assertion.Target}' does not exist.");
            float expected = assertion.Expected?.ToObject<float>() ?? 0f;
            float actual = unit.MaxHealth;
            return Math.Abs(actual - expected) < 0.001f
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"{assertion.Target}.MaxHealth={actual}")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected {assertion.Target}.MaxHealth={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertUnitCountEquals(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            var controller = RequireBattleController(context, assertion.Kind);
            int playerNumber = assertion.Parameters["playerNumber"]?.ToObject<int>() ?? 0;
            int expected = assertion.Expected?.ToObject<int>() ?? 0;
            int actual = controller.GetUnits().Count(u => u.PlayerNumber == playerNumber && !u.IsDowned);
            return actual == expected
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"Player {playerNumber} unit count={actual}")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected Player {playerNumber} unit count={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertUnitCanAct(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (string.IsNullOrWhiteSpace(assertion.Target))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "unitCanAct requires a target unit alias.");
            if (!context.Units.TryGetValue(assertion.Target, out var unit))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Unit alias '{assertion.Target}' does not exist.");
            bool expected = assertion.Expected?.ToObject<bool>() ?? true;
            bool actual = unit.CanAct;
            return actual == expected
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"{assertion.Target}.CanAct={actual}")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected {assertion.Target}.CanAct={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertAiSelectedIntentTypeEquals(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (context.LastAiDecisionLog == null)
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "No AI decision log recorded. Execute executeAI first.");
            string expected = assertion.Expected?.ToString();
            if (string.IsNullOrWhiteSpace(expected))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "aiSelectedIntentTypeEquals requires expected intent type.");
            var entries = context.LastAiDecisionLog.GetEntries();
            var finalSelection = entries.LastOrDefault(e => e.Type == AiDecisionLog.LogType.FinalSelection);
            if (finalSelection == null)
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "No final selection found in decision log.");
            bool matches = finalSelection.Message.Contains(expected, StringComparison.OrdinalIgnoreCase);
            return matches
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"AI selected intent type contains '{expected}'.")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected AI to select '{expected}', but log says: {finalSelection.Message}");
        }

        private static GameplayAssertionResult AssertAiCandidateCountEquals(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (context.LastAiDecisionLog == null)
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "No AI decision log recorded. Execute executeAI first.");
            int expected = assertion.Expected?.ToObject<int>() ?? 0;
            var entries = context.LastAiDecisionLog.GetEntries();
            var candidateEntries = entries.Where(e => e.Type == AiDecisionLog.LogType.CandidateList).ToList();
            // Count candidates from the log (approximate)
            int actual = candidateEntries.Count > 0 ? candidateEntries.Count : 0;
            return actual == expected
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"AI candidate count={actual}")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected AI candidate count={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertAiRuleFilteredCountEquals(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (context.LastAiDecisionLog == null)
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "No AI decision log recorded. Execute executeAI first.");
            int expected = assertion.Expected?.ToObject<int>() ?? 0;
            var entries = context.LastAiDecisionLog.GetEntries();
            int actual = entries.Count(e => e.Type == AiDecisionLog.LogType.RuleFiltered);
            return actual == expected
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"AI rule filtered count={actual}")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected AI rule filtered count={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertAiUsedAbilityEquals(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            var snap = context.LastAiSnapshot;
            if (snap == null)
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "No AI execution snapshot recorded. Execute executeAI first.");
            string expected = assertion.Expected?.ToString();
            if (string.IsNullOrWhiteSpace(expected))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "aiUsedAbilityEquals requires expected ability name.");
            string actual = snap.SelectedAbilityName ?? "";
            return actual.Contains(expected, StringComparison.OrdinalIgnoreCase)
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"AI used ability '{actual}' contains '{expected}'.")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected AI to use ability '{expected}', but used '{actual}'.");
        }

        private static GameplayAssertionResult AssertAiWasNoOpEquals(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            var snap = context.LastAiSnapshot;
            if (snap == null)
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "No AI execution snapshot recorded. Execute executeAI first.");
            bool expected = assertion.Expected?.ToObject<bool>() ?? false;
            bool actual = snap.WasNoOp;
            return actual == expected
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"AI WasNoOp={actual}")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected AI WasNoOp={expected}, actual={actual}. FailureReason={snap.FailureReason}");
        }

        private static GameplayAssertionResult AssertUnitPositionChangedSinceStep(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            var snap = context.LastAiSnapshot;
            if (snap == null)
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "No AI execution snapshot recorded. Execute executeAI first.");
            bool expected = assertion.Expected?.ToObject<bool>() ?? true;
            bool actual = snap.DidMove;
            return actual == expected
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"Unit position changed={actual} (from {snap.ActorPositionBefore.x},{snap.ActorPositionBefore.y} to {snap.ActorPositionAfter.x},{snap.ActorPositionAfter.y})")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected position changed={expected}, actual={actual}. Position: ({snap.ActorPositionBefore.x},{snap.ActorPositionBefore.y}) -> ({snap.ActorPositionAfter.x},{snap.ActorPositionAfter.y})");
        }

        private static GameplayAssertionResult AssertTargetHealthChangedSinceStep(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            var snap = context.LastAiSnapshot;
            if (snap == null)
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "No AI execution snapshot recorded. Execute executeAI first.");
            if (snap.TargetAlias == null)
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "No target specified in executeAI action. Add targetAlias parameter.");
            bool expected = assertion.Expected?.ToObject<bool>() ?? true;
            bool actual = snap.DidDamageTarget || snap.DidHealTarget;
            return actual == expected
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"Target health changed={actual} (before={snap.TargetHealthBefore:F1}, after={snap.TargetHealthAfter:F1})")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected target health changed={expected}, actual={actual}. Health: {snap.TargetHealthBefore:F1} -> {snap.TargetHealthAfter:F1}");
        }

        private static GameplayAssertionResult AssertDecisionLogContains(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (context.LastAiDecisionLog == null)
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "No AI decision log recorded. Execute executeAI first.");
            string expected = assertion.Expected?.ToString();
            if (string.IsNullOrWhiteSpace(expected))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "decisionLogContains requires expected text.");
            var formatted = context.LastAiDecisionLog.GetFormattedLog();
            return formatted.Contains(expected, StringComparison.OrdinalIgnoreCase)
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"Decision log contains '{expected}'.")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Decision log does not contain '{expected}'.");
        }

        private static bool EnsureBattleInitialized(BattleController controller)
        {
            // State 1: Uninitialized — full init
            if (controller.GridState == null)
            {
                try
                {
                    InitializeBattleController(controller);
                    return IsBattleControllerReady(controller);
                }
                catch
                {
                    return false;
                }
            }

            // State 2: Initialized but not started — complete startup
            if (controller.TurnContext.CurrentPlayer == null)
            {
                try
                {
                    controller.StartGame();
                    _appliedResolverTypes[controller.GetInstanceID()] = controller.TurnResolver?.GetType();
                    if (!IsBattleControllerReady(controller))
                        return false;
                    if (!controller.IsBattleActive)
                        _ = controller.StartBattleAsync();
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            // State 3: Already ready — verify ready state, do not trust GridState alone
            if (!IsBattleControllerReady(controller))
                return false;
            if (!controller.IsBattleActive)
                _ = controller.StartBattleAsync();
            return true;
        }

        /// <summary>
        /// Unified ready-state check. A BattleController is "ready" only when both
        /// GridState is set AND TurnContext.CurrentPlayer is non-null. GridState != null
        /// alone is NOT sufficient — StartGame() may early-return without throwing,
        /// leaving a half-initialized anomaly that must be treated as failure.
        /// </summary>
        private static bool IsBattleControllerReady(BattleController controller)
        {
            return controller.GridState != null
                && controller.TurnContext.CurrentPlayer != null;
        }

        private static void InitializeBattleController(BattleController controller)
        {
            controller.InitializeAndStart();
            if (!controller.IsBattleActive)
                _ = controller.StartBattleAsync();
            _appliedResolverTypes[controller.GetInstanceID()] = controller.TurnResolver?.GetType();
        }

        /// <summary>
        /// Unsubscribe the previous BattleEnded handler from the previously bound controller (if any).
        /// Called at the start of BindBattleController to prevent subscription leaks.
        /// </summary>
        private static void CleanupBattleEndedSubscription(GameplayRuntimeContext context)
        {
            if (context.SubscribedBattleController != null && context.BattleEndedHandler != null)
            {
                context.SubscribedBattleController.BattleEnded -= context.BattleEndedHandler;
                context.SubscribedBattleController = null;
                context.BattleEndedHandler = null;
            }
        }

        /// <summary>
        /// Removes only aliases whose key matches <paramref name="pattern"/> from the given dictionary.
        /// Used during rebind to selectively clear battle-registered aliases without touching
        /// aliases registered by other adapters (e.g. Skill's createUnit).
        /// </summary>
        private static void RemoveBattleAliases<T>(Dictionary<string, T> dict, string pattern)
        {
            var regex = new Regex(pattern, RegexOptions.Compiled);
            var keysToRemove = dict.Keys.Where(k => regex.IsMatch(k)).ToList();
            foreach (var key in keysToRemove)
            {
                dict.Remove(key);
            }
        }

        private static BattleController RequireBattleController(GameplayRuntimeContext context, string actionKind)
        {
            return context.BattleController ?? throw new InvalidOperationException($"BattleController has not been bound. Execute 'bindBattleController' before '{actionKind}'.");
        }

        private static GameplayStepResult SpawnCorpse(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            var controller = RequireBattleController(context, action.Kind);
            string unitAlias = action.Parameters?["unitAlias"]?.ToString();
            if (string.IsNullOrWhiteSpace(unitAlias))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "spawnCorpse requires a unitAlias parameter.");

            if (!context.Units.TryGetValue(unitAlias, out var unit))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Unit alias '{unitAlias}' does not exist.");

            unit.IsCorpse = true;
            unit.IsDowned = true;
            unit.Health = -1;
            return GameplayStepResult.Pass(BattleAdapterName, action.Kind, $"Unit '{unitAlias}' marked as corpse.");
        }

        private static GameplayStepResult KillUnit(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            var controller = RequireBattleController(context, action.Kind);
            string unitAlias = action.Parameters?["unitAlias"]?.ToString();
            if (string.IsNullOrWhiteSpace(unitAlias))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "killUnit requires a unitAlias parameter.");

            if (!context.Units.TryGetValue(unitAlias, out var unit))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Unit alias '{unitAlias}' does not exist.");

            unit.ModifyHealth(-unit.Health - 1, null);
            return GameplayStepResult.Pass(BattleAdapterName, action.Kind, $"Unit '{unitAlias}' killed.");
        }

        private static GameplayAssertionResult AssertCellIsBlocked(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            var controller = RequireBattleController(context, assertion.Kind);
            if (string.IsNullOrWhiteSpace(assertion.Target))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "cellIsBlocked requires a target cell alias.");

            if (!context.Cells.TryGetValue(assertion.Target, out var cell))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Cell alias '{assertion.Target}' does not exist.");

            bool expected = assertion.Expected?.ToObject<bool>() ?? true;
            bool actual = cell.IsTaken;
            return actual == expected
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"{assertion.Target}.IsTaken={actual}")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected {assertion.Target}.IsTaken={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertUnitIsCorpse(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (string.IsNullOrWhiteSpace(assertion.Target))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "unitIsCorpse requires a target unit alias.");
            if (!context.Units.TryGetValue(assertion.Target, out var unit))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Unit alias '{assertion.Target}' does not exist.");

            bool expected = assertion.Expected?.ToObject<bool>() ?? true;
            bool actual = unit.IsCorpse;
            return actual == expected
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"{assertion.Target}.IsCorpse={actual}")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected {assertion.Target}.IsCorpse={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertUnitOwnerEquals(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (string.IsNullOrWhiteSpace(assertion.Target))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "unitOwnerEquals requires a target unit alias.");
            if (!context.Units.TryGetValue(assertion.Target, out var unit))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Unit alias '{assertion.Target}' does not exist.");

            string expectedOwner = assertion.Expected?.ToObject<string>();
            if (string.IsNullOrWhiteSpace(expectedOwner))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "unitOwnerEquals requires an expected owner alias string.");
            if (!context.Units.TryGetValue(expectedOwner, out var owner))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Owner alias '{expectedOwner}' does not exist.");

            int actual = unit.OwnerUnitId;
            int expected = owner.UnitID;
            return actual == expected
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"{assertion.Target}.OwnerUnitId={actual}")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected {assertion.Target}.OwnerUnitId={expected}, actual={actual}.");
        }

        /// <summary>
        /// Maps a turn resolver to a normalized semantic key so that wrapper/impl pairs
        /// (e.g. SubsequentTurnResolver vs SubsequentTurnResolverImpl) are treated as equivalent.
        /// </summary>
        private static string NormalizeResolverKind(ITurnResolver resolver)
        {
            return resolver switch
            {
                SubsequentTurnResolver => "subsequent",
                SubsequentTurnResolverImpl => "subsequent",
                UnitSpeedTurnResolver => "unitSpeed",
                _ => $"other:{resolver?.GetType().FullName}"
            };
        }
    }
}
