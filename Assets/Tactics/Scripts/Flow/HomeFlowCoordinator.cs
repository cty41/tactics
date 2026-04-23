using System.Threading.Tasks;
using Tactics.AssetPipeline;
using UnityEngine;

namespace Tactics.Flow.Home
{
    public sealed class HomeFlowCoordinator
    {
        private static readonly HomeFlowCoordinator _instance = new HomeFlowCoordinator();
        public static HomeFlowCoordinator Instance => _instance;

        private bool _isMenuTransitioning;

        private HomeFlowCoordinator() { }

        public async Task ShowHomeUIAsync()
        {
            while (GameAssetManager.Instance == null || !GameAssetManager.Instance.IsInitialized)
            {
                await Task.Yield();
            }

            await UIManager.Instance.ShowAsync(UIManager.UIId.Home);
        }

        public async Task OpenMenuAsync()
        {
            if (_isMenuTransitioning) return;
            _isMenuTransitioning = true;
            try
            {
                await UIManager.Instance.ShowAsync(UIManager.UIId.Menu);
            }
            finally
            {
                _isMenuTransitioning = false;
            }
        }

        public void CloseMenu()
        {
            UIManager.Instance.Hide(UIManager.UIId.Menu);
        }

        public async Task ToggleMenuAsync()
        {
            if (_isMenuTransitioning) return;
            _isMenuTransitioning = true;
            try
            {
                if (UIManager.Instance.IsVisible(UIManager.UIId.Menu))
                {
                    UIManager.Instance.Hide(UIManager.UIId.Menu);
                    return;
                }

                await UIManager.Instance.ShowAsync(UIManager.UIId.Menu);
            }
            finally
            {
                _isMenuTransitioning = false;
            }
        }

        public void DestroyMenu()
        {
            UIManager.Instance.Destroy(UIManager.UIId.Menu);
        }
    }
}
