#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tactics.Common.Units.Abilities.Editor
{
    /// <summary>
    /// Simple setup tool to create AbilityConfigs and fix prefabs.
    /// Menu: Tools > Ability System > Setup Unit Abilities
    /// </summary>
    public class AbilityConfigSetup : EditorWindow
    {
        private Vector2 _scroll;
        private List<string> _log = new List<string>();

        [MenuItem("Tools/Ability System/Setup Unit Abilities")]
        public static void ShowWindow()
        {
            var w = GetWindow<AbilityConfigSetup>("Ability Setup");
            w.minSize = new Vector2(400, 300);
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Unit Ability Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1. Creates AbilityConfig .asset files for each Unit prefab\n" +
                "2. Removes old MonoBehaviour ability components\n" +
                "3. Links AbilityConfigs to Unit._abilityConfigs",
                MessageType.Info);

            EditorGUILayout.Space(5);
            if (GUILayout.Button("Run Setup", GUILayout.Height(35))) Run();

            EditorGUILayout.Space(5);
            if (GUILayout.Button("Clear Old Components Only", GUILayout.Height(30))) ClearOld();

            EditorGUILayout.Space(5);
            if (GUILayout.Button("Create Default Configs Only", GUILayout.Height(30))) CreateDefaults();

            EditorGUILayout.Space(5);
            var fold = EditorGUILayout.Foldout(true, $"Log ({_log.Count})");
            if (fold)
            {
                foreach (var l in _log) EditorGUILayout.LabelField(l, EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.EndScrollView();
        }

        private void Log(string m) { _log.Add(m); Debug.Log($"[AbilitySetup] {m}"); }

        private void Run()
        {
            _log.Clear();
            Log("=== Starting Setup ===");
            string dir = "Assets/Tactics/Arts/ScriptableObjects/Abilities";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();

            var prefabs = Directory.GetFiles("Assets/Tactics/Arts/Prefabs/Units", "*.prefab", SearchOption.AllDirectories);
            Log($"Found {prefabs.Length} prefabs");

            foreach (var p in prefabs) ProcessPrefab(p, dir);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log("=== Done ===");
        }

        private void ProcessPrefab(string prefabPath, string configDir)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (go == null) return;
            var unit = go.GetComponent<Unit>();
            if (unit == null) return;

            Log($"Processing: {go.name}");

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            unit = root.GetComponent<Unit>();

            // Remove old ability components (with specific field names)
            var comps = root.GetComponents<MonoBehaviour>().ToList();
            int removed = 0;
            foreach (var c in comps)
            {
                if (c == null) continue;
                var so = new SerializedObject(c);
                if (so.FindProperty("_isRangedDamage") != null ||
                    so.FindProperty("_hasHalfScaling") != null ||
                    so.FindProperty("_withConfirmation") != null ||
                    so.FindProperty("_useTouchOptimizedControls") != null)
                {
                    DestroyImmediate(c);
                    removed++;
                }
            }
            Log($"  Removed {removed} old components");

            // Create configs
            string name = go.name.Replace(" ", "").Replace("-", "");
            string movePath = $"{configDir}/{name}_Move.asset";
            string atkPath = $"{configDir}/{name}_Attack.asset";

            if (AssetDatabase.LoadAssetAtPath<AbilityConfig>(movePath) == null)
            {
                var cfg = ScriptableObject.CreateInstance<AbilityConfig>();
                cfg.name = Path.GetFileNameWithoutExtension(movePath);
                SetField(cfg, "_displayName", "Move");
                SetField(cfg, "_actionPointCost", 1);
                SetField(cfg, "_targetingStrategy", new SelfTargeting());
                SetField(cfg, "_effects", new List<AbilityEffect>());
                AssetDatabase.CreateAsset(cfg, movePath);
            }

            if (AssetDatabase.LoadAssetAtPath<AbilityConfig>(atkPath) == null)
            {
                var cfg = ScriptableObject.CreateInstance<AbilityConfig>();
                cfg.name = Path.GetFileNameWithoutExtension(atkPath);
                SetField(cfg, "_displayName", "Attack");
                SetField(cfg, "_actionPointCost", 1);
                SetField(cfg, "_targetingStrategy", new SingleTargetEnemy());
                SetField(cfg, "_effects", new List<AbilityEffect> { new DamageEffect() });
                AssetDatabase.CreateAsset(cfg, atkPath);
            }

            // Assign to Unit._abilityConfigs
            var soUnit = new SerializedObject(unit);
            var prop = soUnit.FindProperty("_abilityConfigs");
            if (prop != null)
            {
                prop.ClearArray();
                prop.InsertArrayElementAtIndex(0);
                prop.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AbilityConfig>(movePath);
                prop.InsertArrayElementAtIndex(1);
                prop.GetArrayElementAtIndex(1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AbilityConfig>(atkPath);
                soUnit.ApplyModifiedProperties();
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            Log($"  Assigned 2 AbilityConfigs");
        }

        private void ClearOld()
        {
            _log.Clear();
            var prefabs = Directory.GetFiles("Assets/Tactics/Arts/Prefabs/Units", "*.prefab", SearchOption.AllDirectories);
            foreach (var p in prefabs)
            {
                var root = PrefabUtility.LoadPrefabContents(p);
                var comps = root.GetComponents<MonoBehaviour>().ToList();
                foreach (var c in comps)
                {
                    if (c == null) continue;
                    var so = new SerializedObject(c);
                    if (so.FindProperty("_isRangedDamage") != null || so.FindProperty("_hasHalfScaling") != null)
                    {
                        DestroyImmediate(c);
                        Log($"Removed {c.GetType().Name} from {root.name}");
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root, p);
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void CreateDefaults()
        {
            _log.Clear();
            string dir = "Assets/Tactics/Arts/ScriptableObjects/Abilities";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var move = ScriptableObject.CreateInstance<AbilityConfig>();
            move.name = "Default_Move";
            SetField(move, "_displayName", "Move");
            SetField(move, "_actionPointCost", 1);
            SetField(move, "_targetingStrategy", new SelfTargeting());
            SetField(move, "_effects", new List<AbilityEffect>());
            AssetDatabase.CreateAsset(move, $"{dir}/Default_Move.asset");

            var atk = ScriptableObject.CreateInstance<AbilityConfig>();
            atk.name = "Default_Attack";
            SetField(atk, "_displayName", "Attack");
            SetField(atk, "_actionPointCost", 1);
            SetField(atk, "_targetingStrategy", new SingleTargetEnemy());
            SetField(atk, "_effects", new List<AbilityEffect> { new DamageEffect() });
            AssetDatabase.CreateAsset(atk, $"{dir}/Default_Attack.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log("Created Default_Move.asset and Default_Attack.asset");
        }

        private void SetField<T>(object obj, string name, T val)
        {
            var f = obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            f?.SetValue(obj, val);
        }
    }
}