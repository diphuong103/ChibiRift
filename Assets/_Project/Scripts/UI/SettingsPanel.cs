using UnityEngine;
using UnityEngine.UI;

namespace ChibiRift.UI
{
    /// <summary>
    /// Settings (SRS 19.4). MVP scope: master, music and SFX volume, fullscreen, resolution and
    /// the telemetry toggle (TEL-004). Key Rebind is a Should and Language is a Could; neither
    /// is built here (SRS 43 Q10).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingsPanel : MonoBehaviour
    {
        [Header("Audio (SRS 19.4)")]
        [SerializeField] private Slider _masterVolumeSlider;
        [SerializeField] private Slider _musicVolumeSlider;
        [SerializeField] private Slider _sfxVolumeSlider;

        [Header("Display (SRS 19.4)")]
        [SerializeField] private Toggle _fullscreenToggle;
        [SerializeField] private Dropdown _resolutionDropdown;

        [Header("Privacy (TEL-004)")]
        [Tooltip("Local telemetry on or off. Nothing is ever sent over the network.")]
        [SerializeField] private Toggle _telemetryToggle;

        private void OnEnable()
        {
            // TODO(SAVE-002): load the current values from SettingsManager into the widgets.
        }

        /// <summary>Applies and persists the current widget values (SAVE-002, TEL-004).</summary>
        public void OnApply()
        {
            // TODO(SAVE-002): push the values into SettingsManager, apply, then persist.
        }
    }
}
