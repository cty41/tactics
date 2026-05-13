using System;
using Tactics.Runtime.Utilities;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tactics.AssetPipeline;
using Tactics.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.UIElements;
using System.IO;

namespace Tactics
{
    [Serializable]
    internal class UIConfigEntry
    {
        public string id;
        public string type;
        public string uxml;
        public string uss;
    }

    [Serializable]
    internal class UIConfig
    {
        public List<UIConfigEntry> uis;
    }

    public sealed class UIManager
    {
        private static readonly UIManager _instance = new UIManager();
        public static UIManager Instance => _instance;

        private UIManager() { }

        public enum UIId
        {
            Home,
            Menu,
            RoguelikeMap,
            Battle,
            CheatConsole,
            Loading,
            Inventory,
            BattleSettlement,
            AttributeAllocation,
            SkillSelection,
            LevelUp,
        }

        private enum UIType
        {
            UguiPrefab,
            UiToolkitUxml,
        }

        private sealed class UIInstance
        {
            public UIType Type { get; }
            public GameObject ContainerGO { get; }
            public UIDocument UiDoc { get; }

            public VisualElement RootVE => UiDoc?.rootVisualElement;

            public UIInstance(UIType type, GameObject containerGo, UIDocument uiDoc = null)
            {
                Type = type;
                ContainerGO = containerGo;
                UiDoc = uiDoc;
            }
        }

        private RectTransform _uiRoot;

        /// <summary>
        /// Manually set the UI root before any UI is loaded. Optional - auto-detects Canvas if not set.
        /// </summary>
        public void SetUiRoot(RectTransform root) => _uiRoot = root;

        private RectTransform UiRoot
        {
            get
            {
                if (_uiRoot == null)
                {
                    var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
                    if (canvas != null)
                    {
                        _uiRoot = canvas.GetComponent<RectTransform>();
                    }
                    else
                    {
                        var canvasGo = new GameObject("Canvas");
                        canvas = canvasGo.AddComponent<Canvas>();
                        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                        canvasGo.AddComponent<CanvasScaler>();
                        canvasGo.AddComponent<GraphicRaycaster>();
                        _uiRoot = canvas.GetComponent<RectTransform>();
                    }
                }
                return _uiRoot;
            }
        }

        private const string PanelSettingsPath = "Assets/Tactics/UIToolkit/PanelSettings.asset";

        private static Dictionary<UIId, UIType> s_uiTypeMap;
        private static Dictionary<UIId, (string uxml, string uss)> s_uiPaths;
        private static bool s_configLoaded;

        private static void EnsureConfigLoaded()
        {
            if (s_configLoaded) return;
            LoadConfig();
            s_configLoaded = true;
        }

        private static void LoadConfig()
        {
            const string configPath = "Assets/Tactics/GameData/ui_config.json";

            string json = null;
            var mgr = GameAssetManager.Instance;

            if (mgr != null && mgr.IsInitialized)
            {
                var textAsset = mgr.Load<TextAsset>(configPath);
                if (textAsset != null)
                {
                    json = textAsset.text;
                    mgr.Release(configPath);
                }
            }

            #if UNITY_EDITOR
            if (json == null && File.Exists(configPath))
            {
                json = File.ReadAllText(configPath);
            }
#endif

            if (json == null)
            {
                TLog.Error($"[UIManager] ui_config.json not found at {configPath}");
                s_uiPaths = new Dictionary<UIId, (string, string)>();
                s_uiTypeMap = new Dictionary<UIId, UIType>();
                return;
            }

            var config = JsonUtility.FromJson<UIConfig>(json);
            if (config?.uis == null)
            {
                TLog.Error("[UIManager] Failed to parse ui_config.json");
                s_uiPaths = new Dictionary<UIId, (string, string)>();
                s_uiTypeMap = new Dictionary<UIId, UIType>();
                return;
            }

            s_uiPaths = new Dictionary<UIId, (string, string)>(config.uis.Count);
            s_uiTypeMap = new Dictionary<UIId, UIType>(config.uis.Count);

            foreach (var entry in config.uis)
            {
                if (Enum.TryParse<UIId>(entry.id, out var uiId) &&
                    Enum.TryParse<UIType>(entry.type, out var uiType))
                {
                    s_uiPaths[uiId] = (entry.uxml, entry.uss);
                    s_uiTypeMap[uiId] = uiType;
                }
                else
                {
                    TLog.Warning($"[UIManager] Invalid UI config entry: id={entry.id}, type={entry.type}");
                }
            }
        }

