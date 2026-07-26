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
        private VisualElement _root;
        private Label _resultLabel;
        private Label _roundsLabel;
        private Label _goldLabel;
        private Button _continueButton;

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
            _continueButton = null;
        }

        public void SetBattleResult(BattleRewardSystem.BattleRewards rewards, bool isVictory, Dictionary<string, int> characterLevels = null, Dictionary<string, int> currentCharacterExp = null)
        {
            EnsureUIElements();
            if (_root == null) return;

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
                _continueButton.SetEnabled(true);
        }

        private void OnRootClicked(ClickEvent evt)
        {
        }

        private void OnContinueClicked()
        {
            OnContinue?.Invoke();
        }
    }
}
