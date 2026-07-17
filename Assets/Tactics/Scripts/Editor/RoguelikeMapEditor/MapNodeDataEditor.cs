using Tactics.RoguelikeMap;
using UnityEditor;
using UnityEngine;

namespace Tactics.Editor.RoguelikeMapEditor
{
    /// <summary>
    /// MapNodeDataWrapper 的自定义编辑器，使用 IMGUI 渲染节点属性。
    /// </summary>
    [CustomEditor(typeof(MapNodeDataWrapper))]
    public class MapNodeDataEditor : UnityEditor.Editor
    {
        private string _newOutgoingId = "";

        public override void OnInspectorGUI()
        {
            var wrapper = (MapNodeDataWrapper)target;
            var node = wrapper.NodeData;

            if (node == null)
            {
                EditorGUILayout.HelpBox("No node data assigned.", MessageType.Warning);
                return;
            }

            bool dataChanged = false;
            EditorGUI.BeginChangeCheck();

            // ── 基本信息（只读） ──
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Node ID", node.nodeId);
            EditorGUILayout.EnumPopup("Node Type", node.nodeType);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(4);

            // ── 位置 ──
            EditorGUILayout.LabelField("Position", EditorStyles.boldLabel);
            var pos = node.position;
            float x = EditorGUILayout.FloatField("X", pos.x);
            float y = EditorGUILayout.FloatField("Y", pos.y);
            if (!Mathf.Approximately(x, pos.x) || !Mathf.Approximately(y, pos.y))
            {
                node.position = new Vector2(x, y);
            }

            // ── Event ID（仅 Mystery 类型） ──
            if (node.nodeType == RoguelikeNodeType.Mystery)
            {
                EditorGUILayout.Space(4);
                node.eventId = EditorGUILayout.TextField("Event ID", node.eventId ?? "");
            }

            // ── Treasure 配置（仅 Treasure 类型） ──
            if (node.nodeType == RoguelikeNodeType.Treasure)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Treasure Config", EditorStyles.boldLabel);

                var tc = wrapper.TreasureConfig;
                if (tc != null)
                {
                    tc.goldMin = EditorGUILayout.IntField("Gold Min", tc.goldMin);
                    tc.goldMax = EditorGUILayout.IntField("Gold Max", tc.goldMax);

                    // Buff 列表
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Buff Entries", EditorStyles.miniLabel);

                    int buffRemoveIndex = -1;
                    for (int i = 0; i < tc.buffEntries.Count; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        tc.buffEntries[i].buffConfig = (Tactics.Common.Units.Buffs.BuffConfig)EditorGUILayout.ObjectField(
                            tc.buffEntries[i].buffConfig, typeof(Tactics.Common.Units.Buffs.BuffConfig), false);
                        tc.buffEntries[i].weight = EditorGUILayout.FloatField("W", tc.buffEntries[i].weight, GUILayout.Width(60));
                        if (GUILayout.Button("-", GUILayout.Width(24))) buffRemoveIndex = i;
                        EditorGUILayout.EndHorizontal();
                    }
                    if (buffRemoveIndex >= 0)
                    {
                        tc.buffEntries.RemoveAt(buffRemoveIndex);
                        dataChanged = true;
                    }

                    if (GUILayout.Button("+ Buff"))
                    {
                        tc.buffEntries.Add(new BuffConfigEntry());
                        dataChanged = true;
                    }

                    // Equipment 列表
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Equipment Entries", EditorStyles.miniLabel);

                    int equipRemoveIndex = -1;
                    for (int i = 0; i < tc.equipmentEntries.Count; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        tc.equipmentEntries[i].equipmentId = EditorGUILayout.TextField("ID", tc.equipmentEntries[i].equipmentId);
                        tc.equipmentEntries[i].weight = EditorGUILayout.FloatField("W", tc.equipmentEntries[i].weight, GUILayout.Width(60));
                        if (GUILayout.Button("-", GUILayout.Width(24))) equipRemoveIndex = i;
                        EditorGUILayout.EndHorizontal();
                    }
                    if (equipRemoveIndex >= 0)
                    {
                        tc.equipmentEntries.RemoveAt(equipRemoveIndex);
                        dataChanged = true;
                    }

                    if (GUILayout.Button("+ Equipment"))
                    {
                        tc.equipmentEntries.Add(new EquipmentEntry());
                        dataChanged = true;
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No treasure config. Click to create.", MessageType.Info);
                    if (GUILayout.Button("Create Treasure Config"))
                    {
                        wrapper.SetTreasureConfig(new TreasureNodeConfig());
                        dataChanged = true;
                    }
                }
            }

            // ── Store 配置（仅 Store 类型） ──
            if (node.nodeType == RoguelikeNodeType.Store)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Store Config", EditorStyles.boldLabel);
                var sc = wrapper.StoreConfig;
                if (sc != null)
                {
                    int removeIndex = -1;
                    for (int i = 0; i < sc.goods.Count; i++)
                    {
                        var good = sc.goods[i];
                        StoreGoodKind resolvedKind = good.ResolvedKind;
                        string resolvedContentId = good.ResolvedContentId ?? string.Empty;

                        EditorGUILayout.BeginHorizontal();
                        var itemKind = (StoreGoodKind)EditorGUILayout.EnumPopup("Type", resolvedKind);
                        string contentId = EditorGUILayout.TextField("ID", resolvedContentId);
                        if (itemKind != resolvedKind ||
                            !string.Equals(contentId, resolvedContentId, System.StringComparison.Ordinal))
                        {
                            good.SetContent(itemKind, contentId);
                        }

                        good.price = EditorGUILayout.IntField("Price", good.price, GUILayout.Width(120));
                        if (GUILayout.Button("-", GUILayout.Width(24))) removeIndex = i;
                        EditorGUILayout.EndHorizontal();
                    }

                    if (removeIndex >= 0)
                    {
                        sc.goods.RemoveAt(removeIndex);
                        dataChanged = true;
                    }

                    if (GUILayout.Button("+ Good"))
                    {
                        sc.goods.Add(new StoreGoodEntry());
                        dataChanged = true;
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No store config. Click to create.", MessageType.Info);
                    if (GUILayout.Button("Create Store Config"))
                    {
                        wrapper.SetStoreConfig(new StoreNodeConfig());
                        dataChanged = true;
                    }
                }
            }

            // ── Outgoing Connections ──
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Outgoing Connections", EditorStyles.boldLabel);

            if (node.outgoing.Count == 0)
            {
                EditorGUILayout.LabelField("  (none)", EditorStyles.miniLabel);
            }
            else
            {
                int removeIndex = -1;
                for (int i = 0; i < node.outgoing.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(node.outgoing[i], GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("-", GUILayout.Width(24)))
                    {
                        removeIndex = i;
                    }
                    EditorGUILayout.EndHorizontal();
                }

                if (removeIndex >= 0)
                {
                    dataChanged = true;
                    node.RemoveOutgoing(node.outgoing[removeIndex]);
                }
            }

            // ── 添加连接 ──
            EditorGUILayout.BeginHorizontal();
            _newOutgoingId = EditorGUILayout.TextField(_newOutgoingId);
            if (GUILayout.Button("+", GUILayout.Width(24)))
            {
                var trimmed = _newOutgoingId?.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    dataChanged = true;
                    node.AddOutgoing(trimmed);
                    _newOutgoingId = "";
                }
            }
            EditorGUILayout.EndHorizontal();

            // ── 通知变更 ──
            if (EditorGUI.EndChangeCheck() || dataChanged)
            {
                wrapper.ApplyAndNotify();
            }
        }
    }
}