        private readonly Dictionary<UIId, UIInstance> _instances = new();
        private readonly Dictionary<UIId, Task<UIInstance>> _loadingTasks = new();

        private PanelSettings _panelSettings;

        private async Task<PanelSettings> GetPanelSettingsAsync(GameAssetManager mgr)
        {
            if (_panelSettings == null)
            {
                _panelSettings = await mgr.LoadAsync<PanelSettings>(PanelSettingsPath);
                if (_panelSettings == null)
                    TLog.Warning("[UIManager] Failed to load PanelSettings. UIDocument may not render correctly.");
            }
            return _panelSettings;
        }

        private PanelSettings GetPanelSettingsSync(GameAssetManager mgr)
        {
            if (_panelSettings == null)
            {
                _panelSettings = mgr.Load<PanelSettings>(PanelSettingsPath);
                if (_panelSettings == null)
                    TLog.Warning("[UIManager] Failed to load PanelSettings. UIDocument may not render correctly.");
            }
            return _panelSettings;
        }

        private InputAction _toggleConsoleAction;
        private bool _inputInitialized;

        public Task ShowAsync(UIId id)
        {
            EnsureInputInitialized();
            return ShowUIAsync(id, GetAssetPath(id));
        }

        /// <summary>
        /// Synchronously show a UI. Uses <see cref="GameAssetManager.Load{T}"/> internally.
        /// Note: this blocks the calling thread during asset loading; use <see cref="ShowAsync"/> for non-blocking loads.
        /// </summary>
        public void Show(UIId id)
        {
            EnsureInputInitialized();

            if (_instances.TryGetValue(id, out var existing) && existing?.ContainerGO != null)
            {
                existing.ContainerGO.SetActive(true);
                return;
            }

            var instance = LoadAndCreateSync(id, GetAssetPath(id));
            _instances[id] = instance;
            instance.ContainerGO.SetActive(true);
        }

        private void EnsureInputInitialized()
        {
            if (_inputInitialized) return;

            var module = UnityEngine.Object.FindFirstObjectByType<InputSystemUIInputModule>();
            if (module == null || module.actionsAsset == null)
                return;

            var playerMap = module.actionsAsset.FindActionMap("Player");
            if (playerMap == null)
                return;

            playerMap.Enable();

            _toggleConsoleAction = playerMap.FindAction("ToggleConsole");
            if (_toggleConsoleAction != null)
            {
                _toggleConsoleAction.performed += OnToggleConsolePerformed;
                _toggleConsoleAction.Enable();
            }

            _inputInitialized = true;
        }

        private void OnToggleConsolePerformed(InputAction.CallbackContext ctx)
        {
            if (IsVisible(UIId.CheatConsole))
                Hide(UIId.CheatConsole);
            else
                _ = ShowAsync(UIId.CheatConsole);
        }

        public void Hide(UIId id)
        {
            if (_instances.TryGetValue(id, out var instance) && instance?.ContainerGO != null)
                instance.ContainerGO.SetActive(false);
        }

        public void Destroy(UIId id)
        {
            if (_instances.TryGetValue(id, out var instance) && instance?.ContainerGO != null)
            {
                UnityEngine.Object.Destroy(instance.ContainerGO);
            }

            _instances.Remove(id);
        }

        public bool IsVisible(UIId id)
        {
            return _instances.TryGetValue(id, out var instance)
                && instance?.ContainerGO != null
                && instance.ContainerGO.activeSelf;
        }

        public VisualElement GetRootElement(UIId id)
        {
            return _instances.TryGetValue(id, out var instance) ? instance?.RootVE : null;
        }

        [Obsolete("Use ShowAsync(UIId.Menu) from a domain coordinator.")]
        public Task ShowMenuAsync() => ShowAsync(UIId.Menu);

        [Obsolete("Use Hide(UIId.Menu) from a domain coordinator.")]
        public void HideMenu() => Hide(UIId.Menu);

