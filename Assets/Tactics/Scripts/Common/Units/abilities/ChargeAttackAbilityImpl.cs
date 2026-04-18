using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Controllers.GridStates;
using Tactics.Common.Utilities;
using UnityEngine;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// Charge attack: moves to target then attacks. Combines movement and damage.
    /// </summary>
    public class ChargeAttackAbilityImpl : IAbility
    {
        public event Action<IAbility> AbilitySelected;
        public event Action<IAbility> AbilityDeselected;

        private const int ManaCost = 2;

        private HashSet<ICell> _cellsInRange;
        private ICell _pendingTargetCell;
        private int _pendingAttackTargetId;

        public IUnit UnitReference { get; set; }

        public ChargeAttackAbilityImpl(IUnit unitReference)
        {
            UnitReference = unitReference;
        }

        public void OnAbilitySelected(IGridController gridController)
        {
            UnitReference.CachePaths(gridController.CellManager);
            _cellsInRange = new HashSet<ICell>(UnitReference.GetAvailableDestinations(gridController.CellManager.GetCells()));
        }

        public void Display(IGridController gridController)
        {
            gridController.CellManager.MarkAsReachable(_cellsInRange);
        }

        public void CleanUp(IGridController gridController)
        {
            gridController.CellManager.UnMark(_cellsInRange);
            _cellsInRange?.Clear();
            _pendingTargetCell = null;
            _pendingAttackTargetId = -1;
        }

        public void OnCellClicked(ICell cell, IGridController gridController)
        {
            if (!_cellsInRange.Contains(cell))
            {
                gridController.GridState = new GridStateAwaitInput();
                return;
            }

            if (UnitReference.Mana < ManaCost || UnitReference.ActionPoints <= 0)
            {
                return;
            }

            _pendingTargetCell = cell;
            var enemiesAtTarget = cell.CurrentUnits.Where(u => u.PlayerNumber != UnitReference.PlayerNumber).ToList();
            _pendingAttackTargetId = enemiesAtTarget.FirstOrDefault()?.UnitID ?? -1;

            ExecuteCharge(gridController);
        }

        public void OnCellHighlighted(ICell cell, IGridController gridController)
        {
            if (_cellsInRange.Contains(cell))
            {
                var path = UnitReference.FindPath(cell, gridController.CellManager);
                if (path.Any())
                {
                    gridController.CellManager.MarkAsPath(path, UnitReference.CurrentCell);
                }
            }
        }

        public void OnCellDehighlighted(ICell cell, IGridController gridController)
        {
            if (_cellsInRange.Contains(cell))
            {
                gridController.CellManager.UnMark(cell);
                gridController.CellManager.MarkAsReachable(cell);
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

        public bool CanPerform(IGridController gridController)
        {
            return UnitReference.Mana >= ManaCost &&
                   UnitReference.ActionPoints > 0 &&
                   _cellsInRange != null &&
                   _cellsInRange.Count > 0;
        }

        public void Initialize(IGridController gridController) { }
        public void OnUnitHighlighted(IUnit unit, IGridController gridController) { }
        public void OnUnitDehighlighted(IUnit unit, IGridController gridController) { }
        public void OnUnitDestroyed(IGridController gridController) { }
        public void OnAbilityDeselected(IGridController gridController) { _cellsInRange?.Clear(); }
        public void OnTurnStart(IGridController gridController) { }
        public void OnTurnEnd(IGridController gridController) { }
        public void InvokeAbilitySelected() => AbilitySelected?.Invoke(this);
        public void InvokeAbilityDeselected() => AbilityDeselected?.Invoke(this);

        private void ExecuteCharge(IGridController gridController)
        {
            var path = UnitReference.FindPath(_pendingTargetCell, gridController.CellManager);
            if (!path.Any())
            {
                gridController.GridState = new GridStateAwaitInput();
                return;
            }

            UnitReference.HumanExecuteAbility(
                new ChargeAttackCommand(UnitReference.CurrentCell, _pendingTargetCell, path, _pendingAttackTargetId),
                gridController);
        }
    }

    /// <summary>
    /// Command for charge attack: move to target cell and optionally attack.
    /// </summary>
    public readonly struct ChargeAttackCommand : ICommand
    {
        private readonly ICell _source;
        private readonly ICell _destination;
        private readonly IEnumerable<ICell> _path;
        private readonly int _targetUnitId;

        public ChargeAttackCommand(ICell source, ICell destination, IEnumerable<ICell> path, int attackTargetUnitId = -1)
        {
            _source = source;
            _destination = destination;
            _path = path;
            _targetUnitId = attackTargetUnitId;
        }

        public async Task Execute(IUnit unit, IGridController controller)
        {
            var moveCmd = new MoveCommand(_source, _destination, _path);
            await moveCmd.Execute(unit, controller);

            if (_targetUnitId >= 0)
            {
                var targetId = _targetUnitId;
                var target = controller.UnitManager.GetUnits().FirstOrDefault(u => u.UnitID == targetId);
                if (target != null && unit.ActionPoints > 0)
                {
                    float damage = unit.CalculateTotalDamage(target);
                    var attackCmd = new MeleeAttackCommand(target, damage);
                    await attackCmd.Execute(unit, controller);
                }
            }
        }

        public Task Undo(IUnit unit, IGridController controller)
        {
            _destination.CurrentUnits.Remove(unit);
            _destination.IsTaken = _destination.CurrentUnits.Count > 0;

            unit.CurrentCell = _source;
            unit.WorldPosition = _source.WorldPosition;
            if (!_source.CurrentUnits.Contains(unit))
            {
                _source.CurrentUnits.Add(unit);
            }
            _source.IsTaken = _source.CurrentUnits.Count > 0;

            return Task.CompletedTask;
        }

        private static class SerializationKeys
        {
            public const string Source = "source";
            public const string Destination = "destination";
            public const string Path = "path";
            public const string TargetID = "target_id";
            public const string X = "x";
            public const string Y = "y";
        }

        public Dictionary<string, object> Serialize()
        {
            var self = this;
            static Dictionary<string, int> SerializeCoords(ICell cell) =>
                new() { { SerializationKeys.X, cell.GridCoordinates.x }, { SerializationKeys.Y, cell.GridCoordinates.y } };

            var serializedPath = new List<Dictionary<string, int>>();
            foreach (var cell in self._path)
            {
                serializedPath.Add(SerializeCoords(cell));
            }

            return new Dictionary<string, object>
            {
                { SerializationKeys.Source, SerializeCoords(self._source) },
                { SerializationKeys.Destination, SerializeCoords(self._destination) },
                { SerializationKeys.Path, serializedPath },
                { SerializationKeys.TargetID, self._targetUnitId }
            };
        }

        public ICommand Deserialize(Dictionary<string, object> actionParams, IGridController gridController)
        {
            ICell GetCell(Dictionary<string, object> coords)
            {
                var x = Convert.ToInt32(coords[SerializationKeys.X]);
                var y = Convert.ToInt32(coords[SerializationKeys.Y]);
                return gridController.CellManager.GetCellAt(new Vector2IntImpl(x, y));
            }

            var source = GetCell(actionParams[SerializationKeys.Source] as Dictionary<string, object>);
            var destination = GetCell(actionParams[SerializationKeys.Destination] as Dictionary<string, object>);
            var path = ((IEnumerable<object>)actionParams[SerializationKeys.Path])
                .Cast<Dictionary<string, object>>()
                .Select(GetCell);
            var targetId = Convert.ToInt32(actionParams[SerializationKeys.TargetID]);

            return new ChargeAttackCommand(source, destination, path, targetId);
        }
    }
}
