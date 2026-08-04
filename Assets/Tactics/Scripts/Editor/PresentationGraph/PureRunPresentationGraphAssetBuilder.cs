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
    /// Idempotently rebuilds the published Pure Run presentation graphs.
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
        private const string RecipeRoot =
            "Assets/Tactics/Arts/PureRun/Tween/SkillVfx/Recipes";
        private const string UnitPrefabRoot =
            "Assets/Tactics/Arts/PureRun/Prefabs/Units";

        [MenuItem("Tactics/Tools/Pure Run/Rebuild Presentation Graph Samples")]
        private static void Rebuild()
        {
            EnsureFolder("Assets/Tactics/Arts/PureRun", "Presentation");
            RebuildLightningSamples();
            RebuildCurseSigilSamples();
            RebuildPoisonSpearSamples();
            RebuildThrustSamples();
            RebuildFireballSamples();
            RebuildBoneSpearSamples();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            TLog.Info("[PresentationGraph] Rebuilt published Pure Run presentation graphs.");
        }

        [MenuItem("Tactics/Tools/Pure Run/Rebuild Programmatic Presentation Graphs")]
        private static void RebuildProgrammaticSamples()
        {
            RebuildThrustSamples();
            RebuildFireballSamples();
            RebuildBoneSpearSamples();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            TLog.Info("[PresentationGraph] Rebuilt thrust, fireball and bone spear graphs.");
        }

        [MenuItem("Tactics/Tools/Pure Run/Configure Presentation Preview Scenarios")]
        private static void ConfigurePresentationPreviewScenarios()
        {
            Rebuild();
        }

        public static void RebuildThrustSamples()
        {
            EnsureFolder("Assets/Tactics/Arts/PureRun", "Presentation");
            SkillVfxRecipe recipe = Require<SkillVfxRecipe>(
                $"{RecipeRoot}/ThrustSkillVfxRecipe.asset");
            for (int level = 1; level <= 3; level++)
            {
                string suffix = level == 1 ? string.Empty : $"_Lv{level}";
                string abilityName = $"Thrust{suffix}";
                BattlePresentationGraph presentation = ResetPresentation(abilityName);
                presentation.DefaultPreviewEntry = PresentationCueKind.DirectionalStrike;
                BuildActionEntry(presentation, UnitVisualAction.Melee);
                BuildHybridEntry(
                    presentation,
                    PresentationCueKind.DirectionalStrike,
                    recipe,
                    SkillVfxCueKind.DirectionalStrike,
                    Require<VisualCueProfile>($"{PilotoProfileRoot}/ThrustStrikeLv{level}.asset"),
                    220f);
                BuildHybridEntry(
                    presentation,
                    PresentationCueKind.PrimaryTargetHit,
                    recipe,
                    SkillVfxCueKind.PrimaryTargetHit,
                    Require<VisualCueProfile>($"{PilotoProfileRoot}/ThrustHitLv{level}.asset"),
                    420f);
                ConfigurePreviewScenario(
                    presentation,
                    Require<GameObject>($"{UnitPrefabRoot}/PureRunHunter.prefab"),
                    Phase(PresentationCueKind.Action, PresentationPreviewAdvanceKind.Release),
                    Phase(PresentationCueKind.DirectionalStrike, PresentationPreviewAdvanceKind.Blocking),
                    Phase(PresentationCueKind.PrimaryTargetHit, true));
                FinalizeGraphOnlyAbility(
                    presentation,
                    new[] { $"Thrust{suffix}_Graph_Ability" },
                    Array.Empty<string>());
            }
            AssetDatabase.SaveAssets();
        }

        public static void RebuildLightningSamples()
        {
            EnsureFolder("Assets/Tactics/Arts/PureRun", "Presentation");
            for (int level = 1; level <= 3; level++)
            {
                string suffix = level == 1 ? string.Empty : $"_Lv{level}";
                BuildCueAbility(
                    $"Lightning{suffix}",
                    UnitVisualAction.Cast,
                    Require<VisualCueProfile>($"{PilotoProfileRoot}/LightningLv{level}.asset"),
                    PresentationCueKind.PrimaryTargetHit,
                    Require<GameObject>($"{UnitPrefabRoot}/PureRunMage.prefab"),
                    true);
            }
            AssetDatabase.SaveAssets();
        }

        public static void RebuildPoisonSpearSamples()
        {
            EnsureFolder("Assets/Tactics/Arts/PureRun", "Presentation");
            ProjectileVisualProfile projectile = Require<ProjectileVisualProfile>(
                $"{ProjectileRoot}/AmazonPoisonSpear.asset");
            for (int level = 1; level <= 3; level++)
            {
                string suffix = level == 1 ? string.Empty : $"_Lv{level}";
                BuildProjectileAbility($"PoisonSpear{suffix}", projectile);
            }
            AssetDatabase.SaveAssets();
        }

        public static void RebuildFireballSamples()
        {
            EnsureFolder("Assets/Tactics/Arts/PureRun", "Presentation");
            SkillVfxRecipe recipe = Require<SkillVfxRecipe>(
                $"{RecipeRoot}/FireballSkillVfxRecipe.asset");
            ProjectileVisualProfile projectile = Require<ProjectileVisualProfile>(
                $"{ProjectileRoot}/Fire.asset");
            for (int level = 1; level <= 3; level++)
            {
                string suffix = level == 1 ? string.Empty : $"_Lv{level}";
                BattlePresentationGraph presentation = ResetPresentation($"Fireball{suffix}");
                presentation.DefaultPreviewEntry = PresentationCueKind.Projectile;
                BuildActionEntry(presentation, UnitVisualAction.Cast);
                BuildHybridEntry(
                    presentation,
                    PresentationCueKind.CastCharge,
                    recipe,
                    SkillVfxCueKind.CastCharge,
                    Require<VisualCueProfile>($"{PilotoProfileRoot}/FireballChargeLv{level}.asset"),
                    120f);
                BuildProjectileEntry(presentation, projectile, 8f, 0.5f, 220f);
                BuildHybridEntry(
                    presentation,
                    PresentationCueKind.ProjectileImpact,
                    recipe,
                    SkillVfxCueKind.ProjectileImpact,
                    Require<VisualCueProfile>($"{PilotoProfileRoot}/FireballImpactLv{level}.asset"),
                    320f);
                BuildProceduralEntry(
                    presentation,
                    PresentationCueKind.SecondaryTargetHit,
                    recipe,
                    SkillVfxCueKind.SecondaryTargetHit,
                    420f);
                if (level >= 3)
                {
                    BuildHybridEntry(
                        presentation,
                        PresentationCueKind.ConditionalDetonation,
                        recipe,
                        SkillVfxCueKind.ConditionalDetonation,
                        Require<VisualCueProfile>($"{PilotoProfileRoot}/FireballDetonationLv3.asset"),
                        520f);
                }
                else
                {
                    BuildProceduralEntry(
                        presentation,
                        PresentationCueKind.ConditionalDetonation,
                        recipe,
                        SkillVfxCueKind.ConditionalDetonation,
                        520f);
                }
                var previewPhases = new System.Collections.Generic.List<PresentationPreviewPhaseRecord>
                {
                    Phase(
                        new[] { PresentationCueKind.CastCharge, PresentationCueKind.Action },
                        PresentationCueKind.Action,
                        PresentationPreviewAdvanceKind.Release),
                    Phase(PresentationCueKind.Projectile, PresentationPreviewAdvanceKind.Impact),
                    Phase(PresentationCueKind.ProjectileImpact, PresentationPreviewAdvanceKind.Blocking)
                };
                if (level >= 3)
                {
                    previewPhases.Add(Phase(
                        PresentationCueKind.ConditionalDetonation,
                        PresentationPreviewAdvanceKind.Blocking));
                }
                previewPhases.Add(Phase(PresentationCueKind.SecondaryTargetHit, true));
                ConfigurePreviewScenario(
                    presentation,
                    Require<GameObject>($"{UnitPrefabRoot}/PureRunMage.prefab"),
                    previewPhases.ToArray());

                string[] configNames = level switch
                {
                    1 => new[]
                    {
                        "Fireball_Graph_Ability",
                        "Fireball_Lv1_Ability"
                    },
                    2 => new[] { "Fireball_Lv2_Ability" },
                    _ => new[] { "Fireball_Lv3_Ability" }
                };
                string[] optionalConfigNames = level switch
                {
                    1 => new[] { "SkeletonMageFireball_Lv1_Ability" },
                    2 => new[] { "SkeletonMageFireball_Lv2_Ability" },
                    _ => Array.Empty<string>()
                };
                string[] graphNames = level == 1
                    ? new[] { "Fireball_Graph", "Fireball_Lv1_Graph" }
                    : new[] { $"Fireball_Lv{level}_Graph" };
                FinalizeGraphOnlyAbility(
                    presentation,
                    configNames,
                    graphNames,
                    optionalConfigNames);
            }
            AssetDatabase.SaveAssets();
        }

        public static void RebuildBoneSpearSamples()
        {
            EnsureFolder("Assets/Tactics/Arts/PureRun", "Presentation");
            SkillVfxRecipe recipe = Require<SkillVfxRecipe>(
                $"{RecipeRoot}/BoneSpearSkillVfxRecipe.asset");
            ProjectileVisualProfile projectile = Require<ProjectileVisualProfile>(
                $"{ProjectileRoot}/BoneSpear.asset");
            for (int level = 1; level <= 3; level++)
            {
                string suffix = level == 1 ? string.Empty : $"_Lv{level}";
                BattlePresentationGraph presentation = ResetPresentation($"BoneSpear{suffix}");
                presentation.DefaultPreviewEntry = PresentationCueKind.Projectile;
                BuildActionEntry(presentation, UnitVisualAction.Cast);
                BuildHybridEntry(
                    presentation,
                    PresentationCueKind.CastCharge,
                    recipe,
                    SkillVfxCueKind.CastCharge,
                    Require<VisualCueProfile>($"{PilotoProfileRoot}/BoneSpearChargeLv{level}.asset"),
                    120f);
                BuildProjectileEntry(presentation, projectile, 12f, 0.25f, 220f);
                BuildHybridEntry(
                    presentation,
                    PresentationCueKind.PrimaryTargetHit,
                    recipe,
                    SkillVfxCueKind.PrimaryTargetHit,
                    Require<VisualCueProfile>($"{PilotoProfileRoot}/BoneSpearImpactLv{level}.asset"),
                    320f);
                ConfigurePreviewScenario(
                    presentation,
                    Require<GameObject>($"{UnitPrefabRoot}/PureRunNecromancer.prefab"),
                    Phase(
                        new[] { PresentationCueKind.CastCharge, PresentationCueKind.Action },
                        PresentationCueKind.Action,
                        PresentationPreviewAdvanceKind.Release),
                    Phase(PresentationCueKind.Projectile, PresentationPreviewAdvanceKind.Impact),
                    Phase(PresentationCueKind.PrimaryTargetHit, true));
                FinalizeGraphOnlyAbility(
                    presentation,
                    new[] { $"BoneSpear{suffix}_Graph_Ability" },
                    new[] { $"BoneSpear{suffix}_Graph" });
            }
            AssetDatabase.SaveAssets();
        }

        public static void BindSkeletonMageFireballConsumers()
        {
            BindOptionalGraphOnlyConfig(
                "SkeletonMageFireball_Lv1_Ability",
                "Fireball_Presentation");
            BindOptionalGraphOnlyConfig(
                "SkeletonMageFireball_Lv2_Ability",
                "Fireball_Lv2_Presentation");
            AssetDatabase.SaveAssets();
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
            presentation.DefaultPreviewEntry = PresentationCueKind.PrimaryTargetHit;
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
            ConfigurePreviewScenario(
                presentation,
                Require<GameObject>($"{UnitPrefabRoot}/PureRunNecromancer.prefab"),
                Phase(PresentationCueKind.Action, PresentationPreviewAdvanceKind.Release),
                Phase(PresentationCueKind.PrimaryTargetHit));
            EditorUtility.SetDirty(presentation);

            AssignToConfig(abilityName, presentation);
            ReplaceLegacyVisualCueNode(abilityName, PresentationCueKind.PrimaryTargetHit);
        }

        private static void BuildCueAbility(
            string abilityName,
            UnitVisualAction action,
            VisualCueProfile profile,
            PresentationCueKind cue,
            GameObject previewActor,
            bool playTargetHitReaction)
        {
            if (profile == null)
                throw new InvalidOperationException($"Missing VisualCueProfile for {abilityName}.");
            BattlePresentationGraph presentation = LoadOrCreate(abilityName);
            presentation.Nodes.Clear();
            presentation.Edges.Clear();
            presentation.DefaultPreviewEntry = cue;
            BuildActionEntry(presentation, action);

            var entry = Add<PresentationEntryNodeRecord>(presentation, PresentationNodeType.Entry, 0f, 220f);
            entry.Cue = cue;
            var fx = Add<PresentationPrefabFxNodeRecord>(presentation, PresentationNodeType.PrefabFx, 260f, 220f);
            fx.Profile = profile;
            var impact = Add<PresentationMarkerNodeRecord>(presentation, PresentationNodeType.Marker, 520f, 220f);
            impact.Marker = PresentationMarkerKind.Impact;
            var finish = Add<PresentationFinishNodeRecord>(presentation, PresentationNodeType.Finish, 760f, 220f);
            Connect(presentation, entry, fx, impact, finish);
            ConfigurePreviewScenario(
                presentation,
                previewActor,
                Phase(PresentationCueKind.Action, PresentationPreviewAdvanceKind.Release),
                Phase(cue, playTargetHitReaction));
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
            presentation.DefaultPreviewEntry = PresentationCueKind.Projectile;
            BuildActionEntry(presentation, UnitVisualAction.Ranged);
            BuildProjectileEntry(presentation, profile, 10f, 0.3f, 220f);
            ConfigurePreviewScenario(
                presentation,
                Require<GameObject>($"{UnitPrefabRoot}/PureRunHunter.prefab"),
                Phase(PresentationCueKind.Action, PresentationPreviewAdvanceKind.Release),
                Phase(
                    new[] { PresentationCueKind.Projectile },
                    PresentationCueKind.Projectile,
                    PresentationPreviewAdvanceKind.Impact,
                    true));
            EditorUtility.SetDirty(presentation);
            AssignToConfig(abilityName, presentation);
        }

        private static void BuildProjectileEntry(
            BattlePresentationGraph presentation,
            ProjectileVisualProfile profile,
            float speed,
            float fallbackTravelTime,
            float y)
        {
            var entry = Add<PresentationEntryNodeRecord>(presentation, PresentationNodeType.Entry, 0f, y);
            entry.Cue = PresentationCueKind.Projectile;
            var projectile = Add<PresentationProjectileNodeRecord>(
                presentation,
                PresentationNodeType.Projectile,
                280f,
                y);
            projectile.Profile = profile;
            projectile.Speed = speed;
            projectile.FallbackTravelTime = fallbackTravelTime;
            projectile.EmitImpactMarker = true;
            var finish = Add<PresentationFinishNodeRecord>(presentation, PresentationNodeType.Finish, 570f, y);
            Connect(presentation, entry, projectile, finish);
        }

        private static void BuildProceduralEntry(
            BattlePresentationGraph presentation,
            PresentationCueKind presentationCue,
            SkillVfxRecipe recipe,
            SkillVfxCueKind recipeCue,
            float y)
        {
            var entry = Add<PresentationEntryNodeRecord>(presentation, PresentationNodeType.Entry, 0f, y);
            entry.Cue = presentationCue;
            var vfx = Add<PresentationProceduralVfxNodeRecord>(
                presentation,
                PresentationNodeType.ProceduralVfx,
                280f,
                y);
            vfx.Recipe = recipe;
            vfx.Cue = recipeCue;
            var finish = Add<PresentationFinishNodeRecord>(presentation, PresentationNodeType.Finish, 570f, y);
            Connect(presentation, entry, vfx, finish);
        }

        private static void BuildHybridEntry(
            BattlePresentationGraph presentation,
            PresentationCueKind presentationCue,
            SkillVfxRecipe recipe,
            SkillVfxCueKind recipeCue,
            VisualCueProfile profile,
            float y)
        {
            if (profile == null)
                throw new InvalidOperationException($"Missing hybrid VFX profile for {presentationCue}.");

            var entry = Add<PresentationEntryNodeRecord>(presentation, PresentationNodeType.Entry, 0f, y);
            entry.Cue = presentationCue;
            var fork = Add<PresentationForkNodeRecord>(presentation, PresentationNodeType.Fork, 210f, y);
            var procedural = Add<PresentationProceduralVfxNodeRecord>(
                presentation,
                PresentationNodeType.ProceduralVfx,
                450f,
                y - 35f);
            procedural.Recipe = recipe;
            procedural.Cue = recipeCue;
            var prefabFx = Add<PresentationPrefabFxNodeRecord>(
                presentation,
                PresentationNodeType.PrefabFx,
                450f,
                y + 35f);
            prefabFx.Profile = profile;
            var join = Add<PresentationJoinNodeRecord>(presentation, PresentationNodeType.Join, 690f, y);
            fork.JoinNodeId = join.NodeId;
            var finish = Add<PresentationFinishNodeRecord>(presentation, PresentationNodeType.Finish, 900f, y);
            presentation.AddEdge(entry.NodeId, fork.NodeId);
            presentation.AddEdge(fork.NodeId, procedural.NodeId);
            presentation.AddEdge(fork.NodeId, prefabFx.NodeId);
            presentation.AddEdge(procedural.NodeId, join.NodeId);
            presentation.AddEdge(prefabFx.NodeId, join.NodeId);
            Connect(presentation, join, finish);
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

        private static BattlePresentationGraph ResetPresentation(string abilityName)
        {
            BattlePresentationGraph presentation = LoadOrCreate(abilityName);
            presentation.Nodes.Clear();
            presentation.Edges.Clear();
            return presentation;
        }

        private static void FinalizeGraphOnlyAbility(
            BattlePresentationGraph presentation,
            string[] configNames,
            string[] gameplayGraphNames,
            string[] optionalConfigNames = null)
        {
            EditorUtility.SetDirty(presentation);
            foreach (string configName in configNames)
                AssignGraphOnlyToConfig(configName, presentation, true);
            foreach (string configName in optionalConfigNames ?? Array.Empty<string>())
                AssignGraphOnlyToConfig(configName, presentation, false);
            foreach (string graphName in gameplayGraphNames)
                ClearGameplayProjectileProfile(graphName);
        }

        private static void AssignGraphOnlyToConfig(
            string configName,
            BattlePresentationGraph presentation,
            bool required)
        {
            SkillGraphAbilityConfig config = Load<SkillGraphAbilityConfig>(
                $"{ConfigRoot}/{configName}.asset");
            if (config == null)
            {
                if (required)
                    throw new InvalidOperationException($"Missing required ability config: {configName}");
                return;
            }

            var serialized = new SerializedObject(config);
            serialized.FindProperty("_presentationGraph").objectReferenceValue = presentation;
            serialized.FindProperty("_visualAction").enumValueIndex = (int)UnitVisualAction.None;
            serialized.FindProperty("_skillVfxRecipe").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        private static void BindOptionalGraphOnlyConfig(
            string configName,
            string presentationName)
        {
            SkillGraphAbilityConfig config = Load<SkillGraphAbilityConfig>(
                $"{ConfigRoot}/{configName}.asset");
            BattlePresentationGraph presentation = Load<BattlePresentationGraph>(
                $"{OutputRoot}/{presentationName}.asset");
            if (config == null || presentation == null)
                return;

            AssignGraphOnlyToConfig(configName, presentation, true);
        }

        private static void ClearGameplayProjectileProfile(string graphName)
        {
            SkillGraphAsset graph = Require<SkillGraphAsset>($"{GraphRoot}/{graphName}.asset");
            ProjectileLaunchNodeRecord[] projectiles = graph.Nodes
                .OfType<ProjectileLaunchNodeRecord>()
                .ToArray();
            if (projectiles.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Expected one projectile node in {graphName}, found {projectiles.Length}.");
            }
            projectiles[0].VisualProfile = null;
            EditorUtility.SetDirty(graph);
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

        private static void ConfigurePreviewScenario(
            BattlePresentationGraph graph,
            GameObject actorPrefab,
            params PresentationPreviewPhaseRecord[] phases)
        {
            graph.PreviewActorPrefab = actorPrefab;
            graph.PreviewTargetPrefab = Require<GameObject>(
                $"{UnitPrefabRoot}/PureRunGoatCharger.prefab");
            graph.PreviewPhases.Clear();
            graph.PreviewPhases.AddRange(phases);
            EditorUtility.SetDirty(graph);
        }

        private static PresentationPreviewPhaseRecord Phase(
            PresentationCueKind cue,
            PresentationPreviewAdvanceKind advanceKind)
        {
            return Phase(new[] { cue }, cue, advanceKind);
        }

        private static PresentationPreviewPhaseRecord Phase(
            PresentationCueKind cue,
            bool playTargetHitReaction = false)
        {
            return Phase(
                new[] { cue },
                cue,
                PresentationPreviewAdvanceKind.Complete,
                playTargetHitReaction);
        }

        private static PresentationPreviewPhaseRecord Phase(
            PresentationCueKind[] cues,
            PresentationCueKind continuationCue,
            PresentationPreviewAdvanceKind advanceKind,
            bool playTargetHitReaction = false)
        {
            var phase = new PresentationPreviewPhaseRecord
            {
                ContinuationCue = continuationCue,
                AdvanceKind = advanceKind,
                PlayTargetHitReaction = playTargetHitReaction
            };
            phase.Cues.AddRange(cues);
            return phase;
        }

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static T Require<T>(string path) where T : UnityEngine.Object
        {
            T value = Load<T>(path);
            return value != null
                ? value
                : throw new InvalidOperationException($"Missing required asset: {path}");
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
