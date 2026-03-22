using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Tactics.AssetPipeline
{
    /// <summary>
    /// Legacy static facade. Use <see cref="GameAssetManager"/> (scene prefab) instead.
    /// </summary>
    public static class GameAssets
    {
        public static bool IsInitialized =>
            GameAssetManager.Instance != null && GameAssetManager.Instance.IsInitialized;

        public static string BundlesRoot => RequireInstance().BundlesRoot;

        [Obsolete("Use GameAssetManager.Instance.Initialize() after placing the GameAssetManager prefab in the scene.")]
        public static bool Initialize() => RequireInstance().Initialize();

        [Obsolete("Use GameAssetManager.Instance.InitializeAsync().")]
        public static Task<bool> InitializeAsync() => RequireInstance().InitializeAsync();

        public static string NormalizeAssetPath(string projectPath) => GameAssetManager.NormalizeAssetPath(projectPath);

        [Obsolete("Use GameAssetManager.Instance.ResolveBundleForAsset(path).")]
        public static string ResolveBundleForAsset(string assetProjectPath) =>
            RequireInstance().ResolveBundleForAsset(assetProjectPath);

        [Obsolete("Use GameAssetManager.Instance.GetLoadOrder(bundleName).")]
        public static List<string> GetLoadOrder(string bundleName) => RequireInstance().GetLoadOrder(bundleName);

        private static GameAssetManager RequireInstance()
        {
            var m = GameAssetManager.Instance;
            if (m == null)
                throw new InvalidOperationException(
                    "No GameAssetManager in scene. Add Assets/Tactics/AssetPipeline/GameAssetManager.prefab to the scene.");
            return m;
        }
    }
}
