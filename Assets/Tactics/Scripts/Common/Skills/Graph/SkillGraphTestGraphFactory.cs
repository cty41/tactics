using System;
using Tactics.Common.Units.Buffs;
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

        public static SkillGraphAsset CreateAreaDamageGraph(string displayName, float baseDamage, int radius, int maxRange = 4)
        {
            var graph = CreateGraph(displayName);

            var start = CreateNode<StartNodeRecord>("start");
            var selectPoint = CreateNode<SelectTargetPointNodeRecord>("select_point");
            selectPoint.MaxRange = maxRange;

            var collectTargets = CreateNode<CollectTargetsInAreaNodeRecord>("collect_targets");
            collectTargets.Radius = radius;
            collectTargets.Shape = SkillGraphAreaShape.Circle;

            var forEachTarget = CreateNode<ForEachTargetNodeRecord>("for_each_target");

            var damage = CreateNode<ApplyDamageNodeRecord>("damage");
            damage.BaseDamage = baseDamage;
            damage.CanCrit = false;
            damage.IsRanged = false;

            var finish = CreateNode<FinishNodeRecord>("finish");

            graph.Nodes.Add(start);
            graph.Nodes.Add(selectPoint);
            graph.Nodes.Add(collectTargets);
            graph.Nodes.Add(forEachTarget);
            graph.Nodes.Add(damage);
            graph.Nodes.Add(finish);

            graph.AddEdge("start", "select_point");
            graph.AddEdge("select_point", "collect_targets");
            graph.AddEdge("collect_targets", "for_each_target");
            graph.AddEdge("for_each_target", "damage");
            graph.AddEdge("damage", "for_each_target");
            graph.AddEdge("for_each_target", "finish", SkillGraphPortType.OnComplete);

            return graph;
        }

        public static SkillGraphAsset CreateKnockbackGraph(string displayName, int distance, int maxRange = 1)
        {
            var graph = CreateGraph(displayName);

            var start = CreateNode<StartNodeRecord>("start");
            var selectTarget = CreateNode<SelectPrimaryTargetNodeRecord>("select_target");
            selectTarget.MinRange = 1;
            selectTarget.MaxRange = maxRange;

            var knockback = CreateNode<ApplyKnockbackNodeRecord>("knockback");
            knockback.Distance = distance;

            var finish = CreateNode<FinishNodeRecord>("finish");

            graph.Nodes.Add(start);
            graph.Nodes.Add(selectTarget);
            graph.Nodes.Add(knockback);
            graph.Nodes.Add(finish);

            graph.AddEdge("start", "select_target");
            graph.AddEdge("select_target", "knockback");
            graph.AddEdge("knockback", "finish");

            return graph;
        }

        public static SkillGraphAsset CreateAllyHealGraph(string displayName, float healAmount, int maxRange = 1)
        {
            var graph = CreateGraph(displayName);

            var start = CreateNode<StartNodeRecord>("start");
            var selectAlly = CreateNode<SelectAllyNodeRecord>("select_ally");
            selectAlly.MaxRange = maxRange;

            var heal = CreateNode<ApplyHealNodeRecord>("heal");
            heal.HealAmount = healAmount;

            var finish = CreateNode<FinishNodeRecord>("finish");

            graph.Nodes.Add(start);
            graph.Nodes.Add(selectAlly);
            graph.Nodes.Add(heal);
            graph.Nodes.Add(finish);

            graph.AddEdge("start", "select_ally");
            graph.AddEdge("select_ally", "heal");
            graph.AddEdge("heal", "finish");

            return graph;
        }

        public static SkillGraphAsset CreateApplyBuffGraph(
            string displayName,
            string buffName,
            int duration,
            BuffEffectType effectType,
            BuffTriggerTiming triggerTiming,
            string selectionKind = "self",
            int maxRange = 1,
            bool isUnique = true,
            bool canAct = true)
        {
            var graph = CreateGraph(displayName);
            var start = CreateNode<StartNodeRecord>("start");
            SkillGraphNodeRecord selectionNode;

            switch ((selectionKind ?? "self").Trim().ToLowerInvariant())
            {
                case "self":
                    selectionNode = CreateNode<SelectSelfNodeRecord>("select_self");
                    break;
                case "ally":
                    var selectAlly = CreateNode<SelectAllyNodeRecord>("select_ally");
                    selectAlly.MaxRange = maxRange;
                    selectionNode = selectAlly;
                    break;
                case "enemy":
                    var selectTarget = CreateNode<SelectPrimaryTargetNodeRecord>("select_target");
                    selectTarget.MinRange = 1;
                    selectTarget.MaxRange = maxRange;
                    selectionNode = selectTarget;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(selectionKind), selectionKind, "Unsupported buff selection kind.");
            }

            var applyBuff = CreateNode<ApplyBuffNodeRecord>("apply_buff");
            applyBuff.BuffConfig = CreateBuffConfig(buffName, duration, effectType, triggerTiming, isUnique, canAct);
            applyBuff.Duration = duration;

            var finish = CreateNode<FinishNodeRecord>("finish");

            graph.Nodes.Add(start);
            graph.Nodes.Add(selectionNode);
            graph.Nodes.Add(applyBuff);
            graph.Nodes.Add(finish);

            graph.AddEdge("start", selectionNode.NodeId);
            graph.AddEdge(selectionNode.NodeId, "apply_buff");
            graph.AddEdge("apply_buff", "finish");

            return graph;
        }

        private static BuffConfig CreateBuffConfig(
            string buffName,
            int defaultDuration,
            BuffEffectType effectType,
            BuffTriggerTiming triggerTiming,
            bool isUnique,
            bool canAct)
        {
            var config = ScriptableObject.CreateInstance<BuffConfig>();
            config.name = buffName;

            SetPrivateField(typeof(BuffConfig), config, "_buffName", buffName);
            SetPrivateField(typeof(BuffConfig), config, "_defaultDuration", defaultDuration);
            SetPrivateField(typeof(BuffConfig), config, "_canAct", canAct);
            SetPrivateField(typeof(BuffConfig), config, "_isUnique", isUnique);
            SetPrivateField(typeof(BuffConfig), config, "_effectType", effectType);
            SetPrivateField(typeof(BuffConfig), config, "_triggerTiming", triggerTiming);
            SetPrivateField(typeof(BuffConfig), config, "_damagePerTurn", 0f);
            SetPrivateField(typeof(BuffConfig), config, "_elementType", ElementType.None);

            return config;
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

        private static void SetPrivateField(Type declaringType, object target, string fieldName, object value)
        {
            var field = declaringType.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field == null)
            {
                throw new InvalidOperationException($"Field '{fieldName}' not found on '{declaringType.FullName}'.");
            }

            field.SetValue(target, value);
        }
    }
}
