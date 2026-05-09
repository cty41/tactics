using System.Collections;
using Tactics.Runtime.Utilities;
using System.Threading.Tasks;
using Tactics.Flow.Home;
using Tactics.Flow.Roguelike;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

namespace Tactics.UI
{
    /// <summary>
    /// Home UI controller (UI Toolkit):
    /// - wires StartButton and EscButton from Home.uxml
    /// - handles Esc input through HomeFlowCoordinator
    /// </summary>
    public sealed class HomeUIController : UIControllerBase
    {
        [Header("Input (New Input System)")]
        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField] private string _cancelActionName = "Cancel";

        private Button _startButton;
        private Button _escButton;
        private InputAction _cancelAction;

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

            _startButton = root.Q<Button>("StartButton");
            _escButton = root.Q<Button>("EscButton");

            if (_startButton != null)
                _startButton.clicked += OnStartClicked;
            else
                TLog.Warning("[HomeUIController] StartButton not found in UXML.");

            if (_escButton != null)
                _escButton.clicked += OnEscButtonClicked;
            else
                TLog.Warning("[HomeUIController] EscButton not found in UXML.");

            AutoFindInputActions();
            WireInput();
        }

        private void AutoFindInputActions()
        {
            if (_inputActions != null) return;

            var module = Object.FindFirstObjectByType<InputSystemUIInputModule>();
            if (module != null)
                _inputActions = module.actionsAsset;
        }

        protected override void OnHidden()
        {
            UnwireButtons();
            UnwireInput();
        }

        private void OnDestroy()
        {
            UnwireButtons();
            UnwireInput();
        }

        private void UnwireButtons()
        {
            if (_startButton != null)
                _startButton.clicked -= OnStartClicked;
            if (_escButton != null)
                _escButton.clicked -= OnEscButtonClicked;
        }

        private void WireInput()
        {
            if (_inputActions == null)
            {
                TLog.Warning("[HomeUIController] _inputActions is null; keyboard Esc may not work.");
                return;
            }

            InputActionMap uiMap = _inputActions.FindActionMap("UI", true);
            if (uiMap == null)
            {
                TLog.Warning("[HomeUIController] No action map named 'UI' found in InputActionAsset.");
                return;
            }

            _cancelAction = uiMap.FindAction(_cancelActionName, true);
            if (_cancelAction != null)
            {
                _cancelAction.performed += OnCancelPerformed;
                _cancelAction.Enable();
            }
        }

        private void UnwireInput()
        {
            if (_cancelAction == null) return;
            _cancelAction.performed -= OnCancelPerformed;
            _cancelAction.Disable();
            _cancelAction = null;
        }

        private void OnCancelPerformed(InputAction.CallbackContext context)
        {
            _ = RequestToggleMenuAsync();
        }

        public void OnEscButtonClicked()
        {
            _ = RequestToggleMenuAsync();
        }

        private async Task RequestToggleMenuAsync()
        {
            try
            {
                await HomeFlowCoordinator.Instance.ToggleMenuAsync();
            }
            catch (System.Exception e)
            {
                TLog.Error($"[HomeUIController] Exception: {e.Message}");
            }
        }

        private void OnStartClicked()
        {
            _ = OpenMapUiFromHomeAsync();
        }

        private static async Task OpenMapUiFromHomeAsync()
        {
            try
            {
                UIManager.Instance.Hide(UIManager.UIId.Home);
                await RoguelikeFlowCoordinator.Instance.OpenMapAsync();
            }
            catch (System.Exception e)
            {
                TLog.Error($"[HomeUIController] Exception: {e.Message}");
            }
        }
    }
}
