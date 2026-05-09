using System;
using Tactics.Runtime.Utilities;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tactics.Common.AI;
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
    /// The AIPlayer selects and commands units during its turn using behavior trees for decision making.
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

        private void CancelOngoingAction()
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

                var playableUnits = gridController.TurnContext.PlayableUnits().ToList();
                foreach (var playableUnit in UnitSelector.SelectNext(() => playableUnits, gridController))
                {
                    if (_cancellationTokenSource.IsCancellationRequested)
                    {
                        return;
                    }

                    await gridController.UnitManager.MarkAsSelected(playableUnit);
                    playableUnit.InvokeUnitSelected();

                    if (DebugMode)
                    {
                        TLog.Info($"Current unit: {playableUnit}; Press {Key.N} to proceed to the next action");
                        await WaitForKeypress(Key.N);
                    }

                    await Awaitable.WaitForSecondsAsync(UnitDelay / 1000f, _cancellationTokenSource.Token);
                    if (playableUnit.BehaviourTree == null)
                    {
                        TLog.Error($"[AIPlayer] Unit {playableUnit} has null BehaviourTree. Skipping.");
                        continue;
                    }
                    await playableUnit.BehaviourTree.Execute(DebugMode);

                    await gridController.UnitManager.MarkAsFriendly(new IUnit[] { playableUnit });
                    await gridController.UnitManager.MarkAsFinished(new IUnit[] { playableUnit });
                    playableUnit.InvokeUnitDeselected();
                }

                gridController.EndTurn();
            }
            catch (Exception ex)
            {
                TLog.Error($"[AIPlayer] Exception during Play() for Player {PlayerNumber}: {ex}");
            }
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
