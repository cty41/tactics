using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Controllers;
using Tactics.Common.Controllers.TurnResolvers;
using Tactics.Common.Units;
using UnityEngine;

namespace Tactics.Controllers.TurnResolvers
{
    /// <summary>
    /// A turn resolver that implements a unit-by-unit turn system based on unit speed.
    /// Units take turns in a fixed cycle ordered by speed (highest first), with stable secondary ordering.
    /// Each turn activates only one unit at a time.
    /// </summary>
    public class UnitSpeedTurnResolver : ITurnResolver
    {
        /// <summary>
        /// Internal queue of units in speed order. This queue persists throughout the game.
        /// </summary>
        private Queue<IUnit> _unitQueue = new Queue<IUnit>();

        public TurnContext ResolveStart(GridController gridController)
        {
            // Initialize the unit queue on game start
            _unitQueue.Clear();
            var units = gridController.UnitManager.GetUnits()
                .Where(u => u.Health > 0) // Only include alive units
                .OrderByDescending(u => u.Speed) // Speed descending
                .ThenBy(u => u.PlayerNumber) // Player number ascending (stable order)
                .ThenBy(u => u.UnitID) // Unit ID ascending (stable order)
                .ToList();

            foreach (var unit in units)
            {
                _unitQueue.Enqueue(unit);
            }

            return ResolveNextUnit(gridController);
        }

        public TurnContext ResolveTurn(GridController gridController)
        {
            // Remove dead units from the queue before resolving next turn
            CleanDeadUnits(gridController);

            return ResolveNextUnit(gridController);
        }

        /// <summary>
        /// Resolves the next unit from the queue and packages it into a TurnContext.
        /// </summary>
        /// <param name="gridController">The grid controller.</param>
        /// <returns>A TurnContext containing the next unit's turn, or empty if no units available.</returns>
        private TurnContext ResolveNextUnit(GridController gridController)
        {
            if (_unitQueue.Count == 0)
            {
                // No units available - this shouldn't happen in normal play
                Debug.LogWarning("UnitSpeedTurnResolver: No units available for turn resolution.");
                return CreateEmptyTurnContext(gridController);
            }

            var nextUnit = _unitQueue.Dequeue();

            // Double-check unit is still alive and valid
            if (nextUnit.Health <= 0 || !gridController.UnitManager.GetUnits().Contains(nextUnit))
            {
                // Skip this unit and try the next one
                return ResolveNextUnit(gridController);
            }

            // Put the unit back at the end of the queue for the next cycle
            _unitQueue.Enqueue(nextUnit);

            var player = gridController.PlayerManager.GetPlayerByNumber(nextUnit.PlayerNumber);
            return new TurnContext(player, new IUnit[] { nextUnit });
        }

        /// <summary>
        /// Removes dead or invalid units from the queue.
        /// </summary>
        /// <param name="gridController">The grid controller.</param>
        private void CleanDeadUnits(GridController gridController)
        {
            var aliveUnits = gridController.UnitManager.GetUnits().Where(u => u.Health > 0).ToHashSet();
            var tempQueue = new Queue<IUnit>();

            while (_unitQueue.Count > 0)
            {
                var unit = _unitQueue.Dequeue();
                if (aliveUnits.Contains(unit))
                {
                    tempQueue.Enqueue(unit);
                }
            }

            _unitQueue = tempQueue;
        }

        /// <summary>
        /// Creates a fallback TurnContext for when no units are available.
        /// This should rarely happen but provides a safe fallback.
        /// </summary>
        /// <param name="gridController">The grid controller.</param>
        /// <returns>An empty turn context.</returns>
        private TurnContext CreateEmptyTurnContext(GridController gridController)
        {
            // Use player 0 as fallback, or first available player
            var fallbackPlayer = gridController.PlayerManager.GetPlayers().FirstOrDefault();
            return new TurnContext(fallbackPlayer, new IUnit[] { });
        }
    }
}