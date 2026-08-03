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
            return await PlayActionAsync(
                unit,
                action,
                null,
                targetCell,
                null,
                effectFactory,
                cancellationToken);
        }

        public static async Task<T> PlayActionAsync<T>(
            IUnit unit,
            UnitVisualAction action,
            UnitPoseFamily poseFamily,
            ICell targetCell,
            Action prepareRelease,
            Func<Task<T>> effectFactory,
            CancellationToken cancellationToken)
        {
            if (effectFactory == null)
                throw new ArgumentNullException(nameof(effectFactory));

            int prepared = 0;
            void PrepareRelease()
            {
                if (Interlocked.Exchange(ref prepared, 1) == 0)
                    prepareRelease?.Invoke();
            }

            var visual = Resolve(unit);
            if (visual == null || action == UnitVisualAction.None || targetCell == null)
            {
                PrepareRelease();
                return await effectFactory();
            }

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
                    poseFamily,
                    targetCell.WorldPosition.ToVector3(),
                    PrepareRelease,
                    Release,
                    cancellationToken);
            }
            finally
            {
                if (effectTask == null && !cancellationToken.IsCancellationRequested && visual != null)
                {
                    PrepareRelease();
                    Release();
                }
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

        public static void SetVisualState(IUnit unit, UnitVisualState state)
        {
            var visual = Resolve(unit);
            if (visual != null)
            {
                visual.SetVisualState(state);
                return;
            }

            ResolveDirectional(unit)?.SetVisualState(state, unit?.Facing ?? FacingDirection.South);
        }

        public static void ClearPose(IUnit unit)
        {
            var visual = Resolve(unit);
            if (visual != null)
            {
                visual.ClearActionPose();
                return;
            }

            ResolveDirectional(unit)?.ClearPose(unit?.Facing ?? FacingDirection.South);
        }

        private static UnitTweenVisual Resolve(IUnit unit)
        {
            return unit is Component component
                ? component.GetComponent<UnitTweenVisual>()
                : null;
        }

        private static FourDirectionSpriteVisual ResolveDirectional(IUnit unit)
        {
            return unit is Component component
                ? component.GetComponent<FourDirectionSpriteVisual>()
                : null;
        }
    }
}
