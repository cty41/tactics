using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Controllers.GridStates;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// Pure C# implementation of fireball ability. Range: 4 tiles, AOE cross pattern.
    /// Targets cells rather than units.
    /// </summary>
    public class FireballAbilityImpl : IAbility
    {
        public event Action<IAbility> AbilitySelected;
        public event Action<IAbility> AbilityDeselected;

        private const int MaxRange = 4;
        private const int ManaCost = 3;

        private HashSet<ICell> _aoeHighlight;

        public IUnit UnitReference { get; set; }

        public FireballAbilityImpl(IUnit unitReference, IDamageScalingAbility damageScalingAbility = null)
        {
            UnitReference = unitReference;
        }

        public void OnAbilitySelected(IGridController gridController)
        {
            _aoeHighlight = new HashSet<ICell>();
        }

        public void Display(IGridController gridController)
        {
            // No initial display - highlights appear on cell hover
        }

        public void CleanUp(IGridController gridController)
        {
            if (_aoeHighlight.Count > 0)
            {
                gridController.CellManager.UnMark(_aoeHighlight);
                _aoeHighlight.Clear();
            }
        }

        public void OnUnitClicked(IUnit unit, IGridController gridController)
        {
            var activeUnit = gridController.TurnContext.PlayableUnits().FirstOrDefault();
            if (activeUnit != null && ReferenceEquals(activeUnit, unit))
            {
                gridController.GridState = new GridStateUnitSelected(unit, unit.GetBaseAbilities());
            }
        }

        public void OnUnitHighlighted(IUnit unit, IGridController gridController) { }
        public void OnUnitDehighlighted(IUnit unit, IGridController gridController) { }
        public void OnUnitDestroyed(IGridController gridController) { }

        public void OnCellClicked(ICell cell, IGridController gridController)
        {
            int distance = cell.GetDistance(UnitReference.CurrentCell);
            if (distance > MaxRange)
            {
                gridController.GridState = new GridStateAwaitInput();
                return;
            }

            if (UnitReference.Mana < ManaCost || UnitReference.ActionPoints <= 0)
            {
                return;
            }

            var aoeCells = GetAoeCells(cell, gridController);
            float damage = UnitReference.CalculateDamageDealt(cell.CurrentUnits.FirstOrDefault() ?? UnitReference, UnitReference.CurrentCell, UnitReference.CurrentCell);
            UnitReference.HumanExecuteAbility(
                new FireballCommand(cell, UnitReference, aoeCells, damage),
                gridController);
        }

        public void OnCellHighlighted(ICell cell, IGridController gridController)
        {
            int distance = cell.GetDistance(UnitReference.CurrentCell);
            if (distance > MaxRange)
            {
                return;
            }

            CleanUpAoeHighlight(gridController);

            _aoeHighlight = GetAoeCells(cell, gridController);
            gridController.CellManager.MarkAsReachable(_aoeHighlight);
        }

        public void OnCellDehighlighted(ICell cell, IGridController gridController)
        {
            CleanUpAoeHighlight(gridController);
        }

        public void OnAbilityDeselected(IGridController gridController)
        {
            _aoeHighlight?.Clear();
        }

        public void Initialize(IGridController gridController) { }
        public void OnTurnStart(IGridController gridController) { }
        public void OnTurnEnd(IGridController gridController) { }
        public void InvokeAbilitySelected() => AbilitySelected?.Invoke(this);
        public void InvokeAbilityDeselected() => AbilityDeselected?.Invoke(this);

        public bool CanPerform(IGridController gridController)
        {
            return UnitReference.Mana >= ManaCost && UnitReference.ActionPoints > 0;
        }

        private HashSet<ICell> GetAoeCells(ICell center, IGridController gridController)
        {
            var aoeCells = new HashSet<ICell> { center };
            var neighbours = center.GetNeighbours(gridController.CellManager);
            foreach (var n in neighbours)
            {
                aoeCells.Add(n);
            }
            return aoeCells;
        }

        private void CleanUpAoeHighlight(IGridController gridController)
        {
            if (_aoeHighlight.Count > 0)
            {
                gridController.CellManager.UnMark(_aoeHighlight);
                _aoeHighlight.Clear();
            }
        }
    }
}
