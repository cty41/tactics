using System;
using System.IO;
using Tactics.Runtime.Utilities;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tactics.AssetPipeline;
using Tactics.Common.Battle;
using Tactics.Common.Controllers.GridStates;
using Tactics.Flow.Home;
using Tactics.RoguelikeMap.UI;
using Tactics.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.UIElements;
using AtlasPopulationMode = UnityEngine.TextCore.Text.AtlasPopulationMode;
using GlyphRenderMode = UnityEngine.TextCore.LowLevel.GlyphRenderMode;
using TextCoreFontAsset = UnityEngine.TextCore.Text.FontAsset;

namespace Tactics
{
    internal sealed class RuntimeDefaultFontOwner : ScriptableObject
    {
        [SerializeField] private string marker;
        [SerializeField] private Font source;
        [SerializeField] private TextCoreFontAsset fontAsset;

        internal string Marker => marker;
        internal Font Source => source;
        internal TextCoreFontAsset FontAsset => fontAsset;

        internal void Initialize(string ownerMarker, Font fontSource, TextCoreFontAsset ownedFontAsset)
        {
            marker = ownerMarker;
            source = fontSource;
            fontAsset = ownedFontAsset;
        }
    }

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_configLoaded = false;
            s_uiPaths = null;
            s_uiTypeMap = null;

            // Native DontSave objects can outlive managed statics. Preserve every owned graph
            // before dropping managed references; recovery waits for the expected source TTF.
            SynchronizeOwnedRuntimeDefaultFonts();
            _instance._runtimeDefaultFontSource = null;
            _instance._runtimeDefaultFontAsset = null;
            _instance._runtimeDefaultFontOwner = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterRuntimeDefaultFontQuitBoundary()
        {
            Application.quitting -= SynchronizeOwnedRuntimeDefaultFonts;
            Application.quitting += SynchronizeOwnedRuntimeDefaultFonts;
        }


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
            TreasurePanel,
            ShopPanel,
            RestSitePanel,
            EventPanel,
            RunEndSummary,
            Options,
            SlotSelect,
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
        private const string RuntimeDefaultFontSourcePath = "Assets/Tactics/Arts/Fonts/NotoSansSC.ttf";
        private const string RuntimeDefaultFontAssetName = "NotoSansSC Runtime";
        private const string RuntimeDefaultFontOwnerMarker = "Tactics.UIManager.RuntimeDefaultFont.v1";

        private static Dictionary<UIId, UIType> s_uiTypeMap;
        private static Dictionary<UIId, (string uxml, string uss)> s_uiPaths;
        private static bool s_configLoaded;

