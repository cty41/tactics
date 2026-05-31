using System.Collections;
using Tactics.Runtime.Utilities;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.UI
{
    /// <summary>
    /// Home UI controller (UI Toolkit):
    /// - wires main menu buttons from Home.uxml
    /// </summary>
    public sealed class HomeUIController : UIControllerBase
    {
        private Button _newGameButton;
        private Button _loadGameButton;
        private Button _optionsButton;
        private Button _quitButton;

        protected override void OnShown()
        {
            StartCoroutine(WireButtonsDelayed());
        }

        private IEnumerator WireButtonsDelayed()
        {
            yield return null;
            WireButtons();
        }

        private void WireButtons()
        {
            var root = Ui.GetRootElement(UIManager.UIId.Home);
            if (root == null)
            {
                TLog.Warning("[HomeUIController] Could not get root visual element for Home UI.");
                return;
            }

            _newGameButton = root.Q<Button>("NewGameButton") ?? root.Q<Button>("StartButton");
            _loadGameButton = root.Q<Button>("LoadGameButton");
            _optionsButton = root.Q<Button>("OptionsButton");
            _quitButton = root.Q<Button>("QuitButton");

            if (_newGameButton != null)
                _newGameButton.clicked += OnNewGameClicked;
            else
                TLog.Warning("[HomeUIController] NewGameButton not found in UXML.");

            if (_loadGameButton != null)
                _loadGameButton.clicked += OnLoadGameClicked;
            else
                TLog.Warning("[HomeUIController] LoadGameButton not found in UXML.");

            if (_optionsButton != null)
                _optionsButton.clicked += OnOptionsClicked;
            else
                TLog.Warning("[HomeUIController] OptionsButton not found in UXML.");

            if (_quitButton != null)
                _quitButton.clicked += OnQuitClicked;
            else
                TLog.Warning("[HomeUIController] QuitButton not found in UXML.");
        }

        protected override void OnHidden()
        {
            UnwireButtons();
        }

        private void OnDestroy()
        {
            UnwireButtons();
        }

        private void UnwireButtons()
        {
            if (_newGameButton != null)
                _newGameButton.clicked -= OnNewGameClicked;
            if (_loadGameButton != null)
                _loadGameButton.clicked -= OnLoadGameClicked;
            if (_optionsButton != null)
                _optionsButton.clicked -= OnOptionsClicked;
            if (_quitButton != null)
                _quitButton.clicked -= OnQuitClicked;
        }

        private void OnNewGameClicked()
        {
            _ = OpenNewGameSlotsAsync();
        }

        private void OnLoadGameClicked()
        {
            _ = OpenLoadGameSlotsAsync();
        }

        private void OnOptionsClicked()
        {
            _ = OptionsUIController.ShowAsync(false);
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            TLog.Info("[HomeUIController] Quit requested in Editor.");
#else
            Application.Quit();
#endif
        }

        private static async Task OpenNewGameSlotsAsync()
        {
            try
            {
                await SlotSelectUIController.ShowForNewGameAsync();
            }
            catch (System.Exception e)
            {
                TLog.Error($"[HomeUIController] Exception: {e.Message}");
            }
        }

        private static async Task OpenLoadGameSlotsAsync()
        {
            try
            {
                await SlotSelectUIController.ShowForLoadGameAsync();
            }
            catch (System.Exception e)
            {
                TLog.Error($"[HomeUIController] Exception: {e.Message}");
            }
        }
    }
}
