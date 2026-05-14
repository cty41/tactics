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

            bool hasOptions = _skillOptions != null && _skillOptions.Count > 0;
            bool hasCurrentSkills = _currentCharacter?.LearnedSkills != null && _currentCharacter.LearnedSkills.Count > 0;

            if (_rightPanel != null)
                _rightPanel.style.display = (hasOptions || hasCurrentSkills) ? DisplayStyle.Flex : DisplayStyle.None;

            // Section: Current Skills (compact grid)
            if (hasCurrentSkills)
            {
                var sectionHeader = new VisualElement();
                sectionHeader.AddToClassList("skill-section-header");

                var icon = new Label("◆");
                icon.AddToClassList("section-header-icon");

                var title = new Label("当前技能");
                title.AddToClassList("section-header-title");

                sectionHeader.Add(icon);
                sectionHeader.Add(title);
                _skillList.Add(sectionHeader);

                // Separate active and passive
                var activeSkills = _currentCharacter.LearnedSkills
                    .Select(l => SkillDatabase.GetSkillById(l.SkillId))
                    .Where(s => s != null && s.SkillType == SkillType.Active).ToList();
                var passiveSkills = _currentCharacter.LearnedSkills
                    .Select(l => SkillDatabase.GetSkillById(l.SkillId))
                    .Where(s => s != null && s.SkillType == SkillType.Passive).ToList();

                if (activeSkills.Count > 0)
                {
                    var subTitle = new Label("主动");
                    subTitle.AddToClassList("skill-sub-title");
                    _skillList.Add(subTitle);

                    var grid = new VisualElement();
                    grid.AddToClassList("current-skills-grid");
                    foreach (var skill in activeSkills)
                        grid.Add(CreateCurrentSkillIcon(skill));
                    _skillList.Add(grid);
                }

                if (passiveSkills.Count > 0)
                {
                    var subTitle = new Label("被动");
                    subTitle.AddToClassList("skill-sub-title");
                    _skillList.Add(subTitle);

                    var grid = new VisualElement();
                    grid.AddToClassList("current-skills-grid");
                    foreach (var skill in passiveSkills)
                        grid.Add(CreateCurrentSkillIcon(skill));
                    _skillList.Add(grid);
                }
            }

            // Section: New Skill Options (cards)
            if (hasOptions)
            {
                var sectionHeader = new VisualElement();
                sectionHeader.AddToClassList("skill-section-header");

                var icon = new Label("★");
                icon.AddToClassList("section-header-icon");

                var title = new Label("选择新技能");
                title.AddToClassList("section-header-title");

                sectionHeader.Add(icon);
                sectionHeader.Add(title);
                _skillList.Add(sectionHeader);

                var cardGrid = new VisualElement();
                cardGrid.AddToClassList("skill-card-grid");

                foreach (var skill in _skillOptions)
                {
                    var card = CreateSkillCard(skill, true);
                    string skillId = skill.Id;
                    card.RegisterCallback<ClickEvent>(evt =>
                    {
                        SelectSkill(skillId);
                        evt.StopPropagation();
                    });
                    cardGrid.Add(card);
                    _skillCards[skill.Id] = card;
                }
                _skillList.Add(cardGrid);
            }
        }

        private VisualElement CreateCurrentSkillIcon(SkillDefinition skill)
        {
            var icon = new VisualElement();
            icon.AddToClassList("current-skill-icon");

            var nameLabel = new Label(skill.DisplayName);
            nameLabel.AddToClassList("current-skill-name");

            var levelBadge = new Label(skill.Level >= 2 ? "II" : "I");
            levelBadge.AddToClassList("skill-level-badge");

            var typeColor = new VisualElement();
            typeColor.AddToClassList(skill.SkillType == SkillType.Active ? "current-skill-active" : "current-skill-passive");

            icon.Add(typeColor);
            icon.Add(nameLabel);
            icon.Add(levelBadge);

            return icon;
        }

        private VisualElement CreateSkillCard(SkillDefinition skill, bool isSelectable)
        {
            var card = new VisualElement();
            card.AddToClassList("skill-card");

            // Level badge (top-right)
            var levelBadge = new Label(skill.Level >= 2 ? "II" : "I");
            levelBadge.AddToClassList("skill-level-badge");
            levelBadge.AddToClassList("skill-card-level");
            card.Add(levelBadge);

            // Type color strip (left border)
            if (skill.SkillType == SkillType.Active)
                card.AddToClassList("skill-card-active");
            else
                card.AddToClassList("skill-card-passive");

            // Name row: name + type badge
            var nameRow = new VisualElement();
            nameRow.AddToClassList("skill-card-header");

            var skillName = new Label(skill.DisplayName);
            skillName.AddToClassList("skill-name");

            var typeLabel = new Label(skill.SkillType == SkillType.Active ? "主动" : "被动");
            typeLabel.AddToClassList("skill-type-badge");

            nameRow.Add(skillName);
            nameRow.Add(typeLabel);
            card.Add(nameRow);

            // Description
            var descLabel = new Label(skill.Description);
            descLabel.AddToClassList("skill-desc");
            card.Add(descLabel);

            // Stats row: damage + MP cost
            var statsRow = new VisualElement();
            statsRow.AddToClassList("skill-stats-row");

            if (skill.SkillType == SkillType.Active && skill.DamageBase > 0)
            {
                int damage = SkillDatabase.CalculateSkillDamage(_currentCharacter, skill);

                var damageIcon = new Label("⚔");
                damageIcon.AddToClassList("skill-stat-icon");

                var damageLabel = new Label($"伤害 {damage}");
                damageLabel.AddToClassList("skill-stat-damage");

                var damageGroup = new VisualElement();
                damageGroup.AddToClassList("skill-stat-group");
                damageGroup.Add(damageIcon);
                damageGroup.Add(damageLabel);

                statsRow.Add(damageGroup);
            }

            if (skill.MpCost > 0)
            {
                var mpIcon = new Label("◆");
                mpIcon.AddToClassList("skill-stat-icon");

                var mpLabel = new Label($"MP {skill.MpCost}");
                mpLabel.AddToClassList("skill-stat-mp");

                var mpGroup = new VisualElement();
                mpGroup.AddToClassList("skill-stat-group");
                mpGroup.Add(mpIcon);
                mpGroup.Add(mpLabel);

                statsRow.Add(mpGroup);
            }

            card.Add(statsRow);

            return card;
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
                // 更新剩余点数显示
                if (_pointsRemainingLabel != null)
                    _pointsRemainingLabel.text = $"剩余点数: {_currentCharacter.AttributePoints}";
                RefreshDerivedStats();
                BuildSkillCards();
                // Re-apply skill selection (BuildSkillCards cleared the visual state)
                if (_selectedSkillId != null)
                    SelectSkill(_selectedSkillId);
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
                case AttributeType.Strength: _currentCharacter.Strength -= 1; break;
                case AttributeType.Agility: _currentCharacter.Agility -= 1; break;
                case AttributeType.Intelligence: _currentCharacter.Intelligence -= 1; break;
                case AttributeType.Constitution: _currentCharacter.Constitution -= 1; break;
                case AttributeType.Charisma: _currentCharacter.Charisma -= 1; break;
                case AttributeType.Luck: _currentCharacter.Luck -= 1; break;
                case AttributeType.Speed: _currentCharacter.Speed -= 1f; break;
            }

            RefreshAttributeRows();
            // 更新剩余点数显示
            if (_pointsRemainingLabel != null)
                _pointsRemainingLabel.text = $"剩余点数: {_currentCharacter.AttributePoints}";
            RefreshDerivedStats();
            BuildSkillCards();
            // Re-apply skill selection (BuildSkillCards cleared the visual state)
            if (_selectedSkillId != null)
                SelectSkill(_selectedSkillId);
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
