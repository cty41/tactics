using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Utilities;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// Represents a command to move a unit from a source cell to a destination cell along a specified path.
    /// This command updates the game state by moving the unit, adjusting the movement points, and triggering animations.
    /// </summary>
    public readonly struct MoveCommand : ICommand
    {
        /// <summary>
        /// The source cell from which the unit starts.
        /// </summary>
        private readonly ICell _source;

        /// <summary>
        /// The destination cell to which the unit will move.
        /// </summary>
        private readonly ICell _destination;

        /// <summary>
        /// The path taken by the unit to reach the destination.
        /// </summary>
        private readonly IEnumerable<ICell> _path;

        /// <summary>
        /// Initializes a new instance of the <see cref="MoveCommand"/> struct with the specified source, destination, and path.
        /// </summary>
        /// <param name="source">The source cell from which the unit starts.</param>
        /// <param name="destination">The destination cell to which the unit will move.</param>
        /// <param name="path">The path that the unit will take to reach the destination.</param>
        public MoveCommand(ICell source, ICell destination, IEnumerable<ICell> path)
        {
            _source = source;
            _destination = destination;
            _path = path;
        }

        /// <summary>
        /// Executes the move command, moving the unit from the source cell to the destination cell along the specified path.
        /// Updates the unit's movement points and triggers movement animations.
        /// </summary>
        /// <param name="unit">The unit to be moved.</param>
        /// <param name="controller">The grid controller.</param>
        /// <returns>A task representing the asynchronous execution of the move.</returns>
        public async Task Execute(IUnit unit, IGridController controller)
        {
            if (_destination == null || _source == null)
            {
                return;
            }

            if (!CanOccupyDestination(unit))
            {
                return;
            }

            var path = _path?.ToList() ?? new List<ICell>();
            if (path.Count == 0)
                return;

            var pathCost = path.Prepend(_source).Zip(path.Prepend(_source).Skip(1), (from, to) => unit.GetMovementCost(from, to)).Sum();
            unit.MovementPoints -= pathCost;

            await controller.UnitManager.MarkAsMoving(unit, _source, _destination, path);
            await FacingCoordinator.AnimateMovementAsync(
                unit,
                path,
                _destination,
                MovementFacingPolicy.FollowPath);

            if (!CanOccupyDestination(unit))
            {
                unit.WorldPosition = _source.WorldPosition;
                await controller.UnitManager.UnMarkAsMoving(unit, _source, _destination, path);
                return;
            }

            _source.CurrentUnits.Remove(unit);
            _source.IsTaken = _source.CurrentUnits.Count > 0;

            unit.CurrentCell = _destination;
            if (!unit.CurrentCell.CurrentUnits.Contains(unit))
            {
                unit.CurrentCell.CurrentUnits.Add(unit);
            }
            unit.CurrentCell.IsTaken = unit.CurrentCell.CurrentUnits.Count > 0;
            unit.WorldPosition = _destination.WorldPosition;

            await controller.UnitManager.UnMarkAsMoving(unit, _source, _destination, path);

            unit.InvokeUnitMoved(new UnitMovedEventArgs(unit, _source, _destination, path));
        }

        /// <summary>
        /// Undoes the move command, reverting the unit back to the source cell.
        /// Updates the game state to reflect the unit's original position.
        /// </summary>
        /// <param name="unit">The unit for which the move should be undone.</param>
        /// <param name="controller">The grid controller.</param>
        /// <returns>A task representing the asynchronous undo operation.</returns>
        public readonly Task Undo(IUnit unit, IGridController controller)
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

        private bool CanOccupyDestination(IUnit unit)
        {
            return !_destination.IsTaken || _destination.CurrentUnits.All(u => ReferenceEquals(u, unit));
        }

        private static class SerializationKeys
        {
            public const string Source = "source";
            public const string Destination = "destination";
            public const string Path = "path";

            public const string X = "x";
            public const string Y = "y";
        }

        public Dictionary<string, object> Serialize()
        {
            static Dictionary<string, int> SerializeCoordinates(ICell cell) =>
                new()
                {
                    { SerializationKeys.X, cell.GridCoordinates.x },
                    { SerializationKeys.Y, cell.GridCoordinates.y }
                };

            return new Dictionary<string, object>
            {
                { SerializationKeys.Source, SerializeCoordinates(_source) },
                { SerializationKeys.Destination, SerializeCoordinates(_destination) },
                { SerializationKeys.Path, _path.Select(SerializeCoordinates).ToArray() }
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

            return new MoveCommand(source, destination, path);
        }
    }
}
