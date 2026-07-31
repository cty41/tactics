using DG.Tweening;
using UnityEngine;

namespace Tactics.Common.Skills.Graph
{
    /// <summary>
    /// Builds a deterministic projectile tween shared by runtime and editor preview.
    /// </summary>
    public static class ProjectileTweenBuilder
    {
        /// <summary>
        /// Creates a paused trajectory tween. The caller owns lifecycle and playback.
        /// </summary>
        public static Tween Build(
            Transform projectile,
            ProjectileVisualProfile profile,
            Vector3 start,
            Vector3 end,
            float duration)
        {
            projectile.position = start;
            projectile.localScale = Vector3.one * profile.Scale;
            Vector3 baseScale = projectile.localScale;
            float arcHeight = profile.TrajectoryStyle == ProjectileTrajectoryStyle.MagicStraight
                ? 0f
                : profile.ArcHeight;

            return DOTween.To(
                    () => 0f,
                    progress =>
                    {
                        float t = Mathf.Clamp01(progress);
                        Vector3 position = Vector3.LerpUnclamped(start, end, t);
                        position.y += 4f * arcHeight * t * (1f - t);
                        projectile.position = position;

                        if (profile.RotateAlongTangent)
                        {
                            Vector3 tangent = end - start;
                            tangent.y += 4f * arcHeight * (1f - 2f * t);
                            float angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
                            projectile.rotation = Quaternion.Euler(0f, 0f, angle);
                        }

                        if (profile.PulseAmount > 0f)
                        {
                            float pulse = 1f + Mathf.Sin(t * Mathf.PI * 2f * profile.PulseCycles) *
                                profile.PulseAmount;
                            projectile.localScale = baseScale * pulse;
                        }
                    },
                    1f,
                    Mathf.Max(0.01f, duration))
                .SetEase(Ease.Linear)
                .Pause();
        }
    }
}
