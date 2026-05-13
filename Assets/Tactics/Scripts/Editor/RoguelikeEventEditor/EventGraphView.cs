using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.Editor.RoguelikeEventEditor
{
    /// <summary>
    /// 中央节点图画布。基于便携 GraphView 思路实现的节点编辑器。
    /// 为了兼容性不使用 Experimental.GraphView，改用纯 UI Toolkit 实现。
    /// </summary>
    public class EventGraphView : VisualElement
    {
        // ── 缩放 / 平移 ──────────────────────────
        private VisualElement _canvasContainer;
        private VisualElement _canvas;
        private float _zoom = 1f;
        private Vector2 _panOffset = Vector2.zero;
        private Vector2 _lastMousePos;
        private bool _isPanning;
        private bool _isCreatingConnection;
        private ConnectionPreview _connectionPreview;

        // ── 节点数据 ──────────────────────────────
        private SerializableEventData _currentEvent;
        private readonly List<EventNodeElement> _nodes = new();
        private readonly List<ConnectionElement> _connections = new();

        // ── 当前正在编辑的待连接端口 ────────────────
        private PortElement _pendingPort;

        // ── 回调 ──────────────────────────────────
        public event Action<EventNodeElement> OnNodeSelected;
        public event Action OnGraphChanged;

        public EventGraphView()
        {
            style.flexGrow = 1;
            style.backgroundColor = new Color(0.14f, 0.14f, 0.16f);
            style.overflow = Overflow.Hidden;

            BuildUI();
            RegisterCallbacks();
        }

        private void BuildUI()
        {
            _canvasContainer = new VisualElement { style = { flexGrow = 1, overflow = Overflow.Hidden } };
            Add(_canvasContainer);

            _canvas = new VisualElement { style = { position = Position.Absolute } };
            _canvas.name = "canvas";
            _canvasContainer.Add(_canvas);

            // 节点工具栏
            var toolbar = new VisualElement
            {
                style =
                {
                    position = Position.Absolute, top = 8, right = 8,
                    flexDirection = FlexDirection.Column, width = 120,
                }
            };
            AddNodeButton(toolbar, "+ Start", () => AddNodeToCanvas(EventNodeTypes.Start, 100, 100));
            AddNodeButton(toolbar, "+ Option", () => AddNodeToCanvas(EventNodeTypes.Option, 100, 250));
            AddNodeButton(toolbar, "+ Check", () => AddNodeToCanvas(EventNodeTypes.Check, 100, 400));
            AddNodeButton(toolbar, "+ Success", () => AddNodeToCanvas(EventNodeTypes.Success, 100, 550));
            AddNodeButton(toolbar, "+ Failure", () => AddNodeToCanvas(EventNodeTypes.Failure, 100, 650));
            AddNodeButton(toolbar, "+ End", () => AddNodeToCanvas(EventNodeTypes.End, 100, 800));
            Add(toolbar);
        }

        private void AddNodeButton(VisualElement parent, string label, Action action)
        {
            var btn = new Button(action) { text = label };
            btn.style.fontSize = 11;
            btn.style.marginBottom = 2;
            parent.Add(btn);
        }

        private void RegisterCallbacks()
        {
            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseUpEvent>(OnMouseUp);
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<GeometryChangedEvent>(_ => UpdateCanvasTransform());
        }

        // ── 鼠标事件 ──────────────────────────────
        private void OnMouseDown(MouseDownEvent e)
        {
            if (e.button == 2 || (e.button == 0 && e.ctrlKey))
            {
                _isPanning = true;
                _lastMousePos = e.mousePosition;
                this.CaptureMouse();
                e.StopPropagation();
            }
        }

        private void OnMouseMove(MouseMoveEvent e)
        {
            if (_isPanning)
            {
                var delta = e.mousePosition - _lastMousePos;
                _panOffset += delta;
                _lastMousePos = e.mousePosition;
                UpdateCanvasTransform();
            }
        }

        private void OnMouseUp(MouseUpEvent e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                this.ReleaseMouse();
            }
        }

        private void OnWheel(WheelEvent e)
        {
            if (e.ctrlKey)
            {
                _zoom = Mathf.Clamp(_zoom - e.delta.y * 0.05f, 0.2f, 3f);
                UpdateCanvasTransform();
                e.StopPropagation();
            }
        }

        // ── 画布变换 ──────────────────────────────
        private void UpdateCanvasTransform()
        {
            if (_canvas == null) return;
            _canvas.style.translate = new Translate(_panOffset.x, _panOffset.y);
            _canvas.style.scale = new Scale(new Vector3(_zoom, _zoom, 1));
            _canvas.style.transformOrigin = new TransformOrigin(new Length(0), new Length(0));
        }

        // ── 节点操作 ──────────────────────────────
        public EventNodeElement AddNodeToCanvas(string nodeType, float x = 100, float y = 100, EventNodeData data = null)
        {
            var node = new EventNodeElement(nodeType, data);
            node.style.position = Position.Absolute;
            node.style.left = x;
            node.style.top = y;
            node.OnPortClicked += HandlePortClick;
            node.OnNodeClicked += HandleNodeClick;
            node.OnNodeMoving += () => RedrawConnectionsForNode(node);
            node.OnNodeMoved += () => { RedrawConnectionsForNode(node); OnGraphChanged?.Invoke(); };

            _canvas.Add(node);
            _nodes.Add(node);
            OnGraphChanged?.Invoke();
            return node;
        }

        private void HandlePortClick(PortElement port)
        {
            if (_pendingPort == null)
            {
                _pendingPort = port;
                port.SetHighlight(true);
            }
            else
            {
                CreateConnection(_pendingPort, port);
                _pendingPort.SetHighlight(false);
                _pendingPort = null;
            }
        }

        private void HandleNodeClick(EventNodeElement node)
        {
            OnNodeSelected?.Invoke(node);
            // 取消连接操作
            if (_pendingPort != null)
            {
                _pendingPort.SetHighlight(false);
                _pendingPort = null;
            }
        }

        private void CreateConnection(PortElement from, PortElement to)
        {
            if (from.IsInput == to.IsInput) return;
            if (from.ParentNode == to.ParentNode) return;

            // 移除旧连接
            RemoveConnectionForPort(from);
            RemoveConnectionForPort(to);

            var conn = new ConnectionElement(from, to);
            _canvas.Insert(0, conn); // 放在节点后面
            _connections.Add(conn);
            OnGraphChanged?.Invoke();
        }

        private void RemoveConnectionForPort(PortElement port)
        {
            _connections.RemoveAll(c =>
            {
                if (c.From == port || c.To == port)
                {
                    c.RemoveFromHierarchy();
                    return true;
                }
                return false;
            });
        }

        // ── 删除节点 ──────────────────────────────
        public void DeleteSelectedNodes()
        {
            // 简单实现：待扩展
        }

        /// <summary>
        /// 节点移动时实时刷新所有关联连线。
        /// </summary>
        private void RedrawConnectionsForNode(EventNodeElement node)
        {
            foreach (var conn in _connections)
            {
                if (conn.From?.ParentNode == node || conn.To?.ParentNode == node)
                {
                    conn.MarkDirtyRepaint();
                }
            }
        }

        // ── 图 ↔ 数据 ────────────────────────────
        public SerializableEventData BuildEventData()
        {
            var data = _currentEvent ?? new SerializableEventData();
            data.nodes = new List<EventNodeData>();
            data.connections = new List<EventConnectionData>();

            foreach (var node in _nodes)
            {
                data.nodes.Add(node.ToNodeData());
            }
            foreach (var conn in _connections)
            {
                var fromId = conn.From?.ParentNode?.NodeId;
                var toId = conn.To?.ParentNode?.NodeId;
                if (!string.IsNullOrEmpty(fromId) && !string.IsNullOrEmpty(toId))
                    data.connections.Add(new EventConnectionData { from = fromId, to = toId, port = conn.From.IsInput ? "in" : "out" });
            }

            if (data.nodes.Count > 0)
            {
                var startNode = data.nodes.Find(n => n.type == EventNodeTypes.Start);
                if (startNode?.data != null)
                {
                    data.eventId = startNode.data.eventId ?? data.eventId;
                    data.title = startNode.data.title ?? data.title;
                    data.region = startNode.data.region ?? data.region;
                }
            }

            return data;
        }

        public void LoadEvent(SerializableEventData data)
        {
            ClearCanvas();
            _currentEvent = data;
            if (data == null) return;

            var nodeMap = new Dictionary<string, EventNodeElement>();

            foreach (var nd in data.nodes)
            {
                var node = AddNodeToCanvas(nd.type,
                    nd.position?.x ?? 100, nd.position?.y ?? 100, nd);
                nodeMap[nd.nodeId] = node;
            }

            foreach (var conn in data.connections)
            {
                if (nodeMap.TryGetValue(conn.from, out var from) &&
                    nodeMap.TryGetValue(conn.to, out var to))
                {
                    var outPort = conn.port == "in" ? from.InputPort : from.OutputPort;
                    var inPort = conn.port == "in" ? to.OutputPort : to.InputPort;
                    if (outPort != null && inPort != null)
                    {
                        var c = new ConnectionElement(outPort, inPort);
                        _canvas.Insert(0, c);
                        _connections.Add(c);
                    }
                }
            }
        }

        public void ClearCanvas()
        {
            foreach (var node in _nodes) node.RemoveFromHierarchy();
            foreach (var conn in _connections) conn.RemoveFromHierarchy();
            _nodes.Clear();
            _connections.Clear();
            _currentEvent = null;
        }

        public bool HasPendingEvent => _nodes.Count > 0;
    }

    // ═══════════════════════════════════════════════
    //  EventNodeElement  —  画布上单个节点的可视化
    // ═══════════════════════════════════════════════
    public class EventNodeElement : VisualElement
    {
        public string NodeId { get; set; }
        public string NodeType { get; private set; }
        public EventNodePayload Data { get; set; }
        public PortElement InputPort { get; private set; }
        public PortElement OutputPort { get; private set; }

        public event Action<PortElement> OnPortClicked;
        public event Action<EventNodeElement> OnNodeClicked;
        public event Action OnNodeMoved;
        public event Action OnNodeMoving;  // 实时拖动中触发

        private Label _titleLabel;
        private Label _subtitleLabel;
        private Vector2 _dragStart;
        private bool _isDragging;
        private Vector2 _startPos;

        private static readonly Dictionary<string, Color> TypeColors = new()
        {
            [EventNodeTypes.Start] = new Color(0.15f, 0.45f, 0.15f),
            [EventNodeTypes.Option] = new Color(0.2f, 0.3f, 0.55f),
            [EventNodeTypes.Check] = new Color(0.45f, 0.3f, 0.15f),
            [EventNodeTypes.Success] = new Color(0.1f, 0.5f, 0.2f),
            [EventNodeTypes.Failure] = new Color(0.5f, 0.12f, 0.12f),
            [EventNodeTypes.End] = new Color(0.12f, 0.12f, 0.12f),
        };

        public EventNodeElement(string nodeType, EventNodeData data = null)
        {
            NodeType = nodeType;
            NodeId = data?.nodeId ?? $"{nodeType.ToLower()}_{Guid.NewGuid().ToString()[..5]}";
            Data = data?.data ?? new EventNodePayload();

            style.width = 140;
            style.minHeight = 60;
            style.borderTopLeftRadius = 6;
            style.borderTopRightRadius = 6;
            style.borderBottomLeftRadius = 6;
            style.borderBottomRightRadius = 6;
            style.borderTopWidth = 3;
            style.borderTopColor = TypeColors.GetValueOrDefault(nodeType, Color.gray) * 1.3f;
            style.backgroundColor = new Color(0.22f, 0.22f, 0.24f);
            style.paddingBottom = 4;

            // 标题
            _titleLabel = new Label(GetNodeDisplayName(nodeType))
            {
                style = { fontSize = 11, unityFontStyleAndWeight = FontStyle.Bold, paddingLeft = 6, paddingTop = 4, color = Color.white }
            };
            Add(_titleLabel);

            // 副标题
            _subtitleLabel = new Label("")
            {
                style = { fontSize = 9, color = new Color(0.6f, 0.6f, 0.6f), paddingLeft = 8, paddingTop = 2, whiteSpace = WhiteSpace.Normal }
            };
            Add(_subtitleLabel);

            // 端口行
            var portRow = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, paddingLeft = 4, paddingRight = 4, paddingTop = 4 } };

            InputPort = new PortElement(this, true);
            InputPort.OnClicked += p => OnPortClicked?.Invoke(p);
            portRow.Add(InputPort);

            OutputPort = new PortElement(this, false);
            OutputPort.OnClicked += p => OnPortClicked?.Invoke(p);
            portRow.Add(OutputPort);

            Add(portRow);

            // 拖拽交互
            RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0 && evt.target is not PortElement)
                {
                    _isDragging = true;
                    _dragStart = evt.mousePosition;
                    _startPos = new Vector2(style.left.value.value, style.top.value.value);
                    this.CaptureMouse();
                    OnNodeClicked?.Invoke(this);
                    evt.StopPropagation();
                }
            });

            RegisterCallback<MouseMoveEvent>(evt =>
            {
                if (_isDragging)
                {
                    var delta = evt.mousePosition - _dragStart;
                    style.left = _startPos.x + delta.x;
                    style.top = _startPos.y + delta.y;
                    OnNodeMoving?.Invoke();  // 实时通知连线更新
                }
            });

            RegisterCallback<MouseUpEvent>(evt =>
            {
                if (_isDragging)
                {
                    _isDragging = false;
                    this.ReleaseMouse();
                    OnNodeMoved?.Invoke();
                }
            });

            UpdateLabels();
        }

        public void UpdateLabels()
        {
            _titleLabel.text = GetNodeDisplayName(NodeType);
            switch (NodeType)
            {
                case EventNodeTypes.Start:
                    _subtitleLabel.text = Data.title ?? NodeId;
                    break;
                case EventNodeTypes.Option:
                    _subtitleLabel.text = !string.IsNullOrEmpty(Data.text)
                        ? (Data.text.Length > 12 ? Data.text[..12] + "…" : Data.text)
                        : "";
                    break;
                case EventNodeTypes.Success:
                case EventNodeTypes.Failure:
                    _subtitleLabel.text = !string.IsNullOrEmpty(Data.resultType)
                        ? $"{Data.resultType}{(Data.amount.HasValue ? $" +{Data.amount}" : "")}"
                        : "";
                    break;
                case EventNodeTypes.End:
                    _subtitleLabel.text = Data.summaryText ?? "";
                    break;
            }
        }

        public EventNodeData ToNodeData()
        {
            return new EventNodeData
            {
                nodeId = NodeId,
                type = NodeType,
                position = new NodePosition { x = style.left.value.value, y = style.top.value.value },
                data = Data
            };
        }

        private static string GetNodeDisplayName(string type) => type switch
        {
            EventNodeTypes.Start => "\u25b6 Start",
            EventNodeTypes.Option => "\u25c7 Option",
            EventNodeTypes.Check => "\u25c6 Check",
            EventNodeTypes.Success => "\u25cb Success",
            EventNodeTypes.Failure => "\u2715 Failure",
            EventNodeTypes.Branch => "\u25c7 Branch",
            EventNodeTypes.End => "\u25a0 End",
            _ => type
        };
    }

    // ═══════════════════════════════════════════════
    //  PortElement  —  节点端口
    // ═══════════════════════════════════════════════
    public class PortElement : VisualElement
    {
        public EventNodeElement ParentNode { get; }
        public bool IsInput { get; }
        public event Action<PortElement> OnClicked;

        public PortElement(EventNodeElement parent, bool isInput)
        {
            ParentNode = parent;
            IsInput = isInput;

            style.width = 14; style.height = 14;
            style.borderTopLeftRadius = 7; style.borderTopRightRadius = 7;
            style.borderBottomLeftRadius = 7; style.borderBottomRightRadius = 7;
            style.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
            style.borderTopWidth = 2;
            style.borderTopColor = Color.white;

            RegisterCallback<ClickEvent>(evt =>
            {
                OnClicked?.Invoke(this);
                evt.StopPropagation();
            });
        }

        public void SetHighlight(bool on)
        {
            style.backgroundColor = on ? new Color(0.8f, 0.6f, 0.2f) : new Color(0.5f, 0.5f, 0.5f);
        }

        public Vector2 GetCenterPosition()
        {
            return parent.worldBound.position + worldBound.position + new Vector2(worldBound.width / 2, worldBound.height / 2);
        }
    }

    // ═══════════════════════════════════════════════
    //  ConnectionElement  —  端口间连线
    // ═══════════════════════════════════════════════
    public class ConnectionElement : VisualElement
    {
        public PortElement From { get; }
        public PortElement To { get; }

        public ConnectionElement(PortElement from, PortElement to)
        {
            From = from; To = to;
            style.position = Position.Absolute;
            style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;
            style.backgroundColor = Color.clear;
            generateVisualContent += OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            if (From == null || To == null) return;
            var painter = ctx.painter2D;
            var start = From.worldBound.center;
            var end = To.worldBound.center;

            var canvasPos = parent.worldBound.position;
            painter.strokeColor = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            painter.lineWidth = 2f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(start.x - canvasPos.x, start.y - canvasPos.y));

            // 贝塞尔曲线
            var mid = (end.x - start.x) * 0.5f;
            var cp1 = new Vector2(start.x + mid - canvasPos.x, start.y - canvasPos.y);
            var cp2 = new Vector2(end.x - mid - canvasPos.x, end.y - canvasPos.y);
            painter.BezierCurveTo(cp1, cp2, new Vector2(end.x - canvasPos.x, end.y - canvasPos.y));

            painter.Stroke();
        }
    }

    // ═══════════════════════════════════════════════
    //  ConnectionPreview  —  待用
    // ═══════════════════════════════════════════════
    public class ConnectionPreview : VisualElement
    {
        public ConnectionPreview() { style.position = Position.Absolute; style.left = 0; style.top = 0; style.right = 0; style.bottom = 0; }
    }
}
