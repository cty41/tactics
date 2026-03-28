using System.Collections.Generic;
using System.Threading.Tasks;
using Tactics.Tbsf.Common.Cells;
using Tactics.Tbsf.Common.Units;
using Tactics.Tbsf.Unity.Utilities;
using UnityEngine;

namespace Tactics.Tbsf.Unity.Units.Abilities
{
    /// <summary>
    /// A Unity-specific implementation of the <see cref="MoveComponent"/> responsible for handling unit movement animations in the game.
    /// </summary>
    public class UnityMoveComponent : MoveComponent
    {
        public UnityMoveComponent(IUnit unitReference) : base(unitReference)
        {
        }

        public override async Task MovementAnimation(IEnumerable<ICell> path, ICell destination)
        {
            var currentCell = _unitReference.CurrentCell;
            foreach (var cell in path)
            {
                _unitReference.InvokeUnitLeftCell(new UnitChangedGridPositionEventArgs(_unitReference, currentCell, cell));
                while (!_unitReference.WorldPosition.Equals(cell.WorldPosition))
                {
                    _unitReference.WorldPosition = Vector3.MoveTowards(_unitReference.WorldPosition.ToVector3(), cell.WorldPosition.ToVector3(), Time.deltaTime * _unitReference.MovementAnimationSpeed).ToIVector3();
                    await Awaitable.NextFrameAsync();
                }

                _unitReference.InvokeUnitEnteredCell(new UnitChangedGridPositionEventArgs(_unitReference, currentCell, cell));
                currentCell = cell;
            }
            _unitReference.WorldPosition = destination.WorldPosition;
        }
    }
}