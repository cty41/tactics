using System;
using System.Collections.Generic;
using System.IO;
using Tactics.Common.Skills.Graph;
using Tactics.Editor.PresentationGraph;
using Tactics.Runtime.Utilities;
using UnityEditor;
using UnityEngine;

namespace Tactics.Editor.SkillGraphEditor
{
    /// <summary>
    /// Deterministically rebuilds the three project-owned Piloto VFX vertical samples.
    /// </summary>
    public static class PilotoVfxSampleAssetBuilder
    {
        private const string VendorPrefabRoot =
            "Assets/Piloto Studio/Roguelike VFX Pack/Prefabs";
        private const string AdaptedRoot =
            "Assets/Tactics/Arts/PureRun/VFX/PilotoAdapted";
        private const string PrefabRoot = AdaptedRoot + "/Prefabs";
        private const string ProfileRoot = AdaptedRoot + "/Profiles";
        private const string MaterialRoot = AdaptedRoot + "/Materials";
        private const string TextureRoot = AdaptedRoot + "/Textures";
        private const string VendorFireGroundTexturePath =
            "Assets/Piloto Studio/Textures/Tx_Fire_ground_01.png";
        private const string PoisonSpearProfilePath =
            "Assets/Tactics/Arts/PureRun/Tween/Projectiles/AmazonPoisonSpear.asset";
        private const string FireballProfilePath =
            "Assets/Tactics/Arts/PureRun/Tween/Projectiles/Fire.asset";
        private const string BoneSpearProfilePath =
            "Assets/Tactics/Arts/PureRun/Tween/Projectiles/BoneSpear.asset";
        private const string PreviewCompatibleParticleShader =
            "Tactics/PureRun/ParticleTextureUnlit";

        [MenuItem("Tactics/Tools/Pure Run/Rebuild Piloto VFX Sample Assets")]
        public static void RebuildAll()
        {
            EnsureFolder(PrefabRoot);
            EnsureFolder(ProfileRoot);
            EnsureFolder(MaterialRoot);
            EnsureFolder(TextureRoot);

            var poisonFlight = BuildPrefab(
                "PoisonSpearFlight",
                VendorPrefabRoot + "/MagicMissiles.prefab",
                new[]
                {
                    "MagicMissiles_Rough/Flare_FatDot_Alpha",
                    "MagicMissiles_Rough/Trail_RoughWave_White"
                },
                new Color(0.35f, 1f, 0.15f, 1f),
                true);
            var poisonImpact = BuildPrefab(
                "PoisonSpearImpact",
                VendorPrefabRoot + "/MagicMissiles.prefab",
                new[]
                {
                    "MagicMissiles_Rough/Hit_Punch_Burst_Outline",
                    "MagicMissiles_Rough/Rough_Star_White"
                },
                new Color(0.3f, 1f, 0.12f, 1f),
                false);
            var lightningImpact = BuildPrefab(
                "LightningImpact",
                VendorPrefabRoot + "/ThunderSpearCage.prefab",
                new[]
                {
                    "Sword_Remap_Yellow/Sword_Remap_Yellow/Lightning_Electric_Rough"
                },
                new Color(0.55f, 0.9f, 1f, 1f),
                false,
                alignParticleBottomToRoot: true);
            var amplifyDamage = BuildPrefab(
                "AmplifyDamageCurse",
                VendorPrefabRoot + "/PurpleLightPillar.prefab",
                new[]
                {
                    "HolyCircle_Rough",
                    "LightPilar_Noise_Rough_LIght/LightPilar_Noise_Rough_Dark",
                    "DefaultHDParticleMaterial/Hit_Punch_Burst_Outline_NoSoft",
                    "Dot_Rough"
                },
                new Color(0.55f, 0.1f, 0.85f, 1f),
                false,
                flattenRotationToCameraPlane: true);
            var amplifyDamageSigilGround = BuildAmplifyDamageSigilGround();
            var amplifyDamageSigilForeground = BuildAmplifyDamageSigilForeground();
            var amplifyDamageSigilGroundV2 = BuildAmplifyDamageSigilGroundV2();
            var amplifyDamageSigilRearFlamesV2 = BuildAmplifyDamageSigilFlamesV2(false);
            var amplifyDamageSigilForegroundFlamesV2 = BuildAmplifyDamageSigilFlamesV2(true);
            var fireballFlight = BuildPrefab(
                "FireballFlight",
                VendorPrefabRoot + "/FireWhale.prefab",
                new[]
                {
                    "holder/Whale_Base/Fire_Alpha_Rough",
                    "holder/Whale_Base/Trail_FireWhale"
                },
                new Color(1f, 0.34f, 0.08f, 1f),
                true,
                flattenRotationToCameraPlane: true,
                normalizedWidth: 1f,
                normalizedHeight: 0.45f);
            var boneSpearFlight = BuildPrefab(
                "BoneSpearFlight",
                VendorPrefabRoot + "/MagicMissiles.prefab",
                new[]
                {
                    "MagicMissiles_Rough/Flare_FatDot_Alpha",
                    "MagicMissiles_Rough/Trail_RoughWave_White"
                },
                new Color(0.58f, 0.92f, 0.82f, 1f),
                true,
                flattenRotationToCameraPlane: true,
                normalizedWidth: 0.42f,
                normalizedHeight: 0.14f);

            GameObject[] fireballCharges = new GameObject[3];
            GameObject[] fireballImpacts = new GameObject[3];
            GameObject[] boneSpearCharges = new GameObject[3];
            GameObject[] boneSpearImpacts = new GameObject[3];
            GameObject[] thrustStrikes = new GameObject[3];
            GameObject[] thrustHits = new GameObject[3];
            for (int level = 1; level <= 3; level++)
            {
                fireballCharges[level - 1] = BuildFireballChargePrefab(level);
                fireballImpacts[level - 1] = BuildFireballImpactPrefab(level);
                boneSpearCharges[level - 1] = BuildBoneSpearChargePrefab(level);
                boneSpearImpacts[level - 1] = BuildBoneSpearImpactPrefab(level);
                thrustStrikes[level - 1] = BuildThrustStrikePrefab(level);
                thrustHits[level - 1] = BuildThrustHitPrefab(level);
            }
            GameObject fireballDetonation = BuildFireballDetonationPrefab();

            PruneUnreferencedGeneratedMaterials();

            ConfigurePoisonSpearProfile(poisonFlight, poisonImpact);
            ConfigureHybridProjectileProfiles(fireballFlight, boneSpearFlight);
            CreateOrUpdateCueProfile(
                "LightningLv1", lightningImpact, 0.48f, 0.48f, VisualCueAnchor.PrimaryTargetGround);
            CreateOrUpdateCueProfile(
                "LightningLv2", lightningImpact, 0.54f, 0.56f, VisualCueAnchor.PrimaryTargetGround);
            CreateOrUpdateCueProfile(
                "LightningLv3", lightningImpact, 0.6f, 0.64f, VisualCueAnchor.PrimaryTargetGround);
            CreateOrUpdateCueProfile("AmplifyDamageLv1", amplifyDamage, 0.7f, 0.42f);
            CreateOrUpdateCueProfile("AmplifyDamageLv2", amplifyDamage, 0.8f, 0.68f);
            CreateOrUpdateCueProfile("AmplifyDamageLv3", amplifyDamage, 0.9f, 0.92f);
            CreateOrUpdateCueProfile(
                "AmplifyDamageSigilGroundLv1",
                amplifyDamageSigilGround,
                0.95f,
                1f,
                VisualCueAnchor.PrimaryTargetGround,
                -2);
            CreateOrUpdateCueProfile(
                "AmplifyDamageSigilGroundLv2",
                amplifyDamageSigilGround,
                1.05f,
                2f,
                VisualCueAnchor.PrimaryTargetGround,
                -2);
            CreateOrUpdateCueProfile(
                "AmplifyDamageSigilGroundLv3",
                amplifyDamageSigilGround,
                1.15f,
                2.6f,
                VisualCueAnchor.PrimaryTargetGround,
                -2);
            CreateOrUpdateCueProfile(
                "AmplifyDamageSigilForegroundLv1",
                amplifyDamageSigilForeground,
                0.95f,
                1f,
                VisualCueAnchor.PrimaryTargetGround,
                2);
            CreateOrUpdateCueProfile(
                "AmplifyDamageSigilForegroundLv2",
                amplifyDamageSigilForeground,
                1.05f,
                2f,
                VisualCueAnchor.PrimaryTargetGround,
                2);
            CreateOrUpdateCueProfile(
                "AmplifyDamageSigilForegroundLv3",
                amplifyDamageSigilForeground,
                1.15f,
                2.6f,
                VisualCueAnchor.PrimaryTargetGround,
                2);
            float[] sigilV2Scales = { 1f, 2.35f / 1.35f, 3.15f / 1.35f };
            float[] sigilV2Lifetimes = { 0.95f, 1.05f, 1.15f };
            for (int level = 1; level <= 3; level++)
            {
                float scale = sigilV2Scales[level - 1];
                float lifetime = sigilV2Lifetimes[level - 1];
                CreateOrUpdateCueProfile(
                    $"AmplifyDamageSigilGroundV2Lv{level}",
                    amplifyDamageSigilGroundV2,
                    lifetime,
                    scale,
                    VisualCueAnchor.PrimaryTargetGround,
                    -2);
                CreateOrUpdateCueProfile(
                    $"AmplifyDamageSigilRearFlamesV2Lv{level}",
                    amplifyDamageSigilRearFlamesV2,
                    lifetime,
                    scale,
                    VisualCueAnchor.PrimaryTargetGround,
                    -1);
                CreateOrUpdateCueProfile(
                    $"AmplifyDamageSigilForegroundFlamesV2Lv{level}",
                    amplifyDamageSigilForegroundFlamesV2,
                    lifetime,
                    scale,
                    VisualCueAnchor.PrimaryTargetGround,
                    2);
                CreateOrUpdateCueProfile(
                    $"FireballChargeLv{level}",
                    fireballCharges[level - 1],
                    0.54f,
                    0.32f + level * 0.035f,
                    VisualCueAnchor.Caster,
                    -1);
                CreateOrUpdateCueProfile(
                    $"FireballImpactLv{level}",
                    fireballImpacts[level - 1],
                    0.34f + level * 0.03f,
                    0.72f + level * 0.08f,
                    VisualCueAnchor.PrimaryTarget,
                    32);
                CreateOrUpdateCueProfile(
                    $"BoneSpearChargeLv{level}",
                    boneSpearCharges[level - 1],
                    0.54f,
                    0.30f + level * 0.03f,
                    VisualCueAnchor.Caster,
                    -1);
                CreateOrUpdateCueProfile(
                    $"BoneSpearImpactLv{level}",
                    boneSpearImpacts[level - 1],
                    0.22f + level * 0.025f,
                    0.52f + level * 0.06f,
                    VisualCueAnchor.PrimaryTarget,
                    32);
                CreateOrUpdateCueProfile(
                    $"ThrustStrikeLv{level}",
                    thrustStrikes[level - 1],
                    0.18f + level * 0.015f,
                    1f,
                    VisualCueAnchor.Caster,
                    29,
                    VisualCueOrientationMode.SourceToTarget,
                    true,
                    1f);
                CreateOrUpdateCueProfile(
                    $"ThrustHitLv{level}",
                    thrustHits[level - 1],
                    0.16f + level * 0.02f,
                    0.44f + level * 0.06f,
                    VisualCueAnchor.PrimaryTarget,
                    32);
            }
            CreateOrUpdateCueProfile(
                "FireballDetonationLv3",
                fireballDetonation,
                0.28f,
                0.92f,
                VisualCueAnchor.PrimaryTarget,
                33);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            MageSliceAssetBuilder.RebuildLightningVisualSample();
            PureRunPresentationGraphAssetBuilder.RebuildCurseSigilSamples();
            PureRunPresentationGraphAssetBuilder.RebuildThrustSamples();
            PureRunPresentationGraphAssetBuilder.RebuildFireballSamples();
            PureRunPresentationGraphAssetBuilder.RebuildBoneSpearSamples();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            TLog.Info("[PilotoVfxSampleAssetBuilder] Published adapted VFX samples rebuilt.");
        }

