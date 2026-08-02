using System.Collections.Generic;
using NUnit.Framework;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Skills.Graph.Testing;
using Tactics.Editor.SkillGraphEditor;
using UnityEditor;
using UnityEngine;

namespace Tactics.Tests.PlayMode
{
    public class SkillGraphValidationTests
    {
        [Test]
        public void Validate_MissingStartNode_ReturnsMissingEntryNode()
        {
            var graph = ScriptableObject.CreateInstance<SkillGraphAsset>();
            graph.DisplayName = "NoStart";
            graph.Nodes.Add(new FinishNodeRecord { NodeId = "finish" });

            var valid = SkillGraphValidation.Validate(graph, out var errors, out _);
            Assert.IsFalse(valid);
            Assert.That(errors, Has.Some.Matches<SkillGraphDiagnostic>(d => d.Code == SkillGraphValidation.MissingEntryNode));
        }

        [Test]
        public void Validate_MultipleStartNodes_ReturnsMultipleEntryNodes()
        {
            var graph = ScriptableObject.CreateInstance<SkillGraphAsset>();
            graph.DisplayName = "MultiStart";
            graph.Nodes.Add(new StartNodeRecord { NodeId = "s1" });
            graph.Nodes.Add(new StartNodeRecord { NodeId = "s2" });
            graph.Nodes.Add(new FinishNodeRecord { NodeId = "finish" });
            graph.AddEdge("s1", "finish");
            graph.AddEdge("s2", "finish");

            var valid = SkillGraphValidation.Validate(graph, out var errors, out _);
            Assert.IsFalse(valid);
            Assert.That(errors, Has.Some.Matches<SkillGraphDiagnostic>(d => d.Code == SkillGraphValidation.MultipleEntryNodes));
        }

        [Test]
        public void Validate_NoTerminalNode_ReturnsNoTerminalNode()
        {
            var graph = ScriptableObject.CreateInstance<SkillGraphAsset>();
            graph.DisplayName = "NoTerminal";
            graph.Nodes.Add(new StartNodeRecord { NodeId = "start" });
            graph.Nodes.Add(new SelectPrimaryTargetNodeRecord { NodeId = "target" });
            graph.AddEdge("start", "target");

            var valid = SkillGraphValidation.Validate(graph, out var errors, out _);
            Assert.IsFalse(valid);
            Assert.That(errors, Has.Some.Matches<SkillGraphDiagnostic>(d => d.Code == SkillGraphValidation.NoTerminalNode));
        }

        [Test]
        public void Validate_SelfReferencingEdge_ReturnsSelfReferencingEdge()
        {
            var graph = ScriptableObject.CreateInstance<SkillGraphAsset>();
            graph.DisplayName = "SelfRef";
            graph.Nodes.Add(new StartNodeRecord { NodeId = "start" });
            graph.Nodes.Add(new FinishNodeRecord { NodeId = "finish" });
            graph.Edges.Add(new SkillGraphEdgeRecord
            {
                EdgeId = "self_edge",
                SourceNodeId = "start",
                TargetNodeId = "start",
                PortType = SkillGraphPortType.Default
            });
            graph.AddEdge("start", "finish");

            var valid = SkillGraphValidation.Validate(graph, out var errors, out _);
            Assert.IsFalse(valid);
            Assert.That(errors, Has.Some.Matches<SkillGraphDiagnostic>(d => d.Code == SkillGraphValidation.SelfReferencingEdge));
        }

        [Test]
        public void Validate_EntryNodeHasIncoming_ReturnsError()
        {
            var graph = ScriptableObject.CreateInstance<SkillGraphAsset>();
            graph.DisplayName = "EntryIncoming";
            graph.Nodes.Add(new StartNodeRecord { NodeId = "start" });
            graph.Nodes.Add(new SelectPrimaryTargetNodeRecord { NodeId = "target" });
            graph.Nodes.Add(new FinishNodeRecord { NodeId = "finish" });
            graph.AddEdge("start", "target");
            graph.AddEdge("target", "start");

            var valid = SkillGraphValidation.Validate(graph, out var errors, out _);
            Assert.IsFalse(valid);
            Assert.That(errors, Has.Some.Matches<SkillGraphDiagnostic>(d => d.Code == SkillGraphValidation.EntryNodeHasIncoming));
        }

