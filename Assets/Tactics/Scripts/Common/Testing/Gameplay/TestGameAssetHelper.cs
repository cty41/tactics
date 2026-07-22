using System.Threading.Tasks;
using Tactics.AssetPipeline;
using UnityEngine;

namespace Tactics.Common.Testing.Gameplay
{
    public static class TestGameAssetHelper
    {
        private static GameObject _managerGo;

        public static async Task<GameAssetManager> EnsureInitialized()
        {
            // Check if already initialized
            if (GameAssetManager.Instance != null && GameAssetManager.Instance.IsInitialized)
                return GameAssetManager.Instance;

            // Create new instance
            _managerGo = new GameObject("TestGameAssetManager");
            _managerGo.SetActive(false);
            var mgr = _managerGo.AddComponent<GameAssetManager>();

            // Use reflection to set EditorAssetDatabase mode (no bundles needed)
            var loadModeField = typeof(GameAssetManager).GetField("_loadMode",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            loadModeField?.SetValue(mgr, GameAssetLoadMode.EditorAssetDatabase);

            // Disable auto-initialize on Awake
            var autoInitField = typeof(GameAssetManager).GetField("_autoInitializeOnAwake",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            autoInitField?.SetValue(mgr, false);

            // Activate to trigger Awake (singleton registration)
            _managerGo.SetActive(true);

            // Wait a frame for Awake to complete
            await Task.Yield();

            // Initialize in EditorAssetDatabase mode (manifestless)
            var result = await mgr.InitializeAsync();
            if (!result)
            {
                Debug.LogError("[TestGameAssetHelper] Failed to initialize GameAssetManager.");
                return null;
            }

            return mgr;
        }

        public static void Cleanup()
        {
            if (_managerGo != null)
            {
                Object.DestroyImmediate(_managerGo);
                _managerGo = null;
            }
        }
    }
}
