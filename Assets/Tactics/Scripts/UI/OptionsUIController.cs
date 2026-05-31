using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Tactics.Runtime.Utilities;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.UI
{
    public sealed class OptionsUIController : UIControllerBase
    {
        private static bool s_returnToPauseMenu;

        private DropdownField _resolutionDropdown;
        private Toggle _fullscreenToggle;
        private Slider _masterVolumeSlider;
        private Toggle _muteToggle;
        private Button _applyButton;
        private Button _backButton;
        private Label _audioStatusLabel;
        private List<Resolution> _resolutions = new();
        private bool _wired;

        public static async System.Threading.Tasks.Task ShowAsync(bool returnToPauseMenu)
        {
            s_returnToPauseMenu = returnToPauseMenu;
            await UIManager.Instance.ShowAsync(UIManager.UIId.Options);
        }

        protected override void OnShown()
        {
            if (_wired) return;
            StartCoroutine(WireDelayed());
        }

        protected override void OnHidden()
        {
            Unwire();
        }

        private IEnumerator WireDelayed()
        {
            yield return null;
            Wire();
        }

        private void Wire()
        {
            var root = Ui.GetRootElement(UIManager.UIId.Options);
            if (root == null)
            {
                TLog.Warning("[OptionsUIController] Could not get root visual element.");
                return;
            }

            _resolutionDropdown = root.Q<DropdownField>("ResolutionDropdown");
            _fullscreenToggle = root.Q<Toggle>("FullscreenToggle");
            _masterVolumeSlider = root.Q<Slider>("MasterVolumeSlider");
            _muteToggle = root.Q<Toggle>("MuteToggle");
            _applyButton = root.Q<Button>("ApplyButton");
            _backButton = root.Q<Button>("BackButton");
            _audioStatusLabel = root.Q<Label>("AudioStatusLabel");

            BuildResolutionChoices();
            LoadSettingsIntoControls();

            if (_applyButton != null) _applyButton.clicked += OnApplyClicked;
            if (_backButton != null) _backButton.clicked += OnBackClicked;
            _wired = true;
        }

        private void Unwire()
        {
            if (_applyButton != null) _applyButton.clicked -= OnApplyClicked;
            if (_backButton != null) _backButton.clicked -= OnBackClicked;
            _wired = false;
        }

        private void BuildResolutionChoices()
        {
            if (_resolutionDropdown == null)
                return;

            _resolutions = Screen.resolutions
                .GroupBy(r => (r.width, r.height))
                .Select(g => g.Last())
                .OrderBy(r => r.width)
                .ThenBy(r => r.height)
                .ToList();

            if (_resolutions.Count == 0)
                _resolutions.Add(Screen.currentResolution);

            _resolutionDropdown.choices = _resolutions
                .Select(r => $"{r.width} x {r.height}")
                .ToList();
        }

        private void LoadSettingsIntoControls()
        {
            var settings = GameSettingsStore.Load();

            if (_resolutionDropdown != null)
            {
                int index = _resolutions.FindIndex(r => r.width == settings.ResolutionWidth && r.height == settings.ResolutionHeight);
                if (index < 0)
                    index = Mathf.Max(0, _resolutions.FindIndex(r => r.width == Screen.width && r.height == Screen.height));
                _resolutionDropdown.index = Mathf.Clamp(index, 0, _resolutionDropdown.choices.Count - 1);
            }

            if (_fullscreenToggle != null) _fullscreenToggle.value = settings.FullScreen;
            if (_masterVolumeSlider != null) _masterVolumeSlider.value = settings.MasterVolume;
            if (_muteToggle != null) _muteToggle.value = settings.MasterMuted;
            SetAudioStatus(string.Empty);
        }

        private void OnApplyClicked()
        {
            var settings = GameSettingsStore.Load();
            if (_resolutionDropdown != null && _resolutionDropdown.index >= 0 && _resolutionDropdown.index < _resolutions.Count)
            {
                var resolution = _resolutions[_resolutionDropdown.index];
                settings.ResolutionWidth = resolution.width;
                settings.ResolutionHeight = resolution.height;
            }

            settings.FullScreen = _fullscreenToggle?.value ?? settings.FullScreen;
            settings.MasterVolume = Mathf.Clamp01(_masterVolumeSlider?.value ?? settings.MasterVolume);
            settings.MasterMuted = _muteToggle?.value ?? settings.MasterMuted;

            GameSettingsStore.Save(settings);
            GameSettingsStore.ApplyDisplay(settings);
            bool audioApplied = FmodAudioSettingsService.ApplyMaster(settings.MasterVolume, settings.MasterMuted);
            SetAudioStatus(audioApplied ? "Audio applied" : "FMOD master bus unavailable");
        }

        private void OnBackClicked()
        {
            UIManager.Instance.Hide(UIManager.UIId.Options);
            if (!s_returnToPauseMenu)
                UIManager.Instance.Show(UIManager.UIId.Home);
        }

        private void SetAudioStatus(string text)
        {
            if (_audioStatusLabel != null)
                _audioStatusLabel.text = text;
        }
    }
}