        private static GameObject BuildFireballChargePrefab(int level)
        {
            var nodes = new List<string>
            {
                "FillingBar_RoughTrail_MagicCircle/Trail_RoughWave_White"
            };
            if (level >= 2)
                nodes.Add("FillingBar_RoughTrail_MagicCircle/Trail_RoughWave_Green");
            if (level >= 3)
                nodes.Add("FillingBar_RoughTrail_MagicCircle/Hit_Punch_Burst_Outline");
            return BuildPrefab(
                $"FireballChargeLv{level}",
                VendorPrefabRoot + "/FireWhale.prefab",
                nodes.ToArray(),
                new Color(1f, 0.30f + level * 0.035f, 0.06f, 0.82f),
                false,
                flattenRotationToCameraPlane: true,
                normalizedWidth: 0.7f,
                normalizedHeight: 0.36f);
        }

        private static GameObject BuildFireballImpactPrefab(int level)
        {
            var nodes = new List<string>
            {
                "holder/Whale_Dark/DefaultHDParticleMaterial/Hit_Punch_Burst_Outline",
                "holder/Whale_Dark/DefaultHDParticleMaterial/Fire_Alpha_Rough"
            };
            if (level >= 2)
                nodes.Add("holder/Whale_Dark/DefaultHDParticleMaterial/Tx_Fire_ground_01_Alpha");
            if (level >= 3)
                nodes.Add("holder/Whale_Dark/DefaultHDParticleMaterial/GroundCracks_Spiky_002");
            return BuildPrefab(
                $"FireballImpactLv{level}",
                VendorPrefabRoot + "/FireWhale.prefab",
                nodes.ToArray(),
                new Color(1f, 0.28f + level * 0.04f, 0.06f, 1f),
                false,
                flattenRotationToCameraPlane: true,
                normalizedWidth: 0.52f + level * 0.08f,
                normalizedHeight: 0.42f + level * 0.07f);
        }

