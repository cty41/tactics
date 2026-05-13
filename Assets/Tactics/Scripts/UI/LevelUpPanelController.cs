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
            AttributeType.Intelligence, AttributeType.Charisma, AttributeType.Luck
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
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 4;

                var nameLabel = new Label(AttributePointSystem.GetAttributeDisplayName(type));
                nameLabel.style.width = 40;
                nameLabel.style.color = Color.white;
                nameLabel.style.fontSize = 14;

                var valueLabel = new Label("0");
                valueLabel.style.width = 30;
                valueLabel.style.color = Color.white;
                valueLabel.style.fontSize = 14;
                valueLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

                var minusBtn = new Button { text = "-" };
                minusBtn.style.width = 25;
                minusBtn.style.height = 25;
                minusBtn.style.fontSize = 14;
                minusBtn.SetEnabled(false);
                minusBtn.clicked += () => OnAttributeMinus(type);

                var allocatedLabel = new Label("+0");
                allocatedLabel.style.width = 30;
                allocatedLabel.style.color = new Color(0.4f, 1f, 0.4f);
                allocatedLabel.style.fontSize = 14;
                allocatedLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

                var plusBtn = new Button { text = "+" };
                plusBtn.style.width = 25;
                plusBtn.style.height = 25;
                plusBtn.style.fontSize = 14;
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
                AttributeType.Strength => _currentCharacter.Strength - (allocated * 2),
                AttributeType.Agility => _currentCharacter.Agility - allocated,
                AttributeType.Intelligence => _currentCharacter.Intelligence - (allocated * 2),
                AttributeType.Constitution => _currentCharacter.Constitution - (allocated * 10),
                AttributeType.Charisma => _currentCharacter.Charisma - allocated,
                AttributeType.Luck => _currentCharacter.Luck - (allocated * 2),
                _ => 0,
            };
        }

        private static int GetPerPointBonus(AttributeType type) => type switch
        {
            AttributeType.Strength => 2,
            AttributeType.Agility => 1,
            AttributeType.Intelligence => 2,
            AttributeType.Constitution => 10,
            AttributeType.Charisma => 1,
            AttributeType.Luck => 2,
            _ => 0,
        };

        private void RefreshDerivedStats()
        {
            if (_currentCharacter == null || _derivedStats == null) return;
            _derivedStats.Clear();

            var title = new Label("派生属性");
            title.style.fontSize = 14;
            title.style.color = new Color(0.6f, 0.6f, 1f);
            title.style.marginBottom = 4;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _derivedStats.Add(title);

            int str = _currentCharacter.GetTotalStrength();
            int agi = _currentCharacter.GetTotalAgility();
            int con = _currentCharacter.GetTotalConstitution();
            int intel = _currentCharacter.GetTotalIntelligence();
            int cha = _currentCharacter.GetTotalCharisma();
            int luck = _currentCharacter.GetTotalLuck();

            AddDerivedStat("物理攻击", $"{str * 2}");
            AddDerivedStat("魔法攻击", $"{intel * 2}");
            AddDerivedStat("生命上限", $"{con * 10}");
            AddDerivedStat("法力上限", $"{cha * 10}");
            AddDerivedStat("速度", $"{_currentCharacter.Speed:F1}");
            AddDerivedStat("物理防御", $"{_currentCharacter.DefenceFactor}");
            AddDerivedStat("魔法防御", $"{cha}");
            AddDerivedStat("闪避", $"{agi * 2}%");
            AddDerivedStat("状态抗性", $"{cha * 2}%");
        }

        private void AddDerivedStat(string name, string value)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginBottom = 2;

            var nameLabel = new Label(name);
            nameLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            nameLabel.style.fontSize = 12;

            var valueLabel = new Label(value);
            valueLabel.style.color = Color.white;
            valueLabel.style.fontSize = 12;

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
                card.style.flexDirection = FlexDirection.Column;
                card.style.paddingTop = 8;
                card.style.paddingBottom = 8;
                card.style.paddingLeft = 10;
                card.style.paddingRight = 10;
                card.style.marginBottom = 6;
                card.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
                card.style.borderTopLeftRadius = 4;
                card.style.borderTopRightRadius = 4;
                card.style.borderBottomLeftRadius = 4;
                card.style.borderBottomRightRadius = 4;
                card.style.borderLeftWidth = 3;
                card.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f);

                var nameRow = new VisualElement();
                nameRow.style.flexDirection = FlexDirection.Row;
                nameRow.style.justifyContent = Justify.SpaceBetween;

                var skillName = new Label(skill.DisplayName);
                skillName.style.fontSize = 14;
                skillName.style.color = Color.white;
                skillName.style.unityFontStyleAndWeight = FontStyle.Bold;

                var typeLabel = new Label(skill.SkillType == SkillType.Active ? "主动" : "被动");
                typeLabel.style.fontSize = 11;
                typeLabel.style.color = new Color(0.5f, 0.8f, 1f);
                typeLabel.style.paddingLeft = 6;
                typeLabel.style.paddingRight = 6;
                typeLabel.style.paddingTop = 2;
                typeLabel.style.paddingBottom = 2;
                typeLabel.style.backgroundColor = new Color(0.1f, 0.2f, 0.3f);
                typeLabel.style.borderTopLeftRadius = 3;
                typeLabel.style.borderTopRightRadius = 3;
                typeLabel.style.borderBottomLeftRadius = 3;
                typeLabel.style.borderBottomRightRadius = 3;

                nameRow.Add(skillName);
                nameRow.Add(typeLabel);

                var descLabel = new Label(skill.Description);
                descLabel.style.fontSize = 11;
                descLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
                descLabel.style.marginTop = 2;
                descLabel.style.whiteSpace = WhiteSpace.Normal;

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
                bool selected = kvp.Key == skillId;
                kvp.Value.style.borderLeftColor = selected
                    ? new Color(0.4f, 1f, 0.4f)
                    : new Color(0.3f, 0.3f, 0.3f);
                kvp.Value.style.backgroundColor = selected
                    ? new Color(0.1f, 0.25f, 0.1f)
                    : new Color(0.15f, 0.15f, 0.15f);
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
