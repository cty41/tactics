using System.Threading.Tasks;
using Tactics.Runtime.Utilities;
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
        private bool _settlementInProgress;

        private BattleFlowCoordinator() { }

        public async Task StartBattleAsync(string battleSceneName)
        {
            if (_isTransitioning) return;

            if (SceneManager.GetActiveScene().name == battleSceneName)
            {
                TLog.Warning("[BattleFlowCoordinator] Battle scene is already active.");
                return;
            }

            _isTransitioning = true;
            try
            {
                // Show loading screen synchronously before destroying existing UIs to avoid empty frames.
                UIManager.Instance.Show(UIManager.UIId.Loading);

                UIManager.Instance.Destroy(UIManager.UIId.RoguelikeMap);
                UIManager.Instance.Destroy(UIManager.UIId.Home);
                UIManager.Instance.Destroy(UIManager.UIId.Menu);

                var scenePath = SceneProjectPathHelper.ToProjectPath(battleSceneName);
                var mgr = GameAssetManager.Instance;
                if (mgr == null || !mgr.IsInitialized)
                {
                    TLog.Error("[BattleFlowCoordinator] GameAssetManager is not initialized.");
                    return;
                }

                await mgr.LoadSceneAsync(scenePath, LoadSceneMode.Single);

                await Task.Yield();
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        public async Task EndBattleAsync(GameResult result, bool skipSettlement = false)
        {
            if (_isTransitioning) return;

            if (!skipSettlement)
            {
                if (_settlementInProgress)
                {
                    TLog.Warning("[BattleFlowCoordinator] Settlement already in progress, skipping duplicate.");
                    return;
                }
                _settlementInProgress = true;
            }

            _isTransitioning = true;
            try
            {
                UIManager.Instance.Show(UIManager.UIId.Loading);

                // Drain tracked runtime work before replacing the battle scene.
                var controller = BattleController.Instance;
                if (controller != null)
                {
                    await controller.TeardownRuntimeScopeAsync();
                }

                string returnSceneName = PlayerPrefs.GetString(
                    RoguelikeMapUIController.RoguelikeReturnScenePrefsKey, "Home");
                if (string.IsNullOrWhiteSpace(returnSceneName))
                    returnSceneName = "Home";

                UIManager.Instance.Destroy(UIManager.UIId.Battle);

                var mgr = GameAssetManager.Instance;
                if (mgr != null && mgr.IsInitialized)
                {
                    var returnPath = SceneProjectPathHelper.ToProjectPath(returnSceneName);
                    // Single-mode loading replaces the battle scene atomically. Explicitly
                    // unloading Test1 first is invalid when it is the only loaded scene and
                    // emits an engine Error before the return scene can be loaded.
                    await mgr.LoadSceneAsync(returnPath, LoadSceneMode.Single);
                }
            }
            finally
            {
                _isTransitioning = false;
                if (!skipSettlement)
                {
                    _settlementInProgress = false;
                }
            }
        }
    }
}
