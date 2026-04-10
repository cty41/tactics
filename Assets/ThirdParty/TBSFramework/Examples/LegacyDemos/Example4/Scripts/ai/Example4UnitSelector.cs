using System;
using System.Collections.Generic;
using System.Linq;
using TurnBasedStrategyFramework.Common.Controllers;
using TurnBasedStrategyFramework.Common.Units;
using TurnBasedStrategyFramework.Unity.AI;
using TurnBasedStrategyFramework.Unity.Units;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.AI
{
    /// <summary>
    /// A custom unit selector for Example 4 that prioritizes structures in decision-making, 
    /// selecting units based on their type and the presence of other units on the same cell.
    /// </summary>
    /// <remarks>
    /// Units are selected in the following order:
    /// 1. Structures that are alone on their cell, as these can spawn new units.
    /// 2. All regular units.
    /// 3. Remaining structures that initially had units on them but may now be free to spawn units.
    /// </remarks>
    public partial class Example4UnitSelector : UnityUnitSelector
    {
        [SerializeField] private ScriptableObject _structureUnitType;

        public override IEnumerable<IUnit> SelectNext(Func<IEnumerable<IUnit>> getUnits, GridController gridController)
        {
            var orderedUnits = getUnits()
                .GroupBy(u =>
                    (u as ITypedUnit).UnitType.Equals(_structureUnitType)
                        ? u.CurrentCell.CurrentUnits.Count == 1 ? 0 : 2 // Structures: 0 if alone, 2 if not
                        : 1) // Non-structures: 1
                .OrderBy(g => g.Key) // Ensure the groups are sorted by the key
                .SelectMany(g => g); // Flatten the grouped units into a single sequence

            return orderedUnits;
        }
    }
}