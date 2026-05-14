using System;
using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Battle;
using Tactics.Roster;
using Tactics.Runtime.Utilities;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.UI
{
    /// <summary>
    /// Unified level-up panel: attribute allocation (left) + skill selection (right).
    /// </summary>
    public sealed class LevelUpPanelController : UIControllerBase
    {
        private VisualElement _root;
        private Label _characterNameLabel;
        private Label _levelLabel;
        private Label _pointsRemainingLabel;
        private ScrollView _attributeRows;
        private VisualElement _derivedStats;
        private ScrollView _skillList;
        private Button _confirmButton;
        private VisualElement _rightPanel;

        private CharacterDefinition _currentCharacter;
        private List<SkillDefinition> _skillOptions;
        private string _selectedSkillId;
        private int? _selectedReplaceIndex;

        private readonly Dictionary<AttributeType, AttributeRow> _rows = new();
        private readonly Dictionary<string, VisualElement> _skillCards = new();

        public event Action OnConfirm;

        private static readonly AttributeType[] AllTypes =
        {
            AttributeType.Strength, AttributeType.Agility, AttributeType.Constitution,
            AttributeType.Speed, AttributeType.Intelligence, AttributeType.Charisma, AttributeType.Luck
        };

        protected override void OnShown()
        {
            base.OnShown();
            EnsureUIElements();
            RegisterEvents();
            if (_currentCharacter != null)
                RefreshAll();
        }

        protected override void OnHidden()
        {
            UnregisterEvents();
        }

        private void EnsureUIElements()
        {
            if (_root != null) return;
            _root = Ui.GetRootElement(UIManager.UIId.LevelUp);
            if (_root == null) return;

            _characterNameLabel = _root.Q<Label>("CharacterNameLabel");
            _levelLabel = _root.Q<Label>("LevelLabel");
            _pointsRemainingLabel = _root.Q<Label>("PointsRemainingLabel");
            _attributeRows = _root.Q<ScrollView>("AttributeRows");
            _derivedStats = _root.Q<VisualElement>("DerivedStats");
            _skillList = _root.Q<ScrollView>("SkillList");
            _confirmButton = _root.Q<Button>("ConfirmButton");
            _rightPanel = _root.Q<VisualElement>("RightPanel");

            BuildAttributeRows();
        }

        private void BuildAttributeRows()
        {
            if (_attributeRows == null) return;
            _rows.Clear();

            foreach (var type in AllTypes)
            {
                var row = new VisualElement();
                row.AddToClassList("attr-row");

                var nameLabel = new Label(AttributePointSystem.GetAttributeDisplayName(type));
                nameLabel.AddToClassList("attr-name");

                var valueLabel = new Label("0");
                valueLabel.AddToClassList("attr-value");

                var minusBtn = new Button { text = "-" };
                minusBtn.AddToClassList("attr-btn");
                minusBtn.AddToClassList("attr-btn-minus");
                minusBtn.SetEnabled(false);
                minusBtn.clicked += () => OnAttributeMinus(type);

                var allocatedLabel = new Label("+0");
                allocatedLabel.AddToClassList("attr-allocated");

                var plusBtn = new Button { text = "+" };
                plusBtn.AddToClassList("attr-btn");
                plusBtn.AddToClassList("attr-btn-plus");
                plusBtn.clicked += () => OnAttributePlus(type);

                row.Add(nameLabel);
                row.Add(valueLabel);
                row.Add(minusBtn);
                row.Add(allocatedLabel);
                row.Add(plusBtn);

                _attributeRows.Add(row);
                _rows[type] = new AttributeRow
                {
                    ValueLabel = valueLabel,
                    MinusButton = minusBtn,
                    AllocatedLabel = allocatedLabel,
                    PlusButton = plusBtn,
                    RowElement = row
                };
            }
        }

        private void RegisterEvents()
        {
            if (_confirmButton != null)
                _confirmButton.clicked += OnConfirmClicked;
        }

        private void UnregisterEvents()
        {
            if (_confirmButton != null)
                _confirmButton.clicked -= OnConfirmClicked;
        }

        public void SetCharacter(CharacterDefinition character)
        {
            _currentCharacter = character;
            _skillOptions = null;
            _selectedSkillId = null;
            if (isActiveAndEnabled)
                RefreshAll();
        }

        public void SetSkillOptions(List<SkillDefinition> options)
        {
            _skillOptions = options;
            _selectedSkillId = null;
            BuildSkillCards();
            RefreshConfirmButton();
        }

        public void RefreshAll()
        {
            RefreshCharacterInfo();
            RefreshAttributeRows();
            RefreshDerivedStats();
            BuildSkillCards();
            RefreshConfirmButton();
        }

        private void RefreshCharacterInfo()
        {
            if (_currentCharacter == null) return;
            if (_characterNameLabel != null)
                _characterNameLabel.text = _currentCharacter.DisplayName;
            if (_levelLabel != null)
                _levelLabel.text = $"Lv.{_currentCharacter.Level}";
            if (_pointsRemainingLabel != null)
                _pointsRemainingLabel.text = $"剩余点数: {_currentCharacter.AttributePoints}";
        }

        private void RefreshAttributeRows()
        {
            if (_currentCharacter == null) return;
            foreach (var type in AllTypes)
            {
                if (!_rows.TryGetValue(type, out var row)) continue;
                
                int allocated = _currentCharacter.AllocatedAttributes?.GetValueOrDefault(type, 0) ?? 0;
                int baseValue = GetBaseAttribute(type);
                int effective = baseValue + allocated * GetPerPointBonus(type);

                row.ValueLabel.text = baseValue.ToString();
                row.AllocatedLabel.text = $"+{allocated}";
                row.PlusButton.SetEnabled(_currentCharacter.AttributePoints > 0);
                row.MinusButton.SetEnabled(allocated > 0);
            }
        }

        private int GetBaseAttribute(AttributeType type)
        {
            if (_currentCharacter == null) return 0;
            int allocated = _currentCharacter.AllocatedAttributes?.GetValueOrDefault(type, 0) ?? 0;
            return type switch
            {
                AttributeType.Strength => _currentCharacter.Strength - allocated,
                AttributeType.Agility => _currentCharacter.Agility - allocated,
                AttributeType.Intelligence => _currentCharacter.Intelligence - allocated,
                AttributeType.Constitution => _currentCharacter.Constitution - allocated,
                AttributeType.Charisma => _currentCharacter.Charisma - allocated,
                AttributeType.Luck => _currentCharacter.Luck - allocated,
                AttributeType.Speed => (int)_currentCharacter.Speed - allocated,
                _ => 0,
            };
        }

        private static int GetPerPointBonus(AttributeType type) => 1;

        private void RefreshDerivedStats()
        {
            if (_currentCharacter == null || _derivedStats == null) return;
            _derivedStats.Clear();

            var title = new Label("派生属性");
            title.AddToClassList("derived-title");
            _derivedStats.Add(title);

            int con = _currentCharacter.GetTotalConstitution();
            int cha = _currentCharacter.GetTotalCharisma();
            int agi = _currentCharacter.GetTotalAgility();
            int intel = _currentCharacter.GetTotalIntelligence();
            int luck = _currentCharacter.GetTotalLuck();
            float speed = _currentCharacter.Speed;

            AddDerivedStat("生命上限", $"{Mathf.Max(con * 4, 1)}");
            AddDerivedStat("法力上限", $"{cha * 3}");
            AddDerivedStat("先攻值", $"{Mathf.RoundToInt(speed * 2)}");
            AddDerivedStat("移动力", $"{Mathf.Max(1, Mathf.RoundToInt(speed))}");
            AddDerivedStat("法力恢复", $"{Mathf.Max(Mathf.FloorToInt(intel / 2f), 0)}");
            AddDerivedStat("战后恢复", $"{con * 2}");
            AddDerivedStat("闪避率", $"{agi * 2}%");
            AddDerivedStat("暴击率", $"{10 + (luck - 5) * 2}%");
        }

        private void AddDerivedStat(string name, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("derived-row");

            var nameLabel = new Label(name);
            nameLabel.AddToClassList("derived-name");

            var valueLabel = new Label(value);
            valueLabel.AddToClassList("derived-value");

            row.Add(nameLabel);
            row.Add(valueLabel);
            _derivedStats.Add(row);
        }

        private void BuildSkillCards()
        {
            if (_skillList == null) return;
            _skillList.Clear();
            _skillCards.Clear();

            bool hasSkills = _skillOptions != null && _skillOptions.Count > 0;

            if (_rightPanel != null)
                _rightPanel.style.display = hasSkills ? DisplayStyle.Flex : DisplayStyle.None;

            if (!hasSkills) return;

            foreach (var skill in _skillOptions)
            {
                var card = new VisualElement();
                card.AddToClassList("skill-card");

                var nameRow = new VisualElement();
                nameRow.AddToClassList("skill-card-header");

                var skillName = new Label(skill.DisplayName);
                skillName.AddToClassList("skill-name");

                var typeLabel = new Label(skill.SkillType == SkillType.Active ? "主动" : "被动");
                typeLabel.AddToClassList("skill-type-badge");

                nameRow.Add(skillName);
                nameRow.Add(typeLabel);

                var descLabel = new Label(skill.Description);
                descLabel.AddToClassList("skill-desc");

                card.Add(nameRow);
                card.Add(descLabel);

                card.RegisterCallback<ClickEvent>(evt =>
                {
                    SelectSkill(skill.Id);
                    evt.StopPropagation();
                });

                _skillList.Add(card);
                _skillCards[skill.Id] = card;
            }
        }

        private void SelectSkill(string skillId)
        {
            _selectedSkillId = skillId;
            // Update visual selection
            foreach (var kvp in _skillCards)
            {
                kvp.Value.RemoveFromClassList("selected");
                if (kvp.Key == skillId)
                    kvp.Value.AddToClassList("selected");
            }
            RefreshConfirmButton();
        }

        private void OnAttributePlus(AttributeType type)
        {
            if (_currentCharacter == null) return;
            if (AttributePointSystem.ApplyAttributePoint(_currentCharacter, type))
            {
                RefreshAttributeRows();
                RefreshDerivedStats();
                RefreshConfirmButton();
            }
        }

        private void OnAttributeMinus(AttributeType type)
        {
            if (_currentCharacter == null) return;
            int allocated = _currentCharacter.AllocatedAttributes?.GetValueOrDefault(type, 0) ?? 0;
            if (allocated <= 0) return;

            _currentCharacter.AllocatedAttributes[type] = allocated - 1;
            _currentCharacter.AttributePoints++;

            switch (type)
            {
                case AttributeType.Strength: _currentCharacter.Strength -= 2; break;
                case AttributeType.Agility: _currentCharacter.Agility -= 1; _currentCharacter.Speed -= 1f; break;
                case AttributeType.Intelligence: _currentCharacter.Intelligence -= 2; break;
                case AttributeType.Constitution: _currentCharacter.Constitution -= 10; _currentCharacter.DefenceFactor -= 1; break;
                case AttributeType.Charisma: _currentCharacter.Charisma -= 1; _currentCharacter.Luck -= 2; break;
                case AttributeType.Luck: _currentCharacter.Luck -= 2; break;
            }

            RefreshAttributeRows();
            RefreshDerivedStats();
            RefreshConfirmButton();
        }

        private void RefreshConfirmButton()
        {
            if (_confirmButton == null || _currentCharacter == null) return;
            bool pointsSpent = _currentCharacter.AttributePoints == 0;
            bool hasSkills = _skillOptions != null && _skillOptions.Count > 0;
            bool skillReady = !hasSkills || _selectedSkillId != null;
            _confirmButton.SetEnabled(pointsSpent && skillReady);
        }

        private void OnConfirmClicked()
        {
            if (_currentCharacter == null) return;
            if (_currentCharacter.AttributePoints > 0) return;
            if (_skillOptions != null && _skillOptions.Count > 0 && _selectedSkillId == null) return;

            TLog.Info($"[LevelUpPanel] Confirmed for {_currentCharacter.DisplayName}. Skill: {_selectedSkillId ?? "none"}");
            OnConfirm?.Invoke();
            Ui.Hide(UIManager.UIId.LevelUp);
        }

        private sealed class AttributeRow
        {
            public Label ValueLabel;
            public Button MinusButton;
            public Label AllocatedLabel;
            public Button PlusButton;
            public VisualElement RowElement;
        }
    }
}
