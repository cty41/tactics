using System.Collections.Generic;
using System.Linq;
using TurnBasedStrategyFramework.Common.Controllers;
using TurnBasedStrategyFramework.Common.Controllers.TurnResolvers;
using TurnBasedStrategyFramework.Common.Units;
using TurnBasedStrategyFramework.Unity.Controllers;
using TurnBasedStrategyFramework.Unity.Units;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Features.Initiative
{
    /// <summary>
    /// A turn resolver that implements an Initiative-based system (similar to Active Time Battle).
    /// Units accumulate "charge" based on their Initiative stat. Once the charge crosses a threshold, 
    /// the unit is added to a queue to take their turn.
    /// </summary>
    public class InitiativeTurnResolver : UnityTurnResolver
    {
        /// <summary>
        /// The number of a non-active player to assign empty turns to (used when no units are ready to act).
        /// </summary>
        [SerializeField] private int _dummyPlayerNumber;
        /// <summary>
        /// A multiplier applied to the Initiative stat when calculating charge accumulation speed.
        /// </summary>
        [SerializeField] private float _multiplier = 1;
        /// <summary>
        /// The charge value a unit must reach to take a turn.
        /// </summary>
        [SerializeField] private float _threshold = 1;

        /// <summary>
        /// Internal queue of units that have crossed the threshold and are waiting to take their turn.
        /// </summary>
        private Queue<IUnit> _unitQueue = new Queue<IUnit>();

        public override TurnContext ResolveStart(GridController gridController)
        {
            return ResolveInternal(gridController);
        }
        public override TurnContext ResolveTurn(GridController gridController)
        {
            return ResolveInternal(gridController);
        }

        /// <summary>
        /// Shared logic for determining the next unit. Checks the queue count; if empty,
        /// forces an initiative charge calculation cycle.
        /// </summary>
        /// <returns>A TurnContext containing the next unit, or an empty context if no one is ready.</returns>
        private TurnContext ResolveInternal(GridController gridController)
        {
            if (_unitQueue.Count == 0)
            {
                ProcessInitiativeCharge(gridController);
            }

            return _unitQueue.Count > 0 ? DequeueNextUnit(gridController) : CreateEmptyTurnContext(gridController);
        }

        /// <summary>
        /// Iterates through all units on the grid, increases their internal charge based on their 
        /// Initiative stat, and enqueues any units that cross the <see cref="_threshold"/>.
        /// </summary>
        private void ProcessInitiativeCharge(GridController gridController)
        {
            // Sorts by initiative so high-initiative units enter the queue first if multiple units 
            // cross the threshold in the same tick.
            var unitsOrdered = gridController.UnitManager.GetUnits()
                .Select(u => new { Unit = u, InitiativeComponent = (u as Unit).GetComponent<InitiativeComponent>() })
                .OrderByDescending(u => u.InitiativeComponent.Initiative);

            foreach (var unit in unitsOrdered)
            {
                // Accumulate charge
                unit.InitiativeComponent.ModifyCharge(unit.InitiativeComponent.Initiative * _multiplier);

                // Check if ready to act
                if (unit.InitiativeComponent.Charge >= _threshold)
                {
                    // Spend the charge threshold (keep remainder for next cycle)
                    unit.InitiativeComponent.ModifyCharge(-_threshold);
                    _unitQueue.Enqueue(unit.Unit);
                }
            }
        }

        /// <summary>
        /// Removes the next ready unit from the queue and packages it into a TurnContext.
        /// </summary>
        /// <returns>A context containing the active player and the specific unit acting.</returns>
        private TurnContext DequeueNextUnit(GridController gridController)
        {
            var nextUnit = _unitQueue.Dequeue();
            return new TurnContext(gridController.PlayerManager.GetPlayerByNumber(nextUnit.PlayerNumber), new IUnit[] { nextUnit });
        }

        /// <summary>
        /// Creates a fallback TurnContext for a dummy player. This is returned when 
        /// no units are ready to act.
        /// </summary>
        /// <returns>An empty context representing a passing moment of time.</returns>
        private TurnContext CreateEmptyTurnContext(GridController gridController)
        {
            return new TurnContext(gridController.PlayerManager.GetPlayerByNumber(_dummyPlayerNumber), new IUnit[] { });
        }
    }
}