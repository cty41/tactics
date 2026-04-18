using System;
using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Controllers.GridStates;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// Pure C# implementation of melee attack ability. Range: 1 tile (adjacent only).
    /// </summary>
    public class MeleeAttackAbilityImpl : IAbility
    {
        public event Action<IAbility> AbilitySelected;
        public event Action<IAbility> AbilityDeselected;

        private HashSet<IUnit> _attackableUnits;

        public IUnit UnitReference { get; set; }

        public MeleeAttackAbilityImpl(IUnit unitReference)
        {
            UnitReference = unitReference;
        }

        public void OnAbilitySelected(IGridController gridController)
        {
            var enemyUnits = gridController.UnitManager.GetEnemyUnits(gridController.TurnContext.CurrentPlayer);
            _attackableUnits = new HashSet<IUnit>(enemyUnits.Where(u => UnitReference.IsUnitAttackable(u, u.CurrentCell, UnitReference.CurrentCell)));
        }

        public async void Display(IGridController gridController)
        {
            await gridController.UnitManager.MarkAsTargetable(_attackableUnits);
        }

        public void CleanUp(IGridController gridController)
        {
            gridController.UnitManager.UnMark(_attackableUnits);
        }

        public void OnUnitClicked(IUnit unit, IGridController gridController)
        {
            if (UnitReference.ActionPoints > 0 && _attackableUnits.Contains(unit))
            {
                var damage = UnitReference.CalculateTotalDamage(unit, unit.CurrentCell, UnitReference.CurrentCell);
                UnitReference.HumanExecuteAbility(new MeleeAttackCommand(unit, damage), gridController);
            }
            else
            {
                var activeUnit = gridController.TurnContext.PlayableUnits().FirstOrDefault();
                if (activeUnit != null && ReferenceEquals(activeUnit, unit))
                {
                    gridController.GridState = new GridStateUnitSelected(unit, unit.GetBaseAbilities());
                }
            }
        }

        public bool CanPerform(IGridController gridController)
        {
            if (UnitReference.ActionPoints <= 0)
            {
                return false;
            }

            var enemyUnits = gridController.UnitManager.GetEnemyUnits(gridController.PlayerManager.GetPlayerByNumber(UnitReference.PlayerNumber));
            var attackableUnits = enemyUnits.Where(u => UnitReference.IsUnitAttackable(u, u.CurrentCell, UnitReference.CurrentCell));
            return attackableUnits.Any();
        }

        public void Initialize(IGridController gridController) { }
        public void OnUnitHighlighted(IUnit unit, IGridController gridController) { }
        public void OnUnitDehighlighted(IUnit unit, IGridController gridController) { }
        public void OnUnitDestroyed(IGridController gridController) { }
        public void OnCellClicked(ICell cell, IGridController gridController) { }
        public void OnCellHighlighted(ICell cell, IGridController gridController) { }
        public void OnCellDehighlighted(ICell cell, IGridController gridController) { }
        public void OnAbilityDeselected(IGridController gridController) { }
        public void OnTurnStart(IGridController gridController) { }
        public void OnTurnEnd(IGridController gridController) { }
        public void InvokeAbilitySelected() => AbilitySelected?.Invoke(this);
        public void InvokeAbilityDeselected() => AbilityDeselected?.Invoke(this);
    }
}