        [Test]
        public void Validate_UnreachableNode_ReturnsWarning()
        {
            var graph = ScriptableObject.CreateInstance<SkillGraphAsset>();
            graph.DisplayName = "Unreachable";
            graph.Nodes.Add(new StartNodeRecord { NodeId = "start" });
            graph.Nodes.Add(new SelectPrimaryTargetNodeRecord { NodeId = "target" });
            graph.Nodes.Add(new FinishNodeRecord { NodeId = "finish" });
            graph.Nodes.Add(new ApplyHealNodeRecord { NodeId = "orphan_heal" });
            graph.AddEdge("start", "target");
            graph.AddEdge("target", "finish");

            SkillGraphValidation.Validate(graph, out _, out var warnings);
            Assert.That(warnings, Has.Some.Matches<SkillGraphDiagnostic>(d => d.Code == SkillGraphValidation.UnreachableNode));
        }

        [Test]
        public void Validate_MissingTargetSource_ReturnsMissingTargetSource()
        {
            var graph = ScriptableObject.CreateInstance<SkillGraphAsset>();
            graph.DisplayName = "NoTargetSource";
            graph.Nodes.Add(new StartNodeRecord { NodeId = "start" });
            graph.Nodes.Add(new ApplyDamageNodeRecord { NodeId = "damage" });
            graph.Nodes.Add(new FinishNodeRecord { NodeId = "finish" });
            graph.AddEdge("start", "damage");
            graph.AddEdge("damage", "finish");

            var valid = SkillGraphValidation.Validate(graph, out var errors, out _);
            Assert.IsFalse(valid);
            Assert.That(errors, Has.Some.Matches<SkillGraphDiagnostic>(d => d.Code == SkillGraphValidation.MissingTargetSource));
        }

        [Test]
        public void Validate_OrphanNode_ReturnsOrphanWarning()
        {
            var graph = ScriptableObject.CreateInstance<SkillGraphAsset>();
            graph.DisplayName = "Orphan";
            graph.Nodes.Add(new StartNodeRecord { NodeId = "start" });
            graph.Nodes.Add(new FinishNodeRecord { NodeId = "finish" });
            graph.Nodes.Add(new ApplyHealNodeRecord { NodeId = "orphan" });
            graph.AddEdge("start", "finish");

            SkillGraphValidation.Validate(graph, out _, out var warnings);
            Assert.That(warnings, Has.Some.Matches<SkillGraphDiagnostic>(d => d.Code == SkillGraphValidation.OrphanNode));
        }

        [Test]
        public void Validate_ValidGraph_ReturnsTrue()
        {
            var graph = SkillGraphTestGraphFactory.CreateSelfHealGraph("Valid", 5f);
            var valid = SkillGraphValidation.Validate(graph, out var errors, out _);
            Assert.IsTrue(valid, $"Expected valid graph but got errors: {string.Join(", ", errors)}");
        }

        // ── Spec 预校验测试 ──

        [Test]
        public void ValidateSpec_NoStartNode_ReturnsMissingEntryNode()
        {
            var spec = new SkillGraphSpec
            {
                DisplayName = "NoStart",
                Nodes = new List<SkillNodeSpec>
                {
                    new() { Id = "finish", Type = "Finish" }
                }
            };

            var errors = SkillGraphValidation.ValidateSpec(spec);
            Assert.That(errors, Has.Some.Matches<SkillGraphDiagnostic>(d => d.Code == SkillGraphValidation.MissingEntryNode));
        }

        [Test]
        public void ValidateSpec_DuplicateNodeId_ReturnsError()
        {
            var spec = new SkillGraphSpec
            {
                DisplayName = "DupId",
                Nodes = new List<SkillNodeSpec>
                {
                    new() { Id = "start", Type = "Start" },
                    new() { Id = "start", Type = "Finish" }
                }
            };

            var errors = SkillGraphValidation.ValidateSpec(spec);
            Assert.That(errors, Has.Some.Matches<SkillGraphDiagnostic>(d => d.Code == "DuplicateNodeId"));
        }

        [Test]
        public void ValidateSpec_InvalidEdgeSource_ReturnsError()
        {
            var spec = new SkillGraphSpec
            {
                DisplayName = "BadEdge",
                Nodes = new List<SkillNodeSpec>
                {
                    new() { Id = "start", Type = "Start" },
                    new() { Id = "finish", Type = "Finish" }
                },
                Edges = new List<SkillEdgeSpec>
                {
                    new() { Source = "nonexistent", Target = "finish" }
                }
            };

            var errors = SkillGraphValidation.ValidateSpec(spec);
            Assert.That(errors, Has.Some.Matches<SkillGraphDiagnostic>(d => d.Code == SkillGraphValidation.InvalidEdgeSource));
        }

        [Test]
        public void ValidateSpec_ValidSpec_ReturnsEmpty()
        {
            var spec = new SkillGraphSpec
            {
                DisplayName = "Valid",
                Nodes = new List<SkillNodeSpec>
                {
                    new() { Id = "start", Type = "Start" },
                    new() { Id = "target", Type = "SelectPrimaryTarget" },
                    new() { Id = "damage", Type = "ApplyDamage" },
                    new() { Id = "finish", Type = "Finish" }
                },
                Edges = new List<SkillEdgeSpec>
                {
                    new() { Source = "start", Target = "target" },
                    new() { Source = "target", Target = "damage" },
                    new() { Source = "damage", Target = "finish" }
                }
            };

            var errors = SkillGraphValidation.ValidateSpec(spec);
            Assert.IsEmpty(errors, $"Expected no errors but got: {string.Join(", ", errors)}");
        }

        // ── Spec 端到端链路测试 ──

        [Test]
        public void SpecCompiler_ValidSpec_CompilesAndValidates()
        {
            var spec = CreateValidDamageSpec();
            var compileResult = SkillGraphSpecCompiler.Compile(spec);

            Assert.IsTrue(compileResult.Success, $"Compile failed: {string.Join(", ", compileResult.Errors)}");
            Assert.IsNotNull(compileResult.Asset);

            var graph = compileResult.Asset;
            Assert.AreEqual(4, graph.Nodes.Count);
            Assert.AreEqual(3, graph.Edges.Count);

            var valid = SkillGraphValidation.Validate(graph, out var errors, out _);
            Assert.IsTrue(valid, $"ValidateGraph failed: {string.Join(", ", errors)}");
        }

        [Test]
        public void SpecCompiler_InvalidSpec_DoesNotCompile()
        {
            var spec = new SkillGraphSpec
            {
                DisplayName = "Invalid",
                Nodes = new List<SkillNodeSpec>
                {
                    new() { Id = "finish", Type = "Finish" }
                }
            };

            var compileResult = SkillGraphSpecCompiler.Compile(spec);
            Assert.IsFalse(compileResult.Success);
            Assert.IsNull(compileResult.Asset);
            Assert.That(compileResult.Errors, Has.Some.Matches<SkillGraphDiagnostic>(d => d.Code == SkillGraphValidation.MissingEntryNode));
        }

        [Test]
        public void SpecCompiler_ExportAndRecompile_ProducesEquivalentGraph()
        {
            var spec = CreateValidDamageSpec();
            spec.Targeting = new SkillTargetingProtocol
            {
                Mode = SkillTargetMode.DirectionCone,
                MinimumSelections = 1,
                MaximumSelections = 2,
                ConeDepth = 3,
                ConeWidth = 5,
                AllowsEmptyCell = true,
                UsesPathfinding = false
            };
            var compileResult = SkillGraphSpecCompiler.Compile(spec);
            Assert.IsTrue(compileResult.Success);

            Assert.AreEqual(SkillTargetMode.DirectionCone, compileResult.Asset.Targeting.Mode);
            Assert.AreEqual(2, compileResult.Asset.Targeting.MaximumSelections);
            Assert.AreEqual(3, compileResult.Asset.Targeting.ConeDepth);
            Assert.AreEqual(5, compileResult.Asset.Targeting.ConeWidth);
            Assert.IsTrue(compileResult.Asset.Targeting.AllowsEmptyCell);
            Assert.IsFalse(compileResult.Asset.Targeting.UsesPathfinding);

            var exported = SkillGraphSpecCompiler.ExportSpec(compileResult.Asset);
            Assert.IsNotNull(exported);
            Assert.AreEqual(spec.DisplayName, exported.DisplayName);
            Assert.AreEqual(spec.Nodes.Count, exported.Nodes.Count);
            Assert.AreEqual(spec.Edges.Count, exported.Edges.Count);
            Assert.AreEqual(SkillTargetMode.DirectionCone, exported.Targeting.Mode);
            Assert.AreEqual(2, exported.Targeting.MaximumSelections);

            var recompileResult = SkillGraphSpecCompiler.Compile(exported);
            Assert.IsTrue(recompileResult.Success, $"Recompile failed: {string.Join(", ", recompileResult.Errors)}");

            var regraph = recompileResult.Asset;
            Assert.AreEqual(compileResult.Asset.Nodes.Count, regraph.Nodes.Count);
            Assert.AreEqual(compileResult.Asset.Edges.Count, regraph.Edges.Count);
            Assert.AreEqual(SkillTargetMode.DirectionCone, regraph.Targeting.Mode);
            Assert.AreEqual(3, regraph.Targeting.ConeDepth);
        }

