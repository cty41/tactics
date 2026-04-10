using System;
using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Controllers.GridStates;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// Pure C# implementation of melee heal ability. Range: 1 tile (adjacent friendly units only).
    /// </summary>
    public class MeleeHealAbilityImpl : IAbility
    {
        public event Action<IAbility> AbilitySelected;
        public event Action<IAbility> AbilityDeselected;

        private HashSet<IUnit> _healableUnits;

        public IUnit UnitReference { get; set; }

        public MeleeHealAbilityImpl(IUnit unitReference)
        {
            UnitReference = unitReference;
        }

        public void OnAbilitySelected(IGridController gridController)
        {
            var friendlyUnits = gridController.UnitManager.GetFriendlyUnits(gridController.TurnContext.CurrentPlayer)
                .Where(u => u != UnitReference && u.Health < u.MaxHealth);
            _healableUnits = new HashSet<IUnit>(friendlyUnits.Where(u => u.CurrentCell.GetDistance(UnitReference.CurrentCell) <= 1));
        }

        public async void Display(IGridController gridController)
        {
            await gridController.UnitManager.MarkAsTargetable(_healableUnits);
        }

        public void CleanUp(IGridController gridController)
        {
            gridController.UnitManager.UnMark(_healableUnits);
        }

        public void OnUnitClicked(IUnit unit, IGridController gridController)
        {
            if (UnitReference.ActionPoints > 0 && _healableUnits.Contains(unit))
            {
                UnitReference.HumanExecuteAbility(new HealCommand(unit, UnitReference, healAmount: 3), gridController);
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

            var friendlyUnits = gridController.UnitManager.GetFriendlyUnits(gridController.PlayerManager.GetPlayerByNumber(UnitReference.PlayerNumber));
            return friendlyUnits
                .Where(u => u != UnitReference && u.Health < u.MaxHealth)
                .Any(u => u.CurrentCell.GetDistance(UnitReference.CurrentCell) <= 1);
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
