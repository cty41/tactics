using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Tactics.Common.Skills.Graph;
using Tactics.Runtime.Utilities;

namespace Tactics.Editor.SkillGraphEditor
{
    /// <summary>
    /// 技能图视图，支持 Phase 1 全部节点类型。
    /// </summary>
    public class SkillGraphView : GraphView
    {
        public event Action<string> OnNodeSelected;

        private SkillGraphAsset _graph;
        private readonly Dictionary<string, SkillGraphNode> _nodeViews = new();
        private readonly Dictionary<string, SkillGraphEdgeRecord> _edgeRecords = new();

        public SkillGraphView()
        {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/Tactics/Arts/UI/SkillGraphStyle.uss");
            if (styleSheet != null) styleSheets.Add(styleSheet);

            Insert(0, new GridBackground());
            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            RegisterCallback<ContextualMenuPopulateEvent>(OnContextMenu);
            graphViewChanged = OnGraphViewChanged;
        }

        // ═══════════════════════════════════════════
        //  Load / Save / Refresh
        // ═══════════════════════════════════════════

        public void LoadGraph(SkillGraphAsset graph)
        {
            _graph = graph;
            DeleteElements(graphElements);
            _nodeViews.Clear();
            _edgeRecords.Clear();

            if (graph == null) return;

            foreach (var nodeRecord in graph.Nodes)
            {
                var nodeView = CreateNodeView(nodeRecord);
                if (nodeView != null)
                {
                    AddElement(nodeView);
                    _nodeViews[nodeRecord.NodeId] = nodeView;
                }
            }

            foreach (var edgeRec in graph.Edges)
            {
                if (_nodeViews.TryGetValue(edgeRec.SourceNodeId, out var srcView) &&
                    _nodeViews.TryGetValue(edgeRec.TargetNodeId, out var tgtView))
                {
                    var srcPort = FindOutputPort(srcView, edgeRec.PortType);
                    var tgtPort = tgtView.InputPort;
                    if (srcPort != null && tgtPort != null)
                    {
                        var edge = srcPort.ConnectTo(tgtPort);
                        edge.userData = edgeRec.EdgeId;
                        AddElement(edge);
                        _edgeRecords[edgeRec.EdgeId] = edgeRec;
                    }
                }
            }
        }

        public void SaveGraph(SkillGraphAsset graph)
        {
            if (graph == null) return;

            foreach (var kvp in _nodeViews)
            {
                var record = graph.FindNode(kvp.Key);
                if (record != null)
                {
                    var rect = kvp.Value.GetPosition();
                    record.Position = new Vector2(rect.x, rect.y);
                }
            }
            EditorUtility.SetDirty(graph);
        }

        public void RefreshFromGraph(SkillGraphAsset graph)
        {
            SaveGraph(_graph);
            LoadGraph(graph);
        }

        // ═══════════════════════════════════════════
        //  Auto Layout
        // ═══════════════════════════════════════════

        public void AutoLayoutGraph()
        {
            if (_graph == null) return;

            const float startX = 60f;
            const float startY = 60f;
            const float gapX = 280f;
            const float gapY = 100f;

            var entry = _graph.FindEntryNode();
            if (entry == null) return;

            var visited = new HashSet<string>();
            var queue = new Queue<(string nodeId, int depth, int index)>();
            queue.Enqueue((entry.NodeId, 0, 0));
            visited.Add(entry.NodeId);

            while (queue.Count > 0)
            {
                var (nodeId, depth, index) = queue.Dequeue();
                var record = _graph.FindNode(nodeId);
                if (record == null) continue;

                float x = startX + depth * gapX;
                float y = startY + index * gapY;
                record.Position = new Vector2(x, y);

                var children = _graph.GetEdgesFrom(nodeId);
                int childIndex = 0;
                for (int i = 0; i < children.Count; i++)
                {
                    var childId = children[i].TargetNodeId;
                    if (visited.Add(childId))
                    {
                        queue.Enqueue((childId, depth + 1, index + childIndex));
                        childIndex++;
                    }
                }
            }

            EditorUtility.SetDirty(_graph);
            LoadGraph(_graph);
        }

        // ═══════════════════════════════════════════
        //  Create Node Views
        // ═══════════════════════════════════════════

