using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Units.Abilities;
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
