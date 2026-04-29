using System.Linq;
using Tactics.Roster;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.UI
{
    public sealed class InventoryUIController : UIControllerBase
    {
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

        private CharacterDefinition _currentCharacter;
        private int _selectedPartyIndex = 0;

        protected override void OnShown()
        {
            base.OnShown();
            EnsureUIElements();
            LoadCharacterData();
            RefreshAll();
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

            _equippedSlotsParent = _root.Q<VisualElement>("EquippedSlots");
            _storageGrid = _root.Q<VisualElement>("StorageGrid");
            _skillSlotsParent = _root.Q<VisualElement>("SkillSlots");

            var closeButton = _root.Q<Button>("CloseButton");
            if (closeButton != null)
                closeButton.clicked += OnCloseClicked;
        }

        private void LoadCharacterData()
        {
            var state = PlayerAdventureStateStore.Load();
            if (state?.Roster == null || state.ActivePartyCharacterIds == null)
            {
                _currentCharacter = null;
                return;
            }

            var activeIds = state.ActivePartyCharacterIds;
            if (activeIds.Count == 0)
            {
                _currentCharacter = state.Roster.FirstOrDefault();
            }
            else
            {
                _selectedPartyIndex = Mathf.Clamp(_selectedPartyIndex, 0, activeIds.Count - 1);
                _currentCharacter = state.Roster.FirstOrDefault(c => c.Id == activeIds[_selectedPartyIndex]);
            }
        }

        private void RefreshAll()
        {
            RefreshCharacterInfo();
            RefreshEquippedSlots();
            RefreshStorage();
            RefreshSkillSlots();
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

            int maxHp = 100 + _currentCharacter.Constitution * 5;
            if (_hpLabel != null)
                _hpLabel.text = $"{maxHp}/{maxHp}";
            if (_hpBarFill != null)
                _hpBarFill.style.width = Length.Percent(100);

            if (_strengthValue != null) _strengthValue.text = _currentCharacter.Strength.ToString();
            if (_agilityValue != null) _agilityValue.text = _currentCharacter.Agility.ToString();
            if (_constitutionValue != null) _constitutionValue.text = _currentCharacter.Constitution.ToString();
            if (_intelligenceValue != null) _intelligenceValue.text = _currentCharacter.Intelligence.ToString();
            if (_charismaValue != null) _charismaValue.text = _currentCharacter.Charisma.ToString();
            if (_luckValue != null) _luckValue.text = _currentCharacter.Luck.ToString();
        }

        private void SetDefaultCharacterInfo()
        {
            if (_characterNameLabel != null) _characterNameLabel.text = "未选择角色";
            if (_levelLabel != null) _levelLabel.text = "Lv.--";
            if (_hpLabel != null) _hpLabel.text = "--/--";
            if (_hpBarFill != null) _hpBarFill.style.width = Length.Percent(0);

            if (_strengthValue != null) _strengthValue.text = "--";
            if (_agilityValue != null) _agilityValue.text = "--";
            if (_constitutionValue != null) _constitutionValue.text = "--";
            if (_intelligenceValue != null) _intelligenceValue.text = "--";
            if (_charismaValue != null) _charismaValue.text = "--";
            if (_luckValue != null) _luckValue.text = "--";
        }

        private void RefreshEquippedSlots()
        {
            if (_equippedSlotsParent == null) return;

            foreach (var child in _equippedSlotsParent.Children())
            {
                var slot = child;
                slot.Clear();

                var label = new Label("空");
                label.style.fontSize = 10;
                label.style.color = new Color(0.5f, 0.4f, 0.3f);
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.width = Length.Percent(100);
                label.style.height = Length.Percent(100);
                slot.Add(label);
            }

            Debug.Log("[InventoryUIController] Equipped slots refreshed (placeholder)");
        }

        private void RefreshStorage()
        {
            if (_storageGrid == null) return;

            _storageGrid.Clear();

            int slotCount = 12;
            for (int i = 0; i < slotCount; i++)
            {
                var slot = new VisualElement();
                slot.name = $"StorageSlot_{i}";
                slot.AddToClassList("storage-slot");

                var label = new Label("空");
                label.style.fontSize = 10;
                label.style.color = new Color(0.5f, 0.4f, 0.3f);
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.width = Length.Percent(100);
                label.style.height = Length.Percent(100);
                slot.Add(label);

                _storageGrid.Add(slot);
            }

            Debug.Log("[InventoryUIController] Storage slots refreshed (placeholder)");
        }

        private void RefreshSkillSlots()
        {
            if (_skillSlotsParent == null) return;

            foreach (var child in _skillSlotsParent.Children())
            {
                var slot = child;
                slot.Clear();

                var label = new Label("空");
                label.style.fontSize = 10;
                label.style.color = new Color(0.5f, 0.4f, 0.3f);
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.width = Length.Percent(100);
                label.style.height = Length.Percent(100);
                slot.Add(label);
            }

            Debug.Log("[InventoryUIController] Skill slots refreshed (placeholder)");
        }

        private void OnCloseClicked()
        {
            Ui.Hide(UIManager.UIId.Inventory);
        }
    }
}
