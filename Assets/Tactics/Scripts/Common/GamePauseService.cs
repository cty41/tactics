using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics
{
    public static class GamePauseService
    {
        private static int s_pauseDepth;
        private static float s_previousTimeScale = 1f;

        public static bool IsPaused => s_pauseDepth > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_pauseDepth = 0;
            s_previousTimeScale = 1f;
            Time.timeScale = 1f;
        }

        public static void Pause()
        {
            if (s_pauseDepth == 0)
            {
                s_previousTimeScale = Time.timeScale <= 0f ? 1f : Time.timeScale;
                Time.timeScale = 0f;
                TLog.Info("[GamePauseService] Game paused.");
            }

            s_pauseDepth++;
        }

        public static void Resume()
        {
            if (s_pauseDepth <= 0)
            {
                Time.timeScale = 1f;
                return;
            }

            s_pauseDepth--;
            if (s_pauseDepth != 0)
                return;

            Time.timeScale = s_previousTimeScale <= 0f ? 1f : s_previousTimeScale;
            TLog.Info("[GamePauseService] Game resumed.");
        }

        public static void ForceResume()
        {
            s_pauseDepth = 0;
            Time.timeScale = s_previousTimeScale <= 0f ? 1f : s_previousTimeScale;
        }
    }
}
