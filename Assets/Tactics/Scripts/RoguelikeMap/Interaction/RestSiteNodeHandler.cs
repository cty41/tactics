using Tactics.RoguelikeMap;
using Tactics.Runtime.Utilities;
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

        [Header("UI Settings")]
        [SerializeField] private VisualTreeAsset restSitePanelTemplate;

        private UIDocument _uiDocument;
        private VisualElement _overlay;
        private RoguelikeMapNode _currentNode;

        private void Awake()
        {
            Instance = this;
            _uiDocument = GetComponent<UIDocument>();
        }

        /// <summary>
        /// 处理休息站节点交互
        /// </summary>
        public void HandleRestSiteNode(RoguelikeMapNode node)
        {
            if (node == null)
            {
                TLog.Warning("[RestSiteNodeHandler] 节点为空");
                return;
            }

            _currentNode = node;

            // 实例化 UXML 模板
            InstantiateTemplate();

            TLog.Info($"[RestSiteNodeHandler] 显示篝火营地面板");
        }

        /// <summary>
        /// 实例化 UXML 模板并绑定按钮事件
        /// </summary>
        private void InstantiateTemplate()
        {
            // 清除现有面板
            ClearExisting();

            if (restSitePanelTemplate == null)
            {
                TLog.Error("[RestSiteNodeHandler] restSitePanelTemplate 未设置");
                return;
            }

            // 实例化模板
            _overlay = restSitePanelTemplate.Instantiate();

            // 绑定按钮事件
            var restButton = _overlay.Q<Button>("RestButton");
            if (restButton != null)
            {
                restButton.clicked += OnRestClicked;
            }

            var leaveButton = _overlay.Q<Button>("LeaveButton");
            if (leaveButton != null)
            {
                leaveButton.clicked += OnLeaveClicked;
            }

            // 添加到 UIDocument
            if (_uiDocument != null)
            {
                _uiDocument.rootVisualElement.Add(_overlay);
            }
        }

        /// <summary>
        /// 清除现有面板
        /// </summary>
        private void ClearExisting()
        {
            if (_overlay != null)
            {
                _overlay.RemoveFromHierarchy();
                _overlay = null;
            }
        }

        /// <summary>
        /// 点击休息按钮
        /// </summary>
        private void OnRestClicked()
        {
            TLog.Info("[RestSiteNodeHandler] 每人恢复 20% HP — TODO: 对接角色HP系统");

            // 关闭面板
            ClosePanel();

            // 标记节点已访问
            MarkNodeVisited();
        }

        /// <summary>
        /// 点击离开按钮
        /// </summary>
        private void OnLeaveClicked()
        {
            TLog.Info("[RestSiteNodeHandler] 离开篝火营地");

            // 关闭面板
            ClosePanel();
        }

        /// <summary>
        /// 关闭面板
        /// </summary>
        private void ClosePanel()
        {
            ClearExisting();
            _currentNode = null;
        }

        /// <summary>
        /// 标记节点为已访问状态
        /// </summary>
        private void MarkNodeVisited()
        {
            if (_currentNode != null)
            {
                _currentNode.state = NodeState.Visited;
                TLog.Info($"[RestSiteNodeHandler] 节点 {_currentNode.nodeId} 已标记为已访问");
            }
        }
    }
}
