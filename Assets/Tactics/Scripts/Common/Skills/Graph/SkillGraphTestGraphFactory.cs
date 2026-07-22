using System;
using System.Collections.Generic;
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

        public static SkillGraphAsset CreateSelfManaGraph(string displayName, float manaAmount)
        {
            var graph = CreateGraph(displayName);
            var start = CreateNode<StartNodeRecord>("start");
            var selectSelf = CreateNode<SelectSelfNodeRecord>("select_self");
            var restoreMana = CreateNode<ApplyManaNodeRecord>("restore_mana");
            restoreMana.ManaAmount = manaAmount;
            var finish = CreateNode<FinishNodeRecord>("finish");

            graph.Nodes.Add(start);
            graph.Nodes.Add(selectSelf);
            graph.Nodes.Add(restoreMana);
            graph.Nodes.Add(finish);
            graph.AddEdge("start", "select_self");
            graph.AddEdge("select_self", "restore_mana");
            graph.AddEdge("restore_mana", "finish");
            return graph;
        }

        public static SkillGraphAsset CreateSingleTargetDamageGraph(
            string displayName,
            float baseDamage,
            bool canCrit = false,
            bool isRanged = false,
            int minRange = 1,
            int maxRange = 3)
        {
            var graph = CreateGraph(displayName);

            var start = CreateNode<StartNodeRecord>("start");
            var selectTarget = CreateNode<SelectPrimaryTargetNodeRecord>("select_target");
            selectTarget.MinRange = Math.Max(1, minRange);
            selectTarget.MaxRange = Math.Max(selectTarget.MinRange, maxRange);

            var damage = CreateNode<ApplyDamageNodeRecord>("damage");
            damage.BaseDamage = baseDamage;
            damage.CanCrit = canCrit;
            damage.IsRanged = isRanged;

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

        public static SkillGraphAsset CreateChargeGraph(string displayName, int distance, int maxRange = 3, float collisionDamage = 1f)
        {
            var graph = CreateGraph(displayName);

            var start = CreateNode<StartNodeRecord>("start");
            var selectTarget = CreateNode<SelectPrimaryTargetNodeRecord>("select_target");
            selectTarget.MinRange = 1;
            selectTarget.MaxRange = Math.Max(1, maxRange);

            var dash = CreateNode<DashToTargetNodeRecord>("dash");
            dash.MaxRange = Math.Max(1, maxRange);
            dash.CollisionDamage = collisionDamage;

            var finish = CreateNode<FinishNodeRecord>("finish");

            graph.Nodes.Add(start);
            graph.Nodes.Add(selectTarget);
            graph.Nodes.Add(dash);
            graph.Nodes.Add(finish);

            graph.AddEdge("start", "select_target");
            graph.AddEdge("select_target", "dash");
            graph.AddEdge("dash", "finish");

            return graph;
        }

        public static SkillGraphAsset CreateAreaDamageGraph(string displayName, float baseDamage, int radius = 1, int maxRange = 3)
        {
            var graph = CreateGraph(displayName);

            var start = CreateNode<StartNodeRecord>("start");
            var selectPoint = CreateNode<SelectTargetPointNodeRecord>("select_point");
            selectPoint.MaxRange = maxRange;

            var collect = CreateNode<CollectTargetsInAreaNodeRecord>("collect");
            collect.Radius = radius;

            var forEach = CreateNode<ForEachTargetNodeRecord>("for_each");

            var damage = CreateNode<ApplyDamageNodeRecord>("damage");
            damage.BaseDamage = baseDamage;
            damage.CanCrit = false;

            var finish = CreateNode<FinishNodeRecord>("finish");

            graph.Nodes.Add(start);
            graph.Nodes.Add(selectPoint);
            graph.Nodes.Add(collect);
            graph.Nodes.Add(forEach);
            graph.Nodes.Add(damage);
            graph.Nodes.Add(finish);

            graph.AddEdge("start", "select_point");
            graph.AddEdge("select_point", "collect");
            graph.AddEdge("collect", "for_each");
            graph.AddEdge("for_each", "damage");
            graph.AddEdge("damage", "for_each");
            graph.AddEdge("for_each", "finish", SkillGraphPortType.OnComplete);

            return graph;
        }

        public static SkillGraphAsset CreateKnockbackGraph(string displayName, int distance, int maxRange = 1)
        {
            var graph = CreateGraph(displayName);

            var start = CreateNode<StartNodeRecord>("start");
            var selectTarget = CreateNode<SelectPrimaryTargetNodeRecord>("select_target");
            selectTarget.MinRange = 1;
            selectTarget.MaxRange = Math.Max(1, maxRange);

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

        public static SkillGraphAsset CreateAllyHealGraph(string displayName, float healAmount, int maxRange = 2)
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
            int duration = 2,
            BuffEffectType effectType = BuffEffectType.None,
            BuffTriggerTiming triggerTiming = BuffTriggerTiming.None,
            string selectionKind = "self",
            int maxRange = 1,
            bool canAct = true)
        {
            var graph = CreateGraph(displayName);

            var start = CreateNode<StartNodeRecord>("start");

            SkillGraphNodeRecord selectTarget;
            if (string.Equals(selectionKind, "self", System.StringComparison.OrdinalIgnoreCase))
            {
                selectTarget = CreateNode<SelectSelfNodeRecord>("select_target");
            }
            else
            {
                var selectNode = CreateNode<SelectPrimaryTargetNodeRecord>("select_target");
                selectNode.MinRange = 1;
                selectNode.MaxRange = Math.Max(1, maxRange);
                selectTarget = selectNode;
            }

            var buffConfig = ScriptableObject.CreateInstance<BuffConfig>();
            buffConfig.name = buffName;
            SetPrivateField(typeof(BuffConfig), buffConfig, "_buffName", buffName);
            SetPrivateField(typeof(BuffConfig), buffConfig, "_defaultDuration", duration);
            SetPrivateField(typeof(BuffConfig), buffConfig, "_effectType", effectType);
            SetPrivateField(typeof(BuffConfig), buffConfig, "_triggerTiming", triggerTiming);
            SetPrivateField(typeof(BuffConfig), buffConfig, "_canAct", canAct);

            var buff = CreateNode<ApplyBuffNodeRecord>("buff");
            buff.Duration = duration;
            buff.BuffConfig = buffConfig;

            var finish = CreateNode<FinishNodeRecord>("finish");

            graph.Nodes.Add(start);
            graph.Nodes.Add(selectTarget);
            graph.Nodes.Add(buff);
            graph.Nodes.Add(finish);

            graph.AddEdge("start", "select_target");
            graph.AddEdge("select_target", "buff");
            graph.AddEdge("buff", "finish");

            return graph;
        }

        public static SkillGraphAsset CreateProjectileGraph(string displayName, float baseDamage, float travelTime = 0.05f)
        {
            var graph = CreateGraph(displayName);

            var start = CreateNode<StartNodeRecord>("start");
            var selectTarget = CreateNode<SelectPrimaryTargetNodeRecord>("select_target");
            selectTarget.MinRange = 1;
            selectTarget.MaxRange = 3;

            var projectile = CreateNode<ProjectileLaunchNodeRecord>("projectile");
            projectile.TravelTime = travelTime;
            projectile.Speed = 10f;

            var onHit = CreateNode<OnHitNodeRecord>("on_hit");

            var damage = CreateNode<ApplyDamageNodeRecord>("damage");
            damage.BaseDamage = baseDamage;

            var finish = CreateNode<FinishNodeRecord>("finish");

            graph.Nodes.Add(start);
            graph.Nodes.Add(selectTarget);
            graph.Nodes.Add(projectile);
            graph.Nodes.Add(onHit);
            graph.Nodes.Add(damage);
            graph.Nodes.Add(finish);

            graph.AddEdge("start", "select_target");
            graph.AddEdge("select_target", "projectile");
            graph.AddEdge("projectile", "on_hit");
            graph.AddEdge("on_hit", "damage");
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
