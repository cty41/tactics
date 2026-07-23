using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Battle;
using Tactics.Consumables;
using Tactics.Runtime.Utilities;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.UI
{
    public sealed class BattleSettlementUIController : UIControllerBase
    {
        private const float AnimDuration = 1.5f;
        private const int AnimSteps = 60;

        private VisualElement _root;
        private Label _resultLabel;
        private Label _roundsLabel;
        private Label _goldLabel;
        private Label _itemDropLabel;
        private VisualElement _experienceEntries;
        private Button _continueButton;

        private BattleRewardSystem.BattleRewards _rewards;
        private Dictionary<string, int> _currentCharacterExp;
        private bool _isAnimating;
        private Coroutine _animCoroutine;

        public event System.Action OnContinue;

        protected override void OnShown()
        {
            base.OnShown();
            EnsureUIElements();
            RegisterEvents();
        }

        protected override void OnHidden()
        {
            UnregisterEvents();
            if (_animCoroutine != null) { StopCoroutine(_animCoroutine); _animCoroutine = null; }
            _isAnimating = false;
            ClearUIElementReferences();
        }

        private void EnsureUIElements()
        {
            var currentRoot = Ui.GetRootElement(UIManager.UIId.BattleSettlement);
            if (ReferenceEquals(_root, currentRoot) && _root != null)
                return;

            UnregisterEvents();
            ClearUIElementReferences();
            _root = currentRoot;
            if (_root == null) return;

            _resultLabel = _root.Q<Label>("ResultLabel");
            _roundsLabel = _root.Q<Label>("RoundsLabel");
            _goldLabel = _root.Q<Label>("GoldLabel");
            _itemDropLabel = _root.Q<Label>("ItemDropLabel");
            if (_itemDropLabel == null && _goldLabel?.parent != null)
            {
                _itemDropLabel = new Label { name = "ItemDropLabel" };
                _itemDropLabel.style.color = new Color(0.9f, 0.78f, 0.42f);
                _itemDropLabel.style.fontSize = 16;
                _itemDropLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                _goldLabel.parent.Add(_itemDropLabel);
            }
            _experienceEntries = _root.Q<VisualElement>("ExperienceEntries");
            _continueButton = _root.Q<Button>("ContinueButton");
        }

        private void RegisterEvents()
        {
            if (_continueButton != null)
                _continueButton.clicked += OnContinueClicked;
            if (_root != null)
                _root.RegisterCallback<ClickEvent>(OnRootClicked);
        }

        private void UnregisterEvents()
        {
            if (_continueButton != null)
                _continueButton.clicked -= OnContinueClicked;
            if (_root != null)
                _root.UnregisterCallback<ClickEvent>(OnRootClicked);
        }

        private void ClearUIElementReferences()
        {
            _root = null;
            _resultLabel = null;
            _roundsLabel = null;
            _goldLabel = null;
            _itemDropLabel = null;
            _experienceEntries = null;
            _continueButton = null;
        }

        public void SetBattleResult(BattleRewardSystem.BattleRewards rewards, bool isVictory, Dictionary<string, int> characterLevels = null, Dictionary<string, int> currentCharacterExp = null)
        {
            EnsureUIElements();
            if (_root == null) return;

            _rewards = rewards;
            _currentCharacterExp = currentCharacterExp;

            if (_resultLabel != null)
            {
                _resultLabel.text = isVictory ? "胜利！" : "败北...";
                _resultLabel.RemoveFromClassList("victory-label");
                _resultLabel.RemoveFromClassList("defeat-label");
                _resultLabel.AddToClassList(isVictory ? "victory-label" : "defeat-label");
            }

            if (_roundsLabel != null)
                _roundsLabel.text = $"总回合数：{rewards.TotalRounds}";

            if (_goldLabel != null)
                _goldLabel.text = $"+{rewards.TotalGold}";

            if (_itemDropLabel != null)
            {
                var itemNames = rewards.ItemIds?
                    .Select(ConsumableDatabase.GetAcquisitionDisplayText)
                    .ToList();
                _itemDropLabel.text = itemNames != null && itemNames.Count > 0
                    ? string.Join("、", itemNames)
                    : string.Empty;
                _itemDropLabel.style.display = string.IsNullOrEmpty(_itemDropLabel.text)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }

            if (_continueButton != null)
                _continueButton.SetEnabled(false);

            if (_experienceEntries != null)
                _experienceEntries.Clear();

            foreach (var kvp in rewards.ExperiencePerCharacter)
            {
                int currentLevel = 1;
                if (characterLevels != null && characterLevels.TryGetValue(kvp.Key, out int lv))
                    currentLevel = lv;

                int currentExp = 0;
                if (_currentCharacterExp != null && _currentCharacterExp.TryGetValue(kvp.Key, out int exp))
                    currentExp = exp;

                int expToNext = ExperienceTable.GetExperienceToNextLevel(currentLevel);
                CreateCharacterExpEntry(kvp.Key, kvp.Value, currentLevel, expToNext, currentExp);
            }

            _isAnimating = true;
            _animCoroutine = StartCoroutine(PlayExpAnimation());
        }

        /// <summary>
        /// Maps total accumulated experience to current level and progress within that level.
        /// </summary>
        private (int level, int expInLevel, int expToNext) GetLevelProgress(int totalExp)
        {
            int remaining = totalExp;
            for (int level = 1; level <= ExperienceTable.GetMaxLevel(); level++)
            {
                int expToNext = ExperienceTable.GetExperienceToNextLevel(level);
                if (remaining < expToNext)
                    return (level, remaining, expToNext);
                remaining -= expToNext;
                if (level == ExperienceTable.GetMaxLevel())
                    return (level, expToNext, expToNext);
            }
            return (ExperienceTable.GetMaxLevel(), 0, 0);
        }

        private void CreateCharacterExpEntry(string characterName, int expGained, int currentLevel, int expToNext, int currentExp)
        {
            var entry = new VisualElement();
            entry.AddToClassList("character-exp-entry");

            var infoRow = new VisualElement();
            infoRow.AddToClassList("char-info-row");

            var nameLabel = new Label(characterName);
            nameLabel.AddToClassList("char-name");

            var (startLevel, expInLevel, expForThisLevel) = GetLevelProgress(currentExp);
            var levelLabel = new Label($"Lv.{startLevel}");
            levelLabel.AddToClassList("char-level");
            levelLabel.name = "CharLevelLabel";

            infoRow.Add(nameLabel);
            infoRow.Add(levelLabel);

            var barContainer = new VisualElement();
            barContainer.AddToClassList("exp-bar-container");

            var barBg = new VisualElement();
            barBg.AddToClassList("exp-bar-bg");

            var barFill = new VisualElement();
            barFill.AddToClassList("exp-bar-fill");
            barFill.name = "ExpBarFill";

            barFill.style.width = Length.Percent(expForThisLevel > 0 ? (float)expInLevel / expForThisLevel * 100f : 0);

            barBg.Add(barFill);
            barContainer.Add(barBg);

            var expText = new Label($"{expInLevel} / {expForThisLevel}");
            expText.AddToClassList("exp-text");
            expText.name = "ExpText";
            expText.userData = new Vector2Int(currentExp, expGained);
            barContainer.Add(expText);

            var gainedLabel = new Label($"+{expGained} EXP");
            gainedLabel.AddToClassList("exp-gained");

            entry.Add(infoRow);
            entry.Add(barContainer);
            entry.Add(gainedLabel);

            _experienceEntries.Add(entry);
        }

        private IEnumerator PlayExpAnimation()
        {
            float stepDelay = AnimDuration / AnimSteps;

            for (int step = 1; step <= AnimSteps; step++)
            {
                float t = (float)step / AnimSteps;
                int index = 0;

                foreach (var entry in _experienceEntries.Children())
                {
                    var barFill = entry.Q<VisualElement>("ExpBarFill");
                    var expText = entry.Q<Label>("ExpText");
                    var levelLabel = entry.Q<Label>("CharLevelLabel");

                    if (barFill != null && expText != null && _rewards.ExperiencePerCharacter.Count > index)
                    {
                        int totalGained = _rewards.ExperiencePerCharacter.Values.ElementAt(index);
                        Vector2Int data = (Vector2Int)(expText.userData ?? new Vector2Int(0, 0));
                        int totalStartExp = data.x;
                        int accumulated = totalStartExp + Mathf.RoundToInt(t * totalGained);

                        var (level, expInLevel, expToNext) = GetLevelProgress(accumulated);
                        float pct = expToNext > 0 ? (float)expInLevel / expToNext * 100f : 100f;
                        barFill.style.width = Length.Percent(Mathf.Min(pct, 100f));
                        expText.text = $"{expInLevel} / {expToNext}";

                        if (levelLabel != null)
                            levelLabel.text = $"Lv.{level}";
                    }
                    index++;
                }

                yield return new WaitForSeconds(stepDelay);
            }

            FinishAnimation();
        }

        private void SkipAnimation()
        {
            if (_animCoroutine != null)
            {
                StopCoroutine(_animCoroutine);
                _animCoroutine = null;
            }
            FinishAnimation();
        }

        private void FinishAnimation()
        {
            _isAnimating = false;

            int index = 0;
            foreach (var entry in _experienceEntries.Children())
            {
                var barFill = entry.Q<VisualElement>("ExpBarFill");
                var expText = entry.Q<Label>("ExpText");

                if (barFill != null && expText != null && _rewards.ExperiencePerCharacter.Count > index)
                {
                    int gained = _rewards.ExperiencePerCharacter.Values.ElementAt(index);
                    Vector2Int data = (Vector2Int)(expText.userData ?? new Vector2Int(0, 0));
                    int totalStartExp = data.x;
                    int finalTotal = totalStartExp + gained;

                    var (level, expInLevel, expToNext) = GetLevelProgress(finalTotal);
                    float pct = expToNext > 0 ? (float)expInLevel / expToNext * 100f : 100f;
                    barFill.style.width = Length.Percent(Mathf.Min(pct, 100f));
                    expText.text = $"{expInLevel} / {expToNext}";

                    var levelLabel = entry.Q<Label>("CharLevelLabel");
                    if (levelLabel != null)
                        levelLabel.text = $"Lv.{level}";
                }
                index++;
            }

            if (_continueButton != null)
                _continueButton.SetEnabled(true);
        }

        private void OnRootClicked(ClickEvent evt)
        {
            if (_isAnimating)
                SkipAnimation();
        }

        private void OnContinueClicked()
        {
            if (_isAnimating) return;
            OnContinue?.Invoke();
        }
    }
}
