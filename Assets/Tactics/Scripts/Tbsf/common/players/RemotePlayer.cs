using Tactics.Tbsf.Common.Controllers;
using Tactics.Tbsf.Common.Controllers.GridStates;
using Tactics.Tbsf.Common.Players;
using Tactics.Tbsf.Common.Network;
using UnityEngine;

namespace Tactics.Tbsf.Common.Players
{
    /// <summary>
    /// Represents a remote player in an online game.
    /// </summary>
    public class RemotePlayer : Player
    {
        public override PlayerType PlayerType { get; set; } = PlayerType.AutomatedPlayer;

        public NetworkConnection NetworkConnection { get; set; }

        private bool _playerLeft;

        public override void Initialize(GridController gridController)
        {
            NetworkConnection.PlayerLeftRoom += (sender, networkUser) =>
            {
                if (networkUser.CustomProperties.TryGetValue("playerNumber", out string leavingPlayerNumber) && PlayerNumber.Equals(int.Parse(leavingPlayerNumber)))
                {
                    Debug.Log("Remote player left");
                    _playerLeft = true;

                    if (NetworkConnection.IsHost && PlayerNumber.Equals(gridController.TurnContext.CurrentPlayer.PlayerNumber))
                    {
                        gridController.EndTurn();
                    }
                }
            };
        }

        public override void Play(GridController gridController)
        {
            gridController.GridState = new GridStateBlockInput();
            if (NetworkConnection.IsHost && _playerLeft)
            {
                gridController.EndTurn();
            }
        }
    }
}

