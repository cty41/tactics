using UnityEngine;

namespace Tactics.Common.Skills.Graph.Testing
{
    /// <summary>
    /// 运行时技能图测试用图工厂。
    /// </summary>
    public static class SkillGraphTestGraphFactory
    {
        public static SkillGraphAsset CreateSelfHealGraph(string displayName, float healAmount, bool includeFinishNode = true)
        {
            var graph = CreateGraph(displayName);

            var start = CreateNode<StartNodeRecord>("start");
            var selectSelf = CreateNode<SelectSelfNodeRecord>("select_self");
            var heal = CreateNode<ApplyHealNodeRecord>("heal");
            heal.HealAmount = healAmount;

            graph.Nodes.Add(start);
            graph.Nodes.Add(selectSelf);
            graph.Nodes.Add(heal);

            if (includeFinishNode)
            {
                graph.Nodes.Add(CreateNode<FinishNodeRecord>("finish"));
            }

            graph.AddEdge("start", "select_self");
            graph.AddEdge("select_self", "heal");

            if (includeFinishNode)
            {
                graph.AddEdge("heal", "finish");
            }

            return graph;
        }

        public static SkillGraphAsset CreateSingleTargetDamageGraph(string displayName, float baseDamage)
        {
            var graph = CreateGraph(displayName);

            var start = CreateNode<StartNodeRecord>("start");
            var selectTarget = CreateNode<SelectPrimaryTargetNodeRecord>("select_target");
            selectTarget.MinRange = 1;
            selectTarget.MaxRange = 3;

            var damage = CreateNode<ApplyDamageNodeRecord>("damage");
            damage.BaseDamage = baseDamage;
            damage.CanCrit = false;
            damage.IsRanged = false;

            var finish = CreateNode<FinishNodeRecord>("finish");

            graph.Nodes.Add(start);
            graph.Nodes.Add(selectTarget);
            graph.Nodes.Add(damage);
            graph.Nodes.Add(finish);

            graph.AddEdge("start", "select_target");
            graph.AddEdge("select_target", "damage");
            graph.AddEdge("damage", "finish");

            return graph;
        }

        private static SkillGraphAsset CreateGraph(string displayName)
        {
            var graph = ScriptableObject.CreateInstance<SkillGraphAsset>();
            graph.name = displayName;
            graph.DisplayName = displayName;
            graph.Version = 1;
            return graph;
        }

        private static T CreateNode<T>(string nodeId) where T : SkillGraphNodeRecord, new()
        {
            var node = new T
            {
                NodeId = nodeId,
                Position = Vector2.zero,
                Enabled = true
            };
            return node;
        }
    }
}