        private static GameObject BuildFireballDetonationPrefab()
        {
            return BuildPrefab(
                "FireballDetonationLv3",
                VendorPrefabRoot + "/FireWhale.prefab",
                new[]
                {
                    "FillingBar_RoughTrail_MagicCircle/Hit_Punch_Burst_Outline",
                    "holder/Whale_Base/mat_Glow_Add",
                    "holder/Whale_Dark/DefaultHDParticleMaterial/Fire_Alpha_Rough"
                },
                new Color(1f, 0.56f, 0.12f, 1f),
                false,
                flattenRotationToCameraPlane: true,
                normalizedWidth: 0.82f,
                normalizedHeight: 0.68f);
        }

        private static GameObject BuildBoneSpearChargePrefab(int level)
        {
            var nodes = new List<string> { "Rough_Star_White/mat_Glow_Add" };
            if (level >= 2)
                nodes.Add("Rough_Star_White/Dot_Rough");
            if (level >= 3)
                nodes.Add("Rough_Star_White/mat_GOW_Flare_Cross_Add");
            return BuildPrefab(
                $"BoneSpearChargeLv{level}",
                VendorPrefabRoot + "/DarkSummon.prefab",
                nodes.ToArray(),
                new Color(0.50f, 0.86f, 0.76f, 0.78f),
                false,
                flattenRotationToCameraPlane: true,
                normalizedWidth: 0.58f,
                normalizedHeight: 0.38f);
        }

        private static GameObject BuildBoneSpearImpactPrefab(int level)
        {
            var nodes = new List<string>
            {
                "Rough_Star_White/Hit_Punch_Burst_Outline",
                "Rough_Star_White/Rough_Star_White"
            };
            if (level >= 2)
                nodes.Add("mat_Glow_Alpha_NoSoft/WispySmoke");
            if (level >= 3)
                nodes.Add("Rough_Star_White/mat_GOW_Flare_Cross_Add");
            return BuildPrefab(
                $"BoneSpearImpactLv{level}",
                VendorPrefabRoot + "/DarkSummon.prefab",
                nodes.ToArray(),
                new Color(0.58f, 0.88f, 0.78f, 0.94f),
                false,
                flattenRotationToCameraPlane: true,
                normalizedWidth: 0.38f + level * 0.05f,
                normalizedHeight: 0.34f + level * 0.05f);
        }

        private static GameObject BuildThrustStrikePrefab(int level)
        {
            var nodes = new List<string> { "Sword_Remap_Yellow/Trail_RoughWave_White" };
            if (level >= 2)
                nodes.Add("Sword_Remap_Yellow/Dot_Rough");
            if (level >= 3)
                nodes.Add("Sword_Remap_Yellow/Trail_RoughBurst_White");
            return BuildPrefab(
                $"ThrustStrikeLv{level}",
                VendorPrefabRoot + "/PaladinSword.prefab",
                nodes.ToArray(),
                new Color(1f, 0.82f, 0.38f, 0.9f),
                false,
                flattenRotationToCameraPlane: true,
                normalizedWidth: 1f,
                normalizedHeight: 0.12f + level * 0.015f);
        }

        private static GameObject BuildThrustHitPrefab(int level)
        {
            var nodes = new List<string> { "Sword_Remap_Yellow/Hit_Punch_Burst_Outline" };
            if (level >= 2)
                nodes.Add("Sword_Remap_Yellow/Rough_Star_White");
            if (level >= 3)
                nodes.Add("Sword_Remap_Yellow/Dot_Rough");
            return BuildPrefab(
                $"ThrustHitLv{level}",
                VendorPrefabRoot + "/PaladinSword.prefab",
                nodes.ToArray(),
                new Color(1f, 0.78f, 0.34f, 0.94f),
                false,
                flattenRotationToCameraPlane: true,
                normalizedWidth: 0.30f + level * 0.04f,
                normalizedHeight: 0.28f + level * 0.04f);
        }

        private static GameObject BuildAmplifyDamageSigilGround()
        {
            var targetRoot = new GameObject("AmplifyDamageSigilGround");
            var sourceInstances = new List<GameObject>();
            try
            {
                GameObject circle = CloneNamedSourceNode(
                    VendorPrefabRoot + "/ManaRegenArea.prefab",
                    "FillingBar_RoughTrail_MagicCircle",
                    targetRoot.transform,
                    sourceInstances);
                GameObject shadow = CloneNamedSourceNode(
                    VendorPrefabRoot + "/DarkSummon.prefab",
                    "ShadowPool",
                    targetRoot.transform,
                    sourceInstances);
                circle.name = "MagicCircleAndRunes";
                shadow.name = "ShadowPool";
                RemoveAllChildren(shadow.transform);

                FlattenParticleHierarchy(circle.transform);
                FlattenParticleHierarchy(shadow.transform);
                foreach (ParticleSystem system in circle.GetComponentsInChildren<ParticleSystem>(true))
                {
                    Color tint = system.name.Contains("ArcaneRunes", StringComparison.Ordinal)
                        ? new Color(1f, 0.12f, 0.72f, 0.9f)
                        : new Color(0.9f, 0.08f, 0.48f, 0.82f);
                    ConfigureParticleSystem(system, tint, false, true, preserveRenderMode: true);
                    ConfigureSingleSigilParticle(system, system.name.Contains("ArcaneRunes", StringComparison.Ordinal)
                        ? 0.12f
                        : 0.05f);
                }
                foreach (ParticleSystem system in shadow.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ConfigureParticleSystem(
                        system,
                        new Color(0.12f, 0.025f, 0.18f, 0.48f),
                        false,
                        true,
                        preserveRenderMode: true);
                    ConfigureSingleSigilParticle(system, 0f);
                }

                CloneMaterials(targetRoot, targetRoot.name);
                NormalizeParticleFootprint(targetRoot, 1.25f, 0.625f);
                return PrefabUtility.SaveAsPrefabAsset(
                    targetRoot,
                    $"{PrefabRoot}/{targetRoot.name}.prefab");
            }
            finally
            {
                foreach (GameObject sourceInstance in sourceInstances)
                {
                    if (sourceInstance != null)
                        UnityEngine.Object.DestroyImmediate(sourceInstance);
                }
                UnityEngine.Object.DestroyImmediate(targetRoot);
            }
        }

