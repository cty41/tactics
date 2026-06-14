using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Tactics.Common.AI.MonsterAI;
using Tactics.Common.Battle;
using Tactics.Common.Cells;
using Tactics.Common.Controllers.GameResolvers;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Common.Units.Buffs;
using UnityEngine;

namespace Tactics.Common.Testing.Gameplay
{
    public sealed class BattleGameplayStepAdapter : IGameplayStepAdapter
    {
        private const string BattleAdapterName = "Battle";

        public string AdapterName => BattleAdapterName;

        public bool CanExecute(ExecutableScenarioAction action)
        {
            return action.Kind is "bindBattleController"
                or "advanceTurn"
                or "endBattleWithResult"
                or "executeAbility"
                or "moveUnit"
                or "setUnitState"
                or "addBuff"
                or "executeAI"
                or "createAiBrain";
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
                    case "executeAbility":
                        return await ExecuteAbility(context, action);
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
                or "unitBuffDurationEquals"
                or "playerNumberEquals"
                or "unitMaxHealthEquals"
                or "unitCountEquals"
                or "unitCanAct"
                or "aiSelectedIntentTypeEquals"
                or "aiCandidateCountEquals"
                or "aiRuleFilteredCountEquals";
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
                    "unitBuffDurationEquals" => AssertUnitBuffDurationEquals(context, assertion),
                    "playerNumberEquals" => AssertPlayerNumberEquals(context, assertion),
                    "unitMaxHealthEquals" => AssertUnitMaxHealthEquals(context, assertion),
                    "unitCountEquals" => AssertUnitCountEquals(context, assertion),
                    "unitCanAct" => AssertUnitCanAct(context, assertion),
                    "aiSelectedIntentTypeEquals" => AssertAiSelectedIntentTypeEquals(context, assertion),
                    "aiCandidateCountEquals" => AssertAiCandidateCountEquals(context, assertion),
                    "aiRuleFilteredCountEquals" => AssertAiRuleFilteredCountEquals(context, assertion),
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

            context.BattleController = controller;

            // 订阅 BattleEnded 事件，自动捕获战斗结果
            controller.BattleEnded += result =>
            {
                context.LastBattleResult = result;
            };

            // 确保战斗已初始化（注册单位到 controller）
            if (!EnsureBattleInitialized(controller))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "BattleController initialization failed during bind.");

            // 注册单位别名到 context.Units（按 PlayerNumber 分组）
            var units = controller.GetUnits().ToList();
            var playerCounters = new Dictionary<int, int>();
            foreach (var unit in units)
            {
                int playerNum = unit.PlayerNumber;
                if (!playerCounters.ContainsKey(playerNum))
                    playerCounters[playerNum] = 0;
                int index = playerCounters[playerNum]++;
                string alias = $"p{playerNum}_{index}";
                context.Units[alias] = unit;
            }

            // 注册格子别名到 context.Cells
            if (controller.CellManager != null)
            {
                foreach (var cell in controller.CellManager.GetCells())
                {
                    var coords = cell.GridCoordinates;
                    string alias = $"cell_{coords.x}_{coords.y}";
                    context.Cells[alias] = cell;
                }
            }

            return GameplayStepResult.Pass(BattleAdapterName, action.Kind, $"Bound {units.Count} units ({string.Join(", ", context.Units.Keys)}), {context.Cells.Count} cells.");
        }

        private static GameplayStepResult AdvanceTurn(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            var controller = RequireBattleController(context, action.Kind);
            if (!EnsureBattleInitialized(controller))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "BattleController initialization failed. Ensure the test scene provides CellManager and players.");
            // 禁用 AI 自动 Play，防止 AI 自动推进回合
            controller.DisableAiAutoPlay = true;
            controller.EndTurn();
            controller.DisableAiAutoPlay = false;
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

