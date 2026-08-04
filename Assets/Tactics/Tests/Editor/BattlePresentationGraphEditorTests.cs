using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Units.Abilities;
using Tactics.Common.Units.Tween;
using Tactics.Editor.PresentationGraph;
using UnityEditor;
using UnityEngine;

namespace Tactics.Tests.Editor
{
    public sealed class BattlePresentationGraphEditorTests
    {
        private readonly List<Object> _owned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object value in _owned)
            {
                if (value != null)
                    Object.DestroyImmediate(value);
            }
            _owned.Clear();
        }

        [Test]
        public async Task Runner_EmitsDuplicateMarkersAtMostOnce()
        {
            BattlePresentationGraph graph = CreateGraph();
            var entry = Add<PresentationEntryNodeRecord>(graph, PresentationNodeType.Entry);
            entry.Cue = PresentationCueKind.Action;
            var first = Add<PresentationMarkerNodeRecord>(graph, PresentationNodeType.Marker);
            first.Marker = PresentationMarkerKind.Release;
            var second = Add<PresentationMarkerNodeRecord>(graph, PresentationNodeType.Marker);
            second.Marker = PresentationMarkerKind.Release;
            var finish = Add<PresentationFinishNodeRecord>(graph, PresentationNodeType.Finish);
            Connect(graph, entry, first, second, finish);

            int releases = 0;
            bool handled = await BattlePresentationCoordinator.TryPlayCueAsync(
                graph,
                PresentationCueKind.Action,
                new PresentationExecutionContext(null, null, null, 1, CancellationToken.None),
                marker =>
                {
                    if (marker == PresentationMarkerKind.Release)
                        releases++;
                });

            Assert.That(handled, Is.True);
            Assert.That(releases, Is.EqualTo(1));
        }

        [Test]
        public async Task Coordinator_ProjectileCueReturnsAtImpactBeforeVisualTailCompletes()
        {
            BattlePresentationGraph graph = CreateGraph();
            var entry = Add<PresentationEntryNodeRecord>(graph, PresentationNodeType.Entry);
            entry.Cue = PresentationCueKind.Projectile;
            var impact = Add<PresentationMarkerNodeRecord>(graph, PresentationNodeType.Marker);
            impact.Marker = PresentationMarkerKind.Impact;
            var tail = Add<PresentationDelayNodeRecord>(graph, PresentationNodeType.Delay);
            tail.Duration = 0.5f;
            var finish = Add<PresentationFinishNodeRecord>(graph, PresentationNodeType.Finish);
            Connect(graph, entry, impact, tail, finish);
            using var cancellation = new CancellationTokenSource();

            Task<bool> handledTask = BattlePresentationCoordinator.TryPlayCueUntilMarkerAsync(
                graph,
                PresentationCueKind.Projectile,
                new PresentationExecutionContext(null, null, null, 1, cancellation.Token),
                PresentationMarkerKind.Impact);
            Task first = await Task.WhenAny(handledTask, Task.Delay(100));

            Assert.That(first, Is.SameAs(handledTask));
            Assert.That(await handledTask, Is.True);
            cancellation.Cancel();
        }

        [Test]
        public async Task Runner_NestedForkPreservesOuterJoinBoundary()
        {
            BattlePresentationGraph graph = CreateGraph();
            var entry = Add<PresentationEntryNodeRecord>(graph, PresentationNodeType.Entry);
            entry.Cue = PresentationCueKind.Projectile;
            var outerFork = Add<PresentationForkNodeRecord>(graph, PresentationNodeType.Fork);
            var innerFork = Add<PresentationForkNodeRecord>(graph, PresentationNodeType.Fork);
            var innerFirst = Add<PresentationMarkerNodeRecord>(graph, PresentationNodeType.Marker);
            var innerSecond = Add<PresentationMarkerNodeRecord>(graph, PresentationNodeType.Marker);
            innerFirst.Marker = PresentationMarkerKind.Release;
            innerSecond.Marker = PresentationMarkerKind.Release;
            var innerJoin = Add<PresentationJoinNodeRecord>(graph, PresentationNodeType.Join);
            var outerBranch = Add<PresentationMarkerNodeRecord>(graph, PresentationNodeType.Marker);
            outerBranch.Marker = PresentationMarkerKind.Release;
            var outerJoin = Add<PresentationJoinNodeRecord>(graph, PresentationNodeType.Join);
            var tail = Add<PresentationMarkerNodeRecord>(graph, PresentationNodeType.Marker);
            tail.Marker = PresentationMarkerKind.Release;
            var impact = Add<PresentationMarkerNodeRecord>(graph, PresentationNodeType.Marker);
            impact.Marker = PresentationMarkerKind.Impact;
            var finish = Add<PresentationFinishNodeRecord>(graph, PresentationNodeType.Finish);
            outerFork.JoinNodeId = outerJoin.NodeId;
            innerFork.JoinNodeId = innerJoin.NodeId;
            graph.AddEdge(entry.NodeId, outerFork.NodeId);
            graph.AddEdge(outerFork.NodeId, innerFork.NodeId);
            graph.AddEdge(outerFork.NodeId, outerBranch.NodeId);
            graph.AddEdge(innerFork.NodeId, innerFirst.NodeId);
            graph.AddEdge(innerFork.NodeId, innerSecond.NodeId);
            graph.AddEdge(innerFirst.NodeId, innerJoin.NodeId);
            graph.AddEdge(innerSecond.NodeId, innerJoin.NodeId);
            graph.AddEdge(innerJoin.NodeId, outerJoin.NodeId);
            graph.AddEdge(outerBranch.NodeId, outerJoin.NodeId);
            Connect(graph, outerJoin, tail, impact, finish);
            int tailVisits = 0;
            var runner = new PresentationGraphRunner(
                graph,
                new PresentationExecutionContext(null, null, null, 1, CancellationToken.None),
                null,
                node =>
                {
                    if (node.NodeId == tail.NodeId)
                        tailVisits++;
                });

            await runner.PlayAsync(PresentationCueKind.Projectile);

            Assert.That(tailVisits, Is.EqualTo(1),
                "A nested fork branch must stop at the outer join instead of executing its tail itself.");
        }

        [Test]
        public void Validation_DisabledMarkerDoesNotSatisfyRequiredCueMarker()
        {
            BattlePresentationGraph graph = CreateGraph();
            var entry = Add<PresentationEntryNodeRecord>(graph, PresentationNodeType.Entry);
            entry.Cue = PresentationCueKind.Projectile;
            var impact = Add<PresentationMarkerNodeRecord>(graph, PresentationNodeType.Marker);
            impact.Marker = PresentationMarkerKind.Impact;
            impact.Enabled = false;
            var finish = Add<PresentationFinishNodeRecord>(graph, PresentationNodeType.Finish);
            Connect(graph, entry, impact, finish);

            bool valid = BattlePresentationGraphValidation.Validate(graph, out var errors);

            Assert.That(valid, Is.False);
            Assert.That(errors.Exists(error => error.Code == "MissingRequiredMarker"), Is.True);
        }

        [Test]
        public void Validation_RejectsCyclesAndDuplicateEntries()
        {
            BattlePresentationGraph graph = CreateGraph();
            var first = Add<PresentationEntryNodeRecord>(graph, PresentationNodeType.Entry);
            first.Cue = PresentationCueKind.Action;
            var second = Add<PresentationEntryNodeRecord>(graph, PresentationNodeType.Entry);
            second.Cue = PresentationCueKind.Action;
            var marker = Add<PresentationMarkerNodeRecord>(graph, PresentationNodeType.Marker);
            graph.AddEdge(first.NodeId, marker.NodeId);
            graph.AddEdge(marker.NodeId, first.NodeId);

            bool valid = BattlePresentationGraphValidation.Validate(graph, out var errors);

            Assert.That(valid, Is.False);
            Assert.That(errors.Exists(error => error.Code == "DuplicateEntry"), Is.True);
            Assert.That(errors.Exists(error => error.Code == "CycleDetected"), Is.True);
        }

        [Test]
        public void Validation_RequiresReferencedLeafAssets()
        {
            BattlePresentationGraph graph = CreateGraph();
            var entry = Add<PresentationEntryNodeRecord>(graph, PresentationNodeType.Entry);
            entry.Cue = PresentationCueKind.Projectile;
            var projectile = Add<PresentationProjectileNodeRecord>(graph, PresentationNodeType.Projectile);
            var finish = Add<PresentationFinishNodeRecord>(graph, PresentationNodeType.Finish);
            Connect(graph, entry, projectile, finish);

            bool valid = BattlePresentationGraphValidation.Validate(graph, out var errors);

            Assert.That(valid, Is.False);
            Assert.That(errors.Exists(error => error.Code == "MissingProjectileProfile"), Is.True);
        }

        [Test]
        public void Validation_RequiresEveryForkBranchToReachItsJoin()
        {
            BattlePresentationGraph graph = CreateGraph();
            var entry = Add<PresentationEntryNodeRecord>(graph, PresentationNodeType.Entry);
            var fork = Add<PresentationForkNodeRecord>(graph, PresentationNodeType.Fork);
            var first = Add<PresentationDelayNodeRecord>(graph, PresentationNodeType.Delay);
            var second = Add<PresentationDelayNodeRecord>(graph, PresentationNodeType.Delay);
            var join = Add<PresentationJoinNodeRecord>(graph, PresentationNodeType.Join);
            var finish = Add<PresentationFinishNodeRecord>(graph, PresentationNodeType.Finish);
            fork.JoinNodeId = join.NodeId;
            graph.AddEdge(entry.NodeId, fork.NodeId);
            graph.AddEdge(fork.NodeId, first.NodeId);
            graph.AddEdge(fork.NodeId, second.NodeId);
            graph.AddEdge(first.NodeId, join.NodeId);
            graph.AddEdge(second.NodeId, finish.NodeId);
            graph.AddEdge(join.NodeId, finish.NodeId);

            bool valid = BattlePresentationGraphValidation.Validate(graph, out var errors);

            Assert.That(valid, Is.False);
            Assert.That(errors.Exists(error => error.Code == "ForkBranchMissesJoin"), Is.True);
        }

        [Test]
        public void Validation_ActionEntryRequiresReleaseMarker()
        {
            BattlePresentationGraph graph = CreateGraph();
            var entry = Add<PresentationEntryNodeRecord>(graph, PresentationNodeType.Entry);
            entry.Cue = PresentationCueKind.Action;
            var delay = Add<PresentationDelayNodeRecord>(graph, PresentationNodeType.Delay);
            var finish = Add<PresentationFinishNodeRecord>(graph, PresentationNodeType.Finish);
            Connect(graph, entry, delay, finish);

            bool valid = BattlePresentationGraphValidation.Validate(graph, out var errors);

            Assert.That(valid, Is.False);
            Assert.That(errors.Exists(error => error.Code == "MissingRequiredMarker"), Is.True);
        }

        [Test]
        public void Graph_AddRemoveNode_CleansConnectedEdges()
        {
            BattlePresentationGraph graph = CreateGraph();
            PresentationNodeRecord entry = graph.AddNode(PresentationNodeType.Entry, Vector2.zero);
            PresentationNodeRecord finish = graph.AddNode(PresentationNodeType.Finish, Vector2.right);
            graph.AddEdge(entry.NodeId, finish.NodeId);

            Assert.That(graph.RemoveNode(finish.NodeId), Is.True);
            Assert.That(graph.Edges, Is.Empty);
        }

        [Test]
        public void GraphView_Reload_DoesNotMutateGraphAsset()
        {
            BattlePresentationGraph graph = CreateGraph();
            var entry = Add<PresentationEntryNodeRecord>(graph, PresentationNodeType.Entry);
            var finish = Add<PresentationFinishNodeRecord>(graph, PresentationNodeType.Finish);
            Connect(graph, entry, finish);
            var view = new PresentationGraphView();

            view.Load(graph);
            view.Load(graph);

            Assert.That(graph.Nodes, Has.Count.EqualTo(2));
            Assert.That(graph.Edges, Has.Count.EqualTo(1));
        }

        [TestCase("Lightning")]
        [TestCase("Lightning_Lv2")]
        [TestCase("Lightning_Lv3")]
        [TestCase("Curse")]
        [TestCase("Curse_Lv2")]
        [TestCase("Curse_Lv3")]
        [TestCase("PoisonSpear")]
        [TestCase("PoisonSpear_Lv2")]
        [TestCase("PoisonSpear_Lv3")]
        public void RepresentativeAbility_ReferencesValidPresentationGraph(string abilityName)
        {
            SkillGraphAbilityConfig config = AssetDatabase.LoadAssetAtPath<SkillGraphAbilityConfig>(
                $"Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/{abilityName}_Graph_Ability.asset");

            Assert.That(config, Is.Not.Null);
            Assert.That(config.PresentationGraph, Is.Not.Null);
            Assert.That(
                BattlePresentationGraphValidation.Validate(config.PresentationGraph, out var errors),
                Is.True,
                string.Join("; ", errors.ConvertAll(error => error.Code)));
            Assert.That(config.PresentationGraph.FindEntry(PresentationCueKind.Action), Is.Not.Null);
        }

        [TestCase("Lightning", PresentationCueKind.PrimaryTargetHit)]
        [TestCase("Lightning_Lv2", PresentationCueKind.PrimaryTargetHit)]
        [TestCase("Lightning_Lv3", PresentationCueKind.PrimaryTargetHit)]
        [TestCase("Curse", PresentationCueKind.PrimaryTargetHit)]
        [TestCase("Curse_Lv2", PresentationCueKind.PrimaryTargetHit)]
        [TestCase("Curse_Lv3", PresentationCueKind.PrimaryTargetHit)]
        [TestCase("PoisonSpear", PresentationCueKind.Projectile)]
        [TestCase("PoisonSpear_Lv2", PresentationCueKind.Projectile)]
        [TestCase("PoisonSpear_Lv3", PresentationCueKind.Projectile)]
        public void FirstBatchPresentation_HasExplicitDefaultPreviewEntry(
            string presentationName,
            PresentationCueKind expectedEntry)
        {
            BattlePresentationGraph graph = AssetDatabase.LoadAssetAtPath<BattlePresentationGraph>(
                $"Assets/Tactics/Arts/PureRun/Presentation/{presentationName}_Presentation.asset");

            Assert.That(graph, Is.Not.Null, presentationName);
            Assert.That(graph.DefaultPreviewEntry, Is.EqualTo(expectedEntry), presentationName);
            Assert.That(graph.FindEntry(expectedEntry), Is.Not.Null, presentationName);
        }

        [TestCase("Lightning", "PureRunMage", 2)]
        [TestCase("Lightning_Lv2", "PureRunMage", 2)]
        [TestCase("Lightning_Lv3", "PureRunMage", 2)]
        [TestCase("Curse", "PureRunNecromancer", 2)]
        [TestCase("Curse_Lv2", "PureRunNecromancer", 2)]
        [TestCase("Curse_Lv3", "PureRunNecromancer", 2)]
        [TestCase("PoisonSpear", "PureRunHunter", 2)]
        [TestCase("PoisonSpear_Lv2", "PureRunHunter", 2)]
        [TestCase("PoisonSpear_Lv3", "PureRunHunter", 2)]
        [TestCase("Thrust", "PureRunHunter", 3)]
        [TestCase("Thrust_Lv2", "PureRunHunter", 3)]
        [TestCase("Thrust_Lv3", "PureRunHunter", 3)]
        [TestCase("Fireball", "PureRunMage", 4)]
        [TestCase("Fireball_Lv2", "PureRunMage", 4)]
        [TestCase("Fireball_Lv3", "PureRunMage", 5)]
        [TestCase("BoneSpear", "PureRunNecromancer", 3)]
        [TestCase("BoneSpear_Lv2", "PureRunNecromancer", 3)]
        [TestCase("BoneSpear_Lv3", "PureRunNecromancer", 3)]
        public void PublishedPresentation_HasValidFullPreviewScenario(
            string presentationName,
            string actorName,
            int phaseCount)
        {
            BattlePresentationGraph graph = LoadPresentation(presentationName);

            Assert.That(graph.HasPreviewScenario, Is.True, presentationName);
            Assert.That(graph.PreviewActorPrefab, Is.Not.Null, presentationName);
            Assert.That(graph.PreviewActorPrefab.name, Is.EqualTo(actorName), presentationName);
            Assert.That(graph.PreviewTargetPrefab, Is.Not.Null, presentationName);
            Assert.That(graph.PreviewTargetPrefab.name, Is.EqualTo("PureRunGoatCharger"), presentationName);
            Assert.That(graph.PreviewPhases, Has.Count.EqualTo(phaseCount), presentationName);
            Assert.That(
                PresentationPreviewScenarioValidation.Validate(graph, out List<string> errors),
                Is.True,
                $"{presentationName}: {string.Join("; ", errors)}");

            AssertPublishedScenarioShape(graph, presentationName);
        }

        [Test]
        public void PreviewScenarioValidation_DoesNotChangeRuntimeGraphValidity()
        {
            BattlePresentationGraph graph = CreateGraph();
            var entry = Add<PresentationEntryNodeRecord>(graph, PresentationNodeType.Entry);
            entry.Cue = PresentationCueKind.Action;
            var marker = Add<PresentationMarkerNodeRecord>(graph, PresentationNodeType.Marker);
            marker.Marker = PresentationMarkerKind.Release;
            var finish = Add<PresentationFinishNodeRecord>(graph, PresentationNodeType.Finish);
            Connect(graph, entry, marker, finish);

            Assert.That(BattlePresentationGraphValidation.Validate(graph, out var runtimeErrors), Is.True,
                string.Join("; ", runtimeErrors.ConvertAll(error => error.Code)));
            Assert.That(PresentationPreviewScenarioValidation.Validate(graph, out var previewErrors), Is.False);
            Assert.That(previewErrors, Has.Some.Contains("PreviewScenarioMissing"));
        }

        [TestCase("Thrust_Graph_Ability", "Thrust")]
        [TestCase("Thrust_Lv2_Graph_Ability", "Thrust_Lv2")]
        [TestCase("Thrust_Lv3_Graph_Ability", "Thrust_Lv3")]
        [TestCase("Fireball_Graph_Ability", "Fireball")]
        [TestCase("Fireball_Lv1_Ability", "Fireball")]
        [TestCase("Fireball_Lv2_Ability", "Fireball_Lv2")]
        [TestCase("Fireball_Lv3_Ability", "Fireball_Lv3")]
        [TestCase("SkeletonMageFireball_Lv1_Ability", "Fireball")]
        [TestCase("SkeletonMageFireball_Lv2_Ability", "Fireball_Lv2")]
        [TestCase("BoneSpear_Graph_Ability", "BoneSpear")]
        [TestCase("BoneSpear_Lv2_Graph_Ability", "BoneSpear_Lv2")]
        [TestCase("BoneSpear_Lv3_Graph_Ability", "BoneSpear_Lv3")]
        public void ProgrammaticAbility_UsesGraphOnlyPresentation(
            string configName,
            string presentationName)
        {
            SkillGraphAbilityConfig config = AssetDatabase.LoadAssetAtPath<SkillGraphAbilityConfig>(
                $"Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/{configName}.asset");
            BattlePresentationGraph expected = AssetDatabase.LoadAssetAtPath<BattlePresentationGraph>(
                $"Assets/Tactics/Arts/PureRun/Presentation/{presentationName}_Presentation.asset");

            Assert.That(config, Is.Not.Null, configName);
            Assert.That(expected, Is.Not.Null, presentationName);
            Assert.That(config.PresentationGraph, Is.SameAs(expected), configName);
            Assert.That(config.VisualAction, Is.EqualTo(UnitVisualAction.None), configName);
            Assert.That(config.SkillVfxRecipe, Is.Null, configName);
            Assert.That(BattlePresentationGraphValidation.Validate(expected, out var errors), Is.True,
                string.Join("; ", errors.ConvertAll(error => error.Code)));
            Assert.That(expected.FindEntry(PresentationCueKind.Action), Is.Not.Null, presentationName);
        }

        [TestCase("Thrust", UnitVisualAction.Melee, false, 2, PresentationCueKind.DirectionalStrike)]
        [TestCase("Thrust_Lv2", UnitVisualAction.Melee, false, 2, PresentationCueKind.DirectionalStrike)]
        [TestCase("Thrust_Lv3", UnitVisualAction.Melee, false, 2, PresentationCueKind.DirectionalStrike)]
        [TestCase("Fireball", UnitVisualAction.Cast, true, 4, PresentationCueKind.Projectile)]
        [TestCase("Fireball_Lv2", UnitVisualAction.Cast, true, 4, PresentationCueKind.Projectile)]
        [TestCase("Fireball_Lv3", UnitVisualAction.Cast, true, 4, PresentationCueKind.Projectile)]
        [TestCase("BoneSpear", UnitVisualAction.Cast, true, 2, PresentationCueKind.Projectile)]
        [TestCase("BoneSpear_Lv2", UnitVisualAction.Cast, true, 2, PresentationCueKind.Projectile)]
        [TestCase("BoneSpear_Lv3", UnitVisualAction.Cast, true, 2, PresentationCueKind.Projectile)]
        public void ProgrammaticPresentation_HasExpectedSemanticEntries(
            string presentationName,
            UnitVisualAction action,
            bool hasProjectile,
            int proceduralCount,
            PresentationCueKind defaultPreviewEntry)
        {
            BattlePresentationGraph graph = AssetDatabase.LoadAssetAtPath<BattlePresentationGraph>(
                $"Assets/Tactics/Arts/PureRun/Presentation/{presentationName}_Presentation.asset");

            Assert.That(graph, Is.Not.Null, presentationName);
            Assert.That(graph.DefaultPreviewEntry, Is.EqualTo(defaultPreviewEntry), presentationName);
            Assert.That(graph.Nodes.OfType<PresentationUnitTweenNodeRecord>().Single().Action,
                Is.EqualTo(action), presentationName);
            Assert.That(graph.Nodes.OfType<PresentationProceduralVfxNodeRecord>().ToArray(),
                Has.Length.EqualTo(proceduralCount), presentationName);
            Assert.That(graph.FindEntry(PresentationCueKind.Projectile) != null,
                Is.EqualTo(hasProjectile), presentationName);
            PresentationProjectileNodeRecord[] projectiles = graph.Nodes
                .OfType<PresentationProjectileNodeRecord>()
                .ToArray();
            if (!hasProjectile)
            {
                Assert.That(projectiles, Is.Empty, presentationName);
                return;
            }

            Assert.That(projectiles, Has.Length.EqualTo(1), presentationName);
            bool fireball = presentationName.StartsWith("Fireball");
            Assert.That(AssetDatabase.GetAssetPath(projectiles[0].Profile),
                Is.EqualTo($"Assets/Tactics/Arts/PureRun/Tween/Projectiles/{(fireball ? "Fire" : "BoneSpear")}.asset"),
                presentationName);
            Assert.That(projectiles[0].Speed, Is.EqualTo(fireball ? 8f : 12f), presentationName);
            Assert.That(projectiles[0].FallbackTravelTime,
                Is.EqualTo(fireball ? 0.5f : 0.25f), presentationName);
        }

        [TestCase("Thrust", "ThrustStrikeLv1|ThrustHitLv1")]
        [TestCase("Thrust_Lv2", "ThrustStrikeLv2|ThrustHitLv2")]
        [TestCase("Thrust_Lv3", "ThrustStrikeLv3|ThrustHitLv3")]
        [TestCase("Fireball", "FireballChargeLv1|FireballImpactLv1")]
        [TestCase("Fireball_Lv2", "FireballChargeLv2|FireballImpactLv2")]
        [TestCase("Fireball_Lv3", "FireballChargeLv3|FireballImpactLv3|FireballDetonationLv3")]
        [TestCase("BoneSpear", "BoneSpearChargeLv1|BoneSpearImpactLv1")]
        [TestCase("BoneSpear_Lv2", "BoneSpearChargeLv2|BoneSpearImpactLv2")]
        [TestCase("BoneSpear_Lv3", "BoneSpearChargeLv3|BoneSpearImpactLv3")]
        public void HybridPresentation_UsesClosedProceduralAndPrefabForks(
            string presentationName,
            string expectedProfileNames)
        {
            BattlePresentationGraph graph = LoadPresentation(presentationName);
            string[] expected = expectedProfileNames.Split('|');
            PresentationPrefabFxNodeRecord[] effects = graph.Nodes
                .OfType<PresentationPrefabFxNodeRecord>()
                .ToArray();
            Assert.That(effects.Select(effect => effect.Profile?.name), Is.EquivalentTo(expected));

            PresentationForkNodeRecord[] forks = graph.Nodes
                .OfType<PresentationForkNodeRecord>()
                .ToArray();
            Assert.That(forks, Has.Length.EqualTo(expected.Length));
            foreach (PresentationForkNodeRecord fork in forks)
            {
                PresentationJoinNodeRecord join = graph.Nodes
                    .OfType<PresentationJoinNodeRecord>()
                    .Single(candidate => candidate.NodeId == fork.JoinNodeId);
                PresentationEdgeRecord[] branches = graph.GetEdgesFrom(fork.NodeId).ToArray();
                Assert.That(branches, Has.Length.EqualTo(2));
                Assert.That(branches.Select(edge => graph.FindNode(edge.TargetNodeId)),
                    Has.Exactly(1).InstanceOf<PresentationProceduralVfxNodeRecord>());
                Assert.That(branches.Select(edge => graph.FindNode(edge.TargetNodeId)),
                    Has.Exactly(1).InstanceOf<PresentationPrefabFxNodeRecord>());
                Assert.That(branches.All(edge => graph.GetEdgesFrom(edge.TargetNodeId)
                    .Exists(candidate => candidate.TargetNodeId == join.NodeId)), Is.True);
            }
        }

        [Test]
        public void PresentationExecutionContext_PreservesFullVfxCueSnapshot()
        {
            var path = new[] { Vector3.left, Vector3.zero, Vector3.right };
            var hits = new[] { Vector3.up, Vector3.down };
            var snapshot = new SkillVfxCueContext(
                3,
                Vector3.left,
                Vector3.right,
                Vector3.right,
                path,
                hits,
                hits[1],
                0.55f);
            var context = new PresentationExecutionContext(
                null,
                null,
                null,
                3,
                CancellationToken.None,
                null,
                snapshot.SourceWorldPosition,
                snapshot.TargetWorldPosition,
                snapshot);

            Assert.That(context.VfxCueContext, Is.SameAs(snapshot));
            Assert.That(context.VfxCueContext.PathWorldPositions, Is.EqualTo(path));
            Assert.That(context.VfxCueContext.HitWorldPositions, Is.EqualTo(hits));
            Assert.That(context.VfxCueContext.PrimaryHitWorldPosition, Is.EqualTo(hits[1]));
            Assert.That(context.VfxCueContext.StrengthMultiplier, Is.EqualTo(0.55f));
        }

        [TestCase("Fireball_Graph")]
        [TestCase("Fireball_Lv1_Graph")]
        [TestCase("Fireball_Lv2_Graph")]
        [TestCase("Fireball_Lv3_Graph")]
        [TestCase("BoneSpear_Graph")]
        [TestCase("BoneSpear_Lv2_Graph")]
        [TestCase("BoneSpear_Lv3_Graph")]
        public void GraphOwnedProjectile_ClearsGameplayVisualProfile(string graphName)
        {
            SkillGraphAsset graph = AssetDatabase.LoadAssetAtPath<SkillGraphAsset>(
                $"Assets/Tactics/Battle/Abilities/SkillGraphs/{graphName}.asset");

            Assert.That(graph, Is.Not.Null, graphName);
            Assert.That(graph.Nodes.OfType<ProjectileLaunchNodeRecord>().Single().VisualProfile,
                Is.Null, graphName);
        }

        [TestCase("Lightning")]
        [TestCase("Lightning_Lv2")]
        [TestCase("Lightning_Lv3")]
        [TestCase("Curse")]
        [TestCase("Curse_Lv2")]
        [TestCase("Curse_Lv3")]
        public void MigratedCueGraph_UsesPresentationCueInsteadOfLegacyVisualCue(string abilityName)
        {
            SkillGraphAsset graph = AssetDatabase.LoadAssetAtPath<SkillGraphAsset>(
                $"Assets/Tactics/Battle/Abilities/SkillGraphs/{abilityName}_Graph.asset");

            Assert.That(graph.Nodes.Exists(node => node is PlayPresentationCueNodeRecord), Is.True);
            Assert.That(graph.Nodes.Exists(node => node is PlayVisualCueNodeRecord), Is.False);
        }

        [TestCase("PoisonSpear")]
        [TestCase("PoisonSpear_Lv2")]
        [TestCase("PoisonSpear_Lv3")]
        public void PoisonSpearPresentation_ContainsSingleProjectileEntry(string abilityName)
        {
            BattlePresentationGraph graph = AssetDatabase.LoadAssetAtPath<BattlePresentationGraph>(
                $"Assets/Tactics/Arts/PureRun/Presentation/{abilityName}_Presentation.asset");

            Assert.That(graph.FindEntry(PresentationCueKind.Projectile), Is.Not.Null);
            Assert.That(graph.Nodes.FindAll(node => node is PresentationProjectileNodeRecord), Has.Count.EqualTo(1));
        }

        [TestCase("Curse", 1)]
        [TestCase("Curse_Lv2", 2)]
        [TestCase("Curse_Lv3", 3)]
        public void CursePresentation_UsesClosedLayeredSigilFork(string abilityName, int level)
        {
            BattlePresentationGraph graph = AssetDatabase.LoadAssetAtPath<BattlePresentationGraph>(
                $"Assets/Tactics/Arts/PureRun/Presentation/{abilityName}_Presentation.asset");
            Assert.That(graph, Is.Not.Null);

            PresentationForkNodeRecord fork = graph.Nodes.OfType<PresentationForkNodeRecord>().Single();
            PresentationJoinNodeRecord join = graph.Nodes.OfType<PresentationJoinNodeRecord>().Single();
            Assert.That(fork.JoinNodeId, Is.EqualTo(join.NodeId));

            PresentationPrefabFxNodeRecord[] effects = graph.Nodes
                .OfType<PresentationPrefabFxNodeRecord>()
                .ToArray();
            Assert.That(effects, Has.Length.EqualTo(3));
            Assert.That(effects.Select(effect => AssetDatabase.GetAssetPath(effect.Profile)), Is.EquivalentTo(new[]
            {
                $"Assets/Tactics/Arts/PureRun/VFX/PilotoAdapted/Profiles/AmplifyDamageSigilGroundV2Lv{level}.asset",
                $"Assets/Tactics/Arts/PureRun/VFX/PilotoAdapted/Profiles/AmplifyDamageSigilRearFlamesV2Lv{level}.asset",
                $"Assets/Tactics/Arts/PureRun/VFX/PilotoAdapted/Profiles/AmplifyDamageSigilForegroundFlamesV2Lv{level}.asset"
            }));
            Assert.That(graph.GetEdgesFrom(fork.NodeId), Has.Count.EqualTo(3));
            Assert.That(effects.All(effect => graph.GetEdgesFrom(effect.NodeId)
                .Exists(edge => edge.TargetNodeId == join.NodeId)), Is.True);
            Assert.That(graph.Nodes.OfType<PresentationMarkerNodeRecord>()
                .Count(marker => marker.Marker == PresentationMarkerKind.Impact), Is.EqualTo(1));
            Assert.That(BattlePresentationGraphValidation.Validate(graph, out var errors), Is.True,
                string.Join("; ", errors.ConvertAll(error => error.Code)));
        }

        private BattlePresentationGraph CreateGraph()
        {
            var graph = ScriptableObject.CreateInstance<BattlePresentationGraph>();
            _owned.Add(graph);
            return graph;
        }

        private static BattlePresentationGraph LoadPresentation(string presentationName)
        {
            BattlePresentationGraph graph = AssetDatabase.LoadAssetAtPath<BattlePresentationGraph>(
                $"Assets/Tactics/Arts/PureRun/Presentation/{presentationName}_Presentation.asset");
            Assert.That(graph, Is.Not.Null, presentationName);
            return graph;
        }

        private static void AssertPublishedScenarioShape(
            BattlePresentationGraph graph,
            string presentationName)
        {
            PresentationPreviewPhaseRecord[] phases = graph.PreviewPhases.ToArray();
            bool castProjectile = presentationName.StartsWith("Fireball") ||
                                  presentationName.StartsWith("BoneSpear");
            if (!castProjectile)
                AssertPhase(phases[0], PresentationCueKind.Action, PresentationPreviewAdvanceKind.Release);

            if (presentationName.StartsWith("Lightning"))
            {
                AssertPhase(phases[1], PresentationCueKind.PrimaryTargetHit, PresentationPreviewAdvanceKind.Complete, true);
                return;
            }
            if (presentationName.StartsWith("Curse"))
            {
                AssertPhase(phases[1], PresentationCueKind.PrimaryTargetHit, PresentationPreviewAdvanceKind.Complete);
                return;
            }
            if (presentationName.StartsWith("PoisonSpear"))
            {
                AssertPhase(phases[1], PresentationCueKind.Projectile, PresentationPreviewAdvanceKind.Impact, true);
                return;
            }
            if (presentationName.StartsWith("Thrust"))
            {
                AssertPhase(phases[1], PresentationCueKind.DirectionalStrike, PresentationPreviewAdvanceKind.Blocking);
                AssertPhase(phases[2], PresentationCueKind.PrimaryTargetHit, PresentationPreviewAdvanceKind.Complete, true);
                return;
            }

            Assert.That(phases[0].Cues, Is.EqualTo(new[]
            {
                PresentationCueKind.CastCharge,
                PresentationCueKind.Action
            }), presentationName);
            Assert.That(phases[0].ContinuationCue, Is.EqualTo(PresentationCueKind.Action), presentationName);

            AssertPhase(phases[1], PresentationCueKind.Projectile, PresentationPreviewAdvanceKind.Impact);
            if (presentationName.StartsWith("BoneSpear"))
            {
                AssertPhase(phases[2], PresentationCueKind.PrimaryTargetHit, PresentationPreviewAdvanceKind.Complete, true);
                return;
            }

            AssertPhase(phases[2], PresentationCueKind.ProjectileImpact, PresentationPreviewAdvanceKind.Blocking);
            int finalIndex = 3;
            if (presentationName == "Fireball_Lv3")
            {
                AssertPhase(phases[3], PresentationCueKind.ConditionalDetonation, PresentationPreviewAdvanceKind.Blocking);
                finalIndex = 4;
            }
            AssertPhase(phases[finalIndex], PresentationCueKind.SecondaryTargetHit, PresentationPreviewAdvanceKind.Complete, true);
        }

        private static void AssertPhase(
            PresentationPreviewPhaseRecord phase,
            PresentationCueKind cue,
            PresentationPreviewAdvanceKind advanceKind,
            bool playTargetHitReaction = false)
        {
            Assert.That(phase.Cues, Is.EqualTo(new[] { cue }));
            Assert.That(phase.ContinuationCue, Is.EqualTo(cue));
            Assert.That(phase.AdvanceKind, Is.EqualTo(advanceKind));
            Assert.That(phase.PlayTargetHitReaction, Is.EqualTo(playTargetHitReaction));
        }

        private static T Add<T>(BattlePresentationGraph graph, PresentationNodeType type)
            where T : PresentationNodeRecord
        {
            return (T)graph.AddNode(type, Vector2.zero);
        }

        private static void Connect(
            BattlePresentationGraph graph,
            params PresentationNodeRecord[] nodes)
        {
            for (int index = 0; index < nodes.Length - 1; index++)
                graph.AddEdge(nodes[index].NodeId, nodes[index + 1].NodeId);
        }
    }
}
