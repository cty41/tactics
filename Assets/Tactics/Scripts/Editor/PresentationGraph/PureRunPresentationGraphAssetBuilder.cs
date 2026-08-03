#if UNITY_EDITOR
using System;
using System.Linq;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Units.Abilities;
using Tactics.Common.Units.Tween;
using Tactics.Runtime.Utilities;
using UnityEditor;
using UnityEngine;

namespace Tactics.Editor.PresentationGraph
{
    /// <summary>
    /// Idempotently migrates the three representative Pure Run abilities to presentation graphs.
    /// </summary>
    internal static class PureRunPresentationGraphAssetBuilder
    {
        private const string OutputRoot = "Assets/Tactics/Arts/PureRun/Presentation";
        private const string ConfigRoot =
            "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs";
        private const string GraphRoot = "Assets/Tactics/Battle/Abilities/SkillGraphs";
        private const string PilotoProfileRoot =
            "Assets/Tactics/Arts/PureRun/VFX/PilotoAdapted/Profiles";
        private const string ProjectileRoot =
            "Assets/Tactics/Arts/PureRun/Tween/Projectiles";

        [MenuItem("Tactics/Tools/Pure Run/Rebuild Presentation Graph Samples")]
        private static void Rebuild()
        {
            EnsureFolder("Assets/Tactics/Arts/PureRun", "Presentation");
            for (int level = 1; level <= 3; level++)
            {
                string suffix = level == 1 ? string.Empty : $"_Lv{level}";
                BuildCueAbility(
                    $"Lightning{suffix}",
                    UnitVisualAction.Cast,
                    Load<VisualCueProfile>($"{PilotoProfileRoot}/LightningLv{level}.asset"),
                    PresentationCueKind.PrimaryTargetHit);
                BuildCurseSigilAbility(level);
                BuildProjectileAbility(
                    $"PoisonSpear{suffix}",
                    Load<ProjectileVisualProfile>($"{ProjectileRoot}/AmazonPoisonSpear.asset"));
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            TLog.Info("[PresentationGraph] Rebuilt lightning, curse and poison spear samples.");
        }

        /// <summary>
        /// Rebuilds only the three curse presentation graphs after their layered sigil profiles change.
        /// </summary>
        public static void RebuildCurseSigilSamples()
        {
            EnsureFolder("Assets/Tactics/Arts/PureRun", "Presentation");
            for (int level = 1; level <= 3; level++)
                BuildCurseSigilAbility(level);
            AssetDatabase.SaveAssets();
        }

        private static void BuildCurseSigilAbility(int level)
        {
            string suffix = level == 1 ? string.Empty : $"_Lv{level}";
            string abilityName = $"Curse{suffix}";
            VisualCueProfile ground = Load<VisualCueProfile>(
                $"{PilotoProfileRoot}/AmplifyDamageSigilGroundV2Lv{level}.asset");
            VisualCueProfile rearFlames = Load<VisualCueProfile>(
                $"{PilotoProfileRoot}/AmplifyDamageSigilRearFlamesV2Lv{level}.asset");
            VisualCueProfile foregroundFlames = Load<VisualCueProfile>(
                $"{PilotoProfileRoot}/AmplifyDamageSigilForegroundFlamesV2Lv{level}.asset");
            if (ground == null || rearFlames == null || foregroundFlames == null)
                throw new InvalidOperationException($"Missing layered curse sigil profiles for {abilityName}.");

            BattlePresentationGraph presentation = LoadOrCreate(abilityName);
            presentation.Nodes.Clear();
            presentation.Edges.Clear();
            BuildActionEntry(presentation, UnitVisualAction.Cast);

            var entry = Add<PresentationEntryNodeRecord>(presentation, PresentationNodeType.Entry, 0f, 220f);
            entry.Cue = PresentationCueKind.PrimaryTargetHit;
            var fork = Add<PresentationForkNodeRecord>(presentation, PresentationNodeType.Fork, 220f, 220f);
            var groundFx = Add<PresentationPrefabFxNodeRecord>(
                presentation,
                PresentationNodeType.PrefabFx,
                450f,
                160f);
            groundFx.Profile = ground;
            var rearFlamesFx = Add<PresentationPrefabFxNodeRecord>(
                presentation,
                PresentationNodeType.PrefabFx,
                450f,
                220f);
            rearFlamesFx.Profile = rearFlames;
            var foregroundFlamesFx = Add<PresentationPrefabFxNodeRecord>(
                presentation,
                PresentationNodeType.PrefabFx,
                450f,
                300f);
            foregroundFlamesFx.Profile = foregroundFlames;
            var join = Add<PresentationJoinNodeRecord>(presentation, PresentationNodeType.Join, 700f, 220f);
            fork.JoinNodeId = join.NodeId;
            var impact = Add<PresentationMarkerNodeRecord>(presentation, PresentationNodeType.Marker, 900f, 220f);
            impact.Marker = PresentationMarkerKind.Impact;
            var finish = Add<PresentationFinishNodeRecord>(presentation, PresentationNodeType.Finish, 1100f, 220f);

            presentation.AddEdge(entry.NodeId, fork.NodeId);
            presentation.AddEdge(fork.NodeId, groundFx.NodeId);
            presentation.AddEdge(fork.NodeId, rearFlamesFx.NodeId);
            presentation.AddEdge(fork.NodeId, foregroundFlamesFx.NodeId);
            presentation.AddEdge(groundFx.NodeId, join.NodeId);
            presentation.AddEdge(rearFlamesFx.NodeId, join.NodeId);
            presentation.AddEdge(foregroundFlamesFx.NodeId, join.NodeId);
            Connect(presentation, join, impact, finish);
            EditorUtility.SetDirty(presentation);

            AssignToConfig(abilityName, presentation);
            ReplaceLegacyVisualCueNode(abilityName, PresentationCueKind.PrimaryTargetHit);
        }

        private static void BuildCueAbility(
            string abilityName,
            UnitVisualAction action,
            VisualCueProfile profile,
            PresentationCueKind cue)
        {
            if (profile == null)
                throw new InvalidOperationException($"Missing VisualCueProfile for {abilityName}.");
            BattlePresentationGraph presentation = LoadOrCreate(abilityName);
            presentation.Nodes.Clear();
            presentation.Edges.Clear();
            BuildActionEntry(presentation, action);

            var entry = Add<PresentationEntryNodeRecord>(presentation, PresentationNodeType.Entry, 0f, 220f);
            entry.Cue = cue;
            var fx = Add<PresentationPrefabFxNodeRecord>(presentation, PresentationNodeType.PrefabFx, 260f, 220f);
            fx.Profile = profile;
            var impact = Add<PresentationMarkerNodeRecord>(presentation, PresentationNodeType.Marker, 520f, 220f);
            impact.Marker = PresentationMarkerKind.Impact;
            var finish = Add<PresentationFinishNodeRecord>(presentation, PresentationNodeType.Finish, 760f, 220f);
            Connect(presentation, entry, fx, impact, finish);
            EditorUtility.SetDirty(presentation);

            AssignToConfig(abilityName, presentation);
            ReplaceLegacyVisualCueNode(abilityName, cue);
        }

        private static void BuildProjectileAbility(
            string abilityName,
            ProjectileVisualProfile profile)
        {
            if (profile == null)
                throw new InvalidOperationException($"Missing ProjectileVisualProfile for {abilityName}.");
            BattlePresentationGraph presentation = LoadOrCreate(abilityName);
            presentation.Nodes.Clear();
            presentation.Edges.Clear();
            BuildActionEntry(presentation, UnitVisualAction.Ranged);

            var entry = Add<PresentationEntryNodeRecord>(presentation, PresentationNodeType.Entry, 0f, 220f);
            entry.Cue = PresentationCueKind.Projectile;
            var projectile = Add<PresentationProjectileNodeRecord>(
                presentation,
                PresentationNodeType.Projectile,
                280f,
                220f);
            projectile.Profile = profile;
            projectile.Speed = 10f;
            projectile.FallbackTravelTime = 0.3f;
            projectile.EmitImpactMarker = true;
            var finish = Add<PresentationFinishNodeRecord>(presentation, PresentationNodeType.Finish, 570f, 220f);
            Connect(presentation, entry, projectile, finish);
            EditorUtility.SetDirty(presentation);
            AssignToConfig(abilityName, presentation);
        }

        private static void BuildActionEntry(
            BattlePresentationGraph presentation,
            UnitVisualAction action)
        {
            var entry = Add<PresentationEntryNodeRecord>(presentation, PresentationNodeType.Entry, 0f, 20f);
            entry.Cue = PresentationCueKind.Action;
            var tween = Add<PresentationUnitTweenNodeRecord>(presentation, PresentationNodeType.UnitTween, 270f, 20f);
            tween.Action = action;
            tween.EmitReleaseMarker = true;
            var finish = Add<PresentationFinishNodeRecord>(presentation, PresentationNodeType.Finish, 560f, 20f);
            Connect(presentation, entry, tween, finish);
        }

        private static void ReplaceLegacyVisualCueNode(
            string abilityName,
            PresentationCueKind cue)
        {
            SkillGraphAsset skillGraph = Load<SkillGraphAsset>($"{GraphRoot}/{abilityName}_Graph.asset");
            if (skillGraph == null)
                throw new InvalidOperationException($"Missing SkillGraph for {abilityName}.");
            for (int index = 0; index < skillGraph.Nodes.Count; index++)
            {
                if (skillGraph.Nodes[index] is not PlayVisualCueNodeRecord legacy)
                    continue;
                skillGraph.Nodes[index] = new PlayPresentationCueNodeRecord
                {
                    NodeId = legacy.NodeId,
                    Position = legacy.Position,
                    Enabled = legacy.Enabled,
                    Cue = cue
                };
            }
            EditorUtility.SetDirty(skillGraph);
        }

        private static void AssignToConfig(
            string abilityName,
            BattlePresentationGraph presentation)
        {
            SkillGraphAbilityConfig config = Load<SkillGraphAbilityConfig>(
                $"{ConfigRoot}/{abilityName}_Graph_Ability.asset");
            if (config == null)
                throw new InvalidOperationException($"Missing ability config for {abilityName}.");
            var serialized = new SerializedObject(config);
            SerializedProperty property = serialized.FindProperty("_presentationGraph");
            property.objectReferenceValue = presentation;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        private static BattlePresentationGraph LoadOrCreate(string abilityName)
        {
            string path = $"{OutputRoot}/{abilityName}_Presentation.asset";
            BattlePresentationGraph graph = Load<BattlePresentationGraph>(path);
            if (graph == null)
            {
                graph = ScriptableObject.CreateInstance<BattlePresentationGraph>();
                AssetDatabase.CreateAsset(graph, path);
            }
            graph.DisplayName = abilityName;
            graph.Version = 1;
            return graph;
        }

        private static T Add<T>(
            BattlePresentationGraph graph,
            PresentationNodeType type,
            float x,
            float y)
            where T : PresentationNodeRecord
        {
            return (T)graph.AddNode(type, new Vector2(x, y));
        }

        private static void Connect(
            BattlePresentationGraph graph,
            params PresentationNodeRecord[] nodes)
        {
            for (int index = 0; index < nodes.Length - 1; index++)
                graph.AddEdge(nodes[index].NodeId, nodes[index + 1].NodeId);
        }

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
