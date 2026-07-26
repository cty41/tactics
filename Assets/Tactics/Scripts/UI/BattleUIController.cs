using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.AssetPipeline;
using Tactics.Runtime.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Tactics.Common.Controllers;
using Tactics.Common.Controllers.GameResolvers;
using Tactics.Common.Controllers.GridStates;
using Tactics.Common.Players;
using Tactics.Common.Battle;
using Tactics.Common.Interactables;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Common.Utilities;
using Tactics.Consumables;

using Tactics.Common.Units.Buffs;

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
        private VisualElement _root;
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
        private Button _consumableButton;
        private VisualElement _consumableIcon;
        private Label _consumableNameLabel;
        private Label _consumableChargesLabel;
        private VisualElement _skillPanel;
        private VisualElement _bottomPanel;
        private Label _abilityReasonTooltip;
        private VisualElement _orderedSelectionPanel;
        private Label _orderedSelectionPrompt;
        private VisualElement _orderedMarkerRoot;
        private VisualElement _spearMarkerRoot;

        // State
        private IGridController _gridController;
        private InputAction _endTurnAction;
        private InputAction _cancelTargetingAction;
        private IUnit _currentSelectedUnit;
        private IAbility _currentMoveAbility;
        private ConsumableBattleAbility _currentConsumableAbility;
        private bool _isConsumableTargeting;
        private SkillGraphAbilityImpl _currentOrderedAbility;
        private readonly List<VisualElement> _skillCards = new List<VisualElement>();
        private readonly Dictionary<VisualElement, IAbility> _skillCardAbilities = new();
        private readonly List<System.Action> _skillCallbacks = new List<System.Action>();
        private readonly List<VisualElement> _turnOrderItems = new List<VisualElement>();
        private bool _canEndTurn;
        private readonly Dictionary<DroppedSpear, Label> _spearMarkers = new();

        // Damage Numbers
        [SerializeField] private int poolSize = 20;
        private const string DamageNumberSettingsPath = "Assets/Tactics/ScriptableObjects/DamageNumberSettings.asset";

        // Hover Health Bar
        private VisualElement _hoverHealthBar;
        private VisualElement _hoverHPBarFill;
        private Label _hoverHPText;
        private IUnit _hoveredUnit;
        private Camera _mainCamera;

        private DamageNumberSettings _damageSettings;
        private VisualElement _damageNumberContainer;
        private Queue<Label> _damageNumberPool;
        private List<DamageNumberInstance> _activeDamageNumbers = new();

        // Buff Icons
        private VisualElement _buffIconRoot;
        private readonly Dictionary<IUnit, UnitBuffIcons> _unitBuffIcons = new();

        private class UnitBuffIcons
        {
            public VisualElement Container;
            public readonly Dictionary<Buff, VisualElement> Icons = new();
        }

        private struct DamageNumberInstance
        {
            public Label Label;
            public Vector3 WorldStartPosition;
            public float SpawnTime;
            public float Lifetime;
            public float MoveSpeed;
            public float StartScale;
            public float PeakScale;
            public float EndScale;
            public float FadeInDuration;
            public float FadeOutDuration;
        }

        protected override void OnShown()
        {
            EnableCancelTargetingInput();
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
            DisableCancelTargetingInput();
            UnwireButtons();
            if (_gridController != null)
            {
                _gridController.TurnStarted -= OnTurnStarted;
                _gridController.GameEnded -= OnGameEnded;
            }

            _unitBuffIcons.Clear();
            _buffIconRoot?.Clear();
        }

        private void WireButtons()
        {
            var root = Ui.GetRootElement(UIManager.UIId.Battle);
            if (root == null)
            {
                TLog.Warning("[BattleUIController] Could not get root visual element for Battle UI.");
                return;
            }
            _root = root;

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
            _consumableButton = root.Q<Button>("BattleConsumableButton");
            _consumableIcon = root.Q<VisualElement>("BattleConsumableIcon");
            _consumableNameLabel = root.Q<Label>("BattleConsumableName");
            _consumableChargesLabel = root.Q<Label>("BattleConsumableCharges");
            _skillPanel = root.Q<VisualElement>("SkillPanel");
            _bottomPanel = root.Q<VisualElement>("BottomPanel");
            _abilityReasonTooltip = root.Q<Label>("AbilityReasonTooltip");
            _orderedSelectionPanel = root.Q<VisualElement>("OrderedSelectionPanel");
            _orderedSelectionPrompt = root.Q<Label>("OrderedSelectionPrompt");
            _orderedMarkerRoot = root.Q<VisualElement>("OrderedTargetMarkerRoot");
            _spearMarkerRoot = root.Q<VisualElement>("DroppedSpearMarkerRoot");
            _orderedSelectionPanel?.RegisterCallback<MouseDownEvent>(OnOrderedSelectionMouseDown);
            _root.RegisterCallback<KeyDownEvent>(OnBattleKeyDown);

            _hoverHealthBar = root.Q<VisualElement>("HoverHealthBar");
            _hoverHPBarFill = root.Q<VisualElement>("HoverHPBarFill");
            _hoverHPText = root.Q<Label>("HoverHPText");
            _mainCamera = Camera.main;

            // Initialize damage numbers
            _damageNumberContainer = root.Q<VisualElement>("DamageNumberContainer");
            if (_damageNumberContainer != null)
            {
                LoadDamageNumberSettings();
                _damageNumberPool = new Queue<Label>();
                for (int i = 0; i < poolSize; i++)
                {
                    _damageNumberPool.Enqueue(CreatePooledLabel());
                }
            }

            // Initialize buff icon root
            _buffIconRoot = new VisualElement();
            _buffIconRoot.style.position = Position.Absolute;
            _buffIconRoot.style.left = 0;
            _buffIconRoot.style.top = 0;
            _buffIconRoot.style.width = Length.Percent(100);
            _buffIconRoot.style.height = Length.Percent(100);
            _buffIconRoot.pickingMode = PickingMode.Ignore;
            root.Add(_buffIconRoot);

            if (_endTurnButton != null) _endTurnButton.clicked += OnEndTurnClicked;
            if (_moveButton != null) _moveButton.clicked += OnMoveClicked;
            if (_consumableButton != null) _consumableButton.clicked += OnConsumableClicked;

            // Find GridController from the currently loaded battle scene
            _gridController = Object.FindFirstObjectByType<BattleController>();
            if (_gridController == null)
            {
                TLog.Warning("[BattleUIController] BattleController (IGridController) not found in scene.");
                return;
            }

            _canEndTurn = true;

            // Subscribe to turn/game events for UI state management
            _gridController.TurnStarted += OnTurnStarted;
            _gridController.GameEnded += OnGameEnded;

            // Subscribe to unit selection events for HP/MP display
            SubscribeToUnitEvents();

            // The visual state must not depend on an optional InputActionAsset. Some scenes
            // drive the battle through UI Toolkit callbacks only, but still need the current
            // unit, skill cards, and status panel initialized.
            InitializeCurrentTurnUI();

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
                TLog.Warning("[BattleUIController] No InputActionAsset found (neither InputSystemUIInputModule nor PlayerInput).");
                return;
            }

            var playerMap = inputActions.FindActionMap("Player");
            if (playerMap != null)
            {
                playerMap.Enable();

                _endTurnAction = playerMap.FindAction("EndTurn");
                if (_endTurnAction != null)
                {
                    _endTurnAction.performed += OnEndTurnPerformed;
                    _endTurnAction.Enable();
                }
                else
                {
                    TLog.Warning("[BattleUIController] EndTurn action not found in Player action map.");
                }
            }
            else
            {
                TLog.Warning("[BattleUIController] Player action map not found.");
            }

        }

        private void UnwireButtons()
        {
            if (_endTurnButton != null) _endTurnButton.clicked -= OnEndTurnClicked;
            if (_moveButton != null) _moveButton.clicked -= OnMoveClicked;
            if (_consumableButton != null) _consumableButton.clicked -= OnConsumableClicked;
            _orderedSelectionPanel?.UnregisterCallback<MouseDownEvent>(OnOrderedSelectionMouseDown);
            _root?.UnregisterCallback<KeyDownEvent>(OnBattleKeyDown);

            SetCurrentConsumableAbility(null);
            _isConsumableTargeting = false;

            ClearSkillCards();
            ClearTurnOrder();

            UnsubscribeFromUnitEvents();

            if (_currentSelectedUnit is ICombatant combatant)
            {
                combatant.HealthChanged -= OnUnitHealthChanged;
            }
            if (_currentSelectedUnit != null)
            {
                _currentSelectedUnit.ManaChanged -= OnUnitManaChanged;
                _currentSelectedUnit.BasicAbilityUsed -= OnBasicAbilityUsed;
            }
            _currentSelectedUnit = null;
            _currentOrderedAbility = null;
            _orderedMarkerRoot?.Clear();
            _spearMarkers.Clear();

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
                UpdateConsumableButton(currentUnit);
                UpdateSkillCards(currentUnit);

                if (_currentSelectedUnit is ICombatant combatant)
                {
                    combatant.HealthChanged += OnUnitHealthChanged;
                }
                _currentSelectedUnit.ManaChanged += OnUnitManaChanged;
                _currentSelectedUnit.BasicAbilityUsed += OnBasicAbilityUsed;
            }

            if (currentUnit == null && _gridController.TurnContext.CurrentPlayer == null)
            {
                TLog.Warning("[BattleUIController] No current unit or player, skipping UI initialization.");
                return;
            }

            if (_gridController.TurnContext.CurrentPlayer == null) return;

            bool isHumanTurn = _gridController.TurnContext.CurrentPlayer.PlayerType == PlayerType.HumanPlayer;
            if (_bottomPanel != null)
                _bottomPanel.style.display = isHumanTurn ? DisplayStyle.Flex : DisplayStyle.None;
            _canEndTurn = isHumanTurn;
            if (_endTurnButton != null)
                _endTurnButton.SetEnabled(isHumanTurn);

            // 回合开始时重置为 AwaitInput 状态，不自动进入任何技能状态
            if (isHumanTurn)
            {
                _gridController.GridState = new GridStateAwaitInput();
            }
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
                case 0: return new Color(0.9f, 0.4f, 0.4f);
                case 1: return new Color(0.4f, 0.6f, 0.9f);
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
                if (_currentSelectedUnit is Unit unit && unit != null)
                    _unitNameLabel.text = unit.UnitName;
                else
                    _unitNameLabel.text = "Unknown";
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

            var abilities = GetSkillAbilities(unit);
            if (abilities == null) return;

            for (int i = 0; i < abilities.Count; i++)
            {
                var ability = abilities[i];
                var card = CreateSkillCard(unit, ability, i);
                if (card == null)
                    continue;
                _skillPanel.Add(card);
                _skillCards.Add(card);
                _skillCardAbilities[card] = ability;
            }
        }

        private VisualElement CreateSkillCard(IUnit owner, IAbility ability, int index)
        {
            var card = new VisualElement();
            card.name = $"AbilityCard_{ToElementKey(ability.DisplayName)}";
            card.AddToClassList("skill-card");

            AbilityAvailability availability = ability is IAbilityAvailabilityProvider provider
                ? provider.GetAvailability(_gridController)
                : ability.CanPerform(_gridController)
                    ? AbilityAvailability.Enabled()
                    : AbilityAvailability.Disabled("当前无法使用");
            card.userData = availability;
            if (availability.State == AbilityAvailabilityState.Hidden)
            {
                card.AddToClassList("ability-hidden");
                return null;
            }
            if (!availability.CanExecute)
            {
                card.AddToClassList("disabled");
                card.AddToClassList("ability-disabled-clickable");
                card.tooltip = availability.Reason;
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
            System.Action callback = () =>
            {
                if (!ReferenceEquals(_currentSelectedUnit, owner) ||
                    !GetSkillAbilities(owner).Contains(ability))
                {
                    TLog.Warning($"[BattleUIController] Ignored stale skill card '{ability.DisplayName}' for '{owner?.UnitID}'.");
                    RefreshActionUi();
                    return;
                }

                var currentAvailability = AbilityAvailabilityResolver.Resolve(ability, _gridController);
                if (currentAvailability.CanExecute)
                {
                    OnSkillButtonClicked(owner, ability);
                    return;
                }
                ShowAbilityReason(currentAvailability.Reason);
            };
            _skillCallbacks.Add(callback);
            card.RegisterCallback<ClickEvent>(evt =>
            {
                callback();
                evt.StopPropagation();
            });

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
            _skillCardAbilities.Clear();
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
            _isConsumableTargeting = false;
            if (_gridController != null)
                _gridController.EndTurn();
        }

        private void OnMoveClicked()
        {
            _isConsumableTargeting = false;
            _consumableButton?.EnableInClassList("targeting", false);
            if (_currentSelectedUnit == null || _gridController == null)
                return;

            var playableUnits = _gridController.TurnContext.PlayableUnits?.Invoke()?.ToList();
            if (playableUnits == null || !playableUnits.Any(u => ReferenceEquals(u, _currentSelectedUnit)))
                return;

            var moveAbility = _currentSelectedUnit.GetBaseAbilities()
                .FirstOrDefault(a => IsMoveAbility(a));

            if (moveAbility == null)
                return;

            _currentMoveAbility = moveAbility;

            _gridController.GridState = new GridStateUnitSelected(_currentSelectedUnit, moveAbility);
        }

        private void OnSkillButtonClicked(IUnit owner, IAbility ability)
        {
            _isConsumableTargeting = false;
            _consumableButton?.EnableInClassList("targeting", false);
            if (_currentSelectedUnit == null || _gridController == null)
            {
                TLog.Warning($"[BattleUIController] Cannot use skill: currentSelectedUnit={_currentSelectedUnit != null}, gridController={_gridController != null}");
                return;
            }

            if (!ReferenceEquals(_currentSelectedUnit, owner) ||
                ability == null ||
                !GetSkillAbilities(owner).Contains(ability))
            {
                TLog.Warning($"[BattleUIController] Rejected unbound skill click: ownerMatches={ReferenceEquals(_currentSelectedUnit, owner)}, ability={ability?.DisplayName ?? "null"}.");
                RefreshActionUi();
                return;
            }

            _currentOrderedAbility = ability as SkillGraphAbilityImpl;
            HideAbilityReason();
            TLog.Info($"[BattleUIController] Skill clicked: {ability.DisplayName}");

            if (ability is SkillGraphAbilityImpl graphAbility &&
                graphAbility.TargetMode == SkillTargetMode.RecoveryAction)
            {
                _ = graphAbility.ExecuteRecoveryActionAsync(_gridController);
                return;
            }

            // Switch to unit selected state - OnStateEnter will handle OnAbilitySelected, CanPerform check, and Display
            _gridController.GridState = new GridStateUnitSelected(_currentSelectedUnit, ability);
        }

        private void OnConsumableClicked()
        {
            _currentOrderedAbility = null;
            if (_currentSelectedUnit == null || _gridController == null ||
                _currentConsumableAbility == null ||
                !_currentConsumableAbility.CanPerform(_gridController))
            {
                return;
            }

            _isConsumableTargeting = true;
            _consumableButton?.EnableInClassList("targeting", true);
            _gridController.GridState = new GridStateUnitSelected(
                _currentSelectedUnit,
                _currentConsumableAbility);
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
            _isConsumableTargeting = false;
            _consumableButton?.EnableInClassList("targeting", false);
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
                if (_currentSelectedUnit != null && !ReferenceEquals(_currentSelectedUnit, currentUnit))
                {
                    _currentSelectedUnit.ManaChanged -= OnUnitManaChanged;
                    _currentSelectedUnit.BasicAbilityUsed -= OnBasicAbilityUsed;
                }

                _currentSelectedUnit = currentUnit;
                UpdateStatusPanel();
                UpdateMoveButtonState(currentUnit);
                UpdateConsumableButton(currentUnit);
                UpdateSkillCards(currentUnit);

                if (_currentSelectedUnit is ICombatant newCombatant)
                {
                    newCombatant.HealthChanged += OnUnitHealthChanged;
                }
                _currentSelectedUnit.ManaChanged += OnUnitManaChanged;
                _currentSelectedUnit.BasicAbilityUsed += OnBasicAbilityUsed;
            }
        }

        private void OnGameEnded(GameResult gameResult)
        {
            _canEndTurn = false;
            if (_endTurnButton != null)
                _endTurnButton.SetEnabled(false);
            if (_moveButton != null)
                _moveButton.SetEnabled(false);
            if (_consumableButton != null)
                _consumableButton.SetEnabled(false);

            if (_bottomPanel != null)
                _bottomPanel.style.display = DisplayStyle.None;

            _currentSelectedUnit = null;
            SetCurrentConsumableAbility(null);
            _isConsumableTargeting = false;
            UpdateHpBar(0, 1);
            UpdateMpBar(0, 1);
            ClearSkillCards();
        }

        /// <summary>
        /// Shows the fixed post-victory recovery beat while keeping all battle controls unavailable.
        /// </summary>
        public async Task ShowPostBattleRecoveryAsync(IEnumerable<IUnit> units)
        {
            if (units == null)
                return;

            if (_root != null)
            {
                _root.pickingMode = PickingMode.Ignore;
                HideBattleChromeForRecovery();
            }

            foreach (var unit in units.Where(unit => unit != null && unit.PlayerNumber == 0 && !unit.IsDowned && unit.Health > 0f))
            {
                float hpBefore = unit.Health;
                float mpBefore = unit.Mana;
                unit.Health = Mathf.Min(unit.MaxHealth, unit.Health + unit.Constitution * 2f);
                unit.Mana = Mathf.Min(unit.MaxMana, unit.Mana + unit.Charisma);

                var position = unit.WorldPosition.ToVector3() + Vector3.up * 1.5f;
                int hpGain = Mathf.RoundToInt(unit.Health - hpBefore);
                int mpGain = Mathf.RoundToInt(unit.Mana - mpBefore);
                if (hpGain > 0)
                    SpawnDamageNumber(DamageNumberType.Heal, $"+{hpGain} HP", position, new Color(0.35f, 1f, 0.45f));
                if (mpGain > 0)
                    SpawnDamageNumber(DamageNumberType.Heal, $"+{mpGain} MP", position + Vector3.right * 0.3f, new Color(0.35f, 0.65f, 1f));
            }

            await Task.Delay(800);
        }

        private void HideBattleChromeForRecovery()
        {
            // DamageNumberContainer deliberately stays visible: it is the only feedback during the
            // recovery beat. The battle ends immediately afterwards, so the hidden chrome is rebuilt
            // for the next encounter rather than being restored to an interactable state.
            foreach (string elementName in new[]
                     {
                         "TopPanel", "StatusPanel", "BottomPanel", "AbilityReasonTooltip",
                         "OrderedSelectionPanel", "HoverHealthBar", "DroppedSpearMarkerRoot",
                         "OrderedTargetMarkerRoot"
                     })
            {
                var element = _root.Q<VisualElement>(elementName);
                if (element != null)
                    element.style.display = DisplayStyle.None;
            }
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

                if (unit is Unit concreteUnit)
                {
                    concreteUnit.BuffChanged += args => OnBuffChanged(concreteUnit, args);
                    concreteUnit.UnitDestroyed += _ => OnUnitDestroyed(concreteUnit);
                    concreteUnit.TurnEndManaRestored += OnTurnEndManaRestored;

                    // Sync existing buffs
                    foreach (var buff in concreteUnit.GetActiveBuffs())
                    {
                        AddBuffIcon(concreteUnit, buff);
                    }
                }

                if (unit is ICombatant combatant)
                {
                    combatant.HealthChanged += OnAnyUnitHealthChanged;
                }

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

                if (unit is Unit concreteUnit)
                    concreteUnit.TurnEndManaRestored -= OnTurnEndManaRestored;

                if (unit is ICombatant combatant)
                {
                    combatant.HealthChanged -= OnAnyUnitHealthChanged;
                }

                if (unit is IMoveable moveable)
                {
                    moveable.UnitMoved -= OnUnitMoved;
                }
            }

            _unitBuffIcons.Clear();
            _buffIconRoot?.Clear();
        }

        private void OnUnitSelected(IUnit unit)
        {
            if (_currentSelectedUnit is ICombatant oldCombatant)
            {
                oldCombatant.HealthChanged -= OnUnitHealthChanged;
            }
            if (_currentSelectedUnit != null)
            {
                _currentSelectedUnit.ManaChanged -= OnUnitManaChanged;
                _currentSelectedUnit.BasicAbilityUsed -= OnBasicAbilityUsed;
            }

            _currentSelectedUnit = unit;
            UpdateStatusPanel();
            UpdateMoveButtonState(unit);
            UpdateConsumableButton(unit);
            UpdateSkillCards(unit);

            if (_currentSelectedUnit is ICombatant combatant)
            {
                combatant.HealthChanged += OnUnitHealthChanged;
            }
            _currentSelectedUnit.ManaChanged += OnUnitManaChanged;
            _currentSelectedUnit.BasicAbilityUsed += OnBasicAbilityUsed;
        }

        private void OnUnitDeselected(IUnit unit)
        {
            if (unit is ICombatant combatant)
            {
                combatant.HealthChanged -= OnUnitHealthChanged;
            }
            unit.ManaChanged -= OnUnitManaChanged;
            unit.BasicAbilityUsed -= OnBasicAbilityUsed;
        }

        private void OnUnitMoved(UnitMovedEventArgs args)
        {
            if (ReferenceEquals(args.AffectedUnit, _currentSelectedUnit))
            {
                UpdateMoveButtonState(args.AffectedUnit);
            }
        }

        #endregion

        #region Hover Health Bar

        private void UpdateHoverHealthBar()
        {
            if (_mainCamera == null || _hoverHealthBar == null) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = _mainCamera.ScreenPointToRay(mousePos);
            var hits = Physics2D.GetRayIntersectionAll(ray);
            IUnit hitUnit = null;
            foreach (var hit in hits)
            {
                var unit = hit.collider.GetComponent<Unit>();
                if (unit != null) { hitUnit = unit; break; }
            }

            if (hitUnit != _hoveredUnit)
            {
                _hoveredUnit = hitUnit;
                if (_hoveredUnit == null)
                {
                    _hoverHealthBar.style.display = DisplayStyle.None;
                    return;
                }
            }

            if (_hoveredUnit == null) return;

            float health = _hoveredUnit.Health;
            float maxHealth = _hoveredUnit.MaxHealth;
            float pct = maxHealth > 0 ? health / maxHealth : 0f;
            if (_hoverHPText != null)
                _hoverHPText.text = $"{(int)health}/{(int)maxHealth}";
            if (_hoverHPBarFill != null)
                _hoverHPBarFill.style.width = new StyleLength(new Length(pct * 100f, LengthUnit.Percent));

            Vector3 worldPos = new Vector3(
                _hoveredUnit.WorldPosition.x,
                _hoveredUnit.WorldPosition.y,
                _hoveredUnit.WorldPosition.z);
            Vector3 displayPos = worldPos + Vector3.up * 1.2f;

            Vector3 screenPos = _mainCamera.WorldToScreenPoint(displayPos);
            if (screenPos.z < 0) { _hoverHealthBar.style.display = DisplayStyle.None; return; }

            float uiX = screenPos.x;
            float uiY = Screen.height - screenPos.y;

            _hoverHealthBar.style.left = uiX - 50;
            _hoverHealthBar.style.top = uiY;
            _hoverHealthBar.style.display = DisplayStyle.Flex;
        }

        #endregion

        #region Buff Icons

        private const float BuffIconSize = 28f;
        private const float BuffIconFontSize = 14f;
        private const float BuffIconYOffset = 1.8f;

        public void OnBuffChanged(IUnit unit, BuffChangedEventArgs args)
        {
            if (unit == null || args?.Buff == null) return;

            switch (args.ChangeType)
            {
                case BuffChangeType.Added:
                    AddBuffIcon(unit, args.Buff);
                    break;
                case BuffChangeType.Removed:
                    RemoveBuffIcon(unit, args.Buff);
                    break;
                case BuffChangeType.Refreshed:
                case BuffChangeType.TurnChanged:
                    UpdateBuffTurnCounters(unit);
                    break;
            }
        }

        public void OnUnitDestroyed(IUnit unit)
        {
            if (unit == null) return;

            if (_unitBuffIcons.TryGetValue(unit, out var unitIcons))
            {
                _buffIconRoot.Remove(unitIcons.Container);
                _unitBuffIcons.Remove(unit);
            }
        }

        private void AddBuffIcon(IUnit unit, Buff buff)
        {
            if (!_unitBuffIcons.TryGetValue(unit, out var unitIcons))
            {
                var container = new VisualElement();
                container.style.flexDirection = FlexDirection.Row;
                container.style.alignItems = Align.Center;
                container.style.position = Position.Absolute;
                container.pickingMode = PickingMode.Ignore;
                _buffIconRoot.Add(container);

                unitIcons = new UnitBuffIcons { Container = container };
                _unitBuffIcons[unit] = unitIcons;
            }

            var iconWrapper = new VisualElement();
            iconWrapper.name = $"buff-icon-{buff.BuffName}";
            iconWrapper.style.position = Position.Relative;
            iconWrapper.style.width = BuffIconSize;
            iconWrapper.style.height = BuffIconSize;
            iconWrapper.style.marginLeft = 1;
            iconWrapper.style.marginRight = 1;

            var iconImage = new VisualElement();
            iconImage.style.width = Length.Percent(100);
            iconImage.style.height = Length.Percent(100);
            iconImage.style.backgroundImage = new StyleBackground(buff.Config.Icon);
            iconImage.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            iconWrapper.Add(iconImage);

            if (buff.RemainingTurns > 0)
            {
                var turnLabel = new Label(buff.RemainingTurns.ToString());
                turnLabel.style.position = Position.Absolute;
                turnLabel.style.right = 0;
                turnLabel.style.bottom = 0;
                turnLabel.style.fontSize = BuffIconFontSize;
                turnLabel.style.color = Color.white;
                turnLabel.style.unityTextOutlineColor = Color.black;
                turnLabel.style.unityTextOutlineWidth = 1;
                turnLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                turnLabel.name = "turn-count";
                iconWrapper.Add(turnLabel);
            }

            unitIcons.Container.Add(iconWrapper);
            unitIcons.Icons[buff] = iconWrapper;
            unitIcons.Container.style.display = DisplayStyle.Flex;
        }

        private void RemoveBuffIcon(IUnit unit, Buff buff)
        {
            if (!_unitBuffIcons.TryGetValue(unit, out var unitIcons)) return;

            if (unitIcons.Icons.TryGetValue(buff, out var iconWrapper))
            {
                unitIcons.Container.Remove(iconWrapper);
                unitIcons.Icons.Remove(buff);
            }

            if (unitIcons.Icons.Count == 0)
            {
                _buffIconRoot.Remove(unitIcons.Container);
                _unitBuffIcons.Remove(unit);
            }
        }

        private void UpdateBuffIconPositions()
        {
            if (_unitBuffIcons.Count == 0) return;

            var camera = _mainCamera ?? Camera.main;
            if (camera == null) return;

            List<IUnit> staleUnits = null;
            foreach (var (unit, unitIcons) in _unitBuffIcons)
            {
                if (!IsUnityUnitAvailable(unit))
                {
                    unitIcons.Container?.RemoveFromHierarchy();
                    (staleUnits ??= new List<IUnit>()).Add(unit);
                    continue;
                }
                if (unitIcons.Icons.Count == 0) continue;

                Vector3 worldPos = new Vector3(
                    unit.WorldPosition.x,
                    unit.WorldPosition.y,
                    unit.WorldPosition.z);
                Vector3 displayPos = worldPos + Vector3.up * BuffIconYOffset;

                Vector3 screenPos = camera.WorldToScreenPoint(displayPos);
                if (screenPos.z < 0)
                {
                    unitIcons.Container.style.display = DisplayStyle.None;
                    continue;
                }

                float uiX = screenPos.x;
                float uiY = Screen.height - screenPos.y;

                unitIcons.Container.style.left = uiX;
                unitIcons.Container.style.top = uiY;
                unitIcons.Container.style.display = DisplayStyle.Flex;
            }

            if (staleUnits == null) return;
            foreach (var staleUnit in staleUnits)
                _unitBuffIcons.Remove(staleUnit);
        }

        private static bool IsUnityUnitAvailable(IUnit unit)
        {
            return unit != null &&
                (unit is not UnityEngine.Object unityObject || unityObject != null);
        }

        private void UpdateBuffTurnCounters(IUnit unit)
        {
            if (!_unitBuffIcons.TryGetValue(unit, out var unitIcons)) return;

            foreach (var (buff, iconWrapper) in unitIcons.Icons)
            {
                var turnLabel = iconWrapper.Q<Label>("turn-count");
                if (buff.RemainingTurns > 0)
                {
                    if (turnLabel == null)
                    {
                        turnLabel = new Label(buff.RemainingTurns.ToString());
                        turnLabel.style.position = Position.Absolute;
                        turnLabel.style.right = 0;
                        turnLabel.style.bottom = 0;
                        turnLabel.style.fontSize = BuffIconFontSize;
                        turnLabel.style.color = Color.white;
                        turnLabel.style.unityTextOutlineColor = Color.black;
                        turnLabel.style.unityTextOutlineWidth = 1;
                        turnLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                        turnLabel.name = "turn-count";
                        iconWrapper.Add(turnLabel);
                    }
                    else
                    {
                        turnLabel.text = buff.RemainingTurns.ToString();
                    }
                }
                else if (turnLabel != null)
                {
                    iconWrapper.Remove(turnLabel);
                }
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

        private void UpdateConsumableButton(IUnit unit)
        {
            var ability = unit?.GetBaseAbilities()?
                .OfType<ConsumableBattleAbility>()
                .FirstOrDefault();
            if (ability != null && ability.RemainingCharges <= 0)
                ability = null;

            SetCurrentConsumableAbility(ability);
            if (_consumableButton == null)
                return;

            if (ability == null)
            {
                _consumableButton.SetEnabled(false);
                _consumableButton.tooltip = null;
                _consumableButton.EnableInClassList("targeting", false);
                if (_consumableNameLabel != null)
                    _consumableNameLabel.text = "空";
                if (_consumableChargesLabel != null)
                    _consumableChargesLabel.text = "--";
                SetConsumableIcon(null);
                return;
            }

            int remainingCharges = ability.RemainingCharges;
            _consumableButton.SetEnabled(_gridController != null && ability.CanPerform(_gridController));
            _consumableButton.tooltip =
                $"{ability.Definition.DisplayName}\n{ability.Definition.Description}\n" +
                $"目标：自身或正交相邻友军（距离 {ability.Definition.MaxRange}）\n" +
                $"次数：{remainingCharges}/{ability.Definition.MaxCharges}";
            if (_consumableNameLabel != null)
                _consumableNameLabel.text = ability.Definition.DisplayName;
            if (_consumableChargesLabel != null)
                _consumableChargesLabel.text = $"{remainingCharges}/{ability.Definition.MaxCharges}";
            SetConsumableIcon(ability.Definition.IconPath);
        }

        private void SetCurrentConsumableAbility(ConsumableBattleAbility ability)
        {
            if (ReferenceEquals(_currentConsumableAbility, ability))
                return;

            if (_currentConsumableAbility != null)
                _currentConsumableAbility.UseCommitted -= OnConsumableUseCommitted;

            _currentConsumableAbility = ability;
            if (_currentConsumableAbility != null)
                _currentConsumableAbility.UseCommitted += OnConsumableUseCommitted;
        }

        private void OnConsumableUseCommitted(ConsumableBattleAbility ability)
        {
            _isConsumableTargeting = false;
            _consumableButton?.EnableInClassList("targeting", false);
            UpdateConsumableButton(_currentSelectedUnit);
        }

        private void SetConsumableIcon(string iconPath)
        {
            if (_consumableIcon == null)
                return;

            _consumableIcon.style.backgroundImage = null;
            _consumableIcon.style.backgroundColor = new StyleColor(new Color(0.24f, 0.18f, 0.14f));
            if (string.IsNullOrWhiteSpace(iconPath))
                return;

            var assetManager = GameAssetManager.Instance;
            if (assetManager == null || !assetManager.IsInitialized)
                return;

            var sprite = assetManager.Load<Sprite>(iconPath);
            if (sprite == null)
                return;

            _consumableIcon.style.backgroundImage = new StyleBackground(sprite);
            _consumableIcon.style.backgroundColor = Color.clear;
            assetManager.Release(iconPath);
        }

        private List<IAbility> GetSkillAbilities(IUnit unit)
        {
            return unit?.GetBaseAbilities()?
                .Where(ability => !IsMoveAbility(ability) && ability is not ConsumableBattleAbility)
                .ToList() ?? new List<IAbility>();
        }

        private bool IsMoveAbility(IAbility ability)
        {
            return ability.DisplayName == "Move";
        }

        #endregion

        #region Damage Numbers

        private void Update()
        {
            UpdateDamageNumbers();
            UpdateHoverHealthBar();
            SyncBuffIcons();
            UpdateBuffIconPositions();
            SyncSkillCardAvailability();
            SyncOrderedSelectionUi();
            SyncDroppedSpearMarkers();
        }

        private void EnableCancelTargetingInput()
        {
            _cancelTargetingAction ??= new InputAction("CancelTargeting", InputActionType.Button);
            _cancelTargetingAction.AddBinding("<Keyboard>/escape");
            _cancelTargetingAction.AddBinding("<Mouse>/rightButton");
            _cancelTargetingAction.performed += OnCancelTargetingPerformed;
            _cancelTargetingAction.Enable();
        }

        private void DisableCancelTargetingInput()
        {
            if (_cancelTargetingAction == null)
                return;

            _cancelTargetingAction.performed -= OnCancelTargetingPerformed;
            _cancelTargetingAction.Disable();
            _cancelTargetingAction.Dispose();
            _cancelTargetingAction = null;
        }

        /// <summary>
        /// Handles cancel input through Input System actions so virtual and physical devices share
        /// the same production targeting-cancellation path.
        /// </summary>
        private void OnCancelTargetingPerformed(InputAction.CallbackContext context)
        {
            if (_currentOrderedAbility?.OrderedSelection != null)
                UndoOrCancelOrderedSelection();

            if (_isConsumableTargeting)
            {
                _isConsumableTargeting = false;
                _consumableButton?.EnableInClassList("targeting", false);
                if (_gridController != null)
                    _gridController.GridState = new GridStateAwaitInput();
            }

            bool isHumanTargeting = _gridController?.TurnContext.CurrentPlayer?.PlayerType == PlayerType.HumanPlayer &&
                _gridController.GridState is not GridStateAwaitInput;
            if (isHumanTargeting &&
                _currentOrderedAbility?.OrderedSelection == null &&
                !_isConsumableTargeting)
            {
                _currentOrderedAbility = null;
                _currentMoveAbility = null;
                _gridController.GridState = new GridStateAwaitInput();
                HideAbilityReason();
                RefreshActionUi();
            }
        }

        public void RefreshActionUi()
        {
            if (_currentSelectedUnit != null)
            {
                UpdateMoveButtonState(_currentSelectedUnit);
                UpdateConsumableButton(_currentSelectedUnit);
                UpdateSkillCards(_currentSelectedUnit);
            }
            SyncSkillCardAvailability();
            SyncOrderedSelectionUi();
            SyncDroppedSpearMarkers();
        }

        private void OnOrderedSelectionMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 1 || _currentOrderedAbility?.OrderedSelection == null)
                return;
            UndoOrCancelOrderedSelection();
            evt.StopPropagation();
        }

        private void OnBattleKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape || _currentOrderedAbility?.OrderedSelection == null)
                return;
            UndoOrCancelOrderedSelection();
            evt.StopPropagation();
        }

        private void UndoOrCancelOrderedSelection()
        {
            if (_currentOrderedAbility?.OrderedSelection == null)
                return;
            if (!_currentOrderedAbility.UndoLastOrderedTarget())
            {
                _currentOrderedAbility.CancelOrderedSelection();
                _currentOrderedAbility = null;
                if (_gridController != null)
                    _gridController.GridState = new GridStateAwaitInput();
            }
            SyncOrderedSelectionUi();
        }

        private void ShowAbilityReason(string reason)
        {
            if (_abilityReasonTooltip == null)
                return;
            _abilityReasonTooltip.text = string.IsNullOrWhiteSpace(reason) ? "当前无法使用" : reason;
            _abilityReasonTooltip.style.display = DisplayStyle.Flex;
        }

        private void SyncSkillCardAvailability()
        {
            foreach (var pair in _skillCardAbilities)
            {
                var availability = AbilityAvailabilityResolver.Resolve(pair.Value, _gridController);
                pair.Key.userData = availability;
                pair.Key.style.display = availability.State == AbilityAvailabilityState.Hidden
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
                pair.Key.EnableInClassList("disabled", !availability.CanExecute);
                pair.Key.EnableInClassList("ability-disabled-clickable",
                    availability.State == AbilityAvailabilityState.DisabledClickable);
                pair.Key.EnableInClassList("ability-hidden", availability.State == AbilityAvailabilityState.Hidden);
                pair.Key.tooltip = availability.CanExecute ? null : availability.Reason;
            }
        }

        private void HideAbilityReason()
        {
            if (_abilityReasonTooltip != null)
                _abilityReasonTooltip.style.display = DisplayStyle.None;
        }

        private void SyncOrderedSelectionUi()
        {
            var selection = _currentOrderedAbility?.OrderedSelection;
            if (_orderedSelectionPanel == null || _orderedMarkerRoot == null || selection == null ||
                selection.Stage is OrderedSelectionStage.Cancelled or OrderedSelectionStage.Committed)
            {
                if (_orderedSelectionPanel != null)
                    _orderedSelectionPanel.style.display = DisplayStyle.None;
                _orderedMarkerRoot?.Clear();
                return;
            }

            _orderedSelectionPanel.style.display = DisplayStyle.Flex;
            _orderedSelectionPanel.userData = selection.Stage;
            if (_orderedSelectionPrompt != null)
                _orderedSelectionPrompt.text = $"第 {selection.Targets.Count + 1}/{selection.RequiredCount} 段";

            _orderedMarkerRoot.Clear();
            for (int index = 0; index < selection.Targets.Count; index++)
            {
                var target = selection.Targets[index];
                var marker = new Label((index + 1).ToString())
                {
                    name = $"OrderedTargetMarker_{index + 1}",
                    userData = target
                };
                marker.AddToClassList("ordered-target-marker");
                PositionWorldMarker(marker, target?.WorldPosition);
                _orderedMarkerRoot.Add(marker);
            }
        }

        private void SyncDroppedSpearMarkers()
        {
            if (_spearMarkerRoot == null)
                return;
            var liveSpears = FindObjectsByType<DroppedSpear>(FindObjectsSortMode.None).Where(spear => spear != null).ToHashSet();
            foreach (var stale in _spearMarkers.Keys.Where(spear => !liveSpears.Contains(spear)).ToList())
            {
                _spearMarkers[stale].RemoveFromHierarchy();
                _spearMarkers.Remove(stale);
            }
            foreach (var spear in liveSpears)
            {
                if (!_spearMarkers.TryGetValue(spear, out var marker))
                {
                    marker = new Label("长矛") { name = $"DroppedSpearMarker_{_spearMarkers.Count}" };
                    marker.AddToClassList("dropped-spear-marker");
                    _spearMarkerRoot.Add(marker);
                    _spearMarkers[spear] = marker;
                }
                PositionWorldMarker(marker, spear.CurrentCell?.WorldPosition);
            }
        }

        private void PositionWorldMarker(VisualElement marker, Tactics.Common.Utilities.Vector3Impl? worldPosition)
        {
            var camera = _mainCamera ?? Camera.main;
            if (marker == null || worldPosition == null || camera == null)
                return;
            var value = worldPosition.Value;
            Vector3 screen = camera.WorldToScreenPoint(new Vector3(value.x, value.y + 0.8f, value.z));
            marker.style.left = screen.x - 14f;
            marker.style.top = Screen.height - screen.y - 14f;
        }

        private static string ToElementKey(string value) => string.IsNullOrWhiteSpace(value)
            ? "Unknown"
            : new string(value.Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());

        /// <summary>
        /// Keeps the visual buff-icon state aligned with the unit runtime state.
        /// This self-heals cases where the UI missed a BuffChanged event but the
        /// unit already has active buffs (for example, timing-sensitive UI setup).
        /// </summary>
        private void SyncBuffIcons()
        {
            if (_gridController?.UnitManager == null || _buffIconRoot == null)
            {
                return;
            }

            foreach (var unit in _gridController.UnitManager.GetUnits().OfType<Unit>())
            {
                var activeBuffs = unit.GetActiveBuffs();
                if (!_unitBuffIcons.TryGetValue(unit, out var unitIcons))
                {
                    foreach (var activeBuff in activeBuffs)
                    {
                        AddBuffIcon(unit, activeBuff);
                    }

                    continue;
                }

                foreach (var activeBuff in activeBuffs)
                {
                    if (!unitIcons.Icons.ContainsKey(activeBuff))
                    {
                        AddBuffIcon(unit, activeBuff);
                    }
                }

                foreach (var staleEntry in unitIcons.Icons.Keys.ToList())
                {
                    if (!activeBuffs.Contains(staleEntry))
                    {
                        RemoveBuffIcon(unit, staleEntry);
                    }
                }

                UpdateBuffTurnCounters(unit);
            }
        }

        private void UpdateDamageNumbers()
        {
            if (_activeDamageNumbers.Count == 0) return;

            float currentTime = Time.time;
            var camera = Camera.main;
            if (camera == null) return;

            for (int i = _activeDamageNumbers.Count - 1; i >= 0; i--)
            {
                var instance = _activeDamageNumbers[i];
                float elapsed = currentTime - instance.SpawnTime;

                if (elapsed >= instance.Lifetime)
                {
                    DespawnDamageNumber(i);
                    continue;
                }

                Vector3 screenPos = camera.WorldToScreenPoint(instance.WorldStartPosition);
                if (screenPos.z < 0) continue;

                float uiX = screenPos.x;
                float uiY = Screen.height - screenPos.y;
                float moveOffset = instance.MoveSpeed * elapsed;

                instance.Label.style.left = uiX;
                instance.Label.style.top = uiY - moveOffset;

                float alpha;
                if (elapsed < instance.FadeInDuration)
                {
                    alpha = elapsed / instance.FadeInDuration;
                }
                else if (elapsed > instance.Lifetime - instance.FadeOutDuration)
                {
                    float fadeElapsed = elapsed - (instance.Lifetime - instance.FadeOutDuration);
                    alpha = 1f - (fadeElapsed / instance.FadeOutDuration);
                }
                else
                {
                    alpha = 1f;
                }
                instance.Label.style.opacity = alpha;

                float scale;
                if (elapsed < instance.FadeInDuration)
                {
                    float t = elapsed / instance.FadeInDuration;
                    scale = Mathf.Lerp(instance.StartScale, instance.PeakScale, t);
                }
                else
                {
                    float holdDuration = instance.Lifetime - instance.FadeInDuration;
                    float t = Mathf.Clamp01((elapsed - instance.FadeInDuration) / (holdDuration * 0.5f));
                    scale = Mathf.Lerp(instance.PeakScale, instance.EndScale, t);
                }
                instance.Label.style.scale = new Scale(new Vector2(scale, scale));

                _activeDamageNumbers[i] = instance;
            }
        }

        private void OnUnitHealthChanged(HealthChangedEventArgs args)
        {
            if (ReferenceEquals(args.AffectedUnit, _currentSelectedUnit))
            {
                UpdateStatusPanel();
            }
        }

        private void OnUnitManaChanged(ManaChangedEventArgs args)
        {
            if (ReferenceEquals(args.AffectedUnit, _currentSelectedUnit))
            {
                UpdateStatusPanel();
                UpdateSkillCards(_currentSelectedUnit);
            }
        }

        private void OnTurnEndManaRestored(TurnEndManaRestoredEventArgs args)
        {
            if (args.NewMana <= args.OldMana || !IsUnityUnitAvailable(args.AffectedUnit))
                return;

            int restored = Mathf.RoundToInt(args.NewMana - args.OldMana);
            var worldPosition = args.WorldPosition + Vector3.up * 1.5f;
            SpawnDamageNumber(DamageNumberType.Heal, $"+{restored} MP", worldPosition, new Color(0.35f, 0.65f, 1f));
        }

        private void OnBasicAbilityUsed(string abilityName)
        {
            UpdateSkillCards(_currentSelectedUnit);
            UpdateMoveButtonState(_currentSelectedUnit);
        }

        private void OnAnyUnitHealthChanged(HealthChangedEventArgs args)
        {
            if (args.HealthChangeAmount == 0 || !IsUnityUnitAvailable(args.AffectedUnit))
                return;

            var worldPos = args.AffectedUnit.WorldPosition;
            var unityPos = new Vector3(worldPos.x, worldPos.y, worldPos.z);
            var displayPos = unityPos + Vector3.up * 1.5f;

            if (args.HealthChangeAmount < 0)
            {
                string text = "-" + Mathf.Abs(Mathf.RoundToInt(args.HealthChangeAmount));
                SpawnDamageNumber(DamageNumberType.Normal, text, displayPos);
            }
            else
            {
                string text = "+" + Mathf.RoundToInt(args.HealthChangeAmount);
                SpawnDamageNumber(DamageNumberType.Heal, text, displayPos);
            }
        }

        private void SpawnDamageNumber(DamageNumberType type, string text, Vector3 worldPosition, Color? colorOverride = null)
        {
            if (_damageNumberContainer == null || _damageSettings == null) return;

            var config = _damageSettings.GetConfig(type);

            Label label;
            if (_damageNumberPool.Count > 0)
            {
                label = _damageNumberPool.Dequeue();
            }
            else
            {
                if (_activeDamageNumbers.Count > 0)
                {
                    var oldest = _activeDamageNumbers[0];
                    _activeDamageNumbers.RemoveAt(0);
                    _damageNumberContainer.Remove(oldest.Label);
                    label = oldest.Label;
                }
                else
                {
                    label = CreatePooledLabel();
                }
            }

            string displayText = type == DamageNumberType.Miss ? "Miss" : text;
            label.text = displayText;
            label.style.display = DisplayStyle.Flex;
            if (colorOverride.HasValue)
                label.style.color = colorOverride.Value;
            else
                label.style.color = StyleKeyword.Null;
            label.AddToClassList("damage-number");
            label.AddToClassList(config.ussClassName);

            var instance = new DamageNumberInstance
            {
                Label = label,
                WorldStartPosition = worldPosition,
                SpawnTime = Time.time,
                Lifetime = config.lifetime,
                MoveSpeed = config.moveSpeed,
                StartScale = config.startScale,
                PeakScale = config.peakScale,
                EndScale = config.endScale,
                FadeInDuration = config.fadeInDuration,
                FadeOutDuration = config.fadeOutDuration
            };

            _activeDamageNumbers.Add(instance);
            _damageNumberContainer.Add(label);
        }

        private Label CreatePooledLabel()
        {
            var label = new Label();
            label.style.position = Position.Absolute;
            label.pickingMode = PickingMode.Ignore;
            label.style.display = DisplayStyle.None;
            return label;
        }

        private void LoadDamageNumberSettings()
        {
            var mgr = GameAssetManager.Instance;
            if (mgr == null || !mgr.IsInitialized)
            {
                TLog.Warning("[BattleUIController] GameAssetManager not available, damage numbers disabled.");
                return;
            }

            try
            {
                _damageSettings = mgr.Load<DamageNumberSettings>(DamageNumberSettingsPath);
            }
            catch (System.Exception e)
            {
                TLog.Warning($"[BattleUIController] Failed to load DamageNumberSettings: {e.Message}");
            }
        }

        private void DespawnDamageNumber(int index)
        {
            var instance = _activeDamageNumbers[index];
            instance.Label.style.display = DisplayStyle.None;
            instance.Label.style.opacity = 1f;
            instance.Label.style.scale = new Scale(Vector2.one);

            instance.Label.RemoveFromClassList("damage-number");
            if (_damageSettings != null)
            {
                instance.Label.RemoveFromClassList(_damageSettings.normal.ussClassName);
                instance.Label.RemoveFromClassList(_damageSettings.crit.ussClassName);
                instance.Label.RemoveFromClassList(_damageSettings.heal.ussClassName);
                instance.Label.RemoveFromClassList(_damageSettings.miss.ussClassName);
            }

            _damageNumberContainer.Remove(instance.Label);
            _damageNumberPool.Enqueue(instance.Label);
            _activeDamageNumbers.RemoveAt(index);
        }

        #endregion
    }
}
