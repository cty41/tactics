using Tactics.Common.Skills.Graph;
using Tactics.Common.Units.Abilities;
using Tactics.Runtime.Utilities;
using UnityEditor;
using UnityEngine;

namespace Tactics.Editor.SkillGraphEditor
{
    public static class SkillGraphAssetMenu
    {
        private const string SampleDir = "Assets/Tactics/Battle/Abilities/SkillGraphs";

        [MenuItem("Tactics/Create/Skill Graph Asset")]
        public static void CreateSkillGraphAsset()
        {
            var graph = ScriptableObject.CreateInstance<SkillGraphAsset>();
            graph.DisplayName = "New Skill Graph";

            string path = EditorUtility.SaveFilePanelInProject(
                "Create Skill Graph", "NewSkillGraph", "asset", "Choose location");

            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(graph, path);
                AssetDatabase.SaveAssets();
                Selection.activeObject = graph;
                TLog.Info($"[SkillGraph] Created asset: {path}");
            }
            else
            {
                Object.DestroyImmediate(graph);
            }
        }

        [MenuItem("Tactics/Create Sample Skill Graphs")]
        public static void CreateSampleGraphs()
        {
            if (!AssetDatabase.IsValidFolder(SampleDir))
            {
                System.IO.Directory.CreateDirectory(SampleDir);
                AssetDatabase.Refresh();
            }

            CreateChargeStrike();
            CreateAreaBlast();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            TLog.Info("[SkillGraph] Sample skill graphs created.");
        }

        /// <summary>
        /// 样例1：冲向目标并造成伤害，命中后击退
        /// Start -> SelectPrimaryTarget -> DashToTarget -> ApplyDamage -> ApplyKnockback -> Finish
        /// </summary>
        private static void CreateChargeStrike()
        {
            var graph = ScriptableObject.CreateInstance<SkillGraphAsset>();
            graph.DisplayName = "Charge Strike Lv1";

            var start = graph.AddNode(SkillGraphNodeType.Start, new Vector2(0, 100));
            var selectTarget = graph.AddNode(SkillGraphNodeType.SelectPrimaryTarget, new Vector2(200, 100));
            var dash = graph.AddNode(SkillGraphNodeType.DashToTarget, new Vector2(400, 100));
            var damage = graph.AddNode(SkillGraphNodeType.ApplyDamage, new Vector2(600, 100));
            var knockback = graph.AddNode(SkillGraphNodeType.ApplyKnockback, new Vector2(800, 100));
            var finish = graph.AddNode(SkillGraphNodeType.Finish, new Vector2(1000, 100));

            // Set parameters
            ((SelectPrimaryTargetNodeRecord)selectTarget).MaxRange = 4;
            ((DashToTargetNodeRecord)dash).MaxRange = 4;
            ((DashToTargetNodeRecord)dash).CollisionDamage = 1f;
            ((ApplyDamageNodeRecord)damage).BaseDamage = 10f;
            ((ApplyDamageNodeRecord)damage).DamageType = SkillGraphDamageType.Physical;
            ((ApplyKnockbackNodeRecord)knockback).Distance = 1;

            graph.AddEdge(start.NodeId, selectTarget.NodeId);
            graph.AddEdge(selectTarget.NodeId, dash.NodeId);
            graph.AddEdge(dash.NodeId, damage.NodeId);
            graph.AddEdge(damage.NodeId, knockback.NodeId);
            graph.AddEdge(knockback.NodeId, finish.NodeId);

            string path = $"{SampleDir}/ChargeStrike_Lv1.asset";
            AssetDatabase.CreateAsset(graph, path);
            TLog.Info($"[SkillGraph] Created: {path}");
        }

