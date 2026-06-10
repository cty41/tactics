using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Tactics.Common.Cells;
using Tactics.Common.Players;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Skills.Graph.Testing;
using Tactics.Common.Units;

namespace Tactics.Common.Testing.Gameplay
{
    public sealed class SkillGameplayStepAdapter : IGameplayStepAdapter
    {
        public string AdapterName => "Skill";

        public bool CanExecute(ExecutableScenarioAction action)
        {
            return action.Kind is "createSkillTestWorld"
                or "createSkillGraph"
                or "createUnit"
                or "setTurnContext"
                or "executeSkillGraph";
        }

        public async Task<GameplayStepResult> ExecuteAsync(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            try
            {
                switch (action.Kind)
                {
                    case "createSkillTestWorld":
                        context.SkillWorld?.Dispose();
                        context.SkillWorld = new SkillGraphTestWorld();
                        return GameplayStepResult.Pass(AdapterName, action.Kind);
                    case "createSkillGraph":
                        CreateSkillGraph(context, action.Parameters);
                        return GameplayStepResult.Pass(AdapterName, action.Kind);
                    case "createUnit":
                        CreateUnit(context, action.Parameters);
                        return GameplayStepResult.Pass(AdapterName, action.Kind);
                    case "setTurnContext":
                        SetTurnContext(context, action.Parameters);
                        return GameplayStepResult.Pass(AdapterName, action.Kind);
                    case "executeSkillGraph":
                        await ExecuteSkillGraph(context, action.Parameters);
                        return GameplayStepResult.Pass(AdapterName, action.Kind, context.LastSkillResult?.Summary);
                    default:
                        return GameplayStepResult.Fail(AdapterName, action.Kind, $"Unsupported Skill action '{action.Kind}'.");
                }
            }
            catch (Exception ex)
            {
                return GameplayStepResult.Fail(AdapterName, action.Kind, ex.Message);
            }
        }

        public bool CanAssert(ExecutableScenarioAssertion assertion)
        {
            return assertion.Kind is "executionStateEquals"
                or "validationErrorCodeIncludes"
                or "unitHealthEquals";
        }

        public Task<GameplayAssertionResult> AssertAsync(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            try
            {
                GameplayAssertionResult result = assertion.Kind switch
                {
                    "executionStateEquals" => AssertExecutionState(context, assertion),
                    "validationErrorCodeIncludes" => AssertValidationErrorCode(context, assertion),
                    "unitHealthEquals" => AssertUnitHealth(context, assertion),
                    _ => GameplayAssertionResult.Fail(AdapterName, assertion.Kind, $"Unsupported Skill assertion '{assertion.Kind}'.")
                };

                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                return Task.FromResult(GameplayAssertionResult.Fail(AdapterName, assertion.Kind, ex.Message));
            }
        }

        public ProbeSnapshot CaptureProbe(GameplayRuntimeContext context, GameplayProbeRequest request)
        {
            var data = new JObject();
            if (context.LastSkillResult != null)
            {
                data["executionState"] = context.LastSkillResult.ExecutionState.ToString();
                data["lastError"] = context.LastSkillResult.LastError;
                data["stepCount"] = context.LastSkillResult.StepCount;
            }

            if (!string.IsNullOrWhiteSpace(request.Target) && context.Units.TryGetValue(request.Target, out var unit))
            {
                data["unit"] = request.Target;
                data["health"] = unit.Health;
                data["maxHealth"] = unit.MaxHealth;
                data["mana"] = unit.Mana;
                data["playerNumber"] = unit.PlayerNumber;
            }

            return new ProbeSnapshot
            {
                Adapter = AdapterName,
                Kind = request.Kind,
                Target = request.Target,
                Data = data
            };
        }

        private static void CreateSkillGraph(GameplayRuntimeContext context, JObject parameters)
        {
            string alias = GetString(parameters, "alias", "graph");
            string graphKind = GetRequiredString(parameters, "graphKind");
            SkillGraphAsset graph = graphKind switch
            {
                "selfHeal" => SkillGraphTestGraphFactory.CreateSelfHealGraph(alias, GetFloat(parameters, "healAmount", 5f)),
                "singleTargetDamage" => SkillGraphTestGraphFactory.CreateSingleTargetDamageGraph(alias, GetFloat(parameters, "baseDamage", 7f)),
                "invalidSelfHeal" => SkillGraphTestGraphFactory.CreateSelfHealGraph(alias, GetFloat(parameters, "healAmount", 5f), includeFinishNode: false),
                _ => throw new InvalidOperationException($"Unsupported graphKind '{graphKind}'.")
            };

            context.SkillGraphs[alias] = graph;
        }

        private static void CreateUnit(GameplayRuntimeContext context, JObject parameters)
        {
            var world = RequireWorld(context);
            string alias = GetRequiredString(parameters, "alias");
            int playerNumber = GetInt(parameters, "playerNumber", 0);
            ICell cell = null;

            if (parameters["cell"] is JObject cellParameters)
            {
                int x = GetInt(cellParameters, "x", 0);
                int y = GetInt(cellParameters, "y", 0);
                cell = world.CreateSquareCell($"{alias}_Cell", x, y);
            }

            var unit = world.CreateUnit(alias, playerNumber, cell);
            unit.MaxHealth = GetFloat(parameters, "maxHealth", unit.MaxHealth <= 0 ? 10f : unit.MaxHealth);
            unit.Health = GetFloat(parameters, "health", unit.Health);
            unit.MaxMana = GetFloat(parameters, "maxMana", unit.MaxMana);
            unit.Mana = GetFloat(parameters, "mana", unit.Mana);
            unit.DefenceFactor = GetInt(parameters, "defenceFactor", unit.DefenceFactor);
            context.Units[alias] = unit;
        }

