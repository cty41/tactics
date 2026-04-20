using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Tactics.Common.Controllers;
using Tactics.Common.Controllers.GameResolvers;
using Tactics.Common.Controllers.GridStates;
using Tactics.Common.Players;
using Tactics.Common.Battle;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;

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
        private VisualElement _skillPanel;
        private readonly List<Button> _skillButtons = new List<Button>();
        private readonly List<System.Action> _skillButtonCallbacks = new List<System.Action>();
        private ProgressBar _hpBar;
        private ProgressBar _mpBar;
        private VisualElement _bottomPanel;
        private IGridController _gridController;
        private InputAction _endTurnAction;
        private IUnit _currentSelectedUnit;
        private IAbility _currentMoveAbility;

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
            _skillPanel = root.Q<VisualElement>("SkillPanel");
            _hpBar = root.Q<ProgressBar>("hp");
            _mpBar = root.Q<ProgressBar>("mp");
            _bottomPanel = root.Q<VisualElement>("BottomPanel");

            // Query skill buttons from SkillPanel
            var skill1Button = root.Q<Button>("Skill1Button");
            var skill2Button = root.Q<Button>("Skill2Button");
            var skill3Button = root.Q<Button>("Skill3Button");
            var skill4Button = root.Q<Button>("Skill4Button");

            if (_endTurnButton != null) _endTurnButton.clicked += OnEndTurnClicked;
            if (_moveButton != null) _moveButton.clicked += OnMoveClicked;

            // Register skill buttons
            _skillButtons.Clear();
            RegisterSkillButton(skill1Button, 0);
            RegisterSkillButton(skill2Button, 1);
            RegisterSkillButton(skill3Button, 2);
            RegisterSkillButton(skill4Button, 3);

            // Find GridController from the currently loaded battle scene
            // BattleController now implements IGridController directly
            _gridController = Object.FindFirstObjectByType<BattleController>();
            if (_gridController == null)
            {
                Debug.LogWarning("[BattleUIController] BattleController (IGridController) not found in scene.");
                return;
            }

            _canEndTurn = true;

            // Subscribe to turn/game events for UI state management
            _gridController.TurnStarted += OnTurnStarted;
            _gridController.GameEnded += OnGameEnded;

            // Subscribe to unit selection events for HP/MP display
            SubscribeToUnitEvents();

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

            // Initialize HP/MP for the current turn's unit (handles case where TurnStarted fired before UI subscription)
            InitializeCurrentTurnHPMP();
        }

        private void UnwireButtons()
        {
            if (_endTurnButton != null) _endTurnButton.clicked -= OnEndTurnClicked;
            if (_moveButton != null) _moveButton.clicked -= OnMoveClicked;

            // Unwire all skill buttons using stored callbacks
            for (int i = 0; i < _skillButtons.Count && i < _skillButtonCallbacks.Count; i++)
            {
                if (_skillButtons[i] != null)
                {
                    _skillButtons[i].clicked -= _skillButtonCallbacks[i];
                }
            }
            _skillButtons.Clear();
            _skillButtonCallbacks.Clear();

            UnsubscribeFromUnitEvents();

            if (_currentSelectedUnit is ICombatant combatant)
            {
                combatant.HealthChanged -= OnUnitHealthChanged;
            }
            _currentSelectedUnit = null;

            if (_endTurnAction != null)
            {
                _endTurnAction.performed -= OnEndTurnPerformed;
                _endTurnAction.Disable();
            }
        }

        private void InitializeCurrentTurnHPMP()
        {
            if (_gridController == null) return;

            var playableUnits = _gridController.TurnContext.PlayableUnits?.Invoke();
            var currentUnit = playableUnits?.FirstOrDefault();
            if (currentUnit != null)
            {
                _currentSelectedUnit = currentUnit;
                UpdateHPMPBars();
                UpdateMoveButtonState(currentUnit);

                if (_currentSelectedUnit is ICombatant combatant)
                {
                    combatant.HealthChanged += OnUnitHealthChanged;
                }
            }

            if (currentUnit == null && _gridController.TurnContext.CurrentPlayer == null)
            {
                Debug.LogWarning("[BattleUIController] No current unit or player, skipping UI initialization.");
                return;
            }

            if (_gridController.TurnContext.CurrentPlayer == null) return;

            bool isHumanTurn = _gridController.TurnContext.CurrentPlayer.PlayerType == PlayerType.HumanPlayer;
            if (_bottomPanel != null)
                _bottomPanel.style.display = isHumanTurn ? DisplayStyle.Flex : DisplayStyle.None;
            _canEndTurn = isHumanTurn;
            if (_endTurnButton != null)
                _endTurnButton.SetEnabled(isHumanTurn);
        }

        private void OnEndTurnClicked()
        {
            if (_gridController != null)
                _gridController.EndTurn();
        }

        private void OnMoveClicked()
        {
            if (_currentSelectedUnit == null || _gridController == null)
                return;

            var playableUnits = _gridController.TurnContext.PlayableUnits?.Invoke()?.ToList();
            if (playableUnits == null || !playableUnits.Any(u => ReferenceEquals(u, _currentSelectedUnit)))
                return;

            var moveAbility = _currentSelectedUnit.GetBaseAbilities()
                .FirstOrDefault(a => IsMoveAbility(a));
            
            if (moveAbility == null)
                return;

            // Initialize ability cache before checking CanPerform
            moveAbility.OnAbilitySelected(_gridController);
            
            if (!moveAbility.CanPerform(_gridController))
                return;

            _currentMoveAbility = moveAbility;

            if (_gridController.GridState is GridStateUnitSelected)
            {
                moveAbility.Display(_gridController);
                return;
            }

            _gridController.GridState = new GridStateUnitSelected(_currentSelectedUnit, moveAbility);
        }

        private void OnSkillButtonClicked(int skillIndex)
        {
            Debug.Log($"[BattleUIController] Skill {skillIndex + 1} button clicked");
        }

        /// <summary>
        /// Creates skill buttons dynamically based on the unit's abilities.
        /// TODO: Implement dynamic skill button creation based on unit.GetNonMoveAbilities().
        /// </summary>
        /// <param name="unit">The unit to create skill buttons for.</param>
        private void CreateSkillButtonsForUnit(IUnit unit)
        {
            // TODO: Dynamic skill button creation
            // 1. Clear existing skill buttons from _skillPanel
            // 2. Iterate over unit.GetNonMoveAbilities()
            // 3. Create a Button for each ability and add to _skillPanel
            // 4. Bind click events to the corresponding ability execution
        }

        private void RegisterSkillButton(Button button, int skillIndex)
        {
            if (button == null) return;
            _skillButtons.Add(button);
            System.Action callback = () => OnSkillButtonClicked(skillIndex);
            _skillButtonCallbacks.Add(callback);
            button.clicked += callback;
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
            bool isHumanTurn = turnTransitionParams.TurnContext.CurrentPlayer.PlayerType == PlayerType.HumanPlayer;
            
            if (_bottomPanel != null)
                _bottomPanel.style.display = isHumanTurn ? DisplayStyle.Flex : DisplayStyle.None;
            
            _canEndTurn = isHumanTurn;
            if (_endTurnButton != null)
                _endTurnButton.SetEnabled(isHumanTurn);

            var playableUnits = turnTransitionParams.TurnContext.PlayableUnits();
            var currentUnit = playableUnits.FirstOrDefault();
            if (currentUnit != null)
            {
                if (_currentSelectedUnit is ICombatant oldCombatant && !ReferenceEquals(oldCombatant, currentUnit))
                {
                    oldCombatant.HealthChanged -= OnUnitHealthChanged;
                }

                _currentSelectedUnit = currentUnit;
                UpdateHPMPBars();
                UpdateMoveButtonState(currentUnit);

                if (_currentSelectedUnit is ICombatant newCombatant)
                {
                    newCombatant.HealthChanged += OnUnitHealthChanged;
                }
            }
        }

        private void OnGameEnded(GameResult gameResult)
        {
            _canEndTurn = false;
            if (_endTurnButton != null)
                _endTurnButton.SetEnabled(false);
            if (_moveButton != null)
                _moveButton.SetEnabled(false);
            
            if (_bottomPanel != null)
                _bottomPanel.style.display = DisplayStyle.None;

            _currentSelectedUnit = null;
            if (_hpBar != null) _hpBar.value = 0;
            if (_mpBar != null) _mpBar.value = 0;
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
            _hpBar.title = $"{(int)value}/{(int)maxValue}";
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
            _mpBar.title = $"{(int)value}/{(int)maxValue}";
        }

        private void SubscribeToUnitEvents()
        {
            if (_gridController?.UnitManager == null) return;

            var units = _gridController.UnitManager.GetUnits();
            foreach (var unit in units)
            {
                unit.UnitSelected += OnUnitSelected;
                unit.UnitDeselected += OnUnitDeselected;
                
                if (unit is IMoveable moveable)
                {
                    moveable.UnitMoved += OnUnitMoved;
                }
            }
        }

        private void UnsubscribeFromUnitEvents()
        {
            if (_gridController?.UnitManager == null) return;

            var units = _gridController.UnitManager.GetUnits();
            foreach (var unit in units)
            {
                unit.UnitSelected -= OnUnitSelected;
                unit.UnitDeselected -= OnUnitDeselected;
                
                if (unit is IMoveable moveable)
                {
                    moveable.UnitMoved -= OnUnitMoved;
                }
            }
        }

        private void OnUnitSelected(IUnit unit)
        {
            if (_currentSelectedUnit != null && _currentSelectedUnit != unit && _currentSelectedUnit is ICombatant oldCombatant)
            {
                oldCombatant.HealthChanged -= OnUnitHealthChanged;
            }

            _currentSelectedUnit = unit;
            UpdateHPMPBars();

            if (_currentSelectedUnit is ICombatant combatant)
            {
                combatant.HealthChanged += OnUnitHealthChanged;
            }
        }

        private void OnUnitDeselected(IUnit unit)
        {
            if (unit is ICombatant combatant)
            {
                combatant.HealthChanged -= OnUnitHealthChanged;
            }
        }

        private void OnUnitHealthChanged(HealthChangedEventArgs args)
        {
            if (ReferenceEquals(args.AffectedUnit, _currentSelectedUnit))
            {
                UpdateHPMPBars();
            }
        }

        private void UpdateHPMPBars()
        {
            if (_currentSelectedUnit == null)
            {
                if (_hpBar != null) _hpBar.value = 0;
                if (_mpBar != null) _mpBar.value = 0;
                return;
            }

            UpdateHpBar(_currentSelectedUnit.Health, _currentSelectedUnit.MaxHealth);
            UpdateMpBar(_currentSelectedUnit.Mana, _currentSelectedUnit.MaxMana);
        }

        private void UpdateMoveButtonState(IUnit unit)
        {
            if (_moveButton == null || unit == null)
            {
                if (_moveButton != null) _moveButton.SetEnabled(false);
                return;
            }

            bool hasUsedMove = unit.HasUsedBasicAbilityThisTurn("Move");
            var abilities = unit.GetBaseAbilities()?.ToList();
            int abilityCount = abilities?.Count ?? 0;
            bool hasMoveAbility = abilities?.Any(IsMoveAbility) ?? false;

            Debug.Log($"[BattleUIController] UpdateMoveButtonState: unit={unit}, " +
                $"hasUsedMove={hasUsedMove}, abilityCount={abilityCount}, " +
                $"hasMoveAbility={hasMoveAbility}, " +
                $"abilityTypes={string.Join(", ", abilities?.Select(a => a.GetType().Name) ?? Enumerable.Empty<string>())}");

            bool canMove = !hasUsedMove && hasMoveAbility;

            _moveButton.SetEnabled(canMove);
            Debug.Log($"[BattleUIController] MoveButton enabled={canMove}");
        }

        private void OnUnitMoved(UnitMovedEventArgs args)
        {
            if (ReferenceEquals(args.AffectedUnit, _currentSelectedUnit))
            {
                UpdateMoveButtonState(args.AffectedUnit);
            }
        }

        private bool IsMoveAbility(IAbility ability)
        {
            return ability is MoveAbilityImpl ||
                   (ability is GenericAbilityImpl && ability.GetType().Name.Contains("Move"));
        }
    }
}
