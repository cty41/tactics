using Tactics.Runtime.Utilities;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.RoguelikeMap.UI
{
    /// <summary>
    /// Boss胜利UI控制器
    /// 负责显示Boss击败后的结算界面
    /// </summary>
    public class BossVictoryUIController : MonoBehaviour
    {
        public static BossVictoryUIController Instance { get; private set; }

        [Header("UI Settings")]
        [SerializeField] private VisualTreeAsset bossVictoryPanelTemplate;
        [SerializeField] private StyleSheet bossVictoryPanelStyle;

        private VisualElement _root;
        private VisualElement _victoryPanel;
        private Label _titleLabel;
        private Label _goldLabel;
        private Label _itemsLabel;
        private Button _returnHomeButton;

        private RunSummary _runSummary;
        private System.Action _onReturnHome;

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// 显示Boss胜利界面
        /// </summary>
        public void ShowVictory(RunSummary summary, System.Action onReturnHome)
        {
            if (summary == null)
            {
                TLog.Warning("[BossVictoryUIController] RunSummary为空");
                return;
            }

            _runSummary = summary;
            _onReturnHome = onReturnHome;

            // 创建UI
            CreateVictoryPanel();

            // 显示胜利信息
            DisplayVictory(summary);

            TLog.Info($"[BossVictoryUIController] 显示Boss胜利界面");
        }

        /// <summary>
        /// 创建胜利面板
        /// </summary>
        private void CreateVictoryPanel()
        {
            // 清除现有面板
            if (_victoryPanel != null)
            {
                _victoryPanel.RemoveFromHierarchy();
            }

            // 创建根容器
            _root = new VisualElement();
            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.top = 0;
            _root.style.right = 0;
            _root.style.bottom = 0;
            _root.style.backgroundColor = new Color(0, 0, 0, 0.8f);

            // 创建胜利面板
            _victoryPanel = new VisualElement();
            _victoryPanel.style.width = 500;
            _victoryPanel.style.height = 400;
            _victoryPanel.style.alignSelf = Align.Center;
            _victoryPanel.style.justifyContent = Justify.Center;
            _victoryPanel.style.backgroundColor = new Color(0.1f, 0.3f, 0.1f, 1f);
            _victoryPanel.style.borderTopLeftRadius = 15;
            _victoryPanel.style.borderTopRightRadius = 15;
            _victoryPanel.style.borderBottomLeftRadius = 15;
            _victoryPanel.style.borderBottomRightRadius = 15;
            _victoryPanel.style.paddingTop = 30;
            _victoryPanel.style.paddingBottom = 30;
            _victoryPanel.style.paddingLeft = 30;
            _victoryPanel.style.paddingRight = 30;

            // 标题
            _titleLabel = new Label("Boss已被击败！");
            _titleLabel.style.fontSize = 28;
            _titleLabel.style.color = Color.yellow;
            _titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _titleLabel.style.marginBottom = 30;
            _victoryPanel.Add(_titleLabel);

            // 金币奖励
            _goldLabel = new Label();
            _goldLabel.style.fontSize = 18;
            _goldLabel.style.color = Color.white;
            _goldLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _goldLabel.style.marginBottom = 15;
            _victoryPanel.Add(_goldLabel);

            // 物品奖励
            _itemsLabel = new Label();
            _itemsLabel.style.fontSize = 16;
            _itemsLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            _itemsLabel.style.whiteSpace = WhiteSpace.Normal;
            _itemsLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _itemsLabel.style.marginBottom = 30;
            _victoryPanel.Add(_itemsLabel);

            // 返回Home按钮
            _returnHomeButton = new Button();
            _returnHomeButton.text = "返回避难所";
            _returnHomeButton.style.fontSize = 20;
            _returnHomeButton.style.height = 50;
            _returnHomeButton.style.backgroundColor = new Color(0.8f, 0.6f, 0.2f, 1f);
            _returnHomeButton.style.borderTopLeftRadius = 10;
            _returnHomeButton.style.borderTopRightRadius = 10;
            _returnHomeButton.style.borderBottomLeftRadius = 10;
            _returnHomeButton.style.borderBottomRightRadius = 10;
            _returnHomeButton.clicked += OnReturnHomeClicked;
            _victoryPanel.Add(_returnHomeButton);

            _root.Add(_victoryPanel);

            // 添加到UIManager
            // TODO: 需要集成到UIManager系统
        }

        /// <summary>
        /// 显示胜利信息
        /// </summary>
        private void DisplayVictory(RunSummary summary)
        {
            _goldLabel.text = $"获得金币: {summary.totalGold}";
            
            if (summary.acquiredItems.Count > 0)
            {
                _itemsLabel.text = "获得物品:\n" + string.Join("\n", summary.acquiredItems);
            }
            else
            {
                _itemsLabel.text = "没有获得额外物品";
            }
        }

        /// <summary>
        /// 返回Home按钮被点击
        /// </summary>
        private void OnReturnHomeClicked()
        {
            TLog.Info("[BossVictoryUIController] 返回Home");

            // 关闭胜利面板
            if (_root != null)
            {
                _root.RemoveFromHierarchy();
            }

            // 回调
            _onReturnHome?.Invoke();
            _onReturnHome = null;
            _runSummary = null;
        }

        /// <summary>
        /// 关闭胜利UI
        /// </summary>
        public void Close()
        {
            if (_root != null)
            {
                _root.RemoveFromHierarchy();
            }

            _onReturnHome?.Invoke();
            _onReturnHome = null;
            _runSummary = null;
        }
    }
}
