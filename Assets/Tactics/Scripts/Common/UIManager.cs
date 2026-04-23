using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tactics.AssetPipeline;
using Tactics.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

namespace Tactics
{
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
                }
                return _uiRoot;
            }
        }

        private const string RoguelikeMapPrefabPath = "Assets/Tactics/Arts/UI/RoguelikeMap.prefab";

        private const string HomeUxmlPath = "Assets/Tactics/Arts/UI/Home.uxml";
        private const string HomeUssPath = "Assets/Tactics/Arts/UI/Home.uss";
        private const string MenuUxmlPath = "Assets/Tactics/Arts/UI/Menu.uxml";
        private const string MenuUssPath = "Assets/Tactics/Arts/UI/Menu.uss";
        private const string BattleUxmlPath = "Assets/Tactics/Arts/UI/Battle.uxml";
        private const string BattleUssPath = "Assets/Tactics/Arts/UI/Battle.uss";
        private const string CheatConsoleUxmlPath = "Assets/Tactics/Arts/UI/CheatConsole.uxml";
        private const string CheatConsoleUssPath = "Assets/Tactics/Arts/UI/CheatConsole.uss";
        private const string PanelSettingsPath = "Assets/Tactics/UIToolkit/PanelSettings.asset";

        private static readonly Dictionary<UIId, UIType> s_uiTypeMap = new()
        {
            { UIId.Home, UIType.UiToolkitUxml },
            { UIId.Menu, UIType.UiToolkitUxml },
            { UIId.RoguelikeMap, UIType.UguiPrefab },
            { UIId.Battle, UIType.UiToolkitUxml },
            { UIId.CheatConsole, UIType.UiToolkitUxml },
        };

        private readonly Dictionary<UIId, UIInstance> _instances = new();
        private readonly Dictionary<UIId, Task<UIInstance>> _loadingTasks = new();

        private PanelSettings _panelSettings;

        private async Task<PanelSettings> GetPanelSettingsAsync(GameAssetManager mgr)
        {
            if (_panelSettings == null)
            {
                _panelSettings = await mgr.LoadAsync<PanelSettings>(PanelSettingsPath);
                if (_panelSettings == null)
                    Debug.LogWarning("[UIManager] Failed to load PanelSettings. UIDocument may not render correctly.");
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

        private void EnsureInputInitialized()
        {
            if (_inputInitialized) return;

            var module = UnityEngine.Object.FindFirstObjectByType<InputSystemUIInputModule>();
            if (module == null || module.actionsAsset == null)
                return;

            var playerMap = module.actionsAsset.FindActionMap("Player");
            if (playerMap == null)
                return;

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
            return id switch
            {
                UIId.Home => HomeUxmlPath,
                UIId.Menu => MenuUxmlPath,
                UIId.RoguelikeMap => RoguelikeMapPrefabPath,
                UIId.Battle => BattleUxmlPath,
                UIId.CheatConsole => CheatConsoleUxmlPath,
                _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown UIId asset mapping.")
            };
        }

        private static string GetUssPath(UIId id)
        {
            return id switch
            {
                UIId.Home => HomeUssPath,
                UIId.Menu => MenuUssPath,
                UIId.Battle => BattleUssPath,
                UIId.CheatConsole => CheatConsoleUssPath,
                _ => string.Empty
            };
        }

        private static UIType GetUIType(UIId id)
        {
            return s_uiTypeMap.TryGetValue(id, out var type) ? type : throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown UIId type mapping.");
        }

        private async Task ShowUIAsync(UIId id, string assetPath)
        {
            if (_instances.TryGetValue(id, out var existing) && existing?.ContainerGO != null)
            {
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

        private async Task<UIInstance> LoadUguiPrefabAsync(UIId id, string prefabPath, GameAssetManager mgr)
        {
            var uiRoot = UiRoot;
            if (uiRoot == null)
                throw new InvalidOperationException($"[UIManager] No Canvas found for UGUI prefab: {prefabPath}. Add a Canvas or call SetUiRoot().");

            var prefab = await mgr.LoadAsync<GameObject>(prefabPath);
            if (prefab == null)
                throw new InvalidOperationException($"[UIManager] Failed to load prefab: {prefabPath}");

            var go = UnityEngine.Object.Instantiate(prefab, uiRoot, false);
            go.name = id.ToString();
            EnsureUIController(id, go);
            go.SetActive(false);
            return new UIInstance(UIType.UguiPrefab, go, null);
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

            var hostGo = new GameObject(id.ToString());

            var uiDoc = hostGo.AddComponent<UIDocument>();
            uiDoc.visualTreeAsset = visualTree;
            uiDoc.panelSettings = await GetPanelSettingsAsync(mgr);

            if (styleSheet != null && uiDoc.rootVisualElement != null)
                uiDoc.rootVisualElement.styleSheets.Add(styleSheet);

            EnsureUIController(id, hostGo);

            hostGo.SetActive(false);

            return new UIInstance(UIType.UiToolkitUxml, hostGo, uiDoc);
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
                default:
                    break;
            }
        }
    }
}
