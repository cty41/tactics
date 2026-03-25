using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using Tactics.Flow.Roguelike;
using Tactics.RoguelikeMap;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UI.Extensions;
using Newtonsoft.Json;

namespace Tactics.UI
{
    public enum MapOrientation
    {
        BottomToTop,
        TopToBottom,
        RightToLeft,
        LeftToRight
    }

    public sealed class RoguelikeMapUIController : UIControllerBase
    {
        public const string MapPlayerPrefsKey = "RoguelikeMap";
        public const string RoguelikePendingNodePrefsKey = "RoguelikePendingNode";
        public const string RoguelikeReturnScenePrefsKey = "RoguelikeReturnScene";

        [Header("UI Prefabs")]
        [Tooltip("Prefab for map nodes (must have RoguelikeMapUINode component)")]
        public GameObject nodePrefab;
        [Tooltip("Prefab for UI line between nodes")]
        public UILineRenderer uiLinePrefab;

        [Header("Scroll Rects")]
        public ScrollRect scrollRectHorizontal;
        public ScrollRect scrollRectVertical;

        [Header("Layout")]
        public float unitsToPixelsMultiplier = 10f;
        public float padding = 400f;
        public Vector2 backgroundPadding = new Vector2(-100f, -100f);
        public float backgroundPPUMultiplier = 2f;
        public float offsetFromNodes = 15f;
        [Range(3, 10)]
        public int linePointsCount = 10;

        [Header("Colors")]
        public Color32 visitedColor = Color.white;
        public Color32 lockedColor = Color.gray;
        public Color32 lineVisitedColor = Color.white;
        public Color32 lineLockedColor = Color.gray;

        [Header("Orientation")]
        public MapOrientation orientation = MapOrientation.BottomToTop;

        [Header("Background")]
        public Image backgroundImage;

        [Header("Map Data")]
        public RoguelikeMapConfig mapConfig;

        [Header("Battle Scene")]
        [SerializeField] private string battleSceneName = "Test1";

        private global::Tactics.RoguelikeMap.RoguelikeMap _currentMap;
        private GameObject _firstParent;
        private ScrollRect _activeScrollRect;
        private readonly List<RoguelikeMapUINode> _mapNodes = new List<RoguelikeMapUINode>();
        private readonly List<UILineRenderer> _lineConnections = new List<UILineRenderer>();
        private int _lineInstanceIndex;
        private bool _wired;
        private bool _locked;

        public static RoguelikeMapUIController Instance { get; set; }

        private void Awake()
        {
            Instance = this;
        }

        protected override void OnShown()
        {
            if (_wired) return;
            WireOptionalCloseButtons();
            _wired = true;

            LoadOrGenerateMap();
            if (_currentMap != null)
            {
                ShowMap(_currentMap);
            }
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

        private bool TryEnsureScrollRectsAssigned()
        {
            if (scrollRectHorizontal != null && scrollRectVertical != null) return true;

            ScrollRect[] local = GetComponentsInChildren<ScrollRect>(true);
            AssignScrollRectsFrom(local);

            if (scrollRectHorizontal == null || scrollRectVertical == null)
            {
                CreateRuntimeScrollRectsIfMissing();
            }

            return scrollRectHorizontal != null && scrollRectVertical != null;
        }

        private void AssignScrollRectsFrom(ScrollRect[] candidates)
        {
            foreach (ScrollRect sr in candidates)
            {
                if (sr == null) continue;
                string n = sr.name.ToLowerInvariant();

                if (scrollRectHorizontal == null && (n.Contains("horizontal") || (sr.horizontal && !sr.vertical)))
                    scrollRectHorizontal = sr;

                if (scrollRectVertical == null && (n.Contains("vertical") || (sr.vertical && !sr.horizontal)))
                    scrollRectVertical = sr;
            }
        }

        private void CreateRuntimeScrollRectsIfMissing()
        {
            RectTransform host = GetComponent<RectTransform>();
            if (host == null)
                host = GetComponentInParent<RectTransform>();
            if (host == null) return;

            if (scrollRectHorizontal == null)
                scrollRectHorizontal = CreateRuntimeScrollRect(host, "MapScrollRectHorizontal", horizontal: true);

            if (scrollRectVertical == null)
                scrollRectVertical = CreateRuntimeScrollRect(host, "MapScrollRectVertical", horizontal: false);
        }

        private static ScrollRect CreateRuntimeScrollRect(RectTransform host, string name, bool horizontal)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
            RectTransform rootRT = root.GetComponent<RectTransform>();
            rootRT.SetParent(host, false);
            rootRT.anchorMin = Vector2.zero;
            rootRT.anchorMax = Vector2.one;
            rootRT.offsetMin = Vector2.zero;
            rootRT.offsetMax = Vector2.zero;
            rootRT.localScale = Vector3.one;

            Image viewportImage = root.GetComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);

