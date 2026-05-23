using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Tactics.Runtime.Utilities;

namespace Tactics.Editor.RoguelikeEventEditor
{
    /// <summary>
    /// Graph editor canvas with VS-inspired drag-to-connect, fuzzy finder, and context menus.
    /// </summary>
    public class EventGraphView : VisualElement
    {
        // ── Canvas ────────────────────────────────
        private VisualElement _canvasContainer;
        private VisualElement _canvas;
        private VisualElement _connectionLayer;
        private VisualElement _gridLayer;
        private float _zoom = 1f;
        private Vector2 _panOffset = Vector2.zero;
        private Vector2 _lastMousePos;
        private bool _isPanning;

        // ── Data ──────────────────────────────────
        private SerializableEventData _currentEvent;
        private readonly List<EventNodeElement> _nodes = new();
        private readonly List<ConnectionElement> _connections = new();

        // ── Drag-to-connect state ─────────────────
        private PortElement _dragFromPort;
        private ConnectionPreview _dragPreviewLine;
        private bool _isDraggingConnection;
        private bool _dragPreviewFrozen;     // frozen when fuzzy finder is open

        // ── Selection ─────────────────────────────
        private EventNodeElement _selectedNode;

        // ── Callbacks ─────────────────────────────
        public event Action<EventNodeElement> OnNodeSelected;
        public event Action OnGraphChanged;

        // ── Node palette ──────────────────────────
        private static readonly (string type, string label)[] NodePalette =
        {
            (EventNodeTypes.Start,   "\u25b6 Start"),
            (EventNodeTypes.Option,  "\u25c7 Option"),
            (EventNodeTypes.Check,   "\u25c6 Check"),
            (EventNodeTypes.Success, "\u25cb Success"),
            (EventNodeTypes.Failure, "\u2715 Failure"),
            (EventNodeTypes.End,     "\u25a0 End"),
        };

        // ═══════════════════════════════════════════
        public EventGraphView()
        {
            style.flexGrow = 1;
            style.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
            style.overflow = Overflow.Hidden;
            focusable = true;

            BuildCanvas();
            RegisterCallbacks();
        }

        // ── Build ─────────────────────────────────
        private void BuildCanvas()
        {
            _canvasContainer = new VisualElement {
                style = { flexGrow = 1, overflow = Overflow.Hidden },
                focusable = true, pickingMode = PickingMode.Position
            };
            Add(_canvasContainer);

            // Grid background layer
            _gridLayer = new VisualElement {
                style = { position = Position.Absolute, left = 0, top = 0, right = 0, bottom = 0 },
                name = "grid-layer", pickingMode = PickingMode.Ignore
            };
            _gridLayer.generateVisualContent += DrawGrid;
            _canvasContainer.Add(_gridLayer);

            _canvas = new VisualElement {
                style = { position = Position.Absolute },
                name = "canvas", pickingMode = PickingMode.Position
            };
            _canvasContainer.Add(_canvas);

            // Connection layer (renders on top of nodes)
            _connectionLayer = new VisualElement {
                style = { position = Position.Absolute, left = 0, top = 0, right = 0, bottom = 0 },
                name = "connection-layer", pickingMode = PickingMode.Ignore
            };
            _canvas.Add(_connectionLayer);
        }

        private void RegisterCallbacks()
        {
            RegisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.TrickleDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove, TrickleDown.TrickleDown);
            RegisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);
            RegisterCallback<WheelEvent>(OnWheel, TrickleDown.TrickleDown);
            RegisterCallback<KeyDownEvent>(OnKeyDown);

