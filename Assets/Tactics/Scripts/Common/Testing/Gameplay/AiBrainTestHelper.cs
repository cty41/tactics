using System;
using System.Reflection;
using Tactics.Common.AI.MonsterAI;
using UnityEngine;

namespace Tactics.Common.Testing.Gameplay
{
    public static class AiBrainTestHelper
    {
        public static AiBrainAsset CreateAttackBrain()
        {
            var graph = CreateBasicAttackGraph();
            var profile = CreateDefaultProfile();
            return CreateBrainAsset(graph, profile);
        }

        public static AiBrainAsset CreateHealBrain()
        {
            var graph = CreateHealGraph();
            var profile = CreateDefaultProfile();
            return CreateBrainAsset(graph, profile);
        }

        private static AiDecisionGraph CreateBasicAttackGraph()
        {
            var graph = ScriptableObject.CreateInstance<AiDecisionGraph>();

            // Add BasicAttack intent node
            var intentNode = graph.AddNode(GraphNodeType.Intent, Vector2.zero);
            SetField(intentNode, "_intentType", IntentType.BasicAttack);
            SetField(intentNode, "_basePriority", 50f);

            // Add TargetInRange rule
            var ruleNode = graph.AddNode(GraphNodeType.Rule, new Vector2(200, 0));
            SetField(ruleNode, "_ruleType", RuleType.TargetInRange);

            // Add DistanceToTarget score
            var scoreNode = graph.AddNode(GraphNodeType.Score, new Vector2(400, 0));
            SetField(scoreNode, "_scoreType", ScoreType.DistanceToTarget);
            SetField(scoreNode, "_weight", 5f);

            // Connect nodes
            graph.AddEdge(intentNode.NodeId, ruleNode.NodeId);
            graph.AddEdge(intentNode.NodeId, scoreNode.NodeId);

            return graph;
        }

        private static AiDecisionGraph CreateHealGraph()
        {
            var graph = ScriptableObject.CreateInstance<AiDecisionGraph>();

            // Add AbilityUse intent node
            var intentNode = graph.AddNode(GraphNodeType.Intent, Vector2.zero);
            SetField(intentNode, "_intentType", IntentType.AbilityUse);
            SetField(intentNode, "_basePriority", 60f);

            // Add HasHealAbility rule
            var ruleNode = graph.AddNode(GraphNodeType.Rule, new Vector2(200, 0));
            SetField(ruleNode, "_ruleType", RuleType.HasHealAbility);

            // Add HealUrgency score
            var scoreNode = graph.AddNode(GraphNodeType.Score, new Vector2(400, 0));
            SetField(scoreNode, "_scoreType", ScoreType.HealUrgency);
            SetField(scoreNode, "_weight", 10f);

            // Connect nodes
            graph.AddEdge(intentNode.NodeId, ruleNode.NodeId);
            graph.AddEdge(intentNode.NodeId, scoreNode.NodeId);

            return graph;
        }

        private static AIProfile CreateDefaultProfile()
        {
            var profile = ScriptableObject.CreateInstance<AIProfile>();
            SetField(profile, "_enableDistanceScore", true);
            SetField(profile, "_distanceWeight", 5f);
            SetField(profile, "_enableTargetHealthScore", true);
            SetField(profile, "_targetHealthWeight", 3f);
            SetField(profile, "_enableSelfHealthScore", true);
            SetField(profile, "_selfHealthWeight", 2f);
            SetField(profile, "_noiseFactor", 0f);
            return profile;
        }

        private static AiBrainAsset CreateBrainAsset(AiDecisionGraph graph, AIProfile profile)
        {
            var brain = ScriptableObject.CreateInstance<AiBrainAsset>();
            SetField(brain, "_decisionGraph", graph);
            SetField(brain, "_profile", profile);
            SetField(brain, "_lowHealthThreshold", 0.3f);
            SetField(brain, "_killableDamageThreshold", 0.5f);
            SetField(brain, "_lowHealthTargetBonus", 20f);
            SetField(brain, "_retreatBaseScore", 50f);
            SetField(brain, "_maxEngageCandidatesPerTarget", 3);
            SetField(brain, "_enableVerboseLogging", true);
            return brain;
        }

        private static void SetField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(obj, value);
        }
    }
}
