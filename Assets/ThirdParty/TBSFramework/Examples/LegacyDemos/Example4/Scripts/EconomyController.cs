using System.Collections.Generic;
using TurnBasedStrategyFramework.Unity.Controllers;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4
{
    /// <summary>
    /// Tracks each player's currency in the game. Initializes player balances at the start and provides methods 
    /// to retrieve and update their currency.
    /// </summary>
    public class EconomyController : MonoBehaviour
    {
        [SerializeField] private UnityGridController _gridController;
        [SerializeField] private int _initialAmount = 5000;

        private Dictionary<int, int> _account = new Dictionary<int, int>();

        private void OnEnable()
        {
            _gridController.GameStarted += OnGameStarted;
        }

        private void OnDisable()
        {
            _gridController.GameStarted -= OnGameStarted;
        }

        private void OnGameStarted()
        {
            foreach (var player in _gridController.PlayerManager.GetPlayers())
            {
                _account[player.PlayerNumber] = _initialAmount;
            }
        }

        /// <summary>
        /// Retrieves the current currency value for the specified player.
        /// </summary>
        public int GetValue(int playerNumber)
        {
            return _account.TryGetValue(playerNumber, out int value) ? value : 0;
        }

        /// <summary>
        /// Adds or subtracts from the player's currency value.
        /// </summary>
        public void UpdateValue(int playerNumber, int delta)
        {
            Debug.Assert(_account.ContainsKey(playerNumber), $"The account of player {playerNumber} was not initialized.");
            _account[playerNumber] += delta;
        }
    }
}
