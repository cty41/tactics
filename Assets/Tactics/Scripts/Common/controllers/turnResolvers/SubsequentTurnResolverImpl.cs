using System.Linq;

namespace Tactics.Common.Controllers.TurnResolvers
{
    /// <summary>
    /// Implementation of <see cref="ITurnResolver"/> that handles resolving turns sequentially for all players in the game.
    /// </summary>
    public readonly struct SubsequentTurnResolverImpl : ITurnResolver
    {
        /// <summary>
        /// Resolves the start of the game by selecting the first player and their units.
        /// The first player is chosen based on the lowest player number.
        /// </summary>
        /// <param name="gridController">The grid controller.</param>
        /// <returns>The turn context representing the initial player's turn.</returns>
        public readonly TurnContext ResolveStart(GridController gridController)
        {
            var players = gridController.PlayerManager.GetPlayers();
            if (players == null)
            {
                UnityEngine.Debug.LogError("[SubsequentTurnResolverImpl] PlayerManager.GetPlayers() returned null.");
                return default;
            }
            var playerList = players.ToList();
            if (playerList.Count == 0)
            {
                UnityEngine.Debug.LogError("[SubsequentTurnResolverImpl] PlayerManager.GetPlayers() returned empty list. Ensure players are configured in BattleController._players.");
                return default;
            }

            var nextPlayer = playerList.OrderBy(p => p.PlayerNumber).FirstOrDefault();
            if (nextPlayer == null)
            {
                UnityEngine.Debug.LogError("[SubsequentTurnResolverImpl] nextPlayer is null despite non-empty player list.");
                return default;
            }
            var allowedUnits = gridController.UnitManager.GetUnits().Where(u => u.PlayerNumber == nextPlayer.PlayerNumber);
            return new TurnContext(nextPlayer, allowedUnits);
        }

        /// <summary>
        /// Resolves the next player's turn based on the current player's position in the turn order.
        /// It ensures that the turn moves sequentially to the next player who has units available.
        /// </summary>
        /// <param name="gridController">The grid controller.</param>
        /// <returns>The turn context representing the next player's turn.</returns>
        public readonly TurnContext ResolveTurn(GridController gridController)
        {
            var players = gridController.PlayerManager.GetPlayers().ToList();
            if (players.Count == 0) return default;
            var numberOfPlayers = players.Count();
            var nextPlayerNumber = (gridController.TurnContext.CurrentPlayer.PlayerNumber + 1) % numberOfPlayers;

            while (!gridController.UnitManager.GetUnits().Where(u => u.PlayerNumber.Equals(nextPlayerNumber)).Any())
            {
                nextPlayerNumber = (nextPlayerNumber + 1) % numberOfPlayers;
            }

            var nextPlayer = players.FirstOrDefault(p => p.PlayerNumber == nextPlayerNumber);
            var allowedUnits = gridController.UnitManager.GetUnits().Where(u => u.PlayerNumber == nextPlayerNumber);

            return new TurnContext(nextPlayer, allowedUnits);
        }
    }
}