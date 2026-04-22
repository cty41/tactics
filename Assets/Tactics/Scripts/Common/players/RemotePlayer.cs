using Tactics.Common.Controllers;
using Tactics.Common.Controllers.GridStates;
using Tactics.Common.Network;
using UnityEngine;

namespace Tactics.Common.Players
{
    /// <summary>
    /// Pure C# implementation of a remote player in an online game.
    /// </summary>
    public class RemotePlayer : IPlayer
    {
        public int PlayerNumber { get; set; }
        public PlayerType PlayerType { get; set; } = PlayerType.AutomatedPlayer;

        public NetworkConnection NetworkConnection { get; set; }

        private bool _playerLeft;

        public void Initialize(GridController gridController)
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

        public void Play(GridController gridController)
        {
            gridController.GridState = new GridStateBlockInput();
            if (NetworkConnection.IsHost && _playerLeft)
            {
                gridController.EndTurn();
            }
        }
    }
}