        [Obsolete("Use Destroy(UIId.Menu) from a domain coordinator.")]
        public void DestroyMenu() => Destroy(UIId.Menu);

        private static string GetAssetPath(UIId id)
        {
            EnsureConfigLoaded();
            if (s_uiPaths.TryGetValue(id, out var paths))
                return paths.uxml;
            throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown UIId asset mapping.");
        }

        private static string GetUssPath(UIId id)
        {
            EnsureConfigLoaded();
            if (s_uiPaths.TryGetValue(id, out var paths))
                return paths.uss;
            return string.Empty;
        }

        private static UIType GetUIType(UIId id)
        {
            EnsureConfigLoaded();
            return s_uiTypeMap.TryGetValue(id, out var type) ? type : throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown UIId type mapping.");
        }

        private async Task ShowUIAsync(UIId id, string assetPath)
        {
            if (_instances.TryGetValue(id, out var existing) && existing?.ContainerGO != null)
            {
                EnsureUIController(id, existing.ContainerGO);
                existing.ContainerGO.SetActive(true);
                return;
            }

            if (!_loadingTasks.TryGetValue(id, out var loadTask))
            {
                loadTask = LoadAndCreateAsync(id, assetPath);
                _loadingTasks[id] = loadTask;
            }

            var instance = await loadTask;
            _loadingTasks.Remove(id);

            _instances[id] = instance;
            instance.ContainerGO.SetActive(true);
        }

        private async Task<UIInstance> LoadAndCreateAsync(UIId id, string assetPath)
        {
            var mgr = GameAssetManager.Instance;
            if (mgr == null)
                throw new InvalidOperationException("[UIManager] GameAssetManager.Instance is null. Ensure bootstrap ran before calling UI methods.");

            if (!mgr.IsInitialized)
                throw new InvalidOperationException("[UIManager] GameAssetManager is not initialized. Call GameAssetManager.Initialize/InitializeAsync before calling UI methods.");

            return GetUIType(id) switch
            {
                UIType.UguiPrefab => await LoadUguiPrefabAsync(id, assetPath, mgr),
                UIType.UiToolkitUxml => await LoadUiToolkitAsync(id, assetPath, mgr),
                _ => throw new NotSupportedException("Unsupported UIType.")
            };
        }

        private UIInstance LoadAndCreateSync(UIId id, string assetPath)
        {
            var mgr = GameAssetManager.Instance;
            if (mgr == null)
                throw new InvalidOperationException("[UIManager] GameAssetManager.Instance is null. Ensure bootstrap ran before calling UI methods.");

            if (!mgr.IsInitialized)
                throw new InvalidOperationException("[UIManager] GameAssetManager is not initialized. Call GameAssetManager.Initialize/InitializeAsync before calling UI methods.");

            return GetUIType(id) switch
            {
                UIType.UguiPrefab => LoadUguiPrefabSync(id, assetPath, mgr),
                UIType.UiToolkitUxml => LoadUiToolkitSync(id, assetPath, mgr),
                _ => throw new NotSupportedException("Unsupported UIType.")
            };
        }

        private async Task<UIInstance> LoadUguiPrefabAsync(UIId id, string prefabPath, GameAssetManager mgr)
        {
            var prefab = await mgr.LoadAsync<GameObject>(prefabPath);
            if (prefab == null)
                throw new InvalidOperationException($"[UIManager] Failed to load prefab: {prefabPath}");

            return CreateUguiInstance(id, prefab);
        }

        private UIInstance CreateUguiInstance(UIId id, GameObject prefab)
        {
            var uiRoot = UiRoot;
            if (uiRoot == null)
                throw new InvalidOperationException("[UIManager] No Canvas found for UGUI prefab. Add a Canvas or call SetUiRoot().");

            var go = UnityEngine.Object.Instantiate(prefab, uiRoot, false);
            go.name = id.ToString();
            EnsureUIController(id, go);
            go.SetActive(false);
            return new UIInstance(UIType.UguiPrefab, go, null);
        }

        private UIInstance LoadUguiPrefabSync(UIId id, string prefabPath, GameAssetManager mgr)
        {
            var prefab = mgr.Load<GameObject>(prefabPath);
            if (prefab == null)
                throw new InvalidOperationException($"[UIManager] Failed to load prefab: {prefabPath}");

            return CreateUguiInstance(id, prefab);
        }

