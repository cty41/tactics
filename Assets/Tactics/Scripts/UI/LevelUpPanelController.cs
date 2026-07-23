using System;
using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Battle;
using Tactics.AssetPipeline;
using Tactics.Common.Units.Abilities;
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
        private Func<List<SkillDefinition>> _skillOptionProvider;
        private string _selectedSkillId;
        private int? _selectedReplaceIndex;

        private readonly Dictionary<AttributeType, AttributeRow> _rows = new();
        private readonly Dictionary<string, VisualElement> _skillCards = new();

        public event Action OnConfirm;
        public IReadOnlyList<SkillDefinition> SkillOptions => _skillOptions ?? (IReadOnlyList<SkillDefinition>)Array.Empty<SkillDefinition>();

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
            ClearUIElementReferences();
        }

        private void EnsureUIElements()
        {
            var currentRoot = Ui.GetRootElement(UIManager.UIId.LevelUp);
            if (ReferenceEquals(_root, currentRoot) && _root != null)
                return;

            UnregisterEvents();
            ClearUIElementReferences();
            _root = currentRoot;
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
            _attributeRows.Clear();
            _rows.Clear();

            foreach (var type in AllTypes)
            {
                var row = new VisualElement();
                row.AddToClassList("attr-row");

                var nameLabel = new Label(AttributePointSystem.GetAttributeDisplayName(type));
                nameLabel.AddToClassList("attr-name");

                var valueLabel = new Label("0");
                valueLabel.AddToClassList("attr-value");

                var minusBtn = new Button { text = "-", name = $"AttributeMinus_{type}" };
                minusBtn.AddToClassList("attr-btn");
                minusBtn.AddToClassList("attr-btn-minus");
                minusBtn.SetEnabled(false);
                minusBtn.clicked += () => OnAttributeMinus(type);

                var allocatedLabel = new Label("+0");
                allocatedLabel.AddToClassList("attr-allocated");

                var plusBtn = new Button { text = "+", name = $"AttributePlus_{type}" };
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

        private void ClearUIElementReferences()
        {
            _root = null;
            _characterNameLabel = null;
            _levelLabel = null;
            _pointsRemainingLabel = null;
            _attributeRows = null;
            _derivedStats = null;
            _skillList = null;
            _confirmButton = null;
            _rightPanel = null;
            _rows.Clear();
            _skillCards.Clear();
        }

        public void SetCharacter(CharacterDefinition character)
        {
            _currentCharacter = character;
            _skillOptions = null;
            _skillOptionProvider = null;
            _selectedSkillId = null;
            if (isActiveAndEnabled)
                RefreshAll();
        }

        public void SetSkillOptions(List<SkillDefinition> options)
        {
            _skillOptionProvider = null;
            _skillOptions = options;
            _selectedSkillId = null;
            BuildSkillCards();
            RefreshConfirmButton();
        }

        /// <summary>
        /// Sets a provider so attribute changes can immediately rebuild the legal skill offer.
        /// </summary>
        public void SetSkillOptionProvider(Func<List<SkillDefinition>> provider)
        {
            _skillOptionProvider = provider;
            RefreshSkillOptionsFromProvider();
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
                var visibleSkills = _currentCharacter.LearnedSkills
                    .Where(IsMapVisibleSkill)
                    .Select(learned => (Learned: learned, Skill: ResolveSkill(learned.SkillId)))
                    .Where(entry => entry.Skill != null)
                    .ToList();
                var activeSkills = visibleSkills
                    .Where(entry => entry.Learned.SkillType == SkillType.Active).ToList();
                var passiveSkills = visibleSkills
                    .Where(entry => entry.Learned.SkillType == SkillType.Passive).ToList();

                if (activeSkills.Count > 0)
                {
                    var subTitle = new Label("主动");
                    subTitle.AddToClassList("skill-sub-title");
                    _skillList.Add(subTitle);

                    var grid = new VisualElement();
                    grid.AddToClassList("current-skills-grid");
                    foreach (var entry in activeSkills)
                        grid.Add(CreateCurrentSkillIcon(entry.Skill, entry.Learned.Level));
                    _skillList.Add(grid);
                }

                if (passiveSkills.Count > 0)
                {
                    var subTitle = new Label("被动");
                    subTitle.AddToClassList("skill-sub-title");
                    _skillList.Add(subTitle);

                    var grid = new VisualElement();
                    grid.AddToClassList("current-skills-grid");
                    foreach (var entry in passiveSkills)
                        grid.Add(CreateCurrentSkillIcon(entry.Skill, entry.Learned.Level));
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

                var title = new Label("选择技能或升级");
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

        private VisualElement CreateCurrentSkillIcon(SkillDefinition skill, int actualLevel)
        {
            var icon = new VisualElement { name = $"CurrentSkill_{ToElementKey(skill.Id)}" };
            icon.AddToClassList("current-skill-icon");
            icon.tooltip = ResolveLevelDescription(skill.Id, actualLevel, skill.Description);

            var nameLabel = new Label(skill.DisplayName);
            nameLabel.AddToClassList("current-skill-name");

            var levelBadge = new Label($"Lv.{Mathf.Max(1, actualLevel)}")
            {
                name = $"CurrentSkillLevel_{ToElementKey(skill.Id)}"
            };
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
            var card = new VisualElement { name = $"LevelUpSkillCard_{ToElementKey(skill.Id)}", userData = skill };
            card.AddToClassList("skill-card");
            card.tooltip = ResolveLevelDescription(skill.Id, skill.Level, skill.Description);

            // Level badge (top-right)
            var levelBadge = new Label($"Lv.{Mathf.Max(1, skill.Level)}")
            {
                name = $"LevelUpSkillLevel_{ToElementKey(skill.Id)}"
            };
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
            var descLabel = new Label(ResolveLevelDescription(skill.Id, skill.Level, skill.Description));
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
                RefreshSkillOptionsFromProvider();
                BuildSkillCards();
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
            RefreshSkillOptionsFromProvider();
            BuildSkillCards();
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

            if (!string.IsNullOrEmpty(_selectedSkillId))
            {
                var selectedSkill = _skillOptions?.FirstOrDefault(skill => skill.Id == _selectedSkillId);
                if (selectedSkill == null)
                    return;

                bool applied = SkillSystem.HasSkill(_currentCharacter, selectedSkill.Id)
                    ? SkillSystem.UpgradeSkill(_currentCharacter, selectedSkill.Id)
                    : SkillSystem.LearnSkill(_currentCharacter, selectedSkill, _selectedReplaceIndex);
                if (!applied)
                {
                    TLog.Warning($"[LevelUpPanel] Failed to apply selected skill {_selectedSkillId}.");
                    return;
                }
            }

            TLog.Info($"[LevelUpPanel] Confirmed for {_currentCharacter.DisplayName}. Skill: {_selectedSkillId ?? "none"}");
            OnConfirm?.Invoke();
            Ui.Hide(UIManager.UIId.LevelUp);
        }

        private void RefreshSkillOptionsFromProvider()
        {
            if (_skillOptionProvider == null)
                return;

            _skillOptions = _skillOptionProvider.Invoke() ?? new List<SkillDefinition>();
            _selectedSkillId = null;
            _selectedReplaceIndex = null;
        }

        private static bool IsMapVisibleSkill(CharacterDefinition.LearnedSkill learned)
        {
            if (learned == null || learned.SkillType == SkillType.ExtraUtility)
                return false;
            return !PureRunAbilityCatalog.TryGet(learned.SkillId, out var definition) || definition.IsMapVisible;
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

        private static string ToElementKey(string value) => string.IsNullOrWhiteSpace(value)
            ? "Unknown"
            : new string(value.Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());

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