        [Test]
        public void SpecCompiler_PlayVisualCueProfilePath_RoundTrips()
        {
            const string profilePath =
                "Assets/Tactics/Arts/PureRun/VFX/PilotoAdapted/Profiles/LightningLv1.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VisualCueProfile>(profilePath);
            Assert.That(profile, Is.Not.Null, $"Missing profile fixture: {profilePath}");
            var spec = new SkillGraphSpec
            {
                DisplayName = "VisualCueRoundTrip",
                Nodes = new List<SkillNodeSpec>
                {
                    new() { Id = "start", Type = "Start" },
                    new()
                    {
                        Id = "cue",
                        Type = "PlayVisualCue",
                        Parameters = new Dictionary<string, object> { ["profilePath"] = profilePath }
                    },
                    new() { Id = "finish", Type = "Finish" }
                },
                Edges = new List<SkillEdgeSpec>
                {
                    new() { Source = "start", Target = "cue" },
                    new() { Source = "cue", Target = "finish" }
                }
            };

            var compileResult = SkillGraphSpecCompiler.Compile(spec);
            Assert.That(compileResult.Success, Is.True,
                $"Compile failed: {string.Join(", ", compileResult.Errors)}");
            var compiledCue = compileResult.Asset.FindNode("cue") as PlayVisualCueNodeRecord;
            Assert.That(compiledCue?.Profile, Is.SameAs(profile));

            SkillGraphSpec exported = SkillGraphSpecCompiler.ExportSpec(compileResult.Asset);
            SkillNodeSpec exportedCue = exported.Nodes.Find(node => node.Id == "cue");
            Assert.That(exportedCue, Is.Not.Null);
            Assert.That(exportedCue.Parameters["profilePath"], Is.EqualTo(profilePath));

            var recompileResult = SkillGraphSpecCompiler.Compile(exported);
            Assert.That(recompileResult.Success, Is.True,
                $"Recompile failed: {string.Join(", ", recompileResult.Errors)}");
            var recompiledCue = recompileResult.Asset.FindNode("cue") as PlayVisualCueNodeRecord;
            Assert.That(recompiledCue?.Profile, Is.SameAs(profile));
        }

        [Test]
        public void SpecCompiler_UnknownNodeType_ReportsUnsupportedError()
        {
            var spec = new SkillGraphSpec
            {
                DisplayName = "BadType",
                Nodes = new List<SkillNodeSpec>
                {
                    new() { Id = "start", Type = "Start" },
                    new() { Id = "bad", Type = "NonExistentNodeType" },
                    new() { Id = "finish", Type = "Finish" }
                },
                Edges = new List<SkillEdgeSpec>
                {
                    new() { Source = "start", Target = "bad" },
                    new() { Source = "bad", Target = "finish" }
                }
            };

            var compileResult = SkillGraphSpecCompiler.Compile(spec);
            Assert.IsFalse(compileResult.Success);
            Assert.That(compileResult.Errors, Has.Some.Matches<SkillGraphDiagnostic>(d => d.Code == SkillGraphValidation.UnsupportedNodeType));
        }

        [Test]
        public void SpecCompiler_WithParameters_AppliesCorrectly()
        {
            var spec = new SkillGraphSpec
            {
                DisplayName = "WithParams",
                Nodes = new List<SkillNodeSpec>
                {
                    new() { Id = "start", Type = "Start" },
                    new() { Id = "target", Type = "SelectPrimaryTarget", Parameters = new Dictionary<string, object> { ["minRange"] = 2, ["maxRange"] = 5 } },
                    new() { Id = "damage", Type = "ApplyDamage", Parameters = new Dictionary<string, object> { ["baseDamage"] = 10f } },
                    new() { Id = "finish", Type = "Finish" }
                },
                Edges = new List<SkillEdgeSpec>
                {
                    new() { Source = "start", Target = "target" },
                    new() { Source = "target", Target = "damage" },
                    new() { Source = "damage", Target = "finish" }
                }
            };

            var compileResult = SkillGraphSpecCompiler.Compile(spec);
            Assert.IsTrue(compileResult.Success);

            var graph = compileResult.Asset;
            var selectNode = graph.FindNode("target") as SelectPrimaryTargetNodeRecord;
            Assert.IsNotNull(selectNode);
            Assert.AreEqual(2, selectNode.MinRange);
            Assert.AreEqual(5, selectNode.MaxRange);

            var damageNode = graph.FindNode("damage") as ApplyDamageNodeRecord;
            Assert.IsNotNull(damageNode);
            Assert.AreEqual(10f, damageNode.BaseDamage);
        }

