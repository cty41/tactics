using UnityEngine;

namespace Tactics.Common.Units.Tween
{
    /// <summary>
    /// Stores the shared timing and amplitude contract for standard ground-unit tween visuals.
    /// </summary>
    [CreateAssetMenu(fileName = "StandardUnitTweenProfile", menuName = "Tactics/Visuals/Standard Unit Tween Profile")]
    public sealed class StandardUnitTweenProfile : ScriptableObject
    {
        [Header("Idle")]
        [SerializeField, Min(0.1f)] private float _idleDuration = 1.35f;
        [SerializeField, Min(0f)] private float _idleLift = 0.025f;
        [SerializeField, Min(0f)] private float _idleScaleAmount = 0.025f;

        [Header("Movement")]
        [SerializeField, Min(0.05f)] private float _moveCycleDuration = 0.22f;
        [SerializeField, Range(0f, 12f)] private float _moveTiltDegrees = 5f;
        [SerializeField, Min(0f)] private float _moveLift = 0.03f;
        [SerializeField, Min(0f)] private float _moveSway = 0.03f;
        [SerializeField, Min(0.01f)] private float _moveSettleDuration = 0.06f;

        [Header("Melee")]
        [SerializeField, Min(0f)] private float _meleeWindupDuration = 0.07f;
        [SerializeField, Min(0f)] private float _meleeLungeDuration = 0.09f;
        [SerializeField, Min(0f)] private float _meleeImpactHold = 0.045f;
        [SerializeField, Min(0f)] private float _meleeRecoverDuration = 0.14f;
        [SerializeField, Min(0f)] private float _meleeLungeDistance = 0.22f;

        [Header("Ranged")]
        [SerializeField, Min(0f)] private float _rangedAimDuration = 0.1f;
        [SerializeField, Min(0f)] private float _rangedReleaseDuration = 0.06f;
        [SerializeField, Min(0f)] private float _rangedRecoverDuration = 0.16f;
        [SerializeField, Min(0f)] private float _rangedRecoilDistance = 0.08f;

        [Header("Cast")]
        [SerializeField, Min(0f)] private float _castChargeDuration = 0.28f;
        [SerializeField, Min(0f)] private float _castReleaseHold = 0.06f;
        [SerializeField, Min(0f)] private float _castRecoverDuration = 0.2f;

        [Header("Hit")]
        [SerializeField, Min(0f)] private float _hitRecoilDuration = 0.07f;
        [SerializeField, Min(0f)] private float _hitShakeDuration = 0.07f;
        [SerializeField, Min(0f)] private float _hitRecoverDuration = 0.09f;
        [SerializeField, Min(0f)] private float _hitRecoilDistance = 0.1f;
        [SerializeField, Range(0f, 10f)] private float _hitRotationDegrees = 4f;

        [Header("Lethal Hit")]
        [SerializeField, Min(0f)] private float _lethalShakeDuration = 0.05f;
        [SerializeField, Min(0f)] private float _lethalCollapseDuration = 0.08f;
        [SerializeField, Range(0.1f, 2f)] private float _lethalCollapseScaleX = 1.02f;
        [SerializeField, Range(0.1f, 2f)] private float _lethalCollapseScaleY = 0.58f;

        [Header("Corpse Landing")]
        [SerializeField, Min(0f)] private float _corpseDropDuration = 0.13f;
        [SerializeField, Min(0f)] private float _corpseImpactDuration = 0.07f;
        [SerializeField, Min(0f)] private float _corpseSettleDuration = 0.08f;
        [SerializeField, Min(0f)] private float _corpseStartHeight = 0.08f;

        public float IdleDuration => _idleDuration;
        public float IdleLift => _idleLift;
        public float IdleScaleAmount => _idleScaleAmount;
        public float MoveCycleDuration => _moveCycleDuration;
        public float MoveTiltDegrees => _moveTiltDegrees;
        public float MoveLift => _moveLift;
        public float MoveSway => _moveSway;
        public float MoveSettleDuration => _moveSettleDuration;
        public float MeleeWindupDuration => _meleeWindupDuration;
        public float MeleeLungeDuration => _meleeLungeDuration;
        public float MeleeImpactHold => _meleeImpactHold;
        public float MeleeRecoverDuration => _meleeRecoverDuration;
        public float MeleeLungeDistance => _meleeLungeDistance;
        public float RangedAimDuration => _rangedAimDuration;
        public float RangedReleaseDuration => _rangedReleaseDuration;
        public float RangedRecoverDuration => _rangedRecoverDuration;
        public float RangedRecoilDistance => _rangedRecoilDistance;
        public float CastChargeDuration => _castChargeDuration;
        public float CastReleaseHold => _castReleaseHold;
        public float CastRecoverDuration => _castRecoverDuration;
        public float HitRecoilDuration => _hitRecoilDuration;
        public float HitShakeDuration => _hitShakeDuration;
        public float HitRecoverDuration => _hitRecoverDuration;
        public float HitRecoilDistance => _hitRecoilDistance;
        public float HitRotationDegrees => _hitRotationDegrees;
        internal float LethalShakeDuration => _lethalShakeDuration;
        internal float LethalCollapseDuration => _lethalCollapseDuration;
        internal float LethalCollapseScaleX => _lethalCollapseScaleX;
        internal float LethalCollapseScaleY => _lethalCollapseScaleY;
        public float CorpseDropDuration => _corpseDropDuration;
        public float CorpseImpactDuration => _corpseImpactDuration;
        public float CorpseSettleDuration => _corpseSettleDuration;
        public float CorpseStartHeight => _corpseStartHeight;
    }
}
