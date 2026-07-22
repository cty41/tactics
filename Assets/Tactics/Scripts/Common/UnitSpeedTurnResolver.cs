using System;
using Tactics.Runtime.Utilities;
using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Battle;
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
        private readonly BattleInitiativeService _initiative = new();

        public IReadOnlyList<IUnit> CurrentRoundOrder => _initiative.GetCurrentRoundOrder();

        public TurnContext ResolveStart(GridController gridController)
        {
            BattleInitiativeService.Attach(gridController, _initiative);
            _initiative.StartRound(gridController.UnitManager.GetUnits());
            return ResolveNextUnit(gridController);
        }

        public TurnContext ResolveTurn(GridController gridController)
        {
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
            var nextUnit = _initiative.TakeNext(gridController.UnitManager.GetUnits());
            if (nextUnit == null)
            {
                TLog.Warning("UnitSpeedTurnResolver: No units available for turn resolution.");
                return CreateEmptyTurnContext(gridController);
            }

            var player = gridController.PlayerManager.GetPlayerByNumber(nextUnit.PlayerNumber);
            if (player == null)
            {
                TLog.Error($"[UnitSpeedTurnResolver] Unit {nextUnit} has PlayerNumber={nextUnit.PlayerNumber} but no matching player found. Skipping.");
                return ResolveNextUnit(gridController);
            }
            return new TurnContext(player, new IUnit[] { nextUnit });
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
