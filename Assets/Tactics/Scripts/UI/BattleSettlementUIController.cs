using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Battle;
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
        private VisualElement _experienceEntries;
        private Button _continueButton;

        private BattleRewardSystem.BattleRewards _rewards;
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
        }

        private void EnsureUIElements()
        {
            if (_root != null) return;
            _root = Ui.GetRootElement(UIManager.UIId.BattleSettlement);
            if (_root == null) return;

            _resultLabel = _root.Q<Label>("ResultLabel");
            _roundsLabel = _root.Q<Label>("RoundsLabel");
            _goldLabel = _root.Q<Label>("GoldLabel");
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

        public void SetBattleResult(BattleRewardSystem.BattleRewards rewards, bool isVictory, Dictionary<string, int> characterLevels = null)
        {
            EnsureUIElements();
            if (_root == null) return;

            _rewards = rewards;

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

            if (_continueButton != null)
                _continueButton.SetEnabled(false);

            if (_experienceEntries != null)
                _experienceEntries.Clear();

            foreach (var kvp in rewards.ExperiencePerCharacter)
            {
                int currentLevel = 1;
                if (characterLevels != null && characterLevels.TryGetValue(kvp.Key, out int lv))
                    currentLevel = lv;

                int expToNext = ExperienceTable.GetExperienceToNextLevel(currentLevel);
                CreateCharacterExpEntry(kvp.Key, kvp.Value, currentLevel, expToNext);
            }

            _isAnimating = true;
            _animCoroutine = StartCoroutine(PlayExpAnimation());
        }

        private void CreateCharacterExpEntry(string characterName, int expGained, int currentLevel, int expToNext)
        {
            var entry = new VisualElement();
            entry.AddToClassList("character-exp-entry");

            var infoRow = new VisualElement();
            infoRow.AddToClassList("char-info-row");

            var nameLabel = new Label(characterName);
            nameLabel.AddToClassList("char-name");

            var levelLabel = new Label($"Lv.{currentLevel}");
            levelLabel.AddToClassList("char-level");

            infoRow.Add(nameLabel);
            infoRow.Add(levelLabel);

            var barContainer = new VisualElement();
            barContainer.AddToClassList("exp-bar-container");

            var barBg = new VisualElement();
            barBg.AddToClassList("exp-bar-bg");

            var barFill = new VisualElement();
            barFill.AddToClassList("exp-bar-fill");
            barFill.name = "ExpBarFill";
            barFill.style.width = Length.Percent(0);

            barBg.Add(barFill);
            barContainer.Add(barBg);

            // 进度文本：0 / expToNext
            var expText = new Label($"0 / {expToNext}");
            expText.AddToClassList("exp-text");
            expText.name = "ExpText";
            expText.userData = expToNext;
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

                    if (barFill != null && expText != null && _rewards.ExperiencePerCharacter.Count > index)
                    {
                        int totalGained = _rewards.ExperiencePerCharacter.Values.ElementAt(index);
                        int expToNext = (int)(expText.userData ?? 100);
                        int current = Mathf.RoundToInt(t * totalGained);
                        float pct = expToNext > 0 ? (float)current / expToNext * 100f : 100f;
                        barFill.style.width = Length.Percent(pct);
                        expText.text = $"{current} / {expToNext}";
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
                    int expToNext = (int)(expText.userData ?? 100);
                    float pct = expToNext > 0 ? (float)gained / expToNext * 100f : 100f;
                    barFill.style.width = Length.Percent(Mathf.Min(pct, 100f));
                    expText.text = $"{gained} / {expToNext}";
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
