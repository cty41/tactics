using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Common.Units.Tween;
using Tactics.Runtime.Utilities;
using UnityEditor;
using UnityEngine;

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
        private const string RuntimeProjectileTextureRoot =
            "Assets/Tactics/Arts/PureRun/Textures/Projectiles";
        private const string ApprovedProjectileSourceRoot = "Tools/artworks/doge/concepts";
        private const string StandardProfilePath = Root + "/StandardUnitTweenProfile.asset";
        private const string GlowOverlayShaderName = "Tactics/PureRun/GlowOverlay";
        private const string GlowOverlayMaterialPath = Root + "/PureRunGlowOverlay.mat";
        private const string SpearTexturePath = RuntimeProjectileTextureRoot + "/pure_run_spear_projectile.png";
        private const string ArcaneTexturePath = RuntimeProjectileTextureRoot +
            "/pure_run_arcane_bolt_projectile.png";
        private const string NecromancerOrbTexturePath = RuntimeProjectileTextureRoot +
            "/pure_run_necromancer_orb_projectile.png";

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

        [MenuItem("Tactics/Tools/Pure Run/Configure Tween Visual Assets")]
        public static void Configure()
        {
            EnsureFolder(Root);
            EnsureFolder(ProjectileRoot);
            EnsureFolder(RuntimeProjectileTextureRoot);
            CopyApprovedProjectileTextures();
            ConfigureTexture(SpearTexturePath);
            ConfigureTexture(ArcaneTexturePath);
            ConfigureTexture(NecromancerOrbTexturePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var standardProfile = LoadOrCreate<StandardUnitTweenProfile>(StandardProfilePath);
            Material glowOverlayMaterial = LoadOrCreateGlowOverlayMaterial();
            Sprite spear = AssetDatabase.LoadAssetAtPath<Sprite>(SpearTexturePath);
            Sprite arcane = AssetDatabase.LoadAssetAtPath<Sprite>(ArcaneTexturePath);
            Sprite necromancerOrb = AssetDatabase.LoadAssetAtPath<Sprite>(NecromancerOrbTexturePath);
            var profiles = CreateProjectileProfiles(spear, arcane, necromancerOrb);

            ConfigureAbilityActions();
            ConfigureProjectileGraphs(profiles);
            ConfigurePrefabs(standardProfile, glowOverlayMaterial);

            EditorUtility.SetDirty(standardProfile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tactics/Tools/Pure Run/Repair Glow Overlay Assets")]
        public static void ConfigureGlowOverlayAssets()
        {
            EnsureFolder(Root);
            var standardProfile = AssetDatabase.LoadAssetAtPath<StandardUnitTweenProfile>(StandardProfilePath);
            if (standardProfile == null)
                throw new InvalidOperationException($"Standard tween profile is missing: {StandardProfilePath}");

            Material glowOverlayMaterial = LoadOrCreateGlowOverlayMaterial();
            ConfigurePrefabs(standardProfile, glowOverlayMaterial);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static Dictionary<string, ProjectileVisualProfile> CreateProjectileProfiles(
            Sprite spear,
            Sprite arcane,
            Sprite necromancerOrb)
        {
            return new Dictionary<string, ProjectileVisualProfile>(StringComparer.Ordinal)
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
                    "BoneSpear", necromancerOrb, Color.white, 0.72f,
                    ProjectileTrajectoryStyle.MagicStraight, 0f, true, 0.08f),
                ["Spear"] = ConfigureProjectileProfile(
                    "AmazonSpear", spear, Color.white, 0.72f,
                    ProjectileTrajectoryStyle.SpearArc, 0.07f, true, 0f),
                ["PoisonSpear"] = ConfigureProjectileProfile(
                    "AmazonPoisonSpear", spear, new Color(0.33f, 0.9f, 0.28f), 0.72f,
                    ProjectileTrajectoryStyle.SpearArc, 0.07f, true, 0.04f)
            };
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
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void ConfigureAbilityActions()
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
                serialized.FindProperty("_visualAction").enumValueIndex = (int)action;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(config);
            }
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
            AssetDatabase.Refresh();
        }

        private static void CopyApprovedProjectileTexture(string sourcePath, string destinationPath)
        {
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Approved projectile source is missing.", sourcePath);

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

        private static void ConfigurePrefabs(
            StandardUnitTweenProfile profile,
            Material glowOverlayMaterial)
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
                    serialized.FindProperty("_glowOverlayMaterial").objectReferenceValue = glowOverlayMaterial;
                    serialized.ApplyModifiedPropertiesWithoutUndo();

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static Material LoadOrCreateGlowOverlayMaterial()
        {
            Shader shader = Shader.Find(GlowOverlayShaderName);
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException(
                    $"Glow overlay shader is missing or unsupported: {GlowOverlayShaderName}");

            var material = AssetDatabase.LoadAssetAtPath<Material>(GlowOverlayMaterialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "PureRunGlowOverlay"
                };
                AssetDatabase.CreateAsset(material, GlowOverlayMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }

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
    }
}