        private static void EnsureConfigLoaded()
        {
            if (s_configLoaded && s_uiPaths != null) return;
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
        private Font _runtimeDefaultFontSource;
        private TextCoreFontAsset _runtimeDefaultFontAsset;
        private RuntimeDefaultFontOwner _runtimeDefaultFontOwner;

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

        private async Task<TextCoreFontAsset> GetRuntimeDefaultFontAsync(GameAssetManager mgr)
        {
            if (_runtimeDefaultFontSource == null)
            {
                _runtimeDefaultFontSource =
                    await mgr.LoadAsync<Font>(RuntimeDefaultFontSourcePath);
            }

            if (_runtimeDefaultFontSource == null)
                TLog.Warning("[UIManager] Failed to load the runtime font source. UI Toolkit will use its fallback font.");

            TryRecoverRuntimeDefaultFontAsset(_runtimeDefaultFontSource);
            EnsureRuntimeDefaultFontAsset();
            return _runtimeDefaultFontAsset;
        }

        private TextCoreFontAsset GetRuntimeDefaultFontSync(GameAssetManager mgr)
        {
            if (_runtimeDefaultFontSource == null)
            {
                _runtimeDefaultFontSource =
                    mgr.Load<Font>(RuntimeDefaultFontSourcePath);
            }

            if (_runtimeDefaultFontSource == null)
                TLog.Warning("[UIManager] Failed to load the runtime font source. UI Toolkit will use its fallback font.");

            TryRecoverRuntimeDefaultFontAsset(_runtimeDefaultFontSource);
            EnsureRuntimeDefaultFontAsset();
            return _runtimeDefaultFontAsset;
        }

        private void EnsureRuntimeDefaultFontAsset()
        {
            if (HasUsableRuntimeDefaultFontAsset() || _runtimeDefaultFontSource == null)
                return;

            _runtimeDefaultFontAsset = null;
            _runtimeDefaultFontOwner = null;

            // Keep the dynamic glyph atlas in memory so Play Mode never mutates a project FontAsset.
            _runtimeDefaultFontAsset = TextCoreFontAsset.CreateFontAsset(
                _runtimeDefaultFontSource,
                90,
                9,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic,
                true);

            if (_runtimeDefaultFontAsset == null)
            {
                TLog.Warning("[UIManager] Failed to create the runtime FontAsset. UI Toolkit will use its fallback font.");
                return;
            }

            _runtimeDefaultFontAsset.name = RuntimeDefaultFontAssetName;
            _runtimeDefaultFontOwner = ScriptableObject.CreateInstance<RuntimeDefaultFontOwner>();
            _runtimeDefaultFontOwner.name = RuntimeDefaultFontOwnerMarker;
            _runtimeDefaultFontOwner.Initialize(
                RuntimeDefaultFontOwnerMarker,
                _runtimeDefaultFontSource,
                _runtimeDefaultFontAsset);
            _runtimeDefaultFontOwner.hideFlags |= HideFlags.DontSave;
            ApplyRuntimeDefaultFontResourceHideFlags(_runtimeDefaultFontAsset);
        }

        private bool HasUsableRuntimeDefaultFontAsset()
        {
            return _runtimeDefaultFontSource != null &&
                   IsOwnedRuntimeDefaultFontAsset(
                       _runtimeDefaultFontOwner,
                       _runtimeDefaultFontSource,
                       _runtimeDefaultFontAsset);
        }

        private void TryRecoverRuntimeDefaultFontAsset(Font expectedSource)
        {
            if (expectedSource == null || HasUsableRuntimeDefaultFontAsset())
                return;

            RuntimeDefaultFontOwner recoveredOwner = null;
            var owners = Resources.FindObjectsOfTypeAll<RuntimeDefaultFontOwner>();
            Array.Sort(owners, (left, right) => left.GetInstanceID().CompareTo(right.GetInstanceID()));
            var trustedOwners = new List<RuntimeDefaultFontOwner>();
            foreach (var owner in owners)
            {
                if (owner == null)
                    continue;

                if (!HasRuntimeDefaultFontOwnerProvenance(owner))
                    continue;

                trustedOwners.Add(owner);

                if (HasRuntimeDefaultFontOwnershipAndGraph(owner, expectedSource, owner.FontAsset))
                    ApplyRuntimeDefaultFontResourceHideFlags(owner.FontAsset);

                if (recoveredOwner == null &&
                    IsOwnedRuntimeDefaultFontAsset(owner, expectedSource, owner.FontAsset))
                    recoveredOwner = owner;
            }

            var protectedResourceIds = CollectProtectedRuntimeDefaultFontResourceIds(recoveredOwner, owners);
            foreach (var owner in trustedOwners)
            {
                if (ReferenceEquals(owner, recoveredOwner))
                    continue;

                if (recoveredOwner != null && ReferenceEquals(owner.FontAsset, recoveredOwner.FontAsset))
                    UnityEngine.Object.Destroy(owner);
                else
                    DestroyOwnedRuntimeDefaultFont(owner, protectedResourceIds);
            }

            if (recoveredOwner == null)
                return;

            _runtimeDefaultFontOwner = recoveredOwner;
            _runtimeDefaultFontSource = expectedSource;
            _runtimeDefaultFontAsset = recoveredOwner.FontAsset;
            _runtimeDefaultFontAsset.name = RuntimeDefaultFontAssetName;
            ApplyRuntimeDefaultFontResourceHideFlags(_runtimeDefaultFontAsset);
        }

        private static bool IsOwnedRuntimeDefaultFontAsset(
            RuntimeDefaultFontOwner owner,
            Font expectedSource,
            TextCoreFontAsset fontAsset)
        {
            if (!HasRuntimeDefaultFontOwnershipAndGraph(owner, expectedSource, fontAsset) ||
                (owner.hideFlags & HideFlags.DontSave) != HideFlags.DontSave ||
                (fontAsset.hideFlags & HideFlags.DontSave) != HideFlags.DontSave ||
                (fontAsset.material.hideFlags & HideFlags.DontSave) != HideFlags.DontSave)
            {
                return false;
            }

            for (int index = 0; index < fontAsset.atlasTextureCount; index++)
            {
                var atlasTexture = fontAsset.atlasTextures[index];
                if (atlasTexture == null ||
                    (atlasTexture.hideFlags & HideFlags.DontSave) != HideFlags.DontSave)
                    return false;
            }

            return true;
        }

        private static bool HasRuntimeDefaultFontOwnershipAndGraph(
            RuntimeDefaultFontOwner owner,
            Font expectedSource,
            TextCoreFontAsset fontAsset)
        {
            if (!HasRuntimeDefaultFontOwnerProvenance(owner) ||
                expectedSource == null ||
                owner.Source != expectedSource ||
                owner.FontAsset != fontAsset ||
                fontAsset == null ||
                fontAsset.sourceFontFile != expectedSource ||
                fontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic ||
                !fontAsset.isMultiAtlasTexturesEnabled ||
                fontAsset.material == null ||
                fontAsset.atlasTextures == null ||
                fontAsset.atlasTextureCount <= 0 ||
                fontAsset.atlasTextureCount > fontAsset.atlasTextures.Length ||
                fontAsset.material.mainTexture != fontAsset.atlasTextures[0])
            {
                return false;
            }

            for (int index = 0; index < fontAsset.atlasTextureCount; index++)
            {
                if (fontAsset.atlasTextures[index] == null)
                    return false;
            }

            return true;
        }

        private static bool HasRuntimeDefaultFontOwnerProvenance(RuntimeDefaultFontOwner owner)
        {
            return owner != null &&
                   owner.Marker == RuntimeDefaultFontOwnerMarker &&
                   (owner.hideFlags & HideFlags.DontSave) == HideFlags.DontSave;
        }

        private static void DestroyOwnedRuntimeDefaultFont(
            RuntimeDefaultFontOwner owner,
            HashSet<int> protectedResourceIds)
        {
            if (owner == null)
                return;

            var fontAsset = owner.FontAsset;
            if (fontAsset != null)
            {
                if (IsRuntimeDefaultFontResourceProtected(fontAsset, protectedResourceIds))
                {
                    UnityEngine.Object.Destroy(owner);
                    return;
                }

                var material = fontAsset.material;
                var atlasTextures = fontAsset.atlasTextures;
                var atlasTexturesToDestroy = new List<Texture2D>();
                var atlasTextureIdsToDestroy = new HashSet<int>();
                if (atlasTextures != null)
                {
                    int usedAtlasCount = Math.Min(
                        Math.Max(fontAsset.atlasTextureCount, 0),
                        atlasTextures.Length);
                    for (int index = 0; index < usedAtlasCount; index++)
                    {
                        var atlasTexture = atlasTextures[index];
                        if (atlasTexture != null &&
                            !IsRuntimeDefaultFontResourceProtected(atlasTexture, protectedResourceIds) &&
                            atlasTextureIdsToDestroy.Add(atlasTexture.GetInstanceID()))
                            atlasTexturesToDestroy.Add(atlasTexture);
                    }
                }

                // Detach every referenced sub-resource before destroying the FontAsset. TextCore may
                // otherwise release shared resources or unused atlas capacity from its destruction path.
                fontAsset.material = null;
                if (atlasTextures != null)
                    fontAsset.atlasTextures = new Texture2D[atlasTextures.Length];

                if (material != null && !IsRuntimeDefaultFontResourceProtected(material, protectedResourceIds))
                    UnityEngine.Object.Destroy(material);
                foreach (var atlasTexture in atlasTexturesToDestroy)
                    UnityEngine.Object.Destroy(atlasTexture);
                UnityEngine.Object.Destroy(fontAsset);
            }

            UnityEngine.Object.Destroy(owner);
        }

        private static HashSet<int> CollectProtectedRuntimeDefaultFontResourceIds(
            RuntimeDefaultFontOwner retainedOwner,
            RuntimeDefaultFontOwner[] owners)
        {
            var protectedResourceIds = new HashSet<int>();
            AddRuntimeDefaultFontResourceIds(retainedOwner?.FontAsset, protectedResourceIds);
            foreach (var owner in owners)
            {
                if (owner != null && !HasRuntimeDefaultFontOwnerProvenance(owner))
                    AddRuntimeDefaultFontResourceIds(owner.FontAsset, protectedResourceIds);
            }
            return protectedResourceIds;
        }

        private static void AddRuntimeDefaultFontResourceIds(
            TextCoreFontAsset fontAsset,
            HashSet<int> resourceIds)
        {
            if (fontAsset == null)
                return;

            resourceIds.Add(fontAsset.GetInstanceID());
            if (fontAsset.material != null)
                resourceIds.Add(fontAsset.material.GetInstanceID());
            if (fontAsset.atlasTextures == null)
                return;

            foreach (var atlasTexture in fontAsset.atlasTextures)
            {
                if (atlasTexture != null)
                    resourceIds.Add(atlasTexture.GetInstanceID());
            }
        }

        private static bool IsRuntimeDefaultFontResourceProtected(
            UnityEngine.Object resource,
            HashSet<int> protectedResourceIds)
        {
            return resource != null &&
                   protectedResourceIds != null &&
                   protectedResourceIds.Contains(resource.GetInstanceID());
        }

        private static void SynchronizeOwnedRuntimeDefaultFonts()
        {
            var owners = Resources.FindObjectsOfTypeAll<RuntimeDefaultFontOwner>();
            Array.Sort(owners, (left, right) => left.GetInstanceID().CompareTo(right.GetInstanceID()));
            var trustedOwners = new List<RuntimeDefaultFontOwner>();
            RuntimeDefaultFontOwner retainedOwner = null;
            RuntimeDefaultFontOwner preferredOwner = _instance?._runtimeDefaultFontOwner;

            foreach (var owner in owners)
            {
                if (!HasRuntimeDefaultFontOwnerProvenance(owner))
                    continue;

                trustedOwners.Add(owner);
                if (HasRuntimeDefaultFontOwnershipAndGraph(owner, owner.Source, owner.FontAsset))
                    ApplyRuntimeDefaultFontResourceHideFlags(owner.FontAsset);

                if (IsOwnedRuntimeDefaultFontAsset(owner, owner.Source, owner.FontAsset) &&
                    (retainedOwner == null || ReferenceEquals(owner, preferredOwner)))
                    retainedOwner = owner;
            }

            var protectedResourceIds = CollectProtectedRuntimeDefaultFontResourceIds(retainedOwner, owners);
            foreach (var owner in trustedOwners)
            {
                if (ReferenceEquals(owner, retainedOwner))
                    continue;

                if (retainedOwner != null && ReferenceEquals(owner.FontAsset, retainedOwner.FontAsset))
                    UnityEngine.Object.Destroy(owner);
                else
                    DestroyOwnedRuntimeDefaultFont(owner, protectedResourceIds);
            }
        }

        private static void ApplyRuntimeDefaultFontResourceHideFlags(TextCoreFontAsset fontAsset)
        {
            if (fontAsset == null)
                return;

            fontAsset.hideFlags |= HideFlags.DontSave;
            if (fontAsset.material != null)
                fontAsset.material.hideFlags |= HideFlags.DontSave;

            if (fontAsset.atlasTextures == null)
                return;

            int usedAtlasCount = Math.Min(fontAsset.atlasTextureCount, fontAsset.atlasTextures.Length);
            for (int index = 0; index < usedAtlasCount; index++)
            {
                var atlasTexture = fontAsset.atlasTextures[index];
                if (atlasTexture != null)
                    atlasTexture.hideFlags |= HideFlags.DontSave;
            }
        }

        private static void ApplyRuntimeDefaultFont(UIDocument uiDoc, TextCoreFontAsset defaultFont)
        {
            ApplyRuntimeDefaultFontResourceHideFlags(defaultFont);
            if (uiDoc?.rootVisualElement != null && defaultFont != null)
            {
                uiDoc.rootVisualElement.style.unityFont = StyleKeyword.Null;
                uiDoc.rootVisualElement.style.unityFontDefinition =
                    new StyleFontDefinition(FontDefinition.FromSDFFont(defaultFont));
            }
        }

        private InputAction _toggleConsoleAction;
        private InputAction _toggleMenuAction;
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
                ActivateInstance(existing);
                return;
            }

