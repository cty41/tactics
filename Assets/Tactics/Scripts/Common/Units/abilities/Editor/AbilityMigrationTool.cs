using System.Collections.Generic;
using Tactics.Runtime.Utilities;
using UnityEditor;
using UnityEngine;

namespace Tactics.Common.Units.Abilities.Editor
{
    /// <summary>
    /// Editor tool to clean up orphaned old ability components from prefabs after migration.
    /// Run via Tools > Ability Migration > Cleanup Orphaned Components.
    /// </summary>
    public class AbilityMigrationTool : EditorWindow
    {
        private Vector2 _scrollPosition;
        private int _prefabCount;
        private int _componentCount;
        private bool _scanning;

        private static readonly string[] OldAbilityTypeNames = new string[]
        {
            "AttackAbility",
            "MoveAbility",
            "FireballAbility",
            "MeleeHealAbility",
            "RangedAttackAbility",
            "MeleeAttackAbility",
            "AttackRangeHighlightAbility",
            "Ability"
        };

        [MenuItem("Tactics/Ability System/Cleanup Orphaned Components")]
        public static void ShowWindow()
        {
            GetWindow<AbilityMigrationTool>("Ability Migration");
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.LabelField("Ability System Migration - Cleanup", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "The MonoBehaviour ability scripts have been deleted. This tool finds prefabs " +
                "that still have orphaned component references and removes them.\n\n" +
                "Old component types: AttackAbility, MoveAbility, FireballAbility, " +
                "MeleeHealAbility, RangedAttackAbility, MeleeAttackAbility, AttackRangeHighlightAbility, Ability",
                MessageType.Warning);

            EditorGUILayout.Space();

            if (GUILayout.Button("Scan Prefabs for Orphaned Components", GUILayout.Height(30)))
            {
                ScanAndClean();
            }

            if (_prefabCount > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"Scanned {_prefabCount} prefabs, removed {_componentCount} orphaned components.", EditorStyles.boldLabel);
            }

            EditorGUILayout.EndScrollView();
        }

        private void ScanAndClean()
        {
            _prefabCount = 0;
            _componentCount = 0;

            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                _prefabCount++;

                var components = prefab.GetComponents<Component>();
                var toRemove = new List<Component>();

                foreach (var comp in components)
                {
                    if (comp == null) continue;
                    string typeName = comp.GetType().Name;
                    foreach (var oldName in OldAbilityTypeNames)
                    {
                        if (typeName == oldName)
                        {
                            toRemove.Add(comp);
                            break;
                        }
                    }
                }

                foreach (var comp in toRemove)
                {
                    Undo.DestroyObjectImmediate(comp);
                    _componentCount++;
                }

                if (toRemove.Count > 0)
                {
                    EditorUtility.SetDirty(prefab);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            TLog.Info($"[AbilityMigration] Scanned {_prefabCount} prefabs, removed {_componentCount} orphaned components.");
        }
    }
}