        /// <summary>
        /// 样例2：选点范围伤害
        /// Start -> SelectTargetPoint -> CollectTargetsInArea -> ForEachTarget -> ApplyDamage -> Finish
        /// </summary>
        private static void CreateAreaBlast()
        {
            var graph = ScriptableObject.CreateInstance<SkillGraphAsset>();
            graph.DisplayName = "Area Blast Lv1";

            var start = graph.AddNode(SkillGraphNodeType.Start, new Vector2(0, 100));
            var selectPoint = graph.AddNode(SkillGraphNodeType.SelectTargetPoint, new Vector2(200, 100));
            var collect = graph.AddNode(SkillGraphNodeType.CollectTargetsInArea, new Vector2(400, 100));
            var forEach = graph.AddNode(SkillGraphNodeType.ForEachTarget, new Vector2(600, 100));
            var damage = graph.AddNode(SkillGraphNodeType.ApplyDamage, new Vector2(800, 100));
            var finish = graph.AddNode(SkillGraphNodeType.Finish, new Vector2(1000, 100));

            // Set parameters
            ((SelectTargetPointNodeRecord)selectPoint).MaxRange = 4;
            ((CollectTargetsInAreaNodeRecord)collect).Radius = 2;
            ((CollectTargetsInAreaNodeRecord)collect).Shape = SkillGraphAreaShape.Circle;
            ((ApplyDamageNodeRecord)damage).BaseDamage = 8f;
            ((ApplyDamageNodeRecord)damage).DamageType = SkillGraphDamageType.Magical;

            graph.AddEdge(start.NodeId, selectPoint.NodeId);
            graph.AddEdge(selectPoint.NodeId, collect.NodeId);
            graph.AddEdge(collect.NodeId, forEach.NodeId);
            graph.AddEdge(forEach.NodeId, damage.NodeId);
            graph.AddEdge(damage.NodeId, forEach.NodeId, SkillGraphPortType.Default); // loop back
            graph.AddEdge(forEach.NodeId, finish.NodeId);

            // Note: ForEachTarget loop requires runtime handling - the runner's ForEachTargetNodeExecutor
            // auto-advances index and returns Success when done. The edge back to forEach is for
            // "process next target". In practice, the runner handles this via the ForEachIndex blackboard.

            string path = $"{SampleDir}/AreaBlast_Lv1.asset";
            AssetDatabase.CreateAsset(graph, path);
            TLog.Info($"[SkillGraph] Created: {path}");
        }

        [MenuItem("Tactics/Create Ability Config From Selected Skill Graph", true)]
        public static bool ValidateCreateAbilityConfigFromSelectedSkillGraph()
        {
            return Selection.activeObject is SkillGraphAsset;
        }

        [MenuItem("Tactics/Create Ability Config From Selected Skill Graph")]
        public static void CreateAbilityConfigFromSelectedSkillGraph()
        {
            if (Selection.activeObject is not SkillGraphAsset graph)
                return;

            var config = SkillGraphAbilityConfigGenerator.CreateOrSync(graph);
            if (config != null)
            {
                Selection.activeObject = config;
                TLog.Info($"[SkillGraph] Ability config ready: {AssetDatabase.GetAssetPath(config)}");
            }
        }

        [MenuItem("Tactics/Create Ability Configs For All Skill Graphs")]
        public static void CreateAbilityConfigsForAllSkillGraphs()
        {
            if (!AssetDatabase.IsValidFolder(SkillGraphAbilityConfigGenerator.SkillGraphDir))
            {
                TLog.Warning($"[SkillGraph] Graph directory not found: {SkillGraphAbilityConfigGenerator.SkillGraphDir}");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:SkillGraphAsset", new[] { SkillGraphAbilityConfigGenerator.SkillGraphDir });
            int createdOrUpdated = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var graph = AssetDatabase.LoadAssetAtPath<SkillGraphAsset>(path);
                if (graph == null)
                    continue;

                if (SkillGraphAbilityConfigGenerator.CreateOrSync(graph) != null)
                    createdOrUpdated++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            TLog.Info($"[SkillGraph] Generated or synced {createdOrUpdated} ability configs.");
        }

        [MenuItem("Tactics/Attach Selected SkillGraphAbilityConfig To Test Prefab", true)]
        public static bool ValidateAttachSelectedAbilityConfigToTestPrefab()
        {
            return Selection.activeObject is SkillGraphAbilityConfig;
        }

        [MenuItem("Tactics/Attach Selected SkillGraphAbilityConfig To Test Prefab")]
        public static void AttachSelectedAbilityConfigToTestPrefab()
        {
            if (Selection.activeObject is not SkillGraphAbilityConfig config)
                return;

            if (SkillGraphTestPrefabUtility.AttachAbilityConfigToTestPrefab(config, replaceAllExisting: true))
                TLog.Info($"[SkillGraph] Attached '{config.name}' to test prefab.");
        }

        [MenuItem("Tactics/Build From Selected Skill Graph And Attach To Test Prefab", true)]
        public static bool ValidateBuildFromSelectedSkillGraphAndAttachToTestPrefab()
        {
            return Selection.activeObject is SkillGraphAsset;
        }

        [MenuItem("Tactics/Build From Selected Skill Graph And Attach To Test Prefab")]
        public static void BuildFromSelectedSkillGraphAndAttachToTestPrefab()
        {
            if (Selection.activeObject is not SkillGraphAsset graph)
                return;

            if (SkillGraphTestPrefabUtility.BuildAndAttachFromGraph(graph))
                TLog.Info($"[SkillGraph] Built bridge config and attached '{graph.name}' to test prefab.");
        }
    }
}
