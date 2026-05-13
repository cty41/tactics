using System.Collections.Generic;
using Tactics.Runtime.Utilities;
using System.IO;
using System.Linq;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Common.Units.Buffs;
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
        private const string BuffsPath = "Assets/Tactics/Arts/ScriptableObjects/Buffs";

        [MenuItem("Tactics/Role System/Setup Role System")]
        public static void SetupRoleSystem()
        {
            EnsureDirectory(ClassesPath);
            EnsureDirectory(BuffsPath);

            // Load existing abilities
            var meleeAttack = LoadAsset<AbilityConfig>("MeleeAttack.asset");
            var rangedAttack = LoadAsset<AbilityConfig>("RangedAttack.asset");
            var chargeAttack = LoadAsset<AbilityConfig>("ChargeAttack.asset");

            if (meleeAttack == null || rangedAttack == null || chargeAttack == null)
            {
                TLog.Error("[RoleSystemSetup] Failed to load existing abilities. Please ensure MeleeAttack, RangedAttack, and ChargeAttack assets exist.");
                return;
            }

            // Create buff configs
            var igniteBuffConfig = CreateIgniteBuffConfig();
            var frozenBuffConfig = CreateFrozenBuffConfig();
            var markBuffConfig = CreateMarkBuffConfig();
            var counterBuffConfig = CreateCounterBuffConfig();

            // Create new abilities
            var magicAttack = CreateMagicAttack();
            var heavyShot = CreateHeavyShot();
            var fireball = CreateFireball();
            var uppercut = CreateUppercut();
            var counter = CreateCounter(counterBuffConfig);
            var freeze = CreateFreeze(frozenBuffConfig);
            var mark = CreateMark(markBuffConfig);

            // Create role configs
            var barbarian = CreateRoleConfig("Barbarian", RoleType.Barbarian, new List<AbilityConfig> { meleeAttack, chargeAttack, uppercut, counter });
            var mage = CreateRoleConfig("Mage", RoleType.Mage, new List<AbilityConfig> { magicAttack, fireball, freeze });
            var hunter = CreateRoleConfig("Hunter", RoleType.Hunter, new List<AbilityConfig> { rangedAttack, heavyShot, mark });

            // Save assets
            AssetDatabase.CreateAsset(magicAttack, $"{AbilitiesPath}/MagicAttack.asset");
            AssetDatabase.CreateAsset(heavyShot, $"{AbilitiesPath}/HeavyShot.asset");
            AssetDatabase.CreateAsset(fireball, $"{AbilitiesPath}/Fireball.asset");
            AssetDatabase.CreateAsset(uppercut, $"{AbilitiesPath}/Uppercut.asset");
            AssetDatabase.CreateAsset(counter, $"{AbilitiesPath}/Counter.asset");
            AssetDatabase.CreateAsset(freeze, $"{AbilitiesPath}/Freeze.asset");
            AssetDatabase.CreateAsset(mark, $"{AbilitiesPath}/Mark.asset");
            AssetDatabase.CreateAsset(barbarian, $"{ClassesPath}/Barbarian.asset");
            AssetDatabase.CreateAsset(mage, $"{ClassesPath}/Mage.asset");
            AssetDatabase.CreateAsset(hunter, $"{ClassesPath}/Hunter.asset");

            AssetDatabase.CreateAsset(igniteBuffConfig, $"{BuffsPath}/Ignite.asset");
            AssetDatabase.CreateAsset(frozenBuffConfig, $"{BuffsPath}/Frozen.asset");
            AssetDatabase.CreateAsset(markBuffConfig, $"{BuffsPath}/Mark.asset");
            AssetDatabase.CreateAsset(counterBuffConfig, $"{BuffsPath}/Counter.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Configure prefabs
            ConfigurePrefab("Fighter.prefab", barbarian);
            ConfigurePrefab("Mage.prefab", mage);
            ConfigurePrefab("Hunter.prefab", hunter);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            TLog.Info("[RoleSystemSetup] Role system setup complete!");
        }

        [MenuItem("Tactics/Role System/Setup Test1 Scene")]
        public static void SetupTest1Scene()
        {
            string scenePath = "Assets/Tactics/Scenes/Test1.unity";
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

            // Check if Mage already exists
            var existingMage = GameObject.Find("Mage");
            if (existingMage != null)
            {
                TLog.Info("[RoleSystemSetup] Mage already exists in Test1 scene.");
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
                        var unitSo = new SerializedObject(unit);
                        var cellManager = GameObject.Find("CellManager")?.GetComponent<Tactics.Common.Cells.UnityCellManager>();
                        var dataTilemap = GameObject.Find("BackgroundLayer")?.GetComponent<UnityEngine.Tilemaps.Tilemap>();
                        if (dataTilemap != null)
                        {
                            unitSo.FindProperty("_gridTilemap").objectReferenceValue = dataTilemap;
                        }
                        if (cellManager != null)
                        {
                            unitSo.FindProperty("_cellManager").objectReferenceValue = cellManager;
                        }
                        unitSo.ApplyModifiedProperties();
                    }

                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                    TLog.Info("[RoleSystemSetup] Added Mage to Test1 scene at (0.5, 14.5, 0).");
                }
                else
                {
                    TLog.Warning("[RoleSystemSetup] Mage.prefab not found.");
                }
            }

            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            TLog.Info("[RoleSystemSetup] Test1 scene setup complete!");
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

        private static BuffConfig CreateIgniteBuffConfig()
        {
            var asset = ScriptableObject.CreateInstance<BuffConfig>();
            var so = new SerializedObject(asset);
            so.FindProperty("_buffName").stringValue = "Ignite";
            so.FindProperty("_defaultDuration").intValue = 3;
            so.FindProperty("_canAct").boolValue = true;
            so.FindProperty("_isUnique").boolValue = false;
            so.FindProperty("_effectType").enumValueIndex = (int)BuffEffectType.None;
            so.FindProperty("_triggerTiming").enumValueIndex = (int)BuffTriggerTiming.TurnStart;
            so.FindProperty("_damagePerTurn").floatValue = 5f;
            so.FindProperty("_elementType").enumValueIndex = (int)ElementType.Fire;

            so.ApplyModifiedProperties();
            return asset;
        }

        private static BuffConfig CreateFrozenBuffConfig()
        {
            var asset = ScriptableObject.CreateInstance<BuffConfig>();
            var so = new SerializedObject(asset);
            so.FindProperty("_buffName").stringValue = "Frozen";
            so.FindProperty("_defaultDuration").intValue = 2;
            so.FindProperty("_canAct").boolValue = false;
            so.FindProperty("_isUnique").boolValue = false;
            so.FindProperty("_effectType").enumValueIndex = (int)BuffEffectType.Frozen;
            so.FindProperty("_triggerTiming").enumValueIndex = (int)BuffTriggerTiming.None;

            so.ApplyModifiedProperties();
            return asset;
        }

        private static BuffConfig CreateMarkBuffConfig()
        {
            var asset = ScriptableObject.CreateInstance<BuffConfig>();
            var so = new SerializedObject(asset);
            so.FindProperty("_buffName").stringValue = "Mark";
            so.FindProperty("_defaultDuration").intValue = int.MaxValue;
            so.FindProperty("_canAct").boolValue = true;
            so.FindProperty("_isUnique").boolValue = true;
            so.FindProperty("_effectType").enumValueIndex = (int)BuffEffectType.Marked;
            so.FindProperty("_triggerTiming").enumValueIndex = (int)BuffTriggerTiming.BeforeAttacked;

            so.ApplyModifiedProperties();
            return asset;
        }

        private static BuffConfig CreateCounterBuffConfig()
        {
            var asset = ScriptableObject.CreateInstance<BuffConfig>();
            var so = new SerializedObject(asset);
            so.FindProperty("_buffName").stringValue = "Counter";
            so.FindProperty("_defaultDuration").intValue = 1;
            so.FindProperty("_canAct").boolValue = true;
            so.FindProperty("_isUnique").boolValue = false;
            so.FindProperty("_effectType").enumValueIndex = (int)BuffEffectType.None;
            so.FindProperty("_triggerTiming").enumValueIndex = (int)BuffTriggerTiming.DamageTaken;
            so.FindProperty("_elementType").enumValueIndex = (int)ElementType.None;

            so.ApplyModifiedProperties();
            return asset;
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

        private static AbilityConfig CreateUppercut()
        {
            var asset = ScriptableObject.CreateInstance<AbilityConfig>();
            var so = new SerializedObject(asset);
            so.FindProperty("_displayName").stringValue = "Uppercut";
            so.FindProperty("_description").stringValue = "Punch a unit into the air, lobbing it back 3 tiles.";
            so.FindProperty("_manaCost").intValue = 10;
            so.FindProperty("_cooldown").floatValue = 0f;
            so.FindProperty("_isBasicAbility").boolValue = false;

            // Targeting: SingleTargetEnemy, maxRange=1
            var targetingProp = so.FindProperty("_targetingStrategy");
            targetingProp.managedReferenceValue = new SingleTargetEnemy();
            so.ApplyModifiedProperties();

            targetingProp.FindPropertyRelative("_maxRange").intValue = 1;
            targetingProp.FindPropertyRelative("_minRange").intValue = 0;

            // Effects: DamageEffect + KnockbackEffect
            var effectsProp = so.FindProperty("_effects");
            effectsProp.arraySize = 2;

            // DamageEffect (Strength scaling, melee)
            var damageElem = effectsProp.GetArrayElementAtIndex(0);
            damageElem.managedReferenceValue = new DamageEffect();
            so.ApplyModifiedProperties();

            damageElem.FindPropertyRelative("_baseDamage").floatValue = 3f;
            damageElem.FindPropertyRelative("_scalingType").enumValueIndex = (int)AttributeScalingType.Strength;
            damageElem.FindPropertyRelative("_isRangedDamage").boolValue = false;

            // KnockbackEffect (3 tiles)
            var knockbackElem = effectsProp.GetArrayElementAtIndex(1);
            knockbackElem.managedReferenceValue = new KnockbackEffect();
            so.ApplyModifiedProperties();

            knockbackElem.FindPropertyRelative("_distance").intValue = 3;

            so.ApplyModifiedProperties();
            return asset;
        }

        private static AbilityConfig CreateCounter(BuffConfig counterBuffConfig)
        {
            var asset = ScriptableObject.CreateInstance<AbilityConfig>();
            var so = new SerializedObject(asset);
            so.FindProperty("_displayName").stringValue = "Counter";
            so.FindProperty("_description").stringValue = "Counterattack all attacks until your next turn.";
            so.FindProperty("_manaCost").intValue = 10;
            so.FindProperty("_cooldown").floatValue = 0f;
            so.FindProperty("_isBasicAbility").boolValue = false;

            // Targeting: SelfTargeting
            var targetingProp = so.FindProperty("_targetingStrategy");
            targetingProp.managedReferenceValue = new SelfTargeting();
            so.ApplyModifiedProperties();

            // Effects: ApplyBuffEffect (Counter BuffConfig)
            var effectsProp = so.FindProperty("_effects");
            effectsProp.arraySize = 1;
            var effectElem = effectsProp.GetArrayElementAtIndex(0);
            effectElem.managedReferenceValue = new ApplyBuffEffect();
            so.ApplyModifiedProperties();

            effectElem.FindPropertyRelative("_buffConfig").objectReferenceValue = counterBuffConfig;
            effectElem.FindPropertyRelative("_duration").intValue = 1;

            so.ApplyModifiedProperties();
            return asset;
        }

        private static AbilityConfig CreateFreeze(BuffConfig frozenBuffConfig)
        {
            var asset = ScriptableObject.CreateInstance<AbilityConfig>();
            var so = new SerializedObject(asset);
            so.FindProperty("_displayName").stringValue = "Freeze";
            so.FindProperty("_description").stringValue = "Freeze an enemy for 2 turns. Frozen units cannot act or be damaged (except by fire).";
            so.FindProperty("_manaCost").intValue = 15;
            so.FindProperty("_cooldown").floatValue = 0f;
            so.FindProperty("_isBasicAbility").boolValue = false;

            // Targeting: SingleTargetEnemy, maxRange=3
            var targetingProp = so.FindProperty("_targetingStrategy");
            targetingProp.managedReferenceValue = new SingleTargetEnemy();
            so.ApplyModifiedProperties();

            targetingProp.FindPropertyRelative("_maxRange").intValue = 3;
            targetingProp.FindPropertyRelative("_minRange").intValue = 0;

            // Effects: ApplyBuffEffect (Frozen BuffConfig)
            var effectsProp = so.FindProperty("_effects");
            effectsProp.arraySize = 1;
            var effectElem = effectsProp.GetArrayElementAtIndex(0);
            effectElem.managedReferenceValue = new ApplyBuffEffect();
            so.ApplyModifiedProperties();

            effectElem.FindPropertyRelative("_buffConfig").objectReferenceValue = frozenBuffConfig;
            effectElem.FindPropertyRelative("_duration").intValue = 2;

            so.ApplyModifiedProperties();
            return asset;
        }

        private static AbilityConfig CreateMark(BuffConfig markBuffConfig)
        {
            var asset = ScriptableObject.CreateInstance<AbilityConfig>();
            var so = new SerializedObject(asset);
            so.FindProperty("_displayName").stringValue = "Mark";
            so.FindProperty("_description").stringValue = "Mark an enemy. All attacks against marked enemies are guaranteed critical hits. Only one mark can be active at a time.";
            so.FindProperty("_manaCost").intValue = 10;
            so.FindProperty("_cooldown").floatValue = 0f;
            so.FindProperty("_isBasicAbility").boolValue = false;

            // Targeting: SingleTargetEnemy, maxRange=5
            var targetingProp = so.FindProperty("_targetingStrategy");
            targetingProp.managedReferenceValue = new SingleTargetEnemy();
            so.ApplyModifiedProperties();

            targetingProp.FindPropertyRelative("_maxRange").intValue = 5;
            targetingProp.FindPropertyRelative("_minRange").intValue = 0;

            // Effects: ApplyBuffEffect (Mark BuffConfig)
            var effectsProp = so.FindProperty("_effects");
            effectsProp.arraySize = 1;
            var effectElem = effectsProp.GetArrayElementAtIndex(0);
            effectElem.managedReferenceValue = new ApplyBuffEffect();
            so.ApplyModifiedProperties();

            effectElem.FindPropertyRelative("_buffConfig").objectReferenceValue = markBuffConfig;
            effectElem.FindPropertyRelative("_duration").intValue = int.MaxValue;

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
                TLog.Warning($"[RoleSystemSetup] Failed to load prefab: {prefabPath}");
                return;
            }

            var unit = prefabRoot.GetComponent<Unit>();
            if (unit == null)
            {
                TLog.Warning($"[RoleSystemSetup] No Unit component found on {prefabFileName}");
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

            TLog.Info($"[RoleSystemSetup] Configured {prefabFileName} with {roleConfig.DisplayName}");
        }
    }
}
