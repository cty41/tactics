using Tactics.AssetPipeline;
using Tactics.Flow.Home;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.UI
{
    /// <summary>
    /// Menu UI controller (UI Toolkit):
    /// - wires menu buttons from Menu.uxml
    /// - executes the menu's internal business flows
    /// </summary>
    public sealed class MenuUIController : UIControllerBase
    {
        [SerializeField] private string _homeSceneName = "Home";

        private Button _continueButton;
        private Button _optionsButton;
        private Button _mainMenuButton;
        private Button _saveAndQuitButton;
        private bool _wired;

        protected override void OnShown()
        {
            if (_wired) return;
            WireMenuButtons();
            _wired = true;
        }

        protected override void OnHidden()
        {
            UnwireMenuButtons();
            _wired = false;
        }

        private void WireMenuButtons()
        {
            var root = Ui.GetRootElement(UIManager.UIId.Menu);
            if (root == null)
            {
                Debug.LogWarning("[MenuUIController] Could not get root visual element for Menu UI.");
                return;
            }

            _continueButton = root.Q<Button>("ContinueButton");
            _optionsButton = root.Q<Button>("OptionsButton");
            _mainMenuButton = root.Q<Button>("MainMenuButton");
            _saveAndQuitButton = root.Q<Button>("SaveAndQuitButton");

            if (_continueButton != null)
                _continueButton.clicked += OnContinueClicked;
            else
                Debug.LogWarning("[MenuUIController] ContinueButton not found in UXML.");

            if (_optionsButton != null)
                _optionsButton.clicked += OnOptionsClicked;
            else
                Debug.LogWarning("[MenuUIController] OptionsButton not found in UXML.");

            if (_mainMenuButton != null)
                _mainMenuButton.clicked += OnMainMenuClicked;
            else
                Debug.LogWarning("[MenuUIController] MainMenuButton not found in UXML.");

            if (_saveAndQuitButton != null)
                _saveAndQuitButton.clicked += OnSaveAndQuitClicked;
            else
                Debug.LogWarning("[MenuUIController] SaveAndQuitButton not found in UXML.");
        }

        private void UnwireMenuButtons()
        {
            if (_continueButton != null)
                _continueButton.clicked -= OnContinueClicked;
            if (_optionsButton != null)
                _optionsButton.clicked -= OnOptionsClicked;
            if (_mainMenuButton != null)
                _mainMenuButton.clicked -= OnMainMenuClicked;
            if (_saveAndQuitButton != null)
                _saveAndQuitButton.clicked -= OnSaveAndQuitClicked;
        }

        private void OnContinueClicked()
        {
            HomeFlowCoordinator.Instance.CloseMenu();
        }

        private void OnMainMenuClicked()
        {
            HomeFlowCoordinator.Instance.CloseMenu();
            SceneProjectPathHelper.TryLoadSceneViaAssetManager(_homeSceneName);
        }

        private void OnSaveAndQuitClicked()
        {
            // TODO: hook up your existing save/quit flow.
            HomeFlowCoordinator.Instance.CloseMenu();
            SceneProjectPathHelper.TryLoadSceneViaAssetManager(_homeSceneName);
        }

        private void OnOptionsClicked()
        {
            // TODO: open an options sub-menu if you implement one later.
            HomeFlowCoordinator.Instance.CloseMenu();
        }
    }
}
