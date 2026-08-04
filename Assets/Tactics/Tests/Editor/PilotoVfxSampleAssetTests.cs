using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Tactics.Common.Skills.Graph;
using Tactics.Editor.SkillGraphEditor;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Tactics.Tests.Editor
{
    /// <summary>
    /// Guards the three Piloto VFX vertical samples and their project-owned adaptation boundary.
    /// </summary>
    public sealed class PilotoVfxSampleAssetTests
    {
        private const string AdaptedRoot = "Assets/Tactics/Arts/PureRun/VFX/PilotoAdapted";
        private const string ProjectileProfilePath =
            "Assets/Tactics/Arts/PureRun/Tween/Projectiles/AmazonPoisonSpear.asset";
        private const string FireballProfilePath =
            "Assets/Tactics/Arts/PureRun/Tween/Projectiles/Fire.asset";
        private const string BoneSpearProfilePath =
            "Assets/Tactics/Arts/PureRun/Tween/Projectiles/BoneSpear.asset";

        [Test]
        public void VendorShowcaseAssembly_IsEditorOnly()
        {
            const string assemblyPath = "Assets/Piloto Studio/Scripts/PilotoStudio.Showcase.Editor.asmdef";
            const string vendorScriptPath = "Assets/Piloto Studio/Scripts/ParticleHandler.cs";
            Assert.That(File.Exists(assemblyPath), Is.True, assemblyPath);
            var definition = JsonUtility.FromJson<AssemblyDefinitionData>(File.ReadAllText(assemblyPath));
            Assert.That(definition, Is.Not.Null, assemblyPath);
            Assert.That(definition.Name, Is.EqualTo("PilotoStudio.Showcase.Editor"));
            Assert.That(definition.IncludePlatforms, Is.EquivalentTo(new[] { "Editor" }));
            Assert.That(definition.AutoReferenced, Is.False);

            var owner = CompilationPipeline.GetAssemblies(AssembliesType.Editor)
                .SingleOrDefault(assembly => assembly.sourceFiles != null && assembly.sourceFiles.Any(
                    sourceFile => sourceFile.Replace('\\', '/').EndsWith(
                        vendorScriptPath, StringComparison.Ordinal)));
            Assert.That(owner, Is.Not.Null,
                $"No Editor compilation assembly owns '{vendorScriptPath}'.");
            Assert.That(owner.name, Is.EqualTo(definition.Name));
        }

        [Test]
        public void VendorShowcaseScript_DoesNotUseForbiddenDebugLogging()
        {
            const string sourcePath = "Assets/Piloto Studio/Scripts/ParticleHandler.cs";
            string source = File.ReadAllText(sourcePath);
            Assert.That(source, Does.Not.Match(
                @"\bDebug\s*\.\s*(Log|LogWarning|LogError|LogException)\s*\("),
                "Project source policy forbids Debug.Log* even in editor-only vendor showcase scripts.");
        }

        [Test]
        public void GeneratedMaterialPruner_RemovesOnlyUnreferencedManagedMaterials()
        {
            const string orphanPath = AdaptedRoot + "/Materials/__OrphanMaterialTest.mat";
            var referencedMaterials = AssetDatabase.FindAssets("t:Prefab", new[] { AdaptedRoot + "/Prefabs" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(prefab => prefab != null)
                .SelectMany(prefab => prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
                .SelectMany(renderer => renderer.sharedMaterials.Append(renderer.trailMaterial))
                .Where(material => material != null)
                .Distinct()
                .ToArray();
            Assert.That(referencedMaterials, Is.Not.Empty, "Adapted prefabs must reference managed materials.");
            AssetDatabase.DeleteAsset(orphanPath);
            AssetDatabase.CreateAsset(new Material(referencedMaterials[0]), orphanPath);

            try
            {
                var pruneMethod = typeof(PilotoVfxSampleAssetBuilder).GetMethod(
                    "PruneUnreferencedGeneratedMaterials",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                Assert.That(pruneMethod, Is.Not.Null);
                pruneMethod.Invoke(null, null);

                Assert.That(AssetDatabase.LoadAssetAtPath<Material>(orphanPath), Is.Null);
                foreach (Material material in referencedMaterials)
                {
                    Assert.That(material, Is.Not.Null,
                        "Pruning must retain every material referenced by an adapted prefab.");
                }
            }
            finally
            {
                AssetDatabase.DeleteAsset(orphanPath);
            }
        }

        [Test]
        public void ProjectileVisualProfile_ExposesFlightAndImpactStages()
        {
            Assert.That(typeof(ProjectileVisualProfile).GetProperty("FlightPrefab"), Is.Not.Null);
            Assert.That(typeof(ProjectileVisualProfile).GetProperty("ImpactPrefab"), Is.Not.Null);
            Assert.That(typeof(ProjectileVisualProfile).GetProperty("ImpactLifetime"), Is.Not.Null);
        }

        [Test]
        public void PlayVisualCueNode_IsCreatableAndRegistered()
        {
            Assert.That(Enum.TryParse("PlayVisualCue", out SkillGraphNodeType nodeType), Is.True);
            var record = SkillGraphNodeRecord.Create(nodeType);
            Assert.That(record, Is.Not.Null);
            Assert.That(record.GetType().GetProperty("Profile"), Is.Not.Null);
            Assert.That(SkillNodeExecutorRegistry.Get(nodeType), Is.Not.Null);
        }

        [TestCase("PoisonSpearFlight", 1, 4)]
        [TestCase("PoisonSpearImpact", 1, 4)]
        [TestCase("LightningImpact", 1, 1)]
        [TestCase("AmplifyDamageCurse", 2, 6)]
        [TestCase("AmplifyDamageSigilGround", 3, 3)]
        [TestCase("AmplifyDamageSigilForeground", 1, 1)]
        [TestCase("AmplifyDamageSigilGroundV2", 5, 5)]
        [TestCase("AmplifyDamageSigilRearFlamesV2", 3, 3)]
        [TestCase("AmplifyDamageSigilForegroundFlamesV2", 5, 5)]
        public void AdaptedPrefabs_AreLightweightProjectOwnedParticleCompositions(
            string prefabName,
            int minimumParticleSystems,
            int maximumParticleSystems)
        {
            string path = $"{AdaptedRoot}/Prefabs/{prefabName}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            int count = prefab.GetComponentsInChildren<ParticleSystem>(true).Length;
            Assert.That(count, Is.InRange(minimumParticleSystems, maximumParticleSystems), path);
            Assert.That(prefab.GetComponentsInChildren<ParticleSystemForceField>(true), Is.Empty, path);
            foreach (var particleSystem in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                Assert.That(particleSystem.main.simulationSpace,
                    Is.EqualTo(ParticleSystemSimulationSpace.Local), path);
                Assert.That(particleSystem.velocityOverLifetime.enabled, Is.False, path);
                Assert.That(particleSystem.forceOverLifetime.enabled, Is.False, path);
                Assert.That(particleSystem.externalForces.enabled, Is.False, path);
                Assert.That(particleSystem.inheritVelocity.enabled, Is.False, path);
                Assert.That(particleSystem.subEmitters.enabled, Is.False, path);
                Assert.That(particleSystem.trails.enabled, Is.False, path);
                var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
                if (prefabName == "AmplifyDamageSigilGround" ||
                    prefabName == "AmplifyDamageSigilGroundV2")
                {
                    Assert.That(new[]
                        {
                            ParticleSystemRenderMode.Billboard,
                            ParticleSystemRenderMode.Mesh
                        },
                        Does.Contain(renderer.renderMode),
                        path);
                }
                else
                {
                    Assert.That(renderer.renderMode, Is.EqualTo(ParticleSystemRenderMode.Billboard), path);
                    Assert.That(renderer.alignment, Is.EqualTo(ParticleSystemRenderSpace.View), path);
                }
                Assert.That(renderer.trailMaterial, Is.Null, path);
            }
            foreach (var renderer in prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                Assert.That(renderer.sharedMaterial, Is.Not.Null, path);
                foreach (var material in renderer.sharedMaterials.Where(material => material != null))
                {
                    Assert.That(material.shader, Is.Not.Null, path);
                    Assert.That(material.shader.isSupported, Is.True, material.name);
                    Assert.That(AssetDatabase.GetAssetPath(material),
                        Does.StartWith($"{AdaptedRoot}/Materials/"), material.name);
                    if (material.HasProperty("_USESOFTALPHA"))
                    {
                        Assert.That(
                            material.GetFloat("_USESOFTALPHA"),
                            Is.EqualTo(0f).Within(0.0001f),
                            $"{material.name} must not depend on a scene depth texture.");
                    }
                }
            }
        }

        [Test]
        public void AmplifyDamageCurse_UsesPreviewCompatibleProjectShader()
        {
            const string path = AdaptedRoot + "/Prefabs/AmplifyDamageCurse.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);

            Material[] materials = prefab.GetComponentsInChildren<ParticleSystemRenderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null)
                .Distinct()
                .ToArray();
            Assert.That(materials, Is.Not.Empty, path);
            foreach (Material material in materials)
            {
                Assert.That(material.shader.name, Is.EqualTo("Tactics/PureRun/ParticleTextureUnlit"));
                Assert.That(material.mainTexture, Is.Not.Null, material.name);
            }
        }

        [Test]
        public void AmplifyDamageCurse_UsesCameraFacingParticleRotation()
        {
            const string path = AdaptedRoot + "/Prefabs/AmplifyDamageCurse.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);

            ParticleSystem[] systems = prefab.GetComponentsInChildren<ParticleSystem>(true);
            Assert.That(systems, Is.Not.Empty, path);
            foreach (ParticleSystem system in systems)
            {
                var main = system.main;
                Assert.That(main.startRotation3D, Is.False,
                    $"{system.name} retains a 3D start rotation and can become edge-on to the 2D camera.");
            }
        }

        [TestCase("AmplifyDamageSigilGround")]
        [TestCase("AmplifyDamageSigilForeground")]
        [TestCase("AmplifyDamageSigilGroundV2")]
        [TestCase("AmplifyDamageSigilRearFlamesV2")]
        [TestCase("AmplifyDamageSigilForegroundFlamesV2")]
        public void AmplifyDamageSigil_UsesCameraFacingParticleRotation(string prefabName)
        {
            string path = $"{AdaptedRoot}/Prefabs/{prefabName}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);

            foreach (ParticleSystem system in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                Assert.That(system.main.startRotation3D, Is.False,
                    $"{system.name} retains a 3D start rotation and can become edge-on to the 2D camera.");
                Assert.That(Mathf.Abs(system.transform.localPosition.z), Is.LessThan(0.0001f), system.name);
            }
        }

        [Test]
        public void AmplifyDamageSigil_UsesApprovedMotifsOnly()
        {
            var ground = AssetDatabase.LoadAssetAtPath<GameObject>(
                AdaptedRoot + "/Prefabs/AmplifyDamageSigilGround.prefab");
            var foreground = AssetDatabase.LoadAssetAtPath<GameObject>(
                AdaptedRoot + "/Prefabs/AmplifyDamageSigilForeground.prefab");
            Assert.That(ground, Is.Not.Null);
            Assert.That(foreground, Is.Not.Null);

            string[] groundNames = ground.GetComponentsInChildren<Transform>(true)
                .Select(transform => transform.name)
                .ToArray();
            Assert.That(groundNames, Does.Contain("MagicCircleAndRunes"));
            Assert.That(groundNames, Does.Contain("ArcaneRunes_Atlas_002"));
            Assert.That(groundNames, Does.Contain("ShadowPool"));
            Assert.That(groundNames.Any(name => name.Contains("Dot", StringComparison.Ordinal)), Is.False);
            Assert.That(groundNames.Any(name => name.Contains("LightPilar")), Is.False);
            Assert.That(groundNames.Any(name => name.Contains("WispySmoke")), Is.False);

            string[] foregroundNames = foreground.GetComponentsInChildren<Transform>(true)
                .Select(transform => transform.name)
                .ToArray();
            Assert.That(foregroundNames, Does.Contain("NearSideLowFlames"));
            Assert.That(foregroundNames.Any(name => name.Contains("LightPilar")), Is.False);
            Assert.That(foregroundNames.Any(name => name.Contains("WispySmoke")), Is.False);
        }

        [Test]
        public void AmplifyDamageSigilV2_UsesLargeIndependentGroundMotifsAndEightAnchoredFlameNodes()
        {
            var ground = AssetDatabase.LoadAssetAtPath<GameObject>(
                AdaptedRoot + "/Prefabs/AmplifyDamageSigilGroundV2.prefab");
            var rear = AssetDatabase.LoadAssetAtPath<GameObject>(
                AdaptedRoot + "/Prefabs/AmplifyDamageSigilRearFlamesV2.prefab");
            var foreground = AssetDatabase.LoadAssetAtPath<GameObject>(
                AdaptedRoot + "/Prefabs/AmplifyDamageSigilForegroundFlamesV2.prefab");
            Assert.That(ground, Is.Not.Null);
            Assert.That(rear, Is.Not.Null);
            Assert.That(foreground, Is.Not.Null);

            string[] groundNames = ground.GetComponentsInChildren<Transform>(true)
                .Select(transform => transform.name)
                .ToArray();
            Assert.That(groundNames, Does.Contain("OuterBrightRing"));
            Assert.That(groundNames, Does.Contain("InnerBrightRing"));
            Assert.That(groundNames, Does.Contain("LowIntensityRuneBand"));
            Assert.That(groundNames, Does.Contain("OriginalCentralAngularSigil"));
            Assert.That(groundNames, Does.Contain("DarkGroundDisc"));
            Transform central = ground.GetComponentsInChildren<Transform>(true)
                .Single(transform => transform.name == "OriginalCentralAngularSigil");
            Material centralMaterial = central.GetComponentInChildren<ParticleSystemRenderer>(true)
                .sharedMaterial;
            Assert.That(centralMaterial, Is.Not.Null);
            Assert.That(centralMaterial.mainTexture, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(centralMaterial.mainTexture), Is.EqualTo(
                AdaptedRoot + "/Textures/AmplifyDamageSigilCentralV2.asset"));
            AssertParticleStartColor(ground, "DarkGroundDisc", new Color32(0x19, 0x0D, 0x24, 0x80));
            AssertParticleStartColor(ground, "OuterBrightRing", new Color32(0x55, 0x30, 0xA9, 0xC7));
            AssertParticleStartColor(ground, "InnerBrightRing", new Color32(0xF1, 0x3A, 0x62, 0xEB));
            AssertParticleStartColor(ground, "LowIntensityRuneBand", new Color32(0xA8, 0x32, 0xB7, 0xA3));
            AssertParticleStartColor(ground, "OriginalCentralAngularSigil", new Color32(0xFF, 0x46, 0x66, 0xF5));

            Transform[] flameNodes = rear.GetComponentsInChildren<Transform>(true)
                .Concat(foreground.GetComponentsInChildren<Transform>(true))
                .Where(transform => transform.name.StartsWith("FlameNode_", StringComparison.Ordinal))
                .OrderBy(transform => transform.name)
                .ToArray();
            Assert.That(flameNodes, Has.Length.EqualTo(8));
            Assert.That(flameNodes.Select(node => node.name), Is.EqualTo(
                Enumerable.Range(0, 8).Select(index => $"FlameNode_{index:D2}")));

            float[] delays = flameNodes
                .Select(node => node.GetComponentInChildren<ParticleSystem>(true).main.startDelay.constant)
                .ToArray();
            for (int index = 0; index < delays.Length; index++)
            {
                Assert.That(delays[index], Is.EqualTo(0.10f + index * 0.06f).Within(0.001f));
                float angle = index * 45f * Mathf.Deg2Rad;
                Assert.That(flameNodes[index].localPosition.x,
                    Is.EqualTo(Mathf.Sin(angle) * 0.47f).Within(0.001f));
                Assert.That(flameNodes[index].localPosition.y,
                    Is.EqualTo(Mathf.Cos(angle) * 0.225f).Within(0.001f));
            }

            Assert.That(rear.GetComponentsInChildren<ParticleSystem>(true), Has.Length.EqualTo(3));
            Assert.That(foreground.GetComponentsInChildren<ParticleSystem>(true), Has.Length.EqualTo(5));

            foreach (ParticleSystem system in rear.GetComponentsInChildren<ParticleSystem>(true)
                         .Concat(foreground.GetComponentsInChildren<ParticleSystem>(true)))
            {
                Assert.That(system.main.startSpeed.constantMax, Is.Zero.Within(0.001f));
                Assert.That(system.main.startSize3D, Is.True);
                Assert.That(system.main.startSizeX.constantMin, Is.EqualTo(0.12f).Within(0.001f));
                Assert.That(system.main.startSizeX.constantMax, Is.EqualTo(0.15f).Within(0.001f));
                Assert.That(system.main.startSizeY.constantMin, Is.EqualTo(0.18f).Within(0.001f));
                Assert.That(system.main.startSizeY.constantMax, Is.EqualTo(0.22f).Within(0.001f));
                Assert.That(system.main.scalingMode, Is.EqualTo(ParticleSystemScalingMode.Shape));
                Assert.That(system.GetComponent<ParticleSystemRenderer>().pivot,
                    Is.EqualTo(new Vector3(0f, -0.5f, 0f)));
                AssertColor(system.main.startColor.color, Color.white);
                Gradient flameGradient = system.colorOverLifetime.color.gradient;
                GradientColorKey[] colorKeys = flameGradient.colorKeys;
                Assert.That(colorKeys, Has.Length.EqualTo(4));
                Assert.That(colorKeys[0].time, Is.EqualTo(0f).Within(0.001f));
                Assert.That(colorKeys[1].time, Is.EqualTo(0.42f).Within(0.001f));
                Assert.That(colorKeys[2].time, Is.EqualTo(0.70f).Within(0.001f));
                Assert.That(colorKeys[3].time, Is.EqualTo(1f).Within(0.001f));
                AssertColor(colorKeys[0].color, new Color32(0xFF, 0x33, 0x4F, 0xFF));
                AssertColor(colorKeys[1].color, new Color32(0xD7, 0x2B, 0x88, 0xFF));
                AssertColor(colorKeys[2].color, new Color32(0x6A, 0x2E, 0xA8, 0xFF));
                AssertColor(colorKeys[3].color, new Color32(0x6A, 0x2E, 0xA8, 0xFF));
                ParticleSystem.TextureSheetAnimationModule textureSheet =
                    system.textureSheetAnimation;
                Assert.That(textureSheet.enabled, Is.True);
                Assert.That(textureSheet.animation, Is.EqualTo(ParticleSystemAnimationType.WholeSheet));
                Assert.That(textureSheet.numTilesX, Is.EqualTo(4));
                Assert.That(textureSheet.numTilesY, Is.EqualTo(4));
                Assert.That(textureSheet.cycleCount, Is.EqualTo(1));

                Material material = system.GetComponent<ParticleSystemRenderer>().sharedMaterial;
                Assert.That(material, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(material.mainTexture), Is.EqualTo(
                    "Assets/Piloto Studio/Textures/Tx_Fire_ground_01.png"));
            }
        }

        [Test]
        public void AmplifyDamageSigilV2_FlamesIgniteClockwiseAcrossOneCircle()
        {
            var rearPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                AdaptedRoot + "/Prefabs/AmplifyDamageSigilRearFlamesV2.prefab");
            var foregroundPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                AdaptedRoot + "/Prefabs/AmplifyDamageSigilForegroundFlamesV2.prefab");
            var rear = UnityEngine.Object.Instantiate(rearPrefab);
            var foreground = UnityEngine.Object.Instantiate(foregroundPrefab);
            try
            {
                ParticleSystem[] systems = rear.GetComponentsInChildren<ParticleSystem>(true)
                    .Concat(foreground.GetComponentsInChildren<ParticleSystem>(true))
                    .ToArray();
                Assert.That(systems, Has.Length.EqualTo(8));
                Assert.That(CountActiveFlameNodes(systems, 0.09f), Is.Zero);
                Assert.That(CountActiveFlameNodes(systems, 0.12f), Is.EqualTo(1));
                Assert.That(CountActiveFlameNodes(systems, 0.24f), Is.EqualTo(3));
                Assert.That(CountActiveFlameNodes(systems, 0.36f), Is.EqualTo(5));
                Assert.That(CountActiveFlameNodes(systems, 0.54f), Is.EqualTo(8));
                ParticleSystem firstNode = systems.Single(system =>
                    system.name == "FlameNode_00");
                ParticleSystem lastNode = systems.Single(system =>
                    system.name == "FlameNode_07");
                Color olderFlame = SampleFlameColor(firstNode, 0.72f);
                Color newerFlame = SampleFlameColor(lastNode, 0.72f);
                Assert.That(olderFlame.b, Is.GreaterThan(newerFlame.b + 0.05f));
                Assert.That(newerFlame.r, Is.GreaterThan(olderFlame.r + 0.05f));
                Assert.That(CountActiveFlameNodes(systems, 1.06f), Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rear);
                UnityEngine.Object.DestroyImmediate(foreground);
            }
        }

        [Test]
        public void AmplifyDamageSigilV2_GroundMotifsKeepTheirIndependentFootprints()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                AdaptedRoot + "/Prefabs/AmplifyDamageSigilGroundV2.prefab");
            Assert.That(prefab, Is.Not.Null);
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                AssertMotifWidth(instance, "DarkGroundDisc", 1.45f);
                AssertMotifWidth(instance, "OuterBrightRing", 1.35f);
                AssertMotifWidth(instance, "LowIntensityRuneBand", 1.22f);
                AssertMotifWidth(instance, "InnerBrightRing", 1.12f);
                AssertMotifWidth(instance, "OriginalCentralAngularSigil", 0.54f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void AmplifyDamageSigilV2_Lv1FlameRootsFollowTheVisibleOuterRail()
        {
            var rearPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                AdaptedRoot + "/Prefabs/AmplifyDamageSigilRearFlamesV2.prefab");
            var foregroundPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                AdaptedRoot + "/Prefabs/AmplifyDamageSigilForegroundFlamesV2.prefab");
            var rear = UnityEngine.Object.Instantiate(rearPrefab);
            var foreground = UnityEngine.Object.Instantiate(foregroundPrefab);
            try
            {
                Transform[] nodes = rear.GetComponentsInChildren<Transform>(true)
                    .Concat(foreground.GetComponentsInChildren<Transform>(true))
                    .Where(transform => transform.name.StartsWith("FlameNode_", StringComparison.Ordinal))
                    .OrderBy(transform => transform.name)
                    .ToArray();
                Assert.That(nodes, Has.Length.EqualTo(8));
                for (int index = 0; index < nodes.Length; index++)
                {
                    float normalizedRadius = Mathf.Sqrt(
                        Mathf.Pow(nodes[index].localPosition.x / 0.47f, 2f) +
                        Mathf.Pow(nodes[index].localPosition.y / 0.225f, 2f));
                    Assert.That(normalizedRadius, Is.EqualTo(1f).Within(0.002f), nodes[index].name);
                    Assert.That(nodes[index].GetComponentInChildren<ParticleSystemRenderer>(true).pivot,
                        Is.EqualTo(new Vector3(0f, -0.5f, 0f)), nodes[index].name);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rear);
                UnityEngine.Object.DestroyImmediate(foreground);
            }
        }

        [Test]
        public void AmplifyDamageSigilV2_LevelScalingDoesNotEnlargeFlameParticles()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                AdaptedRoot + "/Prefabs/AmplifyDamageSigilForegroundFlamesV2.prefab");
            Assert.That(prefab, Is.Not.Null);
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                instance.transform.localScale = Vector3.one * (3.15f / 1.35f);
                float maximumSize = 0f;
                foreach (ParticleSystem system in instance.GetComponentsInChildren<ParticleSystem>(true))
                {
                    system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    system.Simulate(0.7f, false, true, true);
                    var particles = new ParticleSystem.Particle[system.particleCount];
                    int count = system.GetParticles(particles);
                    for (int index = 0; index < count; index++)
                        maximumSize = Mathf.Max(
                            maximumSize,
                            particles[index].GetCurrentSize3D(system).y);
                }
                Assert.That(maximumSize, Is.GreaterThan(0f));
                Assert.That(maximumSize, Is.LessThanOrEqualTo(0.221f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static int CountActiveFlameNodes(ParticleSystem[] systems, float elapsed)
        {
            int count = 0;
            foreach (ParticleSystem system in systems)
            {
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                system.useAutoRandomSeed = false;
                system.randomSeed = 1u;
                system.Simulate(elapsed, false, true, true);
                if (system.particleCount > 0)
                    count++;
            }
            return count;
        }

        private static Color SampleFlameColor(ParticleSystem system, float elapsed)
        {
            system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.useAutoRandomSeed = false;
            system.randomSeed = 1u;
            system.Simulate(elapsed, false, true, true);
            var particles = new ParticleSystem.Particle[system.particleCount];
            int count = system.GetParticles(particles);
            Assert.That(count, Is.EqualTo(1), system.name);
            return particles[0].GetCurrentColor(system);
        }

        private static void AssertParticleStartColor(GameObject root, string motifName, Color expected)
        {
            Transform motif = root.GetComponentsInChildren<Transform>(true)
                .Single(transform => transform.name == motifName);
            ParticleSystem system = motif.GetComponentInChildren<ParticleSystem>(true);
            Assert.That(system, Is.Not.Null, motifName);
            AssertColor(system.main.startColor.color, expected);
        }

        private static void AssertColor(Color actual, Color expected, float tolerance = 0.01f)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(tolerance));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(tolerance));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(tolerance));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(tolerance));
        }

        private static void AssertMotifWidth(GameObject root, string motifName, float expectedWidth)
        {
            Transform motif = root.GetComponentsInChildren<Transform>(true)
                .Single(transform => transform.name == motifName);
            Bounds bounds = SampleParticleBounds(motif.gameObject);
            Assert.That(bounds.size.x, Is.EqualTo(expectedWidth).Within(0.06f), motifName);
        }

        [TestCase("AmplifyDamageSigilGround", 0f)]
        [TestCase("AmplifyDamageSigilForeground", -0.08f)]
        public void AmplifyDamageSigil_ParticleFootprintIsCenteredOnCueRoot(
            string prefabName,
            float expectedVerticalOffset)
        {
            string path = $"{AdaptedRoot}/Prefabs/{prefabName}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                Bounds bounds = SampleParticleBounds(instance);
                Vector3 offset = bounds.center - instance.transform.position;
                Assert.That(offset.x, Is.EqualTo(0f).Within(0.04f), path);
                Assert.That(offset.y, Is.EqualTo(expectedVerticalOffset).Within(0.04f), path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [TestCase(1, 1f, 0.95f)]
        [TestCase(2, 2f, 1.05f)]
        [TestCase(3, 2.6f, 1.15f)]
        public void AmplifyDamageSigilProfiles_ArePairedAndGroundAnchored(
            int level,
            float expectedScale,
            float expectedLifetime)
        {
            VisualCueProfile ground = AssetDatabase.LoadAssetAtPath<VisualCueProfile>(
                $"{AdaptedRoot}/Profiles/AmplifyDamageSigilGroundLv{level}.asset");
            VisualCueProfile foreground = AssetDatabase.LoadAssetAtPath<VisualCueProfile>(
                $"{AdaptedRoot}/Profiles/AmplifyDamageSigilForegroundLv{level}.asset");
            Assert.That(ground, Is.Not.Null);
            Assert.That(foreground, Is.Not.Null);

            foreach (VisualCueProfile profile in new[] { ground, foreground })
            {
                Assert.That(profile.Anchor, Is.EqualTo(VisualCueAnchor.PrimaryTargetGround));
                Assert.That(profile.CompletionPolicy, Is.EqualTo(VisualCueCompletionPolicy.FireAndForget));
                Assert.That(profile.Scale, Is.EqualTo(expectedScale).Within(0.001f));
                Assert.That(profile.Lifetime, Is.EqualTo(expectedLifetime).Within(0.001f));
            }
            Assert.That(ground.SortingOrderOffset, Is.EqualTo(-2));
            Assert.That(foreground.SortingOrderOffset, Is.EqualTo(2));
        }

        [TestCase(1, 1f, 0.95f)]
        [TestCase(2, 2.35f / 1.35f, 1.05f)]
        [TestCase(3, 3.15f / 1.35f, 1.15f)]
        public void AmplifyDamageSigilV2Profiles_AreTripleLayeredAndGroundAnchored(
            int level,
            float expectedScale,
            float expectedLifetime)
        {
            VisualCueProfile ground = AssetDatabase.LoadAssetAtPath<VisualCueProfile>(
                $"{AdaptedRoot}/Profiles/AmplifyDamageSigilGroundV2Lv{level}.asset");
            VisualCueProfile rear = AssetDatabase.LoadAssetAtPath<VisualCueProfile>(
                $"{AdaptedRoot}/Profiles/AmplifyDamageSigilRearFlamesV2Lv{level}.asset");
            VisualCueProfile foreground = AssetDatabase.LoadAssetAtPath<VisualCueProfile>(
                $"{AdaptedRoot}/Profiles/AmplifyDamageSigilForegroundFlamesV2Lv{level}.asset");
            Assert.That(ground, Is.Not.Null);
            Assert.That(rear, Is.Not.Null);
            Assert.That(foreground, Is.Not.Null);

            foreach (VisualCueProfile profile in new[] { ground, rear, foreground })
            {
                Assert.That(profile.Anchor, Is.EqualTo(VisualCueAnchor.PrimaryTargetGround));
                Assert.That(profile.CompletionPolicy, Is.EqualTo(VisualCueCompletionPolicy.FireAndForget));
                Assert.That(profile.Scale, Is.EqualTo(expectedScale).Within(0.001f));
                Assert.That(profile.Lifetime, Is.EqualTo(expectedLifetime).Within(0.001f));
            }
            Assert.That(ground.SortingOrderOffset, Is.EqualTo(-2));
            Assert.That(rear.SortingOrderOffset, Is.EqualTo(-1));
            Assert.That(foreground.SortingOrderOffset, Is.EqualTo(2));
        }

        [Test]
        public void PoisonSpearProfile_ReferencesAdaptedFlightAndImpactPrefabs()
        {
            var profile = AssetDatabase.LoadAssetAtPath<ProjectileVisualProfile>(ProjectileProfilePath);
            Assert.That(profile, Is.Not.Null, ProjectileProfilePath);
            var flightProperty = typeof(ProjectileVisualProfile).GetProperty("FlightPrefab");
            var impactProperty = typeof(ProjectileVisualProfile).GetProperty("ImpactPrefab");
            Assert.That(flightProperty, Is.Not.Null);
            Assert.That(impactProperty, Is.Not.Null);
            Assert.That(flightProperty.GetValue(profile), Is.Not.Null);
            Assert.That(impactProperty.GetValue(profile), Is.Not.Null);
        }

        [Test]
        public void HybridProjectileProfiles_PreserveCoreProfilesAndUseAdaptedFlightPrefabs()
        {
            ProjectileVisualProfile fireball = AssetDatabase.LoadAssetAtPath<ProjectileVisualProfile>(
                FireballProfilePath);
            ProjectileVisualProfile boneSpear = AssetDatabase.LoadAssetAtPath<ProjectileVisualProfile>(
                BoneSpearProfilePath);

            Assert.That(fireball, Is.Not.Null);
            Assert.That(boneSpear, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(fireball.FlightPrefab),
                Is.EqualTo(AdaptedRoot + "/Prefabs/FireballFlight.prefab"));
            Assert.That(AssetDatabase.GetAssetPath(boneSpear.FlightPrefab),
                Is.EqualTo(AdaptedRoot + "/Prefabs/BoneSpearFlight.prefab"));
            Assert.That(fireball.ImpactPrefab, Is.Null);
            Assert.That(boneSpear.ImpactPrefab, Is.Null);
            Assert.That(fireball.ParticleTrail.Enabled, Is.False);
            Assert.That(boneSpear.Sprite, Is.Not.Null);
            Assert.That(boneSpear.RotateAlongTangent, Is.True);
            Assert.That(boneSpear.GhostTrail.Enabled, Is.True);
            Assert.That(boneSpear.GhostTrail.MaximumAlive, Is.EqualTo(1));
            Assert.That(boneSpear.GhostTrail.Alpha, Is.EqualTo(0.14f).Within(0.001f));
        }

        [TestCase("FireballFlight", 2, true)]
        [TestCase("BoneSpearFlight", 2, true)]
        [TestCase("FireballChargeLv1", 1, false)]
        [TestCase("FireballChargeLv2", 2, false)]
        [TestCase("FireballChargeLv3", 3, false)]
        [TestCase("FireballImpactLv1", 2, false)]
        [TestCase("FireballImpactLv2", 3, false)]
        [TestCase("FireballImpactLv3", 4, false)]
        [TestCase("FireballDetonationLv3", 3, false)]
        [TestCase("BoneSpearChargeLv1", 1, false)]
        [TestCase("BoneSpearChargeLv2", 2, false)]
        [TestCase("BoneSpearChargeLv3", 3, false)]
        [TestCase("BoneSpearImpactLv1", 2, false)]
        [TestCase("BoneSpearImpactLv2", 3, false)]
        [TestCase("BoneSpearImpactLv3", 4, false)]
        [TestCase("ThrustStrikeLv1", 1, false)]
        [TestCase("ThrustStrikeLv2", 2, false)]
        [TestCase("ThrustStrikeLv3", 3, false)]
        [TestCase("ThrustHitLv1", 1, false)]
        [TestCase("ThrustHitLv2", 2, false)]
        [TestCase("ThrustHitLv3", 3, false)]
        public void HybridAdaptedPrefab_IsFlattenedAndSelfContained(
            string prefabName,
            int particleCount,
            bool looping)
        {
            string path = $"{AdaptedRoot}/Prefabs/{prefabName}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            ParticleSystem[] systems = prefab.GetComponentsInChildren<ParticleSystem>(true);
            Assert.That(systems, Has.Length.EqualTo(particleCount), path);
            Assert.That(prefab.GetComponentsInChildren<ParticleSystemForceField>(true), Is.Empty, path);

            foreach (ParticleSystem system in systems)
            {
                Assert.That(system.main.loop, Is.EqualTo(looping), $"{path}/{system.name}");
                Assert.That(system.main.playOnAwake, Is.True, $"{path}/{system.name}");
                Assert.That(system.main.simulationSpace,
                    Is.EqualTo(ParticleSystemSimulationSpace.Local), $"{path}/{system.name}");
                Assert.That(system.collision.enabled, Is.False, $"{path}/{system.name}");
                Assert.That(system.trigger.enabled, Is.False, $"{path}/{system.name}");
                Assert.That(system.trails.enabled, Is.False, $"{path}/{system.name}");
                Assert.That(system.transform.localPosition.z, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(system.transform.localRotation, Is.EqualTo(Quaternion.identity));
                var renderer = system.GetComponent<ParticleSystemRenderer>();
                Assert.That(renderer, Is.Not.Null);
                Assert.That(renderer.renderMode, Is.EqualTo(ParticleSystemRenderMode.Billboard));
                Assert.That(renderer.alignment, Is.EqualTo(ParticleSystemRenderSpace.View));
                Assert.That(renderer.trailMaterial, Is.Null);
                Assert.That(renderer.sharedMaterial, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(renderer.sharedMaterial),
                    Does.StartWith(AdaptedRoot + "/Materials/"));
            }
        }

        [TestCase("FireballCharge", VisualCueAnchor.Caster, false)]
        [TestCase("FireballImpact", VisualCueAnchor.PrimaryTarget, false)]
        [TestCase("BoneSpearCharge", VisualCueAnchor.Caster, false)]
        [TestCase("BoneSpearImpact", VisualCueAnchor.PrimaryTarget, false)]
        [TestCase("ThrustStrike", VisualCueAnchor.Caster, true)]
        [TestCase("ThrustHit", VisualCueAnchor.PrimaryTarget, false)]
        public void HybridProfiles_AreLevelSpecificAndUseExpectedTransformContract(
            string profilePrefix,
            VisualCueAnchor anchor,
            bool directional)
        {
            for (int level = 1; level <= 3; level++)
            {
                string path = $"{AdaptedRoot}/Profiles/{profilePrefix}Lv{level}.asset";
                VisualCueProfile profile = AssetDatabase.LoadAssetAtPath<VisualCueProfile>(path);
                Assert.That(profile, Is.Not.Null, path);
                Assert.That(profile.Anchor, Is.EqualTo(anchor), path);
                Assert.That(profile.OrientationMode, Is.EqualTo(directional
                    ? VisualCueOrientationMode.SourceToTarget
                    : VisualCueOrientationMode.World), path);
                Assert.That(profile.StretchXToSourceTarget,
                    Is.EqualTo(profilePrefix == "ThrustStrike"), path);
                Assert.That(profile.CompletionPolicy,
                    Is.EqualTo(VisualCueCompletionPolicy.FireAndForget), path);
            }
        }

        [Test]
        public void LightningImpact_VisibleBottomIsAlignedToPrefabRoot()
        {
            const string path = AdaptedRoot + "/Prefabs/LightningImpact.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            Assert.That(prefab, Is.Not.Null, path);
            Assert.That(prefab.transform.childCount, Is.EqualTo(1), path);
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                float minimumY = SampleParticleMinimumY(instance);
                Assert.That(minimumY, Is.EqualTo(instance.transform.position.y).Within(0.02f), path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [TestCase("PoisonSpearFlight")]
        [TestCase("PoisonSpearImpact")]
        [TestCase("AmplifyDamageCurse")]
        [TestCase("AmplifyDamageSigilGround")]
        [TestCase("AmplifyDamageSigilForeground")]
        public void AdaptedPrefab_DoesNotRetainShowcasePlacement(string prefabName)
        {
            string path = $"{AdaptedRoot}/Prefabs/{prefabName}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            Assert.That(prefab, Is.Not.Null, path);
            Assert.That(prefab.transform.childCount, Is.GreaterThan(0), path);
            float maximumAuthoredOffset = prefabName == "AmplifyDamageSigilForeground" ? 0.0225f : 0.000001f;
            Assert.That(prefab.transform.GetChild(0).localPosition.sqrMagnitude,
                Is.LessThan(maximumAuthoredOffset), path);
        }

        [Test]
        public void AmplifyDamageCurse_EmitsParticlesNearItsRootDuringPreviewLifetime()
        {
            const string path = AdaptedRoot + "/Prefabs/AmplifyDamageCurse.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);

            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
                Assert.That(systems, Is.Not.Empty, path);
                int maximumParticleCount = 0;
                byte maximumParticleAlpha = 0;
                float maximumBillboardArea = 0f;
                Bounds combinedBounds = default;
                bool hasBounds = false;
                var countsByTime = new Dictionary<float, int>();

                foreach (float elapsed in new[] { 0.1f, 0.25f, 0.4f, 0.6f })
                {
                    foreach (ParticleSystem system in systems)
                    {
                        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        system.Simulate(elapsed, true, true, true);
                        system.Pause(true);
                    }

                    int particleCount = systems.Sum(system => system.particleCount);
                    countsByTime[elapsed] = particleCount;
                    maximumParticleCount = Mathf.Max(maximumParticleCount, particleCount);
                    foreach (ParticleSystem system in systems)
                    {
                        var particles = new ParticleSystem.Particle[system.particleCount];
                        int count = system.GetParticles(particles);
                        for (int index = 0; index < count; index++)
                        {
                            maximumParticleAlpha = Math.Max(
                                maximumParticleAlpha,
                                particles[index].GetCurrentColor(system).a);
                            Vector3 size = particles[index].GetCurrentSize3D(system);
                            maximumBillboardArea = Mathf.Max(maximumBillboardArea, size.x * size.y);
                        }
                    }
                    foreach (ParticleSystemRenderer renderer in
                             instance.GetComponentsInChildren<ParticleSystemRenderer>(true))
                    {
                        if (!renderer.enabled || renderer.bounds.size.sqrMagnitude < 0.0001f)
                            continue;
                        if (hasBounds)
                            combinedBounds.Encapsulate(renderer.bounds);
                        else
                        {
                            combinedBounds = renderer.bounds;
                            hasBounds = true;
                        }
                    }
                }

                Assert.That(maximumParticleCount, Is.GreaterThan(0),
                    "Curse preview lifetime never emits a particle.");
                Assert.That(maximumParticleAlpha, Is.GreaterThan(0),
                    "Curse particles exist but their evaluated vertex alpha is always zero.");
                Assert.That(maximumBillboardArea, Is.GreaterThan(0.0001f),
                    "Curse particles exist but their evaluated Billboard X/Y area is zero.");
                Assert.That(countsByTime[0.25f], Is.GreaterThan(0),
                    $"Curse is already empty at its visible quarter-second sample: " +
                    string.Join(", ", countsByTime.Select(pair => $"{pair.Key:0.00}s={pair.Value}")));
                Assert.That(countsByTime[0.4f], Is.GreaterThan(0),
                    $"Curse is already empty before the preview timeline midpoint: " +
                    string.Join(", ", countsByTime.Select(pair => $"{pair.Key:0.00}s={pair.Value}")));
                Assert.That(hasBounds, Is.True, "Curse particles never produce renderer bounds.");
                Assert.That(Vector3.Distance(combinedBounds.center, instance.transform.position),
                    Is.LessThan(8f),
                    $"Curse renderer bounds are outside the preview stage: {combinedBounds}.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [TestCase("LightningLv1")]
        [TestCase("LightningLv2")]
        [TestCase("LightningLv3")]
        public void LightningProfiles_AnchorToPrimaryTargetGround(string profileName)
        {
            string path = $"{AdaptedRoot}/Profiles/{profileName}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VisualCueProfile>(path);

            Assert.That(profile, Is.Not.Null, path);
            Assert.That(profile.Anchor, Is.EqualTo(VisualCueAnchor.PrimaryTargetGround), path);
            Assert.That(profile.Prefab, Is.Not.Null, path);
            Assert.That(AssetDatabase.GetAssetPath(profile.Prefab),
                Is.EqualTo($"{AdaptedRoot}/Prefabs/LightningImpact.prefab"), path);
        }

        private static float SampleParticleMinimumY(GameObject instance)
        {
            const float sampleDuration = 0.65f;
            const float sampleStep = 0.05f;
            float minimumY = float.PositiveInfinity;
            foreach (ParticleSystem system in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                system.useAutoRandomSeed = false;
                system.randomSeed = 1u;
                for (float time = sampleStep; time <= sampleDuration + 0.0001f; time += sampleStep)
                {
                    system.Simulate(time, true, true, true);
                    var renderer = system.GetComponent<ParticleSystemRenderer>();
                    if (renderer != null && renderer.bounds.size.sqrMagnitude > 0.0001f)
                        minimumY = Mathf.Min(minimumY, renderer.bounds.min.y);
                }
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            Assert.That(float.IsPositiveInfinity(minimumY), Is.False);
            return minimumY;
        }

        private static Bounds SampleParticleBounds(GameObject instance)
        {
            bool hasBounds = false;
            Bounds combined = default;
            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (float time in new[] { 0.15f, 0.3f, 0.5f, 0.72f })
            {
                foreach (ParticleSystem system in systems)
                {
                    system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    system.useAutoRandomSeed = false;
                    system.randomSeed = 1u;
                    system.Simulate(time, false, true, true);
                    var renderer = system.GetComponent<ParticleSystemRenderer>();
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
            Assert.That(hasBounds, Is.True, instance.name);
            return combined;
        }


        [TestCase("Lightning_Graph", "LightningLv1")]
        [TestCase("Lightning_Lv2_Graph", "LightningLv2")]
        [TestCase("Lightning_Lv3_Graph", "LightningLv3")]
        [TestCase("Curse_Graph", "AmplifyDamageSigilGroundV2Lv1|AmplifyDamageSigilRearFlamesV2Lv1|AmplifyDamageSigilForegroundFlamesV2Lv1")]
        [TestCase("Curse_Lv2_Graph", "AmplifyDamageSigilGroundV2Lv2|AmplifyDamageSigilRearFlamesV2Lv2|AmplifyDamageSigilForegroundFlamesV2Lv2")]
        [TestCase("Curse_Lv3_Graph", "AmplifyDamageSigilGroundV2Lv3|AmplifyDamageSigilRearFlamesV2Lv3|AmplifyDamageSigilForegroundFlamesV2Lv3")]
        public void DirectSampleGraphs_ReferenceLevelSpecificPresentationProfiles(
            string graphName,
            string expectedProfileName)
        {
            var graph = AssetDatabase.LoadAssetAtPath<SkillGraphAsset>(
                $"Assets/Tactics/Battle/Abilities/SkillGraphs/{graphName}.asset");
            Assert.That(graph, Is.Not.Null, graphName);
            var cue = graph.Nodes.SingleOrDefault(node => node is PlayPresentationCueNodeRecord);
            Assert.That(cue, Is.Not.Null, graphName);
            string abilityName = graphName.EndsWith("_Graph", System.StringComparison.Ordinal)
                ? graphName[..^6]
                : graphName;
            var presentation = AssetDatabase.LoadAssetAtPath<BattlePresentationGraph>(
                $"Assets/Tactics/Arts/PureRun/Presentation/{abilityName}_Presentation.asset");
            Assert.That(presentation, Is.Not.Null, graphName);
            string[] expectedProfiles = expectedProfileName.Split('|');
            string[] profileNames = presentation.Nodes.OfType<PresentationPrefabFxNodeRecord>()
                .Select(node => node.Profile?.name)
                .Where(name => !string.IsNullOrEmpty(name))
                .ToArray();
            Assert.That(profileNames, Is.EquivalentTo(expectedProfiles), graphName);
        }

        [TestCase("LightningLv1", "LightningImpact")]
        [TestCase("LightningLv2", "LightningImpact")]
        [TestCase("LightningLv3", "LightningImpact")]
        [TestCase("AmplifyDamageLv1", "AmplifyDamageCurse")]
        [TestCase("AmplifyDamageLv2", "AmplifyDamageCurse")]
        [TestCase("AmplifyDamageLv3", "AmplifyDamageCurse")]
        public void VisualCueProfiles_ReferenceExpectedAdaptedPrefabs(
            string profileName,
            string expectedPrefabName)
        {
            string profilePath = $"{AdaptedRoot}/Profiles/{profileName}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VisualCueProfile>(profilePath);
            Assert.That(profile, Is.Not.Null, profilePath);
            Assert.That(profile.Prefab, Is.Not.Null, profilePath);
            Assert.That(AssetDatabase.GetAssetPath(profile.Prefab),
                Is.EqualTo($"{AdaptedRoot}/Prefabs/{expectedPrefabName}.prefab"),
                profilePath);
        }

        [Test]
        public void MageVisualSampleRebuild_PreservesCompleteGraphState()
        {
            string[] graphNames = { "Lightning_Graph", "Lightning_Lv2_Graph", "Lightning_Lv3_Graph" };
            AssertVisualSampleRebuildPreservesGraphs(
                graphNames,
                MageSliceAssetBuilder.RebuildLightningVisualSample);
        }

        [Test]
        public void NecromancerVisualSampleRebuild_PreservesCompleteGraphState()
        {
            string[] graphNames = { "Curse_Graph", "Curse_Lv2_Graph", "Curse_Lv3_Graph" };
            AssertVisualSampleRebuildPreservesGraphs(
                graphNames,
                NecromancerSliceAssetBuilder.RebuildAmplifyDamageVisualSample);
        }

        [Test]
        public void VisualSampleRebuild_DoesNotSaveDirtyNeighborAsset()
        {
            const string neighborPath =
                "Assets/Tactics/Tests/Editor/__PilotoDirtyNeighbor.asset";
            AssetDatabase.DeleteAsset(neighborPath);
            var neighbor = ScriptableObject.CreateInstance<VisualCueProfile>();
            AssetDatabase.CreateAsset(neighbor, neighborPath);
            AssetDatabase.SaveAssetIfDirty(neighbor);

            try
            {
                neighbor.name = "Unsaved dirty neighbor";
                EditorUtility.SetDirty(neighbor);
                Assert.That(EditorUtility.IsDirty(neighbor), Is.True);

                AssertVisualSampleRebuildPreservesGraphs(
                    new[] { "Lightning_Graph", "Lightning_Lv2_Graph", "Lightning_Lv3_Graph" },
                    MageSliceAssetBuilder.RebuildLightningVisualSample);

                Assert.That(EditorUtility.IsDirty(neighbor), Is.True,
                    "The graph restore helper must not save an unrelated dirty Editor asset.");
            }
            finally
            {
                AssetDatabase.DeleteAsset(neighborPath);
            }
        }

        private static void AssertVisualSampleRebuildPreservesGraphs(
            IEnumerable<string> graphNames,
            Action rebuild)
        {
            var originalJson = graphNames.ToDictionary(
                graphName => graphName,
                graphName => EditorJsonUtility.ToJson(LoadGraph(graphName), true),
                StringComparer.Ordinal);

            try
            {
                rebuild();

                foreach (var pair in originalJson)
                {
                    Assert.That(
                        EditorJsonUtility.ToJson(LoadGraph(pair.Key), true),
                        Is.EqualTo(pair.Value),
                        $"Visual-only rebuild mutated non-VFX graph state in '{pair.Key}'.");
                }
            }
            finally
            {
                foreach (var pair in originalJson)
                {
                    var graph = LoadGraph(pair.Key);
                    if (EditorJsonUtility.ToJson(graph, true) == pair.Value)
                        continue;
                    EditorJsonUtility.FromJsonOverwrite(pair.Value, graph);
                    EditorUtility.SetDirty(graph);
                    AssetDatabase.SaveAssetIfDirty(graph);
                }
            }
        }

        private static SkillGraphAsset LoadGraph(string graphName)
        {
            string path = $"Assets/Tactics/Battle/Abilities/SkillGraphs/{graphName}.asset";
            var graph = AssetDatabase.LoadAssetAtPath<SkillGraphAsset>(path);
            Assert.That(graph, Is.Not.Null, path);
            return graph;
        }

        [Serializable]
        private sealed class AssemblyDefinitionData
        {
            [SerializeField] private string name;
            [SerializeField] private string[] includePlatforms;
            [SerializeField] private bool autoReferenced;

            public string Name => name;
            public string[] IncludePlatforms => includePlatforms;
            public bool AutoReferenced => autoReferenced;
        }
    }
}
