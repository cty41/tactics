using Tactics.Common.Cells;
using Tactics.Common.Controllers;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// Unity component for fireball ability. Range: 4 tiles, AOE cross pattern.
    /// </summary>
    public class FireballAbility : Ability
    {
        private FireballAbilityImpl _impl;

        public override void Initialize(IGridController gridController)
        {
            base.Initialize(gridController);
            _impl = new FireballAbilityImpl(UnitReference, this);
            _impl.Initialize(gridController);
        }

        public override void Display(IGridController gridController) => _impl.Display(gridController);
        public override void CleanUp(IGridController gridController) => _impl.CleanUp(gridController);
        public override void OnAbilitySelected(IGridController gridController) => _impl.OnAbilitySelected(gridController);
        public override void OnAbilityDeselected(IGridController gridController) => _impl.OnAbilityDeselected(gridController);
        public override void OnTurnStart(IGridController gridController) => _impl.OnTurnStart(gridController);
        public override void OnTurnEnd(IGridController gridController) => _impl.OnTurnEnd(gridController);
        public override void OnCellClicked(ICell cell, IGridController gridController) => _impl.OnCellClicked(cell, gridController);
        public override void OnCellHighlighted(ICell cell, IGridController gridController) => _impl.OnCellHighlighted(cell, gridController);
        public override void OnCellDehighlighted(ICell cell, IGridController gridController) => _impl.OnCellDehighlighted(cell, gridController);
        public override void OnUnitClicked(IUnit unit, IGridController gridController) => _impl.OnUnitClicked(unit, gridController);
        public override void OnUnitHighlighted(IUnit unit, IGridController gridController) => _impl.OnUnitHighlighted(unit, gridController);
        public override void OnUnitDehighlighted(IUnit unit, IGridController gridController) => _impl.OnUnitDehighlighted(unit, gridController);
        public override void OnUnitDestroyed(IGridController gridController) => _impl.OnUnitDestroyed(gridController);
        public override bool CanPerform(IGridController gridController) => _impl.CanPerform(gridController);
    }
}
