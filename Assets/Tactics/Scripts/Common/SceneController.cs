using System.Threading.Tasks;
using Tactics.AssetPipeline;
using Tactics.Common.Battle;
using Tactics.Flow.Home;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tactics
{
    /// <summary>
    /// Unified scene entry controller. Replaces GameMain, SceneBootstrap, and HomeSceneEntry.
    /// Automatically dispatches initialization based on the active scene name:
    /// - Splash: initializes managers then loads Home scene.
    /// - Home: initializes managers then opens Home UI via HomeFlowCoordinator.
    /// - Other: initializes managers only (BattleController handles its own lifecycle).
    /// </summary>
    public sealed class SceneController : MonoBehaviour
    {
        [Tooltip("Shared options applied to the manager before it activates. Required for bootstrap.")]
        [SerializeField]
        private GameAssetRuntimeSettings _runtimeSettings;

        [Tooltip("Minimum time to keep the splash scene visible before loading Home.")]
        [SerializeField]
        private float _minimumSplashSeconds = 0.5f;

        private async void Start()
        {
            await InitializeManagersAsync();

            var sceneName = SceneManager.GetActiveScene().name;

            switch (sceneName)
            {
                case "Splash":
                    if (_minimumSplashSeconds > 0f)
                        await Task.Delay((int)(_minimumSplashSeconds * 1000f));
                    await LoadHomeAsync();
                    break;

                case "Home":
                    await HomeFlowCoordinator.Instance.ShowHomeUIAsync();
                    break;

                default:
                    var battleController = FindFirstObjectByType<BattleController>();
                    if (battleController != null)
                    {
                        Debug.Log($"[SceneController] BattleController detected in scene '{sceneName}'. Basic manager initialization complete.");
                    }
                    break;
            }
        }

        private async Task InitializeManagersAsync()
        {
            if (GameAssetManager.Instance != null && GameAssetManager.Instance.IsInitialized)
            {
                Debug.Log("[SceneController] GameAssetManager already initialized.");
                return;
            }

            if (_runtimeSettings == null)
            {
                Debug.LogError("[SceneController] Assign Game Asset Runtime Settings (ScriptableObject).");
                return;
            }

            Debug.Log("[SceneController] Initializing GameAssetManager...");

            if (GameAssetManager.Instance == null)
            {
                GameAssetManager.CreateBootstrap(_runtimeSettings);
            }

            var instance = GameAssetManager.Instance;
            if (instance == null)
            {
                Debug.LogError("[SceneController] GameAssetManager.Instance is still null after bootstrap.");
                return;
            }

            if (!instance.IsInitialized)
            {
                if (!await instance.InitializeAsync())
                {
                    Debug.LogError("[SceneController] GameAssetManager.InitializeAsync failed.");
                    return;
                }
            }

            Debug.Log("[SceneController] GameAssetManager initialized successfully.");
        }

        private async Task LoadHomeAsync()
        {
            var instance = GameAssetManager.Instance;
            if (instance == null)
                return;

            var path = SceneProjectPathHelper.ToProjectPath("Home");
            try
            {
                await instance.LoadSceneAsync(path);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
