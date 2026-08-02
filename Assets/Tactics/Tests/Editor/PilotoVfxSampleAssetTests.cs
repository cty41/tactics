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
                Assert.That(renderer.renderMode, Is.EqualTo(ParticleSystemRenderMode.Billboard), path);
                Assert.That(renderer.alignment, Is.EqualTo(ParticleSystemRenderSpace.View), path);
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
                }
            }
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

        [TestCase("Lightning_Graph", "LightningLv1")]
        [TestCase("Lightning_Lv2_Graph", "LightningLv2")]
        [TestCase("Lightning_Lv3_Graph", "LightningLv3")]
        [TestCase("Curse_Graph", "AmplifyDamageLv1")]
        [TestCase("Curse_Lv2_Graph", "AmplifyDamageLv2")]
        [TestCase("Curse_Lv3_Graph", "AmplifyDamageLv3")]
        public void DirectSampleGraphs_ReferenceLevelSpecificVisualCueProfiles(
            string graphName,
            string expectedProfileName)
        {
            var graph = AssetDatabase.LoadAssetAtPath<SkillGraphAsset>(
                $"Assets/Tactics/Battle/Abilities/SkillGraphs/{graphName}.asset");
            Assert.That(graph, Is.Not.Null, graphName);
            var cue = graph.Nodes.SingleOrDefault(node => node.NodeType.ToString() == "PlayVisualCue");
            Assert.That(cue, Is.Not.Null, graphName);
            var profileProperty = cue.GetType().GetProperty("Profile");
            Assert.That(profileProperty, Is.Not.Null, graphName);
            var profile = profileProperty.GetValue(cue) as UnityEngine.Object;
            Assert.That(profile, Is.Not.Null, graphName);
            Assert.That(profile.name, Is.EqualTo(expectedProfileName), graphName);
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
