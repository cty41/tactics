using System.Threading.Tasks;
using UnityEngine;

namespace Tactics.Flow.Home
{
    /// <summary>
    /// Home domain coordinator.
    /// Owns Home-specific UI policies (menu open/close/toggle) and delegates infra work to UIManager.
    /// </summary>
    public sealed class HomeFlowCoordinator
    {
        private static readonly HomeFlowCoordinator _instance = new HomeFlowCoordinator();
        public static HomeFlowCoordinator Instance => _instance;

        private bool _isMenuTransitioning;

        private HomeFlowCoordinator() { }

        public async Task OpenMenuAsync()
        {
            if (_isMenuTransitioning) return;
            if (UIManager.Instance == null)
            {
                Debug.LogError("[HomeFlowCoordinator] UIManager.Instance is null. Cannot open menu.");
                return;
            }
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
            if (UIManager.Instance == null)
            {
                Debug.LogError("[HomeFlowCoordinator] UIManager.Instance is null. Cannot close menu.");
                return;
            }
            UIManager.Instance.Hide(UIManager.UIId.Menu);
        }

        public async Task ToggleMenuAsync()
        {
            if (_isMenuTransitioning) return;
            if (UIManager.Instance == null)
            {
                Debug.LogError("[HomeFlowCoordinator] UIManager.Instance is null. Cannot toggle menu.");
                return;
            }
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
            if (UIManager.Instance == null)
            {
                Debug.LogError("[HomeFlowCoordinator] UIManager.Instance is null. Cannot destroy menu.");
                return;
            }
            UIManager.Instance.Destroy(UIManager.UIId.Menu);
        }
    }
}
