using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Tactics.AssetPipeline
{
    /// <summary>
    /// How <see cref="GameAssetManager"/> resolves project asset paths at runtime in the Editor or in builds.
    /// </summary>
    public enum GameAssetLoadMode
    {
        /// <summary>Load AssetBundles from <see cref="GameAssetManager.BundlesRoot"/>.</summary>
        StreamingBundles = 0,

        /// <summary>
        /// Editor Play Mode only: load via <c>AssetDatabase.LoadAssetAtPath</c>. No <c>manifest.json</c> or bundles required;
        /// paths are validated with the asset database. <see cref="GameAssetManager.ResolveBundleForAsset"/> and
        /// <see cref="GameAssetManager.GetLoadOrder"/> are unavailable until a manifest is loaded (e.g. switch to
        /// <see cref="StreamingBundles"/> and initialize). Player builds fall back to <see cref="StreamingBundles"/>.
        /// </summary>
        EditorAssetDatabase = 1,
    }

    /// <summary>
    /// Scene-singleton entry point for game asset loading. Place an instance in entry scenes, or use <see cref="CreateBootstrap"/> from code (e.g. Splash).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameAssetManager : MonoBehaviourSingleton<GameAssetManager>
    {
        [SerializeField]
        private GameAssetLoadMode _loadMode = GameAssetLoadMode.StreamingBundles;

        [Tooltip("Leave empty for StreamingAssets/Bundles. Use an absolute path (e.g. intermediate Output/AssetBundles/StandaloneWindows64) to skip copying to StreamingAssets while testing in Editor.")]
        [SerializeField]
        private string _bundlesRootOverride = "";

        [SerializeField]
        private bool _autoInitializeOnAwake = true;

        private GameAssetManifest _manifest;
        private Dictionary<string, BundleRecord> _bundlesByName;
        private Dictionary<string, string> _assetPathToBundle;
        private BundleCache _cache;
        private string _bundlesRoot;

#if UNITY_EDITOR
        private readonly Dictionary<string, int> _editorDirectRefCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Editor + <see cref="GameAssetLoadMode.EditorAssetDatabase"/>: initialized without manifest or <see cref="BundleCache"/>.</summary>
        private bool _editorManifestlessInitialized;
#endif

        public bool IsInitialized
        {
            get
            {
#if UNITY_EDITOR
                if (_editorManifestlessInitialized)
                    return true;
#endif
                return _manifest != null && _cache != null;
            }
        }

        public string BundlesRoot => _bundlesRoot;

        public GameAssetLoadMode SerializedLoadMode => _loadMode;

        private GameAssetLoadMode EffectiveLoadMode
        {
            get
            {
#if !UNITY_EDITOR
                if (_loadMode == GameAssetLoadMode.EditorAssetDatabase)
                {
                    Debug.LogWarning("[GameAssetManager] EditorAssetDatabase is editor-only; using StreamingBundles.");
                    return GameAssetLoadMode.StreamingBundles;
                }
#endif
                return _loadMode;
            }
        }

        /// <summary>True if the path is a Unity scene asset (<c>.unity</c>).</summary>
        public static bool IsSceneProjectPath(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath))
                return false;
            return string.Equals(Path.GetExtension(NormalizeAssetPath(projectPath)), ".unity", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Scene name passed to <see cref="SceneManager.LoadScene"/> (file name without extension). Use unique scene names across the project.</summary>
        public static string GetSceneNameForLoad(string normalizedProjectPath)
        {
            var name = Path.GetFileNameWithoutExtension(normalizedProjectPath);
            if (string.IsNullOrEmpty(name))
                throw new InvalidOperationException($"Invalid scene path: {normalizedProjectPath}");
            return name;
        }

        /// <summary>
        /// Apply shared settings from a <see cref="GameAssetRuntimeSettings"/> asset. Call while the GameObject is **inactive**
        /// (e.g. right after <see cref="Object.Instantiate"/> of a prefab whose root is inactive) so <see cref="Awake"/> runs with final values.
        /// </summary>
        public void ApplyRuntimeSettings(GameAssetRuntimeSettings settings)
        {
            if (settings == null)
                return;
            _loadMode = settings.loadMode;
            _bundlesRootOverride = settings.bundlesRootOverride ?? string.Empty;
            _autoInitializeOnAwake = settings.autoInitializeOnAwake;
            SetPersistAcrossScenes(settings.persistAcrossScenes);
        }

        /// <summary>
        /// Creates the scene singleton from code: inactive root, <see cref="ApplyRuntimeSettings"/>, then activate so <see cref="Awake"/> runs with final values.
        /// Returns null if <paramref name="settings"/> is null.
        /// </summary>
        public static GameAssetManager CreateBootstrap(GameAssetRuntimeSettings settings)
        {
            if (settings == null)
                return null;

            var go = new GameObject("GameAssetManager");
            go.SetActive(false);
            var mgr = go.AddComponent<GameAssetManager>();
            mgr.ApplyRuntimeSettings(settings);
            go.SetActive(true);
            return mgr;
        }

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this)
                return;

            if (_autoInitializeOnAwake && !Initialize())
                Debug.LogError("[GameAssetManager] Auto-initialize failed. Check manifest path and build bundles.");
        }

        protected override void OnDestroy()
        {
            if (Instance == this)
                Shutdown();
            base.OnDestroy();
        }

        private void Shutdown()
        {
            _cache?.UnloadAll();
            _cache = null;
            _manifest = null;
            _bundlesByName = null;
            _assetPathToBundle = null;
            _bundlesRoot = null;
#if UNITY_EDITOR
            _editorManifestlessInitialized = false;
            _editorDirectRefCounts.Clear();
#endif
        }

        /// <summary>Sync manifest load. Editor / Standalone file system; use <see cref="InitializeAsync"/> on Android/WebGL player.</summary>
        public bool Initialize()
        {
            _bundlesRoot = ResolveBundlesRoot();
#if UNITY_EDITOR
            if (EffectiveLoadMode == GameAssetLoadMode.EditorAssetDatabase)
            {
                if (_editorManifestlessInitialized)
                    return true;
                return InitializeEditorManifestless();
            }
#endif
            var manifestPath = Path.Combine(_bundlesRoot, GameAssetPaths.ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                Debug.LogError($"[GameAssetManager] Missing manifest. Build bundles first: {manifestPath}");
                return false;
            }

            var json = File.ReadAllText(manifestPath);
            return ApplyManifestJson(json);
        }

        public async Task<bool> InitializeAsync()
        {
            _bundlesRoot = ResolveBundlesRoot();
#if UNITY_EDITOR
            if (EffectiveLoadMode == GameAssetLoadMode.EditorAssetDatabase)
            {
                if (_editorManifestlessInitialized)
                    return true;
                return InitializeEditorManifestless();
            }
#endif
            string json;

#if (UNITY_ANDROID || UNITY_WEBGL) && !UNITY_EDITOR
            var relative = $"{GameAssetPaths.StreamingBundlesFolder}/{GameAssetPaths.ManifestFileName}";
            json = await LoadStreamingTextAsync(Path.Combine(Application.streamingAssetsPath, relative));
            if (json == null)
                return false;
#else
            var manifestPath = Path.Combine(_bundlesRoot, GameAssetPaths.ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                Debug.LogError($"[GameAssetManager] Missing manifest. Build bundles first: {manifestPath}");
                return false;
            }

            json = File.ReadAllText(manifestPath);
#endif

            // On non-Android/WebGL platforms the method would otherwise be fully synchronous.
            // Yield once to keep this async API truly async and silence CS1998.
            await Task.Yield();

            return ApplyManifestJson(json);
        }

        private string ResolveBundlesRoot()
        {
            if (!string.IsNullOrWhiteSpace(_bundlesRootOverride))
                return _bundlesRootOverride.Trim();
            return Path.Combine(Application.streamingAssetsPath, GameAssetPaths.StreamingBundlesFolder);
        }

