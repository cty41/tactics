#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Units.Tween;
using Tactics.EditorTools;
using UnityEditor;
using UnityEngine;

namespace Tactics.Tests.Editor
{
    public sealed class PresentationAuthoringFacadeEditorTests
    {
        private const string TempFolder = "Assets/Tactics/Tests/TempPresentationAuthoring";
        private readonly List<string> _createdPaths = new();

        [SetUp]
        public void SetUp()
        {
            PresentationAuthoringFacade.CommitFaultInjector = null;
            EnsureTempFolder();
        }

        [TearDown]
        public void TearDown()
        {
            PresentationAuthoringFacade.CommitFaultInjector = null;
            foreach (string path in _createdPaths)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.DeleteAsset(TempFolder);
            AssetDatabase.SaveAssets();
        }

        [Test]
        public void ValidateChangeSet_NoOperationsDoesNotChangeRevisionOrAssetDirtyState()
        {
            string path = CreateGraphWithProjectile(out _);
            JObject before = PresentationAuthoringFacade.GetGraph(path);
            var changeSet = new JObject
            {
                ["graphPath"] = path,
                ["expectedRevision"] = before.Value<string>("revision"),
                ["operations"] = new JArray()
            };

            JObject result = PresentationAuthoringFacade.ValidateChangeSet(changeSet);
            JObject after = PresentationAuthoringFacade.GetGraph(path);

            Assert.That(result.Value<bool>("changed"), Is.False);
            Assert.That(after.Value<string>("revision"), Is.EqualTo(before.Value<string>("revision")));
            Assert.That(EditorUtility.IsDirty(AssetDatabase.LoadMainAssetAtPath(path)), Is.False);
        }

        [Test]
        public void GetGraph_ReturnsStableIdentityRevisionAndDependencies()
        {
            string path = CreateGraphWithProjectile(out ProjectileVisualProfile profile);
            string guid = AssetDatabase.AssetPathToGUID(path);

            JObject snapshot = PresentationAuthoringFacade.GetGraph(path);

            Assert.That(snapshot.Value<string>("guid"), Is.EqualTo(guid));
            Assert.That(snapshot.Value<string>("path"), Is.EqualTo(path));
            Assert.That(snapshot.Value<string>("revision"), Has.Length.EqualTo(64));
            Assert.That(snapshot["nodes"], Is.Not.Null);
            Assert.That(snapshot["edges"], Is.Not.Null);
            Assert.That(snapshot["dependencies"], Is.Not.Null);
            JToken leaf = snapshot["leafAssets"]?.First;
            Assert.That(leaf, Is.Not.Null);
            Assert.That(leaf.Value<string>("path"), Is.EqualTo(AssetDatabase.GetAssetPath(profile)));
            Assert.That(leaf.Value<string>("revision"), Has.Length.EqualTo(64));
            JToken projectileNode = ((JArray)snapshot["nodes"])
                .First(value => value.Value<string>("nodeType") == "Projectile");
            Assert.That(projectileNode["profile"]?["revision"]?.Value<string>(), Has.Length.EqualTo(64));
        }

