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
    /// Charge heal: moves to a location then heals an adjacent ally.
    /// </summary>
    public class ChargeHealAbilityImpl : IAbility
    {
        public event Action<IAbility> AbilitySelected;
        public event Action<IAbility> AbilityDeselected;

        private const int ManaCost = 2;
        private const float HealAmount = 5f;

        private HashSet<ICell> _cellsInRange;
        private int _pendingHealTargetId;

        public IUnit UnitReference { get; set; }

        public ChargeHealAbilityImpl(IUnit unitReference)
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
            _pendingHealTargetId = -1;
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

            var healTarget = FindHealTarget(cell, gridController);
            if (healTarget == null)
            {
                gridController.GridState = new GridStateAwaitInput();
                return;
            }

            _pendingHealTargetId = healTarget.UnitID;
            ExecuteChargeHeal(UnitReference.CurrentCell, cell, gridController);
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

        private IUnit FindHealTarget(ICell destinationCell, IGridController gridController)
        {
            var neighbours = destinationCell.GetNeighbours(gridController.CellManager);
            foreach (var cell in neighbours)
            {
                foreach (var unit in cell.CurrentUnits)
                {
                    if (unit.PlayerNumber == UnitReference.PlayerNumber && unit.Health < unit.MaxHealth)
                    {
                        return unit;
                    }
                }
            }
            return null;
        }

        private void ExecuteChargeHeal(ICell source, ICell destination, IGridController gridController)
        {
            var path = UnitReference.FindPath(destination, gridController.CellManager);
            if (!path.Any())
            {
                gridController.GridState = new GridStateAwaitInput();
                return;
            }

            UnitReference.HumanExecuteAbility(
                new ChargeHealCommand(source, destination, path, _pendingHealTargetId, HealAmount),
                gridController);
        }
    }

    /// <summary>
    /// Command for charge heal: move to target cell then heal an adjacent ally.
    /// </summary>
    public readonly struct ChargeHealCommand : ICommand
    {
        private readonly ICell _source;
        private readonly ICell _destination;
        private readonly IEnumerable<ICell> _path;
        private readonly int _healTargetId;
        private readonly float _healAmount;

        public ChargeHealCommand(ICell source, ICell destination, IEnumerable<ICell> path, int healTargetUnitId, float healAmount)
        {
            _source = source;
            _destination = destination;
            _path = path;
            _healTargetId = healTargetUnitId;
            _healAmount = healAmount;
        }

        public async Task Execute(IUnit unit, IGridController controller)
        {
            var moveCmd = new MoveCommand(_source, _destination, _path);
            await moveCmd.Execute(unit, controller);

            if (_healTargetId >= 0 && unit.ActionPoints > 0)
            {
                var healTargetId = _healTargetId;
                var healTarget = controller.UnitManager.GetUnits().FirstOrDefault(u => u.UnitID == healTargetId);
                if (healTarget != null)
                {
                    var healCmd = new HealCommand(healTarget, unit, _healAmount);
                    await healCmd.Execute(unit, controller);
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
            public const string HealAmount = "heal_amount";
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
                { SerializationKeys.TargetID, self._healTargetId },
                { SerializationKeys.HealAmount, self._healAmount }
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
            var healAmount = Convert.ToSingle(actionParams[SerializationKeys.HealAmount]);

            return new ChargeHealCommand(source, destination, path, targetId, healAmount);
        }
    }
}
