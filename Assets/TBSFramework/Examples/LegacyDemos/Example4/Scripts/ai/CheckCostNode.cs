using System.Threading.Tasks;
using TurnBasedStrategyFramework.Common.AI.BehaviourTrees;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.AI.BehaviourTrees
{
    /// <summary>
    /// A behavior tree node that checks if the player has enough resources to cover a specified cost.
    /// </summary>
    public readonly struct CheckCostNode : ITreeNode
    {
        private readonly EconomyController _economyController;
        private readonly int _cost;
        private readonly int _playerNumber;

        public CheckCostNode(EconomyController economyController, int cost, int playerNumber) : this()
        {
            _economyController = economyController;
            _cost = cost;
            _playerNumber = playerNumber;
        }

        public Task<bool> Execute(bool debugMode)
        {
            var playerFunds = _economyController.GetValue(_playerNumber);
            var result = playerFunds >= _cost;

            if (debugMode)
            {
                Debug.Log($"CostCheck result: {result}; Player Funds: {playerFunds}; Cost: {_cost}");
            }

            return Task.FromResult(result);
        }
    }
}