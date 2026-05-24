using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Tactics.RoguelikeMap;
using Tactics.Runtime.Utilities;

namespace Tactics.Editor.RoguelikeMapEditor
{
    /// <summary>
    /// 地图节点可视化画布。支持缩放、平移、节点拖拽、虚线连线和右键菜单。
    /// 三层结构：canvasContainer → canvas → connectionLayer。
    /// </summary>
    public class MapGraphView : VisualElement
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
        private readonly List<MapNodeElement> _nodes = new();
        private readonly List<MapConnectionElement> _connections = new();
        private readonly List<(string from, string to)> _mapConnections = new();
        private const float DisplayScale = 50f; // 编辑器显示缩放因子
        private float _maxReachableDistance = 200f;
        private float _canvasWidth;
        private float _canvasHeight;
        private bool _hasCanvasBounds;
        private VisualElement _boundaryLayer;

        // ── Selection ─────────────────────────────
        private MapNodeElement _selectedNode;

        // ── Callbacks ─────────────────────────────
        public event Action<MapNodeElement> OnNodeSelected;
        public event Action OnNodeChanged;
        public event Action OnGraphChanged;

        // ── Node color palette ────────────────────
        private static readonly Dictionary<RoguelikeNodeType, Color> NodeColors = new()
        {
            [RoguelikeNodeType.Start]       = new Color(0.2f, 0.7f, 0.3f),   // 绿色
            [RoguelikeNodeType.Boss]        = new Color(0.8f, 0.15f, 0.15f),  // 红色
            [RoguelikeNodeType.Store]       = new Color(0.85f, 0.7f, 0.1f),   // 金色
            [RoguelikeNodeType.RestSite]    = new Color(0.2f, 0.5f, 0.9f),    // 蓝色
            [RoguelikeNodeType.Treasure]    = new Color(0.9f, 0.85f, 0.2f),   // 黄色
            [RoguelikeNodeType.MinorEnemy]  = new Color(0.5f, 0.5f, 0.5f),    // 灰色
            [RoguelikeNodeType.EliteEnemy]  = new Color(0.6f, 0.2f, 0.8f),    // 紫色
            [RoguelikeNodeType.Mystery]     = new Color(0.2f, 0.8f, 0.8f),    // 青色
        };

        private static readonly Dictionary<RoguelikeNodeType, string> NodeLabels = new()
        {
            [RoguelikeNodeType.Start]       = "S",
            [RoguelikeNodeType.Boss]        = "B",
            [RoguelikeNodeType.Store]       = "$",
            [RoguelikeNodeType.RestSite]    = "R",
            [RoguelikeNodeType.Treasure]    = "T",
            [RoguelikeNodeType.MinorEnemy]  = "E",
            [RoguelikeNodeType.EliteEnemy]  = "X",
            [RoguelikeNodeType.Mystery]     = "?",
        };

        // ═══════════════════════════════════════════
        public MapGraphView()
        {
            style.flexGrow = 1;
            style.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
            style.overflow = Overflow.Hidden;
            focusable = true;

            BuildCanvas();
            RegisterCallbacks();
        }

        // ── Configuration ─────────────────────────
        public void SetCanvasBounds(float width, float height)
        {
            _canvasWidth = width;
            _canvasHeight = height;
            _hasCanvasBounds = true;
            UpdateBoundary();
        }

        private void UpdateBoundary()
        {
            if (_boundaryLayer == null || !_hasCanvasBounds) return;
            _boundaryLayer.style.width = _canvasWidth * DisplayScale;
            _boundaryLayer.style.height = _canvasHeight * DisplayScale;
            _boundaryLayer.style.borderLeftWidth = 1;
            _boundaryLayer.style.borderRightWidth = 1;
            _boundaryLayer.style.borderTopWidth = 1;
            _boundaryLayer.style.borderBottomWidth = 1;
            _boundaryLayer.style.borderLeftColor = new Color(0.4f, 0.6f, 0.4f, 0.6f);
            _boundaryLayer.style.borderRightColor = new Color(0.4f, 0.6f, 0.4f, 0.6f);
            _boundaryLayer.style.borderTopColor = new Color(0.4f, 0.6f, 0.4f, 0.6f);
            _boundaryLayer.style.borderBottomColor = new Color(0.4f, 0.6f, 0.4f, 0.6f);
            _boundaryLayer.style.backgroundColor = new Color(0.18f, 0.18f, 0.20f, 0.3f);
        }

        public float MaxReachableDistance
        {
            get => _maxReachableDistance;
            set => _maxReachableDistance = value;
        }

        // ── Build ─────────────────────────────────
        private void BuildCanvas()
        {
            _canvasContainer = new VisualElement
            {
                style = { flexGrow = 1, overflow = Overflow.Hidden },
                focusable = true,
                pickingMode = PickingMode.Position
            };
            Add(_canvasContainer);

            // Grid background layer
            _gridLayer = new VisualElement
            {
                style = { position = Position.Absolute, left = 0, top = 0, right = 0, bottom = 0 },
                name = "grid-layer",
                pickingMode = PickingMode.Ignore
            };
            _gridLayer.generateVisualContent += DrawGrid;
            _canvasContainer.Add(_gridLayer);

            _canvas = new VisualElement
            {
                style = { position = Position.Absolute },
                name = "canvas",
                pickingMode = PickingMode.Position
            };
            _canvasContainer.Add(_canvas);

            // Connection layer (renders on top of nodes)
            _connectionLayer = new VisualElement
            {
                style = { position = Position.Absolute, left = 0, top = 0, right = 0, bottom = 0 },
                name = "connection-layer",
                pickingMode = PickingMode.Ignore
            };
            _canvas.Add(_connectionLayer);

            // Canvas boundary indicator
            _boundaryLayer = new VisualElement
            {
                style = { position = Position.Absolute, left = 0, top = 0 },
                name = "boundary-layer",
                pickingMode = PickingMode.Ignore
            };
            _canvasContainer.Add(_boundaryLayer);
        }

        private void RegisterCallbacks()
        {
            RegisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.TrickleDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove, TrickleDown.TrickleDown);
            RegisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);
            RegisterCallback<WheelEvent>(OnWheel, TrickleDown.TrickleDown);
            RegisterCallback<KeyDownEvent>(OnKeyDown);

            // Right-click on empty canvas → context menu
            RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 1 && (evt.target == _canvasContainer || evt.target == _canvas || evt.target == _gridLayer))
                {
                    var localPos = this.WorldToLocal(evt.position);
                    var canvasPos = new Vector2(
                        (localPos.x - _panOffset.x) / _zoom,
                        (localPos.y - _panOffset.y) / _zoom);
                    ShowContextMenu(evt.position, canvasPos);
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

        private void OnKeyDown(KeyDownEvent e)
        {
            switch (e.keyCode)
            {
                case KeyCode.Delete:
                case KeyCode.Backspace:
                    DeleteSelectedNode();
                    break;
                case KeyCode.F:
                    FocusAll();
                    break;
                case KeyCode.Escape:
                    DeselectNode();
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

        // ── Coordinate conversion ─────────────────
        private Vector2 ScreenToCanvas(Vector2 screenPos)
        {
            return new Vector2(
                (screenPos.x - _panOffset.x) / _zoom,
                (screenPos.y - _panOffset.y) / _zoom);
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
            for (float x = ox; x < viewW; x += gridSize)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, 0));
                painter.LineTo(new Vector2(x, viewH));
                painter.Stroke();
            }
            for (float y = oy; y < viewH; y += gridSize)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(0, y));
                painter.LineTo(new Vector2(viewW, y));
                painter.Stroke();
            }

            // Large grid
            painter.strokeColor = new Color(0.4f, 0.4f, 0.4f, 0.25f);
            painter.lineWidth = 1f;
            ox = _panOffset.x % gridSizeLarge;
            oy = _panOffset.y % gridSizeLarge;
            for (float x = ox; x < viewW; x += gridSizeLarge)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, 0));
                painter.LineTo(new Vector2(x, viewH));
                painter.Stroke();
            }
            for (float y = oy; y < viewH; y += gridSizeLarge)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(0, y));
                painter.LineTo(new Vector2(viewW, y));
                painter.Stroke();
            }
        }

        // ═══════════════════════════════════════════
        //  Context Menu
        // ═══════════════════════════════════════════
        private void ShowContextMenu(Vector2 screenPos, Vector2 canvasPos)
        {
            var menu = new GenericDropdownMenu();

            foreach (var nodeType in Enum.GetValues(typeof(RoguelikeNodeType)))
            {
                var type = (RoguelikeNodeType)nodeType;
                string label = $"Add {type}";
                menu.AddItem(label, false, () =>
                {
                    AddNode(type, canvasPos.x / DisplayScale, canvasPos.y / DisplayScale);
                    OnGraphChanged?.Invoke();
                });
            }

            menu.AddSeparator("");
            menu.AddItem("Focus All (F)", false, FocusAll);

            menu.DropDown(new Rect(screenPos.x, screenPos.y, 0, 0), this, DropdownMenuSizeMode.Content);
        }

        // ═══════════════════════════════════════════
        //  Node operations
        // ═══════════════════════════════════════════
        public MapNodeElement AddNode(RoguelikeNodeType nodeType, float x = 200, float y = 200, string nodeId = null)
        {
            var node = new MapNodeElement(nodeType, nodeId);
            node.style.position = Position.Absolute;
            node.style.left = x * DisplayScale;
            node.style.top = y * DisplayScale;

            // Selection
            node.OnNodeClicked += SelectNode;
            // Double-click → open Event Editor for Mystery nodes
            node.OnNodeDoubleClicked += OnNodeDoubleClickedHandler;
            // Move → update connections
            node.OnNodeMoving += () => RedrawConnectionsForNode(node);
            node.OnNodeMoved += () =>
            {
                RedrawConnectionsForNode(node);
                OnNodeChanged?.Invoke();
                OnGraphChanged?.Invoke();
            };

            // Context menu on node
            node.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Delete", _ => DeleteNode(node));
            }));

            _canvas.Add(node);
            _nodes.Add(node);
            if (_hasCanvasBounds)
                node.SetClampBounds(0, 0, _canvasWidth * DisplayScale - node.NodeSize, _canvasHeight * DisplayScale - node.NodeSize);
            RebuildAllConnections();
            return node;
        }

        public void LoadNodes(List<RoguelikeMapNode> mapNodes)
        {
            ClearCanvas();
            if (mapNodes == null) return;

            TLog.Info($"[MapGraphView] LoadNodes: {mapNodes?.Count ?? 0} nodes, DisplayScale={DisplayScale}");

            foreach (var mapNode in mapNodes)
            {
                var node = new MapNodeElement(mapNode.nodeType, mapNode.nodeId);
                node.EventId = mapNode.eventId ?? "";
                node.style.position = Position.Absolute;
                node.style.left = mapNode.position.x * DisplayScale;
                node.style.top = mapNode.position.y * DisplayScale;

                TLog.Info($"[MapGraphView] Node {mapNode.nodeId}: gamePos=({mapNode.position.x:F1},{mapNode.position.y:F1}) → displayPos=({mapNode.position.x * DisplayScale:F1},{mapNode.position.y * DisplayScale:F1})");

                node.OnNodeClicked += SelectNode;
                node.OnNodeDoubleClicked += OnNodeDoubleClickedHandler;
                node.OnNodeMoving += () => RedrawConnectionsForNode(node);
                node.OnNodeMoved += () =>
                {
                    RedrawConnectionsForNode(node);
                    OnNodeChanged?.Invoke();
                    OnGraphChanged?.Invoke();
                };

                node.AddManipulator(new ContextualMenuManipulator(evt =>
                {
                    evt.menu.AppendAction("Delete", _ => DeleteNode(node));
                }));

                _canvas.Add(node);
                _nodes.Add(node);
                if (_hasCanvasBounds)
                    node.SetClampBounds(0, 0, _canvasWidth * DisplayScale - node.NodeSize, _canvasHeight * DisplayScale - node.NodeSize);
            }

            // Extract connections from nodes' incoming/outgoing lists (set by generator/deserializer)
            _mapConnections.Clear();
            foreach (var mapNode in mapNodes)
            {
                foreach (var targetId in mapNode.outgoing)
                {
                    _mapConnections.Add((mapNode.nodeId, targetId));
                }
            }

            RebuildAllConnections();
            FocusAll();
        }

        private void SelectNode(MapNodeElement node)
        {
            DeselectNode();
            _selectedNode = node;
            node.SetSelected(true);
            OnNodeSelected?.Invoke(node);
        }

        private void OnNodeDoubleClickedHandler(MapNodeElement node)
        {
            if (node.NodeType == RoguelikeNodeType.Mystery)
            {
                TLog.Info($"[MapGraphView] 双击 Mystery 节点 '{node.NodeId}'，打开 Event Editor");
                EditorApplication.ExecuteMenuItem("Tactics/Event Editor");
            }
        }

        private void DeselectNode()
        {
            if (_selectedNode != null)
            {
                _selectedNode.SetSelected(false);
                _selectedNode = null;
                OnNodeSelected?.Invoke(null);
            }
        }

        private void DeleteNode(MapNodeElement node)
        {
            if (_selectedNode == node) DeselectNode();
            RemoveConnectionsForNode(node);
            node.RemoveFromHierarchy();
            _nodes.Remove(node);
            RebuildAllConnections();
            OnGraphChanged?.Invoke();
        }

        private void DeleteSelectedNode()
        {
            if (_selectedNode != null) DeleteNode(_selectedNode);
        }

        // Focus all → adjust pan/zoom to fit
        private void FocusAll()
        {
            if (_nodes.Count == 0) return;

            TLog.Info($"[MapGraphView] FocusAll: {_nodes.Count} nodes, contentRect={contentRect.width:F0}x{contentRect.height:F0}");

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (var n in _nodes)
            {
                float x = n.style.left.value.value;
                float y = n.style.top.value.value;
                float size = n.NodeSize;
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x + size);
                maxY = Mathf.Max(maxY, y + size);
            }

            float pad = 80;
            float w = maxX - minX + pad * 2;
            float h = maxY - minY + pad * 2;
            float zw = contentRect.width / w;
            float zh = contentRect.height / h;
            _zoom = Mathf.Clamp(Mathf.Min(zw, zh), 0.2f, 2f);
            _panOffset = new Vector2(-minX * _zoom + pad, -minY * _zoom + pad);
            UpdateCanvasTransform();

            TLog.Info($"[MapGraphView] FocusAll bounds: minX={minX:F1}, minY={minY:F1}, maxX={maxX:F1}, maxY={maxY:F1}, w={w:F1}, h={h:F1}, zoom={_zoom:F2}");
        }

        // ═══════════════════════════════════════════
        //  Connections
        // ═══════════════════════════════════════════
        public void RebuildAllConnections()
        {
            // Clear existing
            foreach (var conn in _connections) conn.RemoveFromHierarchy();
            _connections.Clear();

            if (_mapConnections.Count == 0) return;

            // Build a lookup from nodeId to MapNodeElement
            var nodeMap = new Dictionary<string, MapNodeElement>();
            foreach (var node in _nodes)
                nodeMap[node.NodeId] = node;

            foreach (var (fromId, toId) in _mapConnections)
            {
                if (nodeMap.TryGetValue(fromId, out var fromElement) &&
                    nodeMap.TryGetValue(toId, out var toElement))
                {
                    var conn = new MapConnectionElement(fromElement, toElement);
                    _connectionLayer.Add(conn);
                    _connections.Add(conn);
                }
            }
        }

        private void RedrawConnectionsForNode(MapNodeElement node)
        {
            foreach (var conn in _connections)
            {
                if (conn.From == node || conn.To == node)
                    conn.MarkDirtyRepaint();
            }
        }

        private void RemoveConnectionsForNode(MapNodeElement node)
        {
            _connections.RemoveAll(c =>
            {
                if (c.From == node || c.To == node)
                {
                    c.RemoveFromHierarchy();
                    return true;
                }
                return false;
            });
        }

        private MapNodeElement FindNodeElement(string nodeId)
        {
            foreach (var node in _nodes)
                if (node.NodeId == nodeId) return node;
            return null;
        }

        public List<RoguelikeMapNode> BuildMapNodeList()
        {
            var result = new List<RoguelikeMapNode>();
            foreach (var elem in _nodes)
            {
                var mapNode = new RoguelikeMapNode(
                    elem.NodeId,
                    elem.NodeType,
                    "",
                    GetNodeOriginalPosition(elem));
                mapNode.eventId = elem.EventId;
                result.Add(mapNode);
            }
            return result;
        }

        // ═══════════════════════════════════════════
        //  Clear
        // ═══════════════════════════════════════════
        public void ClearCanvas()
        {
            foreach (var node in _nodes) node.RemoveFromHierarchy();
            foreach (var conn in _connections) conn.RemoveFromHierarchy();
            _nodes.Clear();
            _connections.Clear();
            _mapConnections.Clear();
            _selectedNode = null;
        }

        public Vector2 GetNodeOriginalPosition(MapNodeElement node)
        {
            TLog.Info($"[MapGraphView] GetNodeOriginalPosition: display=({node.style.left.value.value:F1},{node.style.top.value.value:F1}) → original=({node.style.left.value.value / DisplayScale:F1},{node.style.top.value.value / DisplayScale:F1})");

            return new Vector2(
                node.style.left.value.value / DisplayScale,
                node.style.top.value.value / DisplayScale
            );
        }

        public List<MapNodeElement> GetNodes() => new(_nodes);
    }

    // ═══════════════════════════════════════════════
    //  MapNodeElement — 圆形节点
    // ═══════════════════════════════════════════════
    public class MapNodeElement : VisualElement
    {
        public string NodeId { get; }
        public RoguelikeNodeType NodeType { get; }
        public string EventId { get; set; } = "";
        public float NodeSize => 40f;

        public event Action<MapNodeElement> OnNodeClicked;
        public event Action<MapNodeElement> OnNodeDoubleClicked;
        public event Action OnNodeMoving;
        public event Action OnNodeMoved;

        private readonly Label _label;
        private Vector2 _dragStart;
        private Vector2 _startPos;
        private bool _isDragging;
        private double _lastClickTime;
        private float _clampMinX, _clampMinY, _clampMaxX, _clampMaxY;
        private bool _hasClampBounds;

        private static readonly Dictionary<RoguelikeNodeType, Color> TypeColors = new()
        {
            [RoguelikeNodeType.Start]       = new Color(0.2f, 0.7f, 0.3f),
            [RoguelikeNodeType.Boss]        = new Color(0.8f, 0.15f, 0.15f),
            [RoguelikeNodeType.Store]       = new Color(0.85f, 0.7f, 0.1f),
            [RoguelikeNodeType.RestSite]    = new Color(0.2f, 0.5f, 0.9f),
            [RoguelikeNodeType.Treasure]    = new Color(0.9f, 0.85f, 0.2f),
            [RoguelikeNodeType.MinorEnemy]  = new Color(0.5f, 0.5f, 0.5f),
            [RoguelikeNodeType.EliteEnemy]  = new Color(0.6f, 0.2f, 0.8f),
            [RoguelikeNodeType.Mystery]     = new Color(0.2f, 0.8f, 0.8f),
        };

        private static readonly Dictionary<RoguelikeNodeType, string> TypeLabels = new()
        {
            [RoguelikeNodeType.Start]       = "S",
            [RoguelikeNodeType.Boss]        = "B",
            [RoguelikeNodeType.Store]       = "$",
            [RoguelikeNodeType.RestSite]    = "R",
            [RoguelikeNodeType.Treasure]    = "T",
            [RoguelikeNodeType.MinorEnemy]  = "E",
            [RoguelikeNodeType.EliteEnemy]  = "X",
            [RoguelikeNodeType.Mystery]     = "?",
        };

        public MapNodeElement(RoguelikeNodeType nodeType, string nodeId = null)
        {
            NodeType = nodeType;
            NodeId = nodeId ?? $"{nodeType.ToString().ToLower()}_{Guid.NewGuid().ToString()[..5]}";

            // Circle style
            style.width = NodeSize;
            style.height = NodeSize;
            style.borderTopLeftRadius = NodeSize / 2;
            style.borderTopRightRadius = NodeSize / 2;
            style.borderBottomLeftRadius = NodeSize / 2;
            style.borderBottomRightRadius = NodeSize / 2;
            style.backgroundColor = TypeColors.GetValueOrDefault(nodeType, Color.gray);
            style.borderTopWidth = 2;
            style.borderTopColor = Color.white;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;

            // Label
            _label = new Label(TypeLabels.GetValueOrDefault(nodeType, "?"))
            {
                style =
                {
                    fontSize = 14,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = Color.white,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
            Add(_label);

            // Tooltip
            tooltip = $"{nodeType} ({NodeId})";

            // Drag + double-click detection
            RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    // Double-click detection
                    double now = EditorApplication.timeSinceStartup;
                    if (now - _lastClickTime < 0.3)
                    {
                        OnNodeDoubleClicked?.Invoke(this);
                        _lastClickTime = 0;
                        evt.StopPropagation();
                        return;
                    }
                    _lastClickTime = now;

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
                    var d = evt.mousePosition - _dragStart;
                    if (_hasClampBounds)
                    {
                        style.left = Mathf.Clamp(_startPos.x + d.x, _clampMinX, _clampMaxX);
                        style.top = Mathf.Clamp(_startPos.y + d.y, _clampMinY, _clampMaxY);
                    }
                    else
                    {
                        style.left = _startPos.x + d.x;
                        style.top = _startPos.y + d.y;
                    }
                    OnNodeMoving?.Invoke();
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
        }

        public void SetClampBounds(float minX, float minY, float maxX, float maxY)
        {
            _clampMinX = minX;
            _clampMinY = minY;
            _clampMaxX = maxX;
            _clampMaxY = maxY;
            _hasClampBounds = true;
        }

        public void SetSelected(bool selected)
        {
            style.borderTopColor = selected ? new Color(1f, 0.9f, 0.3f) : Color.white;
            style.borderTopWidth = selected ? 3 : 2;
        }
    }

    // ═══════════════════════════════════════════════
    //  MapConnectionElement — 虚线连接
    // ═══════════════════════════════════════════════
    public class MapConnectionElement : VisualElement
    {
        public MapNodeElement From { get; }
        public MapNodeElement To { get; }

        private const float DashLength = 6f;
        private const float GapLength = 4f;

        public MapConnectionElement(MapNodeElement from, MapNodeElement to)
        {
            From = from;
            To = to;
            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            style.right = 0;
            style.bottom = 0;
            style.backgroundColor = Color.clear;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnDraw;
        }

        private void OnDraw(MeshGenerationContext ctx)
        {
            if (From == null || To == null) return;

            var s = From.worldBound.center;
            var e = To.worldBound.center;

            // Convert to local space using WorldToLocal
            var start = this.WorldToLocal(s);
            var end = this.WorldToLocal(e);

            var painter = ctx.painter2D;
            painter.strokeColor = new Color(0.6f, 0.6f, 0.6f, 0.7f);
            painter.lineWidth = 1.5f;

            DrawDashedLine(painter, start, end);
        }

        private void DrawDashedLine(Painter2D painter, Vector2 start, Vector2 end)
        {
            var dir = end - start;
            float totalLength = dir.magnitude;
            if (totalLength < 0.01f) return;

            var normalized = dir / totalLength;
            float currentDist = 0f;
            bool drawing = true;

            while (currentDist < totalLength)
            {
                float segLength = drawing ? DashLength : GapLength;
                float segEnd = Mathf.Min(currentDist + segLength, totalLength);

                if (drawing)
                {
                    var p1 = start + normalized * currentDist;
                    var p2 = start + normalized * segEnd;
                    painter.BeginPath();
                    painter.MoveTo(p1);
                    painter.LineTo(p2);
                    painter.Stroke();
                }

                currentDist = segEnd;
                drawing = !drawing;
            }
        }
    }
}
