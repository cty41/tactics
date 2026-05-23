using System;
using UnityEngine;

namespace Tactics.Editor.RoguelikeEventEditor
{
    /// <summary>
    /// ScriptableObject 包装器，用于在 Inspector 中显示 EventNodePayload 数据。
    /// 持有对原始 EventNodePayload 的引用（非副本），以便编辑直接修改原始数据。
    /// </summary>
    public class EventNodeDataWrapper : ScriptableObject
    {
        [HideInInspector] public EventNodePayload NodeData;
        [HideInInspector] public string NodeType;
        [HideInInspector] public string NodeId;
        [HideInInspector] public Action OnDataChanged;

        /// <summary>
        /// 初始化包装器。
        /// </summary>
        public void Initialize(EventNodePayload nodeData, string nodeType, string nodeId = null)
        {
            NodeData = nodeData;
            NodeType = nodeType;
            NodeId = nodeId;
        }

        /// <summary>
        /// 通知数据已更改，触发回调。
        /// </summary>
        public void NotifyDataChanged()
        {
            OnDataChanged?.Invoke();
        }
    }
}
