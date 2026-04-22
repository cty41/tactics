using System;
using Tactics.Common.Controllers;
using Tactics.Common.Controllers.GridStates;

namespace Tactics.Common.Players
{
    /// <summary>
    /// Pure C# implementation of a Human player.
    /// Switches grid state to await player input.
    /// </summary>
    public class HumanPlayer : IPlayer
    {
        public int PlayerNumber { get; set; }
        public PlayerType PlayerType { get; set; } = PlayerType.HumanPlayer;

        public void Initialize(GridController gridController)
        {
        }

        public void Play(GridController gridController)
        {
            gridController.GridState = new GridStateAwaitInput();
        }
    }
}
