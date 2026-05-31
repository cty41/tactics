using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Tactics.Common.AI.MonsterAI;
using Tactics.Runtime.Utilities;

namespace Tactics.Editor.MonsterAIEditor
{
    /// <summary>
    /// AI 决策图视图，支持 Intent/Rule/Score 独立子节点。
    /// </summary>
    public class AiDecisionGraphView : GraphView
    {
        public event Action<string> OnNodeSelected;

        private AiDecisionGraph _graph;
        private readonly Dictionary<string, GraphNode> _nodeViews = new();
        private readonly Dictionary<string, GraphEdgeRecord> _edgeRecords = new();

        public AiDecisionGraphView()
        {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/Tactics/Arts/UI/AiGraphStyle.uss");
            if (styleSheet != null) styleSheets.Add(styleSheet);

            Insert(0, new GridBackground());
            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            RegisterCallback<ContextualMenuPopulateEvent>(OnContextMenu);
            graphViewChanged = OnGraphViewChanged;
            serializeGraphElements = SerializeGraphElements;
            unserializeAndPaste = UnserializeAndPaste;
            canPasteSerializedData = (_) => true;
        }

        public void LoadGraph(AiDecisionGraph graph)
        {
            _graph = graph;
            DeleteElements(graphElements);
            _nodeViews.Clear();
            _edgeRecords.Clear();

            if (graph == null) return;

            // 恢复所有节点
            foreach (var nodeRecord in graph.Nodes)
            {
                var nodeView = CreateNodeView(nodeRecord);
                AddElement(nodeView);
                _nodeViews[nodeRecord.NodeId] = nodeView;
            }

            // 恢复所有边
            foreach (var edgeRec in graph.Edges)
            {
                if (_nodeViews.TryGetValue(edgeRec.SourceNodeId, out var srcView) &&
                    _nodeViews.TryGetValue(edgeRec.TargetNodeId, out var tgtView))
                {
                    var srcPort = srcView.outputContainer.Q<Port>();
                    var tgtPort = tgtView.inputContainer.Q<Port>();
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

        public void SaveGraph(AiDecisionGraph graph)
        {
            if (graph == null) return;

            // 保存所有节点位置
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

        public void RefreshFromGraph(AiDecisionGraph graph)
        {
            SaveGraph(_graph);
            LoadGraph(graph);
        }

        public void AutoLayoutGraph()
        {
            if (_graph == null) return;

            const float intentX = 60f;
            const float ruleX = 340f;
            const float scoreX = 620f;
            const float orphanX = 900f;
            const float startY = 60f;
            const float intentGapY = 190f;
            const float childGapY = 72f;

            var positionedChildren = new HashSet<string>();
            var intents = _graph.Nodes
                .OfType<IntentNodeRecord>()
                .OrderBy(intent => GetIntentLayoutOrder(intent.IntentType))
                .ThenBy(intent => ParseNodeId(intent.NodeId))
                .ToList();

            for (int i = 0; i < intents.Count; i++)
            {
                var intent = intents[i];
                float intentY = startY + i * intentGapY;
                SetNodePosition(intent, intentX, intentY);

                var children = GetChildNodes(intent.NodeId);
                var rules = children.OfType<RuleNodeRecord>()
                    .OrderBy(rule => GetRuleLayoutOrder(rule.RuleType))
                    .ThenBy(rule => ParseNodeId(rule.NodeId))
                    .ToList();
                var scores = children.OfType<ScoreNodeRecord>()
                    .OrderBy(score => GetScoreLayoutOrder(score.ScoreType))
                    .ThenBy(score => ParseNodeId(score.NodeId))
                    .ToList();

                LayoutChildColumn(rules, ruleX, intentY, childGapY, positionedChildren);
                LayoutChildColumn(scores, scoreX, intentY, childGapY, positionedChildren);
            }

            LayoutOrphanNodes<RuleNodeRecord>(ruleX, orphanX, startY, childGapY, positionedChildren);
            LayoutOrphanNodes<ScoreNodeRecord>(scoreX, orphanX + 240f, startY, childGapY, positionedChildren);

            EditorUtility.SetDirty(_graph);
            LoadGraph(_graph);
        }

        // ── 创建节点视图 ──

        private GraphNode CreateNodeView(GraphNodeRecord record)
        {
            GraphNode nodeView = record switch
            {
                IntentNodeRecord intent => new IntentNodeView(intent.NodeId, intent.IntentType.ToString(), intent.BasePriority),
                RuleNodeRecord rule => new RuleNodeView(rule.NodeId, rule.RuleName, rule.RuleType),
                ScoreNodeRecord score => new ScoreNodeView(score.NodeId, score.ScoreName, score.ScoreType, score.Weight),
                _ => null
            };

            if (nodeView == null) return null;

            ApplyNodeStyleClasses(record, nodeView);
            nodeView.SetPosition(new Rect(record.Position.x, record.Position.y, 200, 120));
            nodeView.OnNodeSelected += (id) => OnNodeSelected?.Invoke(id);
            return nodeView;
        }

        private void ApplyNodeStyleClasses(GraphNodeRecord record, GraphNode nodeView)
        {
            if (!record.Enabled)
                nodeView.AddToClassList("node-disabled");

            if (IsOrphanChild(record))
                nodeView.AddToClassList("node-orphan");

            switch (record)
            {
                case IntentNodeRecord intent:
                    nodeView.AddToClassList(GetIntentStyleClass(intent.IntentType));
                    break;
                case RuleNodeRecord rule:
                    nodeView.AddToClassList(GetRuleStyleClass(rule.RuleType));
                    break;
                case ScoreNodeRecord score:
                    nodeView.AddToClassList(GetScoreStyleClass(score.ScoreType));
                    break;
            }
        }

        private bool IsOrphanChild(GraphNodeRecord record)
        {
            if (record is IntentNodeRecord || _graph == null) return false;
            return !_graph.Edges.Any(edge => edge.TargetNodeId == record.NodeId);
        }

        private GraphNode CreateNodeViewForRecord(GraphNodeRecord record)
        {
            var nodeView = CreateNodeView(record);
            if (nodeView != null)
            {
                _nodeViews[record.NodeId] = nodeView;
                AddElement(nodeView);
            }
            return nodeView;
        }

        // ── 右键菜单 ──

        private void OnContextMenu(ContextualMenuPopulateEvent evt)
        {
            if (_graph == null) return;

            var mousePos = evt.mousePosition;
            // 转换为 content 坐标系
            var graphPos = viewTransform.matrix.inverse.MultiplyPoint(mousePos);

            evt.menu.AppendAction("Add Intent Node", (_) =>
            {
                var record = _graph.AddNode(GraphNodeType.Intent, new Vector2(graphPos.x, graphPos.y));
                CreateNodeViewForRecord(record);
                EditorUtility.SetDirty(_graph);
            });

            evt.menu.AppendAction("Add Rule Node", (_) =>
            {
                var record = _graph.AddNode(GraphNodeType.Rule, new Vector2(graphPos.x, graphPos.y));
                CreateNodeViewForRecord(record);
                EditorUtility.SetDirty(_graph);
            });

            evt.menu.AppendAction("Add Score Node", (_) =>
            {
                var record = _graph.AddNode(GraphNodeType.Score, new Vector2(graphPos.x, graphPos.y));
                CreateNodeViewForRecord(record);
                EditorUtility.SetDirty(_graph);
            });
        }

        // ── 边校验 ──

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            // 删除
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
                    else if (elem is GraphNode nodeView)
                    {
                        _nodeViews.Remove(nodeView.NodeId);
                        _graph.RemoveNode(nodeView.NodeId);
                    }
                }
                if (change.elementsToRemove.Any(e => e is GraphNode || e is Edge))
                    EditorUtility.SetDirty(_graph);
            }

            // 创建边
            if (change.edgesToCreate != null)
            {
                var validEdges = new List<Edge>();
                foreach (var edge in change.edgesToCreate)
                {
                    var srcNode = edge.output.node as GraphNode;
                    var tgtNode = edge.input.node as GraphNode;
                    if (srcNode == null || tgtNode == null) continue;

                    // 校验合法连接
                    if (!IsValidConnection(srcNode, tgtNode))
                    {
                        continue;
                    }

                    var edgeRec = _graph.AddEdge(srcNode.NodeId, tgtNode.NodeId);
                    edge.userData = edgeRec.EdgeId;
                    _edgeRecords[edgeRec.EdgeId] = edgeRec;
                    validEdges.Add(edge);
                }
                change.edgesToCreate = validEdges;
                if (change.edgesToCreate.Count > 0)
                    EditorUtility.SetDirty(_graph);
            }

            return change;
        }

        private bool IsValidConnection(GraphNode source, GraphNode target)
        {
            // Intent -> Rule
            if (source is IntentNodeView && target is RuleNodeView) return true;
            // Intent -> Score
            if (source is IntentNodeView && target is ScoreNodeView) return true;
            // 禁止：Rule->Rule, Score->Score, Rule->Score, Score->Intent, Rule->Intent
            return false;
        }

        private List<GraphNodeRecord> GetChildNodes(string intentNodeId)
        {
            var children = new List<GraphNodeRecord>();
            foreach (var edge in _graph.Edges)
            {
                if (edge.SourceNodeId != intentNodeId) continue;

                var child = _graph.FindNode(edge.TargetNodeId);
                if (child != null)
                    children.Add(child);
            }
            return children;
        }

        private void LayoutChildColumn<T>(
            List<T> children,
            float x,
            float parentY,
            float gapY,
            HashSet<string> positionedChildren) where T : GraphNodeRecord
        {
            float firstY = parentY - (children.Count - 1) * gapY * 0.5f;

            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (!positionedChildren.Add(child.NodeId)) continue;

                SetNodePosition(child, x, firstY + i * gapY);
            }
        }

