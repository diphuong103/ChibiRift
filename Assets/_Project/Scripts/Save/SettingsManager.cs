using UnityEngine;
using ChibiRift.Core;

namespace ChibiRift.Save
{
    /// <summary>
    /// Applies and persists the settings of SRS 19.4 (SAVE-002).
    /// Values live inside <see cref="MetaSave.Settings"/> so one file covers both progression
    /// and preferences. Key Rebind and Language are out of MVP scope (SRS 19.4, SRS 43 Q10).
    /// </summary>
    public sealed class SettingsManager : IGameService
    {
        private const string LogCategory = "Settings";

        private readonly SaveManager _saveManager;
        private readonly IAudioService _audio;
        private readonly ITelemetryService _telemetry;

        public SettingsManager(SaveManager saveManager, IAudioService audio, ITelemetryService telemetry)
        {
            _saveManager = saveManager;
            _audio = audio;
            _telemetry = telemetry;
        }

        /// <summary>The live settings block.</summary>
        public SettingsSave Current => _saveManager.Data.Settings;

        /// <summary>Pushes the stored settings into the audio, display and telemetry services.</summary>
        public void Apply()
        {
            SettingsSave settings = Current;

            _audio.MasterVolume = settings.MasterVolume;
            _audio.MusicVolume = settings.MusicVolume;
            _audio.SfxVolume = settings.SfxVolume;

            // TEL-004: telemetry must be switchable from Settings.
            _telemetry.IsEnabled = settings.TelemetryEnabled;

            Screen.SetResolution(settings.ResolutionWidth, settings.ResolutionHeight, settings.Fullscreen);
            GameLog.Info(LogCategory, "Settings applied.");
        }

        /// <summary>Writes the settings back to disk (SAVE-002).</summary>
        public void Persist() => _saveManager.Save();
    }
}
