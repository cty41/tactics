#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using Tactics.Common.Skills.Graph;
using UnityEngine;

namespace Tactics.Tests.Editor
{
    public sealed class PresentationExecutionPlanEditorTests
    {
        private BattlePresentationGraph _graph;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<BattlePresentationGraph>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_graph);
        }

        [Test]
        public void Compile_SequenceSkipsDisabledNodeAndStopsAtFinish()
        {
            PresentationNodeRecord entry = Add(PresentationNodeType.Entry, "entry");
            ((PresentationEntryNodeRecord)entry).Cue = PresentationCueKind.Action;
            PresentationNodeRecord first = Add(PresentationNodeType.Delay, "first");
            PresentationNodeRecord disabled = Add(PresentationNodeType.Marker, "disabled");
            disabled.Enabled = false;
            PresentationNodeRecord finish = Add(PresentationNodeType.Finish, "finish");
            PresentationNodeRecord unreachable = Add(PresentationNodeType.Delay, "unreachable");
            Link(entry, first);
            Link(first, disabled);
            Link(disabled, finish);
            Link(finish, unreachable);

            List<string> visited = Flatten(PresentationExecutionPlanCompiler
                .Compile(_graph, PresentationCueKind.Action).Root);

            CollectionAssert.AreEqual(new[] { "first" }, visited);
        }

        [Test]
        public void Compile_ForkCreatesParallelBranchesAndContinuesAfterJoinOnce()
        {
            PresentationNodeRecord entry = Add(PresentationNodeType.Entry, "entry");
            ((PresentationEntryNodeRecord)entry).Cue = PresentationCueKind.Action;
            var fork = (PresentationForkNodeRecord)Add(PresentationNodeType.Fork, "fork");
            PresentationNodeRecord left = Add(PresentationNodeType.Delay, "left");
            PresentationNodeRecord right = Add(PresentationNodeType.Marker, "right");
            PresentationNodeRecord join = Add(PresentationNodeType.Join, "join");
            PresentationNodeRecord after = Add(PresentationNodeType.Delay, "after");
            fork.JoinNodeId = join.NodeId;
            Link(entry, fork);
            Link(fork, left);
            Link(fork, right);
            Link(left, join);
            Link(right, join);
            Link(join, after);

            PresentationExecutionPlan plan = PresentationExecutionPlanCompiler
                .Compile(_graph, PresentationCueKind.Action);
            var root = (PresentationSequenceStep)plan.Root;

            Assert.That(root.Children[0], Is.TypeOf<PresentationLeafStep>());
            Assert.That(((PresentationLeafStep)root.Children[0]).NodeId, Is.EqualTo("fork"));
            Assert.That(root.Children[1], Is.TypeOf<PresentationParallelStep>());
            var parallel = (PresentationParallelStep)root.Children[1];
            Assert.That(parallel.JoinNodeId, Is.EqualTo("join"));
            Assert.That(parallel.Branches, Has.Count.EqualTo(2));
            CollectionAssert.AreEquivalent(new[] { "left", "right" }, Flatten(parallel));
            Assert.That(Flatten(root).FindAll(id => id == "after"), Has.Count.EqualTo(1));
        }

        private PresentationNodeRecord Add(PresentationNodeType type, string id)
        {
            PresentationNodeRecord node = _graph.AddNode(type, Vector2.zero);
            node.NodeId = id;
            return node;
        }

        private void Link(PresentationNodeRecord source, PresentationNodeRecord target)
        {
            _graph.AddEdge(source.NodeId, target.NodeId);
        }

        private static List<string> Flatten(PresentationPlanStep step)
        {
            var result = new List<string>();
            switch (step)
            {
                case PresentationLeafStep leaf:
                    result.Add(leaf.NodeId);
                    break;
                case PresentationSequenceStep sequence:
                    foreach (PresentationPlanStep child in sequence.Children)
                        result.AddRange(Flatten(child));
                    break;
                case PresentationParallelStep parallel:
                    foreach (PresentationPlanStep branch in parallel.Branches)
                        result.AddRange(Flatten(branch));
                    break;
            }
            return result;
        }
    }
}
#endif
