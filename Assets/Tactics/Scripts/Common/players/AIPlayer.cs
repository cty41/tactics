using System;
using Tactics.Runtime.Utilities;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tactics.Common.AI;
using Tactics.Common.AI.MonsterAI;
using Tactics.Common.Battle;
using Tactics.Common.Controllers;
using Tactics.Common.Controllers.GameResolvers;
using Tactics.Common.Units;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;


namespace Tactics.Common.Players
{
    /// <summary>
    /// Pure C# implementation of an AI-controlled player.
    /// The AIPlayer selects and commands units during its turn using AiBrainRunner for decision making.
    /// </summary>

    public class AIPlayer : IPlayer
    {
        public int PlayerNumber { get; set; }
        public PlayerType PlayerType { get; set; } = PlayerType.AutomatedPlayer;

        public bool DebugMode;
        public int TurnStartDelay;
        public int UnitDelay;
        public IUnitSelector UnitSelector;

        private CancellationTokenSource _cancellationTokenSource;

        public AIPlayer()
        {
            UnitSelector = new SubsequentUnitSelector();
        }

        public AIPlayer(bool debugMode, int turnStartDelay, int unitDelay)
        {
            DebugMode = debugMode;
            TurnStartDelay = turnStartDelay;
            UnitDelay = unitDelay;
            UnitSelector = new SubsequentUnitSelector();
        }

        public void Initialize(GridController gridController)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            gridController.GameEnded += OnGameEnded;
            gridController.TurnEnded += OnTurnEnded;
        }

        private void OnTurnEnded(TurnTransitionParams turnTransitionParams)
        {
            CancelOngoingAction();
        }

        private void OnGameEnded(GameResult gameResult)
        {
            CancelOngoingAction();
        }

        internal void CancelOngoingAction()
        {
            _cancellationTokenSource?.Cancel();
        }

        /// <summary>
        /// Test seam: when set, replaces the brain execution call so mid-action
        /// cancellation can be exercised deterministically. Null in production.
        /// </summary>
        private Func<IUnit, GridController, AiBrainAsset, CancellationToken, Task> _brainExecutor;

        /// <summary>
        /// Executes the AI player's turn by selecting and commanding units in sequence.
        /// </summary>
        public async void Play(GridController gridController)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _cancellationTokenSource.Token;

            try
            {
                await global::Tactics.GameTimeService.DelayScaledAsync(
                    TurnStartDelay / 1000f,
                    cancellationToken);

                var playableUnits = gridController.TurnContext.PlayableUnits()
                    .Where(unit => IsUnityUnitAvailable(unit) && unit.PlayerNumber == PlayerNumber)
                    .ToList();
                foreach (var playableUnit in UnitSelector.SelectNext(() => playableUnits, gridController))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    if (!IsUnityUnitAvailable(playableUnit))
                        continue;

                    await gridController.UnitManager.MarkAsSelected(playableUnit);
                    playableUnit.InvokeUnitSelected();

                    // Frozen/downed units enter their turn (buff ticks via OnTurnEnd)
                    // but skip action execution entirely
                    if (!playableUnit.CanAct || playableUnit.IsDowned)
                    {
                        int skipBuffs = (playableUnit as Tactics.Common.Units.Unit)?.BuffComponent?.GetActiveBuffs()?.Count ?? 0;
                        // TEMP: diagnostic log for freeze bug investigation — remove after fix confirmed
                        TLog.Info($"[AIPlayer] SKIP frozen/downed: CanAct={playableUnit.CanAct}, IsDowned={playableUnit.IsDowned}, Buffs={skipBuffs}, Player={playableUnit.PlayerNumber}");
                        bool endedTurn = await TurnSkipHelper.DelayAndEndTurnAsync(
                            gridController,
                            BattleController.Instance,
                            () => !playableUnit.CanAct || playableUnit.IsDowned,
                            TurnSkipHelper.FrozenSkipDelaySeconds,
                            cancellationToken);

                        if (endedTurn)
                        {
                            await gridController.UnitManager.MarkAsFriendly(new IUnit[] { playableUnit });
                            await gridController.UnitManager.MarkAsFinished(new IUnit[] { playableUnit });
                            playableUnit.InvokeUnitDeselected();
                        }

                        return;
                    }

                    if (DebugMode)
                    {
                        TLog.Info($"Current unit: {playableUnit}; Press {Key.N} to proceed to the next action");
                        await WaitForKeypress(Key.N, cancellationToken);
                    }

                    await global::Tactics.GameTimeService.DelayScaledAsync(
                        UnitDelay / 1000f,
                        cancellationToken);
                    if (!IsUnityUnitAvailable(playableUnit))
                        continue;

                    if (playableUnit is Unit concreteUnit && concreteUnit.AiBrainAsset != null)
                    {
                        TLog.Info($"[AIPlayer] EXECUTE AI: CanAct={playableUnit.CanAct}, Brain={concreteUnit.AiBrainAsset.name}, MovePts={concreteUnit.MovementPoints:F1}, Player={playableUnit.PlayerNumber}");
                        if (_brainExecutor != null)
                        {
                            await _brainExecutor(playableUnit, gridController, concreteUnit.AiBrainAsset, cancellationToken);
                        }
                        else
                        {
                            await AiBrainRunner.Execute(playableUnit, gridController, concreteUnit.AiBrainAsset, cancellationToken);
                        }
                    }
                    else
                    {
                        TLog.Warning($"[AIPlayer] Unit {playableUnit} has no AiBrainAsset configured. " +
                            "Skipping this unit for the current turn.");
                        await gridController.UnitManager.MarkAsFriendly(new IUnit[] { playableUnit });
                        await gridController.UnitManager.MarkAsFinished(new IUnit[] { playableUnit });
                        playableUnit.InvokeUnitDeselected();
                        continue;
                    }

                    // Cancellation that landed while the AI action ran must stop the
                    // post-action finalization (mark friendly/finished, deselect).
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!IsUnityUnitAvailable(playableUnit))
                        continue;

                    await gridController.UnitManager.MarkAsFriendly(new IUnit[] { playableUnit });
                    await gridController.UnitManager.MarkAsFinished(new IUnit[] { playableUnit });
                    playableUnit.InvokeUnitDeselected();
                }
                cancellationToken.ThrowIfCancellationRequested();
                gridController.EndTurn();
            }
            catch (OperationCanceledException)
            {
                // Turn transitions and battle shutdown cancel in-flight delays by design.
            }
            catch (Exception ex)
            {
                TLog.Error($"[AIPlayer] Exception during Play() for Player {PlayerNumber}: {ex}");
            }
        }

        private static bool IsUnityUnitAvailable(IUnit unit)
        {
            return unit != null &&
                (unit is not UnityEngine.Object unityObject || unityObject != null);
        }

        /// <summary>
        /// Waits for the user to press the specified key in debug mode before continuing.
        /// </summary>
        private async Task WaitForKeypress(Key key, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            KeyControl keyControl = Keyboard.current[key];
            while (!keyControl.wasPressedThisFrame)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
