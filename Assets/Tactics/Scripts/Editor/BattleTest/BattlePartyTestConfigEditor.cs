using Tactics.Common.Battle;
using UnityEditor;
using UnityEngine;

namespace Tactics.Editor.BattleTest
{
    [CustomEditor(typeof(BattlePartyTestConfig))]
    public sealed class BattlePartyTestConfigEditor : UnityEditor.Editor
    {
        private int _selectedSlot = -1;

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var displayNameProp = serializedObject.FindProperty("_displayName");
            EditorGUILayout.PropertyField(displayNameProp);

            var slotsProp = serializedObject.FindProperty("_slots");
            EditorGUILayout.PropertyField(slotsProp, true);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("SceneView Preview", EditorStyles.boldLabel);

            EditorGUILayout.LabelField($"Slots: {slotsProp.arraySize}");

            for (int i = 0; i < slotsProp.arraySize; i++)
            {
                var slotProp = slotsProp.GetArrayElementAtIndex(i);
                var spawnCellProp = slotProp.FindPropertyRelative("_spawnCell");
                var displayName = slotProp.FindPropertyRelative("_displayName")?.stringValue;
                var cell = spawnCellProp.vector2IntValue;

                EditorGUILayout.BeginHorizontal();
                bool isSelected = _selectedSlot == i;
                var label = string.IsNullOrEmpty(displayName) ? $"[{i}]" : $"[{i}] {displayName}";
                if (GUILayout.Toggle(isSelected, $" {label} ({cell.x}, {cell.y})", EditorStyles.miniButton))
                {
                    if (_selectedSlot != i) _selectedSlot = i;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Frame All"))
            {
                SceneView.RepaintAll();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            var config = target as BattlePartyTestConfig;
            if (config == null) return;

            var slotsProp = serializedObject.FindProperty("_slots");
            if (slotsProp == null) return;

            for (int i = 0; i < slotsProp.arraySize; i++)
            {
                var slotProp = slotsProp.GetArrayElementAtIndex(i);
                var spawnCellProp = slotProp.FindPropertyRelative("_spawnCell");
                var cell = spawnCellProp.vector2IntValue;
                var displayName = slotProp.FindPropertyRelative("_displayName")?.stringValue;
                bool isSelected = _selectedSlot == i;

                var label = string.IsNullOrEmpty(displayName)
                    ? $"Party[{i}] ({cell.x},{cell.y})"
                    : $"Party[{i}] {displayName} ({cell.x},{cell.y})";

                EditorGUI.BeginChangeCheck();
                var dragged = BattleTestSpawnCellSceneGuiUtility.DrawInteractiveHandle(
                    cell,
                    BattleTestSpawnCellSceneGuiUtility.PartyColor,
                    label,
                    isSelected,
                    out var newCell,
                    out var clicked);

                if (clicked)
                {
                    _selectedSlot = i;
                    Repaint();
                }

                if (dragged && EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(config, "Move Party SpawnCell");
                    spawnCellProp.vector2IntValue = newCell;
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(config);
                    Repaint();
                }
            }
        }
    }
}