        private SkillGraphNode CreateNodeView(SkillGraphNodeRecord record)
        {
            SkillGraphNode nodeView = record switch
            {
                StartNodeRecord r => new SkillGraphNode(r.NodeId, "Start", "node-start",
                    hasInput: false, hasOutput: true),
                SelectPrimaryTargetNodeRecord r => new SkillGraphNode(r.NodeId,
                    "Select Primary Target", "node-target",
                    hasInput: true, hasOutput: true,
                    info: $"Range: {r.MaxRange}"),
                SelectTargetPointNodeRecord r => new SkillGraphNode(r.NodeId,
                    "Select Target Point", "node-target",
                    hasInput: true, hasOutput: true,
                    info: $"Range: {r.MaxRange}"),
                CollectTargetsInAreaNodeRecord r => new SkillGraphNode(r.NodeId,
                    "Collect Targets In Area", "node-target",
                    hasInput: true, hasOutput: true,
                    info: $"Radius: {r.Radius}, Shape: {r.Shape}"),
                ForEachTargetNodeRecord r => new SkillGraphNode(r.NodeId,
                    "For Each Target", "node-flow",
                    hasInput: true, hasOutput: true,
                    namedOutputs: new[] { ("Loop", SkillGraphPortType.Default), ("Complete", SkillGraphPortType.OnComplete) }),
                DashToTargetNodeRecord r => new SkillGraphNode(r.NodeId,
                    "Dash To Target", "node-movement",
                    hasInput: true, hasOutput: true,
                    info: $"Range: {r.MaxRange}, Dmg: {r.CollisionDamage}"),
                ApplyDamageNodeRecord r => new SkillGraphNode(r.NodeId,
                    "Apply Damage", "node-effect",
                    hasInput: true, hasOutput: false,
                    info: $"Dmg: {r.BaseDamage}, Type: {r.DamageType}" + (r.AccuracyPenalty > 0 ? $", AccPenalty: {r.AccuracyPenalty}" : "")),
                ApplyKnockbackNodeRecord r => new SkillGraphNode(r.NodeId,
                    "Apply Knockback", "node-effect",
                    hasInput: true, hasOutput: false,
                    info: $"Dist: {r.Distance}"),
                BranchNodeRecord r => new SkillGraphNode(r.NodeId,
                    "Branch", "node-flow",
                    hasInput: true, hasOutput: true,
                    namedOutputs: new[] { ("OnTrue", SkillGraphPortType.OnTrue), ("OnFalse", SkillGraphPortType.OnFalse) }),
                FinishNodeRecord r => new SkillGraphNode(r.NodeId, "Finish", "node-terminal",
                    hasInput: true, hasOutput: false),
                FailNodeRecord r => new SkillGraphNode(r.NodeId, "Fail", "node-terminal",
                    hasInput: true, hasOutput: false),
                ProjectileLaunchNodeRecord r => new SkillGraphNode(r.NodeId,
                    "Projectile Launch", "node-movement",
                    hasInput: true, hasOutput: true,
                    info: $"Travel: {r.TravelTime}s, Speed: {r.Speed}"),
                OnHitNodeRecord r => new SkillGraphNode(r.NodeId,
                    "On Hit", "node-effect",
                    hasInput: true, hasOutput: true),
                ApplyBuffNodeRecord r => new SkillGraphNode(r.NodeId,
                    "Apply Buff", "node-effect",
                    hasInput: true, hasOutput: false,
                    info: $"Buff: {r.BuffConfig?.BuffName ?? "null"}, Dur: {r.Duration}"),
                SelectSelfNodeRecord r => new SkillGraphNode(r.NodeId,
                    "Select Self", "node-target",
                    hasInput: true, hasOutput: true),
                SelectAllyNodeRecord r => new SkillGraphNode(r.NodeId,
                    "Select Ally", "node-target",
                    hasInput: true, hasOutput: true,
                    info: $"Range: {r.MaxRange}, Self: {r.IncludeSelf}"),
                ApplyHealNodeRecord r => new SkillGraphNode(r.NodeId,
                    "Apply Heal", "node-effect",
                    hasInput: true, hasOutput: false,
                    info: $"Heal: {r.HealAmount}"),
                ApplyManaNodeRecord r => new SkillGraphNode(r.NodeId,
                    "Apply Mana", "node-effect",
                    hasInput: true, hasOutput: false,
                    info: $"Mana: {r.ManaAmount}"),
                RemoveHarmfulBuffsNodeRecord r => new SkillGraphNode(r.NodeId,
                    "Remove Harmful Buffs", "node-effect",
                    hasInput: true, hasOutput: false),
                DashToAllyNodeRecord r => new SkillGraphNode(r.NodeId,
                    "Dash To Ally", "node-movement",
                    hasInput: true, hasOutput: true,
                    info: $"Range: {r.MaxRange}"),
                LaunchUnitNodeRecord r => new SkillGraphNode(r.NodeId,
                    "Launch Unit", "node-movement",
                    hasInput: true, hasOutput: true,
                    info: $"Dist: {r.LaunchDistance}, Dmg: {r.LandingDamage}, H: {r.FlightHeight}"),
                SelectMoveDestinationNodeRecord r => new SkillGraphNode(r.NodeId,
                    "Select Move Destination", "node-target",
                    hasInput: true, hasOutput: true,
                    info: $"RespectRules: {r.RespectMovementRules}"),
                ExecuteMoveNodeRecord r => new SkillGraphNode(r.NodeId,
                    "Execute Move", "node-movement",
                    hasInput: true, hasOutput: true,
                    info: $"ConsumeMP: {r.ConsumeMovementPoints}, MarkUsed: {r.MarkAsBasicAbilityUsed}"),
                _ => null
            };

            if (nodeView == null) return null;

            if (!record.Enabled)
                nodeView.AddToClassList("node-disabled");

            nodeView.SetPosition(new Rect(record.Position.x, record.Position.y, 200, 120));
            nodeView.OnNodeSelected += (id) => OnNodeSelected?.Invoke(id);
            return nodeView;
        }

        private SkillGraphNode CreateNodeViewForRecord(SkillGraphNodeRecord record)
        {
            var nodeView = CreateNodeView(record);
            if (nodeView != null)
            {
                _nodeViews[record.NodeId] = nodeView;
                AddElement(nodeView);
            }
            return nodeView;
        }

        // ═══════════════════════════════════════════
        //  Port Helpers
        // ═══════════════════════════════════════════

        private Port FindOutputPort(SkillGraphNode nodeView, SkillGraphPortType portType)
        {
            if (portType == SkillGraphPortType.Default)
                return nodeView.DefaultOutputPort;

            foreach (var kvp in nodeView.NamedOutputPorts)
            {
                if (kvp.Key == portType)
                    return kvp.Value;
            }

            return nodeView.DefaultOutputPort;
        }

        // ═══════════════════════════════════════════
        //  Context Menu
        // ═══════════════════════════════════════════

        private void OnContextMenu(ContextualMenuPopulateEvent evt)
        {
            if (_graph == null) return;

            var mousePos = evt.mousePosition;
            var graphPos = viewTransform.matrix.inverse.MultiplyPoint(mousePos);

            var nodeTypes = new[]
            {
                ("Start", SkillGraphNodeType.Start),
                ("Select Primary Target", SkillGraphNodeType.SelectPrimaryTarget),
                ("Select Target Point", SkillGraphNodeType.SelectTargetPoint),
                ("Collect Targets In Area", SkillGraphNodeType.CollectTargetsInArea),
                ("For Each Target", SkillGraphNodeType.ForEachTarget),
                ("Dash To Target", SkillGraphNodeType.DashToTarget),
                ("Apply Damage", SkillGraphNodeType.ApplyDamage),
                ("Apply Knockback", SkillGraphNodeType.ApplyKnockback),
                ("Remove Harmful Buffs", SkillGraphNodeType.RemoveHarmfulBuffs),
                ("Branch", SkillGraphNodeType.Branch),
                ("Finish", SkillGraphNodeType.Finish),
                ("Fail", SkillGraphNodeType.Fail),
            };

            foreach (var (label, type) in nodeTypes)
            {
                var capturedType = type;
                evt.menu.AppendAction($"Add {label}", (_) =>
                {
                    var record = _graph.AddNode(capturedType, new Vector2(graphPos.x, graphPos.y));
                    CreateNodeViewForRecord(record);
                    EditorUtility.SetDirty(_graph);
                });
            }
        }

