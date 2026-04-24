using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Tactics.AssetPipeline;
using Tactics.Flow.Roguelike;
using Tactics.Flow.Battle;
using Tactics.RoguelikeMap;
using UnityEngine;
using UnityEngine.UIElements;
using Newtonsoft.Json;

namespace Tactics.UI
{
    public sealed class RoguelikeMapUIController : UIControllerBase
    {
        public const string MapPlayerPrefsKey = "RoguelikeMap";
        public const string RoguelikePendingNodePrefsKey = "RoguelikePendingNode";
        public const string RoguelikeReturnScenePrefsKey = "RoguelikeReturnScene";

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

        private global::Tactics.RoguelikeMap.RoguelikeMap _currentMap;
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
                mapConfig = mgr.Load<RoguelikeMapConfig>("Assets/Tactics/Arts/ScriptableObjects/MapConfigs/DefaultRogueLikeMapConfig.asset");
            }

            if (mapConfig == null)
                Debug.LogWarning("[RoguelikeMapUIController] Failed to load default RoguelikeMapConfig.");
        }

        protected override void OnShown()
        {
            Debug.Log($"[RoguelikeMapUIController] OnShown called. gameObject.active={gameObject.activeSelf}");
            WireOptionalCloseButtons();

            LoadOrGenerateMap();
            Debug.Log($"[RoguelikeMapUIController] Starting ShowMapDelayed. _currentMap={_currentMap != null}");
            StartCoroutine(ShowMapDelayed());
        }

        private System.Collections.IEnumerator ShowMapDelayed()
        {
            Debug.Log("[RoguelikeMapUIController] ShowMapDelayed coroutine started.");
            yield return null;
            Debug.Log($"[RoguelikeMapUIController] ShowMapDelayed after yield. gameObject.active={gameObject.activeSelf}");

            // Ensure GameAssetManager has initialized before loading assets
            EnsureMapConfig();
            EnsureNodeTemplate();

            if (_currentMap == null)
            {
                Debug.LogError("[RoguelikeMapUIController] _currentMap is null. Cannot show map.");
                yield break;
            }

            Debug.Log("[RoguelikeMapUIController] Calling ShowMap...");
            ShowMap(_currentMap);
        }

        private void LoadOrGenerateMap()
        {
            string prefsKey = MapPlayerPrefsKey;

            if (PlayerPrefs.HasKey(prefsKey))
            {
                string mapJson = PlayerPrefs.GetString(prefsKey);
                _currentMap = JsonConvert.DeserializeObject<global::Tactics.RoguelikeMap.RoguelikeMap>(mapJson);
                if (_currentMap?.path != null && _currentMap.path.Count > 0)
                {
                    var bossNode = _currentMap.GetBossNode();
                    if (bossNode != null && _currentMap.path.Any(p => p.Equals(bossNode.point)))
                    {
                        GenerateNewMap();
                    }
                }
                else
                {
                    GenerateNewMap();
                }
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
                Debug.LogWarning("[RoguelikeMapUIController] mapConfig is null!");
                return;
            }

            _currentMap = RoguelikeMapGenerator.GetMap(mapConfig);
            Debug.Log(_currentMap?.ToJson());
        }

        private void SaveMap()
        {
            if (_currentMap == null) return;

            string prefsKey = MapPlayerPrefsKey;
            string json = JsonConvert.SerializeObject(_currentMap, Formatting.Indented,
                new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
            PlayerPrefs.SetString(prefsKey, json);
            PlayerPrefs.Save();
        }

        private bool TryEnsureRootElements()
        {
            if (_scrollView != null) return true;

            var root = Ui.GetRootElement(UIManager.UIId.RoguelikeMap);
            Debug.Log($"[RoguelikeMapUIController] TryEnsureRootElements: root={root != null}");
            if (root == null)
            {
                Debug.LogError("[RoguelikeMapUIController] Could not get root visual element for RoguelikeMap UI.");
                return false;
            }

            _scrollView = root.Q<ScrollView>("MapScrollView");
            _backgroundLayer = root.Q<VisualElement>("BackgroundLayer");
            _linesLayer = root.Q<VisualElement>("LinesLayer");
            _nodesLayer = root.Q<VisualElement>("NodesLayer");

            Debug.Log($"[RoguelikeMapUIController] TryEnsureRootElements: scrollView={_scrollView != null}, bgLayer={_backgroundLayer != null}, linesLayer={_linesLayer != null}, nodesLayer={_nodesLayer != null}");

            if (_scrollView == null)
            {
                Debug.LogError("[RoguelikeMapUIController] Missing required ScrollView in UXML.");
                return false;
            }

            _mapContent = root.Q<VisualElement>("MapContainer");
            Debug.Log($"[RoguelikeMapUIController] TryEnsureRootElements: mapContainer={_mapContent != null}");
            if (_mapContent == null)
            {
                Debug.LogError("[RoguelikeMapUIController] MapContainer is null.");
                return false;
            }

            return true;
        }

        public void ShowMap(global::Tactics.RoguelikeMap.RoguelikeMap m)
        {
            if (m == null)
            {
                Debug.LogWarning("Map was null in RoguelikeMapUIController.ShowMap()");
                return;
            }

            if (!TryEnsureRootElements())
                return;

            var root = Ui.GetRootElement(UIManager.UIId.RoguelikeMap);
            Debug.Log($"[RoguelikeMapUIController] ShowMap: root={root != null}, scrollView={_scrollView != null}, mapContainer={_mapContent != null}, mapContainer.layout={_mapContent.layout.width}x{_mapContent.layout.height}, nodesLayer={_nodesLayer != null}, bgLayer={_backgroundLayer != null}");

            _currentMap = m;
            ClearMap();

            SetMapLength();
            ScrollToOrigin();

            // Explicitly set layer sizes to avoid layout timing issues
            float mapLength = padding + _currentMap.DistanceBetweenFirstAndLastLayers() * unitsToPixelsMultiplier;
            float mapHeight = _mapContent.layout.height > 0f ? _mapContent.layout.height : 1080f;
            if (_backgroundLayer != null) { _backgroundLayer.style.width = mapLength; _backgroundLayer.style.height = mapHeight; }
            if (_linesLayer != null) { _linesLayer.style.width = mapLength; _linesLayer.style.height = mapHeight; }
            if (_nodesLayer != null) { _nodesLayer.style.width = mapLength; _nodesLayer.style.height = mapHeight; }

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
                Debug.LogWarning("[RoguelikeMapUIController] Failed to load node template. Nodes will use fallback styling.");
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

            float length = padding + _currentMap.DistanceBetweenFirstAndLastLayers() * unitsToPixelsMultiplier;
            _mapContent.style.width = length;
            _mapContent.style.flexGrow = 0;
            _mapContent.style.flexShrink = 0;

            float viewportHeight = _scrollView.contentViewport.layout.height;
            if (viewportHeight <= 0f)
                viewportHeight = _scrollView.layout.height;
            if (viewportHeight <= 0f)
                viewportHeight = 1080f;
            _mapContent.style.height = viewportHeight;
        }

        private void ScrollToOrigin()
        {
            if (_scrollView == null) return;
            _scrollView.horizontalScroller.value = 0;
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
            float contentWidth = padding + _currentMap.DistanceBetweenFirstAndLastLayers() * unitsToPixelsMultiplier;
            float contentHeight = _mapContent != null && _mapContent.layout.height > 0f ? _mapContent.layout.height : 1080f;

            const float nodeSize = 64f;
            float left = contentWidth / 2f + anchoredPos.x - nodeSize / 2f;
            float top = contentHeight / 2f - anchoredPos.y - nodeSize / 2f;
            return new Vector2(left, top);
        }

        private Vector2 GetNodePosition(RoguelikeMapNode node)
        {
            if (_currentMap == null) return Vector2.zero;

            float length = padding + _currentMap.DistanceBetweenFirstAndLastLayers() * unitsToPixelsMultiplier;
            return new Vector2((padding - length) / 2f, -backgroundPadding.y / 2f) +
                   Flip(node.position) * unitsToPixelsMultiplier;
        }

        private static Vector2 Flip(Vector2 other) => new Vector2(other.y, other.x);

        public void SetAttainableNodes()
        {
            foreach (var node in _mapNodes)
                node.SetState(NodeStates.Locked);

            if (_currentMap.path.Count == 0)
            {
                foreach (var node in _mapNodes.Where(n => n.Node.point.y == 0))
                    node.SetState(NodeStates.Attainable);
            }
            else
            {
                foreach (var point in _currentMap.path)
                {
                    var mapNode = GetNode(point);
                    if (mapNode != null)
                        mapNode.SetState(NodeStates.Visited);
                }

                var currentPoint = _currentMap.path[_currentMap.path.Count - 1];
                var currentNode = _currentMap.GetNode(currentPoint);

                foreach (var point in currentNode.outgoing)
                {
                    var mapNode = GetNode(point);
                    if (mapNode != null)
                        mapNode.SetState(NodeStates.Attainable);
                }
            }
        }

        public void SetLineColors()
        {
            foreach (var line in _lineConnections)
                line.Color = lineLockedColor;

            if (_currentMap.path.Count == 0) return;

            var currentPoint = _currentMap.path[_currentMap.path.Count - 1];
            var currentNode = _currentMap.GetNode(currentPoint);

            foreach (var point in currentNode.outgoing)
            {
                var lineConnection = GetLineConnection(currentNode, GetNode(point)?.Node);
                if (lineConnection != null)
                    lineConnection.Color = lineVisitedColor;
            }

            if (_currentMap.path.Count <= 1) return;

            for (int i = 0; i < _currentMap.path.Count - 1; i++)
            {
                var current = _currentMap.path[i];
                var next = _currentMap.path[i + 1];
                var lineConnection = GetLineConnection(_currentMap.GetNode(current), _currentMap.GetNode(next));
                if (lineConnection != null)
                    lineConnection.Color = lineVisitedColor;
            }

            _linesElement?.Refresh();
        }

        private void DrawLines()
        {
            foreach (var node in _mapNodes)
            {
                foreach (var connection in node.Node.outgoing)
                    AddLineConnection(node, GetNode(connection));
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

        private RoguelikeMapUINode GetNode(Vector2Int p)
        {
            return _mapNodes.FirstOrDefault(n => n.Node.point.Equals(p));
        }

        private MapLineConnection GetLineConnection(RoguelikeMapNode from, RoguelikeMapNode to)
        {
            if (from == null || to == null) return null;
            return _lineConnections.FirstOrDefault(l => l.FromNode.point.Equals(from.point) && l.ToNode.point.Equals(to.point));
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

            if (_currentMap.path.Count == 0)
            {
                if (mapNode.Node.point.y == 0)
                    SendPlayerToNode(mapNode);
                else
                    PlayWarningThatNodeCannotBeAccessed();
            }
            else
            {
                var currentPoint = _currentMap.path[_currentMap.path.Count - 1];
                var currentNode = _currentMap.GetNode(currentPoint);

                if (currentNode != null && currentNode.outgoing.Any(point => point.Equals(mapNode.Node.point)))
                    SendPlayerToNode(mapNode);
                else
                    PlayWarningThatNodeCannotBeAccessed();
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
            _currentMap.path.Add(mapNode.Node.point);
            SaveMap();
            SetAttainableNodes();
            SetLineColors();
        }

        private void EnterNode(RoguelikeMapUINode mapNode)
        {
            Debug.Log("Entering node: " + mapNode.Node.blueprintName + " of type: " + mapNode.Node.nodeType);

            switch (mapNode.Node.nodeType)
            {
                case RoguelikeNodeType.MinorEnemy:
                case RoguelikeNodeType.EliteEnemy:
                case RoguelikeNodeType.Boss:
                    EnterBattleNode(mapNode);
                    break;
                case RoguelikeNodeType.RestSite:
                case RoguelikeNodeType.Treasure:
                case RoguelikeNodeType.Store:
                case RoguelikeNodeType.Mystery:
                    EnterStubNode(mapNode);
                    break;
            }
        }

        private async void EnterBattleNode(RoguelikeMapUINode mapNode)
        {
            var p = mapNode.Node.point;
            PlayerPrefs.SetString(RoguelikePendingNodePrefsKey, $"{p.x},{p.y}");
            PlayerPrefs.SetString(RoguelikeReturnScenePrefsKey, "Home");
            PlayerPrefs.Save();

            await BattleFlowCoordinator.Instance.StartBattleAsync(battleSceneName);
        }

        private void EnterStubNode(RoguelikeMapUINode mapNode)
        {
            Debug.Log($"[Roguelike stub] Node '{mapNode.Node.blueprintName}' ({mapNode.Node.nodeType})");
            StartCoroutine(CoUnlockAfterStub(mapNode));
        }

        private System.Collections.IEnumerator CoUnlockAfterStub(RoguelikeMapUINode mapNode)
        {
            yield return null;
            CommitPathForNode(mapNode);
            _locked = false;
        }

        private void PlayWarningThatNodeCannotBeAccessed()
        {
            Debug.Log("Selected node cannot be accessed");
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

            Debug.Log("[RoguelikeMapUIController] No close/back button found in UXML.");
        }

        private static void OnCloseClicked()
        {
            RoguelikeFlowCoordinator.Instance.CloseMap();
        }

        private void OnDestroy()
        {
            ClearMap();
        }

        private void OnApplicationQuit()
        {
            SaveMap();
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
