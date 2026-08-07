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
using Tactics.Common.Interactables;
using Tactics.Common.Players;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Common.Units.Buffs;
using Tactics.Common.Utilities;
using Tactics.AssetPipeline;
using Tactics.Common.Controllers;
using Tactics.Common.Controllers.TurnResolvers;
using Tactics.Consumables;
using Tactics.Controllers.TurnResolvers;
using Tactics.Roguelike;
using Tactics.Roster;
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
                or "useCarriedConsumable"
                or "addBuff"
                or "executeAI"
                or "createAiBrain"
                or "useRealAssets"
                or "spawnCorpse"
                or "killUnit"
                or "spawnInteractableCorpse"
                or "consumeInteractableCorpseAt"
                or "setUnitFacing"
                or "initializeInitiativeOrder"
                or "advanceInitiative"
                or "tickUnitTurnStart"
                or "tickUnitTurnEnd"
                or "registerSummon"
                or "beginOrderedTargetSelection"
                or "selectOrderedTarget"
                or "undoOrderedTargetSelection"
                or "commitOrderedTargetSelection"
                or "cancelOrderedTargetSelection"
                or "bindPureRunAbilityToUnit"
                or "dropAmazonSpear"
                or "clickBattleUnit"
                or "spawnBattleUnit"
                or "restartBattle"
                or "waitForBattleEnd";
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
                        return await EndBattleWithResult(context, action);
                    case "executeBattleSkillGraph":
                        return await ExecuteBattleSkillGraph(context, action);
                    case "moveUnit":
                        return MoveUnit(context, action);
                    case "setUnitState":
                        return SetUnitState(context, action);
                    case "useCarriedConsumable":
                        return await UseCarriedConsumable(context, action);
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
                    case "spawnInteractableCorpse":
                        return SpawnInteractableCorpse(context, action);
                    case "consumeInteractableCorpseAt":
                        return ConsumeInteractableCorpseAt(context, action);
                    case "setUnitFacing":
                        return SetUnitFacing(context, action);
                    case "initializeInitiativeOrder":
                        return InitializeInitiativeOrder(context, action);
                    case "advanceInitiative":
                        return AdvanceInitiative(context, action);
                    case "tickUnitTurnStart":
                        return TickUnitTurn(context, action, turnStart: true);
                    case "tickUnitTurnEnd":
                        return TickUnitTurn(context, action, turnStart: false);
                    case "registerSummon":
                        return RegisterSummon(context, action);
                    case "beginOrderedTargetSelection":
                        return BeginOrderedTargetSelection(context, action);
                    case "selectOrderedTarget":
                        return SelectOrderedTarget(context, action);
                    case "undoOrderedTargetSelection":
                        return UndoOrderedTargetSelection(context, action);
                    case "commitOrderedTargetSelection":
                        return CommitOrderedTargetSelection(context, action);
                    case "cancelOrderedTargetSelection":
                        return CancelOrderedTargetSelection(context, action);
                    case "bindPureRunAbilityToUnit":
                        return BindPureRunAbilityToUnit(context, action);
                    case "dropAmazonSpear":
                        return DropAmazonSpear(context, action);
                    case "clickBattleUnit":
                        return ClickBattleUnit(context, action);
                    case "spawnBattleUnit":
                        return SpawnBattleUnit(context, action);
                    case "restartBattle":
                        return await RestartBattle(context, action);
                    case "waitForBattleEnd":
                        return await WaitForBattleEnd(context, action);
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
                or "unitCanReceiveHealingEquals"
                or "unitCountEquals"
                or "unitCanAct"
                or "aiSelectedIntentTypeEquals"
                or "aiCandidateCountEquals"
                or "aiRuleFilteredCountEquals"
                or "aiUsedAbilityEquals"
                or "aiWasNoOpEquals"
                or "aiTurnSucceededEquals"
                or "aiTurnAbilityEquals"
                or "aiTurnDestinationEquals"
                or "aiTurnTargetPointEquals"
                or "aiTurnTargetCountEquals"
                or "aiTurnUsedFallbackEquals"
                or "aiTurnPatternStepEquals"
                or "unitPositionChangedSinceStep"
                or "targetHealthChangedSinceStep"
                or "decisionLogContains"
                or "cellIsBlocked"
                or "unitIsCorpse"
                or "interactableCorpseExistsAt"
                or "cellOccupiedByInteractable"
                or "unitOwnerEquals"
                or "unitFacingEquals"
                or "currentRoundOrderEquals"
                or "unitStatusStacksEquals"
                or "unitStatusRemainingActionsEquals"
                or "summonOrderEquals"
                or "summonCategoryEquals"
                or "abilityAvailabilityEquals"
                or "abilityAvailabilityReasonEquals"
                or "actualSkillLevelEquals"
                or "unitAbilityListEquals"
                or "orderedTargetSelectionEquals"
                or "selectionStageEquals"
                or "spearHolderEquals"
                or "spearCellEquals"
                or "decoyRemainingActionsEquals"
                or "aiTargetEquals";
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
                    "unitCanReceiveHealingEquals" => AssertUnitCanReceiveHealingEquals(context, assertion),
                    "unitCountEquals" => AssertUnitCountEquals(context, assertion),
                    "unitCanAct" => AssertUnitCanAct(context, assertion),
                    "aiSelectedIntentTypeEquals" => AssertAiSelectedIntentTypeEquals(context, assertion),
                    "aiCandidateCountEquals" => AssertAiCandidateCountEquals(context, assertion),
                    "aiRuleFilteredCountEquals" => AssertAiRuleFilteredCountEquals(context, assertion),
                    "aiUsedAbilityEquals" => AssertAiUsedAbilityEquals(context, assertion),
                    "aiWasNoOpEquals" => AssertAiWasNoOpEquals(context, assertion),
                    "aiTurnSucceededEquals" => AssertAiTurnBooleanField(context, assertion, "succeeded", result => result.Succeeded),
                    "aiTurnAbilityEquals" => AssertAiTurnStringField(context, assertion, "ability", result => result.AbilityId),
                    "aiTurnDestinationEquals" => AssertAiTurnStringField(context, assertion, "destination", result => result.Destination),
                    "aiTurnTargetPointEquals" => AssertAiTurnStringField(context, assertion, "target point", result => result.TargetPoint),
                    "aiTurnTargetCountEquals" => AssertAiTurnTargetCountEquals(context, assertion),
                    "aiTurnUsedFallbackEquals" => AssertAiTurnBooleanField(context, assertion, "fallback", result => result.UsedFallback),
                    "aiTurnPatternStepEquals" => AssertAiTurnStringField(context, assertion, "pattern step", result => result.PatternStep),
                    "unitPositionChangedSinceStep" => AssertUnitPositionChangedSinceStep(context, assertion),
                    "targetHealthChangedSinceStep" => AssertTargetHealthChangedSinceStep(context, assertion),
                    "decisionLogContains" => AssertDecisionLogContains(context, assertion),
                    "cellIsBlocked" => AssertCellIsBlocked(context, assertion),
                    "unitIsCorpse" => AssertUnitIsCorpse(context, assertion),
                    "unitOwnerEquals" => AssertUnitOwnerEquals(context, assertion),
                    "interactableCorpseExistsAt" => AssertInteractableCorpseExistsAt(context, assertion),
                    "cellOccupiedByInteractable" => AssertCellOccupiedByInteractable(context, assertion),
                    "unitFacingEquals" => AssertUnitFacingEquals(context, assertion),
                    "currentRoundOrderEquals" => AssertCurrentRoundOrderEquals(context, assertion),
                    "unitStatusStacksEquals" => AssertUnitStatusValue(context, assertion, stacks: true),
                    "unitStatusRemainingActionsEquals" => AssertUnitStatusValue(context, assertion, stacks: false),
                    "summonOrderEquals" => AssertSummonOrderEquals(context, assertion),
                    "summonCategoryEquals" => AssertSummonCategoryEquals(context, assertion),
                    "abilityAvailabilityEquals" => AssertAbilityAvailabilityEquals(context, assertion),
                    "abilityAvailabilityReasonEquals" => AssertAbilityAvailabilityReasonEquals(context, assertion),
                    "actualSkillLevelEquals" => AssertActualSkillLevelEquals(context, assertion),
                    "unitAbilityListEquals" => AssertUnitAbilityListEquals(context, assertion),
                    "orderedTargetSelectionEquals" => AssertOrderedTargetSelectionEquals(context, assertion),
                    "selectionStageEquals" => AssertSelectionStageEquals(context, assertion),
                    "spearHolderEquals" => AssertObservedAlias(context, assertion, context.SpearHolderAlias, "spear holder"),
                    "spearCellEquals" => AssertObservedAlias(context, assertion, context.SpearCellAlias, "spear cell"),
                    "decoyRemainingActionsEquals" => AssertDecoyRemainingActionsEquals(context, assertion),
                    "aiTargetEquals" => AssertObservedAlias(context, assertion, context.LastAiTargetAlias, "AI target"),
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
                data["canReceiveHealing"] = unit.CanReceiveHealing;
                data["facing"] = unit.Facing.ToString();
                data["activeBuffs"] = new JArray(unit.GetActiveBuffs().Select(b => b.BuffName));
                data["statusStacks"] = JObject.FromObject(unit.GetActiveBuffs()
                    .GroupBy(buff => buff.BuffName)
                    .ToDictionary(group => group.Key, group => group.Last().StackCount));
                data["statusRemainingActions"] = JObject.FromObject(unit.GetActiveBuffs()
                    .GroupBy(buff => buff.BuffName)
                    .ToDictionary(group => group.Key, group => group.Last().RemainingTurns));
            }

            if (context.InitiativeService != null)
            {
                data["currentRoundOrder"] = new JArray(context.InitiativeService.GetCurrentRoundOrder()
                    .Select(unit => FindUnitAlias(context, unit) ?? $"Unit_{unit.UnitID}"));
            }

            if (context.OrderedTargetSelection != null)
            {
                data["selectionStage"] = context.OrderedTargetSelection.Stage.ToString();
                data["orderedTargets"] = new JArray(context.OrderedTargetSelection.Targets
                    .Select(unit => FindUnitAlias(context, unit) ?? $"Unit_{unit.UnitID}"));
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

            var turnResult = context.LastAiTurnResult;
            if (turnResult != null)
            {
                data["aiTurnSucceeded"] = turnResult.Succeeded;
                data["aiTurnAbility"] = turnResult.AbilityId ?? "";
                data["aiTurnDestination"] = turnResult.Destination ?? "";
                data["aiTurnTargetPoint"] = turnResult.TargetPoint ?? "";
                data["aiTurnTargetCount"] = turnResult.TargetCount;
                data["aiTurnUsedFallback"] = turnResult.UsedFallback;
                data["aiTurnPatternStep"] = turnResult.PatternStep ?? "";
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

        private static GameplayStepResult BindPureRunAbilityToUnit(
            GameplayRuntimeContext context,
            ExecutableScenarioAction action)
        {
            string unitAlias = action.Parameters["unitAlias"]?.ToString();
            string skillId = action.Parameters["skillId"]?.ToString();
            int level = action.Parameters["level"]?.ToObject<int>() ?? 1;
            if (!context.Units.TryGetValue(unitAlias ?? string.Empty, out var unit) || unit is not Unit concreteUnit)
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Unit '{unitAlias}' not found.");
            if (!PureRunAbilityCatalog.TryResolveAbilityPath(skillId, level, out string path, out int resolvedLevel))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Ability '{skillId}' Lv{level} is not published.");
            var config = GameAssetManager.Instance?.Load<AbilityConfig>(path);
            if (config == null)
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"AbilityConfig '{path}' could not be loaded.");
            concreteUnit.ApplyLearnedSkillLevels(new[]
            {
                new CharacterDefinition.LearnedSkill
                {
                    SkillId = skillId,
                    SkillType = SkillType.Active,
                    Level = resolvedLevel
                }
            });
            var ability = config.CreateAbility(unit);
            unit.RegisterAbility(ability, RequireBattleController(context, action.Kind));
            return GameplayStepResult.Pass(BattleAdapterName, action.Kind,
                $"Bound '{skillId}' Lv{resolvedLevel} to '{unitAlias}'.");
        }

        private static GameplayStepResult DropAmazonSpear(
            GameplayRuntimeContext context,
            ExecutableScenarioAction action)
        {
            string unitAlias = action.Parameters["unitAlias"]?.ToString();
            string cellAlias = action.Parameters["cellAlias"]?.ToString();
            if (!context.Units.TryGetValue(unitAlias ?? string.Empty, out var unit))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Unit '{unitAlias}' not found.");
            if (!context.Cells.TryGetValue(cellAlias ?? string.Empty, out var cell))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Cell '{cellAlias}' not found.");
            bool dropped = AmazonBattleState.For(RequireBattleController(context, action.Kind)).DropSpear(unit, cell);
            return dropped
                ? GameplayStepResult.Pass(BattleAdapterName, action.Kind, $"Dropped spear at '{cellAlias}'.")
                : GameplayStepResult.Fail(BattleAdapterName, action.Kind, "Spear drop failed.");
        }

        private static GameplayStepResult SpawnBattleUnit(
            GameplayRuntimeContext context,
            ExecutableScenarioAction action)
        {
            var controller = RequireBattleController(context, action.Kind);
            string alias = action.Parameters["alias"]?.ToString();
            string cellAlias = action.Parameters["cellAlias"]?.ToString();
            if (string.IsNullOrWhiteSpace(alias) || context.Units.ContainsKey(alias))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "spawnBattleUnit requires a unique alias.");
            if (!context.Cells.TryGetValue(cellAlias ?? string.Empty, out var cell))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Cell '{cellAlias}' not found.");

            // Real journey plans configure the next battle's units before restartBattle.
            // Clear only corpses snapshotted from the completed battle before occupancy checks.
            ClearCompletedBattleCorpseResidue(context);
            if (cell.IsTaken)
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Cell '{cellAlias}' is occupied.");

            var gameObject = new GameObject($"Gameplay_{alias}");
            var unit = gameObject.AddComponent<Unit>();
            unit.PlayerNumber = action.Parameters["playerNumber"]?.ToObject<int>() ?? 2;
            unit.MaxHealth = action.Parameters["maxHealth"]?.ToObject<float>() ?? 8f;
            unit.Health = action.Parameters["health"]?.ToObject<float>() ?? unit.MaxHealth;
            unit.MaxMana = action.Parameters["maxMana"]?.ToObject<float>() ?? 0f;
            unit.Mana = action.Parameters["mana"]?.ToObject<float>() ?? unit.MaxMana;
            unit.CurrentCell = cell;
            cell.CurrentUnits.Add(unit);
            cell.IsTaken = true;
            unit.Initialize(controller);
            controller.UnitManager.AddUnit(unit);

            context.OwnedRuntimeGameObjects.Add(gameObject);
            context.Units[alias] = unit;
            return GameplayStepResult.Pass(BattleAdapterName, action.Kind, $"Spawned '{alias}' at '{cellAlias}'.");
        }

        private static async Task<GameplayStepResult> RestartBattle(
            GameplayRuntimeContext context,
            ExecutableScenarioAction action)
        {
            var controller = RequireBattleController(context, action.Kind);
            if (controller.IsBattleActive)
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "Cannot restart an active battle.");

            // Only corpses captured when the previous battle ended are residue. Corpses
            // staged afterwards belong to the next battle and must remain intact.
            ClearCompletedBattleCorpseResidue(context);
            context.LastBattleResult = null;
            await controller.StartBattleAsync();
            return controller.IsBattleActive
                ? GameplayStepResult.Pass(BattleAdapterName, action.Kind, "Battle restarted with the current units.")
                : GameplayStepResult.Fail(BattleAdapterName, action.Kind, "Battle did not become active.");
        }

        private static void CaptureCompletedBattleCorpseResidue(
            GameplayRuntimeContext context,
            BattleController controller)
        {
            context.CompletedBattleCorpseResidue.Clear();
            IEnumerable<ICell> cells = controller?.CellManager?.GetCells() ?? Array.Empty<ICell>();
            foreach (ICell cell in cells)
            {
                foreach (Corpse corpse in cell.CurrentInteractables.OfType<Corpse>())
                    context.CompletedBattleCorpseResidue.Add((cell, corpse));
            }
        }

        private static void ClearCompletedBattleCorpseResidue(GameplayRuntimeContext context)
        {
            foreach ((ICell cell, Corpse corpse) in context.CompletedBattleCorpseResidue)
            {
                if (corpse != null && !corpse.IsDestroyed)
                {
                    corpse.Consume();
                }
                else
                {
                    // Unity fake-null can leave the managed wrapper in the Cell list when
                    // a corpse GameObject was destroyed without going through Consume().
                    cell?.RemoveInteractable(corpse);
                }

                string[] aliases = context.InteractableCorpsesByCell
                    .Where(pair => ReferenceEquals(pair.Value, corpse))
                    .Select(pair => pair.Key)
                    .ToArray();
                foreach (string alias in aliases)
                    context.InteractableCorpsesByCell.Remove(alias);
            }

            context.CompletedBattleCorpseResidue.Clear();
        }

        private static async Task<GameplayStepResult> WaitForBattleEnd(
            GameplayRuntimeContext context,
            ExecutableScenarioAction action)
        {
            int maxFrames = action.Parameters["maxFrames"]?.ToObject<int>() ?? 120;
            for (int frame = 0; frame < Math.Max(1, maxFrames); frame++)
            {
                if (context.LastBattleResult.HasValue)
                {
                    CaptureCompletedBattleCorpseResidue(context, context.BattleController);
                    return GameplayStepResult.Pass(BattleAdapterName, action.Kind, "Natural battle result was observed.");
                }
                await Task.Yield();
            }

            string units = string.Join(", ", context.BattleController?.GetUnits()
                .Select(unit => $"P{unit.PlayerNumber}:HP={unit.Health}:Down={unit.IsDowned}") ?? Array.Empty<string>());
            return GameplayStepResult.Fail(
                BattleAdapterName,
                action.Kind,
                $"No battle result was observed after {maxFrames} frames. Active={context.BattleController?.IsBattleActive}; Units=[{units}].");
        }

        private static GameplayStepResult ClickBattleUnit(
            GameplayRuntimeContext context,
            ExecutableScenarioAction action)
        {
            string unitAlias = action.Parameters["unitAlias"]?.ToString();
            if (!context.Units.TryGetValue(unitAlias ?? string.Empty, out var unit))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Unit '{unitAlias}' not found.");
            var controller = RequireBattleController(context, action.Kind);
            var runtimeController = typeof(BattleController)
                .GetField("_controller", BindingFlags.Instance | BindingFlags.NonPublic)?
                .GetValue(controller) as GridController;
            if (runtimeController == null)
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "Battle runtime GridController not found.");
            var orderedAbility = context.Units.Values
                .SelectMany(candidate => candidate.GetBaseAbilities() ?? Array.Empty<IAbility>())
                .OfType<SkillGraphAbilityImpl>()
                .FirstOrDefault(candidate => candidate.OrderedSelection != null);
            controller.GridState?.OnUnitClicked(unit, runtimeController);
            if (action.Parameters["expectedOrderedCount"] != null)
            {
                int expectedCount = action.Parameters["expectedOrderedCount"].ToObject<int>();
                int actualCount = orderedAbility?.OrderedSelection?.Targets.Count ?? -1;
                if (actualCount != expectedCount)
                {
                    return GameplayStepResult.Fail(BattleAdapterName, action.Kind,
                        $"Expected ordered target count {expectedCount}, actual {actualCount}; " +
                        $"gridState={controller.GridState?.GetType().Name}, targetCell={unit.CurrentCell?.GridCoordinates}.");
                }
            }
            return GameplayStepResult.Pass(BattleAdapterName, action.Kind, $"Clicked battle unit '{unitAlias}'.");
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

        private static async Task<GameplayStepResult> EndBattleWithResult(
            GameplayRuntimeContext context,
            ExecutableScenarioAction action)
        {
            var controller = RequireBattleController(context, action.Kind);
            GameResult result;

            var winnerPlayerNumberToken = action.Parameters["winnerPlayerNumber"];
            if (winnerPlayerNumberToken != null)
            {
                int winnerPlayerNumber = winnerPlayerNumberToken.ToObject<int>();
                var winner = new HumanPlayer
                {
                    PlayerNumber = winnerPlayerNumber,
                    PlayerType = PlayerType.HumanPlayer
                };

                var loserPlayerNumbers = ReadPlayerNumbers(action, "loserPlayerNumber", "loserPlayerNumbers");
                var losers = loserPlayerNumbers.Count > 0
                    ? loserPlayerNumbers.Select(number => (IPlayer)new AIPlayer
                    {
                        PlayerNumber = number,
                        PlayerType = PlayerType.AutomatedPlayer
                    }).ToList()
                    : controller.GetUnits()
                        .Select(unit => unit.PlayerNumber)
                        .Distinct()
                        .Where(number => number != winnerPlayerNumber)
                        .Select(number => (IPlayer)new AIPlayer
                        {
                            PlayerNumber = number,
                            PlayerType = PlayerType.AutomatedPlayer
                        })
                        .ToList();

                result = new GameResult(winner, losers);
            }
            else
            {
                result = new GameResult();
            }

            bool skipControllerEndBattle = action.Parameters["skipControllerEndBattle"]?.ToObject<bool>() ?? false;
            if (!skipControllerEndBattle)
                await controller.EndBattleAsync(result);

            bool applyRoguelikeWriteback = action.Parameters["applyRoguelikeWriteback"]?.ToObject<bool>() ?? false;
            if (applyRoguelikeWriteback)
                ApplyRoguelikeWriteback(controller, result);

            CaptureCompletedBattleCorpseResidue(context, controller);
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
            if (action.Parameters["orderedTargetAliases"] is JArray orderedAliases)
            {
                foreach (string alias in orderedAliases.Values<string>())
                {
                    if (!context.Units.TryGetValue(alias, out var orderedTarget))
                        return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Ordered target alias '{alias}' not found.");
                    skillContext.TargetSet.Add(orderedTarget);
                }
                skillContext.PrimaryTarget = skillContext.TargetSet.FirstOrDefault();
            }

            var facingTarget = targetPoint ?? primaryTarget?.CurrentCell;
            var originalFacing = caster.Facing;
            var actionFacing = originalFacing;
            bool changedFacing = facingTarget != null && caster.CurrentCell != null &&
                FacingResolver.TryResolve(
                    caster.CurrentCell.GridCoordinates,
                    facingTarget.GridCoordinates,
                    originalFacing,
                    out actionFacing);
            if (changedFacing)
                caster.Facing = actionFacing;

            var runner = new SkillGraphRunner();
            var result = SkillGraphExecutionState.Failed;
            try
            {
                result = await runner.Execute(skillContext);
            }
            finally
            {
                if (result != SkillGraphExecutionState.Completed && changedFacing)
                    caster.Facing = originalFacing;
            }

            if (result == SkillGraphExecutionState.Completed)
            {
                var amazonState = AmazonBattleState.For(controller);
                context.SpearHolderAlias = amazonState.IsSpearHeld(caster) ? casterAlias : "none";
                var spearCell = amazonState.GetSpearCell(caster);
                context.SpearCellAlias = context.Cells.FirstOrDefault(pair => ReferenceEquals(pair.Value, spearCell)).Key ?? "none";
                string decoyAlias = action.Parameters["decoyAlias"]?.ToString();
                var decoy = amazonState.GetDecoy(caster);
                if (!string.IsNullOrWhiteSpace(decoyAlias) && decoy != null)
                {
                    context.Units[decoyAlias] = decoy;
                    context.DecoyRemainingActions[decoyAlias] = amazonState.GetDecoyTurnsUntilExpiry(caster);
                }
                return GameplayStepResult.Pass(BattleAdapterName, action.Kind, $"Executed SkillGraph '{graphAlias}' on {casterAlias}.");
            }

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

            // Check if destination is blocked (e.g., by corpse)
            if (destCell.IsTaken && !destCell.CurrentUnits.Contains(unit))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Cell ({destCell.GridCoordinates.x},{destCell.GridCoordinates.y}) is blocked.");

            var oldCell = unit.CurrentCell;
            if (oldCell != null && FacingResolver.TryResolve(
                    oldCell.GridCoordinates,
                    destCell.GridCoordinates,
                    unit.Facing,
                    out var moveFacing))
            {
                unit.Facing = moveFacing;
            }

            var mono = unit as MonoBehaviour;
            if (mono != null)
                mono.transform.position = new Vector3(destCell.GridCoordinates.x, destCell.GridCoordinates.y, 0);

            // Commit occupancy only after every legality check succeeds.
            if (oldCell != null)
            {
                oldCell.CurrentUnits.Remove(unit);
                oldCell.IsTaken = oldCell.CurrentUnits.Count > 0;
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

            var maxMana = action.Parameters["maxMana"];
            if (maxMana != null) unit.MaxMana = maxMana.ToObject<float>();

            var mana = action.Parameters["mana"];
            if (mana != null) unit.Mana = mana.ToObject<float>();

            var playerNumber = action.Parameters["playerNumber"];
            if (playerNumber != null) unit.PlayerNumber = playerNumber.ToObject<int>();

            var isDowned = action.Parameters["isDowned"];
            if (isDowned != null) unit.IsDowned = isDowned.ToObject<bool>();

            var canReceiveHealing = action.Parameters["canReceiveHealing"];
            if (canReceiveHealing != null)
                unit.CanReceiveHealing = canReceiveHealing.ToObject<bool>();

            var speed = action.Parameters["speed"];
            if (speed != null)
                unit.Speed = speed.ToObject<float>();

            var initiative = action.Parameters["initiative"];
            if (initiative != null)
                unit.Initiative = initiative.ToObject<float>();

            if (mono != null)
            {
                var learnedSkills = new List<CharacterDefinition.LearnedSkill>();
                if (action.Parameters["learnedSkills"] is JArray learnedArray)
                {
                    foreach (var token in learnedArray.OfType<JObject>())
                    {
                        string skillId = token["skillId"]?.ToString();
                        if (string.IsNullOrWhiteSpace(skillId))
                            continue;
                        learnedSkills.Add(new CharacterDefinition.LearnedSkill
                        {
                            SkillId = skillId,
                            Level = token["level"]?.ToObject<int>() ?? 1,
                            SkillType = Enum.TryParse<Tactics.Roster.SkillType>(token["skillType"]?.ToString(), true, out var skillType)
                                ? skillType
                                : Tactics.Roster.SkillType.Active
                        });
                    }
                }
                else if (!string.IsNullOrWhiteSpace(action.Parameters["skillId"]?.ToString()))
                {
                    learnedSkills.Add(new CharacterDefinition.LearnedSkill
                    {
                        SkillId = action.Parameters["skillId"].ToString(),
                        Level = action.Parameters["skillLevel"]?.ToObject<int>() ?? 1,
                        SkillType = Tactics.Roster.SkillType.Active
                    });
                }

                if (learnedSkills.Count > 0)
                    mono.ApplyLearnedSkillLevels(learnedSkills);
            }

            string characterId = action.Parameters["characterId"]?.ToString();
            if (!string.IsNullOrWhiteSpace(characterId) && unit is MonoBehaviour monoBehaviour)
            {
                var link = monoBehaviour.GetComponent<RosterCharacterLink>() ?? monoBehaviour.gameObject.AddComponent<RosterCharacterLink>();
                link.CharacterId = characterId;
            }

            return GameplayStepResult.Pass(BattleAdapterName, action.Kind, $"Set state for {unitAlias}.");
        }

        private static async Task<GameplayStepResult> UseCarriedConsumable(
            GameplayRuntimeContext context,
            ExecutableScenarioAction action)
        {
            var controller = RequireBattleController(context, action.Kind);
            if (!EnsureBattleInitialized(controller))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "BattleController is not initialized.");

            string casterAlias = action.Parameters["casterAlias"]?.ToString();
            string targetAlias = action.Parameters["targetAlias"]?.ToString();
            if (string.IsNullOrWhiteSpace(casterAlias) || string.IsNullOrWhiteSpace(targetAlias))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "useCarriedConsumable requires casterAlias and targetAlias.");
            if (!context.Units.TryGetValue(casterAlias, out var caster))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Caster alias '{casterAlias}' not found.");
            if (!context.Units.TryGetValue(targetAlias, out var target) || target.CurrentCell == null)
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Target alias '{targetAlias}' is unavailable.");

            var ability = caster.GetBaseAbilities().OfType<ConsumableBattleAbility>().FirstOrDefault();
            if (ability == null)
            {
                string characterId = action.Parameters["characterId"]?.ToString();
                if (string.IsNullOrWhiteSpace(characterId) && caster is MonoBehaviour casterBehaviour)
                    characterId = casterBehaviour.GetComponent<RosterCharacterLink>()?.CharacterId;
                if (string.IsNullOrWhiteSpace(characterId))
                    return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "Caster has no roster character link.");

                var state = PlayerAdventureStateStore.LoadRepairAndSave();
                var character = state?.Roster?.FirstOrDefault(candidate => candidate?.Id == characterId);
                var instance = state?.ConsumableInstances?.FirstOrDefault(candidate =>
                    candidate?.InstanceId == character?.CarriedConsumableInstanceId);
                ability = ConsumableAbilityFactory.Create(caster, instance, characterId);
                if (ability == null || caster is not Unit concreteUnit)
                    return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "Failed to create the carried consumable ability.");

                concreteUnit.RegisterAbility(ability, controller);
            }

            var result = await ability.ExecuteForTestAsync(target.CurrentCell, controller);
            context.LastSkillResult = result;
            context.LastStepMessage = result.LastError;
            bool expectedSuccess = action.Parameters["expectSuccess"]?.ToObject<bool>() ?? true;
            bool succeeded = result.ExecutionState == SkillGraphExecutionState.Completed;
            if (succeeded == expectedSuccess)
            {
                string outcome = succeeded ? "used" : "rejected";
                return GameplayStepResult.Pass(
                    BattleAdapterName,
                    action.Kind,
                    $"Consumable '{ability.Definition.DisplayName}' was {outcome} for {casterAlias} -> {targetAlias}.");
            }

            return GameplayStepResult.Fail(
                BattleAdapterName,
                action.Kind,
                succeeded
                    ? "Consumable use unexpectedly succeeded."
                    : result.LastError ?? "Consumable execution failed.");
        }

        private static List<int> ReadPlayerNumbers(ExecutableScenarioAction action, string singularKey, string pluralKey)
        {
            if (action.Parameters[pluralKey] is JArray array)
                return array.Select(token => token.ToObject<int>()).ToList();

            if (action.Parameters[singularKey] != null)
                return new List<int> { action.Parameters[singularKey].ToObject<int>() };

            return new List<int>();
        }

        private static void ApplyRoguelikeWriteback(BattleController controller, GameResult result)
        {
            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            var units = controller.GetUnits();
            bool humanWon = result.Winners?.Any(player =>
                player != null && player.PlayerType == PlayerType.HumanPlayer) == true;
            if (humanWon)
            {
                var regenerationMethod = typeof(RoguelikeBattleReturnHandler).GetMethod(
                    "ApplyPostBattleRegeneration",
                    BindingFlags.NonPublic | BindingFlags.Static);
                regenerationMethod?.Invoke(null, new object[] { units });
            }
            var syncMethod = typeof(RoguelikeBattleReturnHandler).GetMethod("SyncPartyStateFromBattleUnits", BindingFlags.NonPublic | BindingFlags.Static);
            syncMethod?.Invoke(null, new object[] { units, state });
            var rewards = BattleRewardSystem.CalculateBattleRewards(
                result,
                controller.CurrentRound,
                units);
            BattleRewardSystem.ApplyRewards(state, rewards);
            PlayerAdventureStateStore.Save(state);
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
            string configAlias = action.Parameters["configAlias"]?.ToString() ?? buffName;
            BuffConfig config;

            if (!string.IsNullOrWhiteSpace(configPath))
            {
                config = GameAssetManager.Instance?.Load<BuffConfig>(configPath);
                if (config == null)
                    return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Failed to load BuffConfig from '{configPath}'.");
            }
            else
            {
                if (!context.RuntimeBuffConfigs.TryGetValue(configAlias, out config) || config == null)
                {
                    config = ScriptableObject.CreateInstance<BuffConfig>();
                    context.RuntimeBuffConfigs[configAlias] = config;
                }

                SetBuffConfigField(config, "_buffName", buffName);
                SetBuffConfigField(config, "_defaultDuration", duration);
                SetBuffConfigField(config, "_canAct", action.Parameters["canAct"]?.ToObject<bool>() ??
                    !string.Equals(action.Parameters["effectType"]?.ToString(), nameof(BuffEffectType.Stun), StringComparison.OrdinalIgnoreCase));
                SetBuffConfigEnum(config, "_effectType", action.Parameters["effectType"]?.ToString(), BuffEffectType.None);
                SetBuffConfigEnum(config, "_triggerTiming", action.Parameters["triggerTiming"]?.ToString(), BuffTriggerTiming.None);
                SetBuffConfigEnum(config, "_polarity", action.Parameters["polarity"]?.ToString(), BuffPolarity.Harmful);
                SetBuffConfigEnum(config, "_elementType", action.Parameters["elementType"]?.ToString(), ElementType.None);
                SetBuffConfigEnum(config, "_damageCategory", action.Parameters["damageCategory"]?.ToString(), DamageCategory.Magic);
                SetBuffConfigEnum(config, "_refreshStrategy", action.Parameters["refreshStrategy"]?.ToString(), BuffRefreshStrategy.AddDuration);
                SetBuffConfigField(config, "_damagePerTurn", action.Parameters["damagePerTurn"]?.ToObject<float>() ?? 0f);
                SetBuffConfigField(config, "_speedModifier", action.Parameters["speedModifier"]?.ToObject<float>() ?? 0f);
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

            var unitStatesBefore = context.Units
                .Where(pair => pair.Value != null)
                .GroupBy(pair => pair.Value.UnitID)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        var pair = group.First();
                        return (
                            Alias: pair.Key,
                            Position: pair.Value.CurrentCell?.GridCoordinates ?? default,
                            Health: pair.Value.Health);
                    });

            // Resolve target alias from context.Units (first enemy unit as default target)
            context.LastAiTargetAlias = null;
            string targetAlias = action.Parameters["targetAlias"]?.ToString();
            if (!string.IsNullOrWhiteSpace(targetAlias) && context.Units.TryGetValue(targetAlias, out var targetUnit))
            {
                context.LastAiTargetAlias = targetAlias;
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

                        var targetMatch = System.Text.RegularExpressions.Regex.Match(execResult.Message, @"Target=Unit_(\d+)");
                        if (targetMatch.Success &&
                            int.TryParse(targetMatch.Groups[1].Value, out int targetUnitId) &&
                            unitStatesBefore.TryGetValue(targetUnitId, out var targetBefore))
                        {
                            snapshot.TargetAlias = targetBefore.Alias;
                            snapshot.TargetUnitId = targetUnitId;
                            snapshot.TargetPositionBefore = targetBefore.Position;
                            snapshot.TargetHealthBefore = targetBefore.Health;
                            context.LastAiTargetAlias = targetBefore.Alias;

                            var observedTarget = context.Units.Values.FirstOrDefault(candidate =>
                                candidate != null && candidate.UnitID == targetUnitId);
                            if (observedTarget != null)
                            {
                                snapshot.TargetPositionAfter = observedTarget.CurrentCell?.GridCoordinates ?? default;
                                snapshot.TargetHealthAfter = observedTarget.Health;
                            }
                        }
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
                    {
                        string abilities = string.Join(", ", unit.GetBaseAbilities()
                            .Select(ability => $"{ability.DisplayName}:{ability.GetType().Name}"));
                        string executionFailure = decisionLog?.GetEntries()
                            .LastOrDefault(entry => entry.Message.StartsWith("AbilityUse failed:", StringComparison.Ordinal))
                            ?.Message;
                        snapshot.FailureReason =
                            $"AI selected '{snapshot.SelectedIntentType}' but produced no observable effect " +
                            $"(no move, no damage, no heal). Available abilities=[{abilities}]. " +
                            $"SelectedAbility={snapshot.SelectedAbilityName ?? "none"}; " +
                            $"ActionType={snapshot.SelectedActionType ?? "none"}; " +
                            $"Execution={executionFailure ?? "no explicit failure"}.";
                    }
                }

                // Store snapshots
                context.PreviousAiSnapshot = context.LastAiSnapshot;
                context.LastAiSnapshot = snapshot;
                context.LastAiTurnResult = BuildAiTurnResultSnapshot(decisionLog, snapshot);

                return GameplayStepResult.Pass(BattleAdapterName, action.Kind, $"Executed AI for {unitAlias} using brain '{brainAlias}'. Intent={snapshot.SelectedIntentType}, DidMove={snapshot.DidMove}, DidDamage={snapshot.DidDamageTarget}, WasNoOp={snapshot.WasNoOp}");
            }
            catch (Exception ex)
            {
                controller.BypassActiveUnitCheck = false;
                snapshot.FailureReason = $"AI execution exception: {ex.Message}";
                context.PreviousAiSnapshot = context.LastAiSnapshot;
                context.LastAiSnapshot = snapshot;
                context.LastAiTurnResult = new AiTurnResultSnapshot
                {
                    Succeeded = false,
                    FailureReason = snapshot.FailureReason
                };
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

        private static GameplayAssertionResult AssertUnitCanReceiveHealingEquals(
            GameplayRuntimeContext context,
            ExecutableScenarioAssertion assertion)
        {
            if (string.IsNullOrWhiteSpace(assertion.Target))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "unitCanReceiveHealingEquals requires a target unit alias.");
            if (!context.Units.TryGetValue(assertion.Target, out var unit))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Unit alias '{assertion.Target}' does not exist.");

            bool expected = assertion.Expected?.ToObject<bool>() ?? true;
            bool actual = unit.CanReceiveHealing;
            return actual == expected
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"{assertion.Target}.CanReceiveHealing={actual}")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected {assertion.Target}.CanReceiveHealing={expected}, actual={actual}.");
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

        private static AiTurnResultSnapshot BuildAiTurnResultSnapshot(AiDecisionLog decisionLog, AiExecutionSnapshot execution)
        {
            string formattedLog = decisionLog?.GetFormattedLog() ?? string.Empty;
            string destination = ReadCoordinate(formattedLog, "Destination");
            if (string.IsNullOrEmpty(destination))
                destination = $"{execution.ActorPositionAfter.x},{execution.ActorPositionAfter.y}";

            string targetPoint = ReadCoordinate(formattedLog, "TargetPoint");
            if (string.IsNullOrEmpty(targetPoint) && execution.TargetAlias != null)
                targetPoint = $"{execution.TargetPositionAfter.x},{execution.TargetPositionAfter.y}";

            int targetCount = ReadInteger(formattedLog, "TargetCount")
                ?? (execution.TargetAlias == null ? 0 : 1);
            bool usedFallback = ReadBoolean(formattedLog, "UsedFallback") ?? false;
            string patternStep = ReadText(formattedLog, "PatternStep");
            string abilityId = ReadText(formattedLog, "AbilityId");
            if (string.IsNullOrWhiteSpace(abilityId))
                abilityId = execution.SelectedAbilityName ?? string.Empty;

            return new AiTurnResultSnapshot
            {
                Succeeded = string.IsNullOrWhiteSpace(execution.FailureReason),
                AbilityId = abilityId,
                Destination = destination,
                TargetPoint = targetPoint ?? string.Empty,
                TargetCount = targetCount,
                UsedFallback = usedFallback,
                PatternStep = patternStep ?? string.Empty,
                FailureReason = execution.FailureReason ?? string.Empty
            };
        }

        private static string ReadCoordinate(string text, string fieldName)
        {
            var match = Regex.Match(
                text,
                $@"{Regex.Escape(fieldName)}\s*=\s*\(?\s*(-?\d+)\s*,\s*(-?\d+)\s*\)?",
                RegexOptions.IgnoreCase);
            return match.Success ? $"{match.Groups[1].Value},{match.Groups[2].Value}" : string.Empty;
        }

        private static int? ReadInteger(string text, string fieldName)
        {
            var match = Regex.Match(
                text,
                $@"{Regex.Escape(fieldName)}\s*=\s*(-?\d+)",
                RegexOptions.IgnoreCase);
            return match.Success && int.TryParse(match.Groups[1].Value, out int value) ? value : null;
        }

        private static bool? ReadBoolean(string text, string fieldName)
        {
            var match = Regex.Match(
                text,
                $@"{Regex.Escape(fieldName)}\s*=\s*(true|false)",
                RegexOptions.IgnoreCase);
            return match.Success && bool.TryParse(match.Groups[1].Value, out bool value) ? value : null;
        }

        private static string ReadText(string text, string fieldName)
        {
            var quoted = Regex.Match(
                text,
                $@"{Regex.Escape(fieldName)}\s*=\s*'([^']*)'",
                RegexOptions.IgnoreCase);
            if (quoted.Success)
                return quoted.Groups[1].Value.Trim();

            var plain = Regex.Match(
                text,
                $@"{Regex.Escape(fieldName)}\s*=\s*([^,;\r\n]+)",
                RegexOptions.IgnoreCase);
            return plain.Success ? plain.Groups[1].Value.Trim() : string.Empty;
        }

        private static GameplayAssertionResult AssertAiTurnStringField(
            GameplayRuntimeContext context,
            ExecutableScenarioAssertion assertion,
            string fieldLabel,
            Func<AiTurnResultSnapshot, string> selector)
        {
            var turnResult = context.LastAiTurnResult;
            if (turnResult == null)
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "No structured AI turn result recorded. Execute executeAI first.");

            string expected = assertion.Expected?.ToString() ?? string.Empty;
            string actual = selector(turnResult) ?? string.Empty;
            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"AI turn {fieldLabel} equals '{actual}'.")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected AI turn {fieldLabel} '{expected}', actual '{actual}'.");
        }

        private static GameplayAssertionResult AssertAiTurnBooleanField(
            GameplayRuntimeContext context,
            ExecutableScenarioAssertion assertion,
            string fieldLabel,
            Func<AiTurnResultSnapshot, bool> selector)
        {
            var turnResult = context.LastAiTurnResult;
            if (turnResult == null)
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "No structured AI turn result recorded. Execute executeAI first.");

            bool expected = assertion.Expected?.ToObject<bool>() ?? false;
            bool actual = selector(turnResult);
            return actual == expected
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"AI turn {fieldLabel} equals {actual}.")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected AI turn {fieldLabel} {expected}, actual {actual}.");
        }

        private static GameplayAssertionResult AssertAiTurnTargetCountEquals(
            GameplayRuntimeContext context,
            ExecutableScenarioAssertion assertion)
        {
            var turnResult = context.LastAiTurnResult;
            if (turnResult == null)
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "No structured AI turn result recorded. Execute executeAI first.");

            int expected = assertion.Expected?.ToObject<int>() ?? 0;
            return turnResult.TargetCount == expected
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"AI turn target count equals {turnResult.TargetCount}.")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected AI turn target count {expected}, actual {turnResult.TargetCount}.");
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

        private static GameplayStepResult SetUnitFacing(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            string unitAlias = action.Parameters?["unitAlias"]?.ToString();
            string facingValue = action.Parameters?["facing"]?.ToString();
            if (!TryGetUnit(context, unitAlias, out var unit, out var error))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, error);
            if (!Enum.TryParse<FacingDirection>(facingValue, true, out var facing))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Unknown facing '{facingValue}'.");

            unit.Facing = facing;
            return GameplayStepResult.Pass(BattleAdapterName, action.Kind, $"Set {unitAlias} facing to {facing}.");
        }

        private static GameplayStepResult InitializeInitiativeOrder(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            var units = ResolveUnitSequence(context, action.Parameters?["unitAliases"] as JArray);
            if (units.Count == 0)
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "initializeInitiativeOrder requires at least one unit.");

            context.InitiativeService = new BattleInitiativeService();
            context.InitiativeUnits.Clear();
            context.InitiativeUnits.AddRange(units);
            context.InitiativeService.StartRound(units);
            var gridController = ResolveGridController(context);
            if (gridController != null)
                BattleInitiativeService.Attach(gridController, context.InitiativeService);
            return GameplayStepResult.Pass(BattleAdapterName, action.Kind, "Initialized current-round initiative order.");
        }

        private static GameplayStepResult AdvanceInitiative(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            if (context.InitiativeService == null)
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "Initiative order has not been initialized.");

            var next = context.InitiativeService.TakeNext(context.InitiativeUnits);
            string alias = FindUnitAlias(context, next) ?? "none";
            return GameplayStepResult.Pass(BattleAdapterName, action.Kind, $"Advanced initiative to '{alias}'.");
        }

        private static GameplayStepResult TickUnitTurn(
            GameplayRuntimeContext context,
            ExecutableScenarioAction action,
            bool turnStart)
        {
            string unitAlias = action.Parameters?["unitAlias"]?.ToString();
            if (!TryGetUnit(context, unitAlias, out var unit, out var error))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, error);

            var gridController = ResolveGridController(context);
            if (turnStart)
                unit.OnTurnStart(gridController);
            else
                unit.OnTurnEnd(gridController);
            return GameplayStepResult.Pass(BattleAdapterName, action.Kind, $"Ticked {(turnStart ? "start" : "end")} of {unitAlias}'s turn.");
        }

        private static GameplayStepResult RegisterSummon(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            string ownerAlias = action.Parameters?["ownerAlias"]?.ToString();
            string summonAlias = action.Parameters?["summonAlias"]?.ToString();
            if (!TryGetUnit(context, ownerAlias, out var owner, out var ownerError))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, ownerError);
            if (!TryGetUnit(context, summonAlias, out var summon, out var summonError))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, summonError);

            var gridController = ResolveGridController(context);
            if (gridController == null)
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "registerSummon requires a battle or skill test grid.");

            string category = action.Parameters?["category"]?.ToString() ?? "Default";
            int maximumActive = action.Parameters?["maximumActive"]?.ToObject<int>() ?? 1;
            var registry = SummonRegistry.For(gridController);
            var replacements = registry.Register(owner, category, summon, maximumActive);
            foreach (var replacement in replacements)
                registry.Despawn(replacement);
            return GameplayStepResult.Pass(
                BattleAdapterName,
                action.Kind,
                $"Registered {summonAlias} in '{category}', replaced {replacements.Count} summon(s).");
        }

        private static GameplayStepResult BeginOrderedTargetSelection(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            int requiredCount = action.Parameters?["requiredCount"]?.ToObject<int>() ?? 1;
            context.OrderedTargetSelection = new OrderedTargetSelectionState(requiredCount);
            context.TargetMarkerOrder.Clear();
            return GameplayStepResult.Pass(BattleAdapterName, action.Kind, $"Started ordered selection for {requiredCount} target(s).");
        }

        private static GameplayStepResult SelectOrderedTarget(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            if (context.OrderedTargetSelection == null)
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "Ordered target selection has not started.");

            string targetAlias = action.Parameters?["targetAlias"]?.ToString();
            if (!TryGetUnit(context, targetAlias, out var target, out var error))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, error);
            if (!context.OrderedTargetSelection.TryAdd(target))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Could not add target '{targetAlias}'.");

            context.TargetMarkerOrder.Add(targetAlias);
            return GameplayStepResult.Pass(BattleAdapterName, action.Kind, $"Selected '{targetAlias}' at position {context.TargetMarkerOrder.Count}.");
        }

        private static GameplayStepResult UndoOrderedTargetSelection(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            if (context.OrderedTargetSelection == null || !context.OrderedTargetSelection.UndoLast())
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "No ordered target segment can be undone.");
            if (context.TargetMarkerOrder.Count > 0)
                context.TargetMarkerOrder.RemoveAt(context.TargetMarkerOrder.Count - 1);
            return GameplayStepResult.Pass(BattleAdapterName, action.Kind, "Removed the most recent ordered target.");
        }

        private static GameplayStepResult CommitOrderedTargetSelection(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            if (context.OrderedTargetSelection == null || context.OrderedTargetSelection.Commit().Count == 0)
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "Ordered target selection is not ready to commit.");
            return GameplayStepResult.Pass(BattleAdapterName, action.Kind, "Committed ordered target selection.");
        }

        private static GameplayStepResult CancelOrderedTargetSelection(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            if (context.OrderedTargetSelection == null)
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "Ordered target selection has not started.");
            context.OrderedTargetSelection.Cancel();
            context.TargetMarkerOrder.Clear();
            return GameplayStepResult.Pass(BattleAdapterName, action.Kind, "Cancelled ordered target selection.");
        }

        private static GameplayAssertionResult AssertUnitFacingEquals(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (!TryGetUnit(context, assertion.Target, out var unit, out var error))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, error);
            string expected = assertion.Expected?.ToString();
            string actual = unit.Facing.ToString();
            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"{assertion.Target}.Facing={actual}")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected {assertion.Target}.Facing={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertCurrentRoundOrderEquals(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (context.InitiativeService == null)
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "Initiative order has not been initialized.");
            var actual = context.InitiativeService.GetCurrentRoundOrder()
                .Select(unit => FindUnitAlias(context, unit) ?? $"Unit_{unit.UnitID}")
                .ToList();
            return AssertStringSequence(assertion, actual, "current-round order");
        }

        private static GameplayAssertionResult AssertUnitStatusValue(
            GameplayRuntimeContext context,
            ExecutableScenarioAssertion assertion,
            bool stacks)
        {
            if (!TryGetUnit(context, assertion.Target, out var unit, out var error))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, error);
            string buffName = assertion.Parameters?["buffName"]?.ToString();
            var buff = unit.GetActiveBuffs().FirstOrDefault(candidate =>
                string.Equals(candidate.BuffName, buffName, StringComparison.OrdinalIgnoreCase));
            if (buff == null)
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Buff '{buffName}' was not found on '{assertion.Target}'.");

            int expected = assertion.Expected?.ToObject<int>() ?? 0;
            int actual = stacks ? buff.StackCount : buff.RemainingTurns;
            return actual == expected
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"{buffName} {(stacks ? "stacks" : "remaining actions")}={actual}")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected {buffName} {(stacks ? "stacks" : "remaining actions")}={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertSummonOrderEquals(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (!TryGetUnit(context, assertion.Target, out var owner, out var error))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, error);
            var gridController = ResolveGridController(context);
            if (gridController == null)
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "No battle or skill test grid is available.");

            string category = assertion.Parameters?["category"]?.ToString() ?? "Default";
            var actual = SummonRegistry.For(gridController).GetOrdered(owner, category)
                .Select(unit => FindUnitAlias(context, unit) ?? $"Unit_{unit.UnitID}")
                .ToList();
            return AssertStringSequence(assertion, actual, $"summon order for {assertion.Target}/{category}");
        }

        private static GameplayAssertionResult AssertSummonCategoryEquals(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (!TryGetUnit(context, assertion.Target, out var summon, out var error))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, error);
            var gridController = ResolveGridController(context);
            string actual = gridController == null ? null : SummonRegistry.For(gridController).GetCategory(summon);
            string expected = assertion.Expected?.ToString();
            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"{assertion.Target}.SummonCategory={actual}")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected summon category '{expected}', actual='{actual ?? "<none>"}'.");
        }

        private static GameplayAssertionResult AssertAbilityAvailabilityEquals(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            var ability = ResolveAbility(context, assertion);
            if (ability == null)
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "Ability could not be resolved.");
            string actual = AbilityAvailabilityResolver.Resolve(ability, ResolveGridController(context)).State.ToString();
            string expected = assertion.Expected?.ToString();
            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"Ability availability={actual}")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected ability availability={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertAbilityAvailabilityReasonEquals(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            var ability = ResolveAbility(context, assertion);
            if (ability == null)
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "Ability could not be resolved.");
            string actual = AbilityAvailabilityResolver.Resolve(ability, ResolveGridController(context)).Reason;
            string expected = assertion.Expected?.ToString() ?? string.Empty;
            return string.Equals(actual, expected, StringComparison.Ordinal)
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"Ability reason='{actual}'")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected ability reason='{expected}', actual='{actual}'.");
        }

        private static GameplayAssertionResult AssertActualSkillLevelEquals(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (!TryGetUnit(context, assertion.Target, out var unit, out var error))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, error);
            if (unit is not Unit concreteUnit)
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Unit '{assertion.Target}' does not expose learned skill levels.");
            string skillId = assertion.Parameters?["skillId"]?.ToString();
            int actual = concreteUnit.GetLearnedSkillLevel(skillId);
            int expected = assertion.Expected?.ToObject<int>() ?? 0;
            return actual == expected
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"{skillId} actual level={actual}")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected {skillId} actual level={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertUnitAbilityListEquals(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (!TryGetUnit(context, assertion.Target, out var unit, out var error))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, error);
            var actual = unit.GetBaseAbilities().Where(ability => ability != null).Select(ability => ability.DisplayName).ToList();
            return AssertStringSequence(assertion, actual, $"ability list for {assertion.Target}");
        }

        private static GameplayAssertionResult AssertOrderedTargetSelectionEquals(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (context.OrderedTargetSelection == null)
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "Ordered target selection has not started.");
            var actual = context.OrderedTargetSelection.Targets
                .Select(unit => FindUnitAlias(context, unit) ?? $"Unit_{unit.UnitID}")
                .ToList();
            return AssertStringSequence(assertion, actual, "ordered target selection");
        }

        private static GameplayAssertionResult AssertSelectionStageEquals(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            string actual = context.OrderedTargetSelection?.Stage.ToString();
            string expected = assertion.Expected?.ToString();
            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"Selection stage={actual}")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected selection stage={expected}, actual={actual ?? "<none>"}.");
        }

        private static GameplayAssertionResult AssertDecoyRemainingActionsEquals(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (!context.DecoyRemainingActions.TryGetValue(assertion.Target ?? string.Empty, out int actual))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"No decoy lifecycle observation exists for '{assertion.Target}'.");
            int expected = assertion.Expected?.ToObject<int>() ?? 0;
            return actual == expected
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"{assertion.Target}.RemainingActions={actual}")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected {assertion.Target}.RemainingActions={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertObservedAlias(
            GameplayRuntimeContext context,
            ExecutableScenarioAssertion assertion,
            string actual,
            string label)
        {
            string expected = assertion.Expected?.ToString();
            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"{label}='{actual}'")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected {label}='{expected}', actual='{actual ?? "<none>"}'.");
        }

        private static GameplayAssertionResult AssertStringSequence(
            ExecutableScenarioAssertion assertion,
            IReadOnlyList<string> actual,
            string label)
        {
            var expected = assertion.Expected is JArray array
                ? array.Values<string>().ToList()
                : new List<string>();
            bool equal = actual.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase);
            return equal
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"{label}=[{string.Join(", ", actual)}]")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected {label}=[{string.Join(", ", expected)}], actual=[{string.Join(", ", actual)}].");
        }

        private static IAbility ResolveAbility(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            string abilityAlias = assertion.Parameters?["abilityAlias"]?.ToString();
            if (string.IsNullOrWhiteSpace(abilityAlias))
                abilityAlias = assertion.Target;
            if (!string.IsNullOrWhiteSpace(abilityAlias) && context.SkillAbilities.TryGetValue(abilityAlias, out var aliased))
                return aliased;

            string unitAlias = assertion.Parameters?["unitAlias"]?.ToString();
            string abilityName = assertion.Parameters?["abilityName"]?.ToString();
            if (TryGetUnit(context, unitAlias, out var unit, out _) && !string.IsNullOrWhiteSpace(abilityName))
            {
                return unit.GetBaseAbilities().FirstOrDefault(ability =>
                    string.Equals(ability.DisplayName, abilityName, StringComparison.OrdinalIgnoreCase));
            }

            return null;
        }

        private static List<IUnit> ResolveUnitSequence(GameplayRuntimeContext context, JArray aliases)
        {
            if (aliases == null)
                return context.Units.Values.Where(unit => unit != null).Distinct().ToList();
            var units = new List<IUnit>();
            foreach (string alias in aliases.Values<string>())
            {
                if (context.Units.TryGetValue(alias, out var unit) && unit != null && !units.Contains(unit))
                    units.Add(unit);
            }
            return units;
        }

        private static bool TryGetUnit(
            GameplayRuntimeContext context,
            string alias,
            out IUnit unit,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                unit = null;
                error = "A unit alias is required.";
                return false;
            }
            if (!context.Units.TryGetValue(alias, out unit) || unit == null)
            {
                error = $"Unit alias '{alias}' not found.";
                return false;
            }
            error = null;
            return true;
        }

        private static string FindUnitAlias(GameplayRuntimeContext context, IUnit unit)
        {
            return unit == null
                ? null
                : context.Units.FirstOrDefault(pair => ReferenceEquals(pair.Value, unit)).Key;
        }

        private static IGridController ResolveGridController(GameplayRuntimeContext context)
        {
            return context.BattleController ?? (IGridController)context.SkillWorld?.GridController;
        }

        private static void SetBuffConfigField<T>(BuffConfig config, string fieldName, T value)
        {
            typeof(BuffConfig).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(config, value);
        }

        private static void SetBuffConfigEnum<TEnum>(BuffConfig config, string fieldName, string rawValue, TEnum fallback)
            where TEnum : struct, Enum
        {
            var value = Enum.TryParse<TEnum>(rawValue, true, out var parsed) ? parsed : fallback;
            SetBuffConfigField(config, fieldName, value);
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

            // Also create a Corpse interactable on the cell
            if (unit.CurrentCell != null)
            {
                var corpse = new Corpse();
                unit.CurrentCell.AddInteractable(corpse);
            }

            return GameplayStepResult.Pass(BattleAdapterName, action.Kind, $"Unit '{unitAlias}' marked as corpse with Corpse interactable.");
        }

        private static GameplayStepResult KillUnit(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            string unitAlias = action.Parameters?["unitAlias"]?.ToString();
            if (string.IsNullOrWhiteSpace(unitAlias))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "killUnit requires a unitAlias parameter.");

            if (!context.Units.TryGetValue(unitAlias, out var unit))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Unit alias '{unitAlias}' does not exist.");

            unit.ModifyHealth(-unit.Health - 1, null);
            SummonRegistry.For(ResolveGridController(context))?.HandleUnitDeath(unit);
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

        private static GameplayStepResult SpawnInteractableCorpse(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            string cellAlias = action.Parameters?["cellAlias"]?.ToString();
            if (string.IsNullOrWhiteSpace(cellAlias))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "spawnInteractableCorpse requires a cellAlias parameter.");

            if (!context.Cells.TryGetValue(cellAlias, out var cell))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Cell alias '{cellAlias}' does not exist.");

            if (context.InteractableCorpsesByCell.TryGetValue(cellAlias, out var existing)
                && existing != null && !existing.IsDestroyed)
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"Interactable corpse already exists at '{cellAlias}'.");

            var corpseObject = new GameObject($"GameplayCorpse_{cellAlias}");
            var corpse = corpseObject.AddComponent<Corpse>();
            corpse.CurrentCell = cell;
            cell.AddInteractable(corpse);
            corpseObject.transform.position = cell.WorldPosition.ToVector3();
            context.InteractableCorpsesByCell[cellAlias] = corpse;
            return GameplayStepResult.Pass(BattleAdapterName, action.Kind, $"Interactable corpse spawned at '{cellAlias}'.");
        }

        private static GameplayStepResult ConsumeInteractableCorpseAt(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            string cellAlias = action.Parameters?["cellAlias"]?.ToString();
            if (string.IsNullOrWhiteSpace(cellAlias))
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, "consumeInteractableCorpseAt requires a cellAlias parameter.");

            if (!context.InteractableCorpsesByCell.TryGetValue(cellAlias, out var corpse)
                || corpse == null || corpse.IsDestroyed)
                return GameplayStepResult.Fail(BattleAdapterName, action.Kind, $"No interactable corpse found at '{cellAlias}'.");

            corpse.Consume();
            context.InteractableCorpsesByCell.Remove(cellAlias);
            return GameplayStepResult.Pass(BattleAdapterName, action.Kind, $"Interactable corpse consumed at '{cellAlias}'.");
        }

        private static GameplayAssertionResult AssertInteractableCorpseExistsAt(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (string.IsNullOrWhiteSpace(assertion.Target))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "interactableCorpseExistsAt requires a target cell alias.");

            bool expected = assertion.Expected?.ToObject<bool>() ?? true;
            bool actual = context.InteractableCorpsesByCell.TryGetValue(assertion.Target, out var corpse)
                && corpse != null && !corpse.IsDestroyed;
            return actual == expected
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"InteractableCorpse at '{assertion.Target}' exists={actual}")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected InteractableCorpse at '{assertion.Target}' exists={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertCellOccupiedByInteractable(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            if (string.IsNullOrWhiteSpace(assertion.Target))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, "cellOccupiedByInteractable requires a target cell alias.");

            if (!context.Cells.TryGetValue(assertion.Target, out var cell))
                return GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Cell alias '{assertion.Target}' does not exist.");

            bool expected = assertion.Expected?.ToObject<bool>() ?? true;
            bool actual = context.InteractableCorpsesByCell.TryGetValue(assertion.Target, out var corpse)
                && corpse != null && !corpse.IsDestroyed && cell.IsTaken;
            return actual == expected
                ? GameplayAssertionResult.Pass(BattleAdapterName, assertion.Kind, $"Cell '{assertion.Target}' occupied by interactable={actual}")
                : GameplayAssertionResult.Fail(BattleAdapterName, assertion.Kind, $"Expected Cell '{assertion.Target}' occupied by interactable={expected}, actual={actual}.");
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
