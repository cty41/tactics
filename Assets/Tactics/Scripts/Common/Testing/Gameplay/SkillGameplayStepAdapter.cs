using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Tactics.Common.Cells;
using Tactics.Common.Players;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Skills.Graph.Testing;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Common.Units.Buffs;
using UnityEngine;

namespace Tactics.Common.Testing.Gameplay
{
    public sealed class SkillGameplayStepAdapter : IGameplayStepAdapter
    {
        private const string SkillAdapterName = "Skill";

        public string AdapterName => "Skill";

        public bool CanExecute(ExecutableScenarioAction action)
        {
            return action.Kind is "createSkillTestWorld"
                or "createSkillGraph"
                or "createCell"
                or "createUnit"
                or "createSkillAbilityConfig"
                or "createSkillAbility"
                or "setTurnContext"
                or "selectAbility"
                or "executeAbilityOnTarget"
                or "executeAbilityOnCell"
                or "executeSkillGraph"
                or "loadSkillGraphAsset";
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
                        context.SkillGraphs.Clear();
                        context.SkillAbilityConfigs.Clear();
                        context.SkillAbilities.Clear();
                        context.Units.Clear();
                        context.Cells.Clear();
                        context.LastSkillResult = null;
                        context.LastStepMessage = null;
                        return GameplayStepResult.Pass(SkillAdapterName, action.Kind);
                    case "createSkillGraph":
                        CreateSkillGraph(context, action.Parameters);
                        return GameplayStepResult.Pass(SkillAdapterName, action.Kind);
                    case "createCell":
                        CreateCell(context, action.Parameters);
                        return GameplayStepResult.Pass(SkillAdapterName, action.Kind);
                    case "createUnit":
                        CreateUnit(context, action.Parameters);
                        return GameplayStepResult.Pass(SkillAdapterName, action.Kind);
                    case "createSkillAbilityConfig":
                        CreateSkillAbilityConfig(context, action.Parameters);
                        return GameplayStepResult.Pass(SkillAdapterName, action.Kind);
                    case "createSkillAbility":
                        CreateSkillAbility(context, action.Parameters);
                        return GameplayStepResult.Pass(SkillAdapterName, action.Kind);
                    case "setTurnContext":
                        SetTurnContext(context, action.Parameters);
                        return GameplayStepResult.Pass(SkillAdapterName, action.Kind);
                    case "selectAbility":
                        SelectAbility(context, action.Parameters);
                        return GameplayStepResult.Pass(SkillAdapterName, action.Kind);
                    case "executeAbilityOnTarget":
                        return await ExecuteAbilityOnTarget(context, action);
                    case "executeAbilityOnCell":
                        return await ExecuteAbilityOnCell(context, action);
                    case "executeSkillGraph":
                        await ExecuteSkillGraph(context, action.Parameters);
                        return GameplayStepResult.Pass(SkillAdapterName, action.Kind, context.LastSkillResult?.Summary);
                    case "loadSkillGraphAsset":
                        return LoadSkillGraphAsset(context, action);
                    default:
                        return GameplayStepResult.Fail(SkillAdapterName, action.Kind, $"Unsupported Skill action '{action.Kind}'.");
                }
            }
            catch (Exception ex)
            {
                return GameplayStepResult.Fail(SkillAdapterName, action.Kind, ex.Message);
            }
        }

        public bool CanAssert(ExecutableScenarioAssertion assertion)
        {
            return assertion.Kind is "executionStateEquals"
                or "validationErrorCodeIncludes"
                or "unitHealthEquals"
                or "unitManaEquals"
                or "unitHasBuff"
                or "unitBuffDurationEquals"
                or "unitBuffCountEquals"
                or "unitBuffIsUnique"
                or "unitCellEquals"
                or "unitCountInArea"
                or "lastErrorContains"
                or "stepMessageContains"
                or "projectileLaunched"
                or "projectileHitTarget"
                or "projectileCompleted"
                or "multiStageStateEquals";
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
                    "unitManaEquals" => AssertUnitMana(context, assertion),
                    "unitHasBuff" => AssertUnitHasBuff(context, assertion),
                    "unitBuffDurationEquals" => AssertUnitBuffDuration(context, assertion),
                    "unitBuffCountEquals" => AssertUnitBuffCount(context, assertion),
                    "unitBuffIsUnique" => AssertUnitBuffIsUnique(context, assertion),
                    "unitCellEquals" => AssertUnitCell(context, assertion),
                    "unitCountInArea" => AssertUnitCountInArea(context, assertion),
                    "lastErrorContains" => AssertLastErrorContains(context, assertion),
                    "stepMessageContains" => AssertStepMessageContains(context, assertion),
                    "projectileLaunched" => AssertProjectileLaunched(context, assertion),
                    "projectileHitTarget" => AssertProjectileHitTarget(context, assertion),
                    "projectileCompleted" => AssertProjectileCompleted(context, assertion),
                    "multiStageStateEquals" => AssertMultiStageState(context, assertion),
                    _ => GameplayAssertionResult.Fail(SkillAdapterName, assertion.Kind, $"Unsupported Skill assertion '{assertion.Kind}'.")
                };

                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                return Task.FromResult(GameplayAssertionResult.Fail(SkillAdapterName, assertion.Kind, ex.Message));
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

            if (!string.IsNullOrWhiteSpace(context.LastStepMessage))
            {
                data["stepMessage"] = context.LastStepMessage;
            }

            if (!string.IsNullOrWhiteSpace(request.Target) && context.Units.TryGetValue(request.Target, out var unit))
            {
                data["unit"] = request.Target;
                data["health"] = unit.Health;
                data["maxHealth"] = unit.MaxHealth;
                data["mana"] = unit.Mana;
                data["playerNumber"] = unit.PlayerNumber;
                data["cellCoordinates"] = unit.CurrentCell?.GridCoordinates.ToString();

                var activeBuffs = new JArray();
                if (unit.BuffComponent != null)
                {
                    foreach (var buff in unit.BuffComponent.GetActiveBuffs())
                    {
                        activeBuffs.Add(new JObject
                        {
                            ["name"] = buff.BuffName,
                            ["remainingTurns"] = buff.RemainingTurns,
                            ["effectType"] = buff.Config?.EffectType.ToString(),
                            ["triggerTiming"] = buff.Config?.TriggerTiming.ToString()
                        });
                    }
                }

                data["activeBuffs"] = activeBuffs;
            }

            return new ProbeSnapshot
            {
                Adapter = SkillAdapterName,
                Kind = request.Kind,
                Target = request.Target,
                Data = data
            };
        }

        private static void CreateSkillGraph(GameplayRuntimeContext context, JObject parameters)
        {
            string alias = GetString(parameters, "alias", "graph");
            string graphKind = GetRequiredString(parameters, "graphKind").Trim().ToLowerInvariant();
            SkillGraphAsset graph = graphKind switch
            {
                "selfheal" => SkillGraphTestGraphFactory.CreateSelfHealGraph(alias, GetFloat(parameters, "healAmount", 5f)),
                "singletargetdamage" => SkillGraphTestGraphFactory.CreateSingleTargetDamageGraph(
                    alias,
                    GetFloat(parameters, "baseDamage", 7f),
                    GetBool(parameters, "canCrit", false),
                    GetBool(parameters, "isRanged", false),
                    GetInt(parameters, "minRange", GetBool(parameters, "isRanged", false) ? 2 : 1),
                    GetInt(parameters, "maxRange", 3)),
                "invalidselfheal" => SkillGraphTestGraphFactory.CreateSelfHealGraph(alias, GetFloat(parameters, "healAmount", 5f), includeFinishNode: false),
                "areadamage" => SkillGraphTestGraphFactory.CreateAreaDamageGraph(alias, GetFloat(parameters, "baseDamage", 3f), GetInt(parameters, "radius", 1), GetInt(parameters, "maxRange", 4)),
                "knockback" => SkillGraphTestGraphFactory.CreateKnockbackGraph(alias, GetInt(parameters, "distance", 1), GetInt(parameters, "maxRange", 1)),
                "allyheal" => SkillGraphTestGraphFactory.CreateAllyHealGraph(alias, GetFloat(parameters, "healAmount", 5f), GetInt(parameters, "maxRange", 1)),
                "charge" => SkillGraphTestGraphFactory.CreateChargeGraph(alias, GetInt(parameters, "distance", 1), GetInt(parameters, "maxRange", 3), GetFloat(parameters, "collisionDamage", 1f)),
                "projectile" => SkillGraphTestGraphFactory.CreateProjectileGraph(alias, GetFloat(parameters, "baseDamage", 5f), GetFloat(parameters, "travelTime", 0.05f)),
                "applybuff" => SkillGraphTestGraphFactory.CreateApplyBuffGraph(
                    alias,
                    GetString(parameters, "buffName", alias),
                    GetInt(parameters, "duration", 2),
                    ParseBuffEffectType(GetString(parameters, "buffEffectType", "None")),
                    ParseBuffTriggerTiming(GetString(parameters, "triggerTiming", "None")),
                    GetString(parameters, "selectionKind", "self"),
                    GetInt(parameters, "maxRange", 1),
                    GetBool(parameters, "isUnique", true),
                    GetBool(parameters, "canAct", true)),
                _ => throw new InvalidOperationException($"Unsupported graphKind '{graphKind}'.")
            };

            context.SkillGraphs[alias] = graph;
        }

        private static GameplayStepResult LoadSkillGraphAsset(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            if (!context.UseRealAssets)
                return GameplayStepResult.Fail(SkillAdapterName, action.Kind, "loadSkillGraphAsset requires useRealAssets to be called first.", "Asset");

            string alias = GetString(action.Parameters, "alias", null);
            if (string.IsNullOrWhiteSpace(alias))
                return GameplayStepResult.Fail(SkillAdapterName, action.Kind, "loadSkillGraphAsset requires an alias parameter.", "Setup");

            string assetPath = GetString(action.Parameters, "assetPath", null);
            if (string.IsNullOrWhiteSpace(assetPath))
                return GameplayStepResult.Fail(SkillAdapterName, action.Kind, "loadSkillGraphAsset requires an assetPath parameter.", "Setup");

            var graphAsset = GameAssetManager.Instance?.Load<SkillGraphAsset>(assetPath);
            if (graphAsset == null)
                return GameplayStepResult.Fail(SkillAdapterName, action.Kind, $"Failed to load SkillGraphAsset from '{assetPath}'.", "Asset");

            context.SkillGraphs[alias] = graphAsset;
            return GameplayStepResult.Pass(SkillAdapterName, action.Kind, $"Loaded SkillGraphAsset '{alias}' from '{assetPath}'.");
        }

        private static void CreateCell(GameplayRuntimeContext context, JObject parameters)
        {
            var world = RequireWorld(context);
            string alias = GetRequiredString(parameters, "alias");
            int x = GetInt(parameters, "x", 0);
            int y = GetInt(parameters, "y", 0);
            float movementCost = GetFloat(parameters, "movementCost", 1f);

            var cell = world.CreateSquareCell(alias, x, y, movementCost);
            context.Cells[alias] = cell;
        }

        private static void CreateUnit(GameplayRuntimeContext context, JObject parameters)
        {
            var world = RequireWorld(context);
            string alias = GetRequiredString(parameters, "alias");
            int playerNumber = GetInt(parameters, "playerNumber", 0);
            ICell cell = null;

            string cellAlias = GetString(parameters, "cellAlias", null);
            if (!string.IsNullOrWhiteSpace(cellAlias))
            {
                cell = RequireCell(context, cellAlias);
            }
            else if (parameters["cell"] is JObject cellParameters)
            {
                int x = GetInt(cellParameters, "x", 0);
                int y = GetInt(cellParameters, "y", 0);
                cell = world.CreateSquareCell($"{alias}_Cell", x, y);
                context.Cells[$"{alias}_Cell"] = cell;
            }

            var unit = world.CreateUnit(alias, playerNumber, cell);
            unit.MaxHealth = GetFloat(parameters, "maxHealth", unit.MaxHealth <= 0 ? 10f : unit.MaxHealth);
            unit.Health = GetFloat(parameters, "health", unit.Health);
            unit.MaxMana = GetFloat(parameters, "maxMana", unit.MaxMana);
            unit.Mana = GetFloat(parameters, "mana", unit.Mana);
            unit.DefenceFactor = GetInt(parameters, "defenceFactor", unit.DefenceFactor);
            unit.AttackFactor = GetInt(parameters, "attackFactor", unit.AttackFactor);
            unit.Strength = GetInt(parameters, "strength", unit.Strength);
            unit.Agility = GetInt(parameters, "agility", unit.Agility);
            unit.Constitution = GetInt(parameters, "constitution", unit.Constitution);
            unit.Intelligence = GetInt(parameters, "intelligence", unit.Intelligence);
            unit.Charisma = GetInt(parameters, "charisma", unit.Charisma);
            unit.Luck = GetInt(parameters, "luck", unit.Luck);
            unit.Speed = GetFloat(parameters, "speed", unit.Speed);
            unit.DodgeRate = GetFloat(parameters, "dodgeRate", unit.DodgeRate);
            unit.Reach = GetInt(parameters, "reach", unit.Reach);
            unit.Range = GetInt(parameters, "range", unit.Range);
            unit.Initiative = GetFloat(parameters, "initiative", unit.Initiative);
            unit.MovementPoints = GetFloat(parameters, "movementPoints", unit.MovementPoints);
            context.Units[alias] = unit;
        }

        private static void CreateSkillAbilityConfig(GameplayRuntimeContext context, JObject parameters)
        {
            string alias = GetRequiredString(parameters, "alias");
            string graphAlias = GetRequiredString(parameters, "graphAlias");
            if (!context.SkillGraphs.TryGetValue(graphAlias, out var graph))
                throw new InvalidOperationException($"Skill graph alias '{graphAlias}' does not exist.");

            var config = ScriptableObject.CreateInstance<SkillGraphAbilityConfig>();
            string displayName = GetString(parameters, "displayName", graph.DisplayName ?? alias);
            string description = GetString(parameters, "description", displayName);
            int manaCost = GetInt(parameters, "manaCost", 0);
            int targetRange = GetInt(parameters, "targetRange", InferTargetRange(graph));

            SetPrivateField(typeof(AbilityConfig), config, "_displayName", displayName);
            SetPrivateField(typeof(AbilityConfig), config, "_description", description);
            SetPrivateField(typeof(AbilityConfig), config, "_manaCost", manaCost);
            SetPrivateField(typeof(AbilityConfig), config, "_cooldown", 0f);
            SetPrivateField(typeof(AbilityConfig), config, "_isBasicAbility", false);
            SetPrivateField(typeof(SkillGraphAbilityConfig), config, "_skillGraph", graph);
            SetPrivateField(typeof(SkillGraphAbilityConfig), config, "_targetRange", targetRange);

            context.SkillAbilityConfigs[alias] = config;
        }

        private static void CreateSkillAbility(GameplayRuntimeContext context, JObject parameters)
        {
            string alias = GetRequiredString(parameters, "alias");
            string configAlias = GetRequiredString(parameters, "configAlias");
            string ownerAlias = GetRequiredString(parameters, "ownerAlias");

            var config = RequireAbilityConfig(context, configAlias);
            var owner = RequireUnit(context, ownerAlias);
            var ability = config.CreateAbility(owner);
            ability.Initialize(RequireWorld(context).GridController);

            context.SkillAbilities[alias] = ability;
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

        private static void SelectAbility(GameplayRuntimeContext context, JObject parameters)
        {
            string abilityAlias = GetRequiredString(parameters, "abilityAlias");
            var ability = RequireAbility(context, abilityAlias);
            ability.OnAbilitySelected(RequireWorld(context).GridController);
        }

        private static async Task<GameplayStepResult> ExecuteAbilityOnTarget(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            string abilityAlias = GetRequiredString(action.Parameters, "abilityAlias");
            string targetAlias = !string.IsNullOrWhiteSpace(action.Target)
                ? action.Target
                : GetRequiredString(action.Parameters, "targetAlias");
            var unit = RequireUnit(context, targetAlias);
            return await ExecuteSkillAbility(context, abilityAlias, unit.CurrentCell, action.Kind);
        }

        private static async Task<GameplayStepResult> ExecuteAbilityOnCell(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            string abilityAlias = GetRequiredString(action.Parameters, "abilityAlias");
            var cell = ResolveCellTarget(context, action);
            return await ExecuteSkillAbility(context, abilityAlias, cell, action.Kind);
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

            ICell targetPoint = null;
            string targetPointAlias = GetString(parameters, "targetPointAlias", null);
            if (!string.IsNullOrWhiteSpace(targetPointAlias))
            {
                targetPoint = ResolvePoint(context, targetPointAlias);
            }

            var runner = new SkillGraphRuntimeTestRunner();
            context.LastSkillResult = await runner.ExecuteAsync(new SkillGraphRuntimeTestRequest
            {
                Name = graph.DisplayName,
                Graph = graph,
                GridController = world.GridController,
                Caster = caster,
                PrimaryTarget = primaryTarget,
                TargetPoint = targetPoint
            });
            context.LastStepMessage = context.LastSkillResult?.Summary;
        }

        private static async Task<GameplayStepResult> ExecuteSkillAbility(GameplayRuntimeContext context, string abilityAlias, ICell selectedCell, string actionKind)
        {
            var ability = RequireAbility(context, abilityAlias);
            if (ability is not SkillGraphAbilityImpl skillAbility)
                throw new InvalidOperationException($"Ability alias '{abilityAlias}' is not a SkillGraphAbilityImpl.");

            var result = await skillAbility.ExecuteForTestAsync(selectedCell, RequireWorld(context).GridController);
            context.LastSkillResult = result;
            context.LastStepMessage = result?.Summary;

            return GameplayStepResult.Pass(SkillAdapterName, actionKind, result?.Summary);
        }

        private static SkillGraphAbilityConfig RequireAbilityConfig(GameplayRuntimeContext context, string alias)
        {
            if (!context.SkillAbilityConfigs.TryGetValue(alias, out var config) || config == null)
                throw new InvalidOperationException($"Skill ability config alias '{alias}' does not exist.");

            return config;
        }

        private static IAbility RequireAbility(GameplayRuntimeContext context, string alias)
        {
            if (!context.SkillAbilities.TryGetValue(alias, out var ability) || ability == null)
                throw new InvalidOperationException($"Skill ability alias '{alias}' does not exist.");

            return ability;
        }

        private static IUnit RequireUnit(GameplayRuntimeContext context, string alias)
        {
            if (!context.Units.TryGetValue(alias, out var unit) || unit == null)
                throw new InvalidOperationException($"Unit alias '{alias}' does not exist.");

            return unit;
        }

        private static ICell RequireCell(GameplayRuntimeContext context, string alias)
        {
            if (!context.Cells.TryGetValue(alias, out var cell) || cell == null)
                throw new InvalidOperationException($"Cell alias '{alias}' does not exist.");

            return cell;
        }

        private static ICell ResolveCellTarget(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            string cellAlias = !string.IsNullOrWhiteSpace(action.Target)
                ? action.Target
                : GetString(action.Parameters, "cellAlias", null);
            if (!string.IsNullOrWhiteSpace(cellAlias))
            {
                return RequireCell(context, cellAlias);
            }

            if (action.Parameters["cell"] is JObject cellParameters)
            {
                int x = GetInt(cellParameters, "x", 0);
                int y = GetInt(cellParameters, "y", 0);
                return RequireWorld(context).GridController.CellManager.GetCellAt(new Tactics.Common.Utilities.Vector2IntImpl(x, y))
                       ?? throw new InvalidOperationException($"Cell ({x}, {y}) does not exist.");
            }

            throw new InvalidOperationException("executeAbilityOnCell requires a cell alias or cell coordinates.");
        }

        private static int InferTargetRange(SkillGraphAsset graph)
        {
            if (graph == null)
                return 1;

            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                if (graph.Nodes[i] is SelectPrimaryTargetNodeRecord selectPrimary)
                    return Math.Max(1, selectPrimary.MaxRange);
            }

            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                if (graph.Nodes[i] is SelectTargetPointNodeRecord selectPoint)
                    return Math.Max(1, selectPoint.MaxRange);
            }

            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                if (graph.Nodes[i] is SelectAllyNodeRecord selectAlly)
                    return Math.Max(1, selectAlly.MaxRange);
            }

            return 1;
        }

        private static void SetPrivateField(Type declaringType, object target, string fieldName, object value)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var field = declaringType.GetField(fieldName, flags);
            if (field == null)
                throw new InvalidOperationException($"Field '{fieldName}' not found on '{declaringType.Name}'.");

            field.SetValue(target, value);
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

        private static GameplayAssertionResult AssertUnitMana(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (string.IsNullOrWhiteSpace(assertion.Target))
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, "unitManaEquals requires a target unit alias.");
            if (!context.Units.TryGetValue(assertion.Target, out var unit))
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, $"Unit alias '{assertion.Target}' does not exist.");

            float expected = assertion.Expected?.ToObject<float>() ?? 0f;
            return Math.Abs(unit.Mana - expected) < 0.001f
                ? GameplayAssertionResult.Pass("Skill", assertion.Kind, $"{assertion.Target}.Mana={unit.Mana}")
                : GameplayAssertionResult.Fail("Skill", assertion.Kind, $"Expected {assertion.Target}.Mana={expected}, actual={unit.Mana}.");
        }

        private static GameplayAssertionResult AssertUnitHasBuff(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (string.IsNullOrWhiteSpace(assertion.Target))
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, "unitHasBuff requires a target unit alias.");
            if (!context.Units.TryGetValue(assertion.Target, out var unit))
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, $"Unit alias '{assertion.Target}' does not exist.");

            string buffName = GetString(assertion.Parameters, "buffName", assertion.Expected?.ToObject<string>());
            if (string.IsNullOrWhiteSpace(buffName))
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, "unitHasBuff requires a buffName parameter or string expected value.");

            var activeBuff = unit.BuffComponent?.GetActiveBuffs()
                .FirstOrDefault(buff => string.Equals(buff.BuffName, buffName, StringComparison.OrdinalIgnoreCase));

            return activeBuff != null
                ? GameplayAssertionResult.Pass("Skill", assertion.Kind, $"{assertion.Target} has buff '{buffName}'.")
                : GameplayAssertionResult.Fail("Skill", assertion.Kind, $"{assertion.Target} does not have buff '{buffName}'.");
        }

        private static GameplayAssertionResult AssertUnitBuffDuration(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (string.IsNullOrWhiteSpace(assertion.Target))
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, "unitBuffDurationEquals requires a target unit alias.");
            if (!context.Units.TryGetValue(assertion.Target, out var unit))
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, $"Unit alias '{assertion.Target}' does not exist.");

            string buffName = GetString(assertion.Parameters, "buffName", null);
            if (string.IsNullOrWhiteSpace(buffName))
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, "unitBuffDurationEquals requires a buffName parameter.");

            var activeBuff = unit.BuffComponent?.GetActiveBuffs()
                .FirstOrDefault(buff => string.Equals(buff.BuffName, buffName, StringComparison.OrdinalIgnoreCase));
            if (activeBuff == null)
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, $"{assertion.Target} does not have buff '{buffName}'.");

            int expected = assertion.Expected?.ToObject<int>() ?? 0;
            return activeBuff.RemainingTurns == expected
                ? GameplayAssertionResult.Pass("Skill", assertion.Kind, $"{assertion.Target}.{buffName}.RemainingTurns={activeBuff.RemainingTurns}")
                : GameplayAssertionResult.Fail("Skill", assertion.Kind, $"Expected {assertion.Target}.{buffName}.RemainingTurns={expected}, actual={activeBuff.RemainingTurns}.");
        }

        private static GameplayAssertionResult AssertUnitCell(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (string.IsNullOrWhiteSpace(assertion.Target))
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, "unitCellEquals requires a target unit alias.");
            if (!context.Units.TryGetValue(assertion.Target, out var unit))
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, $"Unit alias '{assertion.Target}' does not exist.");
            if (unit.CurrentCell == null)
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, $"{assertion.Target} is not placed on a cell.");

            if (!TryParseCellCoordinates(assertion.Expected, out int expectedX, out int expectedY))
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, "unitCellEquals requires expected coordinates in { x, y } form.");

            int actualX = unit.CurrentCell.GridCoordinates.x;
            int actualY = unit.CurrentCell.GridCoordinates.y;
            return actualX == expectedX && actualY == expectedY
                ? GameplayAssertionResult.Pass("Skill", assertion.Kind, $"{assertion.Target}.Cell=({actualX}, {actualY})")
                : GameplayAssertionResult.Fail("Skill", assertion.Kind, $"Expected {assertion.Target}.Cell=({expectedX}, {expectedY}), actual=({actualX}, {actualY}).");
        }

        private static GameplayAssertionResult AssertLastErrorContains(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            var result = RequireSkillResult(context);
            string expected = assertion.Expected?.ToObject<string>();
            if (string.IsNullOrWhiteSpace(expected))
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, "lastErrorContains requires an expected string.");

            bool contains = !string.IsNullOrWhiteSpace(result.LastError)
                && result.LastError.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;

            return contains
                ? GameplayAssertionResult.Pass("Skill", assertion.Kind, $"LastError contains '{expected}'.")
                : GameplayAssertionResult.Fail("Skill", assertion.Kind, $"LastError did not contain '{expected}'. Actual={result.LastError ?? "null"}.");
        }

        private static GameplayAssertionResult AssertStepMessageContains(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            string expected = assertion.Expected?.ToObject<string>();
            if (string.IsNullOrWhiteSpace(expected))
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, "stepMessageContains requires an expected string.");

            bool contains = !string.IsNullOrWhiteSpace(context.LastStepMessage)
                && context.LastStepMessage.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;

            return contains
                ? GameplayAssertionResult.Pass("Skill", assertion.Kind, $"StepMessage contains '{expected}'.")
                : GameplayAssertionResult.Fail("Skill", assertion.Kind, $"StepMessage did not contain '{expected}'. Actual={context.LastStepMessage ?? "null"}.");
        }

        private static GameplayAssertionResult AssertUnitBuffCount(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (string.IsNullOrWhiteSpace(assertion.Target))
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, "unitBuffCountEquals requires a target unit alias.");
            if (!context.Units.TryGetValue(assertion.Target, out var unit))
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, $"Unit alias '{assertion.Target}' does not exist.");

            string buffName = GetString(assertion.Parameters, "buffName", assertion.Expected?.ToObject<string>());
            if (string.IsNullOrWhiteSpace(buffName))
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, "unitBuffCountEquals requires a buffName parameter.");

            int expected = assertion.Expected?.ToObject<int>() ?? GetInt(assertion.Parameters, "expected", 0);
            int actualCount = unit.BuffComponent?.GetActiveBuffs()
                .Count(buff => string.Equals(buff.BuffName, buffName, StringComparison.OrdinalIgnoreCase)) ?? 0;

            return actualCount == expected
                ? GameplayAssertionResult.Pass("Skill", assertion.Kind, $"{assertion.Target} has {actualCount} '{buffName}' buff(s).")
                : GameplayAssertionResult.Fail("Skill", assertion.Kind, $"Expected {assertion.Target} to have {expected} '{buffName}' buff(s), actual={actualCount}.");
        }

        private static GameplayAssertionResult AssertUnitBuffIsUnique(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (string.IsNullOrWhiteSpace(assertion.Target))
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, "unitBuffIsUnique requires a target unit alias.");
            if (!context.Units.TryGetValue(assertion.Target, out var unit))
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, $"Unit alias '{assertion.Target}' does not exist.");

            string buffName = GetString(assertion.Parameters, "buffName", assertion.Expected?.ToObject<string>());
            if (string.IsNullOrWhiteSpace(buffName))
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, "unitBuffIsUnique requires a buffName parameter.");

            int actualCount = unit.BuffComponent?.GetActiveBuffs()
                .Count(buff => string.Equals(buff.BuffName, buffName, StringComparison.OrdinalIgnoreCase)) ?? 0;

            return actualCount <= 1
                ? GameplayAssertionResult.Pass("Skill", assertion.Kind, $"{assertion.Target} has unique '{buffName}' buff (count={actualCount}).")
                : GameplayAssertionResult.Fail("Skill", assertion.Kind, $"Expected {assertion.Target} to have unique '{buffName}' buff, but found {actualCount} instances.");
        }

        private static GameplayAssertionResult AssertUnitCountInArea(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            string centerAlias = GetString(assertion.Parameters, "centerAlias", null);
            if (string.IsNullOrWhiteSpace(centerAlias))
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, "unitCountInArea requires a centerAlias parameter.");

            ICell center = null;
            if (context.Cells.TryGetValue(centerAlias, out var cell))
                center = cell;
            else if (context.Units.TryGetValue(centerAlias, out var unit))
                center = unit.CurrentCell;

            if (center == null)
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, $"Center alias '{centerAlias}' does not resolve to a cell.");

            int radius = GetInt(assertion.Parameters, "radius", 1);
            int expected = assertion.Expected?.ToObject<int>() ?? 0;

            int actualCount = context.Units.Values.Count(u =>
            {
                if (u.CurrentCell == null) return false;
                int dx = Math.Abs(u.CurrentCell.GridCoordinates.x - center.GridCoordinates.x);
                int dy = Math.Abs(u.CurrentCell.GridCoordinates.y - center.GridCoordinates.y);
                return dx <= radius && dy <= radius;
            });

            return actualCount == expected
                ? GameplayAssertionResult.Pass("Skill", assertion.Kind, $"Found {actualCount} unit(s) in area around '{centerAlias}'.")
                : GameplayAssertionResult.Fail("Skill", assertion.Kind, $"Expected {expected} unit(s) in area around '{centerAlias}', actual={actualCount}.");
        }

        private static GameplayAssertionResult AssertProjectileLaunched(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            var result = context.LastSkillResult;
            if (result == null)
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, "Skill graph has not been executed.");

            bool launched = result.ExecutionEvents.Exists(e => e.EventType == "ProjectileLaunched");

            return launched
                ? GameplayAssertionResult.Pass("Skill", assertion.Kind, "Projectile was launched.")
                : GameplayAssertionResult.Fail("Skill", assertion.Kind, "No projectile launch detected.");
        }

        private static GameplayAssertionResult AssertProjectileHitTarget(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (string.IsNullOrWhiteSpace(assertion.Target))
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, "projectileHitTarget requires a target unit alias.");
            if (!context.Units.TryGetValue(assertion.Target, out var unit))
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, $"Unit alias '{assertion.Target}' does not exist.");

            var result = context.LastSkillResult;
            if (result == null)
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, "Skill graph has not been executed.");

            string targetName = unit is UnityEngine.MonoBehaviour mb ? mb.gameObject.name : unit.GetType().Name;
            bool hit = result.ExecutionEvents.Exists(e =>
                e.EventType == "ProjectileHit" &&
                string.Equals(e.TargetUnitName, targetName, StringComparison.OrdinalIgnoreCase));

            return hit
                ? GameplayAssertionResult.Pass("Skill", assertion.Kind, $"Projectile hit target '{assertion.Target}'.")
                : GameplayAssertionResult.Fail("Skill", assertion.Kind, $"Projectile did not hit target '{assertion.Target}'.");
        }

        private static GameplayAssertionResult AssertProjectileCompleted(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            var result = context.LastSkillResult;
            if (result == null)
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, "Skill graph has not been executed.");

            bool completed = result.ExecutionState == SkillGraphExecutionState.Completed &&
                result.ExecutionEvents.Exists(e => e.EventType == "ProjectileHit");

            return completed
                ? GameplayAssertionResult.Pass("Skill", assertion.Kind, "Projectile lifecycle completed.")
                : GameplayAssertionResult.Fail("Skill", assertion.Kind, "Projectile lifecycle did not complete.");
        }

        private static GameplayAssertionResult AssertMultiStageState(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            int stageIndex = GetInt(assertion.Parameters, "stageIndex", -1);
            if (stageIndex < 0)
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, "multiStageStateEquals requires a non-negative stageIndex parameter.");

            string expected = assertion.Expected?.ToObject<string>();
            if (string.IsNullOrWhiteSpace(expected))
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, "multiStageStateEquals requires an expected state string.");

            var result = context.LastSkillResult;
            if (result == null)
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, "Skill graph has not been executed.");

            var stageResult = result.StageResults.Find(s => s.StageIndex == stageIndex);
            if (stageResult == null)
                return GameplayAssertionResult.Fail("Skill", assertion.Kind, $"No stage result found for stageIndex={stageIndex}.");

            string actualState = stageResult.State.ToString();
            return string.Equals(expected, actualState, StringComparison.Ordinal)
                ? GameplayAssertionResult.Pass("Skill", assertion.Kind, $"Stage {stageIndex} state={actualState}.")
                : GameplayAssertionResult.Fail("Skill", assertion.Kind, $"Expected stage {stageIndex} state={expected}, actual={actualState}.");
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

        private static bool GetBool(JObject parameters, string name, bool defaultValue)
        {
            return parameters.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out var token)
                ? token.ToObject<bool>()
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

        private static BuffEffectType ParseBuffEffectType(string value)
        {
            return Enum.TryParse(value, true, out BuffEffectType parsed) ? parsed : BuffEffectType.None;
        }

        private static BuffTriggerTiming ParseBuffTriggerTiming(string value)
        {
            return Enum.TryParse(value, true, out BuffTriggerTiming parsed) ? parsed : BuffTriggerTiming.None;
        }

        private static ICell ResolvePoint(GameplayRuntimeContext context, string alias)
        {
            if (context.Cells.TryGetValue(alias, out var cell) && cell != null)
                return cell;

            if (context.Units.TryGetValue(alias, out var unit) && unit?.CurrentCell != null)
                return unit.CurrentCell;

            throw new InvalidOperationException($"Target point alias '{alias}' does not exist.");
        }

        private static bool TryParseCellCoordinates(JToken token, out int x, out int y)
        {
            x = 0;
            y = 0;

            if (token == null || token.Type == JTokenType.Null)
                return false;

            if (token.Type == JTokenType.Object)
            {
                var obj = (JObject)token;
                if (!obj.TryGetValue("x", StringComparison.OrdinalIgnoreCase, out var xToken))
                    return false;
                if (!obj.TryGetValue("y", StringComparison.OrdinalIgnoreCase, out var yToken))
                    return false;

                x = xToken.ToObject<int>();
                y = yToken.ToObject<int>();
                return true;
            }

            if (token.Type == JTokenType.String)
            {
                var text = token.ToObject<string>();
                if (string.IsNullOrWhiteSpace(text))
                    return false;

                var parts = text.Split(',');
                if (parts.Length != 2)
                    return false;

                return int.TryParse(parts[0].Trim(), out x) && int.TryParse(parts[1].Trim(), out y);
            }

            return false;
        }
    }
}
