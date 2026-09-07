using UnityEngine;
using UnityEngine.UI;
using ChibiRift.Core;

namespace ChibiRift.UI
{
    /// <summary>
    /// Post-Run summary (SRS 18). Reached by all three endings: Victory, Death and Abandon
    /// (RUN-002, RUN-003, RUN-008). Shows the result (RUN-004) and the Gold and Gem haul that
    /// commits to the wallet exactly once (RUN-005, RUN-006).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PostRunPanel : MonoBehaviour
    {
        [Header("Summary (RUN-004)")]
        [SerializeField] private Text _outcomeLabel;
        [SerializeField] private Text _durationLabel;
        [SerializeField] private Text _stageWaveLabel;
        [SerializeField] private Text _levelLabel;

        [Header("Rewards (RUN-006)")]
        [SerializeField] private Text _goldLabel;
        [SerializeField] private Text _gemLabel;

        [Header("Navigation (RUN-007)")]
        [SerializeField] private Button _returnToHubButton;

        private void Awake()
        {
            if (_returnToHubButton != null) _returnToHubButton.onClick.AddListener(OnReturnToHub);
        }

        private void OnEnable()
        {
            // TODO(RUN-004): subscribe to RunEndedEvent and fill the labels from its payload.
            // TODO(RUN-002/003/008): show Victory, Defeat or Abandoned from RunLifecycleState.
        }

        private void OnDisable()
        {
            // TODO(RUN-004): unsubscribe from RunEndedEvent.
        }

        /// <summary>Return to Hub (RUN-007).</summary>
        public void OnReturnToHub()
        {
            if (ServiceLocator.Current == null) return;
            ServiceLocator.Current.Get<ISceneFlowService>().LoadScene(SceneNames.Hub);
        }
    }
}