        private static GameObject BuildAmplifyDamageSigilForeground()
        {
            var targetRoot = new GameObject("AmplifyDamageSigilForeground");
            var sourceInstances = new List<GameObject>();
            try
            {
                GameObject flames = CloneNamedSourceNode(
                    VendorPrefabRoot + "/SpiritSun.prefab",
                    "Fire_Alpha_Rough",
                    targetRoot.transform,
                    sourceInstances);
                flames.name = "NearSideLowFlames";
                flames.transform.localPosition = new Vector3(0f, -0.08f, 0f);
                flames.transform.localRotation = Quaternion.identity;

                foreach (ParticleSystem system in flames.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ConfigureParticleSystem(
                        system,
                        new Color(0.78f, 0.035f, 0.23f, 0.9f),
                        false,
                        true);
                    ConfigureNearSideFlames(system);
                }

                CloneMaterials(targetRoot, targetRoot.name);
                NormalizeParticleFootprint(targetRoot, 1.25f, 0.34f);
                // Keep the foreground accent on the camera-near half only after the
                // third-party particle bounds have been normalized around the cue root.
                flames.transform.localPosition += Vector3.down * 0.08f;
                return PrefabUtility.SaveAsPrefabAsset(
                    targetRoot,
                    $"{PrefabRoot}/{targetRoot.name}.prefab");
            }
            finally
            {
                foreach (GameObject sourceInstance in sourceInstances)
                {
                    if (sourceInstance != null)
                        UnityEngine.Object.DestroyImmediate(sourceInstance);
                }
                UnityEngine.Object.DestroyImmediate(targetRoot);
            }
        }

        private static GameObject BuildAmplifyDamageSigilGroundV2()
        {
            var targetRoot = new GameObject("AmplifyDamageSigilGroundV2");
            var sourceInstances = new List<GameObject>();
            try
            {
                GameObject outerRing = CloneNamedSourceNode(
                    VendorPrefabRoot + "/ManaRegenArea.prefab",
                    "FillingBar_RoughTrail_MagicCircle",
                    targetRoot.transform,
                    sourceInstances);
                GameObject innerRing = CloneNamedSourceNode(
                    VendorPrefabRoot + "/ManaRegenArea.prefab",
                    "FillingBar_RoughTrail_MagicCircle",
                    targetRoot.transform,
                    sourceInstances);
                GameObject runeBand = CloneNamedSourceNode(
                    VendorPrefabRoot + "/ManaRegenArea.prefab",
                    "ArcaneRunes_Atlas_002",
                    targetRoot.transform,
                    sourceInstances);
                GameObject centralSigil = CloneNamedSourceNode(
                    VendorPrefabRoot + "/ManaRegenArea.prefab",
                    "ArcaneRunes_Atlas_002",
                    targetRoot.transform,
                    sourceInstances);
                GameObject shadow = CloneNamedSourceNode(
                    VendorPrefabRoot + "/DarkSummon.prefab",
                    "ShadowPool",
                    targetRoot.transform,
                    sourceInstances);

                outerRing.name = "OuterBrightRing";
                innerRing.name = "InnerBrightRing";
                runeBand.name = "LowIntensityRuneBand";
                centralSigil.name = "OriginalCentralAngularSigil";
                shadow.name = "DarkGroundDisc";
                RemoveNamedDescendants(outerRing.transform, "ArcaneRunes_Atlas_002");
                RemoveNamedDescendants(innerRing.transform, "ArcaneRunes_Atlas_002");
                RemoveAllChildren(shadow.transform);

                ConfigureSigilMotif(
                    outerRing,
                    new Color32(0x55, 0x30, 0xA9, 0xC7),
                    0.06f,
                    1.35f,
                    0.675f);
                ConfigureSigilMotif(
                    innerRing,
                    new Color32(0xF1, 0x3A, 0x62, 0xEB),
                    0.10f,
                    1.12f,
                    0.56f);
                ConfigureSigilMotif(
                    runeBand,
                    new Color32(0xA8, 0x32, 0xB7, 0xA3),
                    0.14f,
                    1.22f,
                    0.61f);
                ConfigureSigilMotif(
                    centralSigil,
                    new Color32(0xFF, 0x46, 0x66, 0xF5),
                    0.39f,
                    0.54f,
                    0.27f,
                    true);
                ConfigureSigilMotif(
                    shadow,
                    new Color32(0x19, 0x0D, 0x24, 0x80),
                    0f,
                    1.45f,
                    0.725f);

                CloneMaterials(targetRoot, targetRoot.name);
                AssignCentralSigilMaterial(centralSigil, GetOrCreateCentralSigilTexture());
                return PrefabUtility.SaveAsPrefabAsset(
                    targetRoot,
                    $"{PrefabRoot}/{targetRoot.name}.prefab");
            }
            finally
            {
                foreach (GameObject sourceInstance in sourceInstances)
                {
                    if (sourceInstance != null)
                        UnityEngine.Object.DestroyImmediate(sourceInstance);
                }
                UnityEngine.Object.DestroyImmediate(targetRoot);
            }
        }

        private static GameObject BuildAmplifyDamageSigilFlamesV2(bool foreground)
        {
            string targetName = foreground
                ? "AmplifyDamageSigilForegroundFlamesV2"
                : "AmplifyDamageSigilRearFlamesV2";
            var targetRoot = new GameObject(targetName);
            var sourceInstances = new List<GameObject>();
            try
            {
                for (int index = 0; index < 8; index++)
                {
                    bool belongsToForeground = index >= 2 && index <= 6;
                    if (belongsToForeground != foreground)
                        continue;

                    GameObject flame = CloneNamedSourceNode(
                        VendorPrefabRoot + "/SpiritSun.prefab",
                        "Fire_Alpha_Rough",
                        targetRoot.transform,
                        sourceInstances);
                    float angle = index * 45f * Mathf.Deg2Rad;
                    flame.name = $"FlameNode_{index:D2}";
                    flame.transform.localPosition = new Vector3(
                        Mathf.Sin(angle) * 0.47f,
                        Mathf.Cos(angle) * 0.225f,
                        0f);
                    flame.transform.localRotation = Quaternion.identity;
                    flame.transform.localScale = Vector3.one;

                    foreach (ParticleSystem system in flame.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        ConfigureParticleSystem(
                            system,
                            Color.white,
                            false,
                            true);
                        ConfigureSigilFlameNode(system, 0.10f + index * 0.06f);
                    }
                }

                CloneMaterials(targetRoot, targetRoot.name);
                AssignSigilFlameTexture(targetRoot);
                return PrefabUtility.SaveAsPrefabAsset(
                    targetRoot,
                    $"{PrefabRoot}/{targetRoot.name}.prefab");
            }
            finally
            {
                foreach (GameObject sourceInstance in sourceInstances)
                {
                    if (sourceInstance != null)
                        UnityEngine.Object.DestroyImmediate(sourceInstance);
                }
                UnityEngine.Object.DestroyImmediate(targetRoot);
            }
        }

        private static void ConfigureSigilMotif(
            GameObject motif,
            Color tint,
            float startDelay,
            float width,
            float height,
            bool pulse = false)
        {
            FlattenParticleHierarchy(motif.transform);
            foreach (ParticleSystem system in motif.GetComponentsInChildren<ParticleSystem>(true))
            {
                ConfigureParticleSystem(system, tint, false, true, preserveRenderMode: true);
                ConfigureSigilV2Particle(system, startDelay, pulse);
            }
            NormalizeSingleParticleFootprint(motif, width, height);
        }

        private static void RemoveNamedDescendants(Transform root, string name)
        {
            for (int index = root.childCount - 1; index >= 0; index--)
            {
                Transform child = root.GetChild(index);
                if (child.name == name)
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                    continue;
                }
                RemoveNamedDescendants(child, name);
            }
        }

