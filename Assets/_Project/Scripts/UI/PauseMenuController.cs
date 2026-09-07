using UnityEngine;
using UnityEngine.UI;
using ChibiRift.Core;

namespace ChibiRift.UI
{
    /// <summary>
    /// Pause Menu (SRS 19.5). ESC opens it during a Run and stops all game time (PAU-001).
    /// Five entries are required: Resume, Settings, How to Play, Abandon Run and Quit to Main
    /// Menu (PAU-002). Abandon and Quit both go through a confirmation step (PAU-003).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseMenuController : MonoBehaviour
    {
        [Header("Buttons (PAU-002)")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _howToPlayButton;
        [SerializeField] private Button _abandonRunButton;
        [SerializeField] private Button _quitToMenuButton;

        [Header("Panels")]
        [SerializeField] private GameObject _root;

        [Tooltip("Shown before Abandon Run or Quit take effect (PAU-003).")]
        [SerializeField] private GameObject _confirmationPanel;

        /// <summary>True while the pause menu is up.</summary>
        public bool IsOpen { get; private set; }

        private void Update()
        {
            // TODO(PAU-001): open on IInputService.PausePressed during a Run.
            // TODO(PAU-005): do nothing while the Level Up panel is awaiting a choice.
            // TODO(SRS-19.5): outside a Run, ESC closes the current panel instead.
        }

        /// <summary>Resume (PAU-002).</summary>
        public void OnResume()
        {
            if (ServiceLocator.Current == null) return;
            ServiceLocator.Current.Get<IPauseService>().Resume(PauseReason.PauseMenu);
            IsOpen = false;
        }

        /// <summary>Abandon Run, after confirmation (PAU-003, PAU-004, RUN-008).</summary>
        public void OnAbandonRun()
        {
            // TODO(PAU-003): require the confirmation panel first.
            // TODO(PAU-004): tell RunManager to abandon. The Run still passes through Post-Run so
            //   the reward commits (RUN-006). UI cannot call RunManager directly, so this goes
            //   out over the EventBus.
        }

        /// <summary>Quit to Main Menu, after confirmation (PAU-002, PAU-003).</summary>
        public void OnQuitToMainMenu()
        {
            // TODO(PAU-003): require the confirmation panel first.
            // TODO(SRS-30): a discarded Run must not lose already-confirmed meta data.
        }
    }
}
