using Tactics.Tbsf.Common.Controllers;
using Tactics.Tbsf.Common.Controllers.GridStates;
using Tactics.Tbsf.Common.Players;

namespace Tactics.Tbsf.Unity.Players
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