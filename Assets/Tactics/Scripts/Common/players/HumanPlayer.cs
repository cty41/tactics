using System.Collections;
using System.Linq;
using Tactics.Common.Battle;
using Tactics.Common.Controllers;
using Tactics.Common.Controllers.GameResolvers;
using Tactics.Common.Controllers.GridStates;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.Common.Players
{
    /// <summary>
    /// Pure C# implementation of a Human player.
    /// Switches grid state to await player input.
    /// If no playable unit can act (e.g., all frozen), auto-EndTurn after one frame
    /// to avoid synchronous recursive MakeTurnTransition chains.
    /// All scheduling state is instance-level — no cross-battle static interference.
    /// </summary>
    public class HumanPlayer : IPlayer
    {
        public int PlayerNumber { get; set; }
        public PlayerType PlayerType { get; set; } = PlayerType.HumanPlayer;

        private GridController _boundGridController;
        private BattleController _host;
        private bool _endTurnScheduled;
        private bool _subscriptionInstalled;

        public void Initialize(GridController gridController)
        {
            Cleanup();
            _boundGridController = gridController;
            // Always prefer a live BattleController host — even if the first Initialize()
            // call happened before Awake(), retry the singleton so we can use the coroutine
            // path (which is reliable) rather than the async fallback.
            _host = BattleController.Instance;
            InstallSubscriptions(gridController);
        }

        public void Play(GridController gridController)
        {
            var playableUnits = gridController.TurnContext.PlayableUnits();
            if (playableUnits != null && playableUnits.Any(u => u.CanAct && !u.IsDowned))
            {
                var unit = playableUnits.First();
                // TEMP: diagnostic log for freeze bug investigation — remove after fix confirmed
                TLog.Info($"[HumanPlayer] AwaitInput: CanAct={unit.CanAct}, IsDowned={unit.IsDowned}, Player={unit.PlayerNumber}");
                gridController.GridState = new GridStateAwaitInput();
                return;
            }

            // TEMP: diagnostic log for freeze bug investigation — remove after fix confirmed
            TLog.Info($"[HumanPlayer] AutoEndTurn: no actionable unit");
            gridController.GridState = new GridStateBlockInput();
            ScheduleAutoEndTurn(gridController);
        }

        private void InstallSubscriptions(GridController gridController)
        {
            if (_subscriptionInstalled)
            {
                return;
            }

            _subscriptionInstalled = true;
            gridController.GameEnded += OnGameEnded;
            // Also subscribe to BattleController.BattleEnded: EndBattle() does NOT trigger
            // GridController.GameEnded, so without this the queued auto-EndTurn may fire
            // on an already-ended battle.
            if (_host != null)
            {
                _host.BattleEnded += OnBattleEnded;
            }
        }

        private void OnGameEnded(GameResult _)
        {
            Cleanup();
        }

        private void OnBattleEnded(GameResult _)
        {
            Cleanup();
        }

        private void Cleanup()
        {
            _endTurnScheduled = false;
            if (_boundGridController != null)
            {
                _boundGridController.GameEnded -= OnGameEnded;
                _boundGridController = null;
            }

            if (_host != null)
            {
                _host.BattleEnded -= OnBattleEnded;
            }

            _subscriptionInstalled = false;
            _host = null;
        }

        private void ScheduleAutoEndTurn(GridController gridController)
        {
            if (_endTurnScheduled)
            {
                return;
            }

            _endTurnScheduled = true;
            // Prefer coroutine path (reliable in all contexts). If _host is null,
            // try BattleController.Instance as fallback host before resorting to async.
            var coroutineHost = _host ?? BattleController.Instance;
            if (coroutineHost != null)
            {
                coroutineHost.StartCoroutine(AutoEndTurnAfterOneFrame(gridController));
            }
            else
            {
                // Fallback: no MonoBehaviour host available — schedule to the next frame
                // so the first unactionable human turn does not deadlock waiting for TurnEnded.
                AutoEndTurnAfterOneFrameWithoutHost(gridController);
            }
        }

        private async void AutoEndTurnAfterOneFrameWithoutHost(GridController gridController)
        {
            await Awaitable.NextFrameAsync();
            ExecutePendingEndTurn(gridController);
        }

        private IEnumerator AutoEndTurnAfterOneFrame(GridController gridController)
        {
            yield return null;

            _endTurnScheduled = false;

            if (!ShouldStillEndTurn(gridController))
            {
                yield break;
            }

            gridController.EndTurn();
        }

        private void ExecutePendingEndTurn(GridController gridController)
        {
            if (!_endTurnScheduled)
            {
                return;
            }

            _endTurnScheduled = false;

            if (!ShouldStillEndTurn(gridController))
            {
                return;
            }

            gridController.EndTurn();
        }

        /// <summary>
        /// Guard checks before invoking EndTurn on a previously-queued auto-end.
        /// Returns false (and drops the queued request) if any of the following fail:
        /// 1. The bound controller is still the same one that queued the request.
        /// 2. The host battle (if any) is still active.
        /// 3. The current turn still has no actionable unit.
        /// </summary>
        private bool ShouldStillEndTurn(GridController gridController)
        {
            // The bound controller must still be the one that scheduled this auto-end.
            // A null or different controller means Cleanup() ran or a new battle started.
            if (gridController == null || gridController != _boundGridController)
            {
                return false;
            }

            // If we have a host battle, it must still be active — EndBattle()/BattleEnded
            // must not have fired between scheduling and execution.
            // When _host is null (fallback path), check BattleController.Instance directly
            // so a same-frame EndBattle() still blocks the queued auto-EndTurn.
            if (_host != null)
            {
                if (!_host.IsBattleActive)
                    return false;
            }
            else
            {
                var instance = BattleController.Instance;
                // If the singleton was destroyed or battle ended, discard the queued request.
                if (instance == null || !instance.IsBattleActive)
                    return false;
            }

            // The turn must still be unactionable; otherwise a real input may have started.
            var playableFunc = gridController.TurnContext.PlayableUnits;
            var units = playableFunc?.Invoke();
            if (units == null || units.Any(u => u.CanAct && !u.IsDowned))
            {
                return false;
            }

            return true;
        }
    }
}
