using System.Threading.Tasks;
using Tactics.AssetPipeline;
using Tactics.Flow.Home;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;

namespace Tactics.UI
{
    /// <summary>
    /// Home UI controller:
    /// - wires StartButton
    /// - handles Esc input + EscButton click through HomeFlowCoordinator
    /// </summary>
    public sealed class HomeUIController : UIControllerBase
    {
        [Header("Scene Navigation")]
        [SerializeField] private string _mapSceneName = "SampleScene";

        [Header("Buttons (from Home.prefab)")]
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _escButton;

        [Header("Input (New Input System)")]
        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField] private string _cancelActionName = "Cancel";

        private InputAction _cancelAction;
        private bool _startWired;

        private void Awake()
        {
            RebindButtonsFromHierarchy();
            WireButtons();
            AutoFindInputActions();

            if (_escButton != null)
            {
                _escButton.onClick.RemoveListener(OnEscButtonClicked);
                _escButton.onClick.AddListener(OnEscButtonClicked);
            }
            else
            {
                Debug.LogWarning("[HomeUIController] EscButton is not assigned/found; Esc UI button click will not work.");
            }
        }

        private void RebindButtonsFromHierarchy()
        {
            // Force-rebind every time to avoid stale serialized refs after prefab reconstruction.
            _startButton = null;
            _escButton = null;

            foreach (var btn in GetComponentsInChildren<Button>(true))
            {
                if (btn == null) continue;
                if (btn.name == "StartButton")
                {
                    _startButton = btn;
                }
                else if (btn.name == "EscButton")
                {
                    _escButton = btn;
                }
            }

            // Fallback: match by TMP label (case-insensitive).
            if (_escButton == null)
            {
                foreach (var btn in GetComponentsInChildren<Button>(true))
                {
                    var label = btn.GetComponentInChildren<TextMeshProUGUI>(true);
                    var txt = label?.text?.Trim();
                    if (string.IsNullOrEmpty(txt)) continue;

                    if (txt.Equals("ESC", System.StringComparison.OrdinalIgnoreCase) ||
                        txt.Equals("ESCAPE", System.StringComparison.OrdinalIgnoreCase))
                    {
                        _escButton = btn;
                        break;
                    }
                }
            }
        }

        private void WireButtons()
        {
            if (_startButton == null)
            {
                Debug.LogWarning("[HomeUIController] StartButton is not assigned/found; Start click will not work.");
                return;
            }

            _startButton.onClick.RemoveListener(OnStartClicked);
            _startButton.onClick.AddListener(OnStartClicked);
            _startWired = true;
        }

        private void AutoFindInputActions()
        {
            if (_inputActions != null) return;

            var module = Object.FindFirstObjectByType<InputSystemUIInputModule>();
            if (module != null)
                _inputActions = module.actionsAsset;
        }

        private void OnDestroy()
        {
            if (_startButton != null && _startWired)
                _startButton.onClick.RemoveListener(OnStartClicked);

            if (_escButton != null) _escButton.onClick.RemoveListener(OnEscButtonClicked);

            UnwireInput();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            WireInput();
        }

        protected override void OnDisable()
        {
            UnwireInput();
            base.OnDisable();
        }

        protected override void OnShown()
        {
            // Intentionally empty: Home UI is typically always active.
        }

        protected override void OnHidden()
        {
            // Intentionally empty.
        }

        private void WireInput()
        {
            if (_inputActions == null)
            {
                Debug.LogWarning("[HomeUIController] _inputActions is null; keyboard Esc may not work.");
                return;
            }

            InputActionMap uiMap = _inputActions.FindActionMap("UI", true);
            if (uiMap == null)
            {
                Debug.LogWarning("[HomeUIController] No action map named 'UI' found in InputActionAsset.");
                return;
            }

            _cancelAction = uiMap.FindAction(_cancelActionName, true);
            _cancelAction.performed += OnCancelPerformed;
            _cancelAction.Enable();
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
                Debug.LogException(e);
            }
        }

        private void OnStartClicked()
        {
            // Reuse the asset-pipeline scene loader (same behavior as HomeStartMenu).
            bool ok = SceneProjectPathHelper.TryLoadSceneViaAssetManager(_mapSceneName);
            if (!ok)
                Debug.LogError($"[HomeUIController] Start click failed to load scene '{_mapSceneName}'.");
        }
    }
}

