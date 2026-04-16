using System.Threading.Tasks;
using UnityEngine;

namespace Tactics.Flow.Roguelike
{
    /// <summary>
    /// Roguelike domain coordinator.
    /// Owns map UI open/close/toggle policies and delegates infra work to UIManager.
    /// </summary>
    public sealed class RoguelikeFlowCoordinator
    {
        private static readonly RoguelikeFlowCoordinator _instance = new RoguelikeFlowCoordinator();
        public static RoguelikeFlowCoordinator Instance => _instance;

        private bool _isMapUiTransitioning;

        private RoguelikeFlowCoordinator() { }

        public async Task OpenMapAsync()
        {
            if (_isMapUiTransitioning) return;

            _isMapUiTransitioning = true;
            try
            {
                await UIManager.Instance.ShowAsync(UIManager.UIId.RoguelikeMap);
            }
            finally
            {
                _isMapUiTransitioning = false;
            }
        }

        public void CloseMap()
        {
            UIManager.Instance.Hide(UIManager.UIId.RoguelikeMap);
        }

        public async Task ToggleMapAsync()
        {
            if (_isMapUiTransitioning) return;

            _isMapUiTransitioning = true;
            try
            {
                if (UIManager.Instance.IsVisible(UIManager.UIId.RoguelikeMap))
                {
                    UIManager.Instance.Hide(UIManager.UIId.RoguelikeMap);
                    return;
                }

                await UIManager.Instance.ShowAsync(UIManager.UIId.RoguelikeMap);
            }
            finally
            {
                _isMapUiTransitioning = false;
            }
        }

        public void DestroyMap()
        {
            UIManager.Instance.Destroy(UIManager.UIId.RoguelikeMap);
        }
    }
}
