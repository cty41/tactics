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

        // ── 创建节点视图 ──

        private GraphNode CreateNodeView(GraphNodeRecord record)
        {
            GraphNode nodeView = record switch
            {
                IntentNodeRecord intent => new IntentNodeView(intent.NodeId, intent.IntentType.ToString(), intent.BasePriority),
                RuleNodeRecord rule => new RuleNodeView(rule.NodeId, rule.RuleName, rule.RuleType),
                ScoreNodeRecord score => new ScoreNodeView(score.NodeId, score.ScoreName, score.ScoreType),
                _ => null
            };

            if (nodeView == null) return null;

            nodeView.SetPosition(new Rect(record.Position.x, record.Position.y, 200, 120));
            nodeView.OnNodeSelected += (id) => OnNodeSelected?.Invoke(id);
            return nodeView;
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
                foreach (var edge in change.edgesToCreate)
                {
                    var srcNode = edge.output.node as GraphNode;
                    var tgtNode = edge.input.node as GraphNode;
                    if (srcNode == null || tgtNode == null) continue;

                    // 校验合法连接
                    if (!IsValidConnection(srcNode, tgtNode))
                    {
                        change.edgesToCreate.Remove(edge);
                        continue;
                    }

                    var edgeRec = _graph.AddEdge(srcNode.NodeId, tgtNode.NodeId);
                    edge.userData = edgeRec.EdgeId;
                    _edgeRecords[edgeRec.EdgeId] = edgeRec;
                }
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
                OnNodeSelected?.Invoke(NodeId);
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

            public ScoreNodeView(string nodeId, string scoreName, ScoreType scoreType)
                : base(nodeId, string.IsNullOrEmpty(scoreName) ? scoreType.ToString() : scoreName, "score-node")
            {
                CreatePorts(true, false);
                _weightLabel = new Label($"w:1.0");
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
