using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Tactics.Roguelike;
using Tactics.RoguelikeMap;
using Tactics.RoguelikeMap.Events;
using Tactics.RoguelikeMap.Interaction;
using Tactics.RoguelikeMap.Economy;
using Tactics.Equipment;
using Tactics.Consumables;
using Tactics.AssetPipeline;
using Tactics.Common.Battle;
using Tactics.Roster;
using UnityEngine;

namespace Tactics.Common.Testing.Gameplay
{
    public sealed class MapGameplayStepAdapter : IGameplayStepAdapter
    {
        private const string MapAdapterName = "Map";

        public string AdapterName => MapAdapterName;

        public bool CanExecute(ExecutableScenarioAction action)
        {
            return action.Kind is "loadRoguelikeMap"
                or "loadPureRunMap"
                or "setRunSeed"
                or "enterNode"
                or "triggerEvent"
                or "completeNode"
                or "setAdventureGold"
                or "setRosterCharacterState"
                or "addInventoryItem"
                or "addConsumableInstance"
                or "equipInventoryEquipmentToRosterCharacter"
                or "carryConsumableToRosterCharacter"
                or "unloadRosterCharacterConsumable"
                or "applyRestSiteResult"
                or "buyShopEquipment"
                or "buyShopGood"
                or "applyEventResult";
        }

        public async Task<GameplayStepResult> ExecuteAsync(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            try
            {
                switch (action.Kind)
                {
                    case "setRunSeed":
                        return SetRunSeed(context, action);
                    case "loadRoguelikeMap":
                        return LoadRoguelikeMap(context, action);
                    case "loadPureRunMap":
                        return LoadPureRunMap(context, action);
                    case "enterNode":
                        return EnterNode(context, action);
                    case "triggerEvent":
                        return await TriggerEvent(context, action);
                    case "completeNode":
                        return CompleteNode(context, action);
                    case "setAdventureGold":
                        return SetAdventureGold(context, action);
                    case "setRosterCharacterState":
                        return SetRosterCharacterState(context, action);
                    case "addInventoryItem":
                        return AddInventoryItem(context, action);
                    case "addConsumableInstance":
                        return AddConsumableInstance(context, action);
                    case "equipInventoryEquipmentToRosterCharacter":
                        return EquipInventoryEquipmentToRosterCharacter(context, action);
                    case "carryConsumableToRosterCharacter":
                        return CarryConsumableToRosterCharacter(context, action);
                    case "unloadRosterCharacterConsumable":
                        return UnloadRosterCharacterConsumable(context, action);
                    case "applyRestSiteResult":
                        return ApplyRestSiteResult(context, action);
                    case "buyShopEquipment":
                        return BuyShopEquipment(context, action);
                    case "buyShopGood":
                        return BuyShopGood(context, action);
                    case "applyEventResult":
                        return ApplyEventResult(context, action);
                    default:
                        return GameplayStepResult.Fail(MapAdapterName, action.Kind, $"Unsupported Map action '{action.Kind}'.");
                }
            }
            catch (Exception ex)
            {
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, ex.Message);
            }
        }

        public bool CanAssert(ExecutableScenarioAssertion assertion)
        {
            return assertion.Kind is "currentNodeEquals"
                or "mapIsActive"
                or "visitedNodeCountEquals"
                or "battleVictoryCountEquals"
                or "nodeTypeEquals"
                or "nodeIsReachable"
                or "nodeIsVisited"
                or "runGoldEquals"
                or "rosterCharacterHpEquals"
                or "rosterCharacterMpEquals"
                or "rosterCharacterDeadEquals"
                or "rosterCharacterExperienceEquals"
                or "rosterCharacterLevelEquals"
                or "rosterCharacterHasSkillId"
                or "rosterCharacterSkillLevelEquals"
                or "pureRunSkillChoiceContains"
                or "pureRunSkillChoicesAreMixed"
                or "rosterCharacterEquipmentEquals"
                or "rosterCharacterTotalAttributeEquals"
                or "runtimeRosterCharacterHasPendingBuff"
                or "rosterCharacterHasPendingBuff"
                or "rosterCharacterPendingBuffHasIcon"
                or "inventoryContains"
                or "consumableCountEquals"
                or "rosterCharacterCarriedConsumableEquals"
                or "backpackConsumableCountEquals"
                or "consumableInstanceExists"
                or "shopGoodCountEquals"
                or "shopConsumableCountAtLeast"
                or "shopConsumableIdsUnique";
        }