        private static void SetTurnContext(GameplayRuntimeContext context, JObject parameters)
        {
            var world = RequireWorld(context);
            int currentPlayerNumber = GetInt(parameters, "currentPlayerNumber", 0);
            IPlayer player = world.PlayerManager.GetPlayerByNumber(currentPlayerNumber);
            if (player == null)
                throw new InvalidOperationException($"Player '{currentPlayerNumber}' does not exist.");

            var aliases = parameters["playableUnitAliases"]?.Values<string>().ToArray() ?? Array.Empty<string>();
            var units = aliases.Select(alias =>
            {
                if (!context.Units.TryGetValue(alias, out var unit))
                    throw new InvalidOperationException($"Playable unit alias '{alias}' does not exist.");
                return unit;
            });

            world.SetTurnContext(player, units);
        }

        private static async Task ExecuteSkillGraph(GameplayRuntimeContext context, JObject parameters)
        {
            var world = RequireWorld(context);
            string graphAlias = GetString(parameters, "graphAlias", "graph");
            string casterAlias = GetRequiredString(parameters, "casterAlias");

            if (!context.SkillGraphs.TryGetValue(graphAlias, out var graph))
                throw new InvalidOperationException($"Skill graph alias '{graphAlias}' does not exist.");
            if (!context.Units.TryGetValue(casterAlias, out var caster))
                throw new InvalidOperationException($"Caster alias '{casterAlias}' does not exist.");

            IUnit primaryTarget = null;
            string targetAlias = GetString(parameters, "primaryTargetAlias", null);
            if (!string.IsNullOrWhiteSpace(targetAlias))
            {
                context.Units.TryGetValue(targetAlias, out primaryTarget);
            }

            var runner = new SkillGraphRuntimeTestRunner();
            context.LastSkillResult = await runner.ExecuteAsync(new SkillGraphRuntimeTestRequest
            {
                Name = graph.DisplayName,
                Graph = graph,
                GridController = world.GridController,
                Caster = caster,
                PrimaryTarget = primaryTarget
            });
        }

        private static GameplayAssertionResult AssertExecutionState(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            var result = RequireSkillResult(context);
            string expected = assertion.Expected?.ToObject<string>();
            string actual = result.ExecutionState.ToString();
            return string.Equals(expected, actual, StringComparison.Ordinal)
                ? GameplayAssertionResult.Pass("Skill", assertion.Kind, $"ExecutionState={actual}")
                : GameplayAssertionResult.Fail("Skill", assertion.Kind, $"Expected ExecutionState={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertValidationErrorCode(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            var result = RequireSkillResult(context);
            string expected = assertion.Expected?.ToObject<string>();
            bool contains = result.ValidationErrors.Any(error => string.Equals(error.Code, expected, StringComparison.Ordinal));
            return contains
                ? GameplayAssertionResult.Pass("Skill", assertion.Kind, $"Validation error '{expected}' found.")
                : GameplayAssertionResult.Fail("Skill", assertion.Kind, $"Validation error '{expected}' was not found.");
        }

        private static GameplayAssertionResult AssertUnitHealth(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (string.IsNullOrWhiteSpace(assertion.Target))
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, "unitHealthEquals requires a target unit alias.");
            if (!context.Units.TryGetValue(assertion.Target, out var unit))
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, $"Unit alias '{assertion.Target}' does not exist.");

            float expected = assertion.Expected?.ToObject<float>() ?? 0f;
            return Math.Abs(unit.Health - expected) < 0.001f
                ? GameplayAssertionResult.Pass("Skill", assertion.Kind, $"{assertion.Target}.Health={unit.Health}")
                : GameplayAssertionResult.Fail("Skill", assertion.Kind, $"Expected {assertion.Target}.Health={expected}, actual={unit.Health}.");
        }

        private static SkillGraphTestWorld RequireWorld(GameplayRuntimeContext context)
        {
            return context.SkillWorld ?? throw new InvalidOperationException("Skill test world has not been created.");
        }

        private static SkillGraphRuntimeTestResult RequireSkillResult(GameplayRuntimeContext context)
        {
            return context.LastSkillResult ?? throw new InvalidOperationException("Skill graph has not been executed.");
        }

        private static string GetRequiredString(JObject parameters, string name)
        {
            string value = GetString(parameters, name, null);
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"Missing required parameter '{name}'.");

            return value;
        }

        private static string GetString(JObject parameters, string name, string defaultValue)
        {
            return parameters.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out var token)
                ? token.ToObject<string>()
                : defaultValue;
        }

        private static int GetInt(JObject parameters, string name, int defaultValue)
        {
            return parameters.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out var token)
                ? token.ToObject<int>()
                : defaultValue;
        }

        private static float GetFloat(JObject parameters, string name, float defaultValue)
        {
            return parameters.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out var token)
                ? token.ToObject<float>()
                : defaultValue;
        }
    }
}
