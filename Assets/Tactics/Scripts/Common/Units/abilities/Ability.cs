using System;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using UnityEngine;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// A Unity-specific abstract base class representing an ability that a unit can perform in the game.
    /// </summary>
    public abstract class Ability : MonoBehaviour, IAbility, IDamageScalingAbility
    {
        public event Action<IAbility> AbilitySelected;
        public event Action<IAbility> AbilityDeselected;

        public IUnit UnitReference { get; set; }
        [SerializeField] private bool _isRangedDamage;
        [SerializeField] private bool _hasHalfScaling;
        public bool IsRangedDamage => _isRangedDamage;
        public bool HasHalfScaling => _hasHalfScaling;

        public virtual void Initialize(IGridController gridController) { }
        public virtual void Display(IGridController gridController) { }
        public virtual void CleanUp(IGridController gridController) { }

        public virtual void OnUnitClicked(IUnit unit, IGridController gridController) { }
        public virtual void OnUnitDehighlighted(IUnit unit, IGridController gridController) { }
        public virtual void OnUnitHighlighted(IUnit unit, IGridController gridController) { }
        public virtual void OnUnitDestroyed(IGridController gridController) { }

        public virtual void OnCellClicked(ICell cell, IGridController gridController) { }
        public virtual void OnCellDehighlighted(ICell cell, IGridController gridController) { }
        public virtual void OnCellHighlighted(ICell cell, IGridController gridController) { }

        public virtual void OnAbilityDeselected(IGridController gridController) { }
        public virtual void OnAbilitySelected(IGridController gridController) { }

        public virtual void OnTurnStart(IGridController gridController) { }
        public virtual void OnTurnEnd(IGridController gridController) { }

        public virtual bool CanPerform(IGridController gridController) { return true; }

        public virtual void InvokeAbilitySelected()
        {
            AbilitySelected?.Invoke(this);
        }

        public virtual void InvokeAbilityDeselected()
        {
            AbilityDeselected?.Invoke(this);
        }
    }
}