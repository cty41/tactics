using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Controllers.GridStates;
using UnityEngine;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// Generic ability implementation that acts as an event coordinator between
    /// the Grid state system and the data-driven ability effects.
    /// </summary>
    public class GenericAbilityImpl : IAbility
    {
        public event Action<IAbility> AbilitySelected;
        public event Action<IAbility> AbilityDeselected;

        private readonly IUnit _owner;
        private readonly AbilityConfig _config;
        private ICell _selectedCell;
        private IEnumerable<IUnit> _pendingTargets;
        private IGridController _gridController;

        public IUnit UnitReference { get; set; }

        public GenericAbilityImpl(IUnit owner, AbilityConfig config)
        {
            _owner = owner;
            _config = config;
            UnitReference = owner;
        }

        public void Initialize(IGridController gridController)
        {
            _gridController = gridController;
        }

        public void OnAbilitySelected(IGridController gridController)
        {
            _gridController = gridController;
            _owner.CachePaths(gridController.CellManager);
        }

        public void Display(IGridController gridController)
        {
            if (_config.TargetingStrategy != null)
            {
                _config.TargetingStrategy.DisplayPreview(gridController);
            }
        }

        public void CleanUp(IGridController gridController)
        {
            if (_config.TargetingStrategy != null)
            {
                _config.TargetingStrategy.CleanUpPreview(gridController);
            }
            _selectedCell = null;
            _pendingTargets = null;
        }

        public void OnCellClicked(ICell cell, IGridController gridController)
        {
            if (!IsValidCell(cell, gridController))
            {
                gridController.GridState = new GridStateAwaitInput();
                return;
            }

            _selectedCell = cell;
            _pendingTargets = _config.TargetingStrategy?.GetTargets(_owner, cell, gridController) ?? Enumerable.Empty<IUnit>();
            ExecuteEffects(gridController);
        }

        public void OnCellHighlighted(ICell cell, IGridController gridController)
        {
            if (IsValidCell(cell, gridController) && _config.TargetingStrategy is AoETargeting aoe)
            {
                var aoeCells = GetAoeCells(cell, aoe, gridController);
                gridController.CellManager.MarkAsReachable(aoeCells);
            }
        }

        public void OnCellDehighlighted(ICell cell, IGridController gridController)
        {
            if (_config.TargetingStrategy is AoETargeting)
            {
                gridController.CellManager.UnMark(cell);
            }
        }

        public void OnUnitClicked(IUnit unit, IGridController gridController)
        {
            if (UnitReference.ActionPoints > 0 && _config.TargetingStrategy != null)
            {
                if (_config.TargetingStrategy.IsValidTarget(_owner, unit, gridController))
                {
                    _pendingTargets = new List<IUnit> { unit };
                    ExecuteEffects(gridController);
                    return;
                }
            }

            var activeUnit = gridController.TurnContext.PlayableUnits().FirstOrDefault();
            if (activeUnit != null && ReferenceEquals(activeUnit, unit))
            {
                gridController.GridState = new GridStateUnitSelected(unit, unit.GetBaseAbilities());
            }
        }

        public void OnUnitHighlighted(IUnit unit, IGridController gridController) { }
        public void OnUnitDehighlighted(IUnit unit, IGridController gridController) { }
        public void OnUnitDestroyed(IGridController gridController) { }
        public void OnAbilityDeselected(IGridController gridController) { }
        public void OnTurnStart(IGridController gridController) { }
        public void OnTurnEnd(IGridController gridController) { }

        public bool CanPerform(IGridController gridController)
        {
            return _owner.Mana >= _config.ManaCost && _owner.ActionPoints >= _config.ActionPointCost;
        }

        public void InvokeAbilitySelected()
        {
            AbilitySelected?.Invoke(this);
        }

        public void InvokeAbilityDeselected()
        {
            AbilityDeselected?.Invoke(this);
        }

        private bool IsValidCell(ICell cell, IGridController gridController)
        {
            if (_config.TargetingStrategy == null) return false;
            if (_config.TargetingStrategy is AoETargeting aoe)
            {
                return cell.GetDistance(_owner.CurrentCell) <= aoe.MaxRange;
            }
            return true;
        }

        private async void ExecuteEffects(IGridController gridController)
        {
            if (_owner.Mana < _config.ManaCost || _owner.ActionPoints < _config.ActionPointCost)
            {
                return;
            }

            try
            {
                _owner.Mana -= _config.ManaCost;
                _owner.ActionPoints -= _config.ActionPointCost;

                foreach (var effect in _config.Effects)
                {
                    await effect.Execute(_owner, _pendingTargets, gridController);
                }

                CleanUp(gridController);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GenericAbilityImpl] Error executing ability {_config.DisplayName}: {ex.Message}");
            }
        }

        private HashSet<ICell> GetAoeCells(ICell center, AoETargeting aoe, IGridController gridController)
        {
            var cells = new HashSet<ICell> { center };
            if (aoe.Shape == AoeShape.Cross)
            {
                var neighbours = center.GetNeighbours(gridController.CellManager);
                foreach (var n in neighbours)
                {
                    cells.Add(n);
                }
            }
            else if (aoe.Shape == AoeShape.Circle)
            {
                cells.UnionWith(gridController.CellManager.GetCells().Where(c => c.GetDistance(center) <= aoe.Radius));
            }
            return cells;
        }
    }
}
