using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Tactics.AssetPipeline;
using Tactics.Common.Controllers.GameResolvers;
using Tactics.Common.Battle;
using Tactics.UI;

namespace Tactics.Flow.Battle
{
    public sealed class BattleFlowCoordinator
    {
        private static readonly BattleFlowCoordinator _instance = new BattleFlowCoordinator();
        public static BattleFlowCoordinator Instance => _instance;

        private bool _isTransitioning;

        private BattleFlowCoordinator() { }

        public async Task StartBattleAsync(string battleSceneName)
        {
            if (_isTransitioning) return;

            if (SceneManager.GetActiveScene().name == battleSceneName)
            {
                Debug.LogWarning("[BattleFlowCoordinator] Battle scene is already active.");
                return;
            }

            _isTransitioning = true;
            try
            {
                var scenePath = SceneProjectPathHelper.ToProjectPath(battleSceneName);
                var mgr = GameAssetManager.Instance;
                if (mgr == null || !mgr.IsInitialized)
                {
                    Debug.LogError("[BattleFlowCoordinator] GameAssetManager is not initialized.");
                    return;
                }

                await mgr.LoadSceneAsync(scenePath, LoadSceneMode.Additive);

                await Task.Yield();
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        public async Task EndBattleAsync(GameResult result)
        {
            if (_isTransitioning) return;

            _isTransitioning = true;
            try
            {
                string returnSceneName = PlayerPrefs.GetString(
                    RoguelikeMapUIController.RoguelikeReturnScenePrefsKey, "Home");
                if (string.IsNullOrWhiteSpace(returnSceneName))
                    returnSceneName = "Home";

                UIManager.Instance.Destroy(UIManager.UIId.Battle);

                var battleScene = SceneManager.GetSceneByName("Test1");
                if (battleScene.isLoaded)
                {
                    var unloadOp = SceneManager.UnloadSceneAsync(battleScene);
                    while (!unloadOp.isDone)
                        await Task.Yield();
                }

                var mgr = GameAssetManager.Instance;
                if (mgr != null && mgr.IsInitialized)
                {
                    var returnPath = SceneProjectPathHelper.ToProjectPath(returnSceneName);
                    await mgr.LoadSceneAsync(returnPath, LoadSceneMode.Single);
                }
            }
            finally
            {
                _isTransitioning = false;
            }
        }
    }
}
