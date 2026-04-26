using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Common.Units.Classes;
using UnityEditor;
using UnityEngine;

namespace Tactics.Editor
{
    public static class RoleSystemSetupEditor
    {
        private const string AbilitiesPath = "Assets/Tactics/Arts/ScriptableObjects/Abilities";
        private const string ClassesPath = "Assets/Tactics/Arts/ScriptableObjects/Classes";
        private const string PrefabsPath = "Assets/Tactics/Arts/Prefabs/Units";

        [MenuItem("Tools/Tactics/Setup Role System")]
        public static void SetupRoleSystem()
        {
            EnsureDirectory(ClassesPath);

            // Load existing abilities
            var meleeAttack = LoadAsset<AbilityConfig>("MeleeAttack.asset");
            var rangedAttack = LoadAsset<AbilityConfig>("RangedAttack.asset");
            var chargeAttack = LoadAsset<AbilityConfig>("ChargeAttack.asset");

            if (meleeAttack == null || rangedAttack == null || chargeAttack == null)
            {
                Debug.LogError("[RoleSystemSetup] Failed to load existing abilities. Please ensure MeleeAttack, RangedAttack, and ChargeAttack assets exist.");
                return;
            }

            // Create new abilities
            var magicAttack = CreateMagicAttack();
            var heavyShot = CreateHeavyShot();
            var fireball = CreateFireball();

            // Create role configs
            var barbarian = CreateRoleConfig("Barbarian", RoleType.Barbarian, new List<AbilityConfig> { meleeAttack, chargeAttack });
            var mage = CreateRoleConfig("Mage", RoleType.Mage, new List<AbilityConfig> { magicAttack, fireball });
            var hunter = CreateRoleConfig("Hunter", RoleType.Hunter, new List<AbilityConfig> { rangedAttack, heavyShot });

            // Save assets
            AssetDatabase.CreateAsset(magicAttack, $"{AbilitiesPath}/MagicAttack.asset");
            AssetDatabase.CreateAsset(heavyShot, $"{AbilitiesPath}/HeavyShot.asset");
            AssetDatabase.CreateAsset(fireball, $"{AbilitiesPath}/Fireball.asset");
            AssetDatabase.CreateAsset(barbarian, $"{ClassesPath}/Barbarian.asset");
            AssetDatabase.CreateAsset(mage, $"{ClassesPath}/Mage.asset");
            AssetDatabase.CreateAsset(hunter, $"{ClassesPath}/Hunter.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Configure prefabs
            ConfigurePrefab("Fighter.prefab", barbarian);
            ConfigurePrefab("Mage.prefab", mage);
            ConfigurePrefab("Hunter.prefab", hunter);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[RoleSystemSetup] Role system setup complete!");
        }

        [MenuItem("Tools/Tactics/Setup Test1 Scene")]
        public static void SetupTest1Scene()
        {
            string scenePath = "Assets/Tactics/Scenes/Test1.unity";
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

            // Check if Mage already exists
            var existingMage = GameObject.Find("Mage");
            if (existingMage != null)
            {
                Debug.Log("[RoleSystemSetup] Mage already exists in Test1 scene.");
            }
            else
            {
                var magePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/Mage.prefab");
                if (magePrefab != null)
                {
                    var mageInstance = (GameObject)PrefabUtility.InstantiatePrefab(magePrefab, scene);
                    mageInstance.name = "Mage";
                    mageInstance.transform.position = new Vector3(0.5f, 14.5f, 0f);

                    var unit = mageInstance.GetComponent<Unit>();
                    if (unit != null)
                    {
                        var cellManager = GameObject.Find("CellManager")?.GetComponent<Tactics.Common.Cells.UnityCellManager>();
                        var dataTilemap = GameObject.Find("DataLayer")?.GetComponent<UnityEngine.Tilemaps.Tilemap>();

                        var unitSo = new SerializedObject(unit);
                        unitSo.FindProperty("_playerNumber").intValue = 0;
                        if (cellManager != null)
                            unitSo.FindProperty("_cellManager").objectReferenceValue = cellManager;
                        if (dataTilemap != null)
                            unitSo.FindProperty("_dataTilemap").objectReferenceValue = dataTilemap;
                        unitSo.ApplyModifiedProperties();
                    }

                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                    Debug.Log("[RoleSystemSetup] Added Mage to Test1 scene at (0.5, 14.5, 0).");
                }
                else
                {
                    Debug.LogWarning("[RoleSystemSetup] Mage.prefab not found.");
                }
            }

            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log("[RoleSystemSetup] Test1 scene setup complete!");
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private static T LoadAsset<T>(string fileName) where T : Object
        {
            string path = $"{AbilitiesPath}/{fileName}";
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static AbilityConfig CreateMagicAttack()
        {
            var asset = ScriptableObject.CreateInstance<AbilityConfig>();
            var so = new SerializedObject(asset);
            so.FindProperty("_displayName").stringValue = "Magic Attack";
            so.FindProperty("_description").stringValue = "A basic magic attack using Intelligence.";
            so.FindProperty("_manaCost").intValue = 0;
            so.FindProperty("_cooldown").floatValue = 0f;
            so.FindProperty("_isBasicAbility").boolValue = true;

            // Targeting: SingleTargetEnemy, maxRange=3
            var targetingProp = so.FindProperty("_targetingStrategy");
            targetingProp.managedReferenceValue = new SingleTargetEnemy();
            so.ApplyModifiedProperties();

            targetingProp.FindPropertyRelative("_maxRange").intValue = 3;
            targetingProp.FindPropertyRelative("_minRange").intValue = 0;

            // Effects: DamageEffect (Intelligence scaling, not ranged)
            var effectsProp = so.FindProperty("_effects");
            effectsProp.arraySize = 1;
            var effectElem = effectsProp.GetArrayElementAtIndex(0);
            effectElem.managedReferenceValue = new DamageEffect();
            so.ApplyModifiedProperties();

            effectElem.FindPropertyRelative("_baseDamage").floatValue = 0f;
            effectElem.FindPropertyRelative("_scalingType").enumValueIndex = (int)AttributeScalingType.Intelligence;
            effectElem.FindPropertyRelative("_isRangedDamage").boolValue = false;

            so.ApplyModifiedProperties();
            return asset;
        }

        private static AbilityConfig CreateHeavyShot()
        {
            var asset = ScriptableObject.CreateInstance<AbilityConfig>();
            var so = new SerializedObject(asset);
            so.FindProperty("_displayName").stringValue = "Heavy Shot";
            so.FindProperty("_description").stringValue = "A powerful ranged attack with reduced accuracy.";
            so.FindProperty("_manaCost").intValue = 15;
            so.FindProperty("_cooldown").floatValue = 0f;
            so.FindProperty("_isBasicAbility").boolValue = false;

            // Targeting: SingleTargetEnemy, maxRange=5
            var targetingProp = so.FindProperty("_targetingStrategy");
            targetingProp.managedReferenceValue = new SingleTargetEnemy();
            so.ApplyModifiedProperties();

            targetingProp.FindPropertyRelative("_maxRange").intValue = 5;
            targetingProp.FindPropertyRelative("_minRange").intValue = 0;

            // Effects: AccuracyDamageEffect (Agility scaling, ranged, accuracyPenalty=0.5)
            var effectsProp = so.FindProperty("_effects");
            effectsProp.arraySize = 1;
            var effectElem = effectsProp.GetArrayElementAtIndex(0);
            effectElem.managedReferenceValue = new AccuracyDamageEffect();
            so.ApplyModifiedProperties();

            effectElem.FindPropertyRelative("_baseDamage").floatValue = 3f;
            effectElem.FindPropertyRelative("_scalingType").enumValueIndex = (int)AttributeScalingType.Agility;
            effectElem.FindPropertyRelative("_isRangedDamage").boolValue = true;
            effectElem.FindPropertyRelative("_accuracyPenalty").floatValue = 0.5f;

            so.ApplyModifiedProperties();
            return asset;
        }

        private static AbilityConfig CreateFireball()
        {
            var asset = ScriptableObject.CreateInstance<AbilityConfig>();
            var so = new SerializedObject(asset);
            so.FindProperty("_displayName").stringValue = "Fireball";
            so.FindProperty("_description").stringValue = "An area-of-effect magic attack.";
            so.FindProperty("_manaCost").intValue = 20;
            so.FindProperty("_cooldown").floatValue = 0f;
            so.FindProperty("_isBasicAbility").boolValue = false;

            // Targeting: AoETargeting, maxRange=4, radius=2, Circle
            var targetingProp = so.FindProperty("_targetingStrategy");
            targetingProp.managedReferenceValue = new AoETargeting();
            so.ApplyModifiedProperties();

            targetingProp.FindPropertyRelative("_maxRange").intValue = 4;
            targetingProp.FindPropertyRelative("_radius").intValue = 2;
            targetingProp.FindPropertyRelative("_shape").enumValueIndex = (int)AoeShape.Circle;

            // Effects: DamageEffect (Intelligence scaling, not ranged)
            var effectsProp = so.FindProperty("_effects");
            effectsProp.arraySize = 1;
            var effectElem = effectsProp.GetArrayElementAtIndex(0);
            effectElem.managedReferenceValue = new DamageEffect();
            so.ApplyModifiedProperties();

            effectElem.FindPropertyRelative("_baseDamage").floatValue = 2f;
            effectElem.FindPropertyRelative("_scalingType").enumValueIndex = (int)AttributeScalingType.Intelligence;
            effectElem.FindPropertyRelative("_isRangedDamage").boolValue = false;

            so.ApplyModifiedProperties();
            return asset;
        }

        private static RoleConfig CreateRoleConfig(string displayName, RoleType roleType, List<AbilityConfig> abilities)
        {
            var asset = ScriptableObject.CreateInstance<RoleConfig>();
            var so = new SerializedObject(asset);
            so.FindProperty("_displayName").stringValue = displayName;
            so.FindProperty("_roleType").enumValueIndex = (int)roleType;

            var abilitiesProp = so.FindProperty("_abilities");
            abilitiesProp.arraySize = abilities.Count;
            for (int i = 0; i < abilities.Count; i++)
            {
                abilitiesProp.GetArrayElementAtIndex(i).objectReferenceValue = abilities[i];
            }

            so.ApplyModifiedProperties();
            return asset;
        }

        private static void ConfigurePrefab(string prefabFileName, RoleConfig roleConfig)
        {
            string prefabPath = $"{PrefabsPath}/{prefabFileName}";
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                Debug.LogWarning($"[RoleSystemSetup] Failed to load prefab: {prefabPath}");
                return;
            }

            var unit = prefabRoot.GetComponent<Unit>();
            if (unit == null)
            {
                Debug.LogWarning($"[RoleSystemSetup] No Unit component found on {prefabFileName}");
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                return;
            }

            var unitSo = new SerializedObject(unit);
            unitSo.FindProperty("_roleConfig").objectReferenceValue = roleConfig;

            // Clear old ability configs to avoid duplication
            var abilityConfigsProp = unitSo.FindProperty("_abilityConfigs");
            abilityConfigsProp.ClearArray();

            unitSo.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);

            Debug.Log($"[RoleSystemSetup] Configured {prefabFileName} with {roleConfig.DisplayName}");
        }
    }
}