            // Right-click on empty canvas → open fuzzy finder directly
            RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 1 && (evt.target == _canvasContainer || evt.target == _canvas || evt.target == _gridLayer))
                {
                    var localPos = this.WorldToLocal(evt.position);
                    var canvasPos = new Vector2(
                        (localPos.x - _panOffset.x) / _zoom,
                        (localPos.y - _panOffset.y) / _zoom);
                    ShowFuzzyFinder(localPos, canvasPos, null);
                    evt.StopPropagation();
                }
            });

            // Left-click on empty canvas → deselect
            RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0 && (evt.target == _canvasContainer || evt.target == _canvas || evt.target == _gridLayer))
                {
                    DeselectNode();
                    evt.StopPropagation();
                }
            });
        }

        // ── Zoom / Pan ────────────────────────────
        private void OnMouseDown(MouseDownEvent e)
        {
            // Middle button or Ctrl+Left = pan
            if (e.button == 2 || (e.button == 0 && e.ctrlKey))
            {
                _isPanning = true; _lastMousePos = e.mousePosition;
                this.CaptureMouse(); e.StopPropagation(); return;
            }
        }

        private void OnMouseMove(MouseMoveEvent e)
        {
            if (_isPanning)
            {
                var delta = e.mousePosition - _lastMousePos;
                _panOffset += delta; _lastMousePos = e.mousePosition;
                UpdateCanvasTransform();
                return;
            }
            if (_isDraggingConnection && !_dragPreviewFrozen)
            {
                UpdateDragPreview(e.mousePosition);
            }
        }

        private void OnMouseUp(MouseUpEvent e)
        {
            if (_isPanning) { _isPanning = false; this.ReleaseMouse(); }
            // Drag release on empty space → open fuzzy finder to create + auto-connect
            if (e.button == 0 && _isDraggingConnection && e.target is not PortElement)
            {
                var fromPort = _dragFromPort;
                _dragPreviewFrozen = true;
                var localPos = this.WorldToLocal(e.mousePosition);
                var canvasPos = new Vector2(
                    (localPos.x - _panOffset.x) / _zoom,
                    (localPos.y - _panOffset.y) / _zoom);
                ShowFuzzyFinder(localPos, canvasPos, e.mousePosition, afterCreate: node =>
                {
                    var targetPort = fromPort.IsInput ? node.OutputPort : node.InputPort;
                    if (targetPort != null)
                        CreateConnection(fromPort, targetPort);
                });
            }
        }

        private void OnWheel(WheelEvent e)
        {
            if (e.ctrlKey) { _zoom = Mathf.Clamp(_zoom - e.delta.y * 0.05f, 0.2f, 3f); UpdateCanvasTransform(); e.StopPropagation(); }
        }

        private void OnKeyDown(KeyDownEvent e)
        {
            switch (e.keyCode)
            {
                case KeyCode.Delete:
                case KeyCode.Backspace:
                    DeleteSelectedNode();
                    break;
                case KeyCode.Space:
                    ShowFuzzyFinder(
                        new Vector2(contentRect.width / 2, contentRect.height / 2),
                        new Vector2((contentRect.width / 2 - _panOffset.x) / _zoom, (contentRect.height / 2 - _panOffset.y) / _zoom),
                        null);
                    e.StopPropagation();
                    break;
                case KeyCode.Escape:
                    if (_isDraggingConnection) CancelDragConnection();
                    DeselectNode();
                    break;
                case KeyCode.F:
                    FocusAll();
                    break;
            }
        }

        private void UpdateCanvasTransform()
        {
            _canvas.style.translate = new Translate(_panOffset.x, _panOffset.y);
            _canvas.style.scale = new Scale(new Vector3(_zoom, _zoom, 1));
            _canvas.style.transformOrigin = new TransformOrigin(new Length(0), new Length(0));
            _gridLayer.MarkDirtyRepaint();
        }

        // ── Grid background ────────────────────────
        private void DrawGrid(MeshGenerationContext ctx)
        {
            var painter = ctx.painter2D;
            float viewW = _canvasContainer.layout.width;
            float viewH = _canvasContainer.layout.height;
            float gridSize = 20f * _zoom;
            float gridSizeLarge = gridSize * 5f;

            // Small grid
            painter.strokeColor = new Color(0.3f, 0.3f, 0.3f, 0.15f);
            painter.lineWidth = 0.5f;
            float ox = _panOffset.x % gridSize;
            float oy = _panOffset.y % gridSize;
            for (float x = ox; x < viewW; x += gridSize) { painter.BeginPath(); painter.MoveTo(new Vector2(x, 0)); painter.LineTo(new Vector2(x, viewH)); painter.Stroke(); }
            for (float y = oy; y < viewH; y += gridSize) { painter.BeginPath(); painter.MoveTo(new Vector2(0, y)); painter.LineTo(new Vector2(viewW, y)); painter.Stroke(); }

            // Large grid
            painter.strokeColor = new Color(0.4f, 0.4f, 0.4f, 0.25f);
            ox = _panOffset.x % gridSizeLarge; oy = _panOffset.y % gridSizeLarge;
            for (float x = ox; x < viewW; x += gridSizeLarge) { painter.BeginPath(); painter.MoveTo(new Vector2(x, 0)); painter.LineTo(new Vector2(x, viewH)); painter.Stroke(); }
            for (float y = oy; y < viewH; y += gridSizeLarge) { painter.BeginPath(); painter.MoveTo(new Vector2(0, y)); painter.LineTo(new Vector2(viewW, y)); painter.Stroke(); }
        }

        // ═══════════════════════════════════════════
        //  Drag-to-Connect (VS FlowDragAndDropUtility)
        // ═══════════════════════════════════════════
        public void StartDragConnection(PortElement fromPort)
        {
            TLog.Info($"[EE] StartDragConnection port={(fromPort.IsInput?"IN":"OUT")} frozen={_dragPreviewFrozen}");
            _dragFromPort = fromPort;
            _isDraggingConnection = true;
            _dragPreviewFrozen = false;
            _dragFromPort.SetHighlight(true);

            _dragPreviewLine = new ConnectionPreview();
            _connectionLayer.Add(_dragPreviewLine);
        }

        private void UpdateDragPreview(Vector2 mousePos)
        {
            if (_dragFromPort == null || _dragPreviewLine == null) return;
            var start = _dragFromPort.worldBound.center;
            var canvasPos = _canvas.worldBound.position;
            _dragPreviewLine.Draw(start - canvasPos, mousePos - canvasPos);
        }

        private void CancelDragConnection()
        {
            TLog.Info($"[EE] CancelDragConnection frozen={_dragPreviewFrozen} hasLine={_dragPreviewLine!=null} isDragging={_isDraggingConnection}");
            _dragPreviewFrozen = false;
            if (_dragFromPort != null) _dragFromPort.SetHighlight(false);
            if (_dragPreviewLine != null) { _dragPreviewLine.RemoveFromHierarchy(); _dragPreviewLine = null; }
            _dragFromPort = null;
            _isDraggingConnection = false;
        }

        public void CompleteDragConnection(PortElement toPort)
        {
            if (_dragFromPort == null) return;
            CreateConnection(_dragFromPort, toPort);
            CancelDragConnection();
        }

        // ═══════════════════════════════════════════
        //  Fuzzy Finder (VS FuzzyWindow)
        // ═══════════════════════════════════════════
        private void ShowFuzzyFinder(Vector2 localPos, Vector2 canvasPos, Vector2? panelMousePos = null, Action<EventNodeElement> afterCreate = null)
        {
            // If a drag is in progress, freeze the preview line at the popup position
            if (_isDraggingConnection && _dragPreviewLine != null && panelMousePos.HasValue)
            {
                var fromCenter = _dragFromPort.worldBound.center;        // panel space
                var canvasOrigin = (Vector2)_canvas.worldBound.position; // panel space
                // Both in panel space → subtraction gives canvas-local (with zoom=1 it's correct)
                var startInCanvas = fromCenter - canvasOrigin;
                var endInCanvas = panelMousePos.Value - canvasOrigin;
                Debug.Log($"[EE] FreezePreview fromCenter={fromCenter} canvasOrigin={canvasOrigin} mousePanel={panelMousePos.Value} startCanvas={startInCanvas} endCanvas={endInCanvas}");
                _dragPreviewLine.Draw(startInCanvas, endInCanvas);
                _dragPreviewFrozen = true;
            }

            var popup = new FuzzySearchPopup(NodePalette, (type, label) =>
            {
                var node = AddNodeToCanvas(type, canvasPos.x, canvasPos.y);
                afterCreate?.Invoke(node);
                CancelDragConnection();
                OnGraphChanged?.Invoke();
            });
            popup.style.position = Position.Absolute;
            popup.style.left = localPos.x;
            popup.style.top = localPos.y;
            // Cancel drag only when popup is truly removed without selecting
            popup.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                TLog.Info($"[EE] Popup DetachFromPanel isDragging={_isDraggingConnection}");
                if (_isDraggingConnection) CancelDragConnection();
            });
            this.Add(popup);
            popup.Focus();
        }

        // ═══════════════════════════════════════════
        //  Node operations
        // ═══════════════════════════════════════════
        public EventNodeElement AddNodeToCanvas(string nodeType, float x = 200, float y = 200, EventNodeData data = null)
        {
            var node = new EventNodeElement(nodeType, data);
            node.style.position = Position.Absolute;
            node.style.left = x; node.style.top = y;

            // Drag-to-connect from port
            node.OnPortDragStart += port => StartDragConnection(port);
            node.OnPortDragEnd += port => CompleteDragConnection(port);
            // Selection
            node.OnNodeClicked += SelectNode;
            // Move
            node.OnNodeMoving += () => RedrawConnectionsForNode(node);
            node.OnNodeMoved += () => { RedrawConnectionsForNode(node); OnGraphChanged?.Invoke(); };

            // Context menu on node
            node.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Delete", _ => DeleteNode(node));
                evt.menu.AppendAction("Duplicate", _ => DuplicateNode(node));
            }));

            _canvas.Add(node);
            _nodes.Add(node);
            return node;
        }

        private void AddNodeAtViewCenter(string nodeType)
        {
            float cx = (contentRect.width / 2 - _panOffset.x) / _zoom;
            float cy = (contentRect.height / 2 - _panOffset.y) / _zoom;
            AddNodeToCanvas(nodeType, cx, cy);
            OnGraphChanged?.Invoke();
        }

        private void SelectNode(EventNodeElement node)
        {
            DeselectNode();
            _selectedNode = node;
            node.AddToClassList("selected");
            OnNodeSelected?.Invoke(node);
        }

        private void DeselectNode()
        {
            if (_selectedNode != null)
            {
                _selectedNode.RemoveFromClassList("selected");
                _selectedNode = null;
                OnNodeSelected?.Invoke(null);
            }
        }

        private void DeleteNode(EventNodeElement node)
        {
            DeselectNode();
            RemoveConnectionsForNode(node);
            node.RemoveFromHierarchy();
            _nodes.Remove(node);
            OnGraphChanged?.Invoke();
        }

        private void DeleteSelectedNode()
        {
            if (_selectedNode != null) DeleteNode(_selectedNode);
        }

        private void DuplicateNode(EventNodeElement node)
        {
            var newData = node.ToNodeData();
            newData.nodeId = $"{node.NodeType.ToLower()}_{Guid.NewGuid().ToString()[..5]}";
            AddNodeToCanvas(node.NodeType, node.style.left.value.value + 160, node.style.top.value.value + 20, newData);
            OnGraphChanged?.Invoke();
        }

        // Focus all → adjust pan/zoom to fit
        private void FocusAll()
        {
            if (_nodes.Count == 0) return;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var n in _nodes)
            {
                float x = n.style.left.value.value, y = n.style.top.value.value;
                minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x + 140); maxY = Mathf.Max(maxY, y + 70);
            }
            float pad = 60;
            float w = maxX - minX + pad * 2, h = maxY - minY + pad * 2;
            float zw = contentRect.width / w, zh = contentRect.height / h;
            _zoom = Mathf.Clamp(Mathf.Min(zw, zh), 0.2f, 2f);
            _panOffset = new Vector2(-minX * _zoom + pad, -minY * _zoom + pad);
            UpdateCanvasTransform();
        }

        // ═══════════════════════════════════════════
        //  Connections
        // ═══════════════════════════════════════════
        public void CreateConnection(PortElement from, PortElement to)
        {
            if (from.IsInput == to.IsInput) return;
            if (from.ParentNode == to.ParentNode) return;
            RemoveConnectionForPort(from);
            RemoveConnectionForPort(to);

            var conn = new ConnectionElement(from, to);
            _connectionLayer.Add(conn);
            _connections.Add(conn);
            OnGraphChanged?.Invoke();
        }

        private void RemoveConnectionForPort(PortElement port)
        {
            _connections.RemoveAll(c =>
            {
                if (c.From == port || c.To == port) { c.RemoveFromHierarchy(); return true; }
                return false;
            });
        }

        private void RemoveConnectionsForNode(EventNodeElement node)
        {
            _connections.RemoveAll(c =>
            {
                if (c.From?.ParentNode == node || c.To?.ParentNode == node)
                { c.RemoveFromHierarchy(); return true; }
                return false;
            });
        }

        private void RedrawConnectionsForNode(EventNodeElement node)
        {
            foreach (var conn in _connections)
                if (conn.From?.ParentNode == node || conn.To?.ParentNode == node)
                    conn.MarkDirtyRepaint();
        }

        // ═══════════════════════════════════════════
        //  Serialization
        // ═══════════════════════════════════════════
        public SerializableEventData BuildEventData()
        {
            var data = _currentEvent ?? new SerializableEventData();
            data.nodes = new List<EventNodeData>();
            data.connections = new List<EventConnectionData>();

            foreach (var node in _nodes) data.nodes.Add(node.ToNodeData());
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
                var node = AddNodeToCanvas(nd.type, nd.position?.x ?? 100, nd.position?.y ?? 100, nd);
                nodeMap[nd.nodeId] = node;
            }
            foreach (var conn in data.connections)
            {
                if (nodeMap.TryGetValue(conn.from, out var from) && nodeMap.TryGetValue(conn.to, out var to))
                {
                    var outPort = conn.port == "in" ? from.InputPort : from.OutputPort;
                    var inPort = conn.port == "in" ? to.OutputPort : to.InputPort;
                    if (outPort != null && inPort != null)
                        CreateConnection(outPort, inPort);
                }
            }
        }

        public void ClearCanvas()
        {
            foreach (var node in _nodes) node.RemoveFromHierarchy();
            foreach (var conn in _connections) conn.RemoveFromHierarchy();
            _nodes.Clear(); _connections.Clear();
            _currentEvent = null;
            if (_dragPreviewLine != null) { _dragPreviewLine.RemoveFromHierarchy(); _dragPreviewLine = null; }
        }
    }

    // ═══════════════════════════════════════════════
    //  FuzzySearchPopup
    // ═══════════════════════════════════════════════
    public class FuzzySearchPopup : VisualElement
    {
        private readonly (string type, string label)[] _items;
        private readonly Action<string, string> _onSelect;
        private TextField _searchField;
        private ListView _listView;
        private List<(string, string)> _filtered;

        public FuzzySearchPopup((string type, string label)[] items, Action<string, string> onSelect)
        {
            _items = items;
            _onSelect = onSelect;
            _filtered = new List<(string, string)>(items);

            style.width = 200; style.maxHeight = 250;
            style.backgroundColor = new Color(0.18f, 0.18f, 0.2f);
            style.borderTopLeftRadius = 6; style.borderTopRightRadius = 6;
            style.borderBottomLeftRadius = 6; style.borderBottomRightRadius = 6;
            style.borderTopWidth = 1; style.borderTopColor = new Color(0.4f, 0.4f, 0.4f);

            focusable = true;

            _searchField = new TextField { style = { marginLeft = 6, marginRight = 6, marginTop = 6, marginBottom = 4, fontSize = 12 } };
            _searchField.RegisterValueChangedCallback(evt => Filter(evt.newValue));
            Add(_searchField);

            _listView = new ListView(_filtered, 24, MakeItem, BindItem);
            _listView.style.flexGrow = 1;
            _listView.selectionType = SelectionType.Single;

            // Single-click → select & create node
            _listView.onSelectionChange += selectedItems =>
            {
                TLog.Info("[EE] Popup onSelectionChange fired");
                if (selectedItems is IEnumerable<object> items)
                {
                    foreach (var item in items)
                    {
                        var (type, label) = ((string, string))item;
                        _onSelect(type, label);
                        RemoveFromHierarchy();
                        return;
                    }
                }
            };

            // Enter / double-click → create node
            _listView.onItemsChosen += items =>
            {
                if (items is IEnumerable<object> chosen)
                {
                    foreach (var c in chosen)
                    {
                        var (type, label) = ((string, string))c;
                        _onSelect(type, label);
                        break;
                    }
                    RemoveFromHierarchy();
                }
            };
            Add(_listView);

            // Close on Escape or focus leaving the popup entirely
            RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape) RemoveFromHierarchy();
            });
            RegisterCallback<FocusOutEvent>(evt =>
            {
                var relatedType = evt.relatedTarget?.GetType().Name ?? "null";
                var isInternal = evt.relatedTarget is VisualElement r && this.Contains(r);
                TLog.Info($"[EE] Popup FocusOut relatedTarget={relatedType} isInternal={isInternal}");
                if (!isInternal) RemoveFromHierarchy();
            });

            _searchField.Focus();
        }

        private VisualElement MakeItem()
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, paddingLeft = 6, paddingRight = 6 } };
            var label = new Label { style = { fontSize = 11, color = Color.white } };
            row.Add(label);
            return row;
        }

        private void BindItem(VisualElement el, int idx)
        {
            var label = el.Q<Label>();
            if (label != null) label.text = _filtered[idx].Item2;
        }

        private void Filter(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                _filtered = new List<(string, string)>(_items);
            else
                _filtered = new List<(string, string)>(System.Array.FindAll(_items,
                    x => x.Item2.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                         x.Item1.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0));
            _listView.itemsSource = _filtered;
            _listView.RefreshItems();
        }

        public void Focus()
        {
            _searchField.Focus();
            _searchField.SelectAll();
        }
    }

    // ═══════════════════════════════════════════════
    //  ConnectionPreview — drag preview line
    // ═══════════════════════════════════════════════
    public class ConnectionPreview : VisualElement
    {
        private Vector2 _start, _end;

        public ConnectionPreview()
        {
            style.position = Position.Absolute; style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;
            style.backgroundColor = Color.clear; pickingMode = PickingMode.Ignore;
            generateVisualContent += OnDraw;
        }

        public void Draw(Vector2 start, Vector2 end) { _start = start; _end = end; MarkDirtyRepaint(); }

        private void OnDraw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            p.strokeColor = new Color(0.4f, 0.7f, 1f, 0.6f);
            p.lineWidth = 2f;
            p.BeginPath();
            p.MoveTo(_start);
            float dx = Mathf.Abs(_end.x - _start.x) * 0.5f;
            p.BezierCurveTo(new Vector2(_start.x + dx, _start.y), new Vector2(_end.x - dx, _end.y), _end);
            p.Stroke();
        }
    }

    // ═══════════════════════════════════════════════
    //  EventNodeElement
    // ═══════════════════════════════════════════════
    public class EventNodeElement : VisualElement
    {
        public string NodeId { get; set; }
        public string NodeType { get; private set; }
        public EventNodePayload Data { get; set; }
        public PortElement InputPort { get; private set; }
        public PortElement OutputPort { get; private set; }

        public event Action<PortElement> OnPortDragStart;
        public event Action<PortElement> OnPortDragEnd;
        public event Action<EventNodeElement> OnNodeClicked;
        public event Action OnNodeMoving;
        public event Action OnNodeMoved;

        public void InvokePortDragStart(PortElement p) => OnPortDragStart?.Invoke(p);
        public void InvokePortDragEnd(PortElement p) => OnPortDragEnd?.Invoke(p);

        private Label _titleLabel, _subtitleLabel;
        private Vector2 _dragStart, _startPos;
        private bool _isDragging;

        private static readonly Dictionary<string, Color> TypeColors = new()
        {
            [EventNodeTypes.Start]   = new Color(0.15f, 0.45f, 0.15f),
            [EventNodeTypes.Option]  = new Color(0.2f, 0.3f, 0.55f),
            [EventNodeTypes.Check]   = new Color(0.45f, 0.3f, 0.15f),
            [EventNodeTypes.Success] = new Color(0.1f, 0.5f, 0.2f),
            [EventNodeTypes.Failure] = new Color(0.5f, 0.12f, 0.12f),
            [EventNodeTypes.End]     = new Color(0.12f, 0.12f, 0.12f),
        };

        public EventNodeElement(string nodeType, EventNodeData data = null)
        {
            NodeType = nodeType;
            NodeId = data?.nodeId ?? $"{nodeType.ToLower()}_{Guid.NewGuid().ToString()[..5]}";
            Data = data?.data ?? new EventNodePayload();

            style.width = 140; style.minHeight = 60;
            style.borderTopLeftRadius = 6; style.borderTopRightRadius = 6;
            style.borderBottomLeftRadius = 6; style.borderBottomRightRadius = 6;
            style.borderTopWidth = 3;
            style.borderTopColor = TypeColors.GetValueOrDefault(nodeType, Color.gray) * 1.3f;
            style.backgroundColor = new Color(0.22f, 0.22f, 0.24f);
            style.paddingBottom = 4;

            _titleLabel = new Label(GetNodeDisplayName(nodeType))
            {
                style = { fontSize = 11, unityFontStyleAndWeight = FontStyle.Bold, paddingLeft = 6, paddingTop = 4, color = Color.white }
            };
            Add(_titleLabel);

            _subtitleLabel = new Label("")
            {
                style = { fontSize = 9, color = new Color(0.6f, 0.6f, 0.6f), paddingLeft = 8, paddingTop = 2, whiteSpace = WhiteSpace.Normal }
            };
            Add(_subtitleLabel);

            var portRow = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, paddingLeft = 4, paddingRight = 4, paddingTop = 4 } };

            InputPort = new PortElement(this, true);
            portRow.Add(InputPort);

            OutputPort = new PortElement(this, false);
            portRow.Add(OutputPort);
            Add(portRow);

            // Drag
            RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0 && evt.target is not PortElement)
                {
                    _isDragging = true; _dragStart = evt.mousePosition;
                    _startPos = new Vector2(style.left.value.value, style.top.value.value);
                    this.CaptureMouse(); OnNodeClicked?.Invoke(this); evt.StopPropagation();
                }
            });
            RegisterCallback<MouseMoveEvent>(evt =>
            {
                if (_isDragging)
                {
                    var d = evt.mousePosition - _dragStart;
                    style.left = _startPos.x + d.x; style.top = _startPos.y + d.y;
                    OnNodeMoving?.Invoke();
                }
            });
            RegisterCallback<MouseUpEvent>(evt =>
            {
                if (_isDragging) { _isDragging = false; this.ReleaseMouse(); OnNodeMoved?.Invoke(); }
            });

            UpdateLabels();
        }

        public void UpdateLabels()
        {
            _titleLabel.text = GetNodeDisplayName(NodeType);
            switch (NodeType)
            {
                case EventNodeTypes.Start:   _subtitleLabel.text = Data.title ?? NodeId; break;
                case EventNodeTypes.Option:  _subtitleLabel.text = !string.IsNullOrEmpty(Data.text) ? (Data.text.Length > 12 ? Data.text[..12] + "…" : Data.text) : ""; break;
                case EventNodeTypes.Check:   _subtitleLabel.text = ""; break;
                case EventNodeTypes.Success:
                case EventNodeTypes.Failure: _subtitleLabel.text = !string.IsNullOrEmpty(Data.resultType) ? $"{Data.resultType}{(Data.amount.HasValue ? $" +{Data.amount}" : "")}" : ""; break;
                case EventNodeTypes.End:     _subtitleLabel.text = Data.summaryText ?? ""; break;
            }
        }

        public EventNodeData ToNodeData() => new()
        {
            nodeId = NodeId, type = NodeType,
            position = new NodePosition { x = style.left.value.value, y = style.top.value.value },
            data = Data
        };

        private static string GetNodeDisplayName(string type) => type switch
        {
            EventNodeTypes.Start => "\u25b6 Start", EventNodeTypes.Option => "\u25c7 Option",
            EventNodeTypes.Check => "\u25c6 Check", EventNodeTypes.Success => "\u25cb Success",
            EventNodeTypes.Failure => "\u2715 Failure", EventNodeTypes.End => "\u25a0 End", _ => type
        };
    }

    // ═══════════════════════════════════════════════
    //  PortElement — drag-to-connect enabled
    // ═══════════════════════════════════════════════
    public class PortElement : VisualElement
    {
        public EventNodeElement ParentNode { get; }
        public bool IsInput { get; }
        private bool _isDragStarted;

        public PortElement(EventNodeElement parent, bool isInput)
        {
            ParentNode = parent; IsInput = isInput;
            style.width = 14; style.height = 14;
            style.borderTopLeftRadius = 7; style.borderTopRightRadius = 7;
            style.borderBottomLeftRadius = 7; style.borderBottomRightRadius = 7;
            style.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
            style.borderTopWidth = 2; style.borderTopColor = Color.white;

            RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0) { _isDragStarted = true; parent.InvokePortDragStart(this); evt.StopPropagation(); }
            });
            RegisterCallback<PointerMoveEvent>(evt => { if (_isDragStarted) evt.StopPropagation(); });
            RegisterCallback<PointerUpEvent>(evt =>
            {
                if (_isDragStarted) { _isDragStarted = false; parent.InvokePortDragEnd(this); evt.StopPropagation(); }
            });
        }

        public void SetHighlight(bool on) { style.backgroundColor = on ? new Color(0.8f, 0.6f, 0.2f) : new Color(0.5f, 0.5f, 0.5f); }
    }

    // ═══════════════════════════════════════════════
    //  ConnectionElement — bezier between ports
    // ═══════════════════════════════════════════════
    public class ConnectionElement : VisualElement
    {
        public PortElement From { get; }
        public PortElement To { get; }

        public ConnectionElement(PortElement from, PortElement to)
        {
            From = from; To = to;
            style.position = Position.Absolute; style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;
            style.backgroundColor = Color.clear; pickingMode = PickingMode.Ignore;
            generateVisualContent += OnDraw;
        }

        private void OnDraw(MeshGenerationContext ctx)
        {
            if (From == null || To == null) return;
            var s = From.worldBound.center;
            var e = To.worldBound.center;
            var cp = parent.worldBound.position;
            var p = ctx.painter2D;
            p.strokeColor = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            p.lineWidth = 2f;
            p.BeginPath();
            p.MoveTo(new Vector2(s.x - cp.x, s.y - cp.y));
            float dx = Mathf.Abs(e.x - s.x) * 0.5f;
            p.BezierCurveTo(new Vector2(s.x + dx - cp.x, s.y - cp.y), new Vector2(e.x - dx - cp.x, e.y - cp.y), new Vector2(e.x - cp.x, e.y - cp.y));
            p.Stroke();
        }
    }
}