#if (UNITY_ANDROID || UNITY_WEBGL) && !UNITY_EDITOR
        private static async Task<string> LoadStreamingTextAsync(string url)
        {
            using var req = UnityWebRequest.Get(url);
            req.downloadHandler = new DownloadHandlerBuffer();
            var op = req.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[GameAssetManager] Failed to load manifest: {req.error}");
                return null;
            }

            return req.downloadHandler.text;
        }
#endif

#if UNITY_EDITOR
        private bool InitializeEditorManifestless()
        {
            _cache?.UnloadAll();
            _editorDirectRefCounts.Clear();
            _manifest = null;
            _bundlesByName = null;
            _assetPathToBundle = null;
            _cache = null;
            _editorManifestlessInitialized = true;
            return true;
        }
#endif

        private bool ApplyManifestJson(string json)
        {
#if UNITY_EDITOR
            _editorManifestlessInitialized = false;
#endif
            _cache?.UnloadAll();
#if UNITY_EDITOR
            _editorDirectRefCounts.Clear();
#endif

            try
            {
                _manifest = JsonUtility.FromJson<GameAssetManifest>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameAssetManager] Invalid manifest JSON: {e.Message}");
                return false;
            }

            if (_manifest == null || _manifest.bundles == null || _manifest.assets == null)
            {
                Debug.LogError("[GameAssetManager] Manifest is empty or invalid.");
                return false;
            }

            RebuildIndices();
            _cache = new BundleCache(_bundlesRoot, GetBundleRecord);
            return true;
        }

        private void RebuildIndices()
        {
            _bundlesByName = new Dictionary<string, BundleRecord>(StringComparer.Ordinal);
            foreach (var b in _manifest.bundles)
            {
                if (b == null || string.IsNullOrEmpty(b.name))
                    continue;
                _bundlesByName[b.name] = b;
            }

            _assetPathToBundle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in _manifest.assets)
            {
                if (a == null || string.IsNullOrEmpty(a.path) || string.IsNullOrEmpty(a.bundle))
                    continue;
                _assetPathToBundle[NormalizeAssetPath(a.path)] = a.bundle;
            }
        }

        private void ThrowIfNotInitialized()
        {
            if (!IsInitialized)
                throw new InvalidOperationException("GameAssetManager is not initialized. Call Initialize() or enable auto-initialize on the prefab.");
        }

        private BundleRecord GetBundleRecord(string bundleName)
        {
            if (_bundlesByName.TryGetValue(bundleName, out var r))
                return r;
            throw new KeyNotFoundException($"Unknown bundle '{bundleName}'.");
        }

        public static string NormalizeAssetPath(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath))
                return projectPath;
            return projectPath.Replace('\\', '/').Trim();
        }

        public string ResolveBundleForAsset(string assetProjectPath)
        {
            ThrowIfNotInitialized();
#if UNITY_EDITOR
            if (_editorManifestlessInitialized)
            {
                throw new InvalidOperationException(
                    "ResolveBundleForAsset is not available in EditorAssetDatabase mode without a manifest. " +
                    "Build bundles and initialize with StreamingBundles, or use paths validated by Load/LoadScene only.");
            }
#endif
            var key = NormalizeAssetPath(assetProjectPath);
            if (_assetPathToBundle.TryGetValue(key, out var bundle))
                return bundle;
            throw new KeyNotFoundException($"Asset not in manifest: {key}");
        }

        public List<string> GetLoadOrder(string bundleName)
        {
            ThrowIfNotInitialized();
#if UNITY_EDITOR
            if (_editorManifestlessInitialized)
            {
                throw new InvalidOperationException(
                    "GetLoadOrder is not available in EditorAssetDatabase mode without a manifest. " +
                    "Build bundles and initialize with StreamingBundles if you need bundle dependency order.");
            }
#endif
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var order = new List<string>();

            void Visit(string name)
            {
                if (!visited.Add(name))
                    return;
                var rec = GetBundleRecord(name);
                if (rec.deps != null)
                {
                    foreach (var dep in rec.deps)
                    {
                        if (string.IsNullOrEmpty(dep))
                            continue;
                        Visit(dep);
                    }
                }

                order.Add(name);
            }

            Visit(bundleName);
            return order;
        }

        private (string bundle, List<string> order) Resolve(string assetProjectPath)
        {
            ThrowIfNotInitialized();
            var path = NormalizeAssetPath(assetProjectPath);
            var bundle = ResolveBundleForAsset(path);
            var order = GetLoadOrder(bundle);
            return (bundle, order);
        }

        private void RetainBundlesForPathSync(string normalizedProjectPath)
        {
            var (_, order) = Resolve(normalizedProjectPath);
            _cache.EnsureLoadedSync(order);
        }

        private async Task RetainBundlesForPathAsync(string normalizedProjectPath)
        {
            var (_, order) = Resolve(normalizedProjectPath);
            await _cache.EnsureLoadedAsync(order);
        }

        private void ReleaseBundlesForPath(string normalizedProjectPath)
        {
            var bundle = ResolveBundleForAsset(normalizedProjectPath);
            var order = GetLoadOrder(bundle);
            _cache.Release(order);
        }

        private static void ThrowIfScenePathUsedAsAsset(string normalizedPath)
        {
            if (IsSceneProjectPath(normalizedPath))
                throw new InvalidOperationException($"Use LoadScene / LoadSceneAsync for scene paths, not Load: {normalizedPath}");
        }

        public T Load<T>(string assetProjectPath) where T : Object
        {
            var path = NormalizeAssetPath(assetProjectPath);
            ThrowIfScenePathUsedAsAsset(path);

            if (EffectiveLoadMode == GameAssetLoadMode.EditorAssetDatabase)
            {
#if UNITY_EDITOR
                var editorObj = LoadEditorDirect<T>(path);
                AssetScopeManager.RegisterLoadedPath(path);
                return editorObj;
#else
                throw new InvalidOperationException("EditorAssetDatabase is only available in the Unity Editor.");
#endif
            }

            RetainBundlesForPathSync(path);
            var (bundle, _) = Resolve(path);
            var ab = _cache.GetLoadedBundle(bundle);
            var obj = ab.LoadAsset<T>(path);
            if (obj == null)
            {
                // Undo the bundle retain performed by RetainBundlesForPathSync.
                Release(path);
                throw new InvalidOperationException($"LoadAsset returned null: {assetProjectPath}");
            }

            AssetScopeManager.RegisterLoadedPath(path);
            return obj;
        }

        public async Task<T> LoadAsync<T>(string assetProjectPath) where T : Object
        {
            var path = NormalizeAssetPath(assetProjectPath);
            ThrowIfScenePathUsedAsAsset(path);

            if (EffectiveLoadMode == GameAssetLoadMode.EditorAssetDatabase)
            {
#if UNITY_EDITOR
                await Task.Yield();
                var editorObj = LoadEditorDirect<T>(path);
                AssetScopeManager.RegisterLoadedPath(path);
                return editorObj;
#else
                throw new InvalidOperationException("EditorAssetDatabase is only available in the Unity Editor.");
#endif
            }

            await RetainBundlesForPathAsync(path);
            var (bundle, _) = Resolve(path);
            var ab = _cache.GetLoadedBundle(bundle);
            var req = ab.LoadAssetAsync<T>(path);
            while (!req.isDone)
                await Task.Yield();
            var obj = req.asset as T;
            if (obj == null)
            {
                // Undo the bundle retain performed by RetainBundlesForPathAsync.
                Release(path);
                throw new InvalidOperationException($"LoadAssetAsync returned null: {assetProjectPath}");
            }

            AssetScopeManager.RegisterLoadedPath(path);
            return obj;
        }

        /// <summary>
        /// Load a scene by manifest project path (<c>Assets/.../Foo.unity</c>). Pair with <see cref="Release"/> (and for Additive, optionally <see cref="UnloadSceneAsync"/>).
        /// </summary>
        public void LoadScene(string sceneProjectPath, LoadSceneMode loadSceneMode = LoadSceneMode.Single)
        {
            ThrowIfNotInitialized();
            var path = NormalizeAssetPath(sceneProjectPath);
            if (!IsSceneProjectPath(path))
                throw new InvalidOperationException($"Not a scene path (.unity): {path}");

            if (EffectiveLoadMode == GameAssetLoadMode.EditorAssetDatabase)
            {
#if UNITY_EDITOR
                LoadSceneEditorPlayMode(path, loadSceneMode);
#else
                throw new InvalidOperationException("EditorAssetDatabase is only available in the Unity Editor.");
#endif
                return;
            }

            RetainBundlesForPathSync(path);
            var sceneName = GetSceneNameForLoad(path);
            SceneManager.LoadScene(sceneName, loadSceneMode);
        }

        /// <summary>Async variant of <see cref="LoadScene"/> (bundle mode uses <see cref="SceneManager.LoadSceneAsync"/>).</summary>
        public async Task LoadSceneAsync(string sceneProjectPath, LoadSceneMode loadSceneMode = LoadSceneMode.Single)
        {
            ThrowIfNotInitialized();
            var path = NormalizeAssetPath(sceneProjectPath);
            if (!IsSceneProjectPath(path))
                throw new InvalidOperationException($"Not a scene path (.unity): {path}");

            if (EffectiveLoadMode == GameAssetLoadMode.EditorAssetDatabase)
            {
#if UNITY_EDITOR
                LoadSceneEditorPlayMode(path, loadSceneMode);
                await Task.Yield();
#else
                throw new InvalidOperationException("EditorAssetDatabase is only available in the Unity Editor.");
#endif
                return;
            }

            await RetainBundlesForPathAsync(path);
            var sceneName = GetSceneNameForLoad(path);
            var op = SceneManager.LoadSceneAsync(sceneName, loadSceneMode);
            if (op == null)
                throw new InvalidOperationException($"LoadSceneAsync failed for scene '{sceneName}'.");
            while (!op.isDone)
                await Task.Yield();
        }

        /// <summary>
        /// Unloads an Additive scene by name derived from <paramref name="sceneProjectPath"/>, then calls <see cref="Release"/> for bundle/editor ref counts.
        /// </summary>
        public async Task UnloadSceneAsync(string sceneProjectPath)
        {
            ThrowIfNotInitialized();
            var path = NormalizeAssetPath(sceneProjectPath);
            if (!IsSceneProjectPath(path))
                throw new InvalidOperationException($"Not a scene path (.unity): {path}");

            var sceneName = GetSceneNameForLoad(path);
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.isLoaded)
            {
                Debug.LogWarning($"[GameAssetManager] Scene '{sceneName}' is not loaded; skipping unload, not calling Release.");
                return;
            }

            var op = SceneManager.UnloadSceneAsync(scene);
            if (op != null)
            {
                while (!op.isDone)
                    await Task.Yield();
            }

            Release(path);
        }

        public void Release(string assetProjectPath)
        {
            ThrowIfNotInitialized();
            var path = NormalizeAssetPath(assetProjectPath);
            if (EffectiveLoadMode == GameAssetLoadMode.EditorAssetDatabase)
            {
#if UNITY_EDITOR
                ReleaseEditorDirect(path);
#endif
                return;
            }

            ReleaseBundlesForPath(path);
        }

#if UNITY_EDITOR
        private void LoadSceneEditorPlayMode(string normalizedPath, LoadSceneMode loadSceneMode)
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("EditorAssetDatabase scene load requires Play Mode.");
            ValidateEditorDirectAssetPath(normalizedPath);
            EditorSceneManager.LoadSceneInPlayMode(normalizedPath, new LoadSceneParameters(loadSceneMode));
            if (!_editorDirectRefCounts.TryGetValue(normalizedPath, out var n))
                n = 0;
            _editorDirectRefCounts[normalizedPath] = n + 1;
        }

        private T LoadEditorDirect<T>(string normalizedPath) where T : Object
        {
            ThrowIfNotInitialized();
            ValidateEditorDirectAssetPath(normalizedPath);
            var obj = AssetDatabase.LoadAssetAtPath<T>(normalizedPath);
            if (obj == null)
                throw new InvalidOperationException($"AssetDatabase.LoadAssetAtPath returned null: {normalizedPath}");

            if (!_editorDirectRefCounts.TryGetValue(normalizedPath, out var n))
                n = 0;
            _editorDirectRefCounts[normalizedPath] = n + 1;
            return obj;
        }

        private void ReleaseEditorDirect(string normalizedPath)
        {
            if (!_editorDirectRefCounts.TryGetValue(normalizedPath, out var c))
            {
                Debug.LogWarning($"[GameAssetManager] Release without matching Load: {normalizedPath}");
                return;
            }

            c--;
            if (c <= 0)
                _editorDirectRefCounts.Remove(normalizedPath);
            else
                _editorDirectRefCounts[normalizedPath] = c;
        }

        private static void ValidateEditorDirectAssetPath(string normalizedPath)
        {
            if (string.IsNullOrEmpty(normalizedPath) ||
                !normalizedPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Asset path must start with \"Assets/\": {normalizedPath}");
            }

            if (!AssetDatabase.AssetPathExists(normalizedPath))
                throw new InvalidOperationException($"Asset path does not exist in the project: {normalizedPath}");
        }
#endif
    }
}
