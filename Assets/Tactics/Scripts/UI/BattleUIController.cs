using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Tactics.Common.Controllers;
using Tactics.Common.Controllers.GameResolvers;
using Tactics.Common.Players;
using Tactics.Common.Battle;

namespace Tactics.UI
{
    /// <summary>
    /// UI Toolkit controller for Battle.uxml.
    /// Attached to the UIDocument GameObject by UIManager.
    /// Manages battle UI buttons, HP/MP bars, and turn state feedback.
    /// </summary>
    public sealed class BattleUIController : UIControllerBase
    {
        private Button _endTurnButton;
        private Button _moveButton;
        private Button _skill1Button;
        private Button _skill2Button;
        private ProgressBar _hpBar;
        private ProgressBar _mpBar;
        private UnityGridController _gridController;
        private InputAction _endTurnAction;

        protected override void OnShown()
        {
            // Delay one frame to ensure UIDocument.rootVisualElement is ready
            StartCoroutine(WireButtonsDelayed());
        }

        private System.Collections.IEnumerator WireButtonsDelayed()
        {
            yield return null;
            WireButtons();
        }

        protected override void OnHidden()
        {
            UnwireButtons();
            if (_gridController != null)
            {
                _gridController.TurnStarted -= OnTurnStarted;
                _gridController.GameEnded -= OnGameEnded;
            }
        }

        private void WireButtons()
        {
            var root = Ui.GetRootElement(UIManager.UIId.Battle);
            if (root == null)
            {
                Debug.LogWarning("[BattleUIController] Could not get root visual element for Battle UI.");
                return;
            }

            _endTurnButton = root.Q<Button>("EndTurnButton");
            _moveButton = root.Q<Button>("MoveButton");
            _skill1Button = root.Q<Button>("Skill1Button");
            _skill2Button = root.Q<Button>("Skill2Button");
            _hpBar = root.Q<ProgressBar>("hp");
            _mpBar = root.Q<ProgressBar>("mp");

            if (_endTurnButton != null) _endTurnButton.clicked += OnEndTurnClicked;
            if (_moveButton != null) _moveButton.clicked += OnMoveClicked;
            if (_skill1Button != null) _skill1Button.clicked += OnSkill1Clicked;
            if (_skill2Button != null) _skill2Button.clicked += OnSkill2Clicked;

            // Find GridController from the currently loaded battle scene
            _gridController = Object.FindFirstObjectByType<UnityGridController>();
            if (_gridController == null)
            {
                Debug.LogWarning("[BattleUIController] UnityGridController not found in scene.");
                return;
            }

            _canEndTurn = true;

            // Subscribe to turn/game events for UI state management
            _gridController.TurnStarted += OnTurnStarted;
            _gridController.GameEnded += OnGameEnded;

            // Subscribe to EndTurn input action
            InputActionAsset inputActions = null;

            var inputModule = Object.FindFirstObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (inputModule != null)
            {
                inputActions = inputModule.actionsAsset;
            }

            if (inputActions == null)
            {
                var playerInputs = Object.FindObjectsByType<UnityEngine.InputSystem.PlayerInput>(FindObjectsSortMode.None);
                if (playerInputs.Length > 0)
                {
                    inputActions = playerInputs[0].actions;
                }
            }

            if (inputActions == null)
            {
                Debug.LogWarning("[BattleUIController] No InputActionAsset found (neither InputSystemUIInputModule nor PlayerInput).");
                return;
            }

            var playerMap = inputActions.FindActionMap("Player");
            if (playerMap != null)
            {
                _endTurnAction = playerMap.FindAction("EndTurn");
                if (_endTurnAction != null)
                {
                    _endTurnAction.performed += OnEndTurnPerformed;
                    _endTurnAction.Enable();
                }
                else
                {
                    Debug.LogWarning("[BattleUIController] EndTurn action not found in Player action map.");
                }
            }
            else
            {
                Debug.LogWarning("[BattleUIController] Player action map not found.");
            }
        }

        private void UnwireButtons()
        {
            if (_endTurnButton != null) _endTurnButton.clicked -= OnEndTurnClicked;
            if (_moveButton != null) _moveButton.clicked -= OnMoveClicked;
            if (_skill1Button != null) _skill1Button.clicked -= OnSkill1Clicked;
            if (_skill2Button != null) _skill2Button.clicked -= OnSkill2Clicked;

            if (_endTurnAction != null)
            {
                _endTurnAction.performed -= OnEndTurnPerformed;
                _endTurnAction.Disable();
            }
        }

        private void OnEndTurnClicked()
        {
            if (_gridController != null)
                _gridController.EndTurn();
        }

        private void OnMoveClicked()
        {
            Debug.Log("[BattleUIController] Move button clicked");
        }

        private void OnSkill1Clicked()
        {
            Debug.Log("[BattleUIController] Skill 1 button clicked");
        }

        private void OnSkill2Clicked()
        {
            Debug.Log("[BattleUIController] Skill 2 button clicked");
        }

        private void OnEndTurnPerformed(InputAction.CallbackContext context)
        {
            if (_canEndTurn && _gridController != null)
            {
                _gridController.EndTurn();
            }
        }

        private bool _canEndTurn;

        private void OnTurnStarted(TurnTransitionParams turnTransitionParams)
        {
            // Only allow manual end turn when it's a human player's turn
            bool isHumanTurn = turnTransitionParams.TurnContext.CurrentPlayer.PlayerType == PlayerType.HumanPlayer;
            _canEndTurn = isHumanTurn;
            if (_endTurnButton != null)
                _endTurnButton.SetEnabled(isHumanTurn);
        }

        private void OnGameEnded(GameResult gameResult)
        {
            // Disable end turn button when game ends
            _canEndTurn = false;
            if (_endTurnButton != null)
                _endTurnButton.SetEnabled(false);
        }

        /// <summary>
        /// Update HP progress bar.
        /// </summary>
        /// <param name="value">Current HP value.</param>
        /// <param name="maxValue">Maximum HP value.</param>
        public void UpdateHpBar(float value, float maxValue)
        {
            if (_hpBar == null) return;
            _hpBar.highValue = maxValue;
            _hpBar.value = value;
        }

        /// <summary>
        /// Update MP progress bar.
        /// </summary>
        /// <param name="value">Current MP value.</param>
        /// <param name="maxValue">Maximum MP value.</param>
        public void UpdateMpBar(float value, float maxValue)
        {
            if (_mpBar == null) return;
            _mpBar.highValue = maxValue;
            _mpBar.value = value;
        }
    }
}
