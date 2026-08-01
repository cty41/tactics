using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Tactics
{
    /// <summary>
    /// Defines the supported session-wide gameplay playback speeds.
    /// </summary>
    public enum GamePlaybackSpeed
    {
        /// <summary>Runs gameplay at half normal speed.</summary>
        Half = -1,

        /// <summary>Runs gameplay at normal speed.</summary>
        Normal = 1,

        /// <summary>Runs gameplay at twice normal speed.</summary>
        Double = 2,

        /// <summary>Runs gameplay at four times normal speed.</summary>
        Quadruple = 4
    }

    /// <summary>
    /// Owns the requested gameplay speed, nested pause state, and effective Unity time scale.
    /// </summary>
    /// <remarks>
    /// The requested speed persists across scenes for the current process. A subsystem reset
    /// restores the deterministic 1x unpaused baseline. Infrastructure deadlines must remain
    /// realtime and must not use <see cref="DelayScaledAsync"/>.
    /// </remarks>
    public static class GameTimeService
    {
        private static GamePlaybackSpeed _playbackSpeed = GamePlaybackSpeed.Normal;
        private static int _pauseDepth;

        /// <summary>Gets the requested playback speed, including while gameplay is paused.</summary>
        public static GamePlaybackSpeed PlaybackSpeed => _playbackSpeed;

        /// <summary>Gets the requested playback speed as a numeric scale.</summary>
        public static float PlaybackScale => _playbackSpeed switch
        {
            GamePlaybackSpeed.Half => 0.5f,
            GamePlaybackSpeed.Normal => 1f,
            GamePlaybackSpeed.Double => 2f,
            GamePlaybackSpeed.Quadruple => 4f,
            _ => throw new InvalidOperationException($"Unsupported playback speed state: {_playbackSpeed}.")
        };

        /// <summary>Gets whether at least one pause owner is active.</summary>
        public static bool IsPaused => _pauseDepth > 0;

        /// <summary>Gets zero while paused; otherwise gets the requested playback scale.</summary>
        public static float EffectiveTimeScale => IsPaused ? 0f : PlaybackScale;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _playbackSpeed = GamePlaybackSpeed.Normal;
            _pauseDepth = 0;
            ApplyEffectiveTimeScale();
        }

        /// <summary>
        /// Selects one of the supported playback speeds and applies it unless gameplay is paused.
        /// </summary>
        /// <param name="speed">The requested 0.5x, 1x, 2x, or 4x playback speed.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for unsupported enum values.</exception>
        public static void SetPlaybackSpeed(GamePlaybackSpeed speed)
        {
            if (speed is not GamePlaybackSpeed.Half
                and not GamePlaybackSpeed.Normal
                and not GamePlaybackSpeed.Double
                and not GamePlaybackSpeed.Quadruple)
            {
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "Playback speed must be 0.5x, 1x, 2x, or 4x.");
            }

            _playbackSpeed = speed;
            ApplyEffectiveTimeScale();
        }

        /// <summary>Advances the requested speed through 1x, 2x, 4x, 0.5x, then back to 1x.</summary>
        /// <returns>The newly selected playback speed.</returns>
        public static GamePlaybackSpeed CyclePlaybackSpeed()
        {
            var next = _playbackSpeed switch
            {
                GamePlaybackSpeed.Normal => GamePlaybackSpeed.Double,
                GamePlaybackSpeed.Double => GamePlaybackSpeed.Quadruple,
                GamePlaybackSpeed.Quadruple => GamePlaybackSpeed.Half,
                _ => GamePlaybackSpeed.Normal
            };

            SetPlaybackSpeed(next);
            return next;
        }

        /// <summary>Adds one pause owner and immediately freezes scaled gameplay time.</summary>
        public static void Pause()
        {
            _pauseDepth++;
            ApplyEffectiveTimeScale();
        }

        /// <summary>
        /// Releases one pause owner. Calling this at zero depth leaves gameplay unpaused.
        /// </summary>
        public static void Resume()
        {
            if (_pauseDepth > 0)
                _pauseDepth--;

            ApplyEffectiveTimeScale();
        }

        /// <summary>Clears every pause owner and restores the currently requested speed.</summary>
        public static void ForceResume()
        {
            _pauseDepth = 0;
            ApplyEffectiveTimeScale();
        }

        /// <summary>
        /// Waits for a duration measured in scaled gameplay seconds.
        /// </summary>
        /// <param name="seconds">A non-negative gameplay duration in seconds.</param>
        /// <param name="cancellationToken">A token that can cancel the wait even while paused.</param>
        /// <returns>A task that completes after the scaled duration elapses.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="seconds"/> is negative.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
        public static async Task DelayScaledAsync(
            float seconds,
            CancellationToken cancellationToken = default)
        {
            if (seconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "Delay duration cannot be negative.");

            cancellationToken.ThrowIfCancellationRequested();
            if (seconds == 0f)
                return;

            await Awaitable.WaitForSecondsAsync(seconds, cancellationToken);
        }

        private static void ApplyEffectiveTimeScale()
        {
            Time.timeScale = EffectiveTimeScale;
        }
    }
}