        private static SkillGraphSpec CreateValidDamageSpec()
        {
            return new SkillGraphSpec
            {
                DisplayName = "TestDamage",
                Nodes = new List<SkillNodeSpec>
                {
                    new() { Id = "start", Type = "Start" },
                    new() { Id = "target", Type = "SelectPrimaryTarget" },
                    new() { Id = "damage", Type = "ApplyDamage" },
                    new() { Id = "finish", Type = "Finish" }
                },
                Edges = new List<SkillEdgeSpec>
                {
                    new() { Source = "start", Target = "target" },
                    new() { Source = "target", Target = "damage" },
                    new() { Source = "damage", Target = "finish" }
                }
            };
        }

        // ── 自动修复测试 ──

        [Test]
        public void AutoFixer_MissingStart_AddsStartNode()
        {
            var spec = new SkillGraphSpec
            {
                DisplayName = "NoStart",
                Nodes = new List<SkillNodeSpec>
                {
                    new() { Id = "damage", Type = "ApplyDamage" },
                    new() { Id = "finish", Type = "Finish" }
                },
                Edges = new List<SkillEdgeSpec>
                {
                    new() { Source = "damage", Target = "finish" }
                }
            };

            var result = SkillGraphSpecAutoFixer.FixSpec(spec);
            Assert.IsTrue(result.AllFixed, $"Not fixed: {string.Join(", ", result.RemainingErrors)}");
            Assert.That(result.FixesApplied, Has.Some.Contains("Start"));
            Assert.That(spec.Nodes, Has.Some.Matches<SkillNodeSpec>(n => n.Type == "Start"));
        }

        [Test]
        public void AutoFixer_MissingFinish_AddsFinishNode()
        {
            var spec = new SkillGraphSpec
            {
                DisplayName = "NoFinish",
                Nodes = new List<SkillNodeSpec>
                {
                    new() { Id = "start", Type = "Start" },
                    new() { Id = "damage", Type = "ApplyDamage" }
                },
                Edges = new List<SkillEdgeSpec>
                {
                    new() { Source = "start", Target = "damage" }
                }
            };

            var result = SkillGraphSpecAutoFixer.FixSpec(spec);
            Assert.IsTrue(result.AllFixed, $"Not fixed: {string.Join(", ", result.RemainingErrors)}");
            Assert.That(result.FixesApplied, Has.Some.Contains("Finish"));
            Assert.That(spec.Nodes, Has.Some.Matches<SkillNodeSpec>(n => n.Type == "Finish"));
        }

        [Test]
        public void AutoFixer_SelfReferencingEdge_RemovesEdge()
        {
            var spec = new SkillGraphSpec
            {
                DisplayName = "SelfRef",
                Nodes = new List<SkillNodeSpec>
                {
                    new() { Id = "start", Type = "Start" },
                    new() { Id = "finish", Type = "Finish" }
                },
                Edges = new List<SkillEdgeSpec>
                {
                    new() { Source = "start", Target = "start" },
                    new() { Source = "start", Target = "finish" }
                }
            };

            var result = SkillGraphSpecAutoFixer.FixSpec(spec);
            Assert.IsTrue(result.AllFixed, $"Not fixed: {string.Join(", ", result.RemainingErrors)}");
            Assert.That(spec.Edges, Has.None.Matches<SkillEdgeSpec>(e => e.Source == "start" && e.Target == "start"));
        }

        [Test]
        public void AutoFixer_ValidSpec_NoChanges()
        {
            var spec = CreateValidDamageSpec();
            var result = SkillGraphSpecAutoFixer.FixSpec(spec);
            Assert.IsTrue(result.AllFixed);
            Assert.IsEmpty(result.FixesApplied);
        }

        [Test]
        public void AutoFixer_FixedSpec_CompilesSuccessfully()
        {
            var spec = new SkillGraphSpec
            {
                DisplayName = "Broken",
                Nodes = new List<SkillNodeSpec>
                {
                    new() { Id = "damage", Type = "ApplyDamage" }
                }
            };

            var fixResult = SkillGraphSpecAutoFixer.FixSpec(spec);
            Assert.IsTrue(fixResult.AllFixed, $"Not fixed: {string.Join(", ", fixResult.RemainingErrors)}");

            var compileResult = SkillGraphSpecCompiler.Compile(spec);
            Assert.IsTrue(compileResult.Success, $"Compile failed: {string.Join(", ", compileResult.Errors)}");
        }
    }
}