        private void LayoutOrphanNodes<T>(
            float fallbackX,
            float orphanX,
            float startY,
            float gapY,
            HashSet<string> positionedChildren) where T : GraphNodeRecord
        {
            var orphans = _graph.Nodes
                .OfType<T>()
                .Where(node => !positionedChildren.Contains(node.NodeId))
                .OrderBy(node => ParseNodeId(node.NodeId))
                .ToList();

            float x = orphans.Count > 0 ? orphanX : fallbackX;
            for (int i = 0; i < orphans.Count; i++)
            {
                SetNodePosition(orphans[i], x, startY + i * gapY);
            }
        }

        private static void SetNodePosition(GraphNodeRecord record, float x, float y)
        {
            record.Position = new Vector2(x, y);
        }

        private static int GetIntentLayoutOrder(IntentType intentType)
        {
            return intentType switch
            {
                IntentType.FinishOff => 0,
                IntentType.BasicAttack => 1,
                IntentType.Engage => 2,
                IntentType.AbilityUse => 3,
                IntentType.Retreat => 4,
                IntentType.HoldPosition => 5,
                _ => 99
            };
        }

        private static int GetRuleLayoutOrder(RuleType ruleType)
        {
            return ruleType switch
            {
                RuleType.TargetInRange => 0,
                RuleType.TargetInMoveAttackRange => 1,
                RuleType.TargetKillable => 2,
                RuleType.HealthBelowThreshold => 3,
                RuleType.DestinationSafe => 4,
                _ => 99
            };
        }

