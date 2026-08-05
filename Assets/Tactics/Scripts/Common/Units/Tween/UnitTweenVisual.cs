using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DG.Tweening;
using Tactics.Common.Utilities;
using UnityEngine;

namespace Tactics.Common.Units.Tween
{
    /// <summary>
    /// Owns all transient DOTween state for one standard ground-unit visual root.
    /// </summary>
    /// <remarks>
    /// The logical unit transform, shadow and UI remain outside VisualRoot. Every foreground
    /// animation restores the authored local pose before handing control back to idle.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class UnitTweenVisual : MonoBehaviour
    {
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private SpriteRenderer _primaryRenderer;
        [SerializeField] private StandardUnitTweenProfile _profile;

        private Sequence _idleSequence;
        private Sequence _foregroundSequence;
        private Sequence _moveSequence;
        private Vector3 _basePosition;
        private Quaternion _baseRotation;
        private Vector3 _baseScale;
        private VisualPriority _priority;
        private Unit _unit;
        private FourDirectionSpriteVisual _directionalVisual;
        private int _foregroundVersion;
        private UnitPresentationLifecycle _lifecycle = UnitPresentationLifecycle.Alive;
        private Action _deathHandoff;
        private bool _deathHandoffInvoked;
        private readonly Dictionary<SpriteRenderer, bool> _rendererVisibility = new();

        public Transform VisualRoot
        {
            get { return ResolveVisualRoot(); }
        }

        public SpriteRenderer PrimaryRenderer
        {
            get { return ResolvePrimaryRenderer(); }
        }

        public StandardUnitTweenProfile Profile => _profile;
        public Vector3 BasePosition => _basePosition;
        public Quaternion BaseRotation => _baseRotation;
        public Vector3 BaseScale => _baseScale;
        internal UnitPresentationLifecycle Lifecycle => _lifecycle;

        /// <summary>
        /// Captures read-only transient state for the Play Mode inspector.
        /// </summary>
        internal UnitTweenVisualDebugSnapshot GetDebugSnapshot()
        {
            return new UnitTweenVisualDebugSnapshot(
                _lifecycle,
                _priority.ToString(),
                IsSequenceActive(_idleSequence),
                IsSequenceActive(_moveSequence),
                IsSequenceActive(_foregroundSequence),
                _foregroundVersion,
                _deathHandoffInvoked);
        }

        private void Awake()
        {
            CaptureBaseline();
        }

        private void OnEnable()
        {
            _unit = GetComponent<Unit>();
            _directionalVisual = GetComponent<FourDirectionSpriteVisual>();
            if (_unit != null)
                _unit.UnitAttacked += OnUnitAttacked;
            if (Application.isPlaying && _lifecycle == UnitPresentationLifecycle.Alive)
                StartIdle();
        }

        private void OnDisable()
        {
            if (_unit != null)
                _unit.UnitAttacked -= OnUnitAttacked;
            StopAllVisualTweens();
        }

        private void OnDestroy()
        {
            if (_unit != null)
                _unit.UnitAttacked -= OnUnitAttacked;
            StopAllVisualTweens();
        }

        /// <summary>
        /// Assigns preview-only dependencies without mutating the prefab asset.
        /// </summary>
        public void ConfigureForPreview(
            Transform visualRoot,
            SpriteRenderer primaryRenderer,
            StandardUnitTweenProfile profile)
        {
            _visualRoot = visualRoot;
            _primaryRenderer = primaryRenderer;
            _profile = profile;
            CaptureBaseline();
        }

        /// <summary>
        /// Starts one foreground action and invokes release exactly once at its authored marker.
        /// </summary>
        public Task PlayActionAsync(
            UnitVisualAction action,
            Vector3 targetWorldPosition,
            Action release,
            CancellationToken cancellationToken)
        {
            return PlayActionAsync(
                action,
                null,
                targetWorldPosition,
                null,
                release,
                cancellationToken);
        }

        /// <summary>
        /// Starts one foreground action with an optional semantic pose and release preparation.
        /// </summary>
        /// <remarks>
        /// Release ordering is prepare visual state, restore a release-bound pose, then invoke
        /// gameplay. This prevents a projectile from starting while its carried weapon remains.
        /// </remarks>
        public Task PlayActionAsync(
            UnitVisualAction action,
            UnitPoseFamily poseFamily,
            Vector3 targetWorldPosition,
            Action prepareRelease,
            Action release,
            CancellationToken cancellationToken)
        {
            if (_lifecycle != UnitPresentationLifecycle.Alive)
                return Task.CompletedTask;

            if (action == UnitVisualAction.None || _profile == null || ResolveVisualRoot() == null)
            {
                prepareRelease?.Invoke();
                release?.Invoke();
                return Task.CompletedTask;
            }

            UnitPoseFamily resolvedFamily = ResolvePoseFamily(action, poseFamily);
            UnitPoseExitPolicy exitPolicy = resolvedFamily != null
                ? resolvedFamily.ExitPolicy
                : UnitPoseExitPolicy.RecoveryStart;

            return PlayForegroundAsync(
                VisualPriority.Action,
                () => ApplyPose(resolvedFamily),
                () =>
                {
                    Vector3 direction = targetWorldPosition - transform.position;
                    var plan = UnitTweenSequenceBuilder.BuildAction(
                        action,
                        _visualRoot,
                        _profile,
                        _basePosition,
                        _baseRotation,
                        _baseScale,
                        direction,
                        exitPolicy);
                    plan.Sequence.InsertCallback(plan.ReleaseTime, () =>
                    {
                        prepareRelease?.Invoke();
                        if (plan.PoseRestoreTime <= plan.ReleaseTime + 0.0001f)
                            ClearActionPose();
                        release?.Invoke();
                    });
                    if (plan.PoseRestoreTime > plan.ReleaseTime + 0.0001f)
                        plan.Sequence.InsertCallback(plan.PoseRestoreTime, ClearActionPose);
                    return plan.Sequence;
                },
                cancellationToken);
        }

        /// <summary>
        /// Starts the per-segment paper-cutout movement loop.
        /// </summary>
        public void BeginMoveStep(Vector3 worldDirection)
        {
            if (_lifecycle != UnitPresentationLifecycle.Alive ||
                _profile == null || ResolveVisualRoot() == null || _priority > VisualPriority.Action)
                return;

            KillSequence(ref _moveSequence);
            InterruptForeground();
            StopIdle();
            RestoreBaseline();
            ClearActionPose();
            _priority = VisualPriority.Move;
            _moveSequence = UnitTweenSequenceBuilder.BuildMoveLoop(
                    _visualRoot,
                    _profile,
                    _basePosition,
                    _baseScale,
                    worldDirection)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }

        /// <summary>
        /// Settles the visual when one path segment reaches its destination.
        /// </summary>
        public void EndMoveStep()
        {
            if (_lifecycle != UnitPresentationLifecycle.Alive ||
                _profile == null || ResolveVisualRoot() == null || _priority != VisualPriority.Move)
                return;

            KillSequence(ref _moveSequence);
            InterruptForeground();
            RestoreBaseline();
            ClearActionPose();
            _foregroundSequence = UnitTweenSequenceBuilder.BuildSettle(
                    _visualRoot,
                    _profile,
                    _basePosition,
                    _baseRotation,
                    _baseScale)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() =>
                {
                    if (_lifecycle != UnitPresentationLifecycle.Alive)
                        return;
                    _foregroundSequence = null;
                    _priority = VisualPriority.Idle;
                    StartIdle();
                });
        }

        /// <summary>
        /// Plays a deterministic hit response without blocking damage resolution.
        /// </summary>
        public void PlayHit(Vector3 attackerWorldPosition)
        {
            if (_lifecycle != UnitPresentationLifecycle.Alive ||
                _profile == null || ResolveVisualRoot() == null)
                return;

            Vector3 direction = transform.position - attackerWorldPosition;
            _ = PlayForegroundAsync(
                VisualPriority.Hit,
                () => ApplyPose(ResolveDirectionalVisual()?.ActionPoseProfile?.HitFamily),
                () =>
                {
                    var plan = UnitTweenSequenceBuilder.BuildHitPlan(
                        _visualRoot,
                        _profile,
                        _basePosition,
                        _baseRotation,
                        _baseScale,
                        direction);
                    plan.Sequence.InsertCallback(plan.PoseRestoreTime, ClearActionPose);
                    return plan.Sequence;
                },
                destroyCancellationToken);
        }

        /// <summary>
        /// Enters the terminal presentation state and hands visual ownership to a corpse once.
        /// </summary>
        /// <returns>The lethal hit sequence, or null when the handoff had to complete immediately.</returns>
        internal Sequence PlayDying(Vector3 attackerWorldPosition, Action handoff)
        {
            if (_lifecycle != UnitPresentationLifecycle.Alive ||
                _profile == null || ResolveVisualRoot() == null)
            {
                handoff?.Invoke();
                return null;
            }

            StopIdle();
            KillSequence(ref _moveSequence);
            InterruptForeground();
            RestoreBaseline();
            ClearActionPose();
            ApplyPose(ResolveDirectionalVisual()?.ActionPoseProfile?.HitFamily);

            _lifecycle = UnitPresentationLifecycle.Dying;
            _priority = VisualPriority.Corpse;
            _deathHandoff = handoff;
            _deathHandoffInvoked = false;

            Vector3 incomingDirection = transform.position - attackerWorldPosition;
            UnitTweenPosePlan plan = UnitTweenSequenceBuilder.BuildLethalHitPlan(
                _visualRoot,
                _profile,
                _basePosition,
                _baseRotation,
                _baseScale,
                incomingDirection);
            Sequence ownedSequence = plan.Sequence;
            _foregroundSequence = ownedSequence;
            ownedSequence
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(CompleteDeathHandoff)
                .OnKill(CompleteDeathHandoff);
            return ownedSequence;
        }

        /// <summary>
        /// Restores terminal preview state without changing any runtime asset.
        /// </summary>
        internal void ResetPresentationForPreview()
        {
            StopIdle();
            KillSequence(ref _moveSequence);
            InterruptForeground();
            RestorePresentationVisibility();
            _deathHandoff = null;
            _deathHandoffInvoked = false;
            _lifecycle = UnitPresentationLifecycle.Alive;
            _priority = VisualPriority.Idle;
            RestoreBaseline();
            ClearActionPose();
        }

        /// <summary>
        /// Changes equipment-dependent artwork without changing gameplay state.
        /// </summary>
        public void SetVisualState(UnitVisualState state)
        {
            if (_lifecycle != UnitPresentationLifecycle.Alive)
                return;
            ResolveDirectionalVisual()?.SetVisualState(state, ResolveFacing());
        }

        /// <summary>
        /// Clears any transient pose and re-resolves idle for the current visual state.
        /// </summary>
        public void ClearActionPose()
        {
            ResolveDirectionalVisual()?.ClearPose(ResolveFacing());
        }

        /// <summary>
        /// Kills all visual tweens and restores the authored pose.
        /// </summary>
        public void StopAllVisualTweens()
        {
            StopIdle();
            KillSequence(ref _moveSequence);
            InterruptForeground();
            if (_lifecycle == UnitPresentationLifecycle.Dying)
            {
                CompleteDeathHandoff();
                return;
            }
            if (_lifecycle == UnitPresentationLifecycle.Removed)
                return;

            _priority = VisualPriority.Idle;
            RestoreBaseline();
            ClearActionPose();
        }

        private async Task PlayForegroundAsync(
            VisualPriority requestedPriority,
            Action beginVisual,
            Func<Sequence> sequenceFactory,
            CancellationToken cancellationToken)
        {
            if (_lifecycle != UnitPresentationLifecycle.Alive || _priority > requestedPriority)
                return;

            StopIdle();
            KillSequence(ref _moveSequence);
            int foregroundVersion = ++_foregroundVersion;
            KillSequence(ref _foregroundSequence);
            RestoreBaseline();
            beginVisual?.Invoke();
            _priority = requestedPriority;

            var completion = new TaskCompletionSource<bool>();
            Sequence ownedSequence = null;
            try
            {
                ownedSequence = sequenceFactory();
                _foregroundSequence = ownedSequence;
                if (ownedSequence == null)
                {
                    completion.TrySetResult(true);
                }
                else
                {
                    ownedSequence
                        .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                        .OnComplete(() => completion.TrySetResult(true))
                        .OnKill(() => completion.TrySetResult(true));
                }

                using var registration = cancellationToken.Register(() =>
                {
                    if (!completion.TrySetCanceled(cancellationToken))
                        return;

                    if (ownedSequence != null && ownedSequence.IsActive())
                        ownedSequence.Kill(false);
                });
                await completion.Task;
            }
            finally
            {
                if (foregroundVersion == _foregroundVersion)
                {
                    if (_foregroundSequence == ownedSequence)
                        _foregroundSequence = null;
                    RestoreBaseline();
                    ClearActionPose();
                    if (this != null && isActiveAndEnabled &&
                        _lifecycle == UnitPresentationLifecycle.Alive)
                    {
                        _priority = VisualPriority.Idle;
                        StartIdle();
                    }
                }
            }
        }

        private void StartIdle()
        {
            if (_lifecycle != UnitPresentationLifecycle.Alive ||
                !Application.isPlaying || _profile == null || ResolveVisualRoot() == null ||
                _idleSequence != null || _priority != VisualPriority.Idle)
            {
                return;
            }

            RestoreBaseline();
            _idleSequence = UnitTweenSequenceBuilder.BuildIdle(
                    _visualRoot,
                    _profile,
                    _basePosition,
                    _baseScale)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }

        private void OnUnitAttacked(UnitAttackedEventArgs eventArgs)
        {
            if (_lifecycle != UnitPresentationLifecycle.Alive ||
                eventArgs.AffectedUnit == null || eventArgs.AffectedUnit.IsDowned ||
                eventArgs.AffectedUnit.Health <= 0f)
            {
                return;
            }

            Vector3 attackerPosition = eventArgs.AttackingUnit?.WorldPosition.ToVector3()
                ?? transform.position - Vector3.right;
            PlayHit(attackerPosition);
        }

        private void StopIdle()
        {
            KillSequence(ref _idleSequence);
        }

        private void CaptureBaseline()
        {
            var visualRoot = ResolveVisualRoot();
            if (visualRoot == null)
                return;

            _basePosition = visualRoot.localPosition;
            _baseRotation = visualRoot.localRotation;
            _baseScale = visualRoot.localScale;
        }

        private void RestoreBaseline()
        {
            var visualRoot = ResolveVisualRoot();
            if (visualRoot == null)
                return;

            visualRoot.localPosition = _basePosition;
            visualRoot.localRotation = _baseRotation;
            visualRoot.localScale = _baseScale;
        }

        private Transform ResolveVisualRoot()
        {
            if (_visualRoot != null)
                return _visualRoot;

            var child = transform.Find("VisualRoot");
            _visualRoot = child != null ? child : transform.Find("Sprite");
            return _visualRoot;
        }

        private SpriteRenderer ResolvePrimaryRenderer()
        {
            if (_primaryRenderer != null)
                return _primaryRenderer;

            foreach (var renderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.gameObject.name == "Sprite")
                {
                    _primaryRenderer = renderer;
                    break;
                }
            }

            return _primaryRenderer;
        }

        private FourDirectionSpriteVisual ResolveDirectionalVisual()
        {
            if (_directionalVisual == null)
                _directionalVisual = GetComponent<FourDirectionSpriteVisual>();
            return _directionalVisual;
        }

        private UnitPoseFamily ResolvePoseFamily(UnitVisualAction action, UnitPoseFamily explicitFamily)
        {
            var profile = ResolveDirectionalVisual()?.ActionPoseProfile;
            return profile != null ? profile.ResolveFamily(action, explicitFamily) : explicitFamily;
        }

        private FacingDirection ResolveFacing()
        {
            return _unit != null
                ? _unit.Facing
                : ResolveDirectionalVisual()?.LastFacing ?? FacingDirection.South;
        }

        private void ApplyPose(UnitPoseFamily family)
        {
            if (family != null)
                ResolveDirectionalVisual()?.SetPose(family, ResolveFacing());
        }

        private void InterruptForeground()
        {
            ++_foregroundVersion;
            KillSequence(ref _foregroundSequence);
        }

        private void CompleteDeathHandoff()
        {
            if (_lifecycle != UnitPresentationLifecycle.Dying || _deathHandoffInvoked)
                return;

            _deathHandoffInvoked = true;
            _foregroundSequence = null;
            CaptureAndHidePresentation();
            _lifecycle = UnitPresentationLifecycle.Removed;
            Action handoff = _deathHandoff;
            _deathHandoff = null;
            handoff?.Invoke();
        }

        private void CaptureAndHidePresentation()
        {
            _rendererVisibility.Clear();
            foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                _rendererVisibility[renderer] = renderer.enabled;
                renderer.enabled = false;
            }
        }

        private void RestorePresentationVisibility()
        {
            foreach (KeyValuePair<SpriteRenderer, bool> entry in _rendererVisibility)
            {
                if (entry.Key != null)
                    entry.Key.enabled = entry.Value;
            }
            _rendererVisibility.Clear();
        }

        private static void KillSequence(ref Sequence sequence)
        {
            if (sequence == null)
                return;

            if (sequence.IsActive())
                sequence.Kill(false);
            sequence = null;
        }

        private static bool IsSequenceActive(Sequence sequence)
        {
            return sequence != null && sequence.IsActive();
        }

        private enum VisualPriority
        {
            Idle = 0,
            Move = 1,
            Action = 2,
            Hit = 3,
            Corpse = 4
        }
    }

    /// <summary>
    /// Separates the living foreground animation channel from terminal visual ownership.
    /// </summary>
    internal enum UnitPresentationLifecycle
    {
        Alive,
        Dying,
        Removed
    }


    /// <summary>
    /// Immutable editor-facing diagnostics for one unit presentation lifecycle.
    /// </summary>
    internal readonly struct UnitTweenVisualDebugSnapshot
    {
        internal UnitTweenVisualDebugSnapshot(
            UnitPresentationLifecycle lifecycle,
            string foregroundPriority,
            bool isIdleTweenActive,
            bool isMoveTweenActive,
            bool isForegroundTweenActive,
            int foregroundVersion,
            bool isDeathHandoffComplete)
        {
            Lifecycle = lifecycle;
            ForegroundPriority = foregroundPriority;
            IsIdleTweenActive = isIdleTweenActive;
            IsMoveTweenActive = isMoveTweenActive;
            IsForegroundTweenActive = isForegroundTweenActive;
            ForegroundVersion = foregroundVersion;
            IsDeathHandoffComplete = isDeathHandoffComplete;
        }

        internal UnitPresentationLifecycle Lifecycle { get; }
        internal string ForegroundPriority { get; }
        internal bool IsIdleTweenActive { get; }
        internal bool IsMoveTweenActive { get; }
        internal bool IsForegroundTweenActive { get; }
        internal int ForegroundVersion { get; }
        internal bool IsDeathHandoffComplete { get; }
    }
}
