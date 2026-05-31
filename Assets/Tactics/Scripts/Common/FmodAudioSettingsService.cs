using FMOD;
using FMOD.Studio;
using FMODUnity;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics
{
    public static class FmodAudioSettingsService
    {
        private const string MasterBusPath = "bus:/";

        public static bool ApplyMaster(float volume, bool muted)
        {
            if (!TryGetMasterBus(out var bus))
                return false;

            volume = Mathf.Clamp01(volume);
            var volumeResult = bus.setVolume(volume);
            var muteResult = bus.setMute(muted);

            if (volumeResult != RESULT.OK)
                TLog.Warning($"[FmodAudioSettingsService] Failed to set master volume: {volumeResult}");
            if (muteResult != RESULT.OK)
                TLog.Warning($"[FmodAudioSettingsService] Failed to set master mute: {muteResult}");

            return volumeResult == RESULT.OK && muteResult == RESULT.OK;
        }

        public static bool ApplyFromStore()
        {
            var settings = GameSettingsStore.Load();
            return ApplyMaster(settings.MasterVolume, settings.MasterMuted);
        }

        private static bool TryGetMasterBus(out Bus bus)
        {
            bus = default;

            try
            {
                var result = RuntimeManager.StudioSystem.getBus(MasterBusPath, out bus);
                if (result == RESULT.OK && bus.isValid())
                    return true;

                TLog.Warning($"[FmodAudioSettingsService] FMOD master bus not available: {result}");
                return false;
            }
            catch (System.Exception ex)
            {
                TLog.Warning($"[FmodAudioSettingsService] FMOD master bus lookup failed: {ex.Message}");
                return false;
            }
        }
    }
}
