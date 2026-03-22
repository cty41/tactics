using System.Threading.Tasks;
using TurnBasedStrategyFramework.Common.AI.BehaviourTrees;
using TurnBasedStrategyFramework.Common.Controllers;
using TurnBasedStrategyFramework.Common.Units;
using TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.Units.Abilities;
using TurnBasedStrategyFramework.Unity.Units;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.AI.BehaviourTrees
{
    /// <summary>
    /// Represents a node in a behavior tree responsible for spawning a new unit during AI execution.
    /// </summary>
    public readonly struct SpawnActionNode : ITreeNode
    {
        private readonly IUnit _unit;
        private readonly GameObject _unitPrefab;
        private readonly int _unitCost;
        private readonly EconomyController _economyController;
        private readonly IGridController _gridController;

        public SpawnActionNode(IUnit unit, GameObject unitPrefab, int unitCost, EconomyController economyController, IGridController gridController)
        {
            _unit = unit;
            _unitPrefab = unitPrefab;
            _unitCost = unitCost;

            _economyController = economyController;
            _gridController = gridController;
        }

        public Task<bool> Execute(bool debugMode)
        {
            var tcs = new TaskCompletionSource<bool>();

            var unitColor = (_unit as IColoredUnit).Color;
            _unit.AIExecuteAbility(new SpawnCommand(_unitPrefab, _unit.CurrentCell, unitColor, _economyController, _unitCost), _gridController, tcs);
            return tcs.Task;
        }
    }
}