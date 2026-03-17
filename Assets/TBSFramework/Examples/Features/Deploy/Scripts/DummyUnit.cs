using TurnBasedStrategyFramework.Common.Controllers;
using TurnBasedStrategyFramework.Common.Controllers.GridStates;
using TurnBasedStrategyFramework.Unity.Units;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Features.UnitDeployment
{
    /// <summary>
    /// A disposable unit that initializes the deployment process
    /// </summary>
    public class DummyUnit : Unit
    {
        [SerializeField] private IGridController _gridController;
        public override void Initialize(IGridController gridController)
        {
            base.Initialize(gridController);
            _gridController = gridController; // Grab a Grid Controller reference to initialize the deployment process later.
        }

        public void InitializeDeploy()
        {
            _gridController.GridState = new GridStateUnitSelected(this, GetBaseAbilities()); // Select this unit and its DeployAbility to initialize the deployment.
        }

        public override void OnTurnEnd(IGridController gridController)
        {
            gridController.TurnEnded += RemoveUnitFromGame;
        }

        private void RemoveUnitFromGame(TurnTransitionParams obj)
        {
            this.RemoveFromGame(); // Once the deployment is finished, this unit is removed from the game.
        }
    }
}