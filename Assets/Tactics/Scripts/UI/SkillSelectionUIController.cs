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
    /// 技能选择 UI 控制器。升级时弹出，从 3 个随机技能中选择 1 个。
    /// 支持替换模式（槽位满时）与学习模式。
    ///
    /// 使用方式：
    /// - 挂载到含有 UIDocument 的 GameObject 上，UXML 会自动绑定
    /// </summary>
    public sealed class SkillSelectionUIController : UIControllerBase
    {
        private VisualElement _root;
        private Label _titleLabel;
        private Label _subtitleLabel;
        private VisualElement _skillsContainer;
        private VisualElement _replaceSection;
        private VisualElement _replaceOptionsContainer;
        private Button _confirmButton;
        private VisualElement _currentSkillsContainer;
        private VisualElement _currentSkillsList;

        private readonly List<VisualElement> _skillOptionElements = new List<VisualElement>();
        private readonly Dictionary<VisualElement, EventCallback<ClickEvent>> _optionClickCallbacks = new();
        private readonly List<SkillDefinition> _currentSkills = new List<SkillDefinition>();
        private CharacterDefinition _currentCharacter;
        private int _selectedIndex = -1;
        private bool _isReplaceMode;
        private int _lastReplaceIndex = -1;

        /// <summary>技能确认事件。参数: skillId, replaceIndex(null=学习, int=替换索引)。</summary>
        public event System.Action<string, int?> OnSkillConfirmed;

        protected override void OnShown()
        {
            base.OnShown();
            EnsureUIElements();
        }

        protected override void OnHidden()
        {
            base.OnHidden();
            UnregisterEvents();
            ClearUIElementReferences();
            _currentSkills.Clear();
            _selectedIndex = -1;
            _isReplaceMode = false;
            _lastReplaceIndex = -1;
        }

        private void EnsureUIElements()
        {
            var currentRoot = Ui.GetRootElement(UIManager.UIId.SkillSelection);
            if (ReferenceEquals(_root, currentRoot) && _root != null) return;

            UnregisterEvents();
            ClearUIElementReferences();
            _root = currentRoot;
            if (_root == null) return;
            CacheElements();
        }

        /// <summary>
        /// 手动设置根元素（用于自定义挂载场景）。
        /// </summary>
        public void SetRootElement(VisualElement root)
        {
            UnregisterEvents();
            ClearUIElementReferences();
            _root = root;
            CacheElements();
            _root.style.display = DisplayStyle.Flex;
        }

        private void CacheElements()
        {
            if (_root == null) return;

            _titleLabel = _root.Q<Label>("TitleLabel");
            _subtitleLabel = _root.Q<Label>("SubtitleLabel");
            _skillsContainer = _root.Q<VisualElement>("SkillsContainer");
            _replaceSection = _root.Q<VisualElement>("ReplaceSection");
            _replaceOptionsContainer = _root.Q<VisualElement>("ReplaceOptions");
            _confirmButton = _root.Q<Button>("ConfirmButton");
            _currentSkillsContainer = _root.Q<VisualElement>("CurrentSkillsContainer");
            _currentSkillsList = _root.Q<VisualElement>("CurrentSkills");

            for (int i = 0; i < 3; i++)
            {
                var option = _root.Q<VisualElement>($"SkillOption_{i}");
                if (option != null)
                {
                    _skillOptionElements.Add(option);
                    int capturedIndex = i;
                    EventCallback<ClickEvent> callback = _ => OnSkillSelected(capturedIndex);
                    _optionClickCallbacks[option] = callback;
                    option.RegisterCallback(callback);
                }
            }

            if (_confirmButton != null)
                _confirmButton.clicked += OnConfirm;

            UpdateConfirmButton();
        }

        private void UnregisterEvents()
        {
            foreach (var pair in _optionClickCallbacks)
                pair.Key.UnregisterCallback(pair.Value);

            _optionClickCallbacks.Clear();
            if (_confirmButton != null)
                _confirmButton.clicked -= OnConfirm;
        }

        private void ClearUIElementReferences()
        {
            _root = null;
            _titleLabel = null;
            _subtitleLabel = null;
            _skillsContainer = null;
            _replaceSection = null;
            _replaceOptionsContainer = null;
            _confirmButton = null;
            _currentSkillsContainer = null;
            _currentSkillsList = null;
            _skillOptionElements.Clear();
        }

        /// <summary>
        /// 设置当前角色（用于检查技能槽位状态）。
        /// </summary>
        public void SetCharacter(CharacterDefinition character)
        {
            _currentCharacter = character;
            BuildCurrentSkills();
        }

        /// <summary>
        /// 设置 3 个可选的技能定义，刷新 UI。
        /// </summary>
        public void SetSkillOptions(List<SkillDefinition> skills)
        {
            _currentSkills.Clear();
            _selectedIndex = -1;
            _isReplaceMode = false;
            _lastReplaceIndex = -1;

            if (skills == null || skills.Count < 3)
            {
                TLog.Warning("[SkillSelectionUIController] SetSkillOptions requires at least 3 skills.");
                return;
            }

            _currentSkills.AddRange(skills);

            for (int i = 0; i < 3 && i < _skillOptionElements.Count; i++)
            {
                var option = _skillOptionElements[i];
                if (option == null) continue;

                var skill = skills[i];
                var nameLabel = option.Q<Label>($"SkillName_{i}");
                var typeLabel = option.Q<Label>($"SkillType_{i}");
                var levelLabel = option.Q<Label>($"SkillLevel_{i}");
                var descLabel = option.Q<Label>($"SkillDesc_{i}");

                if (nameLabel != null) nameLabel.text = skill.DisplayName ?? skill.Id;
                if (typeLabel != null) typeLabel.text = skill.SkillType == SkillType.Active ? "主动" : "被动";
                if (levelLabel != null) levelLabel.text = $"Lv.{skill.Level}";
                if (descLabel != null) descLabel.text = BuildSkillDescription(skill, skill.Level);

                option.RemoveFromClassList("selected");
                option.style.display = DisplayStyle.Flex;
            }

            // 重置替换区域
            if (_replaceSection != null)
                _replaceSection.style.display = DisplayStyle.None;
            if (_replaceOptionsContainer != null)
                _replaceOptionsContainer.Clear();

            // 检查是否需要替换模式
            if (_currentCharacter != null && _currentSkills.Count > 0)
            {
                var skillType = _currentSkills[0].SkillType;
                var slotStatus = SkillSystem.GetSkillSlotStatus(_currentCharacter, skillType);
                _isReplaceMode = slotStatus.Remaining <= 0;

                if (_isReplaceMode)
                {
                    TLog.Info("[SkillSelectionUIController] Skill slots full, entering replace mode.");
                }
            }

            UpdateConfirmButton();
        }

        private void BuildCurrentSkills()
        {
            if (_currentSkillsContainer == null || _currentSkillsList == null)
                return;

            _currentSkillsList.Clear();
            var learnedSkills = _currentCharacter?.LearnedSkills?
                .Where(learned => learned != null && learned.SkillType != SkillType.ExtraUtility)
                .ToList() ?? new List<CharacterDefinition.LearnedSkill>();
            _currentSkillsContainer.style.display = learnedSkills.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            foreach (var learned in learnedSkills)
            {
                var skill = PureRunAbilityCatalog.TryGet(learned.SkillId, out var definition)
                    ? definition.Skill
                    : SkillDatabase.GetSkillById(learned.SkillId);
                if (skill == null) continue;

                var entry = new Label($"{skill.DisplayName} Lv.{learned.Level}\n{BuildSkillDescription(skill, learned.Level)}");
                entry.name = $"CurrentSkill_{learned.SkillId}";
                entry.AddToClassList("replace-option");
                _currentSkillsList.Add(entry);
            }
        }

        private static string BuildSkillDescription(SkillDefinition skill, int level)
        {
            string description = skill?.Description ?? string.Empty;
            int manaCost = skill?.MpCost ?? 0;
            if (skill != null && PureRunAbilityCatalog.TryResolveAbilityPath(skill.Id, level, out string path, out _) &&
                GameAssetManager.Instance?.IsInitialized == true)
            {
                var config = GameAssetManager.Instance.Load<AbilityConfig>(path);
                if (config != null)
                {
                    description = string.IsNullOrWhiteSpace(config.Description) ? description : config.Description;
                    manaCost = config.ManaCost;
                }
                GameAssetManager.Instance.Release(path);
            }
            return $"{description}\n消耗：{manaCost} MP";
        }

        /// <summary>
        /// 显示替换选项（当技能槽位已满时）。
        /// </summary>
        public void ShowReplaceOptions(List<CharacterDefinition.LearnedSkill> existingSkills)
        {
            if (_replaceSection == null || _replaceOptionsContainer == null) return;

            _replaceSection.style.display = DisplayStyle.Flex;
            _replaceOptionsContainer.Clear();

            for (int i = 0; i < existingSkills.Count; i++)
            {
                int capturedIndex = i;
                var skill = existingSkills[i];

                var replaceBtn = new Button();
                replaceBtn.name = $"ReplaceOption_{i}";
                replaceBtn.text = $"{skill.SkillId} (Lv.{skill.Level})";
                replaceBtn.AddToClassList("replace-option");
                replaceBtn.userData = capturedIndex;
                replaceBtn.clicked += () => OnReplaceSkillSelected(capturedIndex);

                _replaceOptionsContainer.Add(replaceBtn);
            }

            _isReplaceMode = true;
        }

        /// <summary>
        /// 选择技能（高亮显示）。
        /// </summary>
        public void OnSkillSelected(int index)
        {
            if (index < 0 || index >= _skillOptionElements.Count) return;

            // 取消旧选择
            if (_selectedIndex >= 0 && _selectedIndex < _skillOptionElements.Count)
            {
                _skillOptionElements[_selectedIndex].RemoveFromClassList("selected");
            }

            // 应用新选择
            _selectedIndex = index;
            _skillOptionElements[index].AddToClassList("selected");

            // 替换模式：选择了新技能后，显示已有技能替换选项
            if (_isReplaceMode && _currentCharacter != null && _selectedIndex >= 0 && _selectedIndex < _currentSkills.Count)
            {
                var selectedSkill = _currentSkills[_selectedIndex];
                var skillsOfType = SkillSystem.GetSkillsOfType(_currentCharacter, selectedSkill.SkillType);
                ShowReplaceOptions(skillsOfType);
            }

            UpdateConfirmButton();
        }

        /// <summary>
        /// 在替换选项中选择了要替换的已有技能。
        /// </summary>
        private void OnReplaceSkillSelected(int replaceIndex)
        {
            if (_replaceOptionsContainer != null)
            {
                var buttons = _replaceOptionsContainer.Query<Button>().ToList();
                foreach (var btn in buttons)
                {
                    btn.RemoveFromClassList("selected");
                }
                if (replaceIndex >= 0 && replaceIndex < buttons.Count)
                {
                    buttons[replaceIndex].AddToClassList("selected");
                }
            }

            _lastReplaceIndex = replaceIndex;
        }

        /// <summary>
        /// 确认选择，触发 OnSkillConfirmed 事件。
        /// </summary>
        public void OnConfirm()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _currentSkills.Count)
            {
                TLog.Warning("[SkillSelectionUIController] No skill selected for confirmation.");
                return;
            }

            var selectedSkill = _currentSkills[_selectedIndex];
            int? replaceIndex = _isReplaceMode ? (int?)_lastReplaceIndex : null;

            TLog.Info($"[SkillSelectionUIController] Skill confirmed: {selectedSkill.Id} (replaceIndex={replaceIndex})");
            OnSkillConfirmed?.Invoke(selectedSkill.Id, replaceIndex);
        }

        private void UpdateConfirmButton()
        {
            if (_confirmButton == null) return;

            bool canConfirm = _selectedIndex >= 0;
            _confirmButton.SetEnabled(canConfirm);
        }
    }
}
