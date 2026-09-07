using UnityEngine;
using UnityEngine.UI;
using ChibiRift.Core;

namespace ChibiRift.UI
{
    /// <summary>
    /// Main Menu (SRS 19.1). Exactly four entries: Play, Settings, How to Play, Quit.
    /// </summary>
    /// <remarks>
    /// There is deliberately no Continue button. SAVE-006 removed it in SRS v1.1 because MVP has
    /// no mid-run save, so Play always enters the Hub with the stored MetaProgression. Adding a
    /// Continue entry here would be a requirement violation, not a feature.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Buttons (SRS 19.1)")]
        [Tooltip("Enters the Hub with the saved MetaProgression (SAVE-006).")]
        [SerializeField] private Button _playButton;

        [Tooltip("Opens the settings panel (SRS 19.4).")]
        [SerializeField] private Button _settingsButton;

        [Tooltip("Opens the control guide (NFR-005, SRS 36).")]
        [SerializeField] private Button _howToPlayButton;

        [Tooltip("Exits the application.")]
        [SerializeField] private Button _quitButton;

        [Header("Panels")]
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private GameObject _howToPlayPanel;

        private void Awake()
        {
            // Exactly four bindings. There is no fifth: Continue is forbidden by SAVE-006.
            if (_playButton != null) _playButton.onClick.AddListener(OnPlay);
            if (_settingsButton != null) _settingsButton.onClick.AddListener(OnSettings);
            if (_howToPlayButton != null) _howToPlayButton.onClick.AddListener(OnHowToPlay);
            if (_quitButton != null) _quitButton.onClick.AddListener(OnQuit);
        }

        /// <summary>Play: straight to the Hub (SRS 19.1, SRS 7 step 2).</summary>
        public void OnPlay()
        {
            if (ServiceLocator.Current == null) return;
            ServiceLocator.Current.Get<ISceneFlowService>().LoadScene(SceneNames.Hub);
        }

        /// <summary>Settings (SRS 19.4).</summary>
        public void OnSettings()
        {
            if (_settingsPanel != null) _settingsPanel.SetActive(true);
        }

        /// <summary>How to Play (NFR-005, SRS 36).</summary>
        public void OnHowToPlay()
        {
            // TODO(SRS-36): fill the panel with the control guide: A/D, Space, Left Shift,
            // Mouse Left, Q/E/R, plus XP, the 3-card pick and the Run to Reward to Hub loop.
            if (_howToPlayPanel != null) _howToPlayPanel.SetActive(true);
        }

        /// <summary>Quit (SRS 19.1).</summary>
        public void OnQuit()
        {
            // TODO(SAVE-001): flush the meta save before quitting.
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
