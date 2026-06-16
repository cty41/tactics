using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tactics.Common.Cells;
using Tactics.Common.Units;
using Tactics.Common.Utilities;
using UnityEngine;

namespace Tactics.Common.Units.Abilities
{
    public class UnityMoveComponent : MoveComponent
    {
        private CancellationTokenSource _moveCts;

        public UnityMoveComponent(IUnit unitReference) : base(unitReference)
        {
        }

        public override Task MovementAnimation(IEnumerable<ICell> path, ICell destination)
        {
            var tcs = new TaskCompletionSource<bool>();
            var mono = _unitReference as MonoBehaviour;
            if (mono == null)
            {
                _unitReference.WorldPosition = destination.WorldPosition;
                tcs.SetResult(true);
                return tcs.Task;
            }

            // 快速路径：如果 path 为空，直接设置位置
            var pathList = path as List<ICell> ?? new List<ICell>(path);
            if (pathList.Count == 0)
            {
                _unitReference.WorldPosition = destination.WorldPosition;
                tcs.SetResult(true);
                return tcs.Task;
            }

            _moveCts?.Cancel();
            _moveCts = new CancellationTokenSource();
            mono.StartCoroutine(AnimateMoveCoroutine(pathList, destination, tcs, _moveCts.Token));
            return tcs.Task;
        }

        public void CancelMovement()
        {
            _moveCts?.Cancel();
        }

        private IEnumerator AnimateMoveCoroutine(List<ICell> path, ICell destination, TaskCompletionSource<bool> tcs, CancellationToken token)
        {
            var currentCell = _unitReference.CurrentCell;
            foreach (var cell in path)
            {
                if (token.IsCancellationRequested)
                {
                    _unitReference.WorldPosition = destination.WorldPosition;
                    tcs.SetCanceled();
                    yield break;
                }

                _unitReference.InvokeUnitLeftCell(new UnitChangedGridPositionEventArgs(_unitReference, currentCell, cell));
                var targetPos = cell.WorldPosition.ToVector3();
                while (UnityEngine.Vector3.Distance(_unitReference.WorldPosition.ToVector3(), targetPos) > 0.001f)
                {
                    if (token.IsCancellationRequested)
                    {
                        _unitReference.WorldPosition = destination.WorldPosition;
                        tcs.SetCanceled();
                        yield break;
                    }
                    _unitReference.WorldPosition = UnityEngine.Vector3.MoveTowards(_unitReference.WorldPosition.ToVector3(), targetPos, UnityEngine.Time.deltaTime * _unitReference.MovementAnimationSpeed).ToIVector3();
                    yield return null;
                }
                _unitReference.WorldPosition = cell.WorldPosition;

                _unitReference.InvokeUnitEnteredCell(new UnitChangedGridPositionEventArgs(_unitReference, currentCell, cell));
                currentCell = cell;
            }
            _unitReference.WorldPosition = destination.WorldPosition;
            tcs.SetResult(true);
        }
    }
}