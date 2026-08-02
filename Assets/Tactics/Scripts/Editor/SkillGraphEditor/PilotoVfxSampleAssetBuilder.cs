using System;
using System.Collections.Generic;
using System.IO;
using Tactics.Common.Skills.Graph;
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
        private const string PoisonSpearProfilePath =
            "Assets/Tactics/Arts/PureRun/Tween/Projectiles/AmazonPoisonSpear.asset";

        [MenuItem("Tactics/Tools/Pure Run/Rebuild Piloto VFX Sample Assets")]
        public static void RebuildAll()
        {
            EnsureFolder(PrefabRoot);
            EnsureFolder(ProfileRoot);
            EnsureFolder(MaterialRoot);

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
                false);
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
                false);

            PruneUnreferencedGeneratedMaterials();

            ConfigurePoisonSpearProfile(poisonFlight, poisonImpact);
            CreateOrUpdateCueProfile("LightningLv1", lightningImpact, 0.48f, 0.48f);
            CreateOrUpdateCueProfile("LightningLv2", lightningImpact, 0.54f, 0.56f);
            CreateOrUpdateCueProfile("LightningLv3", lightningImpact, 0.6f, 0.64f);
            CreateOrUpdateCueProfile("AmplifyDamageLv1", amplifyDamage, 0.7f, 0.42f);
            CreateOrUpdateCueProfile("AmplifyDamageLv2", amplifyDamage, 0.8f, 0.68f);
            CreateOrUpdateCueProfile("AmplifyDamageLv3", amplifyDamage, 0.9f, 0.92f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            MageSliceAssetBuilder.RebuildLightningVisualSample();
            NecromancerSliceAssetBuilder.RebuildAmplifyDamageVisualSample();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            TLog.Info("[PilotoVfxSampleAssetBuilder] Three vertical VFX samples rebuilt.");
        }

        private static GameObject BuildPrefab(
            string targetName,
            string sourcePath,
            string[] sourceNodePaths,
            Color tint,
            bool looping)
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

                foreach (string nodePath in sourceNodePaths)
                {
                    var sourceNode = sourceInstance.transform.Find(nodePath);
                    if (sourceNode == null)
                        throw new InvalidOperationException($"Source VFX node is missing: {sourcePath}/{nodePath}");

                    var clone = UnityEngine.Object.Instantiate(sourceNode.gameObject, targetRoot.transform, true);
                    clone.name = sourceNode.name;
                    clone.SetActive(true);
                }

                foreach (var particleSystem in targetRoot.GetComponentsInChildren<ParticleSystem>(true))
                    ConfigureParticleSystem(particleSystem, tint, looping);
                foreach (var forceField in targetRoot.GetComponentsInChildren<ParticleSystemForceField>(true))
                    UnityEngine.Object.DestroyImmediate(forceField);
                CloneMaterials(targetRoot, targetName);

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

        private static void ConfigureParticleSystem(
            ParticleSystem particleSystem,
            Color tint,
            bool looping)
        {
            var main = particleSystem.main;
            main.loop = looping;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.stopAction = ParticleSystemStopAction.None;
            main.maxParticles = Mathf.Min(main.maxParticles, 64);
            main.startColor = new ParticleSystem.MinMaxGradient(tint);

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
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
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
            clone.DisableKeyword("_SOFTPARTICLES_ON");
            if (clone.HasProperty("_SoftParticlesEnabled"))
                clone.SetFloat("_SoftParticlesEnabled", 0f);
            EditorUtility.SetDirty(clone);
            clones.Add(source, clone);
            return clone;
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

        private static VisualCueProfile CreateOrUpdateCueProfile(
            string name,
            GameObject prefab,
            float lifetime,
            float scale)
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
            serialized.FindProperty("_anchor").enumValueIndex = (int)VisualCueAnchor.TargetPoint;
            serialized.FindProperty("_completionPolicy").enumValueIndex =
                (int)VisualCueCompletionPolicy.FireAndForget;
            serialized.FindProperty("_lifetime").floatValue = lifetime;
            serialized.FindProperty("_scale").floatValue = scale;
            serialized.FindProperty("_sortingOrderOffset").intValue = 20;
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
