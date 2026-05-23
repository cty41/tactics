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
        /// 初始化包装器，绑定节点数据。
        /// </summary>
        public void Initialize(RoguelikeMapNode node)
        {
            NodeData = node;
        }

        /// <summary>
        /// 通知外部数据已变更，触发 OnDataChanged 回调。
        /// </summary>
        public void NotifyDataChanged()
        {
            OnDataChanged?.Invoke();
        }
    }
}