        private static async Task<GameplayStepResult> ExecuteAbility(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            var controller = RequireBattleController(context, action.Kind);
            if (!EnsureBattleInitialized(controller))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "BattleController not initialized.");

            string commandType = action.Parameters["commandType"]?.ToString()?.ToLower() ?? "attack";

            ICommand command;
            string executorAlias;
            switch (commandType)
            {
                case "attack":
                    executorAlias = action.Parameters["attackerAlias"]?.ToString();
                    var attackTargetAlias = action.Parameters["targetAlias"]?.ToString();
                    if (string.IsNullOrWhiteSpace(executorAlias) || string.IsNullOrWhiteSpace(attackTargetAlias))
                        return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "attack requires attackerAlias and targetAlias.");
                    if (!context.Units.TryGetValue(executorAlias, out var attacker))
                        return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Attacker alias '{executorAlias}' not found.");
                    if (!context.Units.TryGetValue(attackTargetAlias, out var attackTarget))
                        return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Target alias '{attackTargetAlias}' not found.");
                    float damage = action.Parameters["damage"]?.ToObject<float>() ?? 5f;
                    command = new AttackCommand(attackTarget, damage);
                    await attacker.HumanExecuteAbility(command, controller);
                    break;

                case "heal":
                    executorAlias = action.Parameters["casterAlias"]?.ToString();
                    var healTargetAlias = action.Parameters["targetAlias"]?.ToString();
                    if (string.IsNullOrWhiteSpace(executorAlias) || string.IsNullOrWhiteSpace(healTargetAlias))
                        return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "heal requires casterAlias and targetAlias.");
                    if (!context.Units.TryGetValue(executorAlias, out var caster))
                        return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Caster alias '{executorAlias}' not found.");
                    if (!context.Units.TryGetValue(healTargetAlias, out var healTarget))
                        return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Target alias '{healTargetAlias}' not found.");
                    float healAmount = action.Parameters["healAmount"]?.ToObject<float>() ?? 3f;
                    command = new HealCommand(healTarget, caster, healAmount);
                    await caster.HumanExecuteAbility(command, controller);
                    break;

                case "move":
                    var moveUnitAlias = action.Parameters["unitAlias"]?.ToString();
                    var destCellAlias = action.Parameters["destinationCellAlias"]?.ToString();
                    if (string.IsNullOrWhiteSpace(moveUnitAlias) || string.IsNullOrWhiteSpace(destCellAlias))
                        return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "move requires unitAlias and destinationCellAlias.");
                    if (!context.Units.TryGetValue(moveUnitAlias, out var moveUnit))
                        return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Unit alias '{moveUnitAlias}' not found.");
                    if (!context.Cells.TryGetValue(destCellAlias, out var destCell))
                        return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Cell alias '{destCellAlias}' not found.");
                    var sourceCell = moveUnit.CurrentCell;
                    if (sourceCell == null)
                        return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Unit '{moveUnitAlias}' has no current cell.");
                    var pathCellAliases = action.Parameters["pathCellAliases"]?.ToObject<string[]>();
                    IEnumerable<ICell> path;
                    if (pathCellAliases != null && pathCellAliases.Length > 0)
                    {
                        var pathCells = new List<ICell>();
                        foreach (var pca in pathCellAliases)
                        {
                            if (context.Cells.TryGetValue(pca, out var pc))
                                pathCells.Add(pc);
                        }
                        path = pathCells;
                    }
                    else
                    {
                        path = new[] { sourceCell, destCell };
                    }
                    command = new MoveCommand(sourceCell, destCell, path);
                    await moveUnit.HumanExecuteAbility(command, controller);
                    break;

                case "fireball":
                    var fbCasterAlias = action.Parameters["casterAlias"]?.ToString();
                    var fbTargetCellAlias = action.Parameters["targetCellAlias"]?.ToString();
                    if (string.IsNullOrWhiteSpace(fbCasterAlias) || string.IsNullOrWhiteSpace(fbTargetCellAlias))
                        return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "fireball requires casterAlias and targetCellAlias.");
                    if (!context.Units.TryGetValue(fbCasterAlias, out var fbCaster))
                        return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Caster alias '{fbCasterAlias}' not found.");
                    if (!context.Cells.TryGetValue(fbTargetCellAlias, out var fbTargetCell))
                        return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Cell alias '{fbTargetCellAlias}' not found.");
                    float fbDamage = action.Parameters["damage"]?.ToObject<float>() ?? 5f;
                    int fbManaCost = action.Parameters["manaCost"]?.ToObject<int>() ?? 3;
                    var fbAoeCellAliases = action.Parameters["aoeCellAliases"]?.ToObject<string[]>();
                    List<ICell> aoeCells;
                    if (fbAoeCellAliases != null && fbAoeCellAliases.Length > 0)
                    {
                        aoeCells = new List<ICell>();
                        foreach (var aca in fbAoeCellAliases)
                        {
                            if (context.Cells.TryGetValue(aca, out var ac))
                                aoeCells.Add(ac);
                        }
                    }
                    else
                    {
                        aoeCells = new List<ICell> { fbTargetCell };
                        // Add neighbors if available
                        var cellMgr = controller.CellManager;
                        if (cellMgr != null)
                        {
                            foreach (var cell in cellMgr.GetCells())
                            {
                                var dist = Math.Abs(cell.GridCoordinates.x - fbTargetCell.GridCoordinates.x)
                                         + Math.Abs(cell.GridCoordinates.y - fbTargetCell.GridCoordinates.y);
                                if (dist == 1)
                                    aoeCells.Add(cell);
                            }
                        }
                    }
                    command = new FireballCommand(fbTargetCell, fbCaster, aoeCells, fbDamage, fbManaCost);
                    await fbCaster.HumanExecuteAbility(command, controller);
                    break;

                default:
                    return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Unsupported commandType '{commandType}'.");
            }

            return GameplayStepResult.Pass(BattleAdapterName, action.Kind, $"Executed {commandType}.");
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
            unit.CurrentCell = destCell;

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

            // Create BuffConfig programmatically
            var config = ScriptableObject.CreateInstance<BuffConfig>();
            var nameField = typeof(BuffConfig).GetField("_buffName", BindingFlags.NonPublic | BindingFlags.Instance);
            nameField?.SetValue(config, buffName);
            var durationField = typeof(BuffConfig).GetField("_defaultDuration", BindingFlags.NonPublic | BindingFlags.Instance);
            durationField?.SetValue(config, duration);

            // Create and add buff
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

            try
            {
                // 绕过活跃单位检查，允许 AI 直接执行命令
                controller.BypassActiveUnitCheck = true;
                var decisionLog = await AI.MonsterAI.AiBrainRunner.ExecuteWithLog(unit, controller, brainAsset);
                controller.BypassActiveUnitCheck = false;
                context.LastAiDecisionLog = decisionLog;
                return GameplayStepResult.Pass(BattleAdapterName, action.Kind, $"Executed AI for {unitAlias} using brain '{brainAlias}'.");
            }
            catch (Exception ex)
            {
                controller.BypassActiveUnitCheck = false;
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

        private static bool EnsureBattleInitialized(BattleController controller)
        {
            if (controller.GridState != null)
            {
                if (!controller.IsBattleActive)
                    _ = controller.StartBattleAsync();
                return true;
            }

            try
            {
                controller.InitializeAndStart();
                if (!controller.IsBattleActive)
                    _ = controller.StartBattleAsync();
                return controller.GridState != null;
            }
            catch
            {
                return false;
            }
        }

        private static BattleController RequireBattleController(GameplayRuntimeContext context, string actionKind)
        {
            return context.BattleController ?? throw new InvalidOperationException($"BattleController has not been bound. Execute 'bindBattleController' before '{actionKind}'.");
        }
    }
}
