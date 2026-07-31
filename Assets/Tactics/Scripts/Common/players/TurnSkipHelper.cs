using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tactics.Common.Battle;
using Tactics.Common.Controllers;
using Tactics.Common.Units;

namespace Tactics.Common.Players
{
    /// <summary>
    /// Shared helper for delayed skip-turn behavior (e.g. frozen/downed units).
    /// Both AI and Human players use the same timing and safety guards.
    /// </summary>
    public static class TurnSkipHelper
    {
        public const float FrozenSkipDelaySeconds = 1f;

        public static async Task<bool> DelayAndEndTurnAsync(
            GridController controller,
            BattleController host,
            Func<bool> shouldStillSkip,
            float delaySeconds = FrozenSkipDelaySeconds,
            CancellationToken cancellationToken = default)
        {
            if (controller == null || shouldStillSkip == null)
                return false;

            var scheduledPlayer = controller.TurnContext.CurrentPlayer;
            var scheduledUnit = controller.TurnContext.PlayableUnits()?.FirstOrDefault();
            if (scheduledPlayer == null || scheduledUnit == null)
                return false;

            try
            {
                await global::Tactics.GameTimeService.DelayScaledAsync(delaySeconds, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            if (cancellationToken.IsCancellationRequested)
                return false;

            if (controller.TurnContext.CurrentPlayer != scheduledPlayer)
                return false;

            var currentUnit = controller.TurnContext.PlayableUnits()?.FirstOrDefault();
            if (!ReferenceEquals(currentUnit, scheduledUnit))
                return false;

            if (host != null && !host.IsBattleActive)
                return false;

            if (!shouldStillSkip())
                return false;

            controller.EndTurn();
            return true;
        }
    }
}
