using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Common.Units.Tween;
using Tactics.Editor.PresentationGraph;
using Tactics.Editor.SkillGraphEditor;
using Tactics.Runtime.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tactics.Editor
{
    /// <summary>
    /// Creates and wires the shared Pure Run tween and projectile visual assets.
    /// </summary>
    /// <remarks>
    /// The operation is deliberately idempotent so a prefab or graph can be repaired without
    /// hand-editing serialized Unity files.
    /// </remarks>
    public static class PureRunTweenAssetConfigurator
    {
        private const string Root = "Assets/Tactics/Arts/PureRun/Tween";
        private const string ProjectileRoot = Root + "/Projectiles";
        private const string SkillVfxRoot = Root + "/SkillVfx";
        private const string SkillVfxMaterialRoot = SkillVfxRoot + "/Materials";
        private const string SkillVfxRecipeRoot = SkillVfxRoot + "/Recipes";
        private const string RuntimeProjectileTextureRoot =
            "Assets/Tactics/Arts/PureRun/Textures/Projectiles";
        private const string ApprovedProjectileSourceRoot = "Tools/artworks/doge/concepts";
        private const string StandardProfilePath = Root + "/StandardUnitTweenProfile.asset";
        private const string SkillVfxShaderName = "Tactics/PureRun/SkillVfxPrimitive";
        private const string SkillVfxTransparentMaterialPath =
            SkillVfxMaterialRoot + "/SkillVfxTransparent.mat";
        private const string SkillVfxAdditiveMaterialPath =
            SkillVfxMaterialRoot + "/SkillVfxAdditive.mat";
        private const string SpearTexturePath = RuntimeProjectileTextureRoot + "/pure_run_spear_projectile.png";
        private const string ArcaneTexturePath = RuntimeProjectileTextureRoot +
            "/pure_run_arcane_bolt_projectile.png";
        private const string NecromancerOrbTexturePath = RuntimeProjectileTextureRoot +
            "/pure_run_necromancer_orb_projectile.png";
        private const string BoneSpearTexturePath = RuntimeProjectileTextureRoot +
            "/pure_run_bone_spear_projectile.png";

        private static readonly string[] PrefabPaths =
        {
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunHunter.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunNecromancer.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunMage.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunSkeletonWarrior.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunSkeletonMage.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunFireDemon.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunGoatCharger.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunGoatEliteCharger.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunGoatRanged.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunGoatAoe.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunGoatElitePoisonCaster.prefab",
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunGoatSupport.prefab"
        };

        private static readonly HashSet<string> NoneActions = new(StringComparer.Ordinal)
        {
            "Move_Graph_Ability",
            "PickupSpear_Graph_Ability"
        };

        private static readonly HashSet<string> MeleeActions = new(StringComparer.Ordinal)
        {
            "ChargeStrike_Lv1_Ability",
            "Counter_Graph_Ability",
            "MeleeAttack_Graph_Ability",
            "MultiStab_Graph_Ability",
            "MultiStab_Lv2_Graph_Ability",
            "SkeletonAttack_Lv1_Ability",
            "SkeletonAttack_Lv2_Ability",
            "SkeletonAttack_Lv3_Ability",
            "Thrust_Graph_Ability",
            "Thrust_Lv2_Graph_Ability",
            "Thrust_Lv3_Graph_Ability",
            "Uppercut_Graph_Ability"
        };

        private static readonly HashSet<string> RangedActions = new(StringComparer.Ordinal)
        {
            "HeavyShot_Graph_Ability",
            "PoisonSpear_Graph_Ability",
            "PoisonSpear_Lv2_Graph_Ability",
            "PoisonSpear_Lv3_Graph_Ability",
            "RangedAttack_Graph_Ability"
        };

        private static readonly HashSet<string> CastActions = new(StringComparer.Ordinal)
        {
            "AreaBlast_Lv1_Ability",
            "BoneShield_Graph_Ability",
            "BoneShield_Lv2_Graph_Ability",
            "BoneSpear_Graph_Ability",
            "BoneSpear_Lv2_Graph_Ability",
            "BoneSpear_Lv3_Graph_Ability",
            "ChargeHeal_Graph_Ability",
            "Curse_Graph_Ability",
            "Curse_Lv2_Graph_Ability",
            "Curse_Lv3_Graph_Ability",
            "Decoy_Graph_Ability",
            "Decoy_Lv2_Graph_Ability",
            "FearCurse_Graph_Ability",
            "FearCurse_Lv2_Graph_Ability",
            "FireDemonAttack_Ability",
            "Fireball_Graph_Ability",
            "Fireball_Lv1_Ability",
            "Fireball_Lv2_Ability",
            "Fireball_Lv3_Ability",
            "Freeze_Graph_Ability",
            "FrostNova_Graph_Ability",
            "IceArmor_Graph_Ability",
            "IceArmor_Lv2_Graph_Ability",
            "IceBolt_Graph_Ability",
            "IceBolt_Lv2_Graph_Ability",
            "IceBolt_Lv3_Graph_Ability",
            "Lightning_Graph_Ability",
            "Lightning_Lv2_Graph_Ability",
            "Lightning_Lv3_Graph_Ability",
            "MagicAttack_Graph_Ability",
            "Mark_Graph_Ability",
            "MeleeHeal_Graph_Ability",
            "RecoverSpear_Graph_Ability",
            "RecoverSpear_Lv2_Graph_Ability",
            "SkeletonMage_Graph_Ability",
            "SkeletonMage_Lv2_Graph_Ability",
            "SkeletonMageFireball_Lv1_Ability",
            "SkeletonMageFireball_Lv2_Ability",
            "SummonFireDemon_Graph_Ability",
            "SummonFireDemon_Lv2_Graph_Ability",
            "SummonSkeleton_Graph_Ability",
            "SummonSkeleton_Lv2_Graph_Ability",
            "SummonSkeleton_Lv3_Graph_Ability",
            "Teleport_Graph_Ability",
            "Teleport_Lv2_Graph_Ability"
        };

        private static readonly HashSet<string> PresentationGraphOnlyAbilities = new(StringComparer.Ordinal)
        {
            "Thrust_Graph_Ability",
            "Thrust_Lv2_Graph_Ability",
            "Thrust_Lv3_Graph_Ability",
            "Fireball_Graph_Ability",
            "Fireball_Lv1_Ability",
            "Fireball_Lv2_Ability",
            "Fireball_Lv3_Ability",
            "SkeletonMageFireball_Lv1_Ability",
            "SkeletonMageFireball_Lv2_Ability",
            "BoneSpear_Graph_Ability",
            "BoneSpear_Lv2_Graph_Ability",
            "BoneSpear_Lv3_Graph_Ability"
        };

        private static readonly HashSet<string> PresentationGraphOnlyProjectileGraphs = new(StringComparer.Ordinal)
        {
            "Fireball_Graph",
            "Fireball_Lv1_Graph",
            "Fireball_Lv2_Graph",
            "Fireball_Lv3_Graph",
            "BoneSpear_Graph",
            "BoneSpear_Lv2_Graph",
            "BoneSpear_Lv3_Graph"
        };

        [MenuItem("Tactics/Tools/Pure Run/Configure Tween Visual Assets")]
        public static void Configure()
        {
            EnsureFolder(Root);
            EnsureFolder(ProjectileRoot);
            EnsureFolder(SkillVfxMaterialRoot);
            EnsureFolder(SkillVfxRecipeRoot);
            EnsureFolder(RuntimeProjectileTextureRoot);
            CopyApprovedProjectileTextures();
            ConfigureTexture(SpearTexturePath);
            ConfigureTexture(ArcaneTexturePath);
            ConfigureTexture(NecromancerOrbTexturePath);
            ConfigureTexture(BoneSpearTexturePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var standardProfile = LoadOrCreate<StandardUnitTweenProfile>(StandardProfilePath);
            (Material transparentVfxMaterial, Material additiveVfxMaterial) =
                LoadOrCreateSkillVfxMaterials();
            var recipes = CreateSkillVfxRecipes(transparentVfxMaterial, additiveVfxMaterial);
            Sprite spear = AssetDatabase.LoadAssetAtPath<Sprite>(SpearTexturePath);
            Sprite arcane = AssetDatabase.LoadAssetAtPath<Sprite>(ArcaneTexturePath);
            Sprite necromancerOrb = AssetDatabase.LoadAssetAtPath<Sprite>(NecromancerOrbTexturePath);
            Sprite boneSpear = AssetDatabase.LoadAssetAtPath<Sprite>(BoneSpearTexturePath);
            var profiles = CreateProjectileProfiles(spear, arcane, boneSpear, additiveVfxMaterial);
            PilotoVfxSampleAssetBuilder.RestoreHybridProjectileProfiles();

            ConfigureAbilityActions(recipes);
            ConfigureProjectileGraphs(profiles);
            PureRunPresentationGraphAssetBuilder.RebuildThrustSamples();
            PureRunPresentationGraphAssetBuilder.RebuildFireballSamples();
            PureRunPresentationGraphAssetBuilder.RebuildBoneSpearSamples();
            ConfigurePrefabs(standardProfile);

            EditorUtility.SetDirty(standardProfile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static Dictionary<string, ProjectileVisualProfile> CreateProjectileProfiles(
            Sprite spear,
            Sprite arcane,
            Sprite boneSpear,
            Material additiveVfxMaterial)
        {
            var profiles = new Dictionary<string, ProjectileVisualProfile>(StringComparer.Ordinal)
            {
                ["Physical"] = ConfigureProjectileProfile(
                    "PhysicalBasic", spear, new Color(0.82f, 0.85f, 0.86f), 0.55f,
                    ProjectileTrajectoryStyle.PhysicalArc, 0.14f, true, 0f),
                ["Magic"] = ConfigureProjectileProfile(
                    "MagicBasic", arcane, new Color(0.42f, 0.68f, 1f), 0.72f,
                    ProjectileTrajectoryStyle.MagicStraight, 0.035f, false, 0.1f),
                ["Fire"] = ConfigureProjectileProfile(
                    "Fire", arcane, new Color(1f, 0.35f, 0.12f), 0.76f,
                    ProjectileTrajectoryStyle.MagicStraight, 0.05f, false, 0.12f),
                ["Ice"] = ConfigureProjectileProfile(
                    "Ice", arcane, new Color(0.5f, 0.92f, 1f), 0.72f,
                    ProjectileTrajectoryStyle.MagicStraight, 0.035f, false, 0.1f),
                ["Bone"] = ConfigureProjectileProfile(
                    "BoneSpear", boneSpear, Color.white, 1f,
                    ProjectileTrajectoryStyle.MagicStraight, 0f, true, 0f),
                ["Spear"] = ConfigureProjectileProfile(
                    "AmazonSpear", spear, Color.white, 0.72f,
                    ProjectileTrajectoryStyle.SpearArc, 0.07f, true, 0f),
                ["PoisonSpear"] = ConfigureProjectileProfile(
                    "AmazonPoisonSpear", spear, new Color(0.33f, 0.9f, 0.28f), 0.72f,
                    ProjectileTrajectoryStyle.SpearArc, 0.07f, true, 0.04f)
            };
            ConfigureFireProjectileProfile(profiles["Fire"], additiveVfxMaterial);
            ConfigureBoneSpearProjectileProfile(profiles["Bone"]);
            return profiles;
        }

        private static ProjectileVisualProfile ConfigureProjectileProfile(
            string name,
            Sprite sprite,
            Color tint,
            float scale,
            ProjectileTrajectoryStyle trajectory,
            float arcHeight,
            bool rotateAlongTangent,
            float pulseAmount)
        {
            string path = $"{ProjectileRoot}/{name}.asset";
            var profile = LoadOrCreate<ProjectileVisualProfile>(path);
            var serialized = new SerializedObject(profile);
            serialized.FindProperty("_visualKind").enumValueIndex = (int)ProjectileVisualKind.Sprite;
            serialized.FindProperty("_sprite").objectReferenceValue = sprite;
            serialized.FindProperty("_material").objectReferenceValue = null;
            serialized.FindProperty("_tint").colorValue = tint;
            serialized.FindProperty("_scale").floatValue = scale;
            serialized.FindProperty("_trajectoryStyle").enumValueIndex = (int)trajectory;
            serialized.FindProperty("_arcHeight").floatValue = arcHeight;
            serialized.FindProperty("_rotateAlongTangent").boolValue = rotateAlongTangent;
            serialized.FindProperty("_pulseAmount").floatValue = pulseAmount;
            serialized.FindProperty("_pulseCycles").floatValue = 2f;
            serialized.FindProperty("_sortingOrderOffset").intValue = 20;
            serialized.FindProperty("_particleTrail").FindPropertyRelative("_enabled").boolValue = false;
            serialized.FindProperty("_ghostTrail").FindPropertyRelative("_enabled").boolValue = false;
            if (serialized.hasModifiedProperties)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(profile);
            }
            return profile;
        }

        private static void ConfigureFireProjectileProfile(
            ProjectileVisualProfile profile,
            Material additiveVfxMaterial)
        {
            var serialized = new SerializedObject(profile);
            serialized.FindProperty("_visualKind").enumValueIndex = (int)ProjectileVisualKind.SoftDisc;
            serialized.FindProperty("_sprite").objectReferenceValue = null;
            serialized.FindProperty("_material").objectReferenceValue = additiveVfxMaterial;
            serialized.FindProperty("_tint").colorValue = new Color(1f, 0.36f, 0.10f, 1f);
            serialized.FindProperty("_scale").floatValue = 0.17f;
            serialized.FindProperty("_pulseAmount").floatValue = 0.06f;
            serialized.FindProperty("_pulseCycles").floatValue = 2f;

            SerializedProperty trail = serialized.FindProperty("_particleTrail");
            trail.FindPropertyRelative("_enabled").boolValue = true;
            trail.FindPropertyRelative("_emissionInterval").floatValue = 0.05f;
            trail.FindPropertyRelative("_maximumParticles").intValue = 3;
            trail.FindPropertyRelative("_lifetimeMin").floatValue = 0.12f;
            trail.FindPropertyRelative("_lifetimeMax").floatValue = 0.18f;
            trail.FindPropertyRelative("_sizeMin").floatValue = 0.025f;
            trail.FindPropertyRelative("_sizeMax").floatValue = 0.045f;
            trail.FindPropertyRelative("_color").colorValue = new Color(1f, 0.38f, 0.09f, 0.8f);
            trail.FindPropertyRelative("_randomSeed").longValue = 417u;
            if (serialized.hasModifiedProperties)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(profile);
            }
        }

        private static void ConfigureBoneSpearProjectileProfile(ProjectileVisualProfile profile)
        {
            var serialized = new SerializedObject(profile);
            SerializedProperty trail = serialized.FindProperty("_ghostTrail");
            trail.FindPropertyRelative("_enabled").boolValue = true;
            trail.FindPropertyRelative("_sampleInterval").floatValue = 0.055f;
            trail.FindPropertyRelative("_lifetime").floatValue = 0.12f;
            trail.FindPropertyRelative("_alpha").floatValue = 0.28f;
            trail.FindPropertyRelative("_scale").floatValue = 0.92f;
            trail.FindPropertyRelative("_maximumAlive").intValue = 2;
            if (serialized.hasModifiedProperties)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(profile);
            }
        }

        private static void ConfigureAbilityActions(
            IReadOnlyDictionary<string, SkillVfxRecipe> recipes)
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:SkillGraphAbilityConfig",
                new[] { "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<SkillGraphAbilityConfig>(path);
                if (config == null)
                    continue;

                UnitVisualAction action;
                if (!TryResolveAction(config.name, out action))
                {
                    action = UnitVisualAction.None;
                    TLog.Warning(
                        $"[PureRunTweenAssetConfigurator] Unknown ability '{config.name}' remains None.");
                }
                var serialized = new SerializedObject(config);
                bool changed = false;
                SerializedProperty visualAction = serialized.FindProperty("_visualAction");
                SerializedProperty skillVfxRecipe = serialized.FindProperty("_skillVfxRecipe");
                if (PresentationGraphOnlyAbilities.Contains(config.name))
                {
                    if (visualAction.enumValueIndex != (int)UnitVisualAction.None)
                    {
                        visualAction.enumValueIndex = (int)UnitVisualAction.None;
                        changed = true;
                    }
                    if (skillVfxRecipe.objectReferenceValue != null)
                    {
                        skillVfxRecipe.objectReferenceValue = null;
                        changed = true;
                    }
                    if (changed)
                    {
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(config);
                    }
                    continue;
                }
                if (visualAction.enumValueIndex != (int)action)
                {
                    visualAction.enumValueIndex = (int)action;
                    changed = true;
                }
                SkillVfxRecipe recipe = ResolveSkillVfxRecipe(
                    config.name,
                    action,
                    skillVfxRecipe.objectReferenceValue as SkillVfxRecipe,
                    recipes);
                if (recipe != null)
                {
                    if (skillVfxRecipe.objectReferenceValue != recipe)
                    {
                        skillVfxRecipe.objectReferenceValue = recipe;
                        changed = true;
                    }
                }
                if (changed)
                {
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(config);
                }
            }
        }

        private static SkillVfxRecipe ResolveSkillVfxRecipe(
            string assetName,
            UnitVisualAction action,
            SkillVfxRecipe existingRecipe,
            IReadOnlyDictionary<string, SkillVfxRecipe> recipes)
        {
            if (assetName.StartsWith("Fireball", StringComparison.Ordinal))
                return recipes["Fireball"];
            if (assetName.StartsWith("BoneSpear", StringComparison.Ordinal))
                return recipes["BoneSpear"];
            if (assetName.StartsWith("Thrust", StringComparison.Ordinal))
                return recipes["Thrust"];
            if (existingRecipe != null)
                return existingRecipe;
            return action == UnitVisualAction.Cast ? recipes["DefaultCast"] : null;
        }

        private static bool TryResolveAction(string assetName, out UnitVisualAction action)
        {
            if (NoneActions.Contains(assetName))
            {
                action = UnitVisualAction.None;
                return true;
            }
            if (MeleeActions.Contains(assetName))
            {
                action = UnitVisualAction.Melee;
                return true;
            }
            if (RangedActions.Contains(assetName))
            {
                action = UnitVisualAction.Ranged;
                return true;
            }
            if (CastActions.Contains(assetName))
            {
                action = UnitVisualAction.Cast;
                return true;
            }

            action = UnitVisualAction.None;
            return false;
        }

        private static void CopyApprovedProjectileTextures()
        {
            CopyApprovedProjectileTexture(
                ApprovedProjectileSourceRoot + "/doge_capsule_hunter_spear_projectile_color_v01.png",
                SpearTexturePath);
            CopyApprovedProjectileTexture(
                ApprovedProjectileSourceRoot + "/doge_capsule_mage_arcane_bolt_projectile_color_v02.png",
                ArcaneTexturePath);
            CopyApprovedProjectileTexture(
                ApprovedProjectileSourceRoot + "/doge_capsule_necromancer_pale_orb_projectile_color_v03.png",
                NecromancerOrbTexturePath);
            CopyApprovedProjectileTexture(
                ApprovedProjectileSourceRoot +
                "/doge_capsule_necromancer_bone_spear_projectile_color_v01.png",
                BoneSpearTexturePath);
            AssetDatabase.Refresh();
        }

        private static void CopyApprovedProjectileTexture(string sourcePath, string destinationPath)
        {
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Approved projectile source is missing.", sourcePath);

            if (File.Exists(destinationPath) &&
                File.ReadAllBytes(sourcePath).SequenceEqual(File.ReadAllBytes(destinationPath)))
            {
                return;
            }

            File.Copy(sourcePath, destinationPath, true);
        }

        private static void ConfigureProjectileGraphs(
            IReadOnlyDictionary<string, ProjectileVisualProfile> profiles)
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:SkillGraphAsset",
                new[] { "Assets/Tactics/Battle/Abilities/SkillGraphs" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var graph = AssetDatabase.LoadAssetAtPath<SkillGraphAsset>(path);
                if (graph == null)
                    continue;

                if (graph.name.StartsWith("PoisonSpear", StringComparison.Ordinal))
                    EnsurePoisonSpearProjectile(graph, profiles["PoisonSpear"]);
                else if (graph.name.StartsWith("FireDemonAttack", StringComparison.Ordinal))
                    EnsureProjectileBeforeEffect(graph, profiles["Fire"]);

                ProjectileVisualProfile profile = ResolveProjectileProfile(graph.name, profiles);
                bool changed = false;
                foreach (var node in graph.Nodes.OfType<ProjectileLaunchNodeRecord>())
                {
                    if (PresentationGraphOnlyProjectileGraphs.Contains(graph.name))
                    {
                        if (node.VisualProfile != null)
                        {
                            node.VisualProfile = null;
                            changed = true;
                        }
                        continue;
                    }
                    if (profile != null && node.VisualProfile != profile)
                    {
                        node.VisualProfile = profile;
                        changed = true;
                    }
                }

                if (changed)
                    EditorUtility.SetDirty(graph);
            }
        }

        private static ProjectileVisualProfile ResolveProjectileProfile(
            string graphName,
            IReadOnlyDictionary<string, ProjectileVisualProfile> profiles)
        {
            if (graphName.StartsWith("PoisonSpear", StringComparison.Ordinal))
                return profiles["PoisonSpear"];
            if (graphName.StartsWith("BoneSpear", StringComparison.Ordinal))
                return profiles["Bone"];
            if (graphName.StartsWith("IceBolt", StringComparison.Ordinal))
                return profiles["Ice"];
            if (graphName.StartsWith("Fireball", StringComparison.Ordinal) ||
                graphName.StartsWith("FireDemonAttack", StringComparison.Ordinal) ||
                graphName.StartsWith("SkeletonMageFireball", StringComparison.Ordinal))
            {
                return profiles["Fire"];
            }
            if (graphName.StartsWith("MagicAttack", StringComparison.Ordinal) ||
                graphName.StartsWith("SkeletonMage", StringComparison.Ordinal))
            {
                return profiles["Magic"];
            }
            if (graphName.StartsWith("RangedAttack", StringComparison.Ordinal) ||
                graphName.StartsWith("HeavyShot", StringComparison.Ordinal))
            {
                return profiles["Physical"];
            }
            return null;
        }

        private static void EnsurePoisonSpearProjectile(
            SkillGraphAsset graph,
            ProjectileVisualProfile profile)
        {
            var existing = graph.Nodes.OfType<ProjectileLaunchNodeRecord>().FirstOrDefault();
            if (existing != null)
            {
                existing.VisualProfile = profile;
                existing.Speed = 7f;
                return;
            }

            var effect = graph.Nodes.OfType<AmazonSkillNodeRecord>().FirstOrDefault();
            if (effect == null)
                return;

            List<SkillGraphEdgeRecord> incoming = graph.GetEdgesTo(effect.NodeId);
            var projectile = (ProjectileLaunchNodeRecord)graph.AddNode(
                SkillGraphNodeType.ProjectileLaunch,
                effect.Position + Vector2.left * 360f);
            projectile.VisualProfile = profile;
            projectile.Speed = 7f;
            projectile.TravelTime = 0.3f;
            projectile.DropOnHit = false;
            projectile.RequiresLineOfSight = true;
            var onHit = graph.AddNode(SkillGraphNodeType.OnHit, effect.Position + Vector2.left * 180f);

            foreach (var edge in incoming)
            {
                graph.RemoveEdge(edge.EdgeId);
                graph.AddEdge(edge.SourceNodeId, projectile.NodeId, edge.PortType);
            }
            graph.AddEdge(projectile.NodeId, onHit.NodeId);
            graph.AddEdge(onHit.NodeId, effect.NodeId);
            EditorUtility.SetDirty(graph);
        }

        private static void EnsureProjectileBeforeEffect(
            SkillGraphAsset graph,
            ProjectileVisualProfile profile)
        {
            var existing = graph.Nodes.OfType<ProjectileLaunchNodeRecord>().FirstOrDefault();
            if (existing != null)
            {
                existing.VisualProfile = profile;
                existing.Speed = 8f;
                EditorUtility.SetDirty(graph);
                return;
            }

            SkillGraphNodeRecord effect = graph.Nodes
                .FirstOrDefault(node => node is ApplyDamageNodeRecord or MageSkillNodeRecord);
            if (effect == null)
                return;

            List<SkillGraphEdgeRecord> incoming = graph.GetEdgesTo(effect.NodeId);
            if (incoming.Count == 0)
                return;

            var projectile = (ProjectileLaunchNodeRecord)graph.AddNode(
                SkillGraphNodeType.ProjectileLaunch,
                effect.Position + Vector2.left * 360f);
            projectile.VisualProfile = profile;
            projectile.Speed = 8f;
            projectile.TravelTime = 0.3f;
            projectile.DropOnHit = false;
            projectile.RequiresLineOfSight = true;
            var onHit = graph.AddNode(SkillGraphNodeType.OnHit, effect.Position + Vector2.left * 180f);

            foreach (var edge in incoming)
            {
                graph.RemoveEdge(edge.EdgeId);
                graph.AddEdge(edge.SourceNodeId, projectile.NodeId, edge.PortType);
            }
            graph.AddEdge(projectile.NodeId, onHit.NodeId);
            graph.AddEdge(onHit.NodeId, effect.NodeId);
            EditorUtility.SetDirty(graph);
        }

        private static void ConfigurePrefabs(StandardUnitTweenProfile profile)
        {
            foreach (string path in PrefabPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                    throw new InvalidOperationException($"Pure Run prefab is missing: {path}");

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var sprite = root.GetComponentsInChildren<SpriteRenderer>(true)
                        .FirstOrDefault(candidate => candidate.gameObject.name == "Sprite");
                    if (sprite == null)
                        throw new InvalidOperationException($"Sprite renderer is missing: {path}");

                    // Pure Run prefabs are variants whose inherited Sprite child cannot be
                    // reparented without breaking the variant relationship. Treat that isolated
                    // child transform as VisualRoot; Shadow and Explosion remain sibling objects.
                    Transform visualRoot = sprite.transform;

                    var visual = root.GetComponent<UnitTweenVisual>();
                    if (visual == null)
                        visual = root.AddComponent<UnitTweenVisual>();
                    var serialized = new SerializedObject(visual);
                    serialized.FindProperty("_visualRoot").objectReferenceValue = visualRoot;
                    serialized.FindProperty("_primaryRenderer").objectReferenceValue = sprite;
                    serialized.FindProperty("_profile").objectReferenceValue = profile;
                    serialized.ApplyModifiedPropertiesWithoutUndo();

                    Transform staleOverlay = visualRoot.Find("GlowOverlay");
                    if (staleOverlay != null)
                        UnityEngine.Object.DestroyImmediate(staleOverlay.gameObject);

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static Dictionary<string, SkillVfxRecipe> CreateSkillVfxRecipes(
            Material transparentMaterial,
            Material additiveMaterial)
        {
            var defaultCastBindings = new[]
            {
                CreateCastChargeBinding(new Color(0.32f, 0.62f, 0.85f, 1f))
            };
            var fireballBindings = new[]
            {
                CreateCastChargeBinding(new Color(1.00f, 0.36f, 0.08f, 1f)),
                new VfxBindingSpec(
                    SkillVfxCueKind.ProjectileImpact,
                    new VfxLayerSpec
                    {
                        PrimitiveKind = SkillVfxPrimitiveKind.RadialCore,
                        ShapeMode = SkillVfxShapeMode.SoftDisc,
                        Color = new Color(1f, 0.86f, 0.58f, 1f),
                        StartSize = 0.16f,
                        UseMiddleKey = true,
                        MiddleSize = 0.12f,
                        MiddleTime = 0.04f,
                        PeakSize = 0.22f,
                        PeakTime = 0.10f,
                        EndSize = 0.22f,
                        Duration = 0.28f,
                        StartAlpha = 1f,
                        MiddleAlpha = 1f,
                        PeakAlpha = 1f,
                        EndAlpha = 0f,
                        BlockingMarker = 0.10f,
                        Emission = 2.2f,
                        Softness = 0.22f
                    },
                    new VfxLayerSpec
                    {
                        PrimitiveKind = SkillVfxPrimitiveKind.RadialRing,
                        ShapeMode = SkillVfxShapeMode.Ring,
                        Color = new Color(1f, 0.24f, 0.05f, 1f),
                        StartSize = 0.10f,
                        PeakSize = 0.32f,
                        EndSize = 0.48f,
                        PeakTime = 0.10f,
                        Duration = 0.28f,
                        StartAlpha = 0f,
                        PeakAlpha = 0.85f,
                        EndAlpha = 0f,
                        BlockingMarker = 0.10f,
                        RadialInner = 0.72f,
                        RadialOuter = 1f,
                        Softness = 0.11f,
                        Emission = 1.6f
                    },
                    new VfxLayerSpec
                    {
                        PrimitiveKind = SkillVfxPrimitiveKind.ParticleBurst,
                        ShapeMode = SkillVfxShapeMode.Solid,
                        Color = new Color(1f, 0.48f, 0.08f, 1f),
                        Duration = 0.28f,
                        ParticleCount = 3,
                        ParticleSize = 0.035f,
                        ParticleSpeed = 0.34f,
                        ParticleLifetimeMin = 0.16f,
                        ParticleLifetimeMax = 0.28f,
                        ParticleDrag = 0.15f,
                        RandomSeed = 7301u,
                        SortingOrderOffset = 31
                    }),
                new VfxBindingSpec(
                    SkillVfxCueKind.SecondaryTargetHit,
                    new VfxLayerSpec
                    {
                        PrimitiveKind = SkillVfxPrimitiveKind.RadialRing,
                        ShapeMode = SkillVfxShapeMode.Ring,
                        Color = new Color(1f, 0.30f, 0.08f, 1f),
                        StartSize = 0.08f,
                        PeakSize = 0.22f,
                        EndSize = 0.22f,
                        PeakTime = 0.06f,
                        Duration = 0.16f,
                        StartAlpha = 0f,
                        PeakAlpha = 0.62f,
                        EndAlpha = 0f,
                        RadialInner = 0.72f,
                        RadialOuter = 1f,
                        Softness = 0.12f,
                        Emission = 1.2f
                    }),
                new VfxBindingSpec(
                    SkillVfxCueKind.ConditionalDetonation,
                    new VfxLayerSpec
                    {
                        PrimitiveKind = SkillVfxPrimitiveKind.RadialRing,
                        ShapeMode = SkillVfxShapeMode.Ring,
                        Color = new Color(1f, 0.55f, 0.13f, 1f),
                        StartSize = 0.34f,
                        PeakSize = 0.13f,
                        EndSize = 0.13f,
                        PeakTime = 0.06f,
                        Duration = 0.14f,
                        StartAlpha = 0.62f,
                        PeakAlpha = 0.90f,
                        EndAlpha = 0f,
                        BlockingMarker = 0.06f,
                        RadialInner = 0.72f,
                        RadialOuter = 1f,
                        Softness = 0.10f,
                        Emission = 1.6f
                    })
            };

            var boneSpearBindings = new[]
            {
                CreateCastChargeBinding(new Color(0.68f, 0.90f, 0.88f, 1f)),
                new VfxBindingSpec(
                    SkillVfxCueKind.PrimaryTargetHit,
                    new VfxLayerSpec
                    {
                        PrimitiveKind = SkillVfxPrimitiveKind.CrossFlash,
                        BlendMode = SkillVfxBlendMode.Transparent,
                        ShapeMode = SkillVfxShapeMode.Solid,
                        Color = new Color(0.94f, 0.91f, 0.78f, 1f),
                        StartSize = 0.05f,
                        PeakSize = 0.15f,
                        EndSize = 0.15f,
                        PeakTime = 0.05f,
                        Duration = 0.13f,
                        StartAlpha = 0.25f,
                        PeakAlpha = 1f,
                        EndAlpha = 0f,
                        BlockingMarker = 0.05f,
                        RootWidth = 0.018f,
                        Angle = 35f
                    },
                    new VfxLayerSpec
                    {
                        PrimitiveKind = SkillVfxPrimitiveKind.ParticleBurst,
                        Color = new Color(0.88f, 0.92f, 0.84f, 1f),
                        Duration = 0.13f,
                        ParticleCount = 2,
                        ParticleSize = 0.024f,
                        ParticleSpeed = 0.22f,
                        ParticleLifetimeMin = 0.08f,
                        ParticleLifetimeMax = 0.13f,
                        RandomSeed = 7302u,
                        MaximumInstances = 4,
                        SortingOrderOffset = 31
                    })
            };

            var thrustBindings = new[]
            {
                new VfxBindingSpec(
                    SkillVfxCueKind.DirectionalStrike,
                    new VfxLayerSpec
                    {
                        PrimitiveKind = SkillVfxPrimitiveKind.TaperedLine,
                        BlendMode = SkillVfxBlendMode.Transparent,
                        ShapeMode = SkillVfxShapeMode.Solid,
                        Color = new Color(0.72f, 0.50f, 0.22f, 1f),
                        StartSize = 0f,
                        PeakSize = 1f,
                        EndSize = 1f,
                        PeakTime = 0.065f,
                        Duration = 0.16f,
                        StartAlpha = 0.12f,
                        PeakAlpha = 0.55f,
                        EndAlpha = 0f,
                        BlockingMarker = 0.065f,
                        RootWidth = 0.060f,
                        TipWidth = 0.014f,
                        Emission = 0.25f,
                        SortingOrderOffset = 29
                    },
                    new VfxLayerSpec
                    {
                        PrimitiveKind = SkillVfxPrimitiveKind.TaperedLine,
                        BlendMode = SkillVfxBlendMode.Transparent,
                        ShapeMode = SkillVfxShapeMode.Solid,
                        Color = new Color(0.98f, 0.94f, 0.78f, 1f),
                        StartSize = 0f,
                        PeakSize = 1f,
                        EndSize = 1f,
                        PeakTime = 0.065f,
                        Duration = 0.16f,
                        StartAlpha = 0.20f,
                        PeakAlpha = 0.85f,
                        EndAlpha = 0f,
                        BlockingMarker = 0.065f,
                        RootWidth = 0.045f,
                        TipWidth = 0.010f,
                        Emission = 0.45f,
                        SortingOrderOffset = 30
                    }),
                new VfxBindingSpec(
                    SkillVfxCueKind.PrimaryTargetHit,
                    new VfxLayerSpec
                    {
                        PrimitiveKind = SkillVfxPrimitiveKind.CrossFlash,
                        BlendMode = SkillVfxBlendMode.Transparent,
                        ShapeMode = SkillVfxShapeMode.Solid,
                        Color = new Color(0.93f, 0.72f, 0.35f, 1f),
                        StartSize = 0.04f,
                        PeakSize = 0.13f,
                        EndSize = 0.13f,
                        PeakTime = 0.05f,
                        Duration = 0.13f,
                        StartAlpha = 0.20f,
                        PeakAlpha = 0.85f,
                        EndAlpha = 0f,
                        BlockingMarker = 0.05f,
                        RootWidth = 0.016f,
                        Angle = 35f
                    })
            };

            return new Dictionary<string, SkillVfxRecipe>(StringComparer.Ordinal)
            {
                ["DefaultCast"] = ConfigureSkillVfxRecipe(
                    SkillVfxRecipeRoot + "/DefaultCastSkillVfxRecipe.asset",
                    transparentMaterial,
                    additiveMaterial,
                    defaultCastBindings),
                ["Fireball"] = ConfigureSkillVfxRecipe(
                    SkillVfxRecipeRoot + "/FireballSkillVfxRecipe.asset",
                    transparentMaterial,
                    additiveMaterial,
                    fireballBindings),
                ["BoneSpear"] = ConfigureSkillVfxRecipe(
                    SkillVfxRecipeRoot + "/BoneSpearSkillVfxRecipe.asset",
                    transparentMaterial,
                    additiveMaterial,
                    boneSpearBindings),
                ["Thrust"] = ConfigureSkillVfxRecipe(
                    SkillVfxRecipeRoot + "/ThrustSkillVfxRecipe.asset",
                    transparentMaterial,
                    additiveMaterial,
                    thrustBindings)
            };
        }

        private static VfxBindingSpec CreateCastChargeBinding(Color color)
        {
            return new VfxBindingSpec(
                SkillVfxCueKind.CastCharge,
                new VfxLayerSpec
                {
                    PrimitiveKind = SkillVfxPrimitiveKind.RadialRing,
                    BlendMode = SkillVfxBlendMode.Additive,
                    ShapeMode = SkillVfxShapeMode.Ring,
                    Color = color,
                    StartSize = 0.22f,
                    PeakSize = 0.42f,
                    EndSize = 0.48f,
                    PeakTime = 0.28f,
                    Duration = 0.54f,
                    StartAlpha = 0f,
                    PeakAlpha = 0.36f,
                    EndAlpha = 0f,
                    BlockingMarker = 0f,
                    RadialInner = 0.72f,
                    RadialOuter = 1f,
                    Softness = 0.12f,
                    Emission = 0.7f,
                    MaximumInstances = 1,
                    SortingOrderOffset = -2
                });
        }

        private static SkillVfxRecipe ConfigureSkillVfxRecipe(
            string path,
            Material transparentMaterial,
            Material additiveMaterial,
            IReadOnlyList<VfxBindingSpec> bindings)
        {
            var recipe = LoadOrCreate<SkillVfxRecipe>(path);
            var serialized = new SerializedObject(recipe);
            serialized.FindProperty("_transparentMaterial").objectReferenceValue = transparentMaterial;
            serialized.FindProperty("_additiveMaterial").objectReferenceValue = additiveMaterial;
            SerializedProperty serializedBindings = serialized.FindProperty("_bindings");
            serializedBindings.arraySize = bindings.Count;
            for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
            {
                VfxBindingSpec binding = bindings[bindingIndex];
                SerializedProperty serializedBinding = serializedBindings.GetArrayElementAtIndex(bindingIndex);
                serializedBinding.FindPropertyRelative("_cue").enumValueIndex = (int)binding.Cue;
                SerializedProperty serializedLayers = serializedBinding.FindPropertyRelative("_layers");
                serializedLayers.arraySize = binding.Layers.Count;
                for (int layerIndex = 0; layerIndex < binding.Layers.Count; layerIndex++)
                    WriteVfxLayer(serializedLayers.GetArrayElementAtIndex(layerIndex), binding.Layers[layerIndex]);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(recipe);
            return recipe;
        }

        private static void WriteVfxLayer(SerializedProperty layer, VfxLayerSpec value)
        {
            layer.FindPropertyRelative("_primitiveKind").enumValueIndex = (int)value.PrimitiveKind;
            layer.FindPropertyRelative("_blendMode").enumValueIndex = (int)value.BlendMode;
            layer.FindPropertyRelative("_shapeMode").enumValueIndex = (int)value.ShapeMode;
            layer.FindPropertyRelative("_color").colorValue = value.Color;
            layer.FindPropertyRelative("_secondaryColor").colorValue = value.SecondaryColor;
            layer.FindPropertyRelative("_startSize").floatValue = value.StartSize;
            layer.FindPropertyRelative("_useMiddleKey").boolValue = value.UseMiddleKey;
            layer.FindPropertyRelative("_middleSize").floatValue = value.MiddleSize;
            layer.FindPropertyRelative("_middleTime").floatValue = value.MiddleTime;
            layer.FindPropertyRelative("_peakSize").floatValue = value.PeakSize;
            layer.FindPropertyRelative("_endSize").floatValue = value.EndSize;
            layer.FindPropertyRelative("_peakTime").floatValue = value.PeakTime;
            layer.FindPropertyRelative("_duration").floatValue = value.Duration;
            layer.FindPropertyRelative("_blockingMarker").floatValue = value.BlockingMarker;
            layer.FindPropertyRelative("_startAlpha").floatValue = value.StartAlpha;
            layer.FindPropertyRelative("_middleAlpha").floatValue = value.MiddleAlpha;
            layer.FindPropertyRelative("_peakAlpha").floatValue = value.PeakAlpha;
            layer.FindPropertyRelative("_endAlpha").floatValue = value.EndAlpha;
            layer.FindPropertyRelative("_radialInner").floatValue = value.RadialInner;
            layer.FindPropertyRelative("_radialOuter").floatValue = value.RadialOuter;
            layer.FindPropertyRelative("_softness").floatValue = value.Softness;
            layer.FindPropertyRelative("_emission").floatValue = value.Emission;
            layer.FindPropertyRelative("_angle").floatValue = value.Angle;
            layer.FindPropertyRelative("_rootWidth").floatValue = value.RootWidth;
            layer.FindPropertyRelative("_tipWidth").floatValue = value.TipWidth;
            layer.FindPropertyRelative("_particleCount").intValue = value.ParticleCount;
            layer.FindPropertyRelative("_particleSize").floatValue = value.ParticleSize;
            layer.FindPropertyRelative("_particleSpeed").floatValue = value.ParticleSpeed;
            layer.FindPropertyRelative("_particleLifetimeMin").floatValue = value.ParticleLifetimeMin;
            layer.FindPropertyRelative("_particleLifetimeMax").floatValue = value.ParticleLifetimeMax;
            layer.FindPropertyRelative("_particleDrag").floatValue = value.ParticleDrag;
            layer.FindPropertyRelative("_randomSeed").longValue = value.RandomSeed;
            layer.FindPropertyRelative("_maximumInstances").intValue = value.MaximumInstances;
            layer.FindPropertyRelative("_sortingOrderOffset").intValue = value.SortingOrderOffset;
        }

        private static (Material transparent, Material additive) LoadOrCreateSkillVfxMaterials()
        {
            Shader shader = Shader.Find(SkillVfxShaderName);
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException(
                    $"Skill VFX shader is missing or unsupported: {SkillVfxShaderName}");

            Material transparent = LoadOrCreateSkillVfxMaterial(
                SkillVfxTransparentMaterialPath,
                "SkillVfxTransparent",
                shader,
                UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            Material additive = LoadOrCreateSkillVfxMaterial(
                SkillVfxAdditiveMaterialPath,
                "SkillVfxAdditive",
                shader,
                UnityEngine.Rendering.BlendMode.One);
            return (transparent, additive);
        }

        private static Material LoadOrCreateSkillVfxMaterial(
            string path,
            string name,
            Shader shader,
            UnityEngine.Rendering.BlendMode destinationBlend)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)destinationBlend);
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void ConfigureTexture(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Projectile texture is missing.", path);

            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 128f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.SaveAndReimport();
        }

        private static void EnsureFolder(string path)
        {
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (AssetDatabase.IsValidFolder(path))
                return;
            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private sealed class VfxBindingSpec
        {
            public SkillVfxCueKind Cue { get; }
            public IReadOnlyList<VfxLayerSpec> Layers { get; }

            public VfxBindingSpec(SkillVfxCueKind cue, params VfxLayerSpec[] layers)
            {
                Cue = cue;
                Layers = layers;
            }
        }

        private sealed class VfxLayerSpec
        {
            public SkillVfxPrimitiveKind PrimitiveKind = SkillVfxPrimitiveKind.RadialCore;
            public SkillVfxBlendMode BlendMode = SkillVfxBlendMode.Additive;
            public SkillVfxShapeMode ShapeMode = SkillVfxShapeMode.Solid;
            public Color Color = Color.white;
            public Color SecondaryColor = Color.white;
            public float StartSize = 0.05f;
            public bool UseMiddleKey;
            public float MiddleSize = 0.10f;
            public float MiddleTime = 0.04f;
            public float PeakSize = 0.15f;
            public float EndSize = 0.20f;
            public float PeakTime = 0.05f;
            public float Duration = 0.15f;
            public float BlockingMarker;
            public float StartAlpha;
            public float MiddleAlpha = 1f;
            public float PeakAlpha = 1f;
            public float EndAlpha;
            public float RadialInner = 0.5f;
            public float RadialOuter = 1f;
            public float Softness = 0.12f;
            public float Emission = 1f;
            public float Angle = 35f;
            public float RootWidth = 0.045f;
            public float TipWidth = 0.010f;
            public int ParticleCount;
            public float ParticleSize = 0.03f;
            public float ParticleSpeed = 0.2f;
            public float ParticleLifetimeMin = 0.12f;
            public float ParticleLifetimeMax = 0.18f;
            public float ParticleDrag;
            public uint RandomSeed = 1u;
            public int MaximumInstances = 16;
            public int SortingOrderOffset = 30;
        }
    }
}
