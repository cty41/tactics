using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tactics.Common.Cells;
using Tactics.Common.Units;
using Tactics.Common.Utilities;
using UnityEngine;

namespace Tactics.Common.Units.Abilities
{
    public class UnityMoveComponent : MoveComponent
    {
        public UnityMoveComponent(IUnit unitReference) : base(unitReference)
        {
        }

        public override Task MovementAnimation(IEnumerable<ICell> path, ICell destination)
        {
            var tcs = new TaskCompletionSource<bool>();
            var mono = _unitReference as MonoBehaviour;
            if (mono == null)
            {
                tcs.SetResult(true);
                return tcs.Task;
            }
            // Fast path: if unit has no Animator or SpriteRenderer, skip animation
            if (mono.GetComponent<Animator>() == null && mono.GetComponent<SpriteRenderer>() == null)
            {
                _unitReference.WorldPosition = destination.WorldPosition;
                tcs.SetResult(true);
                return tcs.Task;
            }
            mono.StartCoroutine(AnimateMoveCoroutine(path, destination, tcs));
            return tcs.Task;
        }

        private IEnumerator AnimateMoveCoroutine(IEnumerable<ICell> path, ICell destination, TaskCompletionSource<bool> tcs)
        {
            var currentCell = _unitReference.CurrentCell;
            foreach (var cell in path)
            {
                _unitReference.InvokeUnitLeftCell(new UnitChangedGridPositionEventArgs(_unitReference, currentCell, cell));
                while (!_unitReference.WorldPosition.Equals(cell.WorldPosition))
                {
                    _unitReference.WorldPosition = Vector3.MoveTowards(_unitReference.WorldPosition.ToVector3(), cell.WorldPosition.ToVector3(), Time.deltaTime * _unitReference.MovementAnimationSpeed).ToIVector3();
                    yield return null;
                }

                _unitReference.InvokeUnitEnteredCell(new UnitChangedGridPositionEventArgs(_unitReference, currentCell, cell));
                currentCell = cell;
            }
            _unitReference.WorldPosition = destination.WorldPosition;
            tcs.SetResult(true);
        }
    }
}