using TurnBasedStrategyFramework.Common.Controllers;
using TurnBasedStrategyFramework.Common.Controllers.GridStates;
using TurnBasedStrategyFramework.Common.Players;

namespace TurnBasedStrategyFramework.Unity.Players
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