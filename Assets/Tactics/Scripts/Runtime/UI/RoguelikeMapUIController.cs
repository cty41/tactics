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
    public sealed class RoguelikeMapUIController : UIControllerBase
    {
        public const string MapPlayerPrefsKey = "RoguelikeMap";
        public const string RoguelikePendingNodePrefsKey = "RoguelikePendingNode";
        public const string RoguelikeReturnScenePrefsKey = "RoguelikeReturnScene";

        [Header("UI Prefabs")]
        [Tooltip("Prefab for map nodes (must have RoguelikeMapUINode component)")]
        public GameObject nodePrefab;

        [Header("Scroll Rect")]
        public ScrollRect scrollRect;

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

        [Header("Background")]
        public Image backgroundImage;

        [Header("Map Data")]
        public RoguelikeMapConfig mapConfig;

        [Header("Battle Scene")]
        [SerializeField] private string battleSceneName = "Test1";

        private global::Tactics.RoguelikeMap.RoguelikeMap _currentMap;
        private GameObject _firstParent;
        private RectTransform _lineParent;
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

        private bool TryEnsureScrollRectAssigned()
        {
            if (scrollRect != null) return true;

            ScrollRect[] local = GetComponentsInChildren<ScrollRect>(true);
            foreach (ScrollRect sr in local)
            {
                if (sr == null) continue;
                scrollRect = sr;
                break;
            }

            if (scrollRect == null)
                CreateRuntimeScrollRectIfMissing();

            return scrollRect != null;
        }

        private void CreateRuntimeScrollRectIfMissing()
        {
            RectTransform host = GetComponent<RectTransform>();
            if (host == null)
                host = GetComponentInParent<RectTransform>();
            if (host == null) return;

            GameObject root = new GameObject("ScrollRectHorizontal", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
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
            sr.horizontal = true;
            sr.vertical = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.inertia = true;
            sr.scrollSensitivity = 1f;
            sr.gameObject.SetActive(false);

            scrollRect = sr;
        }

        public void ShowMap(global::Tactics.RoguelikeMap.RoguelikeMap m)
        {
            if (m == null)
            {
                Debug.LogWarning("Map was null in RoguelikeMapUIController.ShowMap()");
                return;
            }

            if (!TryEnsureScrollRectAssigned())
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
            if (scrollRect != null)
                scrollRect.gameObject.SetActive(false);

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
            return scrollRect;
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

            GameObject lineParentObj = new GameObject("LinesParent");
            lineParentObj.transform.SetParent(_firstParent.transform);
            lineParentObj.transform.localScale = Vector3.one;
            _lineParent = lineParentObj.AddComponent<RectTransform>();
            _lineParent.localPosition = Vector3.zero;
            _lineParent.anchorMin = new Vector2(0.5f, 0.5f);
            _lineParent.anchorMax = new Vector2(0.5f, 0.5f);
            _lineParent.sizeDelta = Vector2.zero;
            _lineParent.anchoredPosition = Vector2.zero;
        }

        private void SetMapLength()
        {
            if (_activeScrollRect == null || _activeScrollRect.content == null || _currentMap == null) return;

            RectTransform rt = _activeScrollRect.content;
            Vector2 sizeDelta = rt.sizeDelta;
            sizeDelta.x = padding + _currentMap.DistanceBetweenFirstAndLastLayers() * unitsToPixelsMultiplier;
            rt.sizeDelta = sizeDelta;
        }

        private void ScrollToOrigin()
        {
            if (_activeScrollRect == null) return;
            _activeScrollRect.normalizedPosition = Vector2.zero;
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
            if (from == null || to == null) return;

            GameObject lineObj = new GameObject($"UILineRenderer_{_lineInstanceIndex++}");
            lineObj.transform.SetParent(_lineParent.transform);
            lineObj.transform.SetAsFirstSibling();

            RectTransform lineRT = lineObj.AddComponent<RectTransform>();
            lineRT.localPosition = Vector3.zero;
            lineRT.anchorMin = new Vector2(0.5f, 0.5f);
            lineRT.anchorMax = new Vector2(0.5f, 0.5f);
            lineRT.pivot = new Vector2(0.5f, 0.5f);
            lineRT.anchoredPosition = Vector2.zero;
            lineRT.sizeDelta = Vector2.zero;
            lineRT.localScale = Vector3.one;

            UILineRenderer lineRenderer = lineObj.AddComponent<UILineRenderer>();

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