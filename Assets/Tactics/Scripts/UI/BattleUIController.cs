using System.Collections.Generic;
using System.Linq;
using Tactics.AssetPipeline;
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
                playerMap.Enable();

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

                if (unit is ICombatant combatant)
                {
                    combatant.HealthChanged -= OnAnyUnitHealthChanged;
                }

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

        #region Damage Numbers

        private void Update()
        {
            UpdateDamageNumbers();
            UpdateHoverHealthBar();
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

        private void OnAnyUnitHealthChanged(HealthChangedEventArgs args)
        {
            if (args.HealthChangeAmount == 0) return;

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

        private void SpawnDamageNumber(DamageNumberType type, string text, Vector3 worldPosition)
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
                Debug.LogWarning("[BattleUIController] GameAssetManager not available, damage numbers disabled.");
                return;
            }

            try
            {
                _damageSettings = mgr.Load<DamageNumberSettings>(DamageNumberSettingsPath);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[BattleUIController] Failed to load DamageNumberSettings: {e.Message}");
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
