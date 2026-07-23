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
        /// Executes the AI player's turn by selecting and commanding units in sequence.
        /// </summary>
        public async void Play(GridController gridController)
        {
            try
            {
                await Awaitable.WaitForSecondsAsync(TurnStartDelay / 1000f);
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                var playableUnits = gridController.TurnContext.PlayableUnits()
                    .Where(IsUnityUnitAvailable)
                    .ToList();
                foreach (var playableUnit in UnitSelector.SelectNext(() => playableUnits, gridController))
                {
                    if (_cancellationTokenSource.IsCancellationRequested)
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
                            _cancellationTokenSource.Token);

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
                        await WaitForKeypress(Key.N);
                    }

                    await Awaitable.WaitForSecondsAsync(UnitDelay / 1000f, _cancellationTokenSource.Token);
                    if (!IsUnityUnitAvailable(playableUnit))
                        continue;

                    if (playableUnit is Unit concreteUnit && concreteUnit.AiBrainAsset != null)
                    {
                        TLog.Info($"[AIPlayer] EXECUTE AI: CanAct={playableUnit.CanAct}, Brain={concreteUnit.AiBrainAsset.name}, MovePts={concreteUnit.MovementPoints:F1}, Player={playableUnit.PlayerNumber}");
                        await AiBrainRunner.Execute(playableUnit, gridController, concreteUnit.AiBrainAsset);
                    }
                    else
                    {
                        TLog.Error($"[AIPlayer] Unit {playableUnit} has no AiBrainAsset configured. Skipping.");
                        continue;
                    }

                    if (!IsUnityUnitAvailable(playableUnit))
                        continue;

                    await gridController.UnitManager.MarkAsFriendly(new IUnit[] { playableUnit });
                    await gridController.UnitManager.MarkAsFinished(new IUnit[] { playableUnit });
                    playableUnit.InvokeUnitDeselected();
                }
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
        private async Task WaitForKeypress(Key key)
        {
            KeyControl keyControl = Keyboard.current[key];
            while (!keyControl.wasPressedThisFrame)
            {
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
