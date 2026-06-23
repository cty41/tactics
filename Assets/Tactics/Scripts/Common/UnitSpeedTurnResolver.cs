using System;
using Tactics.Runtime.Utilities;
using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Controllers;
using Tactics.Common.Controllers.TurnResolvers;
using Tactics.Common.Units;
using UnityEngine;

namespace Tactics.Controllers.TurnResolvers
{
    /// <summary>
    /// A turn resolver that implements a unit-by-unit turn system based on unit initiative.
    /// Units take turns in a fixed cycle ordered by initiative (highest first), with stable secondary ordering.
    /// Each turn activates only one unit at a time.
    /// </summary>
    [Serializable]
    public class UnitSpeedTurnResolver : ITurnResolver
    {
        /// <summary>
        /// Internal queue of units in initiative order. This queue persists throughout the game.
        /// </summary>
        private Queue<IUnit> _unitQueue = new Queue<IUnit>();

        public TurnContext ResolveStart(GridController gridController)
        {
            // Initialize the unit queue on game start
            _unitQueue.Clear();
            var units = gridController.UnitManager.GetUnits()
                .Where(u => u.Health > 0) // Only include alive units
                .OrderByDescending(u => u.Initiative) // Initiative descending
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
        /// All units enter the turn cycle regardless of CanAct/IsDowned status.
        /// The action layer (AIPlayer/HumanPlayer) is responsible for skipping
        /// frozen/downed units' actions while still ticking their buff durations
        /// via normal OnTurnEnd lifecycle.
        /// </summary>
        /// <param name="gridController">The grid controller.</param>
        /// <returns>A TurnContext containing the next unit's turn, or empty if no units available.</returns>
        private TurnContext ResolveNextUnit(GridController gridController)
        {
            if (_unitQueue.Count == 0)
            {
                TLog.Warning("UnitSpeedTurnResolver: No units available for turn resolution.");
                return CreateEmptyTurnContext(gridController);
            }

            var nextUnit = _unitQueue.Dequeue();

            // Only skip dead/invalid units — frozen/downed units still enter their turn
            if (nextUnit.Health <= 0 || !gridController.UnitManager.GetUnits().Contains(nextUnit))
            {
                return ResolveNextUnit(gridController);
            }

            // Put the unit back at the end of the queue for the next cycle
            _unitQueue.Enqueue(nextUnit);

            var player = gridController.PlayerManager.GetPlayerByNumber(nextUnit.PlayerNumber);
            if (player == null)
            {
                TLog.Error($"[UnitSpeedTurnResolver] Unit {nextUnit} has PlayerNumber={nextUnit.PlayerNumber} but no matching player found. Skipping.");
                return ResolveNextUnit(gridController);
            }
            return new TurnContext(player, new IUnit[] { nextUnit });
        }

        /// <summary>
        /// Removes dead or invalid units from the queue.
        /// Also adds any runtime-spawned units (e.g. summoned skeletons) that are not yet in the queue.
        /// </summary>
        /// <param name="gridController">The grid controller.</param>
        private void CleanDeadUnits(GridController gridController)
        {
            var aliveUnits = gridController.UnitManager.GetUnits().Where(u => u.Health > 0).ToList();
            var aliveSet = aliveUnits.ToHashSet();
            var tempQueue = new Queue<IUnit>();

            while (_unitQueue.Count > 0)
            {
                var unit = _unitQueue.Dequeue();
                if (aliveSet.Contains(unit))
                {
                    tempQueue.Enqueue(unit);
                }
            }

            // Add runtime-spawned units not yet in queue (e.g. summoned units)
            foreach (var unit in aliveUnits)
            {
                if (!tempQueue.Contains(unit))
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