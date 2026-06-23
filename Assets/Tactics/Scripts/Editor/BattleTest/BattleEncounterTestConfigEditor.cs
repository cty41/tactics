using Tactics.Common.Battle;
using UnityEditor;
using UnityEngine;

namespace Tactics.Editor.BattleTest
{
    [CustomEditor(typeof(BattleEncounterTestConfig))]
    public sealed class BattleEncounterTestConfigEditor : UnityEditor.Editor
    {
        private enum SlotList { Enemy, Corpse }

        private SlotList _activeList = SlotList.Enemy;
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

            EditorGUILayout.PropertyField(serializedObject.FindProperty("_slots"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_corpseSlots"), true);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("SceneView Preview", EditorStyles.boldLabel);

            _activeList = (SlotList)GUILayout.Toolbar((int)_activeList, new[] { "Enemy Slots", "Corpse Slots" });

            var activeProp = _activeList == SlotList.Enemy
                ? serializedObject.FindProperty("_slots")
                : serializedObject.FindProperty("_corpseSlots");

            if (activeProp != null)
            {
                EditorGUILayout.LabelField($"Slots: {activeProp.arraySize}");
                for (int i = 0; i < activeProp.arraySize; i++)
                {
                    var slotProp = activeProp.GetArrayElementAtIndex(i);
                    var spawnCellProp = slotProp.FindPropertyRelative("_spawnCell");
                    var displayName = slotProp.FindPropertyRelative("_displayName")?.stringValue;
                    var cell = spawnCellProp.vector2IntValue;

                    EditorGUILayout.BeginHorizontal();
                    bool isSelected = _activeList == SlotList.Enemy
                        ? _selectedSlot == i
                        : _selectedSlot == i + 1000;

                    string tag = _activeList == SlotList.Enemy ? "Enemy" : "Corpse";
                    var label = string.IsNullOrEmpty(displayName)
                        ? $"[{i}]"
                        : $"[{i}] {displayName}";

                    if (GUILayout.Toggle(isSelected, $" {tag} {label} ({cell.x}, {cell.y})", EditorStyles.miniButton))
                    {
                        if (_activeList == SlotList.Enemy)
                            _selectedSlot = i;
                        else
                            _selectedSlot = i + 1000;
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            if (GUILayout.Button("Frame All"))
            {
                SceneView.RepaintAll();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            var config = target as BattleEncounterTestConfig;
            if (config == null) return;

            DrawSlotList(
                serializedObject.FindProperty("_slots"),
                SlotList.Enemy,
                BattleTestSpawnCellSceneGuiUtility.EnemyColor,
                "Enemy");

            DrawSlotList(
                serializedObject.FindProperty("_corpseSlots"),
                SlotList.Corpse,
                BattleTestSpawnCellSceneGuiUtility.CorpseColor,
                "Corpse");
        }

        private void DrawSlotList(SerializedProperty slotsProp, SlotList listType, Color color, string tag)
        {
            if (slotsProp == null) return;

            int baseIndex = listType == SlotList.Enemy ? 0 : 1000;

            for (int i = 0; i < slotsProp.arraySize; i++)
            {
                var slotProp = slotsProp.GetArrayElementAtIndex(i);
                var spawnCellProp = slotProp.FindPropertyRelative("_spawnCell");
                var cell = spawnCellProp.vector2IntValue;
                var displayName = slotProp.FindPropertyRelative("_displayName")?.stringValue;
                bool isSelected = _selectedSlot == baseIndex + i;

                var label = string.IsNullOrEmpty(displayName)
                    ? $"{tag}[{i}] ({cell.x},{cell.y})"
                    : $"{tag}[{i}] {displayName} ({cell.x},{cell.y})";

                EditorGUI.BeginChangeCheck();
                var dragged = BattleTestSpawnCellSceneGuiUtility.DrawInteractiveHandle(
                    cell,
                    color,
                    label,
                    isSelected,
                    out var newCell,
                    out var clicked);

                if (clicked)
                {
                    _selectedSlot = baseIndex + i;
                    _activeList = listType;
                    Repaint();
                }

                if (dragged && EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(target, $"Move {tag} SpawnCell");
                    spawnCellProp.vector2IntValue = newCell;
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(target);
                    Repaint();
                }
            }
        }
    }
}
