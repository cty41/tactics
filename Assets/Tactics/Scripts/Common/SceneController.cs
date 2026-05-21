using System.Threading.Tasks;
using Tactics.Runtime.Utilities;
using Tactics.AssetPipeline;
using Tactics.Common.Battle;
using Tactics.Flow.Home;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tactics
{
    /// <summary>
    /// Roguelike 地图生成模式。
    /// </summary>
    public enum MapGenerationMode
    {
        Random,     // 随机生成（默认）
        LocalFile   // 从本地 JSON 配置文件加载
    }

    /// <summary>
    /// Unified scene entry controller. Replaces GameMain, SceneBootstrap, and HomeSceneEntry.
    /// Automatically dispatches initialization based on the active scene name:
    /// - Splash: initializes managers then loads Home scene.
    /// - Home: initializes managers then opens Home UI via HomeFlowCoordinator.
    /// - Other: initializes managers only (BattleController handles its own lifecycle).
    /// </summary>
    public sealed class SceneController : MonoBehaviour
    {
        public static SceneController Instance { get; private set; }

        [Tooltip("Shared options applied to the manager before it activates. Required for bootstrap.")]
        [SerializeField]
        private GameAssetRuntimeSettings _runtimeSettings;

        [Tooltip("Minimum time to keep the splash scene visible before loading Home.")]
        [SerializeField]
        private float _minimumSplashSeconds = 0.5f;

        [Header("Roguelike Map")]
        [Tooltip("地图生成方式：随机生成 / 从本地配置文件加载。")]
        [SerializeField]
        private MapGenerationMode _mapMode = MapGenerationMode.Random;

        [Tooltip("本地地图 JSON 配置文件。仅在 Mode = LocalFile 时使用。")]
        [SerializeField]
        private TextAsset _mapDataFile;

        public MapGenerationMode MapMode => _mapMode;
        public TextAsset MapDataFile => _mapDataFile;

        private void Awake()
        {
            Instance = this;
        }

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
                        TLog.Info($"[SceneController] BattleController detected in scene '{sceneName}'. Basic manager initialization complete.");
                    }
                    break;
            }
        }

        private async Task InitializeManagersAsync()
        {
            if (GameAssetManager.Instance != null && GameAssetManager.Instance.IsInitialized)
            {
                TLog.Info("[SceneController] GameAssetManager already initialized.");
                return;
            }

            if (_runtimeSettings == null)
            {
                TLog.Error("[SceneController] Assign Game Asset Runtime Settings (ScriptableObject).");
                return;
            }

            TLog.Info("[SceneController] Initializing GameAssetManager...");

            if (GameAssetManager.Instance == null)
            {
                GameAssetManager.CreateBootstrap(_runtimeSettings);
            }

            var instance = GameAssetManager.Instance;
            if (instance == null)
            {
                TLog.Error("[SceneController] GameAssetManager.Instance is still null after bootstrap.");
                return;
            }

            if (!instance.IsInitialized)
            {
                if (!await instance.InitializeAsync())
                {
                    TLog.Error("[SceneController] GameAssetManager.InitializeAsync failed.");
                    return;
                }
            }

            TLog.Info("[SceneController] GameAssetManager initialized successfully.");
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
                TLog.Error($"[SceneController] Exception: {e.Message}");
            }
        }
    }
}
