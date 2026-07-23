using System.Linq;
using Tactics.Consumables;
using Tactics.Equipment;
using Tactics.RoguelikeMap;
using Tactics.Runtime.Utilities;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.UI
{
    /// <summary>
    /// Run结束总结UI控制器
    /// 负责显示Run结束后的结算界面，支持Victory和Defeat两种结局
    /// </summary>
    public sealed class RunEndSummaryUIController : UIControllerBase
    {
        private VisualElement _root;
        private VisualElement _panel;
        private Label _titleLabel;
        private Label _goldLabel;
        private Label _statsLabel;
        private Label _equipmentLabel;
        private Label _itemsLabel;
        private Label _bossStatusLabel;
        private Button _returnHomeButton;

        private RunSummary _runSummary;
        private System.Action _onReturnHome;

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
            var currentRoot = Ui.GetRootElement(UIManager.UIId.RunEndSummary);
            if (ReferenceEquals(_root, currentRoot) && _root != null)
                return;

            UnregisterEvents();
            ClearUIElementReferences();
            _root = currentRoot;
            if (_root == null) return;

            _panel = _root.Q<VisualElement>("RunEndSummaryPanel");
            _titleLabel = _root.Q<Label>("TitleLabel");
            _goldLabel = _root.Q<Label>("GoldLabel");
            _statsLabel = _root.Q<Label>("StatsLabel");
            _equipmentLabel = _root.Q<Label>("EquipmentLabel");
            _itemsLabel = _root.Q<Label>("ItemsLabel");
            _bossStatusLabel = _root.Q<Label>("BossStatusLabel");
            _returnHomeButton = _root.Q<Button>("ReturnHomeButton");
        }

        private void RegisterEvents()
        {
            if (_returnHomeButton != null)
                _returnHomeButton.clicked += OnReturnHomeClicked;
        }

        private void UnregisterEvents()
        {
            if (_returnHomeButton != null)
                _returnHomeButton.clicked -= OnReturnHomeClicked;
        }

        private void ClearUIElementReferences()
        {
            _root = null;
            _panel = null;
            _titleLabel = null;
            _goldLabel = null;
            _statsLabel = null;
            _equipmentLabel = null;
            _itemsLabel = null;
            _bossStatusLabel = null;
            _returnHomeButton = null;
        }

        /// <summary>
        /// 显示Run结束总结界面
        /// </summary>
        /// <param name="summary">Run结算数据</param>
        /// <param name="onReturnHome">返回Home的回调</param>
        public void ShowSummary(RunSummary summary, System.Action onReturnHome)
        {
            if (summary == null)
            {
                TLog.Warning("[RunEndSummaryUIController] RunSummary is null");
                return;
            }

            _runSummary = summary;
            _onReturnHome = onReturnHome;

            EnsureUIElements();
            if (_root == null) return;

            RunOutcome outcome = summary.GetRunOutcome();
            bool isVictory = outcome == RunOutcome.Victory;

            // Update theme
            if (_panel != null)
            {
                _panel.RemoveFromClassList("victory");
                _panel.RemoveFromClassList("defeat");
                _panel.AddToClassList(isVictory ? "victory" : "defeat");
            }

            if (_titleLabel != null)
            {
                _titleLabel.text = isVictory ? "Run 完成！" : "Run 失败...";
                _titleLabel.RemoveFromClassList("victory");
                _titleLabel.RemoveFromClassList("defeat");
                _titleLabel.AddToClassList(isVictory ? "victory" : "defeat");
            }

            // Gold
            if (_goldLabel != null)
                _goldLabel.text = summary.totalGold.ToString();

            // Stats
            if (_statsLabel != null)
            {
                _statsLabel.text = $"击败敌人: {summary.enemiesDefeated}\n" +
                                  $"访问节点: {summary.nodesVisited}\n" +
                                  $"完成事件: {summary.eventsCompleted}";
            }

            // Equipment
            if (_equipmentLabel != null)
            {
                if (summary.acquiredEquipment.Count > 0)
                {
                    _equipmentLabel.text = string.Join("\n", summary.acquiredEquipment.Select(ResolveEquipmentName));
                    _equipmentLabel.parent.style.display = DisplayStyle.Flex;
                }
                else
                {
                    _equipmentLabel.parent.style.display = DisplayStyle.None;
                }
            }

            // Items
            if (_itemsLabel != null)
            {
                if (summary.acquiredItems.Count > 0)
                {
                    _itemsLabel.text = string.Join("\n", summary.acquiredItems.Select(ResolveItemName));
                    _itemsLabel.parent.style.display = DisplayStyle.Flex;
                }
                else
                {
                    _itemsLabel.parent.style.display = DisplayStyle.None;
                }
            }

            // Boss Status
            if (_bossStatusLabel != null)
            {
                if (summary.bossDefeated)
                {
                    _bossStatusLabel.text = "✓ Boss已被击败";
                    _bossStatusLabel.style.display = DisplayStyle.Flex;
                }
                else
                {
                    _bossStatusLabel.text = "✗ Boss未被击败";
                    _bossStatusLabel.style.display = DisplayStyle.Flex;
                }
            }

            TLog.Info($"[RunEndSummaryUIController] Showing summary, outcome={outcome}");
        }

        private void OnReturnHomeClicked()
        {
            TLog.Info("[RunEndSummaryUIController] Return home clicked");
            _onReturnHome?.Invoke();
            _onReturnHome = null;
            _runSummary = null;
        }

        private static string ResolveEquipmentName(string id)
        {
            return EquipmentDatabase.GetById(id)?.DisplayName ?? id;
        }

        private static string ResolveItemName(string id)
        {
            return ConsumableDatabase.GetById(id)?.DisplayName ?? id;
        }
    }
}