        private static Texture2D GetOrCreateCentralSigilTexture()
        {
            const int width = 64;
            const int height = 32;
            string path = TextureRoot + "/AmplifyDamageSigilCentralV2.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
                {
                    name = "AmplifyDamageSigilCentralV2"
                };
                AssetDatabase.CreateAsset(texture, path);
            }
            else
            {
                texture.Reinitialize(width, height, TextureFormat.RGBA32, false);
            }

            var pixels = new Color32[width * height];
            var white = new Color32(255, 255, 255, 255);
            DrawTextureLine(pixels, width, height, new Vector2Int(10, 24), new Vector2Int(26, 7), 2, white);
            DrawTextureLine(pixels, width, height, new Vector2Int(26, 7), new Vector2Int(39, 19), 2, white);
            DrawTextureLine(pixels, width, height, new Vector2Int(39, 19), new Vector2Int(54, 6), 2, white);
            DrawTextureLine(pixels, width, height, new Vector2Int(25, 8), new Vector2Int(52, 25), 2, white);
            texture.SetPixels32(pixels);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.Apply(false, false);
            EditorUtility.SetDirty(texture);
            return texture;
        }

        private static void DrawTextureLine(
            Color32[] pixels,
            int width,
            int height,
            Vector2Int start,
            Vector2Int end,
            int radius,
            Color32 color)
        {
            int steps = Mathf.Max(Mathf.Abs(end.x - start.x), Mathf.Abs(end.y - start.y));
            for (int step = 0; step <= steps; step++)
            {
                float t = steps == 0 ? 0f : step / (float)steps;
                int centerX = Mathf.RoundToInt(Mathf.Lerp(start.x, end.x, t));
                int centerY = Mathf.RoundToInt(Mathf.Lerp(start.y, end.y, t));
                for (int y = -radius; y <= radius; y++)
                {
                    for (int x = -radius; x <= radius; x++)
                    {
                        if (x * x + y * y > radius * radius)
                            continue;
                        int pixelX = centerX + x;
                        int pixelY = centerY + y;
                        if (pixelX < 0 || pixelX >= width || pixelY < 0 || pixelY >= height)
                            continue;
                        pixels[pixelY * width + pixelX] = color;
                    }
                }
            }
        }

        private static void AssignCentralSigilMaterial(GameObject centralSigil, Texture2D texture)
        {
            int materialIndex = 0;
            foreach (ParticleSystemRenderer renderer in
                     centralSigil.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                Material source = renderer.sharedMaterial;
                if (source == null)
                    continue;
                string path = $"{MaterialRoot}/AmplifyDamageSigilGroundV2_Central_{materialIndex:D2}.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(source);
                    AssetDatabase.CreateAsset(material, path);
                }
                else
                {
                    EditorUtility.CopySerialized(source, material);
                }
                material.name = $"AmplifyDamageSigilGroundV2_Central_{materialIndex:D2}";
                material.mainTexture = texture;
                material.mainTextureScale = Vector2.one;
                material.mainTextureOffset = Vector2.zero;
                EditorUtility.SetDirty(material);
                renderer.sharedMaterial = material;
                materialIndex++;
            }
        }

        private static GameObject CloneNamedSourceNode(
            string sourcePath,
            string nodeName,
            Transform targetParent,
            ICollection<GameObject> sourceInstances)
        {
            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (sourcePrefab == null)
                throw new FileNotFoundException($"Piloto source prefab is missing: {sourcePath}");
            var sourceInstance = PrefabUtility.InstantiatePrefab(sourcePrefab) as GameObject;
            if (sourceInstance == null)
                throw new InvalidOperationException($"Could not instantiate source prefab: {sourcePath}");
            sourceInstances.Add(sourceInstance);

            Transform sourceNode = FindDescendant(sourceInstance.transform, nodeName);
            if (sourceNode == null)
                throw new InvalidOperationException($"Source VFX node is missing: {sourcePath}/{nodeName}");
            var clone = UnityEngine.Object.Instantiate(sourceNode.gameObject, targetParent, false);
            clone.SetActive(true);
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;
            return clone;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root.name == name)
                return root;
            foreach (Transform child in root)
            {
                Transform result = FindDescendant(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }

        private static void RemoveAllChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
                UnityEngine.Object.DestroyImmediate(parent.GetChild(index).gameObject);
        }

        private static void FlattenParticleHierarchy(Transform root)
        {
            foreach (ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                Transform transform = system.transform;
                Vector3 localPosition = transform.localPosition;
                transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
                transform.localRotation = Quaternion.identity;
            }
        }

        private static void ConfigureSingleSigilParticle(ParticleSystem system, float startDelay)
        {
            var main = system.main;
            main.duration = 0.9f;
            main.startDelay = startDelay;
            main.startLifetime = 0.82f;
            main.startSpeed = 0f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shape = system.shape;
            shape.enabled = false;
            ConfigureFade(system, 0.12f, 0.72f);
        }

        private static void ConfigureSigilV2Particle(
            ParticleSystem system,
            float startDelay,
            bool pulse)
        {
            var main = system.main;
            main.duration = 1.05f;
            main.startDelay = startDelay;
            main.startLifetime = Mathf.Max(0.2f, 1.05f - startDelay);
            main.startSpeed = 0f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shape = system.shape;
            shape.enabled = false;
            ConfigureFade(system, 0.12f, 0.72f);
            var size = system.sizeOverLifetime;
            size.enabled = pulse;
            if (pulse)
            {
                size.size = new ParticleSystem.MinMaxCurve(
                    1f,
                    new AnimationCurve(
                        new Keyframe(0f, 0f),
                        new Keyframe(0.18f, 1.08f),
                        new Keyframe(0.55f, 1f),
                        new Keyframe(1f, 0.94f)));
            }
        }

        private static void ConfigureSigilFlameNode(ParticleSystem system, float startDelay)
        {
            var main = system.main;
            main.duration = 1.05f;
            main.startDelay = startDelay;
            main.startLifetime = Mathf.Max(0.38f, 1.05f - startDelay);
            main.startSpeed = 0f;
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.12f, 0.15f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.18f, 0.22f);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(0.12f, 0.15f);
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = 1;

            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shape = system.shape;
            shape.enabled = false;
            var textureSheet = system.textureSheetAnimation;
            textureSheet.enabled = true;
            textureSheet.mode = ParticleSystemAnimationMode.Grid;
            textureSheet.animation = ParticleSystemAnimationType.WholeSheet;
            textureSheet.numTilesX = 4;
            textureSheet.numTilesY = 4;
            textureSheet.cycleCount = 1;
            textureSheet.startFrame = 0f;
            textureSheet.frameOverTime = new ParticleSystem.MinMaxCurve(
                1f,
                AnimationCurve.Linear(0f, 0f, 1f, 1f));
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                // Tx_Fire_ground_01 keeps the visible flame root on the bottom edge of
                // each atlas frame. Bottom-center the billboard so the node itself sits
                // on the sigil rail while the flame grows upward from that point.
                renderer.pivot = new Vector3(0f, -0.5f, 0f);
            }
            ConfigureSigilFlameColor(system);
        }

        private static void ConfigureSigilFlameColor(ParticleSystem system)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color32(0xFF, 0x33, 0x4F, 0xFF), 0f),
                    new GradientColorKey(new Color32(0xD7, 0x2B, 0x88, 0xFF), 0.42f),
                    new GradientColorKey(new Color32(0x6A, 0x2E, 0xA8, 0xFF), 0.70f),
                    new GradientColorKey(new Color32(0x6A, 0x2E, 0xA8, 0xFF), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.10f),
                    new GradientAlphaKey(1f, 0.76f),
                    new GradientAlphaKey(0f, 1f)
                });
            var color = system.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private static void AssignSigilFlameTexture(GameObject targetRoot)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                VendorFireGroundTexturePath);
            if (texture == null)
                throw new FileNotFoundException(
                    $"Piloto fire atlas is missing: {VendorFireGroundTexturePath}");

            foreach (ParticleSystemRenderer renderer in
                     targetRoot.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null)
                        continue;
                    material.mainTexture = texture;
                    material.mainTextureScale = Vector2.one;
                    material.mainTextureOffset = Vector2.zero;
                    EditorUtility.SetDirty(material);
                }
            }
        }

        private static void NormalizeSingleParticleFootprint(
            GameObject motif,
            float targetWidth,
            float targetHeight)
        {
            Bounds bounds = SampleParticleBounds(motif);
            if (bounds.size.x <= 0.001f || bounds.size.y <= 0.001f)
                throw new InvalidOperationException($"Could not normalize particle motif: {motif.name}");

            float scaleX = targetWidth / bounds.size.x;
            float scaleY = targetHeight / bounds.size.y;
            foreach (ParticleSystem system in motif.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = system.main;
                bool wasThreeDimensional = main.startSize3D;
                ParticleSystem.MinMaxCurve sizeX = wasThreeDimensional ? main.startSizeX : main.startSize;
                ParticleSystem.MinMaxCurve sizeY = wasThreeDimensional ? main.startSizeY : main.startSize;
                main.startSize3D = true;
                ParticleSystem.MinMaxCurve normalizedSizeX = ScaleParticleCurve(sizeX, scaleX);
                main.startSizeX = normalizedSizeX;
                main.startSizeY = ScaleParticleCurve(sizeY, scaleY);
                // Billboard bounds still account for the inherited Z size. Piloto's
                // ground disc carries a Z value of 16, which inflates its bounds even
                // though the rendered quad is flat, so normalize Z with the visible X size.
                main.startSizeZ = normalizedSizeX;
            }
        }

        private static ParticleSystem.MinMaxCurve ScaleParticleCurve(
            ParticleSystem.MinMaxCurve source,
            float scale)
        {
            switch (source.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return new ParticleSystem.MinMaxCurve(source.constant * scale);
                case ParticleSystemCurveMode.TwoConstants:
                    return new ParticleSystem.MinMaxCurve(
                        source.constantMin * scale,
                        source.constantMax * scale);
                case ParticleSystemCurveMode.Curve:
                    return new ParticleSystem.MinMaxCurve(
                        source.curveMultiplier * scale,
                        source.curve);
                case ParticleSystemCurveMode.TwoCurves:
                    return new ParticleSystem.MinMaxCurve(
                        source.curveMultiplier * scale,
                        source.curveMin,
                        source.curveMax);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(source), source.mode, "Unsupported particle size curve mode.");
            }
        }

        private static void ConfigureNearSideFlames(ParticleSystem system)
        {
            var main = system.main;
            main.duration = 0.9f;
            main.startDelay = 0.1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.38f, 0.62f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.11f, 0.18f);
            main.scalingMode = ParticleSystemScalingMode.Shape;

            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0.04f, 7) });

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.52f;
            shape.radiusThickness = 0.18f;
            shape.arc = 180f;
            shape.rotation = new Vector3(0f, 0f, 180f);
            shape.scale = new Vector3(1f, 0.5f, 1f);
            ConfigureFade(system, 0.08f, 0.78f);
        }

        private static void ConfigureFade(ParticleSystem system, float fadeInTime, float fadeOutTime)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, fadeInTime),
                    new GradientAlphaKey(1f, fadeOutTime),
                    new GradientAlphaKey(0f, 1f)
                });
            var color = system.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private static void NormalizeParticleFootprint(
            GameObject targetRoot,
            float targetWidth,
            float targetHeight)
        {
            Bounds bounds = SampleParticleBounds(targetRoot);
            if (bounds.size.x <= 0.001f || bounds.size.y <= 0.001f)
                throw new InvalidOperationException($"Could not normalize particle bounds: {targetRoot.name}");

            float scaleX = targetWidth / bounds.size.x;
            float scaleY = targetHeight / bounds.size.y;
            Vector3 localCenter = targetRoot.transform.InverseTransformPoint(bounds.center);
            foreach (Transform child in targetRoot.transform)
            {
                Vector3 position = child.localPosition;
                child.localPosition = new Vector3(
                    (position.x - localCenter.x) * scaleX,
                    (position.y - localCenter.y) * scaleY,
                    0f);
                child.localScale = Vector3.Scale(child.localScale, new Vector3(scaleX, scaleY, 1f));
            }
        }

        private static Bounds SampleParticleBounds(GameObject targetRoot)
        {
            bool hasBounds = false;
            Bounds combined = default;
            ParticleSystem[] systems = targetRoot.GetComponentsInChildren<ParticleSystem>(true);
            foreach (float time in new[] { 0.05f, 0.15f, 0.3f, 0.5f, 0.72f, 1f, 1.25f })
            {
                foreach (ParticleSystem system in systems)
                {
                    system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    system.useAutoRandomSeed = false;
                    system.randomSeed = 1u;
                    system.Simulate(time, false, true, true);
                    ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
                    if (renderer == null || renderer.bounds.size.sqrMagnitude <= 0.0001f)
                        continue;
                    if (hasBounds)
                        combined.Encapsulate(renderer.bounds);
                    else
                    {
                        combined = renderer.bounds;
                        hasBounds = true;
                    }
                }
            }
            foreach (ParticleSystem system in systems)
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            return combined;
        }

        private static GameObject BuildPrefab(
            string targetName,
            string sourcePath,
            string[] sourceNodePaths,
            Color tint,
            bool looping,
            bool alignParticleBottomToRoot = false,
            bool flattenRotationToCameraPlane = false,
            float normalizedWidth = 0f,
            float normalizedHeight = 0f)
        {
            var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (sourcePrefab == null)
                throw new FileNotFoundException($"Piloto source prefab is missing: {sourcePath}");

            var sourceInstance = PrefabUtility.InstantiatePrefab(sourcePrefab) as GameObject;
            var targetRoot = new GameObject(targetName);
            try
            {
                if (sourceInstance == null)
                    throw new InvalidOperationException($"Could not instantiate source prefab: {sourcePath}");

                var clones = new List<GameObject>();
                foreach (string nodePath in sourceNodePaths)
                {
                    var sourceNode = sourceInstance.transform.Find(nodePath);
                    if (sourceNode == null)
                        throw new InvalidOperationException($"Source VFX node is missing: {sourcePath}/{nodePath}");

                    var clone = UnityEngine.Object.Instantiate(sourceNode.gameObject, targetRoot.transform, true);
                    clone.name = sourceNode.name;
                    clone.SetActive(true);
                    clones.Add(clone);
                }

                NormalizeShowcasePositions(clones);

                foreach (var particleSystem in targetRoot.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ConfigureParticleSystem(
                        particleSystem,
                        tint,
                        looping,
                        flattenRotationToCameraPlane);
                }
                if (flattenRotationToCameraPlane)
                    FlattenParticleHierarchy(targetRoot.transform);
                if (alignParticleBottomToRoot)
                    AlignParticleBottomToRoot(targetRoot);
                foreach (var forceField in targetRoot.GetComponentsInChildren<ParticleSystemForceField>(true))
                    UnityEngine.Object.DestroyImmediate(forceField);
                CloneMaterials(targetRoot, targetName);
                if (normalizedWidth > 0f && normalizedHeight > 0f)
                    NormalizeParticleFootprint(targetRoot, normalizedWidth, normalizedHeight);

                string targetPath = $"{PrefabRoot}/{targetName}.prefab";
                return PrefabUtility.SaveAsPrefabAsset(targetRoot, targetPath);
            }
            finally
            {
                if (sourceInstance != null)
                    UnityEngine.Object.DestroyImmediate(sourceInstance);
                UnityEngine.Object.DestroyImmediate(targetRoot);
            }
        }

        private static void NormalizeShowcasePositions(IReadOnlyList<GameObject> clones)
        {
            if (clones.Count == 0)
                return;

            Vector3 showcaseOrigin = clones[0].transform.position;
            foreach (GameObject clone in clones)
                clone.transform.position -= showcaseOrigin;
        }

        private static void AlignParticleBottomToRoot(GameObject targetRoot)
        {
            const float sampleDuration = 0.65f;
            const float sampleStep = 0.05f;
            float minimumWorldY = float.PositiveInfinity;
            ParticleSystem[] systems = targetRoot.GetComponentsInChildren<ParticleSystem>(true);

            foreach (ParticleSystem system in systems)
            {
                bool useAutoRandomSeed = system.useAutoRandomSeed;
                uint randomSeed = system.randomSeed;
                system.useAutoRandomSeed = false;
                system.randomSeed = 1u;
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                for (float time = sampleStep; time <= sampleDuration + 0.0001f; time += sampleStep)
                {
                    system.Simulate(time, true, true, true);
                    var renderer = system.GetComponent<ParticleSystemRenderer>();
                    if (renderer != null && renderer.bounds.size.sqrMagnitude > 0.0001f)
                        minimumWorldY = Mathf.Min(minimumWorldY, renderer.bounds.min.y);
                }

                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                system.randomSeed = randomSeed;
                system.useAutoRandomSeed = useAutoRandomSeed;
            }

            if (float.IsPositiveInfinity(minimumWorldY))
                throw new InvalidOperationException(
                    $"Could not sample particle bounds for ground alignment: {targetRoot.name}");

            float rootWorldY = targetRoot.transform.position.y;
            float offsetWorldY = rootWorldY - minimumWorldY;
            foreach (Transform child in targetRoot.transform)
                child.position += Vector3.up * offsetWorldY;
        }

        private static void ConfigureParticleSystem(
            ParticleSystem particleSystem,
            Color tint,
            bool looping,
            bool flattenRotationToCameraPlane,
            bool preserveRenderMode = false)
        {
            var main = particleSystem.main;
            main.loop = looping;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.stopAction = ParticleSystemStopAction.None;
            main.maxParticles = Mathf.Min(main.maxParticles, 64);
            main.startColor = new ParticleSystem.MinMaxGradient(tint);
            if (looping)
            {
                // Vendor flight motifs are often emitted by showcase-object movement.
                // Pooled projectiles need a stable time-based fallback so a stationary
                // preview sample and a freshly rented instance both become visible.
                var emission = particleSystem.emission;
                emission.enabled = true;
                if (emission.rateOverTime.constantMax <= 0.001f)
                    emission.rateOverTime = 12f;
            }
            if (flattenRotationToCameraPlane)
            {
                // The vendor effect contains X/Y start rotations intended for a 3D camera.
                // View-aligned Pure Run billboards must preserve only their authored Z spin.
                ParticleSystem.MinMaxCurve startRotationZ = main.startRotationZ;
                main.startRotation3D = false;
                main.startRotation = startRotationZ;
            }

            var velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = false;
            var force = particleSystem.forceOverLifetime;
            force.enabled = false;
            var externalForces = particleSystem.externalForces;
            externalForces.enabled = false;
            var inheritVelocity = particleSystem.inheritVelocity;
            inheritVelocity.enabled = false;
            var subEmitters = particleSystem.subEmitters;
            subEmitters.enabled = false;
            var trails = particleSystem.trails;
            trails.enabled = false;
            var collision = particleSystem.collision;
            collision.enabled = false;
            var trigger = particleSystem.trigger;
            trigger.enabled = false;

            var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                if (!preserveRenderMode || renderer.renderMode != ParticleSystemRenderMode.Mesh)
                    renderer.renderMode = ParticleSystemRenderMode.Billboard;
                if (renderer.renderMode == ParticleSystemRenderMode.Billboard)
                    renderer.alignment = ParticleSystemRenderSpace.View;
                renderer.allowRoll = false;
                renderer.trailMaterial = null;
            }

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static void CloneMaterials(GameObject targetRoot, string targetName)
        {
            var clones = new Dictionary<Material, Material>();
            int materialIndex = 0;
            foreach (var renderer in targetRoot.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++)
                {
                    if (materials[index] == null)
                        continue;
                    materials[index] = GetOrCreateMaterialClone(
                        materials[index], targetName, materialIndex++, clones);
                }
                renderer.sharedMaterials = materials;

                if (renderer.trailMaterial != null)
                {
                    renderer.trailMaterial = GetOrCreateMaterialClone(
                        renderer.trailMaterial, targetName, materialIndex++, clones);
                }
            }
        }

        private static Material GetOrCreateMaterialClone(
            Material source,
            string targetName,
            int materialIndex,
            Dictionary<Material, Material> clones)
        {
            if (clones.TryGetValue(source, out var clone))
                return clone;

            string cloneName = $"{targetName}_{materialIndex:D2}_{SanitizeFileName(source.name)}";
            string clonePath = $"{MaterialRoot}/{cloneName}.mat";
            clone = AssetDatabase.LoadAssetAtPath<Material>(clonePath);
            if (clone == null)
            {
                clone = new Material(source);
                AssetDatabase.CreateAsset(clone, clonePath);
            }
            else
            {
                EditorUtility.CopySerialized(source, clone);
            }
            clone.name = cloneName;
            if (targetName.StartsWith("AmplifyDamage", StringComparison.Ordinal) ||
                targetName.StartsWith("Fireball", StringComparison.Ordinal) ||
                targetName.StartsWith("BoneSpear", StringComparison.Ordinal) ||
                targetName.StartsWith("Thrust", StringComparison.Ordinal))
                ConfigurePreviewCompatibleParticleMaterial(clone, source);
            clone.DisableKeyword("_SOFTPARTICLES_ON");
            clone.DisableKeyword("_USESOFTALPHA_ON");
            clone.DisableKeyword("USESOFTALPHA_ON");
            if (clone.HasProperty("_SoftParticlesEnabled"))
                clone.SetFloat("_SoftParticlesEnabled", 0f);
            if (clone.HasProperty("_USESOFTALPHA"))
                clone.SetFloat("_USESOFTALPHA", 0f);
            EditorUtility.SetDirty(clone);
            clones.Add(source, clone);
            return clone;
        }

        private static void ConfigurePreviewCompatibleParticleMaterial(Material clone, Material source)
        {
            Shader shader = Shader.Find(PreviewCompatibleParticleShader);
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException(
                    $"Required particle shader is unavailable: {PreviewCompatibleParticleShader}");

            Texture mainTexture = source.mainTexture;
            Vector2 textureScale = source.mainTextureScale;
            Vector2 textureOffset = source.mainTextureOffset;
            clone.shader = shader;
            clone.mainTexture = mainTexture;
            clone.mainTextureScale = textureScale;
            clone.mainTextureOffset = textureOffset;
            clone.SetColor("_Tint", Color.white);
            clone.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            clone.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        private static void PruneUnreferencedGeneratedMaterials()
        {
            var referencedPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (string prefabGuid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot }))
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                    continue;

                foreach (ParticleSystemRenderer renderer in
                         prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    foreach (Material material in renderer.sharedMaterials)
                        AddManagedMaterialPath(material, referencedPaths);
                    AddManagedMaterialPath(renderer.trailMaterial, referencedPaths);
                }
            }

            foreach (string materialGuid in AssetDatabase.FindAssets("t:Material", new[] { MaterialRoot }))
            {
                string materialPath = AssetDatabase.GUIDToAssetPath(materialGuid);
                if (!referencedPaths.Contains(materialPath))
                    AssetDatabase.DeleteAsset(materialPath);
            }
        }

        private static void AddManagedMaterialPath(Material material, HashSet<string> referencedPaths)
        {
            if (material == null)
                return;
            string materialPath = AssetDatabase.GetAssetPath(material);
            if (materialPath.StartsWith(MaterialRoot + "/", StringComparison.Ordinal))
                referencedPaths.Add(materialPath);
        }

        private static string SanitizeFileName(string value)
        {
            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            var characters = value.ToCharArray();
            for (int index = 0; index < characters.Length; index++)
            {
                if (Array.IndexOf(invalidCharacters, characters[index]) >= 0 ||
                    char.IsWhiteSpace(characters[index]))
                {
                    characters[index] = '_';
                }
            }
            return new string(characters);
        }

        private static void ConfigurePoisonSpearProfile(
            GameObject flightPrefab,
            GameObject impactPrefab)
        {
            var profile = AssetDatabase.LoadAssetAtPath<ProjectileVisualProfile>(PoisonSpearProfilePath);
            if (profile == null)
                throw new FileNotFoundException("Amazon poison spear visual profile is required.", PoisonSpearProfilePath);

            var serialized = new SerializedObject(profile);
            serialized.FindProperty("_flightPrefab").objectReferenceValue = flightPrefab;
            serialized.FindProperty("_impactPrefab").objectReferenceValue = impactPrefab;
            serialized.FindProperty("_impactLifetime").floatValue = 0.45f;
            serialized.FindProperty("_impactScale").floatValue = 0.55f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        internal static void RestoreHybridProjectileProfiles()
        {
            var fireballFlight = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PrefabRoot}/FireballFlight.prefab");
            var boneSpearFlight = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PrefabRoot}/BoneSpearFlight.prefab");
            if (fireballFlight == null || boneSpearFlight == null)
                return;
            ConfigureHybridProjectileProfiles(fireballFlight, boneSpearFlight);
        }

        private static void ConfigureHybridProjectileProfiles(
            GameObject fireballFlight,
            GameObject boneSpearFlight)
        {
            ConfigureHybridProjectileProfile(
                FireballProfilePath,
                fireballFlight,
                disableParticleTrail: true,
                weakenGhostTrail: false);
            ConfigureHybridProjectileProfile(
                BoneSpearProfilePath,
                boneSpearFlight,
                disableParticleTrail: false,
                weakenGhostTrail: true);
        }

        private static void ConfigureHybridProjectileProfile(
            string path,
            GameObject flightPrefab,
            bool disableParticleTrail,
            bool weakenGhostTrail)
        {
            var profile = AssetDatabase.LoadAssetAtPath<ProjectileVisualProfile>(path);
            if (profile == null)
                throw new FileNotFoundException("Hybrid projectile visual profile is required.", path);
            var serialized = new SerializedObject(profile);
            serialized.FindProperty("_flightPrefab").objectReferenceValue = flightPrefab;
            serialized.FindProperty("_impactPrefab").objectReferenceValue = null;
            if (disableParticleTrail)
                serialized.FindProperty("_particleTrail").FindPropertyRelative("_enabled").boolValue = false;
            if (weakenGhostTrail)
            {
                SerializedProperty trail = serialized.FindProperty("_ghostTrail");
                trail.FindPropertyRelative("_enabled").boolValue = true;
                trail.FindPropertyRelative("_alpha").floatValue = 0.14f;
                trail.FindPropertyRelative("_maximumAlive").intValue = 1;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        private static VisualCueProfile CreateOrUpdateCueProfile(
            string name,
            GameObject prefab,
            float lifetime,
            float scale,
            VisualCueAnchor anchor = VisualCueAnchor.TargetPoint,
            int sortingOrderOffset = 20,
            VisualCueOrientationMode orientationMode = VisualCueOrientationMode.World,
            bool stretchXToSourceTarget = false,
            float referenceDistance = 1f)
        {
            string path = $"{ProfileRoot}/{name}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VisualCueProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VisualCueProfile>();
                profile.name = name;
                AssetDatabase.CreateAsset(profile, path);
            }

            var serialized = new SerializedObject(profile);
            serialized.FindProperty("_prefab").objectReferenceValue = prefab;
            serialized.FindProperty("_anchor").enumValueIndex = (int)anchor;
            serialized.FindProperty("_completionPolicy").enumValueIndex =
                (int)VisualCueCompletionPolicy.FireAndForget;
            serialized.FindProperty("_lifetime").floatValue = lifetime;
            serialized.FindProperty("_scale").floatValue = scale;
            serialized.FindProperty("_sortingOrderOffset").intValue = sortingOrderOffset;
            serialized.FindProperty("_orientationMode").enumValueIndex = (int)orientationMode;
            serialized.FindProperty("_stretchXToSourceTarget").boolValue = stretchXToSourceTarget;
            serialized.FindProperty("_referenceDistance").floatValue = referenceDistance;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