            Mask mask = root.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject content = new GameObject("Content", typeof(RectTransform));
            RectTransform contentRT = content.GetComponent<RectTransform>();
            contentRT.SetParent(rootRT, false);
            contentRT.anchorMin = Vector2.zero;
            contentRT.anchorMax = Vector2.one;
            contentRT.offsetMin = Vector2.zero;
            contentRT.offsetMax = Vector2.zero;
            contentRT.localScale = Vector3.one;

            ScrollRect sr = root.GetComponent<ScrollRect>();
            sr.viewport = rootRT;
            sr.content = contentRT;
            sr.horizontal = horizontal;
            sr.vertical = !horizontal;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.inertia = true;
            sr.scrollSensitivity = 1f;
            sr.gameObject.SetActive(false);
            return sr;
        }

        public void ShowMap(global::Tactics.RoguelikeMap.RoguelikeMap m)
        {
            if (m == null)
            {
                Debug.LogWarning("Map was null in RoguelikeMapUIController.ShowMap()");
                return;
            }

            if (!TryEnsureScrollRectsAssigned())
                return;

            _currentMap = m;
            ClearMap();
            _lineInstanceIndex = 0;

            CreateMapParent();
            CreateNodes(m.nodes);
            DrawLines();
            SetMapLength();
            ScrollToOrigin();
            ResetNodesRotation();
            SetAttainableNodes();
            SetLineColors();
        }

        private void ClearMap()
        {
            if (scrollRectHorizontal != null)
                scrollRectHorizontal.gameObject.SetActive(false);
            if (scrollRectVertical != null)
                scrollRectVertical.gameObject.SetActive(false);

            if (_activeScrollRect != null && _activeScrollRect.content != null)
            {
                foreach (Transform t in _activeScrollRect.content)
                    Destroy(t.gameObject);
            }

            _mapNodes.Clear();
            _lineConnections.Clear();
        }

        private ScrollRect GetScrollRectForMap()
        {
            return orientation == MapOrientation.LeftToRight || orientation == MapOrientation.RightToLeft
                ? scrollRectHorizontal
                : scrollRectVertical;
        }

        private void CreateMapParent()
        {
            _activeScrollRect = GetScrollRectForMap();
            if (_activeScrollRect == null) return;

            _activeScrollRect.gameObject.SetActive(true);

            Transform contentParent = _activeScrollRect.content != null
                ? _activeScrollRect.content
                : _activeScrollRect.transform;

            _firstParent = new GameObject("OuterMapParent");
            _firstParent.transform.SetParent(contentParent);
            _firstParent.transform.localScale = Vector3.one;
            RectTransform fprt = _firstParent.AddComponent<RectTransform>();
            Stretch(fprt);
        }

        private void SetMapLength()
        {
            if (_activeScrollRect == null || _activeScrollRect.content == null || _currentMap == null) return;

            RectTransform rt = _activeScrollRect.content;
            Vector2 sizeDelta = rt.sizeDelta;
            float length = padding + _currentMap.DistanceBetweenFirstAndLastLayers() * unitsToPixelsMultiplier;
            if (orientation == MapOrientation.LeftToRight || orientation == MapOrientation.RightToLeft)
                sizeDelta.x = length;
            else
                sizeDelta.y = length;
            rt.sizeDelta = sizeDelta;
        }

