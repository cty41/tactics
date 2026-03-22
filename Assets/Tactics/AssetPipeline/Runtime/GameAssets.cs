using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Tactics.AssetPipeline
{
    /// <summary>
    /// Loads <see cref="GameAssetPaths.ManifestFileName"/> from StreamingAssets and builds lookup tables.
    /// Call <see cref="Initialize"/> or <see cref="InitializeAsync"/> before <see cref="GameAsset"/> APIs.
    /// </summary>
    public static class GameAssets
    {
        private static GameAssetManifest _manifest;
        private static Dictionary<string, BundleRecord> _bundlesByName;
        private static Dictionary<string, string> _assetPathToBundle;
        private static BundleCache _cache;
        private static string _bundlesRoot;

        public static bool IsInitialized => _manifest != null && _cache != null;

        public static string BundlesRoot => _bundlesRoot;

        /// <summary>Editor / Standalone: file system. Android/WebGL player: use <see cref="InitializeAsync"/>.</summary>
        public static bool Initialize()
        {
            _bundlesRoot = Path.Combine(Application.streamingAssetsPath, GameAssetPaths.StreamingBundlesFolder);
            var manifestPath = Path.Combine(_bundlesRoot, GameAssetPaths.ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                Debug.LogError($"[GameAssets] Missing manifest. Build bundles first: {manifestPath}");
                return false;
            }

            var json = File.ReadAllText(manifestPath);
            return ApplyManifestJson(json);
        }

        /// <summary>Reads manifest via UnityWebRequest when <see cref="File"/> cannot (Android/WebGL player).</summary>
        public static async Task<bool> InitializeAsync()
        {
            _bundlesRoot = Path.Combine(Application.streamingAssetsPath, GameAssetPaths.StreamingBundlesFolder);
            var relative = $"{GameAssetPaths.StreamingBundlesFolder}/{GameAssetPaths.ManifestFileName}";
            string json;

#if (UNITY_ANDROID || UNITY_WEBGL) && !UNITY_EDITOR
            json = await LoadStreamingTextAsync(Path.Combine(Application.streamingAssetsPath, relative));
            if (json == null)
                return false;
#else
            var manifestPath = Path.Combine(_bundlesRoot, GameAssetPaths.ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                Debug.LogError($"[GameAssets] Missing manifest. Build bundles first: {manifestPath}");
                return false;
            }

            json = File.ReadAllText(manifestPath);
#endif

            return ApplyManifestJson(json);
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
                Debug.LogError($"[GameAssets] Failed to load manifest: {req.error}");
                return null;
            }

            return req.downloadHandler.text;
        }
#endif

        private static bool ApplyManifestJson(string json)
        {
            try
            {
                _manifest = JsonUtility.FromJson<GameAssetManifest>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameAssets] Invalid manifest JSON: {e.Message}");
                return false;
            }

            if (_manifest == null || _manifest.bundles == null || _manifest.assets == null)
            {
                Debug.LogError("[GameAssets] Manifest is empty or invalid.");
                return false;
            }

            RebuildIndices();
            _cache = new BundleCache(_bundlesRoot);
            return true;
        }

        private static void RebuildIndices()
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

        internal static void ThrowIfNotInitialized()
        {
            if (!IsInitialized)
                throw new InvalidOperationException("GameAssets is not initialized. Call GameAssets.Initialize() first.");
        }

        internal static BundleRecord GetBundleRecord(string bundleName)
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

        public static string ResolveBundleForAsset(string assetProjectPath)
        {
            ThrowIfNotInitialized();
            var key = NormalizeAssetPath(assetProjectPath);
            if (_assetPathToBundle.TryGetValue(key, out var bundle))
                return bundle;
            throw new KeyNotFoundException($"Asset not in manifest: {key}");
        }

        /// <summary>Dependency-first load order for the given bundle (includes the bundle itself last).</summary>
        public static List<string> GetLoadOrder(string bundleName)
        {
            ThrowIfNotInitialized();
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

        internal static BundleCache Cache => _cache;
    }
}
