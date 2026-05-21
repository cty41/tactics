using System.Collections.Generic;
using Tactics.RoguelikeMap.Economy;
using Tactics.Runtime.Utilities;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.RoguelikeMap.Interaction
{
    /// <summary>
    /// 商店节点处理器
    /// 使用 UXML 构建 FTL 风格商店界面，玩家可浏览并购买商品
    /// </summary>
    public class StoreNodeHandler : MonoBehaviour
    {
        public static StoreNodeHandler Instance { get; private set; }

        [Header("UI Settings")]
        [SerializeField] private VisualTreeAsset shopPanelTemplate;

        private UIDocument _uiDocument;
        private VisualElement _overlay;
        private Label _titleLabel;
        private Label _goldLabel;
        private VisualElement _goodsContainer;
        private Button _closeButton;

        private ShopManager _shopManager;
        private List<ShopGood> _currentGoods;
        private RoguelikeMapNode _currentNode;
        private System.Action _onClose;

        private void Awake()
        {
            Instance = this;
            _uiDocument = GetComponent<UIDocument>();
            _shopManager = new ShopManager();
        }

        /// <summary>
        /// 显示商店界面
        /// </summary>
        /// <param name="node">当前商店节点</param>
        /// <param name="onClose">关闭回调</param>
        public void ShowShop(RoguelikeMapNode node, System.Action onClose = null)
        {
            if (node == null)
            {
                TLog.Warning("[StoreNodeHandler] 节点为空");
                return;
            }

            _currentNode = node;
            _onClose = onClose;

            // 实例化 UXML 模板
            InstantiateTemplate();

            // 生成商品
            int goodCount = Random.Range(2, 4); // 2-3 件
            _currentGoods = _shopManager.GenerateGoods(goodCount);

            // 显示商品列表
            DisplayGoods();

            // 更新金币显示
            UpdateGoldDisplay();

            TLog.Info($"[StoreNodeHandler] 显示商店，{_currentGoods.Count} 件商品");
        }

        /// <summary>
        /// 实例化 UXML 模板并缓存元素引用
        /// </summary>
        private void InstantiateTemplate()
        {
            ClearExisting();

            if (shopPanelTemplate == null)
            {
                TLog.Error("[StoreNodeHandler] shopPanelTemplate 未设置");
                return;
            }

            var root = _uiDocument.rootVisualElement;
            var instance = shopPanelTemplate.Instantiate();
            root.Add(instance);

            _overlay = root.Q<VisualElement>("ShopOverlay");
            _titleLabel = root.Q<Label>("ShopTitle");
            _goldLabel = root.Q<Label>("GoldDisplay");
            _goodsContainer = root.Q<VisualElement>("GoodsContainer");
            _closeButton = root.Q<Button>("CloseButton");

            // 绑定关闭按钮
            if (_closeButton != null)
                _closeButton.RegisterCallback<ClickEvent>(evt => CloseShop());
        }

        /// <summary>
        /// 显示商品列表
        /// </summary>
        private void DisplayGoods()
        {
            if (_goodsContainer == null) return;

            _goodsContainer.Clear();

            for (int i = 0; i < _currentGoods.Count; i++)
            {
                var good = _currentGoods[i];
                var row = CreateGoodRow(good, i);
                _goodsContainer.Add(row);
            }
        }

        /// <summary>
        /// 创建单个商品行
        /// </summary>
        private VisualElement CreateGoodRow(ShopGood good, int index)
        {
            var row = new VisualElement();
            row.AddToClassList("good-row");

            // 图标
            var icon = new Label(good.IconHint);
            icon.AddToClassList("good-icon");
            row.Add(icon);

            // 名称
            var name = new Label(good.Name);
            name.AddToClassList("good-name");
            row.Add(name);

            // 价格
            var price = new Label($"{good.Price}金");
            price.AddToClassList("good-price");
            row.Add(price);

            // 购买按钮
            var buyBtn = new Button();
            buyBtn.text = "购买";

            bool canAfford = RunGoldManager.Instance.HasEnoughGold(good.Price);
            if (canAfford)
            {
                buyBtn.AddToClassList("buy-btn");
                int capturedIndex = index;
                buyBtn.RegisterCallback<ClickEvent>(evt => OnBuyClicked(capturedIndex));
            }
            else
            {
                buyBtn.AddToClassList("buy-btn-disabled");
                buyBtn.text = "金币不足";
                buyBtn.SetEnabled(false);
            }

            row.Add(buyBtn);

            return row;
        }

        /// <summary>
        /// 购买商品
        /// </summary>
        private void OnBuyClicked(int index)
        {
            if (index < 0 || index >= _currentGoods.Count) return;

            var good = _currentGoods[index];

            if (RunGoldManager.Instance.SpendGold(good.Price))
            {
                TLog.Info($"[StoreNodeHandler] 购买成功: {good.Name} ({good.Price}金)");

                // 移除已购买商品
                _currentGoods.RemoveAt(index);

                // 刷新商品列表和金币显示
                DisplayGoods();
                UpdateGoldDisplay();
            }
            else
            {
                TLog.Warning($"[StoreNodeHandler] 购买失败: 金币不足");
                // 刷新按钮状态
                DisplayGoods();
            }
        }

        /// <summary>
        /// 更新金币显示
        /// </summary>
        private void UpdateGoldDisplay()
        {
            if (_goldLabel != null)
            {
                int gold = RunGoldManager.Instance.CurrentGold;
                _goldLabel.text = $"金币: {gold}";
            }
        }

        /// <summary>
        /// 关闭商店
        /// </summary>
        private void CloseShop()
        {
            TLog.Info("[StoreNodeHandler] 关闭商店");

            // 标记节点已访问
            if (_currentNode != null)
                _currentNode.state = NodeState.Visited;

            ClearExisting();
            _onClose?.Invoke();
        }

        /// <summary>
        /// 清除现有面板
        /// </summary>
        private void ClearExisting()
        {
            if (_uiDocument?.rootVisualElement != null)
            {
                var root = _uiDocument.rootVisualElement;
                var existing = root.Q<VisualElement>("ShopOverlay");
                existing?.RemoveFromHierarchy();
            }

            _overlay = null;
            _titleLabel = null;
            _goldLabel = null;
            _goodsContainer = null;
            _closeButton = null;
        }
    }
}
