using System.Collections;
using Tactics.Flow.Roguelike;
using Tactics.Roguelike;
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
        private VisualElement _overwritePrompt;
        private Button _confirmNewRunButton;
        private Button _cancelNewRunButton;
        private bool _isWired;

        public bool IsReadyForInput =>
            _isWired &&
            _newGameButton?.panel != null &&
            _newGameButton.enabledInHierarchy;

        protected override void OnShown()
        {
            // Cached UI instances can be shown with a rebuilt UIDocument tree after a
            // scene/fixture re-entry. Unbind the previous tree before querying the new one.
            UnwireButtons();
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
            _overwritePrompt = root.Q<VisualElement>("OverwritePrompt");
            _confirmNewRunButton = root.Q<Button>("ConfirmNewRunButton");
            _cancelNewRunButton = root.Q<Button>("CancelNewRunButton");

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

            if (_confirmNewRunButton != null)
                _confirmNewRunButton.clicked += OnConfirmNewRunClicked;
            if (_cancelNewRunButton != null)
                _cancelNewRunButton.clicked += HideOverwritePrompt;

            _isWired = true;
            RefreshRunActions();
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
            if (_confirmNewRunButton != null)
                _confirmNewRunButton.clicked -= OnConfirmNewRunClicked;
            if (_cancelNewRunButton != null)
                _cancelNewRunButton.clicked -= HideOverwritePrompt;

            _isWired = false;
        }

        private void OnNewGameClicked()
        {
            if (PureRunSessionStore.HasActiveRun)
            {
                ShowOverwritePrompt();
                return;
            }

            _ = StartNewRunAsync();
        }

        private void OnLoadGameClicked()
        {
            if (!PureRunSessionStore.TryLoad(out _, out _))
            {
                RefreshRunActions();
                return;
            }

            _ = OpenRunMapAsync();
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

        private void OnConfirmNewRunClicked()
        {
            HideOverwritePrompt();
            _ = StartNewRunAsync();
        }

        private static async Task StartNewRunAsync()
        {
            try
            {
                PureRunSessionStore.Clear();
                await OpenRunMapAsync();
            }
            catch (System.Exception e)
            {
                TLog.Error($"[HomeUIController] Exception: {e.Message}");
            }
        }

        private static async Task OpenRunMapAsync()
        {
            UIManager.Instance.Hide(UIManager.UIId.Home);
            await RoguelikeFlowCoordinator.Instance.OpenMapAsync();
        }

        private void RefreshRunActions()
        {
            if (_loadGameButton != null)
                _loadGameButton.SetEnabled(PureRunSessionStore.HasActiveRun);
            HideOverwritePrompt();
        }

        private void ShowOverwritePrompt()
        {
            if (_overwritePrompt != null)
                _overwritePrompt.style.display = DisplayStyle.Flex;
        }

        private void HideOverwritePrompt()
        {
            if (_overwritePrompt != null)
                _overwritePrompt.style.display = DisplayStyle.None;
        }
    }
}
