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
    /// Manages battle UI buttons, HP/MP bars, turn state feedback, skill cards, and turn order.
    /// </summary>
    public sealed class BattleUIController : UIControllerBase
    {
        // Status panel
        private VisualElement _portrait;
        private Label _unitNameLabel;
        private VisualElement _hpBarFill;
        private Label _hpText;
        private VisualElement _mpBarFill;
        private Label _mpText;

        // Top panel
        private Label _roundLabel;
        private VisualElement _turnOrderContainer;

        // Bottom panel
        private Button _endTurnButton;
        private Button _moveButton;
        private VisualElement _skillPanel;
        private VisualElement _bottomPanel;

        // State
        private IGridController _gridController;
        private InputAction _endTurnAction;
        private IUnit _currentSelectedUnit;
        private IAbility _currentMoveAbility;
        private readonly List<VisualElement> _skillCards = new List<VisualElement>();
        private readonly List<System.Action> _skillCallbacks = new List<System.Action>();
        private readonly List<VisualElement> _turnOrderItems = new List<VisualElement>();
        private bool _canEndTurn;

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

            // Query UI elements
            _portrait = root.Q<VisualElement>("Portrait");
            _unitNameLabel = root.Q<Label>("UnitName");
            _hpBarFill = root.Q<VisualElement>("HPBarFill");
            _hpText = root.Q<Label>("HPText");
            _mpBarFill = root.Q<VisualElement>("MPBarFill");
            _mpText = root.Q<Label>("MPText");

            _roundLabel = root.Q<Label>("RoundLabel");
            _turnOrderContainer = root.Q<VisualElement>("TurnOrderContainer");

            _endTurnButton = root.Q<Button>("EndTurnButton");
            _moveButton = root.Q<Button>("MoveButton");
            _skillPanel = root.Q<VisualElement>("SkillPanel");
            _bottomPanel = root.Q<VisualElement>("BottomPanel");

            if (_endTurnButton != null) _endTurnButton.clicked += OnEndTurnClicked;
            if (_moveButton != null) _moveButton.clicked += OnMoveClicked;

            // Find GridController from the currently loaded battle scene
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

            // Initialize UI for the current turn's unit
            InitializeCurrentTurnUI();
        }

        private void UnwireButtons()
        {
            if (_endTurnButton != null) _endTurnButton.clicked -= OnEndTurnClicked;
            if (_moveButton != null) _moveButton.clicked -= OnMoveClicked;

            ClearSkillCards();
            ClearTurnOrder();

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

        private void InitializeCurrentTurnUI()
        {
            if (_gridController == null) return;

            UpdateRoundLabel();
            UpdateTurnOrder();

            var playableUnits = _gridController.TurnContext.PlayableUnits?.Invoke();
            var currentUnit = playableUnits?.FirstOrDefault();
            if (currentUnit != null)
            {
                _currentSelectedUnit = currentUnit;
                UpdateStatusPanel();
                UpdateMoveButtonState(currentUnit);
                UpdateSkillCards(currentUnit);

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

        #region Round & Turn Order

        private void UpdateRoundLabel()
        {
            if (_roundLabel == null) return;
            _roundLabel.text = $"Round {_gridController.CurrentRound}";
        }

        private void UpdateTurnOrder()
        {
            if (_turnOrderContainer == null) return;

            ClearTurnOrder();

            var units = _gridController.UnitManager?.GetUnits()?.ToList();
            if (units == null || units.Count == 0) return;

            var orderedUnits = units.OrderByDescending(u => u.Speed).ToList();
            var currentUnit = _gridController.TurnContext.PlayableUnits?.Invoke()?.FirstOrDefault();

            foreach (var unit in orderedUnits)
            {
                var item = new VisualElement();
                item.AddToClassList("turn-order-item");
                if (ReferenceEquals(unit, currentUnit))
                {
                    item.AddToClassList("active");
                }

                if (unit.Portrait != null)
                {
                    item.style.backgroundImage = new StyleBackground(unit.Portrait);
                }
                else
                {
                    item.style.backgroundColor = new StyleColor(GetPlayerColor(unit.PlayerNumber));
                }

                _turnOrderContainer.Add(item);
                _turnOrderItems.Add(item);
            }
        }

        private void ClearTurnOrder()
        {
            foreach (var item in _turnOrderItems)
            {
                if (item.parent != null) item.RemoveFromHierarchy();
            }
            _turnOrderItems.Clear();
        }

        private Color GetPlayerColor(int playerNumber)
        {
            switch (playerNumber)
            {
                case 0: return new Color(0.4f, 0.6f, 0.9f);
                case 1: return new Color(0.9f, 0.4f, 0.4f);
                case 2: return new Color(0.4f, 0.9f, 0.5f);
                case 3: return new Color(0.9f, 0.8f, 0.3f);
                default: return new Color(0.6f, 0.6f, 0.6f);
            }
        }

        #endregion

        #region Status Panel

        private void UpdateStatusPanel()
        {
            if (_currentSelectedUnit == null) return;

            // Portrait
            if (_portrait != null)
            {
                if (_currentSelectedUnit.Portrait != null)
                {
                    _portrait.style.backgroundImage = new StyleBackground(_currentSelectedUnit.Portrait);
                }
                else
                {
                    _portrait.style.backgroundImage = null;
                    _portrait.style.backgroundColor = new StyleColor(GetPlayerColor(_currentSelectedUnit.PlayerNumber));
                }
            }

            // Name
            if (_unitNameLabel != null)
            {
                _unitNameLabel.text = (_currentSelectedUnit as INamedUnit)?.UnitName ?? "Unknown";
            }

            // HP & MP
            UpdateHpBar(_currentSelectedUnit.Health, _currentSelectedUnit.MaxHealth);
            UpdateMpBar(_currentSelectedUnit.Mana, _currentSelectedUnit.MaxMana);
        }

        public void UpdateHpBar(float value, float maxValue)
        {
            if (_hpBarFill != null)
            {
                float pct = maxValue > 0 ? value / maxValue : 0f;
                _hpBarFill.style.width = new StyleLength(new Length(pct * 100f, LengthUnit.Percent));
            }
            if (_hpText != null)
            {
                _hpText.text = $"{(int)value}/{(int)maxValue}";
            }
        }

        public void UpdateMpBar(float value, float maxValue)
        {
            if (_mpBarFill != null)
            {
                float pct = maxValue > 0 ? value / maxValue : 0f;
                _mpBarFill.style.width = new StyleLength(new Length(pct * 100f, LengthUnit.Percent));
            }
            if (_mpText != null)
            {
                _mpText.text = $"{(int)value}/{(int)maxValue}";
            }
        }

        #endregion

        #region Skill Cards

        private void UpdateSkillCards(IUnit unit)
        {
            ClearSkillCards();
            if (_skillPanel == null || unit == null) return;

            var abilities = unit.GetBaseAbilities()?.Where(a => !IsMoveAbility(a)).ToList();
            if (abilities == null) return;

            for (int i = 0; i < abilities.Count; i++)
            {
                var ability = abilities[i];
                var card = CreateSkillCard(ability, i);
                _skillPanel.Add(card);
                _skillCards.Add(card);
            }
        }

        private VisualElement CreateSkillCard(IAbility ability, int index)
        {
            var card = new VisualElement();
            card.AddToClassList("skill-card");

            bool canPerform = _gridController != null && ability.CanPerform(_gridController);
            if (!canPerform)
            {
                card.AddToClassList("disabled");
            }

            // Icon
            var icon = new VisualElement();
            icon.AddToClassList("skill-card-icon");
            if (ability.Icon != null)
            {
                icon.style.backgroundImage = new StyleBackground(ability.Icon);
            }
            else
            {
                icon.style.backgroundColor = new StyleColor(GetAbilityColor(index));
            }
            card.Add(icon);

            // Name
            var nameLabel = new Label(ability.DisplayName);
            nameLabel.AddToClassList("skill-card-name");
            card.Add(nameLabel);

            // Cost
            var costLabel = new Label(GetCostText(ability));
            costLabel.AddToClassList("skill-card-cost");
            card.Add(costLabel);

            // Click event
            if (canPerform)
            {
                int capturedIndex = index;
                System.Action callback = () => OnSkillButtonClicked(capturedIndex);
                _skillCallbacks.Add(callback);
                card.RegisterCallback<ClickEvent>(evt => callback());
            }

            return card;
        }

        private string GetCostText(IAbility ability)
        {
            if (ability.Cost > 0)
                return $"MP {ability.Cost}";
            return "";
        }

        private Color GetAbilityColor(int index)
        {
            Color[] colors = new[]
            {
                new Color(0.8f, 0.5f, 0.3f),
                new Color(0.3f, 0.7f, 0.4f),
                new Color(0.3f, 0.5f, 0.8f),
                new Color(0.7f, 0.3f, 0.7f),
                new Color(0.3f, 0.7f, 0.7f),
                new Color(0.8f, 0.7f, 0.3f),
            };
            return colors[index % colors.Length];
        }

        private void ClearSkillCards()
        {
            _skillCallbacks.Clear();
            foreach (var card in _skillCards)
            {
                if (card.parent != null) card.RemoveFromHierarchy();
            }
            _skillCards.Clear();
        }

        #endregion

        #region Button Handlers

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
            if (_currentSelectedUnit == null || _gridController == null)
            {
                Debug.LogWarning($"[BattleUIController] Cannot use skill: currentSelectedUnit={_currentSelectedUnit != null}, gridController={_gridController != null}");
                return;
            }

            var abilities = _currentSelectedUnit.GetBaseAbilities()?.Where(a => !IsMoveAbility(a)).ToList();
            if (abilities == null || skillIndex >= abilities.Count)
            {
                Debug.LogWarning($"[BattleUIController] Skill index {skillIndex} out of range. Abilities count: {abilities?.Count ?? 0}");
                return;
            }

            var ability = abilities[skillIndex];
            Debug.Log($"[BattleUIController] Skill {skillIndex + 1} clicked: {ability.DisplayName}");

            // Switch to unit selected state - OnStateEnter will handle OnAbilitySelected, CanPerform check, and Display
            _gridController.GridState = new GridStateUnitSelected(_currentSelectedUnit, ability);
        }

        #endregion

        #region Input & Turn Events

        private void OnEndTurnPerformed(InputAction.CallbackContext context)
        {
            if (_canEndTurn && _gridController != null)
            {
                _gridController.EndTurn();
            }
        }

        private void OnTurnStarted(TurnTransitionParams turnTransitionParams)
        {
            bool isHumanTurn = turnTransitionParams.TurnContext.CurrentPlayer.PlayerType == PlayerType.HumanPlayer;

            if (_bottomPanel != null)
                _bottomPanel.style.display = isHumanTurn ? DisplayStyle.Flex : DisplayStyle.None;

            _canEndTurn = isHumanTurn;
            if (_endTurnButton != null)
                _endTurnButton.SetEnabled(isHumanTurn);

            UpdateRoundLabel();
            UpdateTurnOrder();

            var playableUnits = turnTransitionParams.TurnContext.PlayableUnits();
            var currentUnit = playableUnits.FirstOrDefault();
            if (currentUnit != null)
            {
                if (_currentSelectedUnit is ICombatant oldCombatant && !ReferenceEquals(oldCombatant, currentUnit))
                {
                    oldCombatant.HealthChanged -= OnUnitHealthChanged;
                }

                _currentSelectedUnit = currentUnit;
                UpdateStatusPanel();
                UpdateMoveButtonState(currentUnit);
                UpdateSkillCards(currentUnit);

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
            UpdateHpBar(0, 1);
            UpdateMpBar(0, 1);
            ClearSkillCards();
        }

        #endregion

        #region Unit Events

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
            UpdateStatusPanel();

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
                UpdateStatusPanel();
            }
        }

        private void OnUnitMoved(UnitMovedEventArgs args)
        {
            if (ReferenceEquals(args.AffectedUnit, _currentSelectedUnit))
            {
                UpdateMoveButtonState(args.AffectedUnit);
            }
        }

        #endregion

        #region Helpers

        private void UpdateMoveButtonState(IUnit unit)
        {
            if (_moveButton == null || unit == null)
            {
                if (_moveButton != null) _moveButton.SetEnabled(false);
                return;
            }

            bool hasUsedMove = unit.HasUsedBasicAbilityThisTurn("Move");
            var abilities = unit.GetBaseAbilities()?.ToList();
            bool hasMoveAbility = abilities?.Any(IsMoveAbility) ?? false;

            bool canMove = !hasUsedMove && hasMoveAbility;

            _moveButton.SetEnabled(canMove);
        }

        private bool IsMoveAbility(IAbility ability)
        {
            return ability.DisplayName == "Move";
        }

        #endregion
    }
}