        private static int GetScoreLayoutOrder(ScoreType scoreType)
        {
            return scoreType switch
            {
                ScoreType.KillPotential => 0,
                ScoreType.DistanceToTarget => 1,
                ScoreType.TargetHealth => 2,
                ScoreType.PositionSafety => 3,
                _ => 99
            };
        }

        private static int ParseNodeId(string nodeId)
        {
            return int.TryParse(nodeId, out int id) ? id : int.MaxValue;
        }

        private static string GetIntentStyleClass(IntentType intentType)
        {
            return intentType switch
            {
                IntentType.FinishOff => "intent-finish-off",
                IntentType.BasicAttack => "intent-basic-attack",
                IntentType.Engage => "intent-engage",
                IntentType.AbilityUse => "intent-ability-use",
                IntentType.Retreat => "intent-retreat",
                IntentType.HoldPosition => "intent-hold-position",
                _ => "intent-generic"
            };
        }

        private static string GetRuleStyleClass(RuleType ruleType)
        {
            return ruleType switch
            {
                RuleType.TargetInRange => "rule-target",
                RuleType.TargetInMoveAttackRange => "rule-target",
                RuleType.TargetKillable => "rule-target",
                RuleType.HealthAboveThreshold => "rule-health",
                RuleType.HealthBelowThreshold => "rule-health",
                RuleType.DestinationSafe => "rule-safety",
                RuleType.HasAvailableAbility => "rule-ability",
                RuleType.HasAbilityTag => "rule-ability",
                RuleType.HasDamageAbility => "rule-ability",
                RuleType.HasHealAbility => "rule-ability",
                RuleType.HasControlAbility => "rule-ability",
                RuleType.HasAOEAbility => "rule-ability",
                RuleType.TargetNeedsHealing => "rule-health",
                RuleType.MultiTargetOpportunity => "rule-ability",
                _ => "rule-utility"
            };
        }

