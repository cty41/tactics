using DG.Tweening;
using UnityEngine;

namespace Tactics.Common.Units.Tween
{
    /// <summary>
    /// Visual-only DOTween sequence factory shared by runtime coordination and editor preview.
    /// </summary>
    public static class UnitTweenSequenceBuilder
    {
        /// <summary>
        /// Builds a looping breathing motion around the supplied baseline pose.
        /// </summary>
        public static Sequence BuildIdle(
            Transform target,
            StandardUnitTweenProfile profile,
            Vector3 basePosition,
            Vector3 baseScale)
        {
            float half = profile.IdleDuration * 0.5f;
            var inhaleScale = new Vector3(
                baseScale.x * (1f + profile.IdleScaleAmount),
                baseScale.y * (1f - profile.IdleScaleAmount),
                baseScale.z);

            return DOTween.Sequence()
                .Append(target.DOLocalMoveY(basePosition.y + profile.IdleLift, half).SetEase(Ease.InOutSine))
                .Join(target.DOScale(inhaleScale, half).SetEase(Ease.InOutSine))
                .Append(target.DOLocalMove(basePosition, half).SetEase(Ease.InOutSine))
                .Join(target.DOScale(baseScale, half).SetEase(Ease.InOutSine))
                .SetLoops(-1, LoopType.Restart);
        }

        /// <summary>
        /// Builds a looping paper-cutout sway for one active movement segment.
        /// </summary>
        public static Sequence BuildMoveLoop(
            Transform target,
            StandardUnitTweenProfile profile,
            Vector3 basePosition,
            Vector3 baseScale,
            Vector3 worldDirection)
        {
            Vector3 direction = NormalizeDirection(worldDirection);
            Vector3 perpendicular = new(-direction.y, direction.x, 0f);
            float half = profile.MoveCycleDuration * 0.5f;
            float tilt = direction.x >= 0f ? -profile.MoveTiltDegrees : profile.MoveTiltDegrees;

            return DOTween.Sequence()
                .Append(target.DOLocalMove(
                    basePosition + perpendicular * profile.MoveSway + Vector3.up * profile.MoveLift,
                    half).SetEase(Ease.OutQuad))
                .Join(target.DOLocalRotate(new Vector3(0f, 0f, tilt), half).SetEase(Ease.OutQuad))
                .Join(target.DOScale(
                    new Vector3(baseScale.x * 0.98f, baseScale.y * 1.02f, baseScale.z),
                    half).SetEase(Ease.OutQuad))
                .Append(target.DOLocalMove(
                    basePosition - perpendicular * profile.MoveSway,
                    half).SetEase(Ease.InOutSine))
                .Join(target.DOLocalRotate(new Vector3(0f, 0f, -tilt * 0.65f), half).SetEase(Ease.InOutSine))
                .Join(target.DOScale(baseScale, half).SetEase(Ease.InOutSine))
                .SetLoops(-1, LoopType.Restart);
        }

        /// <summary>
        /// Builds a short return to the authored visual pose.
        /// </summary>
        public static Sequence BuildSettle(
            Transform target,
            StandardUnitTweenProfile profile,
            Vector3 basePosition,
            Quaternion baseRotation,
            Vector3 baseScale)
        {
            return DOTween.Sequence()
                .Append(target.DOLocalMove(basePosition, profile.MoveSettleDuration).SetEase(Ease.OutQuad))
                .Join(target.DOLocalRotateQuaternion(baseRotation, profile.MoveSettleDuration).SetEase(Ease.OutQuad))
                .Join(target.DOScale(baseScale, profile.MoveSettleDuration).SetEase(Ease.OutQuad));
        }

        /// <summary>
        /// Builds a foreground action and returns the release time in sequence seconds.
        /// </summary>
        public static UnitTweenActionPlan BuildAction(
            UnitVisualAction action,
            Transform target,
            SpriteRenderer glowOverlay,
            StandardUnitTweenProfile profile,
            Vector3 basePosition,
            Quaternion baseRotation,
            Vector3 baseScale,
            Vector3 worldDirection)
        {
            return action switch
            {
                UnitVisualAction.Melee => BuildMelee(
                    target, profile, basePosition, baseRotation, baseScale, worldDirection),
                UnitVisualAction.Ranged => BuildRanged(
                    target, profile, basePosition, baseRotation, baseScale, worldDirection),
                UnitVisualAction.Cast => BuildCast(
                    target, glowOverlay, profile, basePosition, baseRotation, baseScale),
                _ => new UnitTweenActionPlan(DOTween.Sequence(), 0f)
            };
        }

