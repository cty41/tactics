using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Tactics.Common.Battle;
using Tactics.Common.Controllers.GameResolvers;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
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
                or "executeAbility";
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
                or "unitPositionEquals";
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

            // 确保战斗已初始化（注册单位到 controller）
            if (!EnsureBattleInitialized(controller))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "BattleController initialization failed during bind.");

            // 注册单位别名到 context.Units
            var units = controller.GetUnits().ToList();
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                string alias = $"p{unit.PlayerNumber}_{i}";
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
            controller.EndTurn();
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
            string attackerAlias = action.Parameters["attackerAlias"]?.ToString();
            string targetAlias = action.Parameters["targetAlias"]?.ToString();

            if (string.IsNullOrWhiteSpace(attackerAlias))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "executeAbility requires attackerAlias.");
            if (string.IsNullOrWhiteSpace(targetAlias))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "executeAbility requires targetAlias.");

            if (!context.Units.TryGetValue(attackerAlias, out var attacker))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Attacker alias '{attackerAlias}' not found.");
            if (!context.Units.TryGetValue(targetAlias, out var target))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Target alias '{targetAlias}' not found.");

            ICommand command;
            switch (commandType)
            {
                case "attack":
                    float damage = action.Parameters["damage"]?.ToObject<float>() ?? 5f;
                    command = new AttackCommand(target, damage);
                    break;
                default:
                    return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Unsupported commandType '{commandType}'.");
            }

            await attacker.HumanExecuteAbility(command, controller);
            return GameplayStepResult.Pass(BattleAdapterName, action.Kind, $"Executed {commandType} from {attackerAlias} to {targetAlias}.");
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