            var instance = LoadAndCreateSync(id, GetAssetPath(id));
            _instances[id] = instance;
            ActivateInstance(instance);
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

            var uiMap = module.actionsAsset.FindActionMap("UI");
            _toggleMenuAction = uiMap?.FindAction("Cancel");
            if (_toggleMenuAction != null)
            {
                _toggleMenuAction.performed += OnToggleMenuPerformed;
                _toggleMenuAction.Enable();
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

        private void OnToggleMenuPerformed(InputAction.CallbackContext ctx)
        {
            // The UI Cancel action also receives the mouse right button. In battle that
            // input belongs to target-selection cancellation and must not open Pause.
            if (ctx.control?.device is Mouse)
                return;
            if (IsVisible(UIId.Battle) &&
                BattleController.Instance is { } battleController &&
                battleController.GridState is not GridStateAwaitInput)
            {
                return;
            }

            if (IsVisible(UIId.Options))
            {
                Hide(UIId.Options);
                return;
            }

            if (!CanTogglePauseMenu())
                return;

            _ = HomeFlowCoordinator.Instance.ToggleMenuAsync();
        }

        private bool CanTogglePauseMenu()
        {
            return IsVisible(UIId.RoguelikeMap) || IsVisible(UIId.Battle) || IsVisible(UIId.Menu);
        }

        public void Hide(UIId id)
        {
            SynchronizeOwnedRuntimeDefaultFonts();
            if (_instances.TryGetValue(id, out var instance) && instance?.ContainerGO != null)
                instance.ContainerGO.SetActive(false);
        }

        public void Destroy(UIId id)
        {
            SynchronizeOwnedRuntimeDefaultFonts();
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

        /// <summary>
        /// Register a pre-created UIDocument for testing without going through the asset loading pipeline.
        /// Automatically attaches the appropriate UIController (BattleUIController, etc.).
        /// </summary>
        public void RegisterTestUI(UIId id, UIDocument uiDoc)
        {
            var go = uiDoc.gameObject;
            var manager = GameAssetManager.Instance;
            if (manager != null && manager.IsInitialized)
                ApplyRuntimeDefaultFont(uiDoc, GetRuntimeDefaultFontSync(manager));
            EnsureUIController(id, go);
            _instances[id] = new UIInstance(UIType.UiToolkitUxml, go, uiDoc);
            ActivateInstance(_instances[id]);
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
                ActivateInstance(existing);
                return;
            }

            if (!_loadingTasks.TryGetValue(id, out var loadTask))
            {
                loadTask = LoadAndCreateAsync(id, assetPath);
                _loadingTasks[id] = loadTask;
            }

            try
            {
                var instance = await loadTask;
                _instances[id] = instance;
                ActivateInstance(instance);
            }
            finally
            {
                if (_loadingTasks.TryGetValue(id, out var pendingTask) && pendingTask == loadTask)
                    _loadingTasks.Remove(id);
            }
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
            var defaultFont = await GetRuntimeDefaultFontAsync(mgr);

            return CreateUiToolkitInstance(id, visualTree, styleSheet, panelSettings, defaultFont);
        }

        private UIInstance CreateUiToolkitInstance(
            UIId id,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet,
            PanelSettings panelSettings,
            TextCoreFontAsset defaultFont)
        {
            var hostGo = new GameObject(id.ToString());

            var uiDoc = hostGo.AddComponent<UIDocument>();
            uiDoc.visualTreeAsset = visualTree;
            uiDoc.panelSettings = panelSettings;
            // Keep the battle console above other UI Toolkit documents so its overlay remains visible.
            uiDoc.sortingOrder = id == UIId.CheatConsole ? 100 : 0;

            if (styleSheet != null && uiDoc.rootVisualElement != null)
                uiDoc.rootVisualElement.styleSheets.Add(styleSheet);

            ApplyRuntimeDefaultFont(uiDoc, defaultFont);

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
            var defaultFont = GetRuntimeDefaultFontSync(mgr);

            return CreateUiToolkitInstance(id, visualTree, styleSheet, panelSettings, defaultFont);
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
                case UIId.EventPanel:
                    if (root.GetComponent<EventUIController>() == null)
                        root.AddComponent<EventUIController>();
                    break;
                case UIId.RunEndSummary:
                    if (root.GetComponent<RunEndSummaryUIController>() == null)
                        root.AddComponent<RunEndSummaryUIController>();
                    break;
                case UIId.Options:
                    if (root.GetComponent<OptionsUIController>() == null)
                        root.AddComponent<OptionsUIController>();
                    break;
                case UIId.SlotSelect:
                    if (root.GetComponent<SlotSelectUIController>() == null)
                        root.AddComponent<SlotSelectUIController>();
                    break;

                default:
                    break;
            }
        }

        private void ActivateInstance(UIInstance instance)
        {
            instance.ContainerGO.SetActive(true);

            // UIDocument rebuilds its root when reactivated, so inherited font state must be restored.
            if (instance.Type == UIType.UiToolkitUxml)
                ApplyRuntimeDefaultFont(instance.UiDoc, _runtimeDefaultFontAsset);
        }
    }
}
