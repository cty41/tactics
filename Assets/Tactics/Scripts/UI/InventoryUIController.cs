using System.Collections.Generic;
using Tactics.Runtime.Utilities;
using System.Linq;
using Tactics.AssetPipeline;
using Tactics.Equipment;
using Tactics.Roster;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.UI
{
    public sealed class InventoryUIController : UIControllerBase
    {
        private const int SlotsPerRow = 4;
        private const int DefaultRows = 5;
        private const int DefaultCapacity = SlotsPerRow * DefaultRows;
        private const int DoubleClickThresholdMs = 300;

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
        private VisualElement _storageGrid;
        private VisualElement _skillSlotsParent;
        private VisualElement _characterSwitchButtons;
        private VisualElement _portraitContainer;

        private CharacterDefinition _currentCharacter;
        private PlayerAdventureState _currentState;
        private int _selectedPartyIndex = 0;
        private string _lastClickedSlotId;
        private long _lastClickTime;

        private readonly Dictionary<EquipmentSlot, VisualElement> _equippedSlotElements = new Dictionary<EquipmentSlot, VisualElement>();
        private readonly Dictionary<EquipmentSlot, Label> _equippedSlotLabels = new Dictionary<EquipmentSlot, Label>();
        private readonly List<VisualElement> _storageSlotElements = new List<VisualElement>();
        private readonly List<Label> _storageSlotLabels = new List<Label>();
        private readonly List<string> _storageSlotEquipmentIds = new List<string>();

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
            UnregisterKeyEvents();
        }

        private void EnsureUIElements()
        {
            if (_root != null) return;

            _root = Ui.GetRootElement(UIManager.UIId.Inventory);
            if (_root == null) return;

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
            _storageGrid = _root.Q<VisualElement>("StorageGrid");
            _skillSlotsParent = _root.Q<VisualElement>("SkillSlots");
            _characterSwitchButtons = _root.Q<VisualElement>("CharacterSwitchButtons");
            _portraitContainer = _root.Q<VisualElement>("PortraitSmall");

            var closeButton = _root.Q<Button>("CloseButton");
            if (closeButton != null)
                closeButton.clicked += OnCloseClicked;

            InitializeEquippedSlots();
        }

        private void InitializeEquippedSlots()
        {
            if (_equippedSlotsParent == null) return;

            _equippedSlotElements.Clear();
            _equippedSlotLabels.Clear();
            _equippedSlotsParent.Clear();

            var slots = new[] { EquipmentSlot.Weapon, EquipmentSlot.Armor, EquipmentSlot.Helmet, EquipmentSlot.Boots, EquipmentSlot.Accessory };
            foreach (var slot in slots)
            {
                var slotElement = new VisualElement();
                slotElement.name = $"EquippedSlot_{slot}";
                slotElement.AddToClassList("equipped-slot");
                slotElement.style.height = Length.Percent(18);
                slotElement.userData = slot;

                var label = new Label("空");
                label.style.fontSize = 16;
                label.style.color = new Color(0.5f, 0.4f, 0.3f);
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.width = Length.Percent(100);
                label.style.height = Length.Percent(100);
                label.style.overflow = Overflow.Hidden;
                label.style.whiteSpace = WhiteSpace.Normal;
                slotElement.Add(label);

                slotElement.RegisterCallback<ClickEvent>(evt => OnEquippedSlotClicked(slot));

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
            }
            else
            {
                _selectedPartyIndex = Mathf.Clamp(_selectedPartyIndex, 0, activeIds.Count - 1);
                _currentCharacter = _currentState.Roster.FirstOrDefault(c => c.Id == activeIds[_selectedPartyIndex]);
            }
        }

        private void SetupCharacterSwitchButtons()
        {
            if (_characterSwitchButtons == null || _currentState == null) return;

            _characterSwitchButtons.Clear();
            var activeIds = _currentState.ActivePartyCharacterIds ?? new List<string>();

            for (int i = 0; i < activeIds.Count; i++)
            {
                int index = i;
                var character = _currentState.Roster.FirstOrDefault(c => c.Id == activeIds[i]);
                string displayName = character?.DisplayName ?? $"角色{i + 1}";

                var btn = new Button();
                btn.name = $"CharBtn_{i}";
                btn.text = displayName;
                btn.AddToClassList("character-switch-btn");
                if (i == _selectedPartyIndex)
                    btn.AddToClassList("active");

                btn.clicked += () => OnCharacterSwitchClicked(index);
                _characterSwitchButtons.Add(btn);
            }
        }

        private void OnCharacterSwitchClicked(int index)
        {
            _selectedPartyIndex = index;
            LoadCharacterData();
            SetupCharacterSwitchButtons();
            RefreshAll();
        }

        private void RefreshAll()
        {
            RefreshCharacterInfo();
            RefreshEquippedSlots();
            RefreshStorage();
            RefreshSkillSlots();
            RefreshPortrait();
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

            int maxHp = 100 + _currentCharacter.GetTotalConstitution() * 5;
            if (_hpLabel != null)
                _hpLabel.text = $"{maxHp}/{maxHp}";
            if (_hpBarFill != null)
                _hpBarFill.style.width = Length.Percent(100);

            SetStatValue(_strengthValue, _currentCharacter.Strength, _currentCharacter.GetTotalStrength());
            SetStatValue(_agilityValue, _currentCharacter.Agility, _currentCharacter.GetTotalAgility());
            SetStatValue(_constitutionValue, _currentCharacter.Constitution, _currentCharacter.GetTotalConstitution());
            SetStatValue(_intelligenceValue, _currentCharacter.Intelligence, _currentCharacter.GetTotalIntelligence());
            SetStatValue(_charismaValue, _currentCharacter.Charisma, _currentCharacter.GetTotalCharisma());
            SetStatValue(_luckValue, _currentCharacter.Luck, _currentCharacter.GetTotalLuck());
        }

        private void SetStatValue(Label label, int baseValue, int totalValue)
        {
            if (label == null) return;

            label.text = totalValue.ToString();

            if (totalValue > baseValue)
                label.style.color = new Color(0.2f, 0.8f, 0.2f);
            else if (totalValue < baseValue)
                label.style.color = new Color(0.8f, 0.2f, 0.2f);
            else
                label.style.color = new Color(0.37f, 0.31f, 0.25f);
        }

        private void SetDefaultCharacterInfo()
        {
            if (_characterNameLabel != null) _characterNameLabel.text = "未选择角色";
            if (_levelLabel != null) _levelLabel.text = "Lv.--";
            if (_hpLabel != null) _hpLabel.text = "--/--";
            if (_hpBarFill != null) _hpBarFill.style.width = Length.Percent(0);

            if (_strengthValue != null) { _strengthValue.text = "--"; _strengthValue.style.color = new Color(0.37f, 0.31f, 0.25f); }
            if (_agilityValue != null) { _agilityValue.text = "--"; _agilityValue.style.color = new Color(0.37f, 0.31f, 0.25f); }
            if (_constitutionValue != null) { _constitutionValue.text = "--"; _constitutionValue.style.color = new Color(0.37f, 0.31f, 0.25f); }
            if (_intelligenceValue != null) { _intelligenceValue.text = "--"; _intelligenceValue.style.color = new Color(0.37f, 0.31f, 0.25f); }
            if (_charismaValue != null) { _charismaValue.text = "--"; _charismaValue.style.color = new Color(0.37f, 0.31f, 0.25f); }
            if (_luckValue != null) { _luckValue.text = "--"; _luckValue.style.color = new Color(0.37f, 0.31f, 0.25f); }
        }

        private void RefreshEquippedSlots()
        {
            if (_currentCharacter == null) return;

            foreach (var kvp in _equippedSlotLabels)
            {
                var slot = kvp.Key;
                var label = kvp.Value;

                if (_currentCharacter.Equipment.TryGetValue(slot, out string equipmentId) && !string.IsNullOrEmpty(equipmentId))
                {
                    var def = EquipmentDatabase.GetById(equipmentId);
                    label.text = def?.DisplayName ?? equipmentId;
                    label.style.color = new Color(0.2f, 0.15f, 0.1f);
                }
                else
                {
                    label.text = "空";
                    label.style.color = new Color(0.5f, 0.4f, 0.3f);
                }
            }
        }

        private void RefreshStorage()
        {
            if (_storageGrid == null || _currentState == null) return;

            _storageGrid.Clear();
            _storageSlotElements.Clear();
            _storageSlotLabels.Clear();
            _storageSlotEquipmentIds.Clear();

            var inventory = _currentState.Inventory ?? new List<string>();
            int totalSlots = Mathf.Max(DefaultCapacity, GetNextMultipleOf(inventory.Count, SlotsPerRow));

            for (int i = 0; i < totalSlots; i++)
            {
                int rowIndex = i / SlotsPerRow;
                int colIndex = i % SlotsPerRow;

                if (colIndex == 0 && i > 0)
                {
                }

                string equipmentId = i < inventory.Count ? inventory[i] : null;

                var slotElement = new VisualElement();
                slotElement.name = $"StorageSlot_{i}";
                slotElement.AddToClassList("storage-slot-4col");
                slotElement.userData = i;
                if (i % 4 != 3)
                    slotElement.style.marginRight = Length.Percent(4);

                var label = new Label(string.IsNullOrEmpty(equipmentId) ? "空" : GetEquipmentDisplayName(equipmentId));
                label.style.fontSize = 16;
                label.style.color = string.IsNullOrEmpty(equipmentId) ? new Color(0.5f, 0.4f, 0.3f) : new Color(0.2f, 0.15f, 0.1f);
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.width = Length.Percent(100);
                label.style.height = Length.Percent(100);
                label.style.overflow = Overflow.Hidden;
                label.style.whiteSpace = WhiteSpace.Normal;
                slotElement.Add(label);

                int slotIndex = i;
                slotElement.RegisterCallback<ClickEvent>(evt => OnStorageSlotClicked(slotIndex));

                _storageSlotElements.Add(slotElement);
                _storageSlotLabels.Add(label);
                _storageSlotEquipmentIds.Add(equipmentId);
                _storageGrid.Add(slotElement);
            }
        }

        private int GetNextMultipleOf(int value, int multiple)
        {
            if (value <= 0) return multiple;
            return ((value + multiple - 1) / multiple) * multiple;
        }

        private string GetEquipmentDisplayName(string equipmentId)
        {
            var def = EquipmentDatabase.GetById(equipmentId);
            return def?.DisplayName ?? equipmentId;
        }

        private void OnStorageSlotClicked(int slotIndex)
        {
            if (_currentState == null || _currentCharacter == null) return;
            if (slotIndex >= _storageSlotEquipmentIds.Count) return;

            string equipmentId = _storageSlotEquipmentIds[slotIndex];
            if (string.IsNullOrEmpty(equipmentId)) return;

            long currentTime = GetCurrentTimeMs();
            string slotId = $"storage_{slotIndex}";

            if (_lastClickedSlotId == slotId && (currentTime - _lastClickTime) < DoubleClickThresholdMs)
            {
                EquipItem(equipmentId, slotIndex);
                _lastClickedSlotId = null;
                _lastClickTime = 0;
            }
            else
            {
                _lastClickedSlotId = slotId;
                _lastClickTime = currentTime;
            }
        }

        private void OnEquippedSlotClicked(EquipmentSlot slot)
        {
            if (_currentState == null || _currentCharacter == null) return;

            long currentTime = GetCurrentTimeMs();
            string slotId = $"equipped_{slot}";

            if (_lastClickedSlotId == slotId && (currentTime - _lastClickTime) < DoubleClickThresholdMs)
            {
                UnequipItem(slot);
                _lastClickedSlotId = null;
                _lastClickTime = 0;
            }
            else
            {
                _lastClickedSlotId = slotId;
                _lastClickTime = currentTime;
            }
        }

        private void EquipItem(string equipmentId, int inventoryIndex)
        {
            if (_currentState == null || _currentCharacter == null) return;

            var equipmentDef = EquipmentDatabase.GetById(equipmentId);
            if (equipmentDef == null) return;

            EquipmentSlot targetSlot = equipmentDef.Slot;

            if (_currentCharacter.Equipment.TryGetValue(targetSlot, out string existingId) && !string.IsNullOrEmpty(existingId))
            {
                TLog.Info($"[InventoryUIController] 槽位 {targetSlot} 已被占用，请先卸下");
                return;
            }

            if (inventoryIndex < 0 || inventoryIndex >= _currentState.Inventory.Count) return;
            if (_currentState.Inventory[inventoryIndex] != equipmentId) return;

            _currentState.Inventory.RemoveAt(inventoryIndex);
            _currentCharacter.Equipment[targetSlot] = equipmentId;

            PlayerAdventureStateStore.Save(_currentState);
            RefreshAll();
        }

        private void UnequipItem(EquipmentSlot slot)
        {
            if (_currentState == null || _currentCharacter == null) return;

            if (!_currentCharacter.Equipment.TryGetValue(slot, out string equipmentId) || string.IsNullOrEmpty(equipmentId))
                return;

            _currentCharacter.Equipment[slot] = null;
            _currentState.Inventory.Add(equipmentId);

            PlayerAdventureStateStore.Save(_currentState);
            RefreshAll();
        }

        private void RefreshSkillSlots()
        {
            if (_skillSlotsParent == null) return;

            foreach (var child in _skillSlotsParent.Children())
            {
                var slot = child;
                slot.Clear();

                var label = new Label("空");
                label.style.fontSize = 16;
                label.style.color = new Color(0.5f, 0.4f, 0.3f);
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.width = Length.Percent(100);
                label.style.height = Length.Percent(100);
                slot.Add(label);
            }
        }

        private void RefreshPortrait()
        {
            if (_portraitContainer == null) return;

            _portraitContainer.Clear();

            if (_currentCharacter == null)
            {
                var placeholder = new Label("[立绘]");
                placeholder.AddToClassList("portrait-placeholder");
                _portraitContainer.Add(placeholder);
                return;
            }

            var resolvedPath = CharacterDefinition.ResolvePrefabPath(_currentCharacter.PrefabPath);
            if (string.IsNullOrEmpty(resolvedPath))
            {
                var fallback = new Label("[无立绘]");
                fallback.AddToClassList("portrait-placeholder");
                _portraitContainer.Add(fallback);
                return;
            }

            var mgr = GameAssetManager.Instance;
            if (mgr == null)
                return;

            var prefab = mgr.Load<GameObject>(resolvedPath);
            if (prefab != null)
            {
                var spriteTransform = prefab.transform.Find("Sprite");
                if (spriteTransform != null)
                {
                    var renderer = spriteTransform.GetComponent<SpriteRenderer>();
                    if (renderer != null && renderer.sprite != null)
                    {
                        var image = new Image();
                        image.sprite = renderer.sprite;
                        image.scaleMode = ScaleMode.ScaleToFit;
                        image.style.width = Length.Percent(100);
                        image.style.height = Length.Percent(100);
                        _portraitContainer.Add(image);
                    }
                }

                mgr.Release(resolvedPath);
            }
        }

        private void RegisterKeyEvents()
        {
            if (_root != null)
                _root.RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        private void UnregisterKeyEvents()
        {
            if (_root != null)
                _root.UnregisterCallback<KeyDownEvent>(OnKeyDown);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                OnCloseClicked();
                evt.StopPropagation();
            }
        }

        private void OnCloseClicked()
        {
            Ui.Hide(UIManager.UIId.Inventory);
        }

        private static long GetCurrentTimeMs()
        {
            return System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
