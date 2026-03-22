using System;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tactics.AssetPipeline
{
    /// <summary>
    /// Legacy static facade. Use <see cref="GameAssetManager"/> (scene prefab) instead.
    /// </summary>
    public static class GameAsset
    {
        [Obsolete("Use GameAssetManager.Instance.Load after placing the GameAssetManager prefab in the scene.")]
        public static T Load<T>(string assetProjectPath) where T : Object =>
            RequireInstance().Load<T>(assetProjectPath);

        [Obsolete("Use GameAssetManager.Instance.LoadAsync.")]
        public static Task<T> LoadAsync<T>(string assetProjectPath) where T : Object =>
            RequireInstance().LoadAsync<T>(assetProjectPath);

        [Obsolete("Use GameAssetManager.Instance.Release.")]
        public static void Release(string assetProjectPath) => RequireInstance().Release(assetProjectPath);

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