        private async Task<UIInstance> LoadUiToolkitAsync(UIId id, string uxmlPath, GameAssetManager mgr)
        {
            var visualTree = await mgr.LoadAsync<VisualTreeAsset>(uxmlPath);
            if (visualTree == null)
                throw new InvalidOperationException($"[UIManager] Failed to load VisualTreeAsset: {uxmlPath}");

            StyleSheet styleSheet = null;
            var ussPath = GetUssPath(id);
            if (!string.IsNullOrEmpty(ussPath))
            {
                styleSheet = await mgr.LoadAsync<StyleSheet>(ussPath);
            }

            var panelSettings = await GetPanelSettingsAsync(mgr);

            return CreateUiToolkitInstance(id, visualTree, styleSheet, panelSettings);
        }

        private UIInstance CreateUiToolkitInstance(UIId id, VisualTreeAsset visualTree, StyleSheet styleSheet, PanelSettings panelSettings)
        {
            var hostGo = new GameObject(id.ToString());

            var uiDoc = hostGo.AddComponent<UIDocument>();
            uiDoc.visualTreeAsset = visualTree;
            uiDoc.panelSettings = panelSettings;

            if (styleSheet != null && uiDoc.rootVisualElement != null)
                uiDoc.rootVisualElement.styleSheets.Add(styleSheet);

            EnsureUIController(id, hostGo);

            hostGo.SetActive(false);

            return new UIInstance(UIType.UiToolkitUxml, hostGo, uiDoc);
        }

        private UIInstance LoadUiToolkitSync(UIId id, string uxmlPath, GameAssetManager mgr)
        {
            var visualTree = mgr.Load<VisualTreeAsset>(uxmlPath);
            if (visualTree == null)
                throw new InvalidOperationException($"[UIManager] Failed to load VisualTreeAsset: {uxmlPath}");

            StyleSheet styleSheet = null;
            var ussPath = GetUssPath(id);
            if (!string.IsNullOrEmpty(ussPath))
            {
                styleSheet = mgr.Load<StyleSheet>(ussPath);
            }

            var panelSettings = GetPanelSettingsSync(mgr);

            return CreateUiToolkitInstance(id, visualTree, styleSheet, panelSettings);
        }

        private static void EnsureUIController(UIId id, GameObject root)
        {
            switch (id)
            {
                case UIId.Home:
                    if (root.GetComponent<HomeUIController>() == null)
                        root.AddComponent<HomeUIController>();
                    break;
                case UIId.Menu:
                    if (root.GetComponent<MenuUIController>() == null)
                        root.AddComponent<MenuUIController>();
                    break;
                case UIId.RoguelikeMap:
                    if (root.GetComponent<RoguelikeMapUIController>() == null)
                        root.AddComponent<RoguelikeMapUIController>();
                    break;
                case UIId.Battle:
                    if (root.GetComponent<BattleUIController>() == null)
                        root.AddComponent<BattleUIController>();
                    break;
                case UIId.CheatConsole:
                    if (root.GetComponent<CheatConsoleUI>() == null)
                        root.AddComponent<CheatConsoleUI>();
                    break;
                case UIId.Loading:
                    // Loading UI has no controller; purely visual.
                    break;
                case UIId.Inventory:
                    if (root.GetComponent<InventoryUIController>() == null)
                        root.AddComponent<InventoryUIController>();
                    break;
                case UIId.BattleSettlement:
                    if (root.GetComponent<BattleSettlementUIController>() == null)
                        root.AddComponent<BattleSettlementUIController>();
                    break;
                case UIId.AttributeAllocation:
                    if (root.GetComponent<AttributeAllocationUIController>() == null)
                        root.AddComponent<AttributeAllocationUIController>();
                    break;
                case UIId.SkillSelection:
                    if (root.GetComponent<SkillSelectionUIController>() == null)
                        root.AddComponent<SkillSelectionUIController>();
                    break;
                case UIId.LevelUp:
                    if (root.GetComponent<LevelUpPanelController>() == null)
                        root.AddComponent<LevelUpPanelController>();
                    break;
                default:
                    break;
            }
        }
    }
}
