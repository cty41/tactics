using Tactics.RoguelikeMap;
using Tactics.Roster;
using Tactics.Runtime.Utilities;
using Tactics.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.RoguelikeMap.Interaction
{
    /// <summary>
    /// 休息站节点处理器
    /// 显示篝火营地面板，提供休息选项
    /// </summary>
    public class RestSiteNodeHandler : MonoBehaviour
    {
        public static RestSiteNodeHandler Instance { get; private set; }

        private VisualElement _overlay;
        private RoguelikeMapNode _currentNode;
        private System.Action _onClose;

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// 处理休息站节点交互
        /// </summary>
        public async void HandleRestSiteNode(RoguelikeMapNode node, System.Action onClose = null)
        {
            if (node == null)
            {
                TLog.Warning("[RestSiteNodeHandler] 节点为空");
                onClose?.Invoke();
                return;
            }

            _currentNode = node;
            _onClose = onClose;

            // 通过 UIManager 显示 UI
            await UIManager.Instance.ShowAsync(UIManager.UIId.RestSitePanel);
            var root = UIManager.Instance.GetRootElement(UIManager.UIId.RestSitePanel);
            if (root == null)
            {
                TLog.Error("[RestSiteNodeHandler] 无法获取 RestSitePanel 根元素");
                _onClose?.Invoke();
                _onClose = null;
                return;
            }

            _overlay = root;

            // 绑定休息按钮
            var restBtn = root.Q<Button>("RestButton");
            if (restBtn != null)
                restBtn.RegisterCallback<ClickEvent>(_ => OnRestClicked());

            // 绑定关闭按钮
            var closeBtn = root.Q<Button>("LeaveButton");
            if (closeBtn != null)
                closeBtn.RegisterCallback<ClickEvent>(_ => ClosePanel());

            TLog.Info("[RestSiteNodeHandler] 显示篝火营地面板");
        }

        private void OnRestClicked()
        {
            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            if (state?.Roster == null || state.Roster.Count == 0)
            {
                TLog.Warning("[RestSiteNodeHandler] 队伍为空，无法恢复 HP");
                ClosePanel();
                return;
            }

            var summaryLines = new System.Text.StringBuilder();
            var rewardResult = RewardResult.Empty();
            rewardResult.HealPercent = 0.3f;
            rewardResult.ManaHealPercent = 0.3f;
            NodeInteractionManager.Instance?.ApplyRewardResult(rewardResult, state);

            foreach (var character in state.Roster)
            {
                if (character.IsDead)
                {
                    summaryLines.AppendLine($"{character.DisplayName}: 已死亡，无法休息恢复");
                    continue;
                }

                summaryLines.AppendLine($"{character.DisplayName}: {character.CurrentHp}/{character.MaxHp} HP, {character.CurrentMp}/{character.MaxMp} MP");
            }

            TLog.Info("[RestSiteNodeHandler] 全队休息完成，HP 已恢复");

            // 显示效果结果弹窗
            NodeInteractionManager.Instance?.ShowEffectResult(
                "休息完成",
                $"全队恢复了 HP！\n{summaryLines}",
                ClosePanel
            );
        }

        private void ClosePanel()
        {
            UIManager.Instance.Hide(UIManager.UIId.RestSitePanel);
            _overlay = null;
            var callback = _onClose;
            _onClose = null;
            callback?.Invoke();
        }
    }
}
