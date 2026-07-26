using UnityEngine;

namespace Tactics
{
    /// <summary>
    /// Applies persisted master audio settings to Unity's native audio pipeline.
    /// </summary>
    public static class UnityAudioSettingsService
    {
        /// <summary>
        /// Applies the effective master volume to every Unity AudioSource.
        /// </summary>
        public static bool ApplyMaster(float volume, bool muted)
        {
            AudioListener.volume = muted ? 0f : Mathf.Clamp01(volume);
            return true;
        }

        /// <summary>
        /// Loads and applies the persisted audio settings.
        /// </summary>
        public static bool ApplyFromStore()
        {
            var settings = GameSettingsStore.Load();
            return ApplyMaster(settings.MasterVolume, settings.MasterMuted);
        }
    }
}
