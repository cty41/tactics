using System;
using Tactics.Runtime.Utilities;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Tactics.AssetPipeline;
using Tactics.Flow.Roguelike;
using Tactics.Roguelike;
using Tactics.Flow.Battle;
using Tactics.RoguelikeMap;
using Tactics.RoguelikeMap.Interaction;
using Tactics.Roster;
using UnityEngine;
using UnityEngine.UIElements;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace Tactics.UI
{
    public sealed class RoguelikeMapUIController : UIControllerBase
    {
        public const string MapPlayerPrefsKey = PureRunSessionStore.MapPrefsKey;
        public const string RoguelikePendingNodePrefsKey = PureRunSessionStore.PendingNodePrefsKey;
        public const string RoguelikeReturnScenePrefsKey = PureRunSessionStore.ReturnScenePrefsKey;

        [Header("Layout")]
        public float unitsToPixelsMultiplier = 60f;
        public float padding = 400f;
        public Vector2 backgroundPadding = new Vector2(-100f, -100f);
        public float offsetFromNodes = 15f;

        [Header("Colors")]
        public Color32 visitedColor = Color.white;
        public Color32 lockedColor = Color.gray;
        public Color32 lineVisitedColor = Color.white;
        public Color32 lineLockedColor = Color.gray;

        [Header("Map Data")]
        public RoguelikeMapConfig mapConfig;

        [Header("Battle Scene")]
        [SerializeField] private string battleSceneName = "Test1";
        public string BattleSceneName => battleSceneName;

        private global::Tactics.RoguelikeMap.RoguelikeMap _currentMap;
        private NodeStateManager _nodeStateManager;
        private bool _locked;

        private ScrollView _scrollView;
        private VisualElement _mapContent;
        private VisualElement _linesLayer;
        private VisualElement _nodesLayer;
        private VisualElement _backgroundLayer;
        private MapConnectionLinesElement _linesElement;

        private readonly List<RoguelikeMapUINode> _mapNodes = new List<RoguelikeMapUINode>();
        private readonly List<MapLineConnection> _lineConnections = new List<MapLineConnection>();
        private VisualTreeAsset _nodeTemplate;
        private float _cachedViewportHeight;
        private Sprite _mapBackgroundSprite;

        private bool _isDragging;
        private float _dragStartX;
        private float _dragStartScrollValue;
        private float _dragStartY;
        private float _dragStartVerticalScrollValue;
        private TaskCompletionSource<bool> _mapReadyTcs;

        public static RoguelikeMapUIController Instance { get; set; }

        private void Awake()
        {
            Instance = this;
            EnsureMapConfig();
        }

        private void EnsureMapConfig()
        {
            if (mapConfig != null) return;

            var mgr = GameAssetManager.Instance;
            if (mgr != null)
            {
                // 优先加载黑暗森林原型配置，如果不存在则加载默认配置
                mapConfig = mgr.Load<RoguelikeMapConfig>("Assets/Tactics/RoguelikeMap/MapConfigs/DarkForestPrototypeConfig.asset");
                if (mapConfig == null)
                {
                    mapConfig = mgr.Load<RoguelikeMapConfig>("Assets/Tactics/RoguelikeMap/MapConfigs/DefaultRogueLikeMapConfig.asset");
                    TLog.Info("[RoguelikeMapUIController] 使用默认地图配置");
                }
                else
                {
                    TLog.Info("[RoguelikeMapUIController] 使用黑暗森林原型配置");
                }
            }

            if (mapConfig == null)
                TLog.Warning("[RoguelikeMapUIController] Failed to load RoguelikeMapConfig.");
        }

        protected override void OnShown()
        {
            TLog.Info($"[RoguelikeMapUIController] OnShown called. gameObject.active={gameObject.activeSelf}");
            ResetMapReadyState();
            WireOptionalCloseButtons();
            WireInventoryButton();

            LoadOrGenerateMap();
            TLog.Info($"[RoguelikeMapUIController] Starting ShowMapDelayed. _currentMap={_currentMap != null}");
            StartCoroutine(ShowMapDelayed());
        }

        protected override void OnHidden()
        {
            SetMapReady(false);
        }

        private System.Collections.IEnumerator ShowMapDelayed()
        {
            TLog.Info("[RoguelikeMapUIController] ShowMapDelayed coroutine started.");
            int frames = 0;
            VisualElement root = null;
            while (frames < 60)
            {
                root = Ui.GetRootElement(UIManager.UIId.RoguelikeMap);
                if (root != null && !float.IsNaN(root.layout.height) && root.layout.height > 0f)
                {
                    TryEnsureRootElements();
                    if (_mapContent != null && !float.IsNaN(_mapContent.layout.height) && _mapContent.layout.height > 0f)
                        break;
                }
                yield return null;
                frames++;
            }
            TLog.Info($"[RoguelikeMapUIController] Layout ready after {frames} frames. root.layout={root?.layout.width}x{root?.layout.height}, mapContainer.layout={_mapContent?.layout.width}x{_mapContent?.layout.height}");

            // Ensure GameAssetManager has initialized before loading assets
            EnsureMapConfig();
            EnsureNodeTemplate();

            if (_currentMap == null)
            {
                TLog.Error("[RoguelikeMapUIController] _currentMap is null. Cannot show map.");
                SetMapReady(false);
                yield break;
            }

            TLog.Info("[RoguelikeMapUIController] Calling ShowMap...");
            ShowMap(_currentMap);
            RefreshPartyPanel();
            yield return StartCoroutine(WaitForMapReadyCoroutine());
        }

        private void LoadOrGenerateMap()
        {
            if (RoguelikeMapRuntimeState.HasActiveRun && RoguelikeMapRuntimeState.CurrentMap != null)
            {
                if (RoguelikeMapRuntimeState.CurrentMap.layoutVersion == RoguelikeMapGenerator.PureRunLayoutVersion)
                {
                    _currentMap = RoguelikeMapRuntimeState.CurrentMap;
                    _nodeStateManager = CreateNodeStateManager(_currentMap);
                    TLog.Info("[RoguelikeMapUIController] 从运行时状态恢复地图");
                }
                else
                {
                    GenerateNewMap();
                }
            }
            else
            {
                LoadFreshMap();
            }

            // 检测是否有中断的事件（玩家在事件节点中退出游戏）
            if (RoguelikeEventReentryManager.IsEventInProgress(out string interruptedEventType, out string interruptedNodeId))
            {
                TLog.Warning($"[RoguelikeMapUIController] Detected interrupted event: type={interruptedEventType}, nodeId={interruptedNodeId}");
                RoguelikeEventReentryManager.ClearEventInProgress();
            }
        }

        private void LoadFreshMap()
        {
            // 检查 SceneController 的地图模式配置
            var sc = SceneController.Instance;
            if (sc != null && sc.MapMode == MapGenerationMode.LocalFile && sc.MapDataFile != null)
            {
                // 从本地 JSON 配置文件加载地图
                string json = sc.MapDataFile.text;
                _currentMap = JsonConvert.DeserializeObject<global::Tactics.RoguelikeMap.RoguelikeMap>(json);
                if (_currentMap.visionRange <= 5f) _currentMap.visionRange = 15f;
                if (_currentMap.maxReachableDistance < 1f) _currentMap.maxReachableDistance = 10f;
                RoguelikeMapRuntimeState.AttachMap(_currentMap);
                _nodeStateManager = CreateNodeStateManager(_currentMap);
                TLog.Info($"[RoguelikeMapUIController] 从本地配置加载地图: {sc.MapDataFile.name}");
            }
            else if (PureRunSessionStore.TryLoad(out _, out var savedMap))
            {
                _currentMap = savedMap;
                if (_currentMap.visionRange < 5f) _currentMap.visionRange = 15f;
                if (_currentMap.maxReachableDistance < 1f) _currentMap.maxReachableDistance = 10f;
                _nodeStateManager = CreateNodeStateManager(_currentMap);
            }
            else
            {
                GenerateNewMap();
            }
        }

        private void GenerateNewMap()
        {
            if (mapConfig == null)
            {
                TLog.Warning("[RoguelikeMapUIController] mapConfig is null!");
                return;
            }

            int runSeed = RoguelikeMapGenerator.CreateRunSeed();
            _currentMap = RoguelikeMapGenerator.GetPureRunMap(mapConfig, runSeed);
            _nodeStateManager = CreateNodeStateManager(_currentMap);
            PureRunSessionStore.StartNew(PlayerAdventureStateStore.CreatePureRunState(runSeed), _currentMap);
        }

        private void SaveMap()
        {
            if (_currentMap == null) return;

            PureRunSessionStore.SaveMap(_currentMap);
        }

        private bool TryEnsureRootElements()
        {
            if (_scrollView != null) return true;

            var root = Ui.GetRootElement(UIManager.UIId.RoguelikeMap);
            TLog.Info($"[RoguelikeMapUIController] TryEnsureRootElements: root={root != null}");
            if (root == null)
            {
                TLog.Error("[RoguelikeMapUIController] Could not get root visual element for RoguelikeMap UI.");
                return false;
            }

            _scrollView = root.Q<ScrollView>("MapScrollView");
            _backgroundLayer = root.Q<VisualElement>("BackgroundLayer");
            _linesLayer = root.Q<VisualElement>("LinesLayer");
            _nodesLayer = root.Q<VisualElement>("NodesLayer");

            TLog.Info($"[RoguelikeMapUIController] TryEnsureRootElements: scrollView={_scrollView != null}, bgLayer={_backgroundLayer != null}, linesLayer={_linesLayer != null}, nodesLayer={_nodesLayer != null}");

            if (_scrollView == null)
            {
                TLog.Error("[RoguelikeMapUIController] Missing required ScrollView in UXML.");
                return false;
            }

            _mapContent = root.Q<VisualElement>("MapContainer");
            TLog.Info($"[RoguelikeMapUIController] TryEnsureRootElements: mapContainer={_mapContent != null}");
            if (_mapContent == null)
            {
                TLog.Error("[RoguelikeMapUIController] MapContainer is null.");
                return false;
            }

            _scrollView.contentViewport.pickingMode = PickingMode.Position;
            _scrollView.horizontalScrollerVisibility = ScrollerVisibility.Auto;

            _scrollView.RegisterCallback<PointerDownEvent>(OnScrollViewPointerDown, TrickleDown.TrickleDown);
            _scrollView.RegisterCallback<PointerMoveEvent>(OnScrollViewPointerMove, TrickleDown.TrickleDown);
            _scrollView.RegisterCallback<PointerUpEvent>(OnScrollViewPointerUp, TrickleDown.TrickleDown);

            return true;
        }

        public void ShowMap(global::Tactics.RoguelikeMap.RoguelikeMap m)
        {
            if (m == null)
            {
                TLog.Warning("Map was null in RoguelikeMapUIController.ShowMap()");
                return;
            }

            if (!TryEnsureRootElements())
                return;

            var root = Ui.GetRootElement(UIManager.UIId.RoguelikeMap);
            TLog.Info($"[RoguelikeMapUIController] ShowMap: root={root != null}, scrollView={_scrollView != null}, mapContainer={_mapContent != null}, mapContainer.layout={_mapContent.layout.width}x{_mapContent.layout.height}, nodesLayer={_nodesLayer != null}, bgLayer={_backgroundLayer != null}");

            _currentMap = m;
            RoguelikeMapRuntimeState.AttachMap(_currentMap, ResolveCurrentNodeId());
            _nodeStateManager = CreateNodeStateManager(_currentMap);
            ClearMap();

            SetMapLength();
            ScrollToOrigin();

            // Explicitly set layer sizes to avoid layout timing issues
            float mapWidth = padding * 2 + CalculateMapXSpan() * unitsToPixelsMultiplier;
            float mapHeight = padding * 2 + CalculateMapYSpan() * unitsToPixelsMultiplier;
            if (_backgroundLayer != null) { _backgroundLayer.style.width = mapWidth; _backgroundLayer.style.height = mapHeight; _backgroundLayer.style.position = Position.Absolute; }
            if (_linesLayer != null) { _linesLayer.style.width = mapWidth; _linesLayer.style.height = mapHeight; _linesLayer.style.position = Position.Absolute; }
            if (_nodesLayer != null) { _nodesLayer.style.width = mapWidth; _nodesLayer.style.height = mapHeight; _nodesLayer.style.position = Position.Absolute; }

            EnsureMapBackgroundSprite();
            if (_backgroundLayer != null && _mapBackgroundSprite != null)
            {
                var bgElement = new VisualElement();
                bgElement.style.position = UnityEngine.UIElements.Position.Absolute;
                bgElement.style.left = 0;
                bgElement.style.top = 0;
                
                // 当mapWidth较小时，拉大背景到全屏
                float screenWidth = Screen.width;
                float screenHeight = Screen.height;
                float bgWidth = Mathf.Max(mapWidth, screenWidth);
                float bgHeight = Mathf.Max(mapHeight, screenHeight);
                
                bgElement.style.width = bgWidth;
                bgElement.style.height = bgHeight;
                bgElement.style.backgroundImage = new StyleBackground(_mapBackgroundSprite);
                bgElement.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
                bgElement.style.unitySliceLeft = (int)_mapBackgroundSprite.border.x;
                bgElement.style.unitySliceTop = (int)_mapBackgroundSprite.border.w;
                bgElement.style.unitySliceRight = (int)_mapBackgroundSprite.border.z;
                bgElement.style.unitySliceBottom = (int)_mapBackgroundSprite.border.y;
                _backgroundLayer.Add(bgElement);
            }

            // Add a relative-positioned sizer to force flex layout to allocate non-zero space for MapContainer
            if (_mapContent != null)
            {
                var existingSizer = _mapContent.Q("MapContainerSizer");
                if (existingSizer == null)
                {
                    var sizer = new VisualElement();
                    sizer.name = "MapContainerSizer";
                    sizer.style.position = Position.Absolute;
                    sizer.pickingMode = PickingMode.Ignore;
                    sizer.style.width = mapWidth;
                    sizer.style.height = mapHeight;
                    _mapContent.Add(sizer);
                }
                else
                {
                    existingSizer.style.width = mapWidth;
                    existingSizer.style.height = mapHeight;
                }
            }

            BuildMapContent(m);
        }

        private void RefreshPartyPanel()
        {
            var root = Ui.GetRootElement(UIManager.UIId.RoguelikeMap);
            if (root == null) return;

            var partyPanel = root.Q<VisualElement>("PartyPanel");
            if (partyPanel == null) return;

            partyPanel.Clear();

            var state = PlayerAdventureStateStore.Load();
            if (state?.Roster == null || state.ActivePartyCharacterIds == null) return;

            var goldLabel = root.Q<Label>("GoldLabel");
            if (goldLabel != null)
                goldLabel.text = state.Gold.ToString();

            foreach (string id in state.ActivePartyCharacterIds)
            {
                var data = state.Roster.FirstOrDefault(c => c.Id == id);
                if (data == null) continue;

                var slot = CreatePartySlot(data);
                partyPanel.Add(slot);
            }
        }

        private VisualElement CreatePartySlot(CharacterDefinition data)
        {
            var slot = new VisualElement();
            slot.AddToClassList("party-slot");
            if (data.IsDead)
                slot.AddToClassList("party-dead");

            // Avatar
            var avatar = new VisualElement();
            avatar.AddToClassList("party-avatar");
            slot.Add(avatar);

            // Name
            string displayName = string.IsNullOrWhiteSpace(data.DisplayName)
                ? GetRoleDisplayName(data.RoleType)
                : data.DisplayName;
            var nameLabel = new Label(data.IsDead ? $"{displayName} [DEAD]" : displayName);
            nameLabel.AddToClassList("party-name");
            slot.Add(nameLabel);

            // HP Bar
            var hpBg = new VisualElement();
            hpBg.AddToClassList("hp-bar-background");
            var hpFill = new VisualElement();
            hpFill.AddToClassList("hp-bar-fill");
            float hpPercent = data.IsDead ? 0f : Mathf.Clamp01(data.CurrentHp / (float)data.MaxHp) * 100f;
            hpFill.style.width = Length.Percent(hpPercent);
            hpBg.Add(hpFill);
            slot.Add(hpBg);

            var vitalsLabel = new Label(
                $"HP {Mathf.Max(0, data.CurrentHp)}/{data.MaxHp}  MP {Mathf.Max(0, data.CurrentMp ?? 0)}/{data.MaxMp}");
            vitalsLabel.AddToClassList("party-vitals");
            slot.Add(vitalsLabel);

            // Level
            var levelLabel = new Label($"Level: {data.Level}");
            levelLabel.AddToClassList("party-level");
            slot.Add(levelLabel);

            return slot;
        }

        private static string GetRoleDisplayName(Tactics.Common.Units.Classes.RoleType roleType)
        {
            return roleType switch
            {
                Tactics.Common.Units.Classes.RoleType.Barbarian => "\u6218\u58eb",
                Tactics.Common.Units.Classes.RoleType.Mage => "\u6cd5\u5e08",
                Tactics.Common.Units.Classes.RoleType.Hunter => "\u730e\u624b",
                _ => roleType.ToString()
            };
        }

        private void BuildMapContent(global::Tactics.RoguelikeMap.RoguelikeMap m)
        {
            CreateNodes(m.nodes);
            DrawLines();
            SetAttainableNodes();
            SetLineColors();
        }

        private void EnsureNodeTemplate()
        {
            if (_nodeTemplate != null) return;

            var mgr = GameAssetManager.Instance;
            if (mgr != null)
            {
                _nodeTemplate = mgr.Load<VisualTreeAsset>("Assets/Tactics/Arts/UI/RoguelikeMapNode.uxml");
            }

            if (_nodeTemplate == null)
                TLog.Warning("[RoguelikeMapUIController] Failed to load node template. Nodes will use fallback styling.");
        }

        private void EnsureMapBackgroundSprite()
        {
            if (_mapBackgroundSprite != null) return;

            var mgr = GameAssetManager.Instance;
            if (mgr != null)
            {
                _mapBackgroundSprite = mgr.Load<Sprite>("Assets/Tactics/Arts/Sprites/Kenney RPG Pack panels/panel_beige.png");
            }

            if (_mapBackgroundSprite == null)
                TLog.Warning("[RoguelikeMapUIController] Failed to load map background sprite. Background will use fallback color.");
        }

        private void ClearMap()
        {
            foreach (var node in _mapNodes)
                node.Dispose();

            _nodesLayer?.Clear();
            _linesLayer?.Clear();
            _backgroundLayer?.Clear();
            _linesElement = null;
            _mapNodes.Clear();
            _lineConnections.Clear();
        }

        private void SetMapLength()
        {
            if (_mapContent == null || _currentMap == null) return;

            float mapWidth = padding * 2 + CalculateMapXSpan() * unitsToPixelsMultiplier;
            float mapHeight = padding * 2 + CalculateMapYSpan() * unitsToPixelsMultiplier;
            
            _mapContent.style.width = mapWidth;
            _mapContent.style.height = mapHeight;
            _mapContent.style.flexGrow = 0;
            _mapContent.style.flexShrink = 0;
            _mapContent.style.minWidth = mapWidth;
            _mapContent.style.minHeight = mapHeight;
            _mapContent.style.position = Position.Relative;
            _cachedViewportHeight = mapHeight;
        }

        private void ScrollToOrigin()
        {
            if (_scrollView == null) return;
            _scrollView.horizontalScroller.value = 0f;
            _scrollView.verticalScroller.value = 0f;
        }

        private void CreateNodes(IEnumerable<RoguelikeMapNode> nodes)
        {
            int nodeIndex = 0;
            foreach (var node in nodes)
            {
                RoguelikeMapUINode mapNode = CreateMapNode(node, nodeIndex++);
                if (mapNode != null)
                    _mapNodes.Add(mapNode);
            }
        }

        private RoguelikeMapUINode CreateMapNode(RoguelikeMapNode node, int instanceIndex)
        {
            RoguelikeNodeBlueprint blueprint = GetBlueprint(node.blueprintName);
            var mapNode = new RoguelikeMapUINode(node, blueprint, visitedColor, lockedColor, _nodeTemplate);
            Vector2 vePos = ConvertToVisualElementPosition(GetNodePosition(node));
            mapNode.SetPosition(vePos);
            _nodesLayer.Add(mapNode.Root);
            return mapNode;
        }

        private Vector2 ConvertToVisualElementPosition(Vector2 anchoredPos)
        {
            const float nodeSize = 64f;
            float left = anchoredPos.x - nodeSize / 2f;
            float top = anchoredPos.y - nodeSize / 2f;
            return new Vector2(left, top);
        }

        private Vector2 GetNodePosition(RoguelikeMapNode node)
        {
            if (_currentMap == null) return Vector2.zero;
            return new Vector2(padding, padding) + node.position * unitsToPixelsMultiplier;
        }

        private float CalculateMapXSpan()
        {
            if (_currentMap == null || _currentMap.nodes == null || _currentMap.nodes.Count == 0)
                return 0f;
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            foreach (var node in _currentMap.nodes)
            {
                if (node.position.x < minX) minX = node.position.x;
                if (node.position.x > maxX) maxX = node.position.x;
            }
            return maxX - minX;
        }

        private float CalculateMapYSpan()
        {
            if (_currentMap == null || _currentMap.nodes == null || _currentMap.nodes.Count == 0)
                return 0f;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            foreach (var node in _currentMap.nodes)
            {
                if (node.position.y < minY) minY = node.position.y;
                if (node.position.y > maxY) maxY = node.position.y;
            }
            return maxY - minY;
        }

        public void SetAttainableNodes()
        {
            TLog.Info($"[RoguelikeMapUIController] SetAttainableNodes: nodeStateManager={_nodeStateManager != null}, mapNodes={_mapNodes?.Count ?? 0}, visitedNodes={_currentMap?.visitedNodes?.Count ?? -1}");
            // 使用 NodeStateManager 管理节点状态
            if (_nodeStateManager != null)
            {
                // 初始化节点状态
                _nodeStateManager.InitializeStates();
                RoguelikeMapRuntimeState.AttachMap(_currentMap, _nodeStateManager.CurrentNodeId);
                
                // 更新UI节点状态
                foreach (var node in _mapNodes)
                {
                    node.ApplyVisualState();
                }
            }
            else
            {
                // 回退到旧逻辑（兼容性）
                foreach (var node in _mapNodes)
                {
                    node.Node.Visibility = NodeVisibility.Hidden;
                    node.Node.IsReachable = false;
                    node.ApplyVisualState();
                }

                if (_currentMap.visitedNodes.Count == 0)
                {
                    // 无入边的节点设为可到达（起始节点）
                    foreach (var node in _mapNodes.Where(n => n.Node.incoming.Count == 0))
                    {
                        node.Node.Visibility = NodeVisibility.Revealed;
                        node.Node.IsReachable = true;
                        node.ApplyVisualState();
                    }
                }
                else
                {
                    foreach (var nodeId in _currentMap.visitedNodes)
                    {
                        var mapNode = GetNode(nodeId);
                        if (mapNode != null)
                        {
                            mapNode.Node.VisitState = NodeVisitState.Visited;
                            mapNode.ApplyVisualState();
                        }
                    }

                    var currentNode = _currentMap.GetNode(ResolveCurrentNodeId());

                    if (currentNode != null)
                    {
                        foreach (var outgoingId in currentNode.outgoing)
                        {
                            var mapNode = GetNode(outgoingId);
                            if (mapNode != null)
                            {
                                mapNode.Node.Visibility = NodeVisibility.Revealed;
                                mapNode.Node.IsReachable = true;
                                mapNode.ApplyVisualState();
                            }
                        }
                    }
                }
            }
        }

        public void SetLineColors()
        {
            foreach (var line in _lineConnections)
                line.Color = lineLockedColor;

            string currentNodeId = ResolveCurrentNodeId();
            if (string.IsNullOrEmpty(currentNodeId)) return;

            var currentNode = _currentMap.GetNode(currentNodeId);
            if (currentNode == null) return;

            foreach (var outgoingId in currentNode.outgoing)
            {
                var lineConnection = GetLineConnection(currentNode, GetNode(outgoingId)?.Node);
                if (lineConnection != null)
                    lineConnection.Color = lineVisitedColor;
            }

            var visitedList = RoguelikeMapRuntimeState.VisitedPathNodeIds;
            if (visitedList.Count <= 1) return;

            for (int i = 0; i < visitedList.Count - 1; i++)
            {
                var current = _currentMap.GetNode(visitedList[i]);
                var next = _currentMap.GetNode(visitedList[i + 1]);
                var lineConnection = GetLineConnection(current, next);
                if (lineConnection != null)
                    lineConnection.Color = lineVisitedColor;
            }

            _linesElement?.Refresh();
        }

        private void DrawLines()
        {
            foreach (var node in _mapNodes)
            {
                foreach (var connectionId in node.Node.outgoing)
                    AddLineConnection(node, GetNode(connectionId));
            }

            _linesElement = new MapConnectionLinesElement();
            _linesElement.style.position = UnityEngine.UIElements.Position.Absolute;
            _linesElement.style.left = 0;
            _linesElement.style.top = 0;
            _linesElement.style.width = Length.Percent(100);
            _linesElement.style.height = Length.Percent(100);
            _linesElement.SetConnections(_lineConnections);
            _linesLayer.Add(_linesElement);
        }

        private void AddLineConnection(RoguelikeMapUINode from, RoguelikeMapUINode to)
        {
            if (from == null || to == null) return;

            const float nodeSize = 64f;
            Vector2 fromCenter = from.NodePosition + new Vector2(nodeSize / 2f, nodeSize / 2f);
            Vector2 toCenter = to.NodePosition + new Vector2(nodeSize / 2f, nodeSize / 2f);

            Vector2 fromPoint = fromCenter + (toCenter - fromCenter).normalized * offsetFromNodes;
            Vector2 toPoint = toCenter + (fromCenter - toCenter).normalized * offsetFromNodes;

            var line = new MapLineConnection(from.Node, to.Node, fromPoint, toPoint);
            _lineConnections.Add(line);
        }

        private RoguelikeMapUINode GetNode(string nodeId)
        {
            return _mapNodes.FirstOrDefault(n => n.Node.nodeId == nodeId);
        }

        private MapLineConnection GetLineConnection(RoguelikeMapNode from, RoguelikeMapNode to)
        {
            if (from == null || to == null) return null;
            return _lineConnections.FirstOrDefault(l => l.FromNode.nodeId == from.nodeId && l.ToNode.nodeId == to.nodeId);
        }

        private RoguelikeNodeBlueprint GetBlueprint(string blueprintName)
        {
            if (mapConfig == null) return null;
            return mapConfig.nodeBlueprints.FirstOrDefault(n => n.name == blueprintName);
        }

        public void SelectNode(RoguelikeMapUINode mapNode)
        {
            if (_locked) return;
            if (mapNode == null || mapNode.Node == null) return;
            if (_currentMap == null) return;

            // 使用 NodeStateManager 检查节点是否可点击
            if (_nodeStateManager != null)
            {
                if (_nodeStateManager.IsNodeClickable(mapNode.Node.nodeId))
                {
                    SendPlayerToNode(mapNode);
                }
                else
                {
                    PlayWarningThatNodeCannotBeAccessed();
                }
            }
            else
            {
                // 回退到旧逻辑（兼容性）
                if (_currentMap.visitedNodes.Count == 0)
                {
                    if (mapNode.Node.incoming.Count == 0)
                        SendPlayerToNode(mapNode);
                    else
                        PlayWarningThatNodeCannotBeAccessed();
                }
                else
                {
                    var currentNode = _currentMap.GetNode(ResolveCurrentNodeId());

                    bool isNeighbor = currentNode != null &&
                        (currentNode.outgoing.Any(id => id == mapNode.Node.nodeId) ||
                         currentNode.incoming.Any(id => id == mapNode.Node.nodeId));
                    if (isNeighbor)
                        SendPlayerToNode(mapNode);
                    else
                        PlayWarningThatNodeCannotBeAccessed();
                }
            }
        }

        private void SendPlayerToNode(RoguelikeMapUINode mapNode)
        {
            _locked = true;
            mapNode.ShowSwirlAnimation();

            DOTween.Sequence().AppendInterval(1f).OnComplete(() => EnterNode(mapNode));
        }

        private void CommitPathForNode(RoguelikeMapUINode mapNode)
        {
            if (_nodeStateManager != null)
            {
                _nodeStateManager.VisitNode(mapNode.Node.nodeId);

                // 更新所有UI节点状态
                foreach (var node in _mapNodes)
                {
                    node.ApplyVisualState();
                }
            }

            RoguelikeMapRuntimeState.AttachMap(
                _currentMap,
                _nodeStateManager?.CurrentNodeId ?? mapNode.Node.nodeId);
            RoguelikeMapRuntimeState.CommitNodeProgress(mapNode.Node.nodeId);

            SaveMap();
            SetLineColors();
        }

private void EnterNode(RoguelikeMapUINode mapNode)
        {
            TLog.Info("Entering node: " + mapNode.Node.blueprintName + " of type: " + mapNode.Node.nodeType);

            // Ensure NodeInteractionManager exists
            if (NodeInteractionManager.Instance == null)
            {
                var existing = FindFirstObjectByType<NodeInteractionManager>();
                if (existing != null)
                {
                    TLog.Info("[RoguelikeMapUIController] Found existing NodeInteractionManager in scene");
                }
                else
                {
                    var go = new GameObject("NodeInteractionManager");
                    go.AddComponent<NodeInteractionManager>();
                    TLog.Info("[RoguelikeMapUIController] Created NodeInteractionManager at runtime");
                }
            }

            string eventType = GetNodeEventType(mapNode.Node.nodeType);
            if (NodeInteractionManager.Instance != null)
            {
                NodeInteractionManager.Instance.CurrentMap = _currentMap;
                if (mapNode.Node.nodeType == RoguelikeNodeType.MinorEnemy ||
                    mapNode.Node.nodeType == RoguelikeNodeType.EliteEnemy ||
                    mapNode.Node.nodeType == RoguelikeNodeType.Boss)
                {
                    NodeInteractionManager.Instance.HandleNodeInteraction(mapNode.Node);
                }
                else
                {
                    RoguelikeEventReentryManager.MarkEventInProgress(eventType, mapNode.Node.nodeId);
                    NodeInteractionManager.Instance.HandleNodeInteraction(
                        mapNode.Node,
                        () => OnNonBattleNodeInteractionCompleted(mapNode));
                    return;
                }
            }
            else
            {
                TLog.Warning("[RoguelikeMapUIController] NodeInteractionManager.Instance still null after creation attempt");
                if (mapNode.Node.nodeType != RoguelikeNodeType.MinorEnemy &&
                    mapNode.Node.nodeType != RoguelikeNodeType.EliteEnemy &&
                    mapNode.Node.nodeType != RoguelikeNodeType.Boss)
                {
                    RoguelikeEventReentryManager.MarkEventInProgress(eventType, mapNode.Node.nodeId);
                }
            }

            if (mapNode.Node.nodeType != RoguelikeNodeType.MinorEnemy &&
                mapNode.Node.nodeType != RoguelikeNodeType.EliteEnemy &&
                mapNode.Node.nodeType != RoguelikeNodeType.Boss)
            {
                StartCoroutine(CoUnlockAfterStub(mapNode));
            }
        }

        private void OnNonBattleNodeInteractionCompleted(RoguelikeMapUINode mapNode)
        {
            if (mapNode == null || mapNode.Node == null)
            {
                _locked = false;
                return;
            }

            CommitPathForNode(mapNode);
            RoguelikeEventReentryManager.ClearEventInProgress();
            _locked = false;
        }

        private static string GetNodeEventType(RoguelikeNodeType nodeType)
        {
            return nodeType switch
            {
                RoguelikeNodeType.RestSite => "Rest",
                RoguelikeNodeType.Store => "Store",
                RoguelikeNodeType.Treasure => "Treasure",
                RoguelikeNodeType.Mystery => "Mystery",
                _ => "Unknown"
            };
        }

        private async void EnterBattleNode(RoguelikeMapUINode mapNode)
        {
            var nodeId = mapNode.Node.nodeId;
            PlayerPrefs.SetString(RoguelikePendingNodePrefsKey, nodeId);
            PlayerPrefs.SetString(RoguelikeReturnScenePrefsKey, "Home");
            PlayerPrefs.Save();

            RoguelikeEventReentryManager.MarkEventInProgress("Battle", nodeId);

            await BattleFlowCoordinator.Instance.StartBattleAsync(battleSceneName);
        }

        private void EnterStubNode(RoguelikeMapUINode mapNode)
        {
            TLog.Info($"[Roguelike stub] Node '{mapNode.Node.blueprintName}' ({mapNode.Node.nodeType})");

            var nodeId = mapNode.Node.nodeId;
            string eventType = mapNode.Node.nodeType switch
            {
                RoguelikeNodeType.RestSite => "Rest",
                RoguelikeNodeType.Store => "Store",
                RoguelikeNodeType.Treasure => "Treasure",
                RoguelikeNodeType.Mystery => "Mystery",
                _ => "Unknown"
            };
            RoguelikeEventReentryManager.MarkEventInProgress(eventType, nodeId);

            StartCoroutine(CoUnlockAfterStub(mapNode));
        }

        private System.Collections.IEnumerator CoUnlockAfterStub(RoguelikeMapUINode mapNode)
        {
            yield return null;
            CommitPathForNode(mapNode);
            RoguelikeEventReentryManager.ClearEventInProgress();
            _locked = false;
        }

        private void PlayWarningThatNodeCannotBeAccessed()
        {
            TLog.Info("Selected node cannot be accessed");
        }

        private void WireOptionalCloseButtons()
        {
            var root = Ui.GetRootElement(UIManager.UIId.RoguelikeMap);
            if (root == null) return;

            Button closeButton = root.Q<Button>("CloseButton");
            if (closeButton != null)
            {
                closeButton.clicked -= OnCloseClicked;
                closeButton.clicked += OnCloseClicked;
                return;
            }

            Button backButton = root.Q<Button>("BackButton");
            if (backButton != null)
            {
                backButton.clicked -= OnCloseClicked;
                backButton.clicked += OnCloseClicked;
                return;
            }

            Button escButton = root.Q<Button>("EscButton");
            if (escButton != null)
            {
                escButton.clicked -= OnCloseClicked;
                escButton.clicked += OnCloseClicked;
                return;
            }

            TLog.Info("[RoguelikeMapUIController] No close/back button found in UXML.");
        }

        private void WireInventoryButton()
        {
            var root = Ui.GetRootElement(UIManager.UIId.RoguelikeMap);
            if (root == null) return;

            Button inventoryButton = root.Q<Button>("InventoryButton");
            if (inventoryButton != null)
            {
                inventoryButton.clicked -= OnInventoryClicked;
                inventoryButton.clicked += OnInventoryClicked;
                TLog.Info("[RoguelikeMapUIController] InventoryButton wired.");
            }
            else
            {
                TLog.Warning("[RoguelikeMapUIController] InventoryButton not found in UXML.");
            }
        }

        private static void OnCloseClicked()
        {
            RoguelikeFlowCoordinator.Instance.CloseMap();
        }

        private static void OnInventoryClicked()
        {
            UIManager.Instance.Show(UIManager.UIId.Inventory);
        }

        private void OnDestroy()
        {
            SetMapReady(false);
            ClearMap();
            if (_scrollView != null)
            {
                _scrollView.UnregisterCallback<PointerDownEvent>(OnScrollViewPointerDown, TrickleDown.TrickleDown);
                _scrollView.UnregisterCallback<PointerMoveEvent>(OnScrollViewPointerMove, TrickleDown.TrickleDown);
                _scrollView.UnregisterCallback<PointerUpEvent>(OnScrollViewPointerUp, TrickleDown.TrickleDown);
            }
        }

        private void OnScrollViewPointerDown(PointerDownEvent evt)
        {
            _isDragging = true;
            _dragStartX = evt.position.x;
            _dragStartY = evt.position.y;
            _dragStartScrollValue = _scrollView.horizontalScroller.value;
            _dragStartVerticalScrollValue = _scrollView.verticalScroller.value;
        }

        private void OnScrollViewPointerMove(PointerMoveEvent evt)
        {
            if (!_isDragging || _scrollView == null) return;
            float deltaX = _dragStartX - evt.position.x;
            float deltaY = _dragStartY - evt.position.y;
            _scrollView.horizontalScroller.value = Mathf.Clamp(
                _dragStartScrollValue + deltaX,
                _scrollView.horizontalScroller.lowValue,
                _scrollView.horizontalScroller.highValue
            );
            _scrollView.verticalScroller.value = Mathf.Clamp(
                _dragStartVerticalScrollValue + deltaY,
                _scrollView.verticalScroller.lowValue,
                _scrollView.verticalScroller.highValue
            );
        }

        private void OnScrollViewPointerUp(PointerUpEvent evt)
        {
            _isDragging = false;
        }

        private void OnApplicationQuit()
        {
            SaveMap();
        }

        public Task<bool> WaitUntilReadyAsync()
        {
            if (_mapReadyTcs == null)
                ResetMapReadyState();

            return _mapReadyTcs.Task;
        }

        private NodeStateManager CreateNodeStateManager(global::Tactics.RoguelikeMap.RoguelikeMap map)
        {
            return map == null ? null : new NodeStateManager(map, RoguelikeMapRuntimeState.CurrentNodeId);
        }

        private string ResolveCurrentNodeId()
        {
            if (_currentMap == null)
                return null;

            if (ReferenceEquals(RoguelikeMapRuntimeState.CurrentMap, _currentMap) &&
                !string.IsNullOrEmpty(RoguelikeMapRuntimeState.CurrentNodeId) &&
                _currentMap.GetNode(RoguelikeMapRuntimeState.CurrentNodeId) != null)
            {
                return RoguelikeMapRuntimeState.CurrentNodeId;
            }

            if (!string.IsNullOrEmpty(_nodeStateManager?.CurrentNodeId) &&
                _currentMap.GetNode(_nodeStateManager.CurrentNodeId) != null)
            {
                return _nodeStateManager.CurrentNodeId;
            }

            if (ReferenceEquals(RoguelikeMapRuntimeState.CurrentMap, _currentMap) &&
                RoguelikeMapRuntimeState.VisitedPathNodeIds.Count > 0)
            {
                string lastVisitedNodeId = RoguelikeMapRuntimeState.VisitedPathNodeIds[^1];
                if (!string.IsNullOrEmpty(lastVisitedNodeId) &&
                    _currentMap.GetNode(lastVisitedNodeId) != null)
                {
                    return lastVisitedNodeId;
                }
            }

            if (_currentMap.visitedNodes.Count == 1)
                return _currentMap.visitedNodes.First();

            return null;
        }

        private void ResetMapReadyState()
        {
            _mapReadyTcs = new TaskCompletionSource<bool>();
        }

        private void SetMapReady(bool isReady)
        {
            _mapReadyTcs?.TrySetResult(isReady);
        }

        private System.Collections.IEnumerator WaitForMapReadyCoroutine()
        {
            const int maxFrames = 120;
            for (int frame = 0; frame < maxFrames; frame++)
            {
                yield return null;
                if (IsMapFullyReady())
                {
                    TLog.Info($"[RoguelikeMapUIController] Map ready after {frame + 1} validation frames.");
                    SetMapReady(true);
                    yield break;
                }
            }

            TLog.Error("[RoguelikeMapUIController] Timed out waiting for map to become ready.");
            SetMapReady(false);
        }

        private bool IsMapFullyReady()
        {
            var root = Ui.GetRootElement(UIManager.UIId.RoguelikeMap);
            if (root == null || _mapContent == null || _nodesLayer == null || _linesLayer == null)
                return false;

            if (float.IsNaN(root.layout.height) || root.layout.height <= 0f)
                return false;

            if (float.IsNaN(_mapContent.layout.height) || _mapContent.layout.height <= 0f)
                return false;

            if (_currentMap?.nodes == null || _nodeStateManager == null)
                return false;

            if (_mapNodes.Count != _currentMap.nodes.Count)
                return false;

            if (_linesElement == null)
                return false;

            if (_nodesLayer.childCount < _mapNodes.Count)
                return false;

            var partyPanel = root.Q<VisualElement>("PartyPanel");
            if (partyPanel == null)
                return false;

            var state = PlayerAdventureStateStore.Load();
            int expectedPartyCount = state?.ActivePartyCharacterIds?.Count ?? 0;
            return partyPanel.childCount == expectedPartyCount;
        }
    }

    internal sealed class MapLineConnection
    {
        public RoguelikeMapNode FromNode { get; }
        public RoguelikeMapNode ToNode { get; }
        public Vector2 FromPoint { get; }
        public Vector2 ToPoint { get; }
        public Color Color { get; set; }

        public MapLineConnection(RoguelikeMapNode fromNode, RoguelikeMapNode toNode, Vector2 fromPoint, Vector2 toPoint)
        {
            FromNode = fromNode;
            ToNode = toNode;
            FromPoint = fromPoint;
            ToPoint = toPoint;
            Color = Color.gray;
        }
    }
}
