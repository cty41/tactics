using System.Linq;
using Tactics.Common.Battle;
using Tactics.Runtime.Utilities;

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
                TLog.Error("[SubsequentTurnResolverImpl] PlayerManager.GetPlayers() returned null.");
                return default;
            }
            var playerList = players.ToList();
            if (playerList.Count == 0)
            {
                TLog.Error("[SubsequentTurnResolverImpl] PlayerManager.GetPlayers() returned empty list. Ensure players are configured in BattleController._players.");
                return default;
            }

            var nextPlayer = playerList.OrderBy(p => p.PlayerNumber).FirstOrDefault();
            if (nextPlayer == null)
            {
                TLog.Error("[SubsequentTurnResolverImpl] nextPlayer is null despite non-empty player list.");
                return default;
            }
            var allowedUnits = gridController.UnitManager.GetUnits().Where(u =>
                u.PlayerNumber == nextPlayer.PlayerNumber && !AmazonBattleState.IsDecoy(u));
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
            var currentPlayerNumber = gridController.TurnContext.CurrentPlayer != null ? gridController.TurnContext.CurrentPlayer.PlayerNumber.ToString() : "null";
            TLog.Info($"[SubsequentTurnResolverImpl] ResolveTurn: playerCount={players.Count}, currentPlayer={currentPlayerNumber}, playerNumbers=[{string.Join(",", players.Select(p => p.PlayerNumber))}]");
            if (players.Count == 0) return default;

            // 按 PlayerNumber 排序后按索引循环，而非用 PlayerNumber 取模
            var sortedPlayers = players.OrderBy(p => p.PlayerNumber).ToList();
            int currentIndex = sortedPlayers.FindIndex(p => p.PlayerNumber == gridController.TurnContext.CurrentPlayer.PlayerNumber);
            if (currentIndex < 0) currentIndex = 0;

            int numberToCheck = sortedPlayers.Count;
            for (int offset = 1; offset <= numberToCheck; offset++)
            {
                int nextIndex = (currentIndex + offset) % numberToCheck;
                var candidate = sortedPlayers[nextIndex];
                if (candidate == null) continue;
                bool hasUnits = gridController.UnitManager.GetUnits().Any(u =>
                    u.PlayerNumber == candidate.PlayerNumber && !AmazonBattleState.IsDecoy(u));
                if (hasUnits)
                {
                    var allowedUnits = gridController.UnitManager.GetUnits().Where(u =>
                        u.PlayerNumber == candidate.PlayerNumber && !AmazonBattleState.IsDecoy(u));
                    return new TurnContext(candidate, allowedUnits);
                }
            }

            return default;
        }
    }
}
