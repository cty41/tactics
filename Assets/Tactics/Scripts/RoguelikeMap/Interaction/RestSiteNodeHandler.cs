using Tactics.RoguelikeMap;
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

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// 处理休息站节点交互
        /// </summary>
        public async void HandleRestSiteNode(RoguelikeMapNode node)
        {
            if (node == null)
            {
                TLog.Warning("[RestSiteNodeHandler] 节点为空");
                return;
            }

            _currentNode = node;

            // 通过 UIManager 显示 UI
            await UIManager.Instance.ShowAsync(UIManager.UIId.RestSitePanel);
            var root = UIManager.Instance.GetRootElement(UIManager.UIId.RestSitePanel);
            if (root == null)
            {
                TLog.Error("[RestSiteNodeHandler] 无法获取 RestSitePanel 根元素");
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
            TLog.Info("[RestSiteNodeHandler] 休息：每人恢复 20% HP — TODO: 对接角色HP系统");
            ClosePanel();
        }

        private void ClosePanel()
        {
            UIManager.Instance.Hide(UIManager.UIId.RestSitePanel);
            _overlay = null;
        }
    }
}
