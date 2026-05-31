using System.Threading.Tasks;
using Tactics.AssetPipeline;
using Tactics.Runtime.Utilities;
using UnityEngine;
using Tactics.Flow.Roguelike;
using Tactics.Roguelike;

namespace Tactics.Flow.Home
{
    public sealed class HomeFlowCoordinator
    {
        private static readonly HomeFlowCoordinator _instance = new HomeFlowCoordinator();
        public static HomeFlowCoordinator Instance => _instance;

        private bool _isMenuTransitioning;
        private bool _menuPausedGame;

        private HomeFlowCoordinator() { }

        public async Task ShowHomeUIAsync()
        {
            // 等待 GameAssetManager 就绪，带超时保护
            for (int i = 0; i < 120; i++) // 最多等 120 帧
            {
                var mgr = GameAssetManager.Instance;
                if (mgr != null && mgr.IsInitialized)
                {
                    TLog.Info("[HomeFlowCoordinator] GameAssetManager ready, showing Home UI");
                    await UIManager.Instance.ShowAsync(UIManager.UIId.Home);
                    await TryResumeRoguelikeMapAsync();
                    return;
                }
                await Task.Yield();
            }
            
            TLog.Error("[HomeFlowCoordinator] GameAssetManager not initialized after 120 frames, giving up");
        }

        private static async Task TryResumeRoguelikeMapAsync()
        {
            if (!RoguelikeMapRuntimeState.ConsumeResumeMapOnHomeFlag())
            {
                UIManager.Instance.Hide(UIManager.UIId.Loading);
                return;
            }

            UIManager.Instance.Show(UIManager.UIId.Loading);
            UIManager.Instance.Hide(UIManager.UIId.Home);

            bool mapReady = await RoguelikeFlowCoordinator.Instance.OpenMapAndWaitReadyAsync();
            if (mapReady)
            {
                UIManager.Instance.Hide(UIManager.UIId.Loading);
                return;
            }

            TLog.Error("[HomeFlowCoordinator] RoguelikeMap failed to become ready after battle return.");
            UIManager.Instance.Hide(UIManager.UIId.Loading);
            await UIManager.Instance.ShowAsync(UIManager.UIId.Home);
        }

        public async Task OpenMenuAsync()
        {
            if (_isMenuTransitioning) return;
            _isMenuTransitioning = true;
            try
            {
                await UIManager.Instance.ShowAsync(UIManager.UIId.Menu);
                if (!_menuPausedGame)
                {
                    GamePauseService.Pause();
                    _menuPausedGame = true;
                }
            }
            finally
            {
                _isMenuTransitioning = false;
            }
        }

        public void CloseMenu()
        {
            UIManager.Instance.Hide(UIManager.UIId.Menu);
            if (_menuPausedGame)
            {
                GamePauseService.Resume();
                _menuPausedGame = false;
            }
        }

        public async Task ToggleMenuAsync()
        {
            if (_isMenuTransitioning) return;
            _isMenuTransitioning = true;
            try
            {
                if (UIManager.Instance.IsVisible(UIManager.UIId.Menu))
                {
                    CloseMenu();
                    return;
                }

                await UIManager.Instance.ShowAsync(UIManager.UIId.Menu);
                if (!_menuPausedGame)
                {
                    GamePauseService.Pause();
                    _menuPausedGame = true;
                }
            }
            finally
            {
                _isMenuTransitioning = false;
            }
        }

        public void DestroyMenu()
        {
            UIManager.Instance.Destroy(UIManager.UIId.Menu);
            if (_menuPausedGame)
            {
                GamePauseService.Resume();
                _menuPausedGame = false;
            }
        }

        public async Task OpenOptionsFromMenuAsync()
        {
            await UIManager.Instance.ShowAsync(UIManager.UIId.Options);
        }

        public void ForceResumeForSceneTransition()
        {
            _menuPausedGame = false;
            GamePauseService.ForceResume();
        }
    }
}
