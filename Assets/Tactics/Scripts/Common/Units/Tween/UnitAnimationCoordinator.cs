using System;
using System.Threading;
using System.Threading.Tasks;
using Tactics.Common.Cells;
using Tactics.Common.Utilities;
using UnityEngine;

namespace Tactics.Common.Units.Tween
{
    /// <summary>
    /// Bridges semantic battle actions to optional unit tween visuals without changing unit interfaces.
    /// </summary>
    internal static class UnitAnimationCoordinator
    {
        public static async Task<T> PlayActionAsync<T>(
            IUnit unit,
            UnitVisualAction action,
            ICell targetCell,
            Func<Task<T>> effectFactory,
            CancellationToken cancellationToken)
        {
            if (effectFactory == null)
                throw new ArgumentNullException(nameof(effectFactory));

            var visual = Resolve(unit);
            if (visual == null || action == UnitVisualAction.None || targetCell == null)
                return await effectFactory();

            Task<T> effectTask = null;
            int released = 0;
            void Release()
            {
                if (Interlocked.Exchange(ref released, 1) == 0)
                    effectTask = effectFactory();
            }

            try
            {
                await visual.PlayActionAsync(
                    action,
                    targetCell.WorldPosition.ToVector3(),
                    Release,
                    cancellationToken);
            }
            finally
            {
                if (effectTask == null && !cancellationToken.IsCancellationRequested && visual != null)
                    Release();
            }

            cancellationToken.ThrowIfCancellationRequested();
            return effectTask != null ? await effectTask : await effectFactory();
        }

        public static void BeginMoveStep(IUnit unit, ICell source, ICell destination)
        {
            var visual = Resolve(unit);
            if (visual == null || source == null || destination == null)
                return;

            visual.BeginMoveStep(destination.WorldPosition.ToVector3() - source.WorldPosition.ToVector3());
        }

        public static void EndMoveStep(IUnit unit)
        {
            Resolve(unit)?.EndMoveStep();
        }

        public static void PlayHit(IUnit unit, IUnit attacker)
        {
            var visual = Resolve(unit);
            if (visual == null)
                return;

            Vector3 attackerPosition = attacker?.WorldPosition.ToVector3()
                ?? visual.transform.position - Vector3.right;
            visual.PlayHit(attackerPosition);
        }

        private static UnitTweenVisual Resolve(IUnit unit)
        {
            return unit is Component component
                ? component.GetComponent<UnitTweenVisual>()
                : null;
        }
    }
}
