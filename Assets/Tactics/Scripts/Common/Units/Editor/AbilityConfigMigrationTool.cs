#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Tactics.Runtime.Utilities;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tactics.Common.Units.Abilities.Editor
{
    /// <summary>
    /// Editor tool to migrate units from old MonoBehaviour abilities to new AbilityConfig system.
    /// Menu: Tools > Ability System > Migrate to AbilityConfig
    /// </summary>
    public class AbilityConfigMigrationTool : EditorWindow
    {
        private Vector2 _scrollPosition;
        private List<string> _logs = new List<string>();
        private bool _showLog = true;

        [MenuItem("Tactics/Ability System/Migrate to AbilityConfig")]
        public static void ShowWindow()
        {
            var window = GetWindow<AbilityConfigMigrationTool>("AbilityConfig Migration");
            window.minSize = new Vector2(500, 400);
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("AbilityConfig Migration Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox(
                "This tool will:\n" +
                "1. Create AbilityConfig .asset files for each Unit prefab\n" +
                "2. Remove old MonoBehaviour ability components (Missing scripts)\n" +
                "3. Assign new AbilityConfigs to _abilityConfigs list\n\n" +
                "Make sure to backup your project before running!",
                MessageType.Warning);

            EditorGUILayout.Space(10);

            GUI.color = Color.green;
            if (GUILayout.Button("Run Full Migration", GUILayout.Height(40)))
            {
                RunMigration();
            }
            GUI.color = Color.white;

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Clear Old Ability Components Only", GUILayout.Height(30)))
            {
                ClearOldAbilitiesOnly();
            }

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Create Default AbilityConfigs", GUILayout.Height(30)))
            {
                CreateDefaultConfigs();
            }

            EditorGUILayout.Space(10);

            _showLog = EditorGUILayout.Foldout(_showLog, $"Log ({_logs.Count} entries)");
            if (_showLog)
            {
                var boxStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
                using (new EditorGUILayout.VerticalScope(boxStyle))
                {
                    foreach (var log in _logs)
                    {
                        EditorGUILayout.LabelField(log, EditorStyles.wordWrappedMiniLabel);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void RunMigration()
        {
            _logs.Clear();
            Log("=== Starting AbilityConfig Migration ===");

            string configsPath = "Assets/Tactics/Battle/Abilities";
            EnsureDirectoryExists(configsPath);

            var unitPrefabs = FindUnitPrefabs();
            Log($"Found {unitPrefabs.Count} Unit prefabs");

            foreach (var prefabPath in unitPrefabs)
            {
                ProcessPrefab(prefabPath, configsPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Log("=== Migration Complete ===");
            EditorUtility.DisplayDialog("Migration Complete", "Processed all prefabs. Check log for details.", "OK");
        }

        private void ClearOldAbilitiesOnly()
        {
            _logs.Clear();
            Log("=== Clearing Old Ability Components ===");

            var unitPrefabs = FindUnitPrefabs();
            int totalRemoved = 0;

            foreach (var prefabPath in unitPrefabs)
            {
                var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                var removed = RemoveOldAbilityComponents(prefabRoot);
                totalRemoved += removed.Count;

                if (removed.Count > 0)
                {
                    Log($"  {prefabRoot.name}: Removed {removed.Count} old components");
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                }

                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Log($"Total components removed: {totalRemoved}");
            Log("=== Clear Complete ===");
        }

        private void CreateDefaultConfigs()
        {
            _logs.Clear();
            Log("=== Creating Default AbilityConfigs ===");

            string configsPath = "Assets/Tactics/Battle/Abilities";
            EnsureDirectoryExists(configsPath);

            CreateAttackConfig($"{configsPath}/Default_MeleeAttack.asset", false);
            CreateAttackConfig($"{configsPath}/Default_RangedAttack.asset", true);
            CreateMoveConfig($"{configsPath}/Default_Move.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Log("=== Default Configs Created ===");
        }

        private List<string> FindUnitPrefabs()
        {
            var prefabs = new List<string>();
            string prefabsPath = "Assets/Tactics/Arts/Prefabs/Units";

            if (!Directory.Exists(prefabsPath))
            {
                Log($"Warning: Directory not found: {prefabsPath}");
                return prefabs;
            }

            var prefabFiles = Directory.GetFiles(prefabsPath, "*.prefab", SearchOption.AllDirectories);
            prefabs.AddRange(prefabFiles);
            return prefabs;
        }

        private void ProcessPrefab(string prefabPath, string configsPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Log($"  Error: Could not load prefab: {prefabPath}");
                return;
            }

            var unitComponent = prefab.GetComponent<Unit>();
            if (unitComponent == null)
            {
                Log($"  Skip: {prefab.name} (no Unit component)");
                return;
            }

            Log($"Processing: {prefab.name}");

            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

            var oldAbilities = RemoveOldAbilityComponents(prefabRoot);
            Log($"  Removed {oldAbilities.Count} old ability components");

            var configPaths = CreateAbilityConfigsForPrefab(prefabRoot, configsPath);
            Log($"  Created/Found {configPaths.Count} AbilityConfigs");

            AssignConfigsToUnit(prefabRoot, configPaths);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);

            Log($"  Done: {prefab.name}");
        }

        private List<string> RemoveOldAbilityComponents(GameObject prefabRoot)
        {
            var removed = new List<string>();
            var components = prefabRoot.GetComponents<MonoBehaviour>();

            foreach (var comp in components.ToList())
            {
                if (comp == null) continue;
                if (comp is ScriptableObject) continue;

                var so = new SerializedObject(comp);
                bool isOldAbility = false;

                string[] oldAbilityFieldNames = new[] {
                    "_isRangedDamage", "_hasHalfScaling", "_withConfirmation",
                    "_useTouchOptimizedControls"
                };

                foreach (var fieldName in oldAbilityFieldNames)
                {
                    if (so.FindProperty(fieldName) != null)
                    {
                        isOldAbility = true;
                        break;
                    }
                }

                if (isOldAbility)
                {
                    removed.Add(comp.GetType().Name);
                    Object.DestroyImmediate(comp);
                }
            }

            return removed;
        }

        private List<string> CreateAbilityConfigsForPrefab(GameObject prefabRoot, string configsPath)
        {
            var configPaths = new List<string>();
            string unitName = prefabRoot.name.Replace(" ", "").Replace("-", "");

            string moveConfigPath = $"{configsPath}/{unitName}_Move.asset";
            if (AssetDatabase.LoadAssetAtPath<AbilityConfig>(moveConfigPath) == null)
            {
                CreateMoveConfig(moveConfigPath);
            }
            configPaths.Add(moveConfigPath);

            string attackConfigPath = $"{configsPath}/{unitName}_Attack.asset";
            if (AssetDatabase.LoadAssetAtPath<AbilityConfig>(attackConfigPath) == null)
            {
                bool isRanged = prefabRoot.GetComponent<Tactics.Units.LandUnitMovementRules>() == null;
                CreateAttackConfig(attackConfigPath, isRanged);
            }
            configPaths.Add(attackConfigPath);

            return configPaths;
        }

        private void CreateMoveConfig(string path)
        {
            var config = ScriptableObject.CreateInstance<AbilityConfig>();
            config.name = Path.GetFileNameWithoutExtension(path);
            SetPrivateField(config, "_displayName", "Move");
            SetPrivateField(config, "_targetingStrategy", new SelfTargeting());
            SetPrivateField(config, "_effects", new List<AbilityEffect>());
            AssetDatabase.CreateAsset(config, path);
            Log($"  Created MoveConfig: {path}");
        }

        private void CreateAttackConfig(string path, bool isRanged)
        {
            var config = ScriptableObject.CreateInstance<AbilityConfig>();
            config.name = Path.GetFileNameWithoutExtension(path);
            SetPrivateField(config, "_displayName", isRanged ? "Ranged Attack" : "Melee Attack");
            var targetingStrategy = new SingleTargetEnemy();
            SetPrivateField(targetingStrategy, "_minRange", isRanged ? 2 : 0);
            SetPrivateField(targetingStrategy, "_maxRange", isRanged ? 5 : 1);
            SetPrivateField(config, "_targetingStrategy", targetingStrategy);
            SetPrivateField(config, "_effects", new List<AbilityEffect> { new DamageEffect() });
            AssetDatabase.CreateAsset(config, path);
            Log($"  Created AttackConfig: {path} (isRanged={isRanged})");
        }

        private void AssignConfigsToUnit(GameObject prefabRoot, List<string> configPaths)
        {
            var unit = prefabRoot.GetComponent<Unit>();
            if (unit == null) return;

            var serializedObject = new SerializedObject(unit);
            var abilityConfigsProp = serializedObject.FindProperty("_abilityConfigs");

            if (abilityConfigsProp == null)
            {
                Log("    Warning: _abilityConfigs property not found");
                return;
            }

            abilityConfigsProp.ClearArray();

            for (int i = 0; i < configPaths.Count; i++)
            {
                abilityConfigsProp.InsertArrayElementAtIndex(i);
                var element = abilityConfigsProp.GetArrayElementAtIndex(i);
                var config = AssetDatabase.LoadAssetAtPath<AbilityConfig>(configPaths[i]);
                element.objectReferenceValue = config;
            }

            serializedObject.ApplyModifiedProperties();
            Log($"  Assigned {configPaths.Count} configs to Unit");
        }

        private void SetPrivateField<T>(object target, string fieldName, T value)
        {
            var field = target.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }

        private void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
                Log($"Created directory: {path}");
            }
        }

        private void Log(string message)
        {
            _logs.Add(message);
            TLog.Info($"[AbilityMigration] {message}");
        }
    }
}