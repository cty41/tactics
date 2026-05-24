using System;
using Tactics.RoguelikeMap;
using UnityEngine;

namespace Tactics.Editor.RoguelikeMapEditor
{
    /// <summary>
    /// ScriptableObject 包装器，用于在 Inspector 中显示 RoguelikeMapNode 属性。
    /// 持有节点的引用（非拷贝），以便编辑直接修改原始数据。
    /// </summary>
    public class MapNodeDataWrapper : ScriptableObject
    {
        [HideInInspector]
        public RoguelikeMapNode NodeData;

        [HideInInspector]
        public Action OnDataChanged;

        /// <summary>
        /// 宝藏节点奖励配置（序列化副本，用于 Inspector 编辑及 Undo 支持）。
        /// </summary>
        [SerializeField]
        private TreasureNodeConfig _treasureConfig;

        /// <summary>
        /// 商店节点商品配置（序列化副本，用于 Inspector 编辑及 Undo 支持）。
        /// </summary>
        [SerializeField]
        private StoreNodeConfig _storeConfig;

        public TreasureNodeConfig TreasureConfig => _treasureConfig;
        public StoreNodeConfig StoreConfig => _storeConfig;

        /// <summary>
        /// 初始化包装器，绑定节点数据，并从节点同步配置。
        /// </summary>
        public void Initialize(RoguelikeMapNode node)
        {
            NodeData = node;
            SyncFromNode();
        }

        /// <summary>
        /// 将包装器的配置写入到 <see cref="NodeData"/>。
        /// </summary>
        public void SyncToNode()
        {
            if (NodeData == null) return;
            NodeData.treasureConfig = DeepCopyTreasureConfig(_treasureConfig);
            NodeData.storeConfig = DeepCopyStoreConfig(_storeConfig);
        }

        /// <summary>
        /// 从 <see cref="NodeData"/> 读取配置到包装器。
        /// </summary>
        public void SyncFromNode()
        {
            if (NodeData == null) return;
            _treasureConfig = DeepCopyTreasureConfig(NodeData.treasureConfig);
            _storeConfig = DeepCopyStoreConfig(NodeData.storeConfig);
        }

        /// <summary>
        /// 通知外部数据已变更，触发 OnDataChanged 回调。
        /// </summary>
        public void NotifyDataChanged()
        {
            OnDataChanged?.Invoke();
        }

        /// <summary>
        /// 在编辑器修改后调用：同步到节点并触发变更通知。
        /// </summary>
        public void ApplyAndNotify()
        {
            SyncToNode();
            NotifyDataChanged();
        }

        // ── Deep Copy Helpers ────────────────────────────────────────────────

        private static TreasureNodeConfig DeepCopyTreasureConfig(TreasureNodeConfig src)
        {
            if (src == null) return null;
            return new TreasureNodeConfig
            {
                goldMin = src.goldMin,
                goldMax = src.goldMax,
                buffEntries = src.buffEntries?.ConvertAll(e => new BuffConfigEntry
                {
                    buffConfig = e.buffConfig,
                    weight = e.weight
                }) ?? new System.Collections.Generic.List<BuffConfigEntry>(),
                equipmentEntries = src.equipmentEntries?.ConvertAll(e => new EquipmentEntry
                {
                    equipmentId = e.equipmentId,
                    weight = e.weight
                }) ?? new System.Collections.Generic.List<EquipmentEntry>()
            };
        }

        private static StoreNodeConfig DeepCopyStoreConfig(StoreNodeConfig src)
        {
            if (src == null) return null;
            return new StoreNodeConfig();
        }
    }
}
