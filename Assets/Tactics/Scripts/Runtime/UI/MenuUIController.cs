using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Tactics.AssetPipeline;
using Tactics.Flow.Home;

namespace Tactics.UI
{
    /// <summary>
    /// Menu prefab controller:
    /// - wires MenuRow_* buttons
    /// - executes the menu's internal business flows
    /// </summary>
    public sealed class MenuUIController : UIControllerBase
    {
        [SerializeField] private string _homeSceneName = "Home";

        private bool _wired;

        protected override void OnShown()
        {
            if (_wired) return;
            WireMenuButtons();
            _wired = true;
        }

        private void WireMenuButtons()
        {
            // Menu prefab uses TMP labels (CONTINUE / MAIN MENU / SAVE AND QUIT / OPTIONS).
            // Instead of relying on child object names, match by displayed text for robustness.
            bool wiredContinue = false;
            bool wiredMainMenu = false;
            bool wiredSaveAndQuit = false;
            bool wiredOptions = false;

            Button[] allButtons = GetComponentsInChildren<Button>(true);
            foreach (Button button in allButtons)
            {
                TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label == null) continue;

                string text = (label.text ?? string.Empty).Trim();
                if (text.Length == 0) continue;

                if (text.Equals("CONTINUE", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (!wiredContinue)
                    {
                        button.onClick.AddListener(OnContinueClicked);
                        wiredContinue = true;
                    }
                }
                else if (text.Equals("MAIN MENU", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (!wiredMainMenu)
                    {
                        button.onClick.AddListener(OnMainMenuClicked);
                        wiredMainMenu = true;
                    }
                }
                else if (text.Equals("SAVE AND QUIT", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (!wiredSaveAndQuit)
                    {
                        button.onClick.AddListener(OnSaveAndQuitClicked);
                        wiredSaveAndQuit = true;
                    }
                }
                else if (text.Equals("OPTIONS", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (!wiredOptions)
                    {
                        button.onClick.AddListener(OnOptionsClicked);
                        wiredOptions = true;
                    }
                }
            }

            if (!wiredContinue)
                Debug.LogWarning("[MenuUIController] Could not find a CONTINUE button (TMP text not found).");
            if (!wiredMainMenu)
                Debug.LogWarning("[MenuUIController] Could not find a MAIN MENU button (TMP text not found).");
            if (!wiredSaveAndQuit)
                Debug.LogWarning("[MenuUIController] Could not find a SAVE AND QUIT button (TMP text not found).");
            if (!wiredOptions)
                Debug.LogWarning("[MenuUIController] Could not find an OPTIONS button (TMP text not found).");
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

