using System.Collections.Generic;
using Tactics.Common.Battle;
using Tactics.Runtime.Utilities;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.UI
{
    /// <summary>
    /// 战斗结算 UI 控制器。
    /// 显示战斗奖励（金币、经验值）和战斗结果，处理"继续"按钮点击事件。
    /// </summary>
    public sealed class BattleSettlementUIController : UIControllerBase
    {
        private VisualElement _root;
        private Label _resultLabel;
        private Label _roundsLabel;
        private Label _goldLabel;
        private ScrollView _experienceList;
        private Button _continueButton;

        /// <summary>当玩家点击"继续"按钮时触发。</summary>
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
        }

        private void EnsureUIElements()
        {
            if (_root != null) return;

            _root = Ui.GetRootElement(UIManager.UIId.BattleSettlement);
            if (_root == null) return;

            _resultLabel = _root.Q<Label>("ResultLabel");
            _roundsLabel = _root.Q<Label>("RoundsLabel");
            _goldLabel = _root.Q<Label>("GoldLabel");
            _experienceList = _root.Q<ScrollView>("ExperienceList");
            _continueButton = _root.Q<Button>("ContinueButton");
        }

        private void RegisterEvents()
        {
            if (_continueButton != null)
                _continueButton.clicked += OnContinueClicked;
        }

        private void UnregisterEvents()
        {
            if (_continueButton != null)
                _continueButton.clicked -= OnContinueClicked;
        }

        /// <summary>
        /// 设置战斗结算显示数据。
        /// </summary>
        /// <param name="rewards">战斗奖励数据。</param>
        /// <param name="isVictory">是否胜利。</param>
        public void SetBattleResult(BattleRewardSystem.BattleRewards rewards, bool isVictory)
        {
            EnsureUIElements();
            if (_root == null) return;

            // 设置战斗结果
            if (_resultLabel != null)
            {
                _resultLabel.text = isVictory ? "胜利！" : "败北...";
                _resultLabel.RemoveFromClassList("victory-label");
                _resultLabel.RemoveFromClassList("defeat-label");
                _resultLabel.AddToClassList(isVictory ? "victory-label" : "defeat-label");
            }

            // 设置回合数
            if (_roundsLabel != null)
                _roundsLabel.text = $"总回合数：{rewards.TotalRounds}";

            // 设置金币
            if (_goldLabel != null)
                _goldLabel.text = $"+{rewards.TotalGold}";

            // 设置经验值列表
            if (_experienceList != null && rewards.ExperiencePerCharacter != null)
            {
                _experienceList.Clear();
                foreach (var kvp in rewards.ExperiencePerCharacter)
                {
                    CreateExperienceEntry(kvp.Key, kvp.Value);
                }
            }
        }

        /// <summary>
        /// 动态创建单个角色经验值条目。
        /// </summary>
        /// <param name="characterName">角色名称。</param>
        /// <param name="exp">获得的经验值。</param>
        private void CreateExperienceEntry(string characterName, int exp)
        {
            if (_experienceList == null) return;

            var entry = new VisualElement();
            entry.AddToClassList("experience-entry");

            var nameLabel = new Label(characterName);
            nameLabel.AddToClassList("exp-character-name");

            var expLabel = new Label($"+{exp}");
            expLabel.AddToClassList("exp-value");

            entry.Add(nameLabel);
            entry.Add(expLabel);

            _experienceList.Add(entry);
        }

        /// <summary>
        /// 处理"继续"按钮点击，触发 Continue 事件。
        /// </summary>
        private void OnContinueClicked()
        {
            TLog.Info("[BattleSettlementUIController] Continue button clicked.");
            OnContinue?.Invoke();
        }
    }
}
