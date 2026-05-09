using System.Linq;
using Tactics.Runtime.Utilities;
using Tactics.Common.Battle;
using Tactics.Common.Units.Classes;
using Tactics.Roster;
using Tactics.Units;
using UnityEditor;
using UnityEngine;

namespace Tactics.Editor
{
    public static class PartyBootstrapSetupEditor
    {
        private const string FighterPrefabPath = "Assets/Tactics/Arts/Prefabs/Units/Fighter.prefab";
        private const string MagePrefabPath = "Assets/Tactics/Arts/Prefabs/Units/Mage.prefab";
        private const string HunterPrefabPath = "Assets/Tactics/Arts/Prefabs/Units/Hunter.prefab";

        [MenuItem("Tools/Tactics/Setup Party Bootstrap")]
        private static void SetupPartyBootstrap()
        {
            var battleController = Object.FindFirstObjectByType<BattleController>();
            if (battleController == null)
            {
                TLog.Error("[PartyBootstrapSetupEditor] BattleController not found in current scene.");
                return;
            }

            var serializedObject = new SerializedObject(battleController);

            // Setup role prefab mappings
            var mappingsProp = serializedObject.FindProperty("_rolePrefabMappings");
            mappingsProp.ClearArray();
            mappingsProp.arraySize = 3;

            var fighterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FighterPrefabPath);
            var magePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MagePrefabPath);
            var hunterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HunterPrefabPath);

            SetupMapping(mappingsProp.GetArrayElementAtIndex(0), RoleType.Barbarian, fighterPrefab, new Vector2Int(0, 0));
            SetupMapping(mappingsProp.GetArrayElementAtIndex(1), RoleType.Mage, magePrefab, new Vector2Int(1, 0));
            SetupMapping(mappingsProp.GetArrayElementAtIndex(2), RoleType.Hunter, hunterPrefab, new Vector2Int(2, 0));

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(battleController);

            TLog.Info("[PartyBootstrapSetupEditor] BattleController role prefab mappings configured. Verify in Inspector.");
        }

        private static void SetupMapping(SerializedProperty mappingProp, RoleType roleType, GameObject prefab, Vector2Int cell)
        {
            mappingProp.FindPropertyRelative("RoleType").enumValueIndex = (int)roleType;
            mappingProp.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
            mappingProp.FindPropertyRelative("StartingCell").vector2IntValue = cell;
        }
    }
}
