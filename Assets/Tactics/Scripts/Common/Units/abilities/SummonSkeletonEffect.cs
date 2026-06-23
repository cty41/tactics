using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Interactables;
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
            await Task.CompletedTask;
        }

        /// <summary>
        /// Execute summon using Corpse interactable targets.
        /// </summary>
        public async Task ExecuteWithCorpses(IUnit caster, IEnumerable<Corpse> corpses, IGridController gridController)
        {
            foreach (var corpse in corpses)
            {
                if (corpse == null || corpse.IsDestroyed) continue;

                if (caster.SummonedUnit != null && !caster.SummonedUnit.IsDowned)
                {
                    TLog.Info($"[SummonSkeletonEffect] Caster {caster.UnitID} already has a living skeleton.");
                    continue;
                }

                ICell corpseCell = corpse.CurrentCell;
                if (corpseCell == null) continue;

                corpse.Consume();

                if (_skeletonPrefab != null)
                {
                    var skeletonGO = UnityEngine.Object.Instantiate(_skeletonPrefab, corpseCell.WorldPosition.ToVector3(), Quaternion.identity);
                    var skeletonUnit = skeletonGO.GetComponent<IUnit>();
                    if (skeletonUnit != null)
                    {
                        skeletonUnit.OwnerUnitId = caster.UnitID;
                        skeletonUnit.PlayerNumber = caster.PlayerNumber;
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
