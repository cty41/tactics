using TurnBasedStrategyFramework.Common.Controllers;
using TurnBasedStrategyFramework.Common.Players;
using TurnBasedStrategyFramework.Unity.Players;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Features.Initiative
{
    /// <summary>
    /// A simple, automated player used as a "dummy" or placeholder turn, typically to allow 
    /// initiative charge time to pass without stalling the game.
    /// </summary>
    public class DummyPlayer : Player
    {
        /// <summary>
        /// The time (in ms) the DummyPlayer waits before ending its turn.
        /// </summary>
        [SerializeField] private float _delay;

        public override PlayerType PlayerType { get; set; } = PlayerType.AutomatedPlayer;

        public override void Initialize(GridController gridController)
        {
        }

        public async override void Play(GridController gridController)
        {
            // Waits for the defined delay, then immediately ends the turn.
            await Awaitable.WaitForSecondsAsync(_delay / 1000f);
            gridController.EndTurn();
        }
    }
}