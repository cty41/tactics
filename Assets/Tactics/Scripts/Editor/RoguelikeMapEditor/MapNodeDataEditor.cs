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
                    if (GUILayout.Button("×", GUILayout.Width(24)))
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
                wrapper.NotifyDataChanged();
            }
        }
    }
}
