using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Units;
using Tactics.Units;
using UnityEngine;

namespace Tactics.Roster
{
    /// <summary>
    /// Strategy A: reuse existing friendly <see cref="TilemapUnit"/> placeholders in the scene and overwrite stats from <see cref="PlayerAdventureState"/>.
    /// Must run before <see cref="Tactics.Common.Controllers.UnityGridController"/> starts the game (Awake + early execution order).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class BattlePartyBootstrap : MonoBehaviour
    {
        [SerializeField] private UnityUnitManager _unitManager;
        [SerializeField] private int _humanPlayerNumber;
        [Tooltip("If both elements are set, these are used in order; otherwise first two human TilemapUnits under UnitManager are used (child order).")]
        [SerializeField] private List<TilemapUnit> _partySlots = new List<TilemapUnit>();

        private void Awake()
        {
            if (_unitManager == null)
                _unitManager = FindFirstObjectByType<UnityUnitManager>();

            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            var slots = ResolvePartySlots();
            if (slots.Count < 2)
            {
                Debug.LogWarning("[BattlePartyBootstrap] Need 2 party slots with TilemapUnit; roster not applied.");
                return;
            }

            for (int i = 0; i < 2; i++)
            {
                string id = state.ActivePartyCharacterIds[i];
                var def = state.Roster.FirstOrDefault(c => c.Id == id);
                if (def == null)
                {
                    Debug.LogWarning($"[BattlePartyBootstrap] Party id '{id}' not in roster; skipping slot {i}.");
                    continue;
                }

                var unit = slots[i];
                CharacterStatsApplicator.ApplyToUnit(def, unit);
                var link = unit.GetComponent<RosterCharacterLink>();
                if (link == null)
                    link = unit.gameObject.AddComponent<RosterCharacterLink>();
                link.CharacterId = def.Id;
            }
        }

        private List<TilemapUnit> ResolvePartySlots()
        {
            if (_partySlots != null && _partySlots.Count >= 2 && _partySlots[0] != null && _partySlots[1] != null)
                return new List<TilemapUnit> { _partySlots[0], _partySlots[1] };

            var list = new List<TilemapUnit>();
            if (_unitManager == null)
                return list;

            Transform root = _unitManager.transform;
            for (int i = 0; i < root.childCount && list.Count < 2; i++)
            {
                var tu = root.GetChild(i).GetComponentInChildren<TilemapUnit>(true);
                if (tu != null && tu.PlayerNumber == _humanPlayerNumber)
                    list.Add(tu);
            }

            return list;
        }
    }
}
