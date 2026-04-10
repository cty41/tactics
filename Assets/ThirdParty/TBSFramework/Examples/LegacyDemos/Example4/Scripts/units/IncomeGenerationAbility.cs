using TurnBasedStrategyFramework.Common.Controllers;
using TurnBasedStrategyFramework.Unity.Units.Abilities;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.Units.Abilities
{
    /// <summary>
    /// An ability that generates income for the unit's player at the end of each turn.
    /// </summary>
    public class IncomeGenerationAbility : Ability
    {
        [SerializeField] private int _amount;
        [SerializeField] private EconomyController _economyController;

        public int Amount { get { return _amount; } set { _amount = value; } }

        public override void OnTurnEnd(IGridController controller)
        {
            _economyController.UpdateValue(UnitReference.PlayerNumber, _amount);
        }
    }
}

