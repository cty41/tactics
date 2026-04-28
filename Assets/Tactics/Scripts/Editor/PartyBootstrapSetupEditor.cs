using System.Linq;
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
            var bootstrap = Object.FindFirstObjectByType<BattlePartyBootstrap>();
            if (bootstrap == null)
            {
                var battleController = Object.FindFirstObjectByType<BattleController>();
                if (battleController == null)
                {
                    Debug.LogError("[PartyBootstrapSetupEditor] BattleController not found in current scene.");
                    return;
                }
                bootstrap = battleController.gameObject.AddComponent<BattlePartyBootstrap>();
                Debug.Log($"[PartyBootstrapSetupEditor] Added BattlePartyBootstrap to {battleController.gameObject.name}.");
            }

            var serializedObject = new SerializedObject(bootstrap);

            // Clear old party slots
            var partySlotsProp = serializedObject.FindProperty("_partySlots");
            partySlotsProp.ClearArray();

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

            // Try to infer starting cells from old placeholder units if available
            var oldSlots = bootstrap.GetComponent<BattlePartyBootstrap>() != null
                ? bootstrap.GetComponent<BattlePartyBootstrap>().GetType()
                    .GetField("_partySlots", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(bootstrap) as System.Collections.Generic.List<TilemapUnit>
                : null;

            // Actually, let's just search for existing human player units in the scene
            var existingUnits = Object.FindObjectsByType<TilemapUnit>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var humanUnits = existingUnits.Where(u => u.PlayerNumber == 0).ToList();

            // Map units to roles by their existing RoleConfig (if any)
            for (int i = 0; i < humanUnits.Count && i < 3; i++)
            {
                var unit = humanUnits[i];
                var mapping = mappingsProp.GetArrayElementAtIndex(i);
                var cellProp = mapping.FindPropertyRelative("StartingCell");

                // Try to read starting cell coordinates from the unit's serialized field
                var unitSO = new SerializedObject(unit);
                var startingCellProp = unitSO.FindProperty("_startingCellCoordinates");
                if (startingCellProp != null)
                {
                    cellProp.vector2IntValue = new Vector2Int(
                        startingCellProp.FindPropertyRelative("x").intValue,
                        startingCellProp.FindPropertyRelative("y").intValue);
                }
                else
                {
                    // Fallback: use unit's current transform position to infer cell if possible
                    // For now, keep the default values set above
                }
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(bootstrap);

            Debug.Log("[PartyBootstrapSetupEditor] Party Bootstrap configured. Please verify StartingCell coordinates in the Inspector.");
        }

        private static void SetupMapping(SerializedProperty mappingProp, RoleType roleType, GameObject prefab, Vector2Int cell)
        {
            mappingProp.FindPropertyRelative("RoleType").enumValueIndex = (int)roleType;
            mappingProp.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
            mappingProp.FindPropertyRelative("StartingCell").vector2IntValue = cell;
        }
    }
}
