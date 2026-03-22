using System;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tactics.AssetPipeline
{
    /// <summary>
    /// Load assets from locally built AssetBundles. Pair each successful load with <see cref="Release"/> when done.
    /// </summary>
    public static class GameAsset
    {
        public static T Load<T>(string assetProjectPath) where T : Object
        {
            var (bundle, order) = Resolve(assetProjectPath);
            GameAssets.Cache.EnsureLoadedSync(order);
            var ab = GameAssets.Cache.GetLoadedBundle(bundle);
            var obj = ab.LoadAsset<T>(GameAssets.NormalizeAssetPath(assetProjectPath));
            if (obj == null)
                throw new InvalidOperationException($"LoadAsset returned null: {assetProjectPath}");
            return obj;
        }

        public static async Task<T> LoadAsync<T>(string assetProjectPath) where T : Object
        {
            var (bundle, order) = Resolve(assetProjectPath);
            await GameAssets.Cache.EnsureLoadedAsync(order);
            var ab = GameAssets.Cache.GetLoadedBundle(bundle);
            var path = GameAssets.NormalizeAssetPath(assetProjectPath);
            var req = ab.LoadAssetAsync<T>(path);
            while (!req.isDone)
                await Task.Yield();
            var obj = req.asset as T;
            if (obj == null)
                throw new InvalidOperationException($"LoadAssetAsync returned null: {assetProjectPath}");
            return obj;
        }

        public static void Release(string assetProjectPath)
        {
            GameAssets.ThrowIfNotInitialized();
            var path = GameAssets.NormalizeAssetPath(assetProjectPath);
            var bundle = GameAssets.ResolveBundleForAsset(path);
            var order = GameAssets.GetLoadOrder(bundle);
            GameAssets.Cache.Release(order);
        }

        private static (string bundle, System.Collections.Generic.List<string> order) Resolve(string assetProjectPath)
        {
            GameAssets.ThrowIfNotInitialized();
            var path = GameAssets.NormalizeAssetPath(assetProjectPath);
            var bundle = GameAssets.ResolveBundleForAsset(path);
            var order = GameAssets.GetLoadOrder(bundle);
            return (bundle, order);
        }
    }
}