        private static string GetScoreStyleClass(ScoreType scoreType)
        {
            return scoreType switch
            {
                ScoreType.DistanceToTarget => "score-position",
                ScoreType.PositionSafety => "score-position",
                ScoreType.AllyProximity => "score-position",
                ScoreType.TargetHealth => "score-health",
                ScoreType.SelfHealth => "score-health",
                ScoreType.KillPotential => "score-offense",
                ScoreType.TargetValue => "score-offense",
                ScoreType.AbilityEffectiveness => "score-ability",
                ScoreType.AOEValue => "score-ability",
                ScoreType.HealUrgency => "score-health",
                ScoreType.ControlValue => "score-ability",
                ScoreType.BuffUtility => "score-utility",
                ScoreType.DebuffUtility => "score-utility",
                _ => "score-utility"
            };
        }

        // ── 端口兼容 ──

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return ports.ToList()
                .Where(p => p != startPort && p.node != startPort.node)
                .ToList();
        }

        // ── 序列化（用于复制粘贴） ──

        private string SerializeGraphElements(IEnumerable<GraphElement> elements)
        {
            return "";
        }

        private void UnserializeAndPaste(string operationName, string data) { }

        // ═══════════════════════════════════════════
        //  节点视图基类
        // ═══════════════════════════════════════════

        public abstract class GraphNode : Node
        {
            public event Action<string> OnNodeSelected;
            public string NodeId { get; }

            protected Port InputPort;
            protected Port OutputPort;

            protected GraphNode(string nodeId, string title, string cssClass)
            {
                NodeId = nodeId;
                this.title = title;
                AddToClassList(cssClass);
            }

            protected void CreatePorts(bool hasInput, bool hasOutput)
            {
                if (hasInput)
                {
                    InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(bool));
                    InputPort.portName = "";
                    inputContainer.Add(InputPort);
                }
                if (hasOutput)
                {
                    OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
                    OutputPort.portName = "";
                    outputContainer.Add(OutputPort);
                }
                RefreshExpandedState();
                RefreshPorts();
            }

            protected void AddInfoLine(string text)
            {
                var label = new Label(text);
                label.style.fontSize = 9;
                label.style.color = new Color(0.65f, 0.65f, 0.65f);
                label.style.whiteSpace = WhiteSpace.Normal;
                mainContainer.Add(label);
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

        // ═══════════════════════════════════════════
        //  IntentNodeView
        // ═══════════════════════════════════════════

        public class IntentNodeView : GraphNode
        {
            private readonly Label _priorityLabel;

            public IntentNodeView(string nodeId, string title, float priority)
                : base(nodeId, title, "intent-node")
            {
                CreatePorts(false, true);
                _priorityLabel = new Label($"P:{priority:F0}");
                _priorityLabel.style.fontSize = 9;
                _priorityLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                mainContainer.Add(_priorityLabel);
            }

            public void UpdatePriority(float priority)
            {
                _priorityLabel.text = $"P:{priority:F0}";
            }
        }

        // ═══════════════════════════════════════════
        //  RuleNodeView
        // ═══════════════════════════════════════════

        public class RuleNodeView : GraphNode
        {
            private readonly Label _typeLabel;

            public RuleNodeView(string nodeId, string ruleName, RuleType ruleType)
                : base(nodeId, string.IsNullOrEmpty(ruleName) ? ruleType.ToString() : ruleName, "rule-node")
            {
                CreatePorts(true, false);
                _typeLabel = new Label(ruleType.ToString());
                _typeLabel.style.fontSize = 9;
                _typeLabel.style.color = new Color(0.5f, 0.8f, 0.5f);
                mainContainer.Add(_typeLabel);
            }
        }

        // ═══════════════════════════════════════════
        //  ScoreNodeView
        // ═══════════════════════════════════════════

        public class ScoreNodeView : GraphNode
        {
            private readonly Label _weightLabel;

            public ScoreNodeView(string nodeId, string scoreName, ScoreType scoreType, float weight)
                : base(nodeId, string.IsNullOrEmpty(scoreName) ? scoreType.ToString() : scoreName, "score-node")
            {
                CreatePorts(true, false);
                _weightLabel = new Label($"w:{weight:F1}");
                _weightLabel.style.fontSize = 9;
                _weightLabel.style.color = new Color(0.8f, 0.7f, 0.3f);
                mainContainer.Add(_weightLabel);
            }

            public void UpdateWeight(float weight)
            {
                _weightLabel.text = $"w:{weight:F1}";
            }
        }
    }
}