        [Test]
        public void GetGraph_AllFormalGraphsRemainUnchangedAndClean()
        {
            string[] graphPaths = AssetDatabase.FindAssets(
                    "t:BattlePresentationGraph",
                    new[] { "Assets/Tactics/Arts/PureRun/Presentation" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(value => value)
                .ToArray();
            Assert.That(graphPaths, Has.Length.EqualTo(18));

            foreach (string graphPath in graphPaths)
            {
                BattlePresentationGraph graph = AssetDatabase.LoadAssetAtPath<BattlePresentationGraph>(graphPath);
                JObject before = PresentationAuthoringFacade.GetGraph(graphPath);
                JObject after = PresentationAuthoringFacade.GetGraph(graphPath);
                Assert.That(JToken.DeepEquals(after, before), Is.True, graphPath);
                Assert.That(EditorUtility.IsDirty(graph), Is.False, graphPath);
            }
        }

        [Test]
        public void GetGraph_UnitTweenNodeIncludesPreviewActorProfileRevision()
        {
            BattlePresentationGraph graph = FindFormalGraph(value => value.Nodes.Any(node =>
                node is PresentationUnitTweenNodeRecord));
            StandardUnitTweenProfile profile = graph.PreviewActorPrefab
                .GetComponent<UnitTweenVisual>()
                .Profile;
            Assert.That(profile, Is.Not.Null);

            JObject snapshot = PresentationAuthoringFacade.GetGraph(AssetDatabase.GetAssetPath(graph));
            JToken leaf = ((JArray)snapshot["leafAssets"]).First(value =>
                value.Value<string>("type") == nameof(StandardUnitTweenProfile));
            JToken node = ((JArray)snapshot["nodes"]).First(value =>
                value.Value<string>("nodeType") == "UnitTween");

            Assert.That(node["profile"]?["revision"]?.Value<string>(), Is.EqualTo(
                leaf.Value<string>("revision")));
        }

        [Test]
        public void ApplyChangeSet_CreatesGraphAndUndoRemovesIt()
        {
            string path = TrackPath($"{TempFolder}/CreatedGraph.asset");
            JObject changeSet = CreateGraphChangeSet(path);
            JObject validation = PresentationAuthoringFacade.ValidateChangeSet(changeSet);
            JObject result = PresentationAuthoringFacade.ApplyChangeSet(changeSet);

            Assert.That(AssetDatabase.LoadAssetAtPath<BattlePresentationGraph>(path), Is.Not.Null);
            Assert.That(result.Value<string>("revision"), Has.Length.EqualTo(64));
            Assert.That(result.Value<string>("revision"), Is.EqualTo(validation.Value<string>("predictedRevision")));

            Undo.PerformUndo();
            Assert.That(AssetDatabase.LoadAssetAtPath<BattlePresentationGraph>(path), Is.Null);
        }

        [Test]
        public void ApplyChangeSets_InjectedCommitFailureLeavesNoPartialAssets()
        {
            string firstPath = TrackPath($"{TempFolder}/First.asset");
            string secondPath = TrackPath($"{TempFolder}/Second.asset");
            PresentationAuthoringFacade.CommitFaultInjector = step =>
            {
                if (step == $"graph:{firstPath}")
                    throw new InvalidOperationException("Injected commit failure.");
            };

            Assert.Throws<InvalidOperationException>(() => PresentationAuthoringFacade.ApplyChangeSets(
                new JArray(CreateGraphChangeSet(firstPath), CreateGraphChangeSet(secondPath))));

            Assert.That(AssetDatabase.LoadMainAssetAtPath(firstPath), Is.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(secondPath), Is.Null);
        }

        [Test]
        public void ApplyChangeSet_InjectedFailureRestoresContentAndInitialDirtyState()
        {
            string graphPath = CreateGraphWithProjectile(out ProjectileVisualProfile profile);
            JObject before = PresentationAuthoringFacade.GetGraph(graphPath);
            string leafRevision = before["leafAssets"]?[0]?["revision"]?.Value<string>();
            EditorUtility.SetDirty(profile);
            PresentationAuthoringFacade.CommitFaultInjector = step =>
            {
                if (step == $"graph:{graphPath}")
                    throw new InvalidOperationException("Injected graph failure.");
            };
            var changeSet = new JObject
            {
                ["graphPath"] = graphPath,
                ["expectedRevision"] = before.Value<string>("revision"),
                ["operations"] = new JArray(),
                ["assetChanges"] = new JArray(new JObject
                {
                    ["kind"] = "updateLeafAsset",
                    ["assetPath"] = AssetDatabase.GetAssetPath(profile),
                    ["expectedRevision"] = leafRevision,
                    ["fields"] = new JObject { ["impactLifetime"] = 0.73f }
                })
            };

            Assert.Throws<InvalidOperationException>(() => PresentationAuthoringFacade.ApplyChangeSet(changeSet));

            JObject after = PresentationAuthoringFacade.GetGraph(graphPath);
            Assert.That(after.Value<string>("revision"), Is.EqualTo(before.Value<string>("revision")));
            Assert.That(after["leafAssets"]?[0]?["revision"]?.Value<string>(), Is.EqualTo(leafRevision));
            Assert.That(EditorUtility.IsDirty(profile), Is.True);
            Assert.That(EditorUtility.IsDirty(AssetDatabase.LoadMainAssetAtPath(graphPath)), Is.False);
        }

        [Test]
        public void ApplyChangeSets_NewLeafCanBeBoundByMultipleGraphsAtomically()
        {
            string firstPath = TrackPath($"{TempFolder}/SharedFirst.asset");
            string secondPath = TrackPath($"{TempFolder}/SharedSecond.asset");
            string leafPath = TrackPath($"{TempFolder}/SharedVisualCue.asset");
            JObject first = CreateSharedLeafGraphChangeSet(firstPath, leafPath);
            first["assetChanges"] = new JArray(new JObject
            {
                ["kind"] = "createLeafAsset",
                ["assetType"] = "VisualCueProfile",
                ["assetPath"] = leafPath
            });
            JObject second = CreateSharedLeafGraphChangeSet(secondPath, leafPath);

            var batch = new JArray(first, second);
            JArray validation = PresentationAuthoringFacade.ValidateChangeSets(batch);
            JArray results = PresentationAuthoringFacade.ApplyChangeSets(batch);

            Assert.That(validation, Has.Count.EqualTo(2));
            Assert.That(results, Has.Count.EqualTo(2));
            VisualCueProfile leaf = AssetDatabase.LoadAssetAtPath<VisualCueProfile>(leafPath);
            Assert.That(leaf, Is.Not.Null);
            foreach (string graphPath in new[] { firstPath, secondPath })
            {
                BattlePresentationGraph graph = AssetDatabase.LoadAssetAtPath<BattlePresentationGraph>(graphPath);
                var node = graph.FindNode("fx") as PresentationPrefabFxNodeRecord;
                Assert.That(node?.Profile, Is.SameAs(leaf));
            }
        }

        [Test]
        public void ApplyChangeSet_CreatesTypedRecipeAndReturnsUsableRevision()
        {
            string graphPath = TrackPath($"{TempFolder}/RecipeGraph.asset");
            string recipePath = TrackPath($"{TempFolder}/Recipe.asset");
            JObject changeSet = CreateGraphChangeSet(graphPath);
            ((JObject)changeSet["createGraph"])["defaultPreviewEntry"] = "PrimaryTargetHit";
            ((JObject)changeSet["createGraph"])["phases"] = new JArray(CreatePreviewPhase("PrimaryTargetHit"));
            changeSet["operations"] = new JArray(
                new JObject { ["kind"] = "addNode", ["nodeType"] = "Entry", ["nodeId"] = "entry", ["cue"] = "PrimaryTargetHit" },
                new JObject { ["kind"] = "addNode", ["nodeType"] = "ProceduralVfx", ["nodeId"] = "vfx", ["cue"] = "PrimaryTargetHit" },
                new JObject { ["kind"] = "addNode", ["nodeType"] = "Finish", ["nodeId"] = "finish" },
                new JObject { ["kind"] = "addEdge", ["sourceNodeId"] = "entry", ["targetNodeId"] = "vfx" },
                new JObject { ["kind"] = "addEdge", ["sourceNodeId"] = "vfx", ["targetNodeId"] = "finish" },
                new JObject { ["kind"] = "bindNodeAsset", ["nodeId"] = "vfx", ["assetPath"] = recipePath });
            changeSet["assetChanges"] = new JArray(new JObject
            {
                ["kind"] = "createLeafAsset",
                ["assetPath"] = recipePath,
                ["assetType"] = "SkillVfxRecipe",
                ["replaceRecipeBindings"] = new JArray(new JObject
                {
                    ["cue"] = "PrimaryTargetHit",
                    ["layers"] = new JArray(new JObject
                    {
                        ["primitiveKind"] = "CrossFlash",
                        ["duration"] = 0.2f,
                        ["peakTime"] = 0.08f,
                        ["middleTime"] = 0.04f,
                        ["blockingMarker"] = 0.08f,
                        ["startSize"] = 0.05f,
                        ["peakSize"] = 0.2f,
                        ["endSize"] = 0.3f
                    })
                })
            });

            PresentationAuthoringFacade.ApplyChangeSet(changeSet);
            SkillVfxRecipe recipe = AssetDatabase.LoadAssetAtPath<SkillVfxRecipe>(recipePath);
            JObject snapshot = PresentationAuthoringFacade.GetGraph(graphPath);

            Assert.That(recipe, Is.Not.Null);
            Assert.That(recipe.GetLayers(SkillVfxCueKind.PrimaryTargetHit), Has.Count.EqualTo(1));
            Assert.That(snapshot["leafAssets"]?[0]?["revision"]?.Value<string>(), Has.Length.EqualTo(64));
        }

        [Test]
        public void ValidateChangeSet_PredictedLeafRevisionMatchesPersistedRevision()
        {
            string graphPath = TrackPath($"{TempFolder}/PredictedRevisionGraph.asset");
            string recipePath = TrackPath($"{TempFolder}/PredictedRevisionRecipe.asset");
            JObject changeSet = CreateGraphChangeSet(graphPath);
            changeSet["assetChanges"] = new JArray(new JObject
            {
                ["kind"] = "createLeafAsset",
                ["assetType"] = "SkillVfxRecipe",
                ["assetPath"] = recipePath,
                ["replaceRecipeBindings"] = new JArray()
            });

            JObject validation = PresentationAuthoringFacade.ValidateChangeSet(changeSet);
            string predicted = validation["assetChanges"]?[0]?["revision"]?.Value<string>();
            JObject applied = PresentationAuthoringFacade.ApplyChangeSet(changeSet);
            string persisted = applied["assetChanges"]?[0]?["revision"]?.Value<string>();

            Assert.That(predicted, Has.Length.EqualTo(64));
            Assert.That(persisted, Is.EqualTo(predicted));
        }

        [Test]
        public void ValidateChangeSet_InvalidRecipeSemanticsFailBeforeAssetCreation()
        {
            string graphPath = TrackPath($"{TempFolder}/InvalidRecipeGraph.asset");
            string recipePath = TrackPath($"{TempFolder}/InvalidRecipe.asset");
            JObject changeSet = CreateGraphChangeSet(graphPath);
            changeSet["assetChanges"] = new JArray(new JObject
            {
                ["kind"] = "createLeafAsset",
                ["assetType"] = "SkillVfxRecipe",
                ["assetPath"] = recipePath,
                ["replaceRecipeBindings"] = new JArray(new JObject
                {
                    ["cue"] = "PrimaryTargetHit",
                    ["layers"] = new JArray(new JObject
                    {
                        ["primitiveKind"] = "ParticleBurst",
                        ["duration"] = 0.2f,
                        ["blockingMarker"] = 0.1f
                    })
                })
            });

            Assert.Throws<ArgumentException>(() => PresentationAuthoringFacade.ValidateChangeSet(changeSet));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(recipePath), Is.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(graphPath), Is.Null);
        }

        [Test]
        public void ApplyChangeSet_RevisionConflictFailsBeforeWriting()
        {
            string path = CreateGraphWithProjectile(out _);
            BattlePresentationGraph graph = AssetDatabase.LoadAssetAtPath<BattlePresentationGraph>(path);
            string originalName = graph.DisplayName;
            var changeSet = new JObject
            {
                ["graphPath"] = path,
                ["expectedRevision"] = new string('0', 64),
                ["operations"] = new JArray(new JObject
                {
                    ["kind"] = "setGraph",
                    ["displayName"] = "Must Not Apply"
                })
            };

            Assert.Throws<InvalidOperationException>(() => PresentationAuthoringFacade.ApplyChangeSet(changeSet));
            Assert.That(graph.DisplayName, Is.EqualTo(originalName));
        }

        [Test]
        public void ApplyChangeSet_LeafEditDoesNotChangeIndependentGraphRevision()
        {
            string path = CreateGraphWithProjectile(out ProjectileVisualProfile profile);
            JObject before = PresentationAuthoringFacade.GetGraph(path);
            string leafRevision = before["leafAssets"]?[0]?["revision"]?.Value<string>();
            var changeSet = new JObject
            {
                ["graphPath"] = path,
                ["expectedRevision"] = before.Value<string>("revision"),
                ["operations"] = new JArray(),
                ["assetChanges"] = new JArray(new JObject
                {
                    ["kind"] = "updateLeafAsset",
                    ["assetPath"] = AssetDatabase.GetAssetPath(profile),
                    ["expectedRevision"] = leafRevision,
                    ["fields"] = new JObject { ["impactLifetime"] = 0.42f }
                })
            };

            PresentationAuthoringFacade.ApplyChangeSet(changeSet);
            JObject after = PresentationAuthoringFacade.GetGraph(path);

            Assert.That(after.Value<string>("revision"), Is.EqualTo(before.Value<string>("revision")));
            Assert.That(after["leafAssets"]?[0]?["revision"]?.Value<string>(), Is.Not.EqualTo(leafRevision));
        }

        [Test]
        public void Preview_ReturnsPreviewRenderUtilityPngAndStructuredTimeline()
        {
            BattlePresentationGraph graph = FindFormalGraph(value => value.FindEntry(PresentationCueKind.Action) != null);
            string path = AssetDatabase.GetAssetPath(graph);

            JObject preview = PresentationAuthoringFacade.Preview(
                path,
                PresentationCueKind.Action,
                320,
                180);

            Assert.That(preview.Value<string>("renderKind"), Is.EqualTo("preview-render-utility"));
            Assert.That(preview.Value<string>("imageBase64"), Is.Not.Empty);
            Assert.That(preview.Value<int>("width"), Is.EqualTo(320));
            Assert.That(preview.Value<int>("height"), Is.EqualTo(180));
            Assert.That(preview["timeline"], Is.Not.Null);
        }

        [Test]
        public void Preview_EntryScopeRendersRequestedCueAndActualNodeTimes()
        {
            BattlePresentationGraph graph = FindFormalGraph(value => value.FindEntry(PresentationCueKind.Action) != null);
            string path = AssetDatabase.GetAssetPath(graph);
            JObject preview = PresentationAuthoringFacade.Preview(path, new JObject
            {
                ["scope"] = new JObject { ["kind"] = "Entry", ["cue"] = "Action" },
                ["width"] = 320,
                ["height"] = 180,
                ["randomSeed"] = 42
            });

            Assert.That(preview["requestedScope"]?["kind"]?.Value<string>(), Is.EqualTo("entry"));
            Assert.That(preview["resolvedScope"]?["cue"]?.Value<string>(), Is.EqualTo("Action"));
            JArray timeline = (JArray)preview["timeline"];
            Assert.That(timeline.Any(value => value.Value<string>("event") == "NodeStart"), Is.True);
            Assert.That(timeline.Any(value => value.Value<string>("event") == "NodeEnd"), Is.True);
            Assert.That(timeline.All(value => value.Value<float>("time") >= 0f), Is.True);
        }

        [Test]
        public void Preview_FullScenarioReturnsActualPhaseAdvanceAndDeterministicTimeline()
        {
            BattlePresentationGraph graph = FindFormalGraph(value => value.HasPreviewScenario);
            string path = AssetDatabase.GetAssetPath(graph);
            var request = new JObject
            {
                ["scope"] = new JObject { ["kind"] = "FullScenario" },
                ["width"] = 320,
                ["height"] = 180,
                ["randomSeed"] = 99
            };

            JObject first = PresentationAuthoringFacade.Preview(path, request);
            JObject second = PresentationAuthoringFacade.Preview(path, request);
            JObject phase = PresentationAuthoringFacade.Preview(path, new JObject
            {
                ["scope"] = new JObject { ["kind"] = "Phase", ["phaseIndex"] = 0 },
                ["width"] = 320,
                ["height"] = 180,
                ["randomSeed"] = 99
            });

            Assert.That(((JArray)first["timeline"]).Any(value =>
                value.Value<string>("event") == "PhaseAdvance"), Is.True);
            Assert.That(JToken.DeepEquals(first["timeline"], second["timeline"]), Is.True);
            Assert.That(first.Value<int>("randomSeed"), Is.EqualTo(99));
            Assert.That(((JArray)phase["timeline"]).Where(value =>
                value.Value<int>("phaseIndex") >= 0).All(value => value.Value<int>("phaseIndex") == 0), Is.True);
        }

        [Test]
        public void Preview_LeafAndForkScopesStayInsideRequestedContext()
        {
            BattlePresentationGraph leafGraph = FindFormalGraph(value => value.Nodes.Any(IsExecutableLeaf));
            PresentationNodeRecord leaf = leafGraph.Nodes.First(node =>
                IsExecutableLeaf(node));
            JObject leafPreview = PresentationAuthoringFacade.Preview(
                AssetDatabase.GetAssetPath(leafGraph),
                new JObject
                {
                    ["scope"] = new JObject { ["kind"] = "Leaf", ["nodeId"] = leaf.NodeId },
                    ["width"] = 320,
                    ["height"] = 180
                });
            string[] leafNodeIds = ((JArray)leafPreview["timeline"])
                .Where(value => value.Value<string>("event") == "NodeStart")
                .Select(value => value.Value<string>("nodeId"))
                .Distinct()
                .ToArray();
            Assert.That(leafNodeIds, Is.EqualTo(new[] { leaf.NodeId }));

            BattlePresentationGraph forkGraph = FindFormalGraph(value => value.Nodes.Any(node =>
                node.Enabled && node is PresentationForkNodeRecord));
            var fork = (PresentationForkNodeRecord)forkGraph.Nodes.First(node =>
                node.Enabled && node is PresentationForkNodeRecord);
            JObject forkPreview = PresentationAuthoringFacade.Preview(
                AssetDatabase.GetAssetPath(forkGraph),
                new JObject
                {
                    ["scope"] = "forkRegion",
                    ["forkNodeId"] = fork.NodeId,
                    ["width"] = 320,
                    ["height"] = 180
                });
            Assert.That(forkPreview["requestedScope"]?["kind"]?.Value<string>(), Is.EqualTo("forkRegion"));
            JArray forkTimeline = (JArray)forkPreview["timeline"];
            Assert.That(forkTimeline.Any(value => value.Value<string>("nodeId") == fork.NodeId), Is.True);
            Assert.That(forkTimeline.Any(value => value.Value<int>("lane") > 0), Is.True);
        }

        private JObject CreateGraphChangeSet(string path)
        {
            return new JObject
            {
                ["graphPath"] = path,
                ["createGraph"] = new JObject
                {
                    ["displayName"] = "Created Graph",
                    ["version"] = 1,
                    ["defaultPreviewEntry"] = "Idle",
                    ["phases"] = new JArray(CreatePreviewPhase("Idle"))
                },
                ["operations"] = new JArray(
                    new JObject { ["kind"] = "addNode", ["nodeType"] = "Entry", ["nodeId"] = "entry", ["cue"] = "Idle" },
                    new JObject { ["kind"] = "addNode", ["nodeType"] = "Finish", ["nodeId"] = "finish" },
                    new JObject { ["kind"] = "addEdge", ["sourceNodeId"] = "entry", ["targetNodeId"] = "finish" }),
                ["assetChanges"] = new JArray()
            };
        }

        private JObject CreateSharedLeafGraphChangeSet(string graphPath, string leafPath)
        {
            JObject changeSet = CreateGraphChangeSet(graphPath);
            changeSet["operations"] = new JArray(
                new JObject { ["kind"] = "addNode", ["nodeType"] = "Entry", ["nodeId"] = "entry", ["cue"] = "Idle" },
                new JObject { ["kind"] = "addNode", ["nodeType"] = "PrefabFx", ["nodeId"] = "fx" },
                new JObject { ["kind"] = "addNode", ["nodeType"] = "Finish", ["nodeId"] = "finish" },
                new JObject { ["kind"] = "addEdge", ["sourceNodeId"] = "entry", ["targetNodeId"] = "fx" },
                new JObject { ["kind"] = "addEdge", ["sourceNodeId"] = "fx", ["targetNodeId"] = "finish" },
                new JObject { ["kind"] = "bindNodeAsset", ["nodeId"] = "fx", ["assetPath"] = leafPath });
            return changeSet;
        }

        private string CreateGraphWithProjectile(out ProjectileVisualProfile profile)
        {
            string profilePath = TrackPath($"{TempFolder}/{Guid.NewGuid():N}Projectile.asset");
            profile = ScriptableObject.CreateInstance<ProjectileVisualProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);

            string graphPath = TrackPath($"{TempFolder}/{Guid.NewGuid():N}Graph.asset");
            var graph = ScriptableObject.CreateInstance<BattlePresentationGraph>();
            graph.DisplayName = "Revision Test Graph";
            graph.DefaultPreviewEntry = PresentationCueKind.Projectile;
            graph.PreviewPhases.Add(new PresentationPreviewPhaseRecord
            {
                ContinuationCue = PresentationCueKind.Projectile,
                AdvanceKind = PresentationPreviewAdvanceKind.Complete
            });
            graph.PreviewPhases[0].Cues.Add(PresentationCueKind.Projectile);
            PresentationNodeRecord entry = graph.AddNode(PresentationNodeType.Entry, Vector2.zero);
            ((PresentationEntryNodeRecord)entry).Cue = PresentationCueKind.Projectile;
            var projectile = (PresentationProjectileNodeRecord)graph.AddNode(PresentationNodeType.Projectile, Vector2.right);
            projectile.Profile = profile;
            PresentationNodeRecord finish = graph.AddNode(PresentationNodeType.Finish, Vector2.right * 2f);
            graph.AddEdge(entry.NodeId, projectile.NodeId);
            graph.AddEdge(projectile.NodeId, finish.NodeId);
            AssetDatabase.CreateAsset(graph, graphPath);
            AssetDatabase.SaveAssets();
            return graphPath;
        }

        private string TrackPath(string path)
        {
            _createdPaths.Add(path);
            return path;
        }

        private static void EnsureTempFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Tactics/Tests"))
                AssetDatabase.CreateFolder("Assets/Tactics", "Tests");
            if (!AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.CreateFolder("Assets/Tactics/Tests", "TempPresentationAuthoring");
        }

        private static JObject CreatePreviewPhase(string cue)
        {
            return new JObject
            {
                ["cues"] = new JArray(cue),
                ["continuationCue"] = cue,
                ["advanceKind"] = "Complete",
                ["playTargetHitReaction"] = false
            };
        }

        private static BattlePresentationGraph FindFormalGraph(Func<BattlePresentationGraph, bool> predicate)
        {
            return AssetDatabase.FindAssets("t:BattlePresentationGraph")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<BattlePresentationGraph>)
                .First(value => value != null && value.PreviewActorPrefab != null && predicate(value));
        }

        private static bool IsExecutableLeaf(PresentationNodeRecord node)
        {
            return node != null && node.Enabled &&
                   node is not PresentationEntryNodeRecord and not PresentationFinishNodeRecord and
                       not PresentationJoinNodeRecord and not PresentationForkNodeRecord;
        }
    }
}
#endif