        /// <summary>
        /// Builds deterministic recoil rather than random shake so scrubbing remains stable.
        /// </summary>
        public static Sequence BuildHit(
            Transform target,
            StandardUnitTweenProfile profile,
            Vector3 basePosition,
            Quaternion baseRotation,
            Vector3 baseScale,
            Vector3 incomingDirection)
        {
            Vector3 recoil = -NormalizeDirection(incomingDirection) * profile.HitRecoilDistance;
            var squash = new Vector3(baseScale.x * 1.06f, baseScale.y * 0.92f, baseScale.z);
            float sign = incomingDirection.x >= 0f ? -1f : 1f;

            return DOTween.Sequence()
                .Append(target.DOLocalMove(basePosition + recoil, profile.HitRecoilDuration).SetEase(Ease.OutQuad))
                .Join(target.DOScale(squash, profile.HitRecoilDuration).SetEase(Ease.OutQuad))
                .Join(target.DOLocalRotate(
                    new Vector3(0f, 0f, sign * profile.HitRotationDegrees),
                    profile.HitRecoilDuration).SetEase(Ease.OutQuad))
                .Append(target.DOLocalRotate(
                    new Vector3(0f, 0f, -sign * profile.HitRotationDegrees * 0.45f),
                    profile.HitShakeDuration).SetEase(Ease.InOutQuad))
                .Append(target.DOLocalMove(basePosition, profile.HitRecoverDuration).SetEase(Ease.OutBack))
                .Join(target.DOLocalRotateQuaternion(baseRotation, profile.HitRecoverDuration).SetEase(Ease.OutQuad))
                .Join(target.DOScale(baseScale, profile.HitRecoverDuration).SetEase(Ease.OutQuad));
        }

        /// <summary>
        /// Builds the authored corpse sprite landing without delaying corpse gameplay state.
        /// </summary>
        public static Sequence BuildCorpseLanding(
            Transform target,
            StandardUnitTweenProfile profile,
            Vector3 basePosition,
            Quaternion baseRotation,
            Vector3 baseScale)
        {
            var startScale = new Vector3(baseScale.x * 0.85f, baseScale.y * 0.85f, baseScale.z);
            var impactScale = new Vector3(baseScale.x * 1.08f, baseScale.y * 0.88f, baseScale.z);
            target.localPosition = basePosition + Vector3.up * profile.CorpseStartHeight;
            target.localRotation = baseRotation;
            target.localScale = startScale;

            return DOTween.Sequence()
                .Append(target.DOLocalMove(basePosition, profile.CorpseDropDuration).SetEase(Ease.InQuad))
                .Join(target.DOScale(baseScale, profile.CorpseDropDuration).SetEase(Ease.InQuad))
                .Append(target.DOScale(impactScale, profile.CorpseImpactDuration).SetEase(Ease.OutQuad))
                .Append(target.DOScale(baseScale, profile.CorpseSettleDuration).SetEase(Ease.OutBack));
        }

        private static UnitTweenActionPlan BuildMelee(
            Transform target,
            StandardUnitTweenProfile profile,
            Vector3 basePosition,
            Quaternion baseRotation,
            Vector3 baseScale,
            Vector3 worldDirection)
        {
            Vector3 direction = NormalizeDirection(worldDirection);
            var windupScale = new Vector3(baseScale.x * 1.04f, baseScale.y * 0.94f, baseScale.z);
            var sequence = DOTween.Sequence()
                .Append(target.DOLocalMove(
                    basePosition - direction * profile.MeleeLungeDistance * 0.25f,
                    profile.MeleeWindupDuration).SetEase(Ease.OutQuad))
                .Join(target.DOScale(windupScale, profile.MeleeWindupDuration).SetEase(Ease.OutQuad))
                .Append(target.DOLocalMove(
                    basePosition + direction * profile.MeleeLungeDistance,
                    profile.MeleeLungeDuration).SetEase(Ease.InQuad));
            float releaseTime = profile.MeleeWindupDuration + profile.MeleeLungeDuration;
            sequence.AppendInterval(profile.MeleeImpactHold)
                .Append(target.DOLocalMove(basePosition, profile.MeleeRecoverDuration).SetEase(Ease.OutBack))
                .Join(target.DOLocalRotateQuaternion(baseRotation, profile.MeleeRecoverDuration))
                .Join(target.DOScale(baseScale, profile.MeleeRecoverDuration).SetEase(Ease.OutQuad));
            return new UnitTweenActionPlan(sequence, releaseTime);
        }

        private static UnitTweenActionPlan BuildRanged(
            Transform target,
            StandardUnitTweenProfile profile,
            Vector3 basePosition,
            Quaternion baseRotation,
            Vector3 baseScale,
            Vector3 worldDirection)
        {
            Vector3 direction = NormalizeDirection(worldDirection);
            var aimScale = new Vector3(baseScale.x * 0.98f, baseScale.y * 1.02f, baseScale.z);
            var sequence = DOTween.Sequence()
                .Append(target.DOLocalMove(
                    basePosition + direction * 0.025f,
                    profile.RangedAimDuration).SetEase(Ease.OutQuad))
                .Join(target.DOScale(aimScale, profile.RangedAimDuration).SetEase(Ease.OutQuad))
                .Append(target.DOLocalMove(
                    basePosition - direction * profile.RangedRecoilDistance,
                    profile.RangedReleaseDuration).SetEase(Ease.OutQuad));
            float releaseTime = profile.RangedAimDuration;
            sequence.Append(target.DOLocalMove(basePosition, profile.RangedRecoverDuration).SetEase(Ease.OutBack))
                .Join(target.DOLocalRotateQuaternion(baseRotation, profile.RangedRecoverDuration))
                .Join(target.DOScale(baseScale, profile.RangedRecoverDuration).SetEase(Ease.OutQuad));
            return new UnitTweenActionPlan(sequence, releaseTime);
        }

        private static UnitTweenActionPlan BuildCast(
            Transform target,
            SpriteRenderer glowOverlay,
            StandardUnitTweenProfile profile,
            Vector3 basePosition,
            Quaternion baseRotation,
            Vector3 baseScale)
        {
            var chargeScale = new Vector3(baseScale.x * 1.04f, baseScale.y * 0.94f, baseScale.z);
            var sequence = DOTween.Sequence()
                .Append(target.DOScale(chargeScale, profile.CastChargeDuration).SetEase(Ease.InOutSine))
                .Join(target.DOLocalMoveY(
                    basePosition.y + profile.IdleLift,
                    profile.CastChargeDuration).SetEase(Ease.InOutSine));

            if (glowOverlay != null)
            {
                Color glow = profile.CastGlowColor;
                glow.a = 0f;
                glowOverlay.color = glow;
                sequence.Join(glowOverlay.DOFade(profile.CastOverlayAlpha, profile.CastChargeDuration));
            }

            float releaseTime = profile.CastChargeDuration;
            sequence.AppendInterval(profile.CastReleaseHold)
                .Append(target.DOLocalMove(basePosition, profile.CastRecoverDuration).SetEase(Ease.OutQuad))
                .Join(target.DOLocalRotateQuaternion(baseRotation, profile.CastRecoverDuration))
                .Join(target.DOScale(baseScale, profile.CastRecoverDuration).SetEase(Ease.OutQuad));
            if (glowOverlay != null)
                sequence.Join(glowOverlay.DOFade(0f, profile.CastRecoverDuration));
            return new UnitTweenActionPlan(sequence, releaseTime);
        }

        private static Vector3 NormalizeDirection(Vector3 direction)
        {
            direction.z = 0f;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;
        }
    }

    /// <summary>
    /// Identifies the authored visual language used for one skill.
    /// </summary>
    public enum UnitVisualAction
    {
        None,
        Melee,
        Ranged,
        Cast
    }

    /// <summary>
    /// Couples a visual-only sequence with its gameplay release marker.
    /// </summary>
    public readonly struct UnitTweenActionPlan
    {
        public UnitTweenActionPlan(Sequence sequence, float releaseTime)
        {
            Sequence = sequence;
            ReleaseTime = releaseTime;
        }

        public Sequence Sequence { get; }
        public float ReleaseTime { get; }
    }
}
