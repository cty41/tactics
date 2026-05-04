using System;
using System.Collections.Generic;
using System.Linq;
using Tactics.AssetPipeline;
using Tactics.Common.Battle;
using Tactics.Common.Cells;
using Tactics.Common.Units.Classes;
using Tactics.Common.Utilities;
using Tactics.Units;
using UnityEngine;

namespace Tactics.Roster
{
    [Serializable]
    public class RolePrefabMapping
    {
        public RoleType RoleType;
        public GameObject Prefab;
        public Vector2Int StartingCell;
    }

    /// <summary>
    /// Spawns party units dynamically based on <see cref="PlayerAdventureState.ActivePartyCharacterIds"/>.
    /// Must run before <see cref="Tactics.Common.Battle.BattleController"/> starts the game.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class BattlePartyBootstrap : MonoBehaviour
    {
        [SerializeField] private int _humanPlayerNumber;

        [Tooltip("Deprecated: dynamic generation now uses RolePrefabMappings instead of scene placeholders.")]
        [SerializeField] private List<TilemapUnit> _partySlots = new List<TilemapUnit>();

        [SerializeField] private List<RolePrefabMapping> _rolePrefabMappings = new List<RolePrefabMapping>();

        private readonly HashSet<string> _loadedPaths = new HashSet<string>();

        private void OnDestroy()
        {
            var mgr = GameAssetManager.Instance;
            if (mgr == null) return;
            foreach (var path in _loadedPaths)
                mgr.Release(path);
            _loadedPaths.Clear();
        }

        private void Start()
        {
            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            if (state?.ActivePartyCharacterIds == null || state.ActivePartyCharacterIds.Count == 0)
            {
                Debug.LogWarning("[BattlePartyBootstrap] No active party characters found.");
                return;
            }

            var battleController = FindFirstObjectByType<BattleController>();
            if (battleController == null)
            {
                Debug.LogError("[BattlePartyBootstrap] BattleController not found in scene.");
                return;
            }

            Transform container = battleController.UnitContainerTransform;
            if (container == null)
                container = battleController.transform;

            // Remove old placeholder units before spawning new ones
            var unitManager = battleController.UnitManager;
            var existingUnits = FindObjectsByType<TilemapUnit>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var existing in existingUnits)
            {
                if (existing.PlayerNumber == _humanPlayerNumber)
                {
                    unitManager?.RemoveUnit(existing);
                    Destroy(existing.gameObject);
                }
            }

            // Build prefab lookup: TestParty.json > Inspector mappings > fallback
            var prefabLookup = new Dictionary<RoleType, GameObject>();
            var mgr = GameAssetManager.Instance;

            foreach (var jsonMapping in PlayerAdventureStateStore.TestPrefabMappings)
            {
                if (string.IsNullOrEmpty(jsonMapping.PrefabPath))
                    continue;

                GameObject prefab = null;
                if (mgr != null)
                {
                    var resolvedPath = CharacterDefinition.ResolvePrefabPath(jsonMapping.PrefabPath);
                    if (!string.IsNullOrEmpty(resolvedPath))
                        prefab = mgr.Load<GameObject>(resolvedPath);
                }

                if (prefab != null)
                {
                    _loadedPaths.Add(jsonMapping.PrefabPath);
                    prefabLookup[jsonMapping.RoleType] = prefab;
                }
            }

            foreach (var mapping in _rolePrefabMappings)
            {
                if (mapping.Prefab != null && !prefabLookup.ContainsKey(mapping.RoleType))
                    prefabLookup[mapping.RoleType] = mapping.Prefab;
            }

            GameObject fallbackPrefab = prefabLookup.Values.FirstOrDefault();

            // Find Respawn spawnpoints under UnitManager
            var respawnPoints = new List<Transform>();
            var unitManagerGo = container.gameObject;
            if (unitManagerGo != null)
            {
                foreach (Transform child in unitManagerGo.transform)
                {
                    if (child.CompareTag("Respawn"))
                        respawnPoints.Add(child);
                }
            }

            for (int i = 0; i < state.ActivePartyCharacterIds.Count; i++)
            {
                string id = state.ActivePartyCharacterIds[i];
                var def = state.Roster.FirstOrDefault(c => c.Id == id);
                if (def == null)
                {
                    Debug.LogWarning($"[BattlePartyBootstrap] Party id '{id}' not in roster; skipping slot {i}.");
                    continue;
                }

                GameObject prefab = null;

                // Priority 1: Character's own PrefabPath
                var characterPath = CharacterDefinition.ResolvePrefabPath(def.PrefabPath);
                if (!string.IsNullOrEmpty(characterPath) && mgr != null)
                {
                    prefab = mgr.Load<GameObject>(characterPath);
                    if (prefab != null)
                        _loadedPaths.Add(characterPath);
                }

                // Priority 2: RoleType → prefabLookup
                if (prefab == null && !prefabLookup.TryGetValue(def.RoleType, out prefab))
                    prefab = null;

                // Priority 3: fallback
                if (prefab == null)
                {
                    prefab = fallbackPrefab;
                    if (prefab == null)
                    {
                        Debug.LogError($"[BattlePartyBootstrap] No prefab for {def.Id} (path={def.PrefabPath}, role={def.RoleType}) and no fallback available.");
                        continue;
                    }
                    Debug.LogWarning($"[BattlePartyBootstrap] No prefab for {def.Id}, using fallback.");
                }

                var go = Instantiate(prefab, container);
                go.name = $"PartyUnit_{def.DisplayName}";

                var unit = go.GetComponent<TilemapUnit>();
                if (unit == null)
                {
                    Debug.LogError($"[BattlePartyBootstrap] Prefab for {def.RoleType} does not have a TilemapUnit component.");
                    Destroy(go);
                    continue;
                }

                unit.PlayerNumber = _humanPlayerNumber;

                // Set world position from Respawn objects; TilemapUnit.Initialize will resolve CurrentCell later.
                if (i < respawnPoints.Count)
                {
                    go.transform.position = respawnPoints[i].position;
                }
                else
                {
                    // Fallback: position adjacent to Infantry Blue if present, else default grid origin
                    var referenceUnit = GameObject.Find("Infantry Blue");
                    if (referenceUnit != null)
                    {
                        go.transform.position = referenceUnit.transform.position + new Vector3(i * 2.5f, 0, 0);
                    }
                    else
                    {
                        Debug.LogWarning($"[BattlePartyBootstrap] No Respawn point for slot {i} and no Infantry Blue reference. Using prefab default position.");
                    }
                }

                CharacterStatsApplicator.ApplyToUnit(def, unit);

                var link = unit.GetComponent<RosterCharacterLink>();
                if (link == null)
                    link = unit.gameObject.AddComponent<RosterCharacterLink>();
                link.CharacterId = def.Id;
            }
        }
    }
}
