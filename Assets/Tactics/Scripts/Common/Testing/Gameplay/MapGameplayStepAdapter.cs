using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Tactics.Roguelike;
using Tactics.RoguelikeMap;
using Tactics.RoguelikeMap.Events;
using Tactics.RoguelikeMap.Interaction;
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
                or "enterNode"
                or "triggerEvent"
                or "completeNode";
        }

        public async Task<GameplayStepResult> ExecuteAsync(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            try
            {
                switch (action.Kind)
                {
                    case "loadRoguelikeMap":
                        return LoadRoguelikeMap(context, action);
                    case "enterNode":
                        return EnterNode(context, action);
                    case "triggerEvent":
                        return await TriggerEvent(context, action);
                    case "completeNode":
                        return CompleteNode(context, action);
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
                or "nodeTypeEquals"
                or "nodeIsReachable"
                or "nodeIsVisited";
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
                    "nodeTypeEquals" => AssertNodeTypeEquals(context, assertion),
                    "nodeIsReachable" => AssertNodeIsReachable(context, assertion),
                    "nodeIsVisited" => AssertNodeIsVisited(context, assertion),
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
            var mapState = RoguelikeMapRuntimeState;

            data["hasActiveRun"] = mapState.HasActiveRun;
            data["currentNodeId"] = mapState.CurrentNodeId;
            data["visitedNodeCount"] = mapState.VisitedPathNodeIds?.Count ?? 0;

            if (mapState.CurrentMap != null)
            {
                var currentNode = mapState.CurrentMap.GetNode(mapState.CurrentNodeId);
                if (currentNode != null)
                {
                    data["currentNodeType"] = currentNode.nodeType.ToString();
                    data["currentNodeReachable"] = currentNode.IsReachable;
                    data["currentNodeVisited"] = currentNode.IsVisited;
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

        private static GameplayStepResult LoadRoguelikeMap(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            string mapConfigPath = action.Parameters["mapConfigPath"]?.ToString();
            if (string.IsNullOrWhiteSpace(mapConfigPath))
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, "loadRoguelikeMap requires mapConfigPath.");

            // Load map config from GameAssetManager
            var mapConfig = GameAssetManager.Instance?.Load<RoguelikeMapConfig>(mapConfigPath);
            if (mapConfig == null)
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, $"Failed to load map config from '{mapConfigPath}'.");

            // Generate map from config
            var generator = new RoguelikeMapGenerator();
            var map = generator.Generate(mapConfig);
            if (map == null)
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, "Failed to generate map from config.");

            // Attach map to runtime state
            RoguelikeMapRuntimeState.AttachMap(map);
            context.RoguelikeMap = map;

            return GameplayStepResult.Pass(MapAdapterName, action.Kind, $"Loaded map with {map.nodes?.Count ?? 0} nodes.");
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
                return GameplayStepResult.Fail(MapAdapterName, action.Kind, $"Event '{eventId}' not found.");

            // Store event in context for assertions
            context.CurrentEvent = evt;
            context.EventCompleted = false;

            return GameplayStepResult.Pass(MapAdapterName, action.Kind, $"Triggered event '{eventId}'.");
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

            // Mark node as visited
            node.IsVisited = true;
            map.visitedNodes.Add(nodeId);

            // Update reachable nodes
            foreach (var connection in node.outgoing)
            {
                var connectedNode = map.GetNode(connection);
                if (connectedNode != null && !connectedNode.IsVisited)
                {
                    connectedNode.IsReachable = true;
                }
            }

            context.EventCompleted = true;

            return GameplayStepResult.Pass(MapAdapterName, action.Kind, $"Completed node '{nodeId}'.");
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

            bool actual = node.IsVisited;
            return actual == expected
                ? GameplayAssertionResult.Pass(MapAdapterName, assertion.Kind, $"Node '{nodeId}' IsVisited={actual}")
                : GameplayAssertionResult.Fail(MapAdapterName, assertion.Kind, $"Expected Node '{nodeId}' IsVisited={expected}, actual={actual}.");
        }
    }
}