        private void ScrollToOrigin()
        {
            if (_activeScrollRect == null) return;

            switch (orientation)
            {
                case MapOrientation.BottomToTop:
                    _activeScrollRect.normalizedPosition = Vector2.zero;
                    break;
                case MapOrientation.TopToBottom:
                    _activeScrollRect.normalizedPosition = new Vector2(0, 1);
                    break;
                case MapOrientation.RightToLeft:
                    _activeScrollRect.normalizedPosition = new Vector2(1, 0);
                    break;
                case MapOrientation.LeftToRight:
                    _activeScrollRect.normalizedPosition = Vector2.zero;
                    break;
            }
        }

        private static void Stretch(RectTransform tr)
        {
            tr.localPosition = Vector3.zero;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.sizeDelta = Vector2.zero;
            tr.anchoredPosition = Vector2.zero;
        }

        private void CreateNodes(IEnumerable<RoguelikeMapNode> nodes)
        {
            int nodeIndex = 0;
            foreach (var node in nodes)
            {
                RoguelikeMapUINode mapNode = CreateMapNode(node, nodeIndex++);
                _mapNodes.Add(mapNode);
            }
        }

        private RoguelikeMapUINode CreateMapNode(RoguelikeMapNode node, int instanceIndex)
        {
            if (nodePrefab == null)
            {
                Debug.LogError("[RoguelikeMapUIController] nodePrefab is null!");
                return null;
            }

            GameObject mapNodeObject = Instantiate(nodePrefab, _firstParent.transform);
            mapNodeObject.name = $"Node_{node.nodeType}_{instanceIndex}";
            RoguelikeMapUINode mapNode = mapNodeObject.GetComponent<RoguelikeMapUINode>();
            if (mapNode == null)
            {
                Debug.LogError($"[RoguelikeMapUIController] nodePrefab missing RoguelikeMapUINode component: {nodePrefab.name}");
                return null;
            }

            RoguelikeNodeBlueprint blueprint = GetBlueprint(node.blueprintName);
            mapNode.SetUp(node, blueprint, visitedColor, lockedColor);
            mapNode.transform.localPosition = GetNodePosition(node);
            return mapNode;
        }

        private Vector2 GetNodePosition(RoguelikeMapNode node)
        {
            if (_currentMap == null) return Vector2.zero;

            float length = padding + _currentMap.DistanceBetweenFirstAndLastLayers() * unitsToPixelsMultiplier;

            switch (orientation)
            {
                case MapOrientation.BottomToTop:
                    return new Vector2(-backgroundPadding.x / 2f, (padding - length) / 2f) +
                           node.position * unitsToPixelsMultiplier;
                case MapOrientation.TopToBottom:
                    return new Vector2(backgroundPadding.x / 2f, (length - padding) / 2f) -
                           node.position * unitsToPixelsMultiplier;
                case MapOrientation.RightToLeft:
                    return new Vector2((length - padding) / 2f, backgroundPadding.y / 2f) -
                           Flip(node.position) * unitsToPixelsMultiplier;
                case MapOrientation.LeftToRight:
                    return new Vector2((padding - length) / 2f, -backgroundPadding.y / 2f) +
                           Flip(node.position) * unitsToPixelsMultiplier;
                default:
                    return Vector2.zero;
            }
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
            foreach (UILineRenderer lr in _lineConnections)
                lr.color = lineLockedColor;

            if (_currentMap.path.Count == 0) return;

            var currentPoint = _currentMap.path[_currentMap.path.Count - 1];
            var currentNode = _currentMap.GetNode(currentPoint);

            foreach (var point in currentNode.outgoing)
            {
                var lineConnection = GetLineConnection(currentNode, GetNode(point)?.Node);
                if (lineConnection != null)
                    lineConnection.color = lineVisitedColor;
            }

            if (_currentMap.path.Count <= 1) return;

            for (int i = 0; i < _currentMap.path.Count - 1; i++)
            {
                var current = _currentMap.path[i];
                var next = _currentMap.path[i + 1];
                var lineConnection = GetLineConnection(_currentMap.GetNode(current), _currentMap.GetNode(next));
                if (lineConnection != null)
                    lineConnection.color = lineVisitedColor;
            }
        }

        private void DrawLines()
        {
            foreach (var node in _mapNodes)
            {
                foreach (var connection in node.Node.outgoing)
                    AddLineConnection(node, GetNode(connection));
            }
        }

