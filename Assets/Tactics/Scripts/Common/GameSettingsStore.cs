using System;
using UnityEngine;

namespace Tactics
{
    [Serializable]
    public sealed class GameSettings
    {
        public int ResolutionWidth = 0;
        public int ResolutionHeight = 0;
        public bool FullScreen = true;
        public float MasterVolume = 1f;
        public bool MasterMuted = false;
    }

    public static class GameSettingsStore
    {
        private const string WidthKey = "Tactics_Settings_ResolutionWidth";
        private const string HeightKey = "Tactics_Settings_ResolutionHeight";
        private const string FullScreenKey = "Tactics_Settings_FullScreen";
        private const string MasterVolumeKey = "Tactics_Settings_MasterVolume";
        private const string MasterMutedKey = "Tactics_Settings_MasterMuted";

        public static GameSettings Load()
        {
            return new GameSettings
            {
                ResolutionWidth = PlayerPrefs.GetInt(WidthKey, Screen.currentResolution.width),
                ResolutionHeight = PlayerPrefs.GetInt(HeightKey, Screen.currentResolution.height),
                FullScreen = PlayerPrefs.GetInt(FullScreenKey, Screen.fullScreen ? 1 : 0) == 1,
                MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, 1f)),
                MasterMuted = PlayerPrefs.GetInt(MasterMutedKey, 0) == 1
            };
        }

        public static void Save(GameSettings settings)
        {
            if (settings == null)
                return;

            PlayerPrefs.SetInt(WidthKey, settings.ResolutionWidth);
            PlayerPrefs.SetInt(HeightKey, settings.ResolutionHeight);
            PlayerPrefs.SetInt(FullScreenKey, settings.FullScreen ? 1 : 0);
            PlayerPrefs.SetFloat(MasterVolumeKey, Mathf.Clamp01(settings.MasterVolume));
            PlayerPrefs.SetInt(MasterMutedKey, settings.MasterMuted ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void ApplyDisplay(GameSettings settings)
        {
            if (settings == null)
                return;

            int width = settings.ResolutionWidth > 0 ? settings.ResolutionWidth : Screen.currentResolution.width;
            int height = settings.ResolutionHeight > 0 ? settings.ResolutionHeight : Screen.currentResolution.height;
            var mode = settings.FullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Screen.SetResolution(width, height, mode);
        }
    }
}
