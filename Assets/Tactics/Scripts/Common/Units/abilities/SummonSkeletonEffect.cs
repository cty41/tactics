using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Interactables;
using Tactics.Common.Units;
using Tactics.Common.Utilities;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// Summons a skeleton warrior at the corpse location, consuming the corpse.
    /// Only one skeleton per necromancer. Skeleton joins turn cycle next round.
    /// </summary>
    [Serializable]
    public class SummonSkeletonEffect : AbilityEffect
    {
        [SerializeField] private GameObject _skeletonPrefab;

        public GameObject SkeletonPrefab => _skeletonPrefab;

        public override async Task Execute(IUnit caster, IEnumerable<IUnit> targets, IGridController gridController)
        {
            foreach (var target in targets)
            {
                if (target == null || !target.IsCorpse) continue;

                // Check summon limit
                if (caster.SummonedUnit != null && !caster.SummonedUnit.IsDowned)
                {
                    TLog.Info($"[SummonSkeletonEffect] Caster {caster.UnitID} already has a living skeleton.");
                    continue;
                }

                ICell corpseCell = target.CurrentCell;
                if (corpseCell == null) continue;

                // Consume Corpse interactable
                var corpse = corpseCell.CurrentInteractables
                    .FirstOrDefault(i => i is Corpse && !i.IsDestroyed) as Corpse;
                if (corpse != null)
                {
                    corpse.Consume();
                }

                // Remove the dead unit from cell
                corpseCell.CurrentUnits.Remove(target);
                corpseCell.IsTaken = corpseCell.CurrentUnits.Count > 0 || corpseCell.CurrentInteractables.Any(i => i.OccupiesCell);
                target.RemoveFromGame();

                // Spawn skeleton unit
                if (_skeletonPrefab != null)
                {
                    var skeletonGO = GameObject.Instantiate(_skeletonPrefab, corpseCell.WorldPosition.ToVector3(), Quaternion.identity);
                    var skeletonUnit = skeletonGO.GetComponent<IUnit>();
                    if (skeletonUnit != null)
                    {
                        skeletonUnit.OwnerUnitId = caster.UnitID;
                        skeletonUnit.CurrentCell = corpseCell;
                        corpseCell.CurrentUnits.Add(skeletonUnit);
                        corpseCell.IsTaken = true;

                        caster.SummonedUnit = skeletonUnit;

                        gridController.UnitManager.AddUnit(skeletonUnit);
                        skeletonUnit.Initialize(gridController);

                        TLog.Info($"[SummonSkeletonEffect] Skeleton summoned for caster {caster.UnitID} at {corpseCell.GridCoordinates}");
                    }
                }
            }
            await Task.CompletedTask;
        }
    }
}