        public Task<GameplayAssertionResult> AssertAsync(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            try
            {
                GameplayAssertionResult result = assertion.Kind switch
                {
                    "currentNodeEquals" => AssertCurrentNodeEquals(context, assertion),
                    "mapIsActive" => AssertMapIsActive(context, assertion),
                    "visitedNodeCountEquals" => AssertVisitedNodeCountEquals(context, assertion),
                    "battleVictoryCountEquals" => AssertBattleVictoryCountEquals(assertion),
                    "nodeTypeEquals" => AssertNodeTypeEquals(context, assertion),
                    "nodeIsReachable" => AssertNodeIsReachable(context, assertion),
                    "nodeIsVisited" => AssertNodeIsVisited(context, assertion),
                    "runGoldEquals" => AssertRunGoldEquals(assertion),
                    "rosterCharacterHpEquals" => AssertRosterCharacterHpEquals(assertion),
                    "rosterCharacterMpEquals" => AssertRosterCharacterMpEquals(assertion),
                    "rosterCharacterDeadEquals" => AssertRosterCharacterDeadEquals(assertion),
                    "rosterCharacterExperienceEquals" => AssertRosterCharacterExperienceEquals(assertion),
                    "rosterCharacterLevelEquals" => AssertRosterCharacterLevelEquals(assertion),
                    "rosterCharacterHasSkillId" => AssertRosterCharacterHasSkillId(assertion),
                    "rosterCharacterSkillLevelEquals" => AssertRosterCharacterSkillLevelEquals(assertion),
                    "pureRunSkillChoiceContains" => AssertPureRunSkillChoiceContains(assertion),
                    "pureRunSkillChoicesAreMixed" => AssertPureRunSkillChoicesAreMixed(assertion),
                    "rosterCharacterEquipmentEquals" => AssertRosterCharacterEquipmentEquals(assertion),
                    "rosterCharacterTotalAttributeEquals" => AssertRosterCharacterTotalAttributeEquals(assertion),
                    "runtimeRosterCharacterHasPendingBuff" => AssertRuntimeRosterCharacterHasPendingBuff(context, assertion),
                    "rosterCharacterHasPendingBuff" => AssertRosterCharacterHasPendingBuff(context, assertion),
                    "rosterCharacterPendingBuffHasIcon" => AssertRosterCharacterPendingBuffHasIcon(assertion),
                    "inventoryContains" => AssertInventoryContains(assertion),
                    "consumableCountEquals" => AssertConsumableCountEquals(assertion),
                    "rosterCharacterCarriedConsumableEquals" => AssertRosterCharacterCarriedConsumableEquals(assertion),
                    "backpackConsumableCountEquals" => AssertBackpackConsumableCountEquals(assertion),
                    "consumableInstanceExists" => AssertConsumableInstanceExists(assertion),
                    "shopGoodCountEquals" => AssertShopGoodCountEquals(context, assertion),
                    "shopConsumableCountAtLeast" => AssertShopConsumableCountAtLeast(context, assertion),
                    "shopConsumableIdsUnique" => AssertShopConsumableIdsUnique(context, assertion),
                    _ => GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Unsupported Map assertion '{assertion.Kind}'.")
                };

                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                return Task.FromResult(GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, ex.Message));
            }
        }

        public ProbeSnapshot CaptureProbe(GameplayRuntimeContext context, GameplayProbeRequest request)
        {
            var data = new JObject();

            data["hasActiveRun"] = RoguelikeMapRuntimeState.HasActiveRun;
            data["currentNodeId"] = RoguelikeMapRuntimeState.CurrentNodeId;
            data["visitedNodeCount"] = RoguelikeMapRuntimeState.VisitedPathNodeIds?.Count ?? 0;
            data["runSeed"] = context.RunSeed;
            data["strictAsset"] = context.StrictAsset;

            if (RoguelikeMapRuntimeState.CurrentMap != null)
            {
                var currentNode = RoguelikeMapRuntimeState.CurrentMap.GetNode(RoguelikeMapRuntimeState.CurrentNodeId);
                if (currentNode != null)
                {
                    data["currentNodeType"] = currentNode.nodeType.ToString();
                    data["currentNodeReachable"] = currentNode.IsReachable;
                    data["currentNodeVisited"] = currentNode.VisitState == NodeVisitState.Visited;
                }
            }

            return new ProbeSnapshot
            {
                Adapter = MapAdapterName,
                Kind = request.Kind,
                Target = request.Target,
                Data = data
            };
        }

        private static GameplayStepResult SetRunSeed(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            if (!action.Parameters.TryGetValue("seed", out var seedToken) || seedToken.Type != JTokenType.Integer)
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, "setRunSeed requires an integer seed.", "Setup");

            int seed = seedToken.ToObject<int>();
            context.RunSeed = seed;
            UnityEngine.Random.InitState(seed);
            return GameplayStepResult.Pass(MapAdapterName, action.Kind, $"Set run seed to {seed}.");
        }

        private static GameplayStepResult LoadRoguelikeMap(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            string mapConfigPath = action.Parameters["mapConfigPath"]?.ToString();
            if (string.IsNullOrWhiteSpace(mapConfigPath))
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, "loadRoguelikeMap requires mapConfigPath.", "Setup");

            bool strictAsset = action.Parameters["strictAsset"]?.ToObject<bool>() ?? context.StrictAsset;
            context.StrictAsset = strictAsset;

            // Load map config from GameAssetManager
            var mgr = GameAssetManager.Instance;
            var mapConfig = mgr?.Load<RoguelikeMapConfig>(mapConfigPath);
            if (!strictAsset && mapConfig == null && !string.Equals(mapConfigPath, "Assets/Tactics/RoguelikeMap/MapConfigs/DarkForestPrototypeConfig.asset", StringComparison.Ordinal))
            {
                mapConfig = mgr?.Load<RoguelikeMapConfig>("Assets/Tactics/RoguelikeMap/MapConfigs/DarkForestPrototypeConfig.asset");
            }

            if (!strictAsset && mapConfig == null && !string.Equals(mapConfigPath, "Assets/Tactics/RoguelikeMap/MapConfigs/DefaultRogueLikeMapConfig.asset", StringComparison.Ordinal))
            {
                mapConfig = mgr?.Load<RoguelikeMapConfig>("Assets/Tactics/RoguelikeMap/MapConfigs/DefaultRogueLikeMapConfig.asset");
            }

            if (mapConfig == null)
            {
                if (strictAsset)
                    return GameplayStepResult.Fail(MapAdapterName, action.Kind, $"Map config asset '{mapConfigPath}' was not found and strictAsset is enabled.", "Asset");

                // Final fallback for PlayMode tests: create a tiny in-memory map instead of failing on asset lookup.
                var fallbackMap = CreateFallbackTestMap();
                RoguelikeMapRuntimeState.AttachMap(fallbackMap);
                context.RoguelikeMap = fallbackMap;
                return GameplayStepResult.Pass(MapAdapterName, action.Kind, $"Loaded fallback in-memory map with {fallbackMap.nodes?.Count ?? 0} nodes.");
            }

            // Generate map from config
            var map = RoguelikeMapGenerator.GetMap(mapConfig);
            if (map == null)
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, "Failed to generate map from config.", "Asset");

            // Attach map to runtime state
            RoguelikeMapRuntimeState.AttachMap(map);
            context.RoguelikeMap = map;

            return GameplayStepResult.Pass(MapAdapterName, action.Kind, $"Loaded map with {map.nodes?.Count ?? 0} nodes.");
        }

        private static GameplayStepResult LoadPureRunMap(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            string mapConfigPath = action.Parameters["mapConfigPath"]?.ToString();
            if (string.IsNullOrWhiteSpace(mapConfigPath))
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, "loadPureRunMap requires mapConfigPath.", "Setup");

            var mapConfig = GameAssetManager.Instance?.Load<RoguelikeMapConfig>(mapConfigPath);
            if (mapConfig == null)
            {
                bool strictAsset = action.Parameters["strictAsset"]?.ToObject<bool>() ?? false;
                if (strictAsset)
                    return GameplayStepResult.Fail(MapAdapterName, action.Kind, $"Map config asset '{mapConfigPath}' was not found.", "Asset");

                mapConfig = ScriptableObject.CreateInstance<RoguelikeMapConfig>();
                mapConfig.name = "PureRunGameplayTestFallback";
            }

            int seed = context.RunSeed ?? 0;
            var map = RoguelikeMapGenerator.GetPureRunMap(mapConfig, seed);
            var state = PlayerAdventureStateStore.CreatePureRunState(seed);
            PureRunSessionStore.StartNew(state, map);
            context.RoguelikeMap = map;
            context.CurrentAdventureState = state;
            context.CurrentNodeId = map.currentNodeId;
            return GameplayStepResult.Pass(MapAdapterName, action.Kind, $"Loaded Pure Run map with seed {seed}.");
        }

        private static global::Tactics.RoguelikeMap.RoguelikeMap CreateFallbackTestMap()
        {
            var start = new RoguelikeMapNode("start_node", RoguelikeNodeType.Start, "Start", new Vector2(0, 0))
            {
                Visibility = NodeVisibility.Revealed,
                VisitState = NodeVisitState.Unvisited,
                IsReachable = true
            };

            var battle = new RoguelikeMapNode("battle_node_1", RoguelikeNodeType.MinorEnemy, "Battle", new Vector2(1, 0))
            {
                Visibility = NodeVisibility.Revealed,
                VisitState = NodeVisitState.Unvisited,
                IsReachable = true
            };

            var mystery = new RoguelikeMapNode("mystery_node_1", RoguelikeNodeType.Mystery, "Mystery", new Vector2(2, 0))
            {
                Visibility = NodeVisibility.Revealed,
                VisitState = NodeVisitState.Unvisited,
                IsReachable = true,
                eventId = "cursed_chest_001"
            };

            start.AddOutgoing(battle.nodeId);
            battle.AddIncoming(start.nodeId);
            battle.AddOutgoing(mystery.nodeId);
            mystery.AddIncoming(battle.nodeId);

            return new global::Tactics.RoguelikeMap.RoguelikeMap(
                "FallbackTestMap",
                null,
                new List<RoguelikeMapNode> { start, battle, mystery },
                new HashSet<string>());
        }

        private static GameplayStepResult EnterNode(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            string nodeId = action.Parameters["nodeId"]?.ToString();
            if (string.IsNullOrWhiteSpace(nodeId))
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, "enterNode requires nodeId.");

            var map = context.RoguelikeMap ?? RoguelikeMapRuntimeState.CurrentMap;
            if (map == null)
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, "No active map. Call loadRoguelikeMap first.");

            var node = map.GetNode(nodeId);
            if (node == null)
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, $"Node '{nodeId}' not found in map.");

            if (!node.IsReachable)
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, $"Node '{nodeId}' is not reachable.");

            // Update runtime state
            RoguelikeMapRuntimeState.AttachMap(map, nodeId);
            context.CurrentNodeId = nodeId;

            return GameplayStepResult.Pass(MapAdapterName, action.Kind, $"Entered node '{nodeId}' ({node.nodeType}).");
        }

        private static async Task<GameplayStepResult> TriggerEvent(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            string eventId = action.Parameters["eventId"]?.ToString();
            if (string.IsNullOrWhiteSpace(eventId))
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, "triggerEvent requires eventId.");

            var eventManager = EventManager.Instance;
            if (eventManager == null)
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, "EventManager.Instance is not available.");

            var evt = eventManager.GetEvent(eventId);
            if (evt == null)
                evt = LoadFallbackEventAsset(eventId);
            if (evt == null)
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, $"Event '{eventId}' not found.");

            // Store event in context for assertions
            context.CurrentEvent = evt;
            context.EventCompleted = false;

            return GameplayStepResult.Pass(MapAdapterName, action.Kind, $"Triggered event '{eventId}'.");
        }

        private static RoguelikeEvent LoadFallbackEventAsset(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId))
                return null;

            string[] candidatePaths =
            {
                $"Assets/Tactics/GameData/Events/DarkForest/{eventId}.json"
            };

            foreach (var path in candidatePaths)
            {
                try
                {
                    var textAsset = GameAssetManager.Instance?.Load<TextAsset>(path);
                    if (textAsset == null)
                    {
                        if (File.Exists(path))
                        {
                            var json = File.ReadAllText(path);
                            var fallbackEvent = RoguelikeEvent.FromJson(json);
                            if (fallbackEvent != null)
                                return fallbackEvent;
                        }
                        continue;
                    }

                    var evt = RoguelikeEvent.FromJson(textAsset.text);
                    GameAssetManager.Instance.Release(path);
                    if (evt != null)
                        return evt;
                }
                catch
                {
                    // ignore fallback load errors and continue trying
                }
            }

            return null;
        }

        private static GameplayStepResult CompleteNode(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            string nodeId = action.Parameters["nodeId"]?.ToString() ?? context.CurrentNodeId;
            if (string.IsNullOrWhiteSpace(nodeId))
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, "completeNode requires nodeId or current node.");

            var map = context.RoguelikeMap ?? RoguelikeMapRuntimeState.CurrentMap;
            if (map == null)
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, "No active map.");

            var node = map.GetNode(nodeId);
            if (node == null)
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, $"Node '{nodeId}' not found.");

            RoguelikeMapRuntimeState.CommitNodeProgress(
                nodeId,
                global::Tactics.RoguelikeMap.RoguelikeMap.IsBattleNode(node));
            context.CurrentNodeId = nodeId;
            PureRunSessionStore.SaveMap(map);

            context.EventCompleted = true;

            return GameplayStepResult.Pass(MapAdapterName, action.Kind, $"Completed node '{nodeId}'.");
        }

        private static GameplayStepResult SetAdventureGold(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            int amount = action.Parameters["amount"]?.ToObject<int>() ?? 0;
            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            state.Gold = amount;
            RunGoldManager.Instance.SyncFromState(state);
            PlayerAdventureStateStore.Save(state);
            context.CurrentAdventureState = state;
            return GameplayStepResult.Pass(MapAdapterName, action.Kind, $"Set adventure gold to {amount}.");
        }

        private static GameplayStepResult SetRosterCharacterState(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            string characterId = action.Parameters["characterId"]?.ToString();
            if (string.IsNullOrWhiteSpace(characterId))
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, "setRosterCharacterState requires characterId.");

            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            var character = state?.Roster?.FirstOrDefault(c => c.Id == characterId);
            if (character == null)
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, $"Character '{characterId}' not found.");

            if (action.Parameters.TryGetValue("currentHp", out var currentHpToken))
                character.CurrentHp = currentHpToken.ToObject<int>();

            if (action.Parameters.TryGetValue("currentMp", out var currentMpToken))
                character.CurrentMp = currentMpToken.ToObject<int>();

            if (action.Parameters.TryGetValue("experience", out var experienceToken))
                character.Experience = experienceToken.ToObject<int>();

            if (action.Parameters.TryGetValue("level", out var levelToken))
                character.Level = levelToken.ToObject<int>();

            if (action.Parameters.TryGetValue("skillId", out var skillIdToken))
            {
                string skillId = skillIdToken.ToString();
                if (string.IsNullOrWhiteSpace(skillId))
                    return GameplayStepResult.Fail(MapAdapterName, action.Kind, "setRosterCharacterState skillId cannot be empty.");

                character.LearnedSkills ??= new List<CharacterDefinition.LearnedSkill>();
                var learnedSkill = character.LearnedSkills.FirstOrDefault(skill =>
                    string.Equals(skill?.SkillId, skillId, StringComparison.OrdinalIgnoreCase));
                if (learnedSkill == null)
                {
                    learnedSkill = new CharacterDefinition.LearnedSkill
                    {
                        SkillId = skillId,
                        SkillType = SkillType.Active,
                        Level = 1
                    };
                    character.LearnedSkills.Add(learnedSkill);
                }

                if (action.Parameters.TryGetValue("skillLevel", out var skillLevelToken))
                    learnedSkill.Level = skillLevelToken.ToObject<int>();

                if (action.Parameters.TryGetValue("skillType", out var skillTypeToken) &&
                    Enum.TryParse(skillTypeToken.ToString(), true, out SkillType skillType))
                {
                    learnedSkill.SkillType = skillType;
                }
            }

            if (action.Parameters.TryGetValue("attributePoints", out var attributePointsToken))
                character.AttributePoints = attributePointsToken.ToObject<int>();

            if (action.Parameters.TryGetValue("isDead", out var isDeadToken))
                character.IsDead = isDeadToken.ToObject<bool>();

            if (action.Parameters.TryGetValue("equipmentSlot", out var equipmentSlotToken) &&
                action.Parameters.TryGetValue("equipmentId", out var equipmentIdToken))
            {
                string slotName = equipmentSlotToken.ToString();
                string equipmentId = equipmentIdToken.ToString();
                if (Enum.TryParse<EquipmentSlot>(slotName, true, out var slot))
                {
                    character.Equipment ??= new Dictionary<EquipmentSlot, string>();
                    character.Equipment[slot] = equipmentId;
                }
            }

            PlayerAdventureStateStore.Save(state);
            context.CurrentAdventureState = state;
            return GameplayStepResult.Pass(MapAdapterName, action.Kind, $"Updated character state for '{characterId}'.");
        }

        private static GameplayStepResult AddInventoryItem(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            string itemId = action.Parameters["itemId"]?.ToString();
            if (string.IsNullOrWhiteSpace(itemId))
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, "addInventoryItem requires itemId.");

            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            state.Inventory ??= new List<string>();
            state.Inventory.Add(itemId);
            PlayerAdventureStateStore.Save(state);
            context.CurrentAdventureState = state;
            return GameplayStepResult.Pass(MapAdapterName, action.Kind, $"Added inventory item '{itemId}'.");
        }

        private static GameplayStepResult AddConsumableInstance(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            string definitionId = action.Parameters["definitionId"]?.ToString();
            string instanceId = action.Parameters["instanceId"]?.ToString();
            var definition = ConsumableDatabase.GetById(definitionId);
            if (definition == null)
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, $"Consumable definition '{definitionId}' not found.");

            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            state.ConsumableInstances ??= new List<ConsumableInstance>();
            if (!string.IsNullOrWhiteSpace(instanceId) &&
                state.ConsumableInstances.Any(item => string.Equals(item?.InstanceId, instanceId, StringComparison.Ordinal)))
            {
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, $"Consumable instance '{instanceId}' already exists.");
            }

            var instance = ConsumableInstance.Create(definition, instanceId);
            state.ConsumableInstances.Add(instance);
            PlayerAdventureStateStore.Save(state);
            context.CurrentAdventureState = state;
            return GameplayStepResult.Pass(
                MapAdapterName,
                action.Kind,
                $"Added consumable '{definition.Id}' as instance '{instance.InstanceId}'.");
        }

        private static GameplayStepResult EquipInventoryEquipmentToRosterCharacter(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            string characterId = action.Parameters["characterId"]?.ToString();
            string equipmentId = action.Parameters["equipmentId"]?.ToString();
            if (string.IsNullOrWhiteSpace(characterId) || string.IsNullOrWhiteSpace(equipmentId))
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, "equipInventoryEquipmentToRosterCharacter requires characterId and equipmentId.");

            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            var character = state?.Roster?.FirstOrDefault(c => c.Id == characterId);
            if (character == null)
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, $"Character '{characterId}' not found.");

            if (state.Inventory == null || !state.Inventory.Contains(equipmentId))
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, $"Inventory does not contain '{equipmentId}'.");

            var equipmentDef = EquipmentDatabase.GetById(equipmentId);
            if (equipmentDef == null)
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, $"Equipment '{equipmentId}' not found in database.");

            if (!CharacterLoadoutService.TryEquipEquipment(state, characterId, equipmentId))
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, $"Failed to equip '{equipmentId}' to '{characterId}'.");

            PlayerAdventureStateStore.Save(state);
            context.CurrentAdventureState = state;
            return GameplayStepResult.Pass(MapAdapterName, action.Kind, $"Equipped '{equipmentId}' to '{characterId}' on slot '{equipmentDef.Slot}'.");
        }

        private static GameplayStepResult CarryConsumableToRosterCharacter(
            GameplayRuntimeContext context,
            ExecutableScenarioAction action)
        {
            string characterId = action.Parameters["characterId"]?.ToString();
            string instanceId = action.Parameters["instanceId"]?.ToString();
            if (string.IsNullOrWhiteSpace(characterId) || string.IsNullOrWhiteSpace(instanceId))
            {
                return GameplayStepResult.Fail(
                    MapAdapterName,
                    action.Kind,
                    "carryConsumableToRosterCharacter requires characterId and instanceId.");
            }

            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            if (!CharacterLoadoutService.TryCarryConsumable(state, characterId, instanceId))
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, $"Failed to carry '{instanceId}' on '{characterId}'.");

            PlayerAdventureStateStore.Save(state);
            context.CurrentAdventureState = state;
            return GameplayStepResult.Pass(MapAdapterName, action.Kind, $"'{characterId}' now carries '{instanceId}'.");
        }

        private static GameplayStepResult UnloadRosterCharacterConsumable(
            GameplayRuntimeContext context,
            ExecutableScenarioAction action)
        {
            string characterId = action.Parameters["characterId"]?.ToString();
            if (string.IsNullOrWhiteSpace(characterId))
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, "unloadRosterCharacterConsumable requires characterId.");

            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            if (!CharacterLoadoutService.TryUnloadConsumable(state, characterId))
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, $"Character '{characterId}' has no carried consumable.");

            PlayerAdventureStateStore.Save(state);
            context.CurrentAdventureState = state;
            return GameplayStepResult.Pass(MapAdapterName, action.Kind, $"Unloaded consumable from '{characterId}'.");
        }

        private static GameplayStepResult ApplyRestSiteResult(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            float healPercent = action.Parameters["healPercent"]?.ToObject<float>() ?? 0.3f;
            float manaHealPercent = action.Parameters["manaHealPercent"]?.ToObject<float>() ?? 0.3f;

            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            var reward = RewardResult.Empty();
            reward.HealPercent = healPercent;
            reward.ManaHealPercent = manaHealPercent;
            if (NodeInteractionManager.Instance != null)
                NodeInteractionManager.Instance.ApplyRewardResult(reward, state);
            else
            {
                reward.ApplyToState(state);
                PlayerAdventureStateStore.Save(state);
            }
            context.CurrentAdventureState = state;
            return GameplayStepResult.Pass(MapAdapterName, action.Kind, $"Applied rest site result: HP {healPercent:P0}, MP {manaHealPercent:P0}.");
        }

        private static GameplayStepResult BuyShopEquipment(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            string equipmentId = action.Parameters["equipmentId"]?.ToString();
            int price = action.Parameters["price"]?.ToObject<int>() ?? 0;
            if (string.IsNullOrWhiteSpace(equipmentId))
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, "buyShopEquipment requires equipmentId.");

            return ApplyShopPurchase(context, action.Kind, StoreGoodKind.Equipment, equipmentId, price);
        }

        private static GameplayStepResult BuyShopGood(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            string kindName = action.Parameters["itemKind"]?.ToString();
            string contentId = action.Parameters["contentId"]?.ToString();
            int price = action.Parameters["price"]?.ToObject<int>() ?? 0;
            if (!Enum.TryParse(kindName, true, out StoreGoodKind kind) || string.IsNullOrWhiteSpace(contentId))
            {
                return GameplayStepResult.Fail(
                    MapAdapterName,
                    action.Kind,
                    "buyShopGood requires a valid itemKind and contentId.");
            }

            return ApplyShopPurchase(context, action.Kind, kind, contentId, price);
        }

        private static GameplayStepResult ApplyShopPurchase(
            GameplayRuntimeContext context,
            string actionKind,
            StoreGoodKind kind,
            string contentId,
            int price)
        {
            bool isValid = kind == StoreGoodKind.Consumable
                ? ConsumableDatabase.GetById(contentId) != null
                : EquipmentDatabase.GetById(contentId) != null;
            if (!isValid)
                return GameplayStepResult.Fail(MapAdapterName, actionKind, $"Shop content '{contentId}' was not found.");

            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            var reward = RewardResult.Empty();
            reward.GoldCost = price;
            if (kind == StoreGoodKind.Consumable)
                reward.ItemIds.Add(contentId);
            else
                reward.EquipmentIds.Add(contentId);

            if (NodeInteractionManager.Instance != null)
                NodeInteractionManager.Instance.ApplyRewardResult(reward, state);
            else
            {
                reward.ApplyToState(state);
                PlayerAdventureStateStore.Save(state);
            }
            context.CurrentAdventureState = state;
            return GameplayStepResult.Pass(MapAdapterName, actionKind, $"Bought shop {kind} '{contentId}' for {price} gold.");
        }

        private static GameplayStepResult ApplyEventResult(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            string resultTypeName = action.Parameters["resultType"]?.ToString();
            if (string.IsNullOrWhiteSpace(resultTypeName) || !Enum.TryParse<EventResultType>(resultTypeName, true, out var resultType))
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, "applyEventResult requires a valid resultType.");

            string targetTypeName = action.Parameters["targetType"]?.ToString();
            EventTargetType targetType = EventTargetType.All;
            if (!string.IsNullOrWhiteSpace(targetTypeName) && !Enum.TryParse<EventTargetType>(targetTypeName, true, out targetType))
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, "applyEventResult targetType is invalid.");

            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            if (state == null)
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, "Adventure state is unavailable.");

            string selfCharacterId = action.Parameters["selfCharacterId"]?.ToString();
            var contextParty = ResolveContextParty(state, action);
            var eventContext = new EventEffectContext(contextParty, selfCharacterId, state);
            var eventResult = new EventResult
            {
                type = resultType,
                target = targetType,
                amount = action.Parameters["amount"]?.ToObject<int>() ?? 0,
                itemId = action.Parameters["itemId"]?.ToString(),
                description = action.Parameters["description"]?.ToString()
            };

            eventResult.Apply(eventContext);
            context.CurrentAdventureState = state;
            return GameplayStepResult.Pass(MapAdapterName, action.Kind, $"Applied event result '{resultType}' to target '{targetType}'.");
        }

        private static List<CharacterDefinition> ResolveContextParty(PlayerAdventureState state, ExecutableScenarioAction action)
        {
            var roster = state?.Roster ?? new List<CharacterDefinition>();
            if (!action.Parameters.TryGetValue("partyCharacterIds", out var partyIdsToken) || partyIdsToken == null)
                return roster;

            List<string> partyCharacterIds = partyIdsToken.Type switch
            {
                JTokenType.Array => partyIdsToken.ToObject<List<string>>(),
                JTokenType.String => partyIdsToken.ToString().Split(',').Select(id => id.Trim()).Where(id => !string.IsNullOrWhiteSpace(id)).ToList(),
                _ => null
            };

            if (partyCharacterIds == null || partyCharacterIds.Count == 0)
                return roster;

            return roster.Where(character => character != null && partyCharacterIds.Contains(character.Id)).ToList();
        }

        private static GameplayAssertionResult AssertCurrentNodeEquals(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            string expected = assertion.Expected?.ToString();
            if (string.IsNullOrWhiteSpace(expected))
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, "currentNodeEquals requires expected nodeId.");

            string actual = context.CurrentNodeId ?? RoguelikeMapRuntimeState.CurrentNodeId;
            return actual == expected
                ? GameplayAssertionResult.Pass(MapAdapterName, assertion.Kind, $"CurrentNode={actual}")
                : GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Expected CurrentNode={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertMapIsActive(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            bool expected = assertion.Expected?.ToObject<bool>() ?? true;
            bool actual = RoguelikeMapRuntimeState.HasActiveRun;
            return actual == expected
                ? GameplayAssertionResult.Pass(MapAdapterName, assertion.Kind, $"MapIsActive={actual}")
                : GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Expected MapIsActive={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertVisitedNodeCountEquals(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            int expected = assertion.Expected?.ToObject<int>() ?? 0;
            int actual = RoguelikeMapRuntimeState.VisitedPathNodeIds?.Count ?? 0;
            return actual == expected
                ? GameplayAssertionResult.Pass(MapAdapterName, assertion.Kind, $"VisitedNodeCount={actual}")
                : GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Expected VisitedNodeCount={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertBattleVictoryCountEquals(ExecutableScenarioAssertion assertion)
        {
            int expected = assertion.Expected?.ToObject<int>() ?? 0;
            int actual = RoguelikeMapRuntimeState.BattleVictoryCount;
            return actual == expected
                ? GameplayAssertionResult.Pass(MapAdapterName, assertion.Kind, $"BattleVictoryCount={actual}")
                : GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Expected BattleVictoryCount={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertNodeTypeEquals(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            string nodeId = assertion.Target ?? context.CurrentNodeId;
            if (string.IsNullOrWhiteSpace(nodeId))
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, "nodeTypeEquals requires target nodeId.");

            string expected = assertion.Expected?.ToString();
            if (string.IsNullOrWhiteSpace(expected))
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, "nodeTypeEquals requires expected type.");

            var map = context.RoguelikeMap ?? RoguelikeMapRuntimeState.CurrentMap;
            if (map == null)
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, "No active map.");

            var node = map.GetNode(nodeId);
            if (node == null)
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Node '{nodeId}' not found.");

            string actual = node.nodeType.ToString();
            return actual == expected
                ? GameplayAssertionResult.Pass(MapAdapterName, assertion.Kind, $"Node '{nodeId}' type={actual}")
                : GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Expected Node '{nodeId}' type={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertNodeIsReachable(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            string nodeId = assertion.Target ?? context.CurrentNodeId;
            if (string.IsNullOrWhiteSpace(nodeId))
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, "nodeIsReachable requires target nodeId.");

            bool expected = assertion.Expected?.ToObject<bool>() ?? true;

            var map = context.RoguelikeMap ?? RoguelikeMapRuntimeState.CurrentMap;
            if (map == null)
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, "No active map.");

            var node = map.GetNode(nodeId);
            if (node == null)
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Node '{nodeId}' not found.");

            bool actual = node.IsReachable;
            return actual == expected
                ? GameplayAssertionResult.Pass(MapAdapterName, assertion.Kind, $"Node '{nodeId}' IsReachable={actual}")
                : GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Expected Node '{nodeId}' IsReachable={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertNodeIsVisited(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            string nodeId = assertion.Target ?? context.CurrentNodeId;
            if (string.IsNullOrWhiteSpace(nodeId))
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, "nodeIsVisited requires target nodeId.");

            bool expected = assertion.Expected?.ToObject<bool>() ?? true;

            var map = context.RoguelikeMap ?? RoguelikeMapRuntimeState.CurrentMap;
            if (map == null)
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, "No active map.");

            var node = map.GetNode(nodeId);
            if (node == null)
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Node '{nodeId}' not found.");

            bool actual = node.VisitState == NodeVisitState.Visited;
            return actual == expected
                ? GameplayAssertionResult.Pass(MapAdapterName, assertion.Kind, $"Node '{nodeId}' IsVisited={actual}")
                : GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Expected Node '{nodeId}' IsVisited={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertRunGoldEquals(ExecutableScenarioAssertion assertion)
        {
            int expected = assertion.Expected?.ToObject<int>() ?? 0;
            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            int actual = state?.Gold ?? 0;
            return actual == expected
                ? GameplayAssertionResult.Pass(MapAdapterName, assertion.Kind, $"RunGold={actual}")
                : GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Expected RunGold={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertRosterCharacterHpEquals(ExecutableScenarioAssertion assertion)
        {
            string characterId = assertion.Target;
            if (string.IsNullOrWhiteSpace(characterId))
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, "rosterCharacterHpEquals requires target characterId.");

            int expected = assertion.Expected?.ToObject<int>() ?? 0;
            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            var character = state?.Roster?.FirstOrDefault(c => c.Id == characterId);
            if (character == null)
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Character '{characterId}' not found.");

            int actual = character.CurrentHp;
            return actual == expected
                ? GameplayAssertionResult.Pass(MapAdapterName, assertion.Kind, $"{characterId}.CurrentHp={actual}")
                : GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Expected {characterId}.CurrentHp={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertRosterCharacterMpEquals(ExecutableScenarioAssertion assertion)
        {
            string characterId = assertion.Target;
            if (string.IsNullOrWhiteSpace(characterId))
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, "rosterCharacterMpEquals requires target characterId.");

            int expected = assertion.Expected?.ToObject<int>() ?? 0;
            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            var character = state?.Roster?.FirstOrDefault(c => c.Id == characterId);
            if (character == null)
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Character '{characterId}' not found.");

            int actual = character.CurrentMp ?? 0;
            return actual == expected
                ? GameplayAssertionResult.Pass(MapAdapterName, assertion.Kind, $"{characterId}.CurrentMp={actual}")
                : GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Expected {characterId}.CurrentMp={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertRosterCharacterDeadEquals(ExecutableScenarioAssertion assertion)
        {
            string characterId = assertion.Target;
            if (string.IsNullOrWhiteSpace(characterId))
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, "rosterCharacterDeadEquals requires target characterId.");

            bool expected = assertion.Expected?.ToObject<bool>() ?? false;
            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            var character = state?.Roster?.FirstOrDefault(c => c.Id == characterId);
            if (character == null)
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Character '{characterId}' not found.");

            bool actual = character.IsDead;
            return actual == expected
                ? GameplayAssertionResult.Pass(MapAdapterName, assertion.Kind, $"{characterId}.IsDead={actual}")
                : GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Expected {characterId}.IsDead={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertRosterCharacterExperienceEquals(ExecutableScenarioAssertion assertion)
        {
            string characterId = assertion.Target;
            if (string.IsNullOrWhiteSpace(characterId))
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, "rosterCharacterExperienceEquals requires target characterId.");

            int expected = assertion.Expected?.ToObject<int>() ?? 0;
            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            var character = state?.Roster?.FirstOrDefault(c => c.Id == characterId);
            if (character == null)
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Character '{characterId}' not found.");

            int actual = character.Experience;
            return actual == expected
                ? GameplayAssertionResult.Pass(MapAdapterName, assertion.Kind, $"{characterId}.Experience={actual}")
                : GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Expected {characterId}.Experience={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertRosterCharacterLevelEquals(ExecutableScenarioAssertion assertion)
        {
            string characterId = assertion.Target;
            if (string.IsNullOrWhiteSpace(characterId))
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, "rosterCharacterLevelEquals requires target characterId.");

            int expected = assertion.Expected?.ToObject<int>() ?? 0;
            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            var character = state?.Roster?.FirstOrDefault(c => c.Id == characterId);
            if (character == null)
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Character '{characterId}' not found.");

            int actual = character.Level;
            return actual == expected
                ? GameplayAssertionResult.Pass(MapAdapterName, assertion.Kind, $"{characterId}.Level={actual}")
                : GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Expected {characterId}.Level={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertRosterCharacterHasSkillId(ExecutableScenarioAssertion assertion)
        {
            string characterId = assertion.Target;
            if (string.IsNullOrWhiteSpace(characterId))
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, "rosterCharacterHasSkillId requires target characterId.");

            string expectedSkillId = assertion.Expected?.ToString();
            if (string.IsNullOrWhiteSpace(expectedSkillId))
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, "rosterCharacterHasSkillId requires expected SkillId.");

            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            var character = state?.Roster?.FirstOrDefault(c => c.Id == characterId);
            if (character == null)
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Character '{characterId}' not found.");

            bool actual = character.LearnedSkills?.Any(skill =>
                string.Equals(skill?.SkillId, expectedSkillId, StringComparison.OrdinalIgnoreCase)) == true;
            return actual
                ? GameplayAssertionResult.Pass(MapAdapterName, assertion.Kind, $"{characterId} has learned skill '{expectedSkillId}'.")
                : GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Expected {characterId} to have learned skill '{expectedSkillId}'.");
        }

        private static GameplayAssertionResult AssertRosterCharacterSkillLevelEquals(ExecutableScenarioAssertion assertion)
        {
            string characterId = assertion.Target;
            string skillId = assertion.Parameters["skillId"]?.ToString();
            if (string.IsNullOrWhiteSpace(characterId) || string.IsNullOrWhiteSpace(skillId))
            {
                return GameplayAssertionResult.Fail(
                    MapAdapterName,
                    assertion.Kind,
                    "rosterCharacterSkillLevelEquals requires target characterId and parameters.skillId.");
            }

            int expected = assertion.Expected?.ToObject<int>() ?? 0;
            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            var character = state?.Roster?.FirstOrDefault(candidate => candidate?.Id == characterId);
            var learned = character?.LearnedSkills?.FirstOrDefault(skill =>
                string.Equals(skill?.SkillId, skillId, StringComparison.Ordinal));
            if (learned == null)
            {
                return GameplayAssertionResult.Fail(
                    MapAdapterName,
                    assertion.Kind,
                    $"Character '{characterId}' has not learned '{skillId}'.");
            }

            return learned.Level == expected
                ? GameplayAssertionResult.Pass(MapAdapterName, assertion.Kind, $"{characterId}.{skillId}=Lv{learned.Level}")
                : GameplayAssertionResult.Fail(
                    MapAdapterName,
                    assertion.Kind,
                    $"Expected {characterId}.{skillId}=Lv{expected}, actual=Lv{learned.Level}.");
        }

        private static GameplayAssertionResult AssertPureRunSkillChoiceContains(ExecutableScenarioAssertion assertion)
        {
            string characterId = assertion.Target;
            string expectedSkillId = assertion.Expected?.ToString();
            int targetLevel = assertion.Parameters["targetLevel"]?.ToObject<int>() ?? 1;
            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            var character = state?.Roster?.FirstOrDefault(candidate => candidate?.Id == characterId);
            if (state?.IsPureRun != true || character == null)
            {
                return GameplayAssertionResult.Fail(
                    MapAdapterName,
                    assertion.Kind,
                    $"Pure Run character '{characterId}' was not found.");
            }

            var choices = PureRunProgression.BuildSkillChoices(character, state.RunSeed, character.Level);
            bool actual = choices.Any(skill =>
                string.Equals(skill?.Id, expectedSkillId, StringComparison.Ordinal) && skill.Level == targetLevel);
            return actual
                ? GameplayAssertionResult.Pass(
                    MapAdapterName,
                    assertion.Kind,
                    $"Offer contains {expectedSkillId} Lv{targetLevel}.")
                : GameplayAssertionResult.Fail(
                    MapAdapterName,
                    assertion.Kind,
                    $"Offer does not contain {expectedSkillId} Lv{targetLevel}.");
        }

        private static GameplayAssertionResult AssertPureRunSkillChoicesAreMixed(ExecutableScenarioAssertion assertion)
        {
            string characterId = assertion.Target;
            bool expected = assertion.Expected?.ToObject<bool>() ?? true;
            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            var character = state?.Roster?.FirstOrDefault(candidate => candidate?.Id == characterId);
            if (state?.IsPureRun != true || character == null)
            {
                return GameplayAssertionResult.Fail(
                    MapAdapterName,
                    assertion.Kind,
                    $"Pure Run character '{characterId}' was not found.");
            }

            var choices = PureRunProgression.BuildSkillChoices(character, state.RunSeed, character.Level);
            bool hasUpgrade = choices.Any(skill => SkillSystem.HasSkill(character, skill.Id));
            bool hasNewSkill = choices.Any(skill => !SkillSystem.HasSkill(character, skill.Id));
            bool actual = hasUpgrade && hasNewSkill;
            return actual == expected
                ? GameplayAssertionResult.Pass(MapAdapterName, assertion.Kind, $"Mixed offer={actual}.")
                : GameplayAssertionResult.Fail(
                    MapAdapterName,
                    assertion.Kind,
                    $"Expected mixed offer={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertRosterCharacterEquipmentEquals(ExecutableScenarioAssertion assertion)
        {
            string characterId = assertion.Target;
            if (string.IsNullOrWhiteSpace(characterId))
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, "rosterCharacterEquipmentEquals requires target characterId.");

            string slotName = assertion.Parameters["equipmentSlot"]?.ToString();
            if (string.IsNullOrWhiteSpace(slotName) || !Enum.TryParse<EquipmentSlot>(slotName, true, out var slot))
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, "rosterCharacterEquipmentEquals requires a valid equipmentSlot parameter.");

            string expected = assertion.Expected?.Type == JTokenType.Null
                ? null
                : assertion.Expected?.ToString();
            expected = string.IsNullOrWhiteSpace(expected) ? null : expected;
            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            var character = state?.Roster?.FirstOrDefault(c => c.Id == characterId);
            if (character == null)
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Character '{characterId}' not found.");

            character.Equipment ??= new Dictionary<EquipmentSlot, string>();
            character.Equipment.TryGetValue(slot, out string actual);
            return actual == expected
                ? GameplayAssertionResult.Pass(MapAdapterName, assertion.Kind, $"{characterId}.{slot}={actual}")
                : GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Expected {characterId}.{slot}={expected}, actual={actual ?? "<null>"}.");
        }

        private static GameplayAssertionResult AssertRosterCharacterTotalAttributeEquals(ExecutableScenarioAssertion assertion)
        {
            string characterId = assertion.Target;
            if (string.IsNullOrWhiteSpace(characterId))
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, "rosterCharacterTotalAttributeEquals requires target characterId.");

            string attribute = assertion.Parameters["attribute"]?.ToString();
            if (string.IsNullOrWhiteSpace(attribute))
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, "rosterCharacterTotalAttributeEquals requires attribute parameter.");

            int expected = assertion.Expected?.ToObject<int>() ?? 0;
            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            var character = state?.Roster?.FirstOrDefault(c => c.Id == characterId);
            if (character == null)
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Character '{characterId}' not found.");

            int actual = attribute switch
            {
                "Strength" => character.GetTotalStrength(),
                "Agility" => character.GetTotalAgility(),
                "Constitution" => character.GetTotalConstitution(),
                "Intelligence" => character.GetTotalIntelligence(),
                "Charisma" => character.GetTotalCharisma(),
                "Luck" => character.GetTotalLuck(),
                _ => int.MinValue
            };

            if (actual == int.MinValue)
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Unsupported attribute '{attribute}'.");

            return actual == expected
                ? GameplayAssertionResult.Pass(MapAdapterName, assertion.Kind, $"{characterId}.Total{attribute}={actual}")
                : GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Expected {characterId}.Total{attribute}={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertInventoryContains(ExecutableScenarioAssertion assertion)
        {
            string expectedItem = assertion.Expected?.ToString();
            if (string.IsNullOrWhiteSpace(expectedItem))
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, "inventoryContains requires expected item/equipment id.");

            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            bool actual = state?.Inventory?.Contains(expectedItem) == true;
            return actual
                ? GameplayAssertionResult.Pass(MapAdapterName, assertion.Kind, $"Inventory contains '{expectedItem}'.")
                : GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Expected inventory to contain '{expectedItem}'.");
        }

        private static GameplayAssertionResult AssertConsumableCountEquals(ExecutableScenarioAssertion assertion)
        {
            int expected = assertion.Expected?.ToObject<int>() ?? 0;
            int actual = PlayerAdventureStateStore.LoadRepairAndSave()?.ConsumableInstances?.Count ?? 0;
            return actual == expected
                ? GameplayAssertionResult.Pass(MapAdapterName, assertion.Kind, $"ConsumableCount={actual}")
                : GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Expected ConsumableCount={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertRosterCharacterCarriedConsumableEquals(
            ExecutableScenarioAssertion assertion)
        {
            string characterId = assertion.Target;
            if (string.IsNullOrWhiteSpace(characterId))
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, "rosterCharacterCarriedConsumableEquals requires target characterId.");

            string expected = assertion.Expected?.Type == JTokenType.Null
                ? null
                : assertion.Expected?.ToString();
            expected = string.IsNullOrWhiteSpace(expected) ? null : expected;
            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            string actual = state?.Roster?
                .FirstOrDefault(character => character?.Id == characterId)?
                .CarriedConsumableInstanceId;
            actual = string.IsNullOrWhiteSpace(actual) ? null : actual;

            return string.Equals(actual, expected, StringComparison.Ordinal)
                ? GameplayAssertionResult.Pass(MapAdapterName, assertion.Kind, $"{characterId}.CarriedConsumable={actual ?? "<empty>"}")
                : GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Expected {characterId}.CarriedConsumable={expected ?? "<empty>"}, actual={actual ?? "<empty>"}.");
        }

        private static GameplayAssertionResult AssertBackpackConsumableCountEquals(ExecutableScenarioAssertion assertion)
        {
            int expected = assertion.Expected?.ToObject<int>() ?? 0;
            string definitionId = assertion.Parameters["definitionId"]?.ToString();
            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            var backpack = CharacterLoadoutService.GetBackpackConsumables(state);
            int actual = string.IsNullOrWhiteSpace(definitionId)
                ? backpack.Count
                : backpack.Count(item => string.Equals(item.DefinitionId, definitionId, StringComparison.Ordinal));

            return actual == expected
                ? GameplayAssertionResult.Pass(MapAdapterName, assertion.Kind, $"BackpackConsumableCount={actual}")
                : GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Expected BackpackConsumableCount={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertConsumableInstanceExists(ExecutableScenarioAssertion assertion)
        {
            string instanceId = assertion.Target ?? assertion.Parameters["instanceId"]?.ToString();
            if (string.IsNullOrWhiteSpace(instanceId))
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, "consumableInstanceExists requires target instanceId.");

            bool expected = assertion.Expected?.ToObject<bool>() ?? true;
            bool actual = PlayerAdventureStateStore.LoadRepairAndSave()?.ConsumableInstances?
                .Any(instance => string.Equals(instance?.InstanceId, instanceId, StringComparison.Ordinal)) == true;
            return actual == expected
                ? GameplayAssertionResult.Pass(MapAdapterName, assertion.Kind, $"ConsumableInstanceExists({instanceId})={actual}")
                : GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Expected ConsumableInstanceExists({instanceId})={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertShopGoodCountEquals(
            GameplayRuntimeContext context,
            ExecutableScenarioAssertion assertion)
        {
            int expected = assertion.Expected?.ToObject<int>() ?? 3;
            int actual = GenerateShopGoods(context, assertion).Count;
            return actual == expected
                ? GameplayAssertionResult.Pass(MapAdapterName, assertion.Kind, $"ShopGoodCount={actual}")
                : GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Expected ShopGoodCount={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertShopConsumableCountAtLeast(
            GameplayRuntimeContext context,
            ExecutableScenarioAssertion assertion)
        {
            int expected = assertion.Expected?.ToObject<int>() ?? 1;
            int actual = GenerateShopGoods(context, assertion).Count(good => good.IsConsumable);
            return actual >= expected
                ? GameplayAssertionResult.Pass(MapAdapterName, assertion.Kind, $"ShopConsumableCount={actual}")
                : GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Expected ShopConsumableCount>={expected}, actual={actual}.");
        }

        private static GameplayAssertionResult AssertShopConsumableIdsUnique(
            GameplayRuntimeContext context,
            ExecutableScenarioAssertion assertion)
        {
            bool expected = assertion.Expected?.ToObject<bool>() ?? true;
            var consumableIds = GenerateShopGoods(context, assertion)
                .Where(good => good.IsConsumable)
                .Select(good => good.ConsumableId)
                .ToList();
            bool actual = consumableIds.Count == consumableIds.Distinct(StringComparer.Ordinal).Count();
            return actual == expected
                ? GameplayAssertionResult.Pass(MapAdapterName, assertion.Kind, $"ShopConsumableIdsUnique={actual}")
                : GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Expected ShopConsumableIdsUnique={expected}, actual={actual}.");
        }

        private static List<ShopGood> GenerateShopGoods(
            GameplayRuntimeContext context,
            ExecutableScenarioAssertion assertion)
        {
            string nodeId = assertion.Parameters["nodeId"]?.ToString();
            nodeId = string.IsNullOrWhiteSpace(nodeId)
                ? context.CurrentNodeId ?? RoguelikeMapRuntimeState.CurrentNodeId ?? "test-store"
                : nodeId;

            int runSeed = assertion.Parameters["runSeed"]?.ToObject<int>()
                ?? context.RunSeed
                ?? RoguelikeMapRuntimeState.RunSeed;
            int seed = assertion.Parameters["seed"]?.ToObject<int>()
                ?? RoguelikeMapRuntimeState.DeriveSeed(runSeed, $"store:{nodeId}");

            var map = context.RoguelikeMap ?? RoguelikeMapRuntimeState.CurrentMap;
            var configuredGoods = map?.GetNode(nodeId)?.storeConfig?.goods;
            return new ShopManager().GenerateGoods(3, seed, configuredGoods);
        }

        private static GameplayAssertionResult AssertRosterCharacterHasPendingBuff(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            string characterId = assertion.Target;
            if (string.IsNullOrWhiteSpace(characterId))
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, "rosterCharacterHasPendingBuff requires target characterId.");

            string buffName = assertion.Expected?.ToString();
            if (string.IsNullOrWhiteSpace(buffName))
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, "rosterCharacterHasPendingBuff requires expected buffName.");

            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            var character = state?.Roster?.FirstOrDefault(c => c.Id == characterId);
            if (character == null)
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Character '{characterId}' not found.");

            bool actual = character.HasPendingBuff(buffName);
            return actual
                ? GameplayAssertionResult.Pass(MapAdapterName, assertion.Kind, $"{characterId} has persisted pending buff '{buffName}'.")
                : GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Expected {characterId} to have persisted pending buff '{buffName}'.");
        }

        private static GameplayAssertionResult AssertRuntimeRosterCharacterHasPendingBuff(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            string characterId = assertion.Target;
            if (string.IsNullOrWhiteSpace(characterId))
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, "runtimeRosterCharacterHasPendingBuff requires target characterId.");

            string buffName = assertion.Expected?.ToString();
            if (string.IsNullOrWhiteSpace(buffName))
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, "runtimeRosterCharacterHasPendingBuff requires expected buffName.");

            var state = context?.CurrentAdventureState ?? PlayerAdventureStateStore.LoadRepairAndSave();
            var character = state?.Roster?.FirstOrDefault(c => c.Id == characterId);
            if (character == null)
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Character '{characterId}' not found.");

            bool actual = character.HasPendingBuff(buffName);
            return actual
                ? GameplayAssertionResult.Pass(MapAdapterName, assertion.Kind, $"{characterId} has runtime pending buff '{buffName}'.")
                : GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Expected {characterId} to have runtime pending buff '{buffName}'.");
        }

        private static GameplayAssertionResult AssertRosterCharacterPendingBuffHasIcon(ExecutableScenarioAssertion assertion)
        {
            string characterId = assertion.Target;
            if (string.IsNullOrWhiteSpace(characterId))
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, "rosterCharacterPendingBuffHasIcon requires target characterId.");

            string buffName = assertion.Expected?.ToString();
            if (string.IsNullOrWhiteSpace(buffName))
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, "rosterCharacterPendingBuffHasIcon requires expected buffName.");

            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            var character = state?.Roster?.FirstOrDefault(c => c.Id == characterId);
            if (character == null)
                return GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Character '{characterId}' not found.");

            var buff = character.PendingBuffs?.FirstOrDefault(b => b != null && b.BuffName == buffName);
            bool actual = buff?.Icon != null;
            return actual
                ? GameplayAssertionResult.Pass(MapAdapterName, assertion.Kind, $"{characterId} pending buff '{buffName}' has icon.")
                : GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Expected {characterId} pending buff '{buffName}' to retain icon after reload.");
        }
    }
}
