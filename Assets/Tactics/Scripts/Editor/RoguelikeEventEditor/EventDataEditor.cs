using UnityEditor;
using UnityEngine;

namespace Tactics.Editor.RoguelikeEventEditor
{
    /// <summary>
    /// 自定义编辑器，用于在 Inspector 中显示 EventNodePayload 数据。
    /// 根据节点类型显示不同的属性字段。
    /// </summary>
    [CustomEditor(typeof(EventNodeDataWrapper))]
    public class EventDataEditor : UnityEditor.Editor
    {
        private EventNodeDataWrapper _wrapper;

        private void OnEnable()
        {
            _wrapper = (EventNodeDataWrapper)target;
        }

        public override void OnInspectorGUI()
        {
            if (_wrapper == null || _wrapper.NodeData == null)
            {
                EditorGUILayout.HelpBox("No node data assigned.", MessageType.Warning);
                return;
            }

            var data = _wrapper.NodeData;

            // Node Type (只读标签)
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Node Type", _wrapper.NodeType);
            EditorGUI.EndDisabledGroup();

            // Node ID (可编辑)
            EditorGUI.BeginChangeCheck();
            string nodeId = EditorGUILayout.TextField("Node ID", _wrapper.NodeId ?? "");
            if (EditorGUI.EndChangeCheck())
            {
                _wrapper.NodeId = nodeId;
                _wrapper.NotifyDataChanged();
            }

            EditorGUILayout.Space();

            // 根据节点类型显示特定字段
            switch (_wrapper.NodeType)
            {
                case EventNodeTypes.Start:
                    DrawStartFields(data);
                    break;
                case EventNodeTypes.Option:
                    DrawOptionFields(data);
                    break;
                case EventNodeTypes.Check:
                    DrawCheckFields(data);
                    break;
                case EventNodeTypes.Success:
                case EventNodeTypes.Failure:
                    DrawResultFields(data);
                    break;
                case EventNodeTypes.End:
                    DrawEndFields(data);
                    break;
            }
        }

        private void DrawStartFields(EventNodePayload data)
        {
            EditorGUI.BeginChangeCheck();

            string eventId = EditorGUILayout.TextField("Event ID", data.eventId ?? "");
            string title = EditorGUILayout.TextField("Title", data.title ?? "");
            string description = EditorGUILayout.TextArea(data.description ?? "", GUILayout.MinHeight(60));
            int regionIndex = System.Array.IndexOf(EventRegions.All, data.region ?? EventRegions.DarkForest);
            if (regionIndex < 0) regionIndex = 0;
            regionIndex = EditorGUILayout.Popup("Region", regionIndex, EventRegions.DisplayNames);

            if (EditorGUI.EndChangeCheck())
            {
                data.eventId = eventId;
                data.title = title;
                data.description = description;
                data.region = EventRegions.All[regionIndex];
                _wrapper.NotifyDataChanged();
            }
        }

        private void DrawOptionFields(EventNodePayload data)
        {
            EditorGUI.BeginChangeCheck();

            string text = EditorGUILayout.TextArea(data.text ?? "", GUILayout.MinHeight(60));
            int attrIndex = System.Array.IndexOf(EventAttributes.All, data.attribute ?? EventAttributes.Strength);
            if (attrIndex < 0) attrIndex = 0;
            attrIndex = EditorGUILayout.Popup("Attribute", attrIndex, EventAttributes.DisplayNames);
            int successRate = EditorGUILayout.IntSlider("Success Rate%", data.successRate ?? 40, 0, 100);

            if (EditorGUI.EndChangeCheck())
            {
                data.text = text;
                data.attribute = EventAttributes.All[attrIndex];
                data.successRate = successRate;
                _wrapper.NotifyDataChanged();
            }
        }

        private void DrawCheckFields(EventNodePayload data)
        {
            EditorGUI.BeginChangeCheck();

            int difficultyModifier = EditorGUILayout.IntSlider("Difficulty Modifier", data.difficultyModifier ?? 0, -20, 20);

            if (EditorGUI.EndChangeCheck())
            {
                data.difficultyModifier = difficultyModifier;
                _wrapper.NotifyDataChanged();
            }
        }

        private void DrawResultFields(EventNodePayload data)
        {
            EditorGUI.BeginChangeCheck();

            int resultTypeIndex = System.Array.IndexOf(EventResultTypes.All, data.resultType ?? EventResultTypes.Gold);
            if (resultTypeIndex < 0) resultTypeIndex = 0;
            resultTypeIndex = EditorGUILayout.Popup("Result Type", resultTypeIndex, EventResultTypes.All);

            int targetIndex = System.Array.IndexOf(EventTargetTypes.AllValues, data.target ?? EventTargetTypes.Self);
            if (targetIndex < 0) targetIndex = 0;
            targetIndex = EditorGUILayout.Popup("Target", targetIndex, EventTargetTypes.DisplayNames);

            int amount = EditorGUILayout.IntSlider("Amount", data.amount ?? 0, -100, 100);
            string resultText = EditorGUILayout.TextArea(data.resultText ?? "", GUILayout.MinHeight(60));

            if (EditorGUI.EndChangeCheck())
            {
                data.resultType = EventResultTypes.All[resultTypeIndex];
                data.target = EventTargetTypes.AllValues[targetIndex];
                data.amount = amount;
                data.resultText = resultText;
                _wrapper.NotifyDataChanged();
            }

            // 条件字段：根据 Result Type 显示
            string selectedResultType = EventResultTypes.All[resultTypeIndex];
            switch (selectedResultType)
            {
                case EventResultTypes.Item:
                    EditorGUI.BeginChangeCheck();
                    string itemId = EditorGUILayout.TextField("Item ID", data.itemId ?? "");
                    if (EditorGUI.EndChangeCheck())
                    {
                        data.itemId = itemId;
                        _wrapper.NotifyDataChanged();
                    }
                    break;

                case EventResultTypes.Equip:
                    EditorGUI.BeginChangeCheck();
                    string equipId = EditorGUILayout.TextField("Equip ID", data.equipId ?? "");
                    if (EditorGUI.EndChangeCheck())
                    {
                        data.equipId = equipId;
                        _wrapper.NotifyDataChanged();
                    }
                    break;

                case EventResultTypes.Buff:
                    EditorGUI.BeginChangeCheck();
                    string buffId = EditorGUILayout.TextField("Buff ID", data.buffId ?? "");
                    if (EditorGUI.EndChangeCheck())
                    {
                        data.buffId = buffId;
                        _wrapper.NotifyDataChanged();
                    }
                    break;

                case EventResultTypes.Battle:
                    EditorGUI.BeginChangeCheck();
                    string enemyGroupId = EditorGUILayout.TextField("Enemy Group ID", data.enemyGroupId ?? "");
                    if (EditorGUI.EndChangeCheck())
                    {
                        data.enemyGroupId = enemyGroupId;
                        _wrapper.NotifyDataChanged();
                    }
                    break;
            }
        }

        private void DrawEndFields(EventNodePayload data)
        {
            EditorGUI.BeginChangeCheck();

            string summaryText = EditorGUILayout.TextArea(data.summaryText ?? "", GUILayout.MinHeight(60));

            if (EditorGUI.EndChangeCheck())
            {
                data.summaryText = summaryText;
                _wrapper.NotifyDataChanged();
            }
        }
    }
}