        private void AddLineConnection(RoguelikeMapUINode from, RoguelikeMapUINode to)
        {
            if (uiLinePrefab == null || from == null || to == null) return;

            UILineRenderer lineRenderer = Instantiate(uiLinePrefab, _firstParent.transform);
            lineRenderer.gameObject.name = $"{uiLinePrefab.gameObject.name}_{_lineInstanceIndex++}";
            lineRenderer.transform.SetAsFirstSibling();

            RectTransform fromRT = from.transform as RectTransform;
            RectTransform toRT = to.transform as RectTransform;

            if (fromRT == null || toRT == null) return;

            Vector2 fromPoint = fromRT.anchoredPosition +
                                (toRT.anchoredPosition - fromRT.anchoredPosition).normalized * offsetFromNodes;

            Vector2 toPoint = toRT.anchoredPosition +
                              (fromRT.anchoredPosition - toRT.anchoredPosition).normalized * offsetFromNodes;

            var list = new List<Vector2>();
            for (int i = 0; i < linePointsCount; i++)
            {
                list.Add(Vector3.Lerp(Vector3.zero, toPoint - fromPoint +
                                                    2 * (fromRT.anchoredPosition - toRT.anchoredPosition).normalized *
                                                    offsetFromNodes, (float)i / (linePointsCount - 1)));
            }

            lineRenderer.Points = list.ToArray();
            _lineConnections.Add(lineRenderer);
        }

        private void ResetNodesRotation()
        {
            foreach (var node in _mapNodes)
                node.transform.rotation = Quaternion.identity;
        }

        private RoguelikeMapUINode GetNode(Vector2Int p)
        {
            return _mapNodes.FirstOrDefault(n => n.Node.point.Equals(p));
        }

        private UILineRenderer GetLineConnection(RoguelikeMapNode from, RoguelikeMapNode to)
        {
            if (from == null || to == null) return null;

            for (int i = 0; i < _lineConnections.Count; i++)
            {
                var fromNode = _mapNodes.ElementAtOrDefault(i);
                if (fromNode == null) continue;

                bool matchesFrom = fromNode.Node.point.Equals(from.point);
                bool matchesTo = fromNode.Node.outgoing.Contains(to.point);

                if (matchesFrom && matchesTo)
                    return _lineConnections[i];
            }

            return null;
        }

        private RoguelikeNodeBlueprint GetBlueprint(string blueprintName)
        {
            if (mapConfig == null) return null;
            return mapConfig.nodeBlueprints.FirstOrDefault(n => n.name == blueprintName);
        }

        public void SelectNode(RoguelikeMapUINode mapNode)
        {
            if (_locked) return;

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

        private void EnterBattleNode(RoguelikeMapUINode mapNode)
        {
            var p = mapNode.Node.point;
            PlayerPrefs.SetString(RoguelikePendingNodePrefsKey, $"{p.x},{p.y}");
            PlayerPrefs.SetString(RoguelikeReturnScenePrefsKey, "Home");
            PlayerPrefs.Save();

            Tactics.AssetPipeline.SceneProjectPathHelper.TryLoadSceneViaAssetManager(battleSceneName);
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
            bool wired = false;
            Button[] allButtons = GetComponentsInChildren<Button>(true);
            foreach (Button button in allButtons)
            {
                if (button == null) continue;

                string name = (button.name ?? string.Empty).Trim();
                if (name.Equals("CloseButton", System.StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("BackButton", System.StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("EscButton", System.StringComparison.OrdinalIgnoreCase))
                {
                    button.onClick.AddListener(OnCloseClicked);
                    wired = true;
                    continue;
                }

                TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                string text = (label?.text ?? string.Empty).Trim();
                if (text.Equals("CLOSE", System.StringComparison.OrdinalIgnoreCase) ||
                    text.Equals("BACK", System.StringComparison.OrdinalIgnoreCase))
                {
                    button.onClick.AddListener(OnCloseClicked);
                    wired = true;
                }
            }

            if (!wired)
                Debug.Log("[RoguelikeMapUIController] No close/back button found.");
        }

        private static void OnCloseClicked()
        {
            RoguelikeFlowCoordinator.Instance.CloseMap();
        }

        private void OnApplicationQuit()
        {
            SaveMap();
        }
    }
}