using Tactics.Common.Skills.Graph;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Runtime.Utilities;
using UnityEditor;
using UnityEngine;

namespace Tactics.Editor.SkillGraphEditor
{
    /// <summary>
    /// Maintains a dedicated test prefab that always resolves abilities from Unit._abilityConfigs.
    /// This avoids mutating formal RoleConfig assets while validating SkillGraph abilities.
    /// </summary>
    public static class SkillGraphTestPrefabUtility
    {
        public const string TestPrefabPath = "Assets/Tactics/Arts/Prefabs/Units/SkillGraphTestUnit.prefab";
        private const string DefaultTemplatePrefabPath = "Assets/Tactics/Arts/Prefabs/Units/Mage.prefab";

        public static string EnsureTestPrefabExists()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(TestPrefabPath) != null)
                return TestPrefabPath;

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultTemplatePrefabPath);
            if (source == null)
            {
                TLog.Error($"[SkillGraphTestPrefabUtility] Missing template prefab: {DefaultTemplatePrefabPath}");
                return null;
            }

            if (!AssetDatabase.CopyAsset(DefaultTemplatePrefabPath, TestPrefabPath))
            {
                TLog.Error($"[SkillGraphTestPrefabUtility] Failed to copy template prefab to: {TestPrefabPath}");
                return null;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            TLog.Info($"[SkillGraphTestPrefabUtility] Created test prefab: {TestPrefabPath}");
            return TestPrefabPath;
        }

        public static bool AttachAbilityConfigToTestPrefab(SkillGraphAbilityConfig config, bool replaceAllExisting = true)
        {
            if (config == null)
            {
                TLog.Warning("[SkillGraphTestPrefabUtility] Ability config is null.");
                return false;
            }

            string prefabPath = EnsureTestPrefabExists();
            if (string.IsNullOrEmpty(prefabPath))
                return false;

            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                TLog.Error($"[SkillGraphTestPrefabUtility] Failed to load prefab contents: {prefabPath}");
                return false;
            }

            try
            {
                var unit = prefabRoot.GetComponent<Unit>();
                if (unit == null)
                {
                    TLog.Error($"[SkillGraphTestPrefabUtility] Prefab has no Unit component: {prefabPath}");
                    return false;
                }

                var serializedUnit = new SerializedObject(unit);

                // Force Unit.Initialize() to use _abilityConfigs instead of RoleConfig.Abilities.
                var roleConfigProp = serializedUnit.FindProperty("_roleConfig");
                if (roleConfigProp != null)
                    roleConfigProp.objectReferenceValue = null;

                var abilityConfigsProp = serializedUnit.FindProperty("_abilityConfigs");
                if (abilityConfigsProp == null)
                {
                    TLog.Error("[SkillGraphTestPrefabUtility] Unit prefab is missing _abilityConfigs serialized field.");
                    return false;
                }

                if (replaceAllExisting)
                {
                    abilityConfigsProp.arraySize = 1;
                    abilityConfigsProp.GetArrayElementAtIndex(0).objectReferenceValue = config;
                }
                else
                {
                    bool exists = false;
                    for (int i = 0; i < abilityConfigsProp.arraySize; i++)
                    {
                        if (abilityConfigsProp.GetArrayElementAtIndex(i).objectReferenceValue == config)
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists)
                    {
                        int index = abilityConfigsProp.arraySize;
                        abilityConfigsProp.arraySize++;
                        abilityConfigsProp.GetArrayElementAtIndex(index).objectReferenceValue = config;
                    }
                }

                serializedUnit.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                AssetDatabase.SaveAssets();

                TLog.Info($"[SkillGraphTestPrefabUtility] Attached '{config.name}' to test prefab: {prefabPath}");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        public static bool BuildAndAttachFromGraph(SkillGraphAsset graph, int manaCost = 0, int? targetRangeOverride = null, string iconAssetPath = null)
        {
            if (graph == null)
            {
                TLog.Warning("[SkillGraphTestPrefabUtility] Graph is null.");
                return false;
            }

            if (!SkillGraphValidation.Validate(graph, out var errors, out var warnings))
            {
                TLog.Warning($"[SkillGraphTestPrefabUtility] Graph '{graph.name}' is invalid, cannot build test prefab attachment. Errors={errors.Count}, Warnings={warnings.Count}");
                return false;
            }

            var config = SkillGraphAbilityConfigGenerator.CreateOrSync(graph, manaCost: manaCost, targetRangeOverride: targetRangeOverride, iconAssetPath: iconAssetPath);
            return AttachAbilityConfigToTestPrefab(config, replaceAllExisting: true);
        }
    }
}
