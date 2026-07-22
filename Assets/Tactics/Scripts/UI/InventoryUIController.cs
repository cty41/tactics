using System;
using System.Collections.Generic;
using System.Linq;
using Tactics.AssetPipeline;
using Tactics.Common.Battle;
using Tactics.Common.Units.Abilities;
using Tactics.Consumables;
using Tactics.Equipment;
using Tactics.Roster;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.UI
{
    /// <summary>
    /// Presents character loadouts and the shared equipment/consumable backpack.
    /// </summary>
    /// <remarks>
    /// All mutations are delegated to CharacterLoadoutService. The controller owns only
    /// selection, filtering, anchored popover state, immediate persistence, and rendering.
    /// </remarks>
    public sealed class InventoryUIController : UIControllerBase
    {
        private const int SlotsPerRow = 4;
        private const int DefaultRows = 5;
        private const int DefaultCapacity = SlotsPerRow * DefaultRows;
        private const float PopoverWidth = 320f;
        private const float PopoverGap = 8f;

        private enum InventoryFilter
        {
            All,
            Equipment,
            Consumable
        }

        private enum ItemLocation
        {
            BackpackEquipment,
            BackpackConsumable,
            EquippedEquipment,
            CarriedConsumable
        }

        private sealed class InventoryEntry
        {
            public ItemLocation Location;
            public string EquipmentId;
            public string ConsumableInstanceId;
            public EquipmentSlot EquipmentSlot;
        }

        private VisualElement _root;
        private Label _characterNameLabel;
        private Label _levelLabel;
        private Label _hpLabel;
        private VisualElement _hpBarFill;
        private Label _strengthValue;
        private Label _agilityValue;
        private Label _constitutionValue;
        private Label _intelligenceValue;
        private Label _charismaValue;
        private Label _luckValue;

        private VisualElement _equippedSlotsParent;
        private VisualElement _carriedConsumableSlot;
        private Label _carriedConsumableLabel;
        private VisualElement _storageGrid;
        private VisualElement _skillSlotsParent;
        private VisualElement _characterSwitchButtons;
        private VisualElement _portraitContainer;

        private Button _filterAllButton;
        private Button _filterEquipmentButton;
        private Button _filterConsumableButton;
        private VisualElement _itemPopover;
        private Image _itemIcon;
        private Label _itemIconPlaceholder;
        private Label _itemName;
        private Label _itemMeta;
        private Label _itemDescription;
        private Button _itemActionButton;

        private CharacterDefinition _currentCharacter;
        private PlayerAdventureState _currentState;
        private int _selectedPartyIndex;
        private InventoryFilter _activeFilter = InventoryFilter.All;
        private InventoryEntry _selectedEntry;
        private VisualElement _popoverAnchor;

        private readonly Dictionary<EquipmentSlot, VisualElement> _equippedSlotElements = new();
        private readonly Dictionary<EquipmentSlot, Label> _equippedSlotLabels = new();
        private readonly List<VisualElement> _storageSlotElements = new();
        private readonly List<InventoryEntry> _storageEntries = new();

        protected override void OnShown()
        {
            base.OnShown();
            EnsureUIElements();
            RegisterKeyEvents();
            LoadState();
            SetupCharacterSwitchButtons();
            RefreshAll();
        }

        protected override void OnHidden()
        {
            HideItemPopover();
            UnregisterKeyEvents();
        }

        /// <summary>
        /// Reloads the persisted inventory state and redraws the current character view.
        /// </summary>
        public void RefreshView()
        {
            EnsureUIElements();
            LoadState();
            SetupCharacterSwitchButtons();
            RefreshAll();
        }

        private void EnsureUIElements()
        {
            if (_root != null)
                return;

            _root = Ui.GetRootElement(UIManager.UIId.Inventory);
            if (_root == null)
                return;

            _characterNameLabel = _root.Q<Label>("CharacterNameLabel");
            _levelLabel = _root.Q<Label>("LevelLabel");
            _hpLabel = _root.Q<Label>("HPLabel");
            _hpBarFill = _root.Q<VisualElement>("HPBarFill");

            _strengthValue = _root.Q<Label>("StrengthValue");
            _agilityValue = _root.Q<Label>("AgilityValue");
            _constitutionValue = _root.Q<Label>("ConstitutionValue");
            _intelligenceValue = _root.Q<Label>("IntelligenceValue");
            _charismaValue = _root.Q<Label>("CharismaValue");
            _luckValue = _root.Q<Label>("LuckValue");

            _equippedSlotsParent = _root.Q<VisualElement>("EquipmentSlotsGrid");
            _carriedConsumableSlot = _root.Q<VisualElement>("CarriedConsumableSlot");
            _carriedConsumableLabel = _root.Q<Label>("CarriedConsumableLabel");
            _storageGrid = _root.Q<VisualElement>("StorageGrid");
            _skillSlotsParent = _root.Q<VisualElement>("SkillSlots");
            _characterSwitchButtons = _root.Q<VisualElement>("CharacterSwitchButtons");
            _portraitContainer = _root.Q<VisualElement>("PortraitSmall");

            _filterAllButton = _root.Q<Button>("InventoryFilterAll");
            _filterEquipmentButton = _root.Q<Button>("InventoryFilterEquipment");
            _filterConsumableButton = _root.Q<Button>("InventoryFilterConsumable");
            _itemPopover = _root.Q<VisualElement>("InventoryItemPopover");
            _itemIcon = _root.Q<Image>("InventoryItemIcon");
            _itemIconPlaceholder = _root.Q<Label>("InventoryItemIconPlaceholder");
            _itemName = _root.Q<Label>("InventoryItemName");
            _itemMeta = _root.Q<Label>("InventoryItemMeta");
            _itemDescription = _root.Q<Label>("InventoryItemDescription");
            _itemActionButton = _root.Q<Button>("InventoryItemActionButton");

            var closeButton = _root.Q<Button>("CloseButton");
            if (closeButton != null)
                closeButton.clicked += OnCloseClicked;
            if (_filterAllButton != null)
                _filterAllButton.clicked += () => SetFilter(InventoryFilter.All);
            if (_filterEquipmentButton != null)
                _filterEquipmentButton.clicked += () => SetFilter(InventoryFilter.Equipment);
            if (_filterConsumableButton != null)
                _filterConsumableButton.clicked += () => SetFilter(InventoryFilter.Consumable);
            if (_itemActionButton != null)
                _itemActionButton.clicked += OnItemActionClicked;
            if (_carriedConsumableSlot != null)
                _carriedConsumableSlot.RegisterCallback<ClickEvent>(_ => OnCarriedConsumableClicked());

            _root.RegisterCallback<PointerDownEvent>(OnRootPointerDown, TrickleDown.TrickleDown);
            InitializeEquippedSlots();
        }

        private void InitializeEquippedSlots()
        {
            if (_equippedSlotsParent == null)
                return;

            _equippedSlotElements.Clear();
            _equippedSlotLabels.Clear();
            _equippedSlotsParent.Clear();

            var slots = new[]
            {
                EquipmentSlot.Weapon,
                EquipmentSlot.Armor,
                EquipmentSlot.Helmet,
                EquipmentSlot.Boots,
                EquipmentSlot.Accessory
            };

            foreach (var slot in slots)
            {
                var slotElement = new VisualElement
                {
                    name = $"EquippedSlot_{slot}",
                    userData = slot
                };
                slotElement.AddToClassList("equipped-slot");

                var label = new Label("空");
                label.AddToClassList("inventory-slot-label");
                slotElement.Add(label);
                slotElement.RegisterCallback<ClickEvent>(_ => OnEquippedSlotClicked(slot));

                _equippedSlotElements[slot] = slotElement;
                _equippedSlotLabels[slot] = label;
                _equippedSlotsParent.Add(slotElement);
            }
        }

        private void LoadState()
        {
            _currentState = PlayerAdventureStateStore.LoadRepairAndSave();
            LoadCharacterData();
        }

        private void LoadCharacterData()
        {
            if (_currentState?.Roster == null || _currentState.ActivePartyCharacterIds == null)
            {
                _currentCharacter = null;
                return;
            }

            var activeIds = _currentState.ActivePartyCharacterIds;
            if (activeIds.Count == 0)
            {
                _currentCharacter = _currentState.Roster.FirstOrDefault();
                return;
            }

            _selectedPartyIndex = Mathf.Clamp(_selectedPartyIndex, 0, activeIds.Count - 1);
            _currentCharacter = _currentState.Roster.FirstOrDefault(
                character => character.Id == activeIds[_selectedPartyIndex]);
        }

        private void SetupCharacterSwitchButtons()
        {
            if (_characterSwitchButtons == null || _currentState == null)
                return;

            _characterSwitchButtons.Clear();
            var activeIds = _currentState.ActivePartyCharacterIds ?? new List<string>();
            for (int index = 0; index < activeIds.Count; index++)
            {
                int capturedIndex = index;
                var character = _currentState.Roster.FirstOrDefault(candidate => candidate.Id == activeIds[index]);
                var button = new Button
                {
                    name = $"CharBtn_{index}",
                    text = character?.DisplayName ?? $"角色{index + 1}"
                };
                button.AddToClassList("character-switch-btn");
                if (index == _selectedPartyIndex)
                    button.AddToClassList("active");
                button.clicked += () => OnCharacterSwitchClicked(capturedIndex);
                _characterSwitchButtons.Add(button);
            }
        }

        private void OnCharacterSwitchClicked(int index)
        {
            HideItemPopover();
            _selectedPartyIndex = index;
            LoadCharacterData();
            SetupCharacterSwitchButtons();
            RefreshAll();
        }

        private void SetFilter(InventoryFilter filter)
        {
            if (_activeFilter == filter)
                return;

            HideItemPopover();
            _activeFilter = filter;
            RefreshFilterButtons();
            RefreshStorage();
        }

        private void RefreshAll()
        {
            RefreshFilterButtons();
            RefreshCharacterInfo();
            RefreshEquippedSlots();
            RefreshCarriedConsumableSlot();
            RefreshStorage();
            RefreshSkillSlots();
            RefreshPortrait();
        }

        private void RefreshFilterButtons()
        {
            SetFilterButtonState(_filterAllButton, _activeFilter == InventoryFilter.All);
            SetFilterButtonState(_filterEquipmentButton, _activeFilter == InventoryFilter.Equipment);
            SetFilterButtonState(_filterConsumableButton, _activeFilter == InventoryFilter.Consumable);
        }

        private static void SetFilterButtonState(Button button, bool active)
        {
            if (button == null)
                return;

            button.EnableInClassList("active", active);
        }

        private void RefreshCharacterInfo()
        {
            if (_currentCharacter == null)
            {
                SetDefaultCharacterInfo();
                return;
            }

            if (_characterNameLabel != null)
                _characterNameLabel.text = _currentCharacter.DisplayName ?? "未知";
            if (_levelLabel != null)
                _levelLabel.text = $"Lv.{_currentCharacter.Level}";
            if (_hpLabel != null)
            {
                _hpLabel.text = _currentCharacter.IsDead
                    ? $"DEAD 0/{_currentCharacter.MaxHp}"
                    : $"{_currentCharacter.CurrentHp}/{_currentCharacter.MaxHp}  MP {_currentCharacter.CurrentMp ?? 0}/{_currentCharacter.MaxMp}";
            }
            if (_hpBarFill != null)
            {
                _hpBarFill.style.width = Length.Percent(
                    _currentCharacter.IsDead
                        ? 0f
                        : Mathf.Clamp01(_currentCharacter.CurrentHp / (float)_currentCharacter.MaxHp) * 100f);
            }

            SetStatValue(_strengthValue, _currentCharacter.Strength, _currentCharacter.GetTotalStrength());
            SetStatValue(_agilityValue, _currentCharacter.Agility, _currentCharacter.GetTotalAgility());
            SetStatValue(_constitutionValue, _currentCharacter.Constitution, _currentCharacter.GetTotalConstitution());
            SetStatValue(_intelligenceValue, _currentCharacter.Intelligence, _currentCharacter.GetTotalIntelligence());
            SetStatValue(_charismaValue, _currentCharacter.Charisma, _currentCharacter.GetTotalCharisma());
            SetStatValue(_luckValue, _currentCharacter.Luck, _currentCharacter.GetTotalLuck());
        }

        private static void SetStatValue(Label label, int baseValue, int totalValue)
        {
            if (label == null)
                return;

            label.text = totalValue.ToString();
            label.style.color = totalValue switch
            {
                _ when totalValue > baseValue => new Color(0.2f, 0.8f, 0.2f),
                _ when totalValue < baseValue => new Color(0.8f, 0.2f, 0.2f),
                _ => new Color(0.37f, 0.31f, 0.25f)
            };
        }

        private void SetDefaultCharacterInfo()
        {
            if (_characterNameLabel != null)
                _characterNameLabel.text = "未选择角色";
            if (_levelLabel != null)
                _levelLabel.text = "Lv.--";
            if (_hpLabel != null)
                _hpLabel.text = "--/--";
            if (_hpBarFill != null)
                _hpBarFill.style.width = Length.Percent(0);

            foreach (var label in new[]
                     {
                         _strengthValue,
                         _agilityValue,
                         _constitutionValue,
                         _intelligenceValue,
                         _charismaValue,
                         _luckValue
                     })
            {
                if (label == null)
                    continue;
                label.text = "--";
                label.style.color = new Color(0.37f, 0.31f, 0.25f);
            }
        }

        private void RefreshEquippedSlots()
        {
            foreach (var pair in _equippedSlotLabels)
            {
                var label = pair.Value;
                string equipmentId = null;
                bool hasItem = _currentCharacter?.Equipment != null &&
                               _currentCharacter.Equipment.TryGetValue(pair.Key, out equipmentId) &&
                               !string.IsNullOrWhiteSpace(equipmentId);
                label.text = hasItem
                    ? EquipmentDatabase.GetById(equipmentId)?.DisplayName ?? equipmentId
                    : "空";
                label.EnableInClassList("filled", hasItem);
            }
        }

        private void RefreshCarriedConsumableSlot()
        {
            if (_carriedConsumableLabel == null)
                return;

            var instance = GetCarriedConsumable();
            var definition = instance == null ? null : ConsumableDatabase.GetById(instance.DefinitionId);
            bool hasItem = instance != null;
            _carriedConsumableLabel.text = hasItem
                ? $"{definition?.DisplayName ?? instance.DefinitionId}\n{instance.RemainingCharges}/{instance.MaxCharges}"
                : "空";
            _carriedConsumableSlot?.EnableInClassList("filled", hasItem);
        }

        private void RefreshStorage()
        {
            if (_storageGrid == null || _currentState == null)
                return;

            _storageGrid.Clear();
            _storageSlotElements.Clear();
            _storageEntries.Clear();

            if (_activeFilter is InventoryFilter.All or InventoryFilter.Equipment)
            {
                foreach (string equipmentId in _currentState.Inventory ?? Enumerable.Empty<string>())
                {
                    _storageEntries.Add(new InventoryEntry
                    {
                        Location = ItemLocation.BackpackEquipment,
                        EquipmentId = equipmentId
                    });
                }
            }

            if (_activeFilter is InventoryFilter.All or InventoryFilter.Consumable)
            {
                foreach (var instance in CharacterLoadoutService.GetBackpackConsumables(_currentState))
                {
                    _storageEntries.Add(new InventoryEntry
                    {
                        Location = ItemLocation.BackpackConsumable,
                        ConsumableInstanceId = instance.InstanceId
                    });
                }
            }

            int totalSlots = Mathf.Max(
                DefaultCapacity,
                GetNextMultipleOf(_storageEntries.Count, SlotsPerRow));
            for (int index = 0; index < totalSlots; index++)
            {
                InventoryEntry entry = index < _storageEntries.Count ? _storageEntries[index] : null;
                var slotElement = new VisualElement
                {
                    name = $"StorageSlot_{index}",
                    userData = entry
                };
                slotElement.AddToClassList("storage-slot-4col");
                if (index % SlotsPerRow != SlotsPerRow - 1)
                    slotElement.style.marginRight = Length.Percent(4);

                var label = new Label(GetEntryDisplayName(entry));
                label.AddToClassList("inventory-slot-label");
                label.EnableInClassList("empty", entry == null);
                slotElement.Add(label);

                if (entry != null)
                {
                    int capturedIndex = index;
                    slotElement.RegisterCallback<ClickEvent>(_ => OnStorageSlotClicked(capturedIndex));
                }

                _storageSlotElements.Add(slotElement);
                _storageGrid.Add(slotElement);
            }
        }

        private string GetEntryDisplayName(InventoryEntry entry)
        {
            if (entry == null)
                return "空";

            if (entry.Location == ItemLocation.BackpackEquipment)
                return EquipmentDatabase.GetById(entry.EquipmentId)?.DisplayName ?? entry.EquipmentId;

            var instance = FindConsumable(entry.ConsumableInstanceId);
            var definition = instance == null ? null : ConsumableDatabase.GetById(instance.DefinitionId);
            return instance == null
                ? "空"
                : $"{definition?.DisplayName ?? instance.DefinitionId}\n{instance.RemainingCharges}/{instance.MaxCharges}";
        }

        private static int GetNextMultipleOf(int value, int multiple)
        {
            if (value <= 0)
                return multiple;
            return ((value + multiple - 1) / multiple) * multiple;
        }

        private void OnStorageSlotClicked(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _storageEntries.Count)
                return;

            ShowItemPopover(_storageEntries[slotIndex], _storageSlotElements[slotIndex]);
        }

        private void OnEquippedSlotClicked(EquipmentSlot slot)
        {
            if (_currentCharacter?.Equipment == null ||
                !_currentCharacter.Equipment.TryGetValue(slot, out string equipmentId) ||
                string.IsNullOrWhiteSpace(equipmentId))
            {
                return;
            }

            ShowItemPopover(
                new InventoryEntry
                {
                    Location = ItemLocation.EquippedEquipment,
                    EquipmentId = equipmentId,
                    EquipmentSlot = slot
                },
                _equippedSlotElements[slot]);
        }

        private void OnCarriedConsumableClicked()
        {
            var instance = GetCarriedConsumable();
            if (instance == null)
                return;

            ShowItemPopover(
                new InventoryEntry
                {
                    Location = ItemLocation.CarriedConsumable,
                    ConsumableInstanceId = instance.InstanceId
                },
                _carriedConsumableSlot);
        }

        private void ShowItemPopover(InventoryEntry entry, VisualElement anchor)
        {
            if (_itemPopover == null || entry == null || anchor == null)
                return;

            _selectedEntry = entry;
            _popoverAnchor = anchor;
            if (_itemActionButton != null)
                _itemActionButton.style.display = DisplayStyle.Flex;
            PopulateItemPopover(entry);
            _itemPopover.style.display = DisplayStyle.Flex;

            PositionPopover(anchor);
        }

        private void PositionPopover(VisualElement anchor)
        {
            if (_itemPopover == null || _root == null || anchor == null)
                return;

            Rect rootBounds = _root.worldBound;
            Rect anchorBounds = anchor.worldBound;
            float left = anchorBounds.xMax - rootBounds.xMin + PopoverGap;
            if (left + PopoverWidth > rootBounds.width)
                left = anchorBounds.xMin - rootBounds.xMin - PopoverWidth - PopoverGap;
            float top = anchorBounds.yMin - rootBounds.yMin;

            _itemPopover.style.left = Mathf.Max(8f, left);
            _itemPopover.style.top = Mathf.Max(8f, top);
        }

        private void PopulateItemPopover(InventoryEntry entry)
        {
            if (entry.Location is ItemLocation.BackpackEquipment or ItemLocation.EquippedEquipment)
            {
                PopulateEquipmentPopover(entry);
                return;
            }

            PopulateConsumablePopover(entry);
        }

        private void PopulateEquipmentPopover(InventoryEntry entry)
        {
            var definition = EquipmentDatabase.GetById(entry.EquipmentId);
            if (_itemName != null)
                _itemName.text = definition?.DisplayName ?? entry.EquipmentId;
            if (_itemMeta != null)
            {
                _itemMeta.text = definition == null
                    ? "装备"
                    : $"{definition.Rarity} · {definition.Slot}";
            }
            if (_itemDescription != null)
                _itemDescription.text = BuildEquipmentDescription(definition);
            if (_itemActionButton != null)
            {
                _itemActionButton.text = entry.Location == ItemLocation.EquippedEquipment
                    ? "卸下"
                    : IsEquipmentSlotOccupied(definition?.Slot) ? "替换" : "装备";
                _itemActionButton.SetEnabled(_currentCharacter?.IsDead == false);
            }

            SetPopoverIcon(null);
        }

        private void PopulateConsumablePopover(InventoryEntry entry)
        {
            var instance = FindConsumable(entry.ConsumableInstanceId);
            var definition = instance == null ? null : ConsumableDatabase.GetById(instance.DefinitionId);
            if (_itemName != null)
                _itemName.text = definition?.DisplayName ?? instance?.DefinitionId ?? "未知消耗品";
            if (_itemMeta != null)
            {
                _itemMeta.text = instance == null
                    ? "消耗品"
                    : $"{definition?.Rarity.ToString() ?? "Unknown"} · {instance.RemainingCharges}/{instance.MaxCharges} · 自身及正交相邻 1 格";
            }
            if (_itemDescription != null)
                _itemDescription.text = definition?.Description ?? string.Empty;
            if (_itemActionButton != null)
            {
                _itemActionButton.text = entry.Location == ItemLocation.CarriedConsumable
                    ? "卸下"
                    : GetCarriedConsumable() == null ? "携带" : "替换";
                _itemActionButton.SetEnabled(_currentCharacter?.IsDead == false);
            }

            SetPopoverIcon(definition?.IconPath);
        }

        private static string BuildEquipmentDescription(EquipmentDefinition definition)
        {
            if (definition == null)
                return string.Empty;

            var bonuses = new List<string>();
            AddBonus(bonuses, "力量", definition.StrengthBonus);
            AddBonus(bonuses, "敏捷", definition.AgilityBonus);
            AddBonus(bonuses, "体质", definition.ConstitutionBonus);
            AddBonus(bonuses, "智力", definition.IntelligenceBonus);
            AddBonus(bonuses, "魅力", definition.CharismaBonus);
            AddBonus(bonuses, "幸运", definition.LuckBonus);
            return bonuses.Count == 0 ? "无额外属性。" : string.Join("，", bonuses);
        }

        private static void AddBonus(ICollection<string> bonuses, string name, int value)
        {
            if (value != 0)
                bonuses.Add($"{name} {(value > 0 ? "+" : string.Empty)}{value}");
        }

        private void SetPopoverIcon(string iconPath)
        {
            if (_itemIcon == null || _itemIconPlaceholder == null)
                return;

            _itemIcon.sprite = null;
            _itemIcon.style.display = DisplayStyle.None;
            _itemIconPlaceholder.style.display = DisplayStyle.Flex;
            if (string.IsNullOrWhiteSpace(iconPath))
                return;

            var assetManager = GameAssetManager.Instance;
            if (assetManager == null || !assetManager.IsInitialized)
                return;

            var sprite = assetManager.Load<Sprite>(iconPath);
            if (sprite == null)
                return;

            _itemIcon.sprite = sprite;
            _itemIcon.style.display = DisplayStyle.Flex;
            _itemIconPlaceholder.style.display = DisplayStyle.None;
            assetManager.Release(iconPath);
        }

        private void OnItemActionClicked()
        {
            if (_selectedEntry == null || _currentState == null || _currentCharacter == null ||
                _currentCharacter.IsDead)
            {
                return;
            }

            bool changed = _selectedEntry.Location switch
            {
                ItemLocation.BackpackEquipment => CharacterLoadoutService.TryEquipEquipment(
                    _currentState,
                    _currentCharacter.Id,
                    _selectedEntry.EquipmentId),
                ItemLocation.BackpackConsumable => CharacterLoadoutService.TryCarryConsumable(
                    _currentState,
                    _currentCharacter.Id,
                    _selectedEntry.ConsumableInstanceId),
                ItemLocation.EquippedEquipment => CharacterLoadoutService.TryUnequipEquipment(
                    _currentState,
                    _currentCharacter.Id,
                    _selectedEntry.EquipmentSlot),
                ItemLocation.CarriedConsumable => CharacterLoadoutService.TryUnloadConsumable(
                    _currentState,
                    _currentCharacter.Id),
                _ => false
            };

            if (!changed)
                return;

            PlayerAdventureStateStore.Save(_currentState);
            HideItemPopover();
            RefreshAll();
        }

        private bool IsEquipmentSlotOccupied(EquipmentSlot? slot)
        {
            if (!slot.HasValue || _currentCharacter?.Equipment == null)
                return false;

            return _currentCharacter.Equipment.TryGetValue(slot.Value, out string equipmentId) &&
                   !string.IsNullOrWhiteSpace(equipmentId);
        }

        private ConsumableInstance GetCarriedConsumable()
        {
            return FindConsumable(_currentCharacter?.CarriedConsumableInstanceId);
        }

        private ConsumableInstance FindConsumable(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
                return null;

            return _currentState?.ConsumableInstances?.FirstOrDefault(instance =>
                instance != null &&
                string.Equals(instance.InstanceId, instanceId, StringComparison.Ordinal));
        }

        private void HideItemPopover()
        {
            if (_itemPopover != null)
                _itemPopover.style.display = DisplayStyle.None;
            _selectedEntry = null;
            _popoverAnchor = null;
        }

        private void OnRootPointerDown(PointerDownEvent evt)
        {
            if (_itemPopover == null || _itemPopover.style.display == DisplayStyle.None)
                return;

            var target = evt.target as VisualElement;
            if (target != null &&
                (_itemPopover.Contains(target) || _popoverAnchor?.Contains(target) == true))
            {
                return;
            }

            HideItemPopover();
        }

        private void RefreshSkillSlots()
        {
            if (_skillSlotsParent == null)
                return;

            var learnedSkills = _currentCharacter?.LearnedSkills?
                .Where(learned => learned != null && learned.SkillType != SkillType.ExtraUtility)
                .Where(learned => !PureRunAbilityCatalog.TryGet(learned.SkillId, out var definition) || definition.IsMapVisible)
                .OrderBy(learned => learned.SkillType == SkillType.Active ? 0 : 1)
                .ToList() ?? new List<CharacterDefinition.LearnedSkill>();

            int slotCount = Mathf.Max(3, learnedSkills.Count);
            _skillSlotsParent.Clear();
            for (int index = 0; index < slotCount; index++)
            {
                var slot = new VisualElement { name = $"InventorySkillSlot_{index}" };
                slot.AddToClassList("skill-slot");
                var learned = index < learnedSkills.Count ? learnedSkills[index] : null;
                var skill = learned == null ? null : ResolveSkill(learned.SkillId);
                var label = new Label(skill == null ? "空" : $"{skill.DisplayName}\nLv.{Mathf.Max(1, learned.Level)}")
                {
                    name = $"InventorySkillLabel_{index}"
                };
                label.AddToClassList("inventory-slot-label");
                if (skill == null)
                {
                    label.AddToClassList("empty");
                }
                else
                {
                    slot.AddToClassList("filled");
                    var capturedLearned = learned;
                    var capturedSkill = skill;
                    slot.RegisterCallback<ClickEvent>(evt =>
                    {
                        ShowSkillPopover(capturedLearned, capturedSkill, slot);
                        evt.StopPropagation();
                    });
                }
                slot.Add(label);
                _skillSlotsParent.Add(slot);
            }
        }

        private void ShowSkillPopover(
            CharacterDefinition.LearnedSkill learned,
            SkillDefinition skill,
            VisualElement anchor)
        {
            if (_itemPopover == null || learned == null || skill == null || anchor == null)
                return;

            _selectedEntry = null;
            _popoverAnchor = anchor;
            if (_itemName != null)
                _itemName.text = skill.DisplayName;
            if (_itemMeta != null)
                _itemMeta.text = $"{(learned.SkillType == SkillType.Active ? "主动" : "被动")} · Lv.{Mathf.Max(1, learned.Level)}";
            if (_itemDescription != null)
                _itemDescription.text = ResolveLevelDescription(learned.SkillId, learned.Level, skill.Description);
            if (_itemActionButton != null)
                _itemActionButton.style.display = DisplayStyle.None;
            SetPopoverIcon(null);
            _itemPopover.style.display = DisplayStyle.Flex;
            PositionPopover(anchor);
        }

        private static SkillDefinition ResolveSkill(string skillId)
        {
            if (PureRunAbilityCatalog.TryGet(skillId, out var definition))
                return definition.Skill;
            return SkillDatabase.GetSkillById(skillId);
        }

        private static string ResolveLevelDescription(string skillId, int level, string fallback)
        {
            if (!PureRunAbilityCatalog.TryResolveAbilityPath(skillId, level, out string path, out _) ||
                string.IsNullOrWhiteSpace(path))
                return fallback ?? string.Empty;
            var manager = GameAssetManager.Instance;
            if (manager == null || !manager.IsInitialized)
                return fallback ?? string.Empty;
            var config = manager.Load<AbilityConfig>(path);
            string description = string.IsNullOrWhiteSpace(config?.Description) ? fallback : config.Description;
            manager.Release(path);
            return description ?? string.Empty;
        }

        private void RefreshPortrait()
        {
            if (_portraitContainer == null)
                return;

            _portraitContainer.Clear();
            if (_currentCharacter == null)
            {
                AddPortraitPlaceholder("[立绘]");
                return;
            }

            string resolvedPath = CharacterDefinition.ResolvePrefabPath(_currentCharacter.PrefabPath);
            var assetManager = GameAssetManager.Instance;
            if (string.IsNullOrWhiteSpace(resolvedPath) || assetManager == null || !assetManager.IsInitialized)
            {
                AddPortraitPlaceholder("[无立绘]");
                return;
            }

            var prefab = assetManager.Load<GameObject>(resolvedPath);
            if (prefab == null)
            {
                AddPortraitPlaceholder("[无立绘]");
                return;
            }

            var renderer = prefab.transform.Find("Sprite")?.GetComponent<SpriteRenderer>();
            if (renderer?.sprite != null)
            {
                var image = new Image
                {
                    sprite = renderer.sprite,
                    scaleMode = ScaleMode.ScaleToFit
                };
                image.style.width = Length.Percent(100);
                image.style.height = Length.Percent(100);
                _portraitContainer.Add(image);
            }
            else
            {
                AddPortraitPlaceholder("[无立绘]");
            }

            assetManager.Release(resolvedPath);
        }

        private void AddPortraitPlaceholder(string text)
        {
            var placeholder = new Label(text);
            placeholder.AddToClassList("portrait-placeholder");
            _portraitContainer.Add(placeholder);
        }

        private void RegisterKeyEvents()
        {
            _root?.RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        private void UnregisterKeyEvents()
        {
            _root?.UnregisterCallback<KeyDownEvent>(OnKeyDown);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape)
                return;

            if (_itemPopover != null && _itemPopover.style.display != DisplayStyle.None)
                HideItemPopover();
            else
                OnCloseClicked();
            evt.StopPropagation();
        }

        private void OnCloseClicked()
        {
            HideItemPopover();
            Ui.Hide(UIManager.UIId.Inventory);
        }
    }
}