        // ═══════════════════════════════════════════
        //  Graph Change Handler
        // ═══════════════════════════════════════════

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (change.elementsToRemove != null)
            {
                foreach (var elem in change.elementsToRemove)
                {
                    if (elem is Edge edge)
                    {
                        string edgeId = edge.userData as string;
                        if (!string.IsNullOrEmpty(edgeId))
                        {
                            _graph.RemoveEdge(edgeId);
                            _edgeRecords.Remove(edgeId);
                        }
                    }
                    else if (elem is SkillGraphNode nodeView)
                    {
                        _nodeViews.Remove(nodeView.NodeId);
                        _graph.RemoveNode(nodeView.NodeId);
                    }
                }
                if (change.elementsToRemove.Any(e => e is SkillGraphNode || e is Edge))
                    EditorUtility.SetDirty(_graph);
            }

            if (change.edgesToCreate != null)
            {
                var validEdges = new List<Edge>();
                foreach (var edge in change.edgesToCreate)
                {
                    var srcNode = edge.output.node as SkillGraphNode;
                    var tgtNode = edge.input.node as SkillGraphNode;
                    if (srcNode == null || tgtNode == null) continue;
                    if (srcNode.NodeId == tgtNode.NodeId) continue;

                    var portType = SkillGraphPortType.Default;
                    if (srcNode.NamedOutputPorts.ContainsValue(edge.output))
                    {
                        foreach (var kvp in srcNode.NamedOutputPorts)
                        {
                            if (kvp.Value == edge.output)
                            {
                                portType = kvp.Key;
                                break;
                            }
                        }
                    }

                    var edgeRec = _graph.AddEdge(srcNode.NodeId, tgtNode.NodeId, portType);
                    if (edgeRec != null)
                    {
                        edge.userData = edgeRec.EdgeId;
                        _edgeRecords[edgeRec.EdgeId] = edgeRec;
                        validEdges.Add(edge);
                    }
                }
                change.edgesToCreate = validEdges;
                if (change.edgesToCreate.Count > 0)
                    EditorUtility.SetDirty(_graph);
            }

            return change;
        }

        // ═══════════════════════════════════════════
        //  Node View
        // ═══════════════════════════════════════════

        public class SkillGraphNode : Node
        {
            public event Action<string> OnNodeSelected;
            public string NodeId { get; }

            public Port InputPort { get; private set; }
            public Port DefaultOutputPort { get; private set; }
            public Dictionary<SkillGraphPortType, Port> NamedOutputPorts { get; } = new();

            public SkillGraphNode(string nodeId, string title, string cssClass,
                bool hasInput, bool hasOutput,
                string info = null,
                (string label, SkillGraphPortType portType)[] namedOutputs = null)
            {
                NodeId = nodeId;
                this.title = title;
                AddToClassList(cssClass);

                if (hasInput)
                {
                    InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
                    InputPort.portName = "";
                    inputContainer.Add(InputPort);
                }

                if (namedOutputs != null && namedOutputs.Length > 0)
                {
                    foreach (var (label, portType) in namedOutputs)
                    {
                        var port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
                        port.portName = label;
                        port.userData = portType;
                        outputContainer.Add(port);
                        NamedOutputPorts[portType] = port;
                    }
                }
                else if (hasOutput)
                {
                    DefaultOutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
                    DefaultOutputPort.portName = "";
                    outputContainer.Add(DefaultOutputPort);
                }

                if (!string.IsNullOrEmpty(info))
                {
                    var label = new Label(info);
                    label.style.fontSize = 9;
                    label.style.color = new Color(0.65f, 0.65f, 0.65f);
                    label.style.whiteSpace = WhiteSpace.Normal;
                    mainContainer.Add(label);
                }

                RefreshExpandedState();
                RefreshPorts();
            }

            public override void OnSelected()
            {
                base.OnSelected();
                AddToClassList("node-selected");
                OnNodeSelected?.Invoke(NodeId);
            }

            public override void OnUnselected()
            {
                base.OnUnselected();
                RemoveFromClassList("node-selected");
            }
        }
    }
}
