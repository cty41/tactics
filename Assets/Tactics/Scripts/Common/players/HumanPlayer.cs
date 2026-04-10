using Tactics.Common.Controllers;
using Tactics.Common.Controllers.GridStates;
using Tactics.Common.Players;

namespace Tactics.Common.Players
{
    /// <summary>
    /// Unity-specific implementation of a Human player.
    /// </summary>
    public class HumanPlayer : Player
    {
        public override PlayerType PlayerType { get; set; } = PlayerType.HumanPlayer;
        public override void Initialize(GridController gridController)
        {
        }

        public override void Play(GridController gridController)
        {
            gridController.GridState = new GridStateAwaitInput();
        }
    }
}