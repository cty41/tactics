using System;
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
        private int _foregroundVersion;

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

        private void Awake()
        {
            CaptureBaseline();
        }

        private void OnEnable()
        {
            _unit = GetComponent<Unit>();
            if (_unit != null)
                _unit.UnitAttacked += OnUnitAttacked;
            if (Application.isPlaying)
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
            if (action == UnitVisualAction.None || _profile == null || ResolveVisualRoot() == null)
            {
                release?.Invoke();
                return Task.CompletedTask;
            }

            return PlayForegroundAsync(
                VisualPriority.Action,
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
                        direction);
                    plan.Sequence.InsertCallback(plan.ReleaseTime, () => release?.Invoke());
                    return plan.Sequence;
                },
                cancellationToken);
        }

        /// <summary>
        /// Starts the per-segment paper-cutout movement loop.
        /// </summary>
        public void BeginMoveStep(Vector3 worldDirection)
        {
            if (_profile == null || ResolveVisualRoot() == null || _priority > VisualPriority.Move)
                return;

            KillSequence(ref _moveSequence);
            InterruptForeground();
            StopIdle();
            RestoreBaseline();
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
            if (_profile == null || ResolveVisualRoot() == null || _priority != VisualPriority.Move)
                return;

            KillSequence(ref _moveSequence);
            InterruptForeground();
            RestoreBaseline();
            _foregroundSequence = UnitTweenSequenceBuilder.BuildSettle(
                    _visualRoot,
                    _profile,
                    _basePosition,
                    _baseRotation,
                    _baseScale)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() =>
                {
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
            if (_profile == null || ResolveVisualRoot() == null)
                return;

            Vector3 direction = transform.position - attackerWorldPosition;
            _ = PlayForegroundAsync(
                VisualPriority.Hit,
                () => UnitTweenSequenceBuilder.BuildHit(
                    _visualRoot,
                    _profile,
                    _basePosition,
                    _baseRotation,
                    _baseScale,
                    direction),
                destroyCancellationToken);
        }

        /// <summary>
        /// Kills all visual tweens and restores the authored pose.
        /// </summary>
        public void StopAllVisualTweens()
        {
            StopIdle();
            KillSequence(ref _moveSequence);
            InterruptForeground();
            _priority = VisualPriority.Idle;
            RestoreBaseline();
        }

        private async Task PlayForegroundAsync(
            VisualPriority requestedPriority,
            Func<Sequence> sequenceFactory,
            CancellationToken cancellationToken)
        {
            if (_priority > requestedPriority)
                return;

            StopIdle();
            KillSequence(ref _moveSequence);
            int foregroundVersion = ++_foregroundVersion;
            KillSequence(ref _foregroundSequence);
            RestoreBaseline();
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
                    if (this != null && isActiveAndEnabled)
                    {
                        _priority = VisualPriority.Idle;
                        StartIdle();
                    }
                }
            }
        }

        private void StartIdle()
        {
            if (!Application.isPlaying || _profile == null || ResolveVisualRoot() == null ||
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
            if (eventArgs.AffectedUnit == null || eventArgs.AffectedUnit.IsDowned ||
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

        private void InterruptForeground()
        {
            ++_foregroundVersion;
            KillSequence(ref _foregroundSequence);
        }

        private static void KillSequence(ref Sequence sequence)
        {
            if (sequence == null)
                return;

            if (sequence.IsActive())
                sequence.Kill(false);
            sequence = null;
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
}
