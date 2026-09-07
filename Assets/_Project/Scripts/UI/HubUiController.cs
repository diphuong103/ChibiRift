using UnityEngine;
using UnityEngine.UI;
using ChibiRift.Core;

namespace ChibiRift.UI
{
    /// <summary>
    /// Hub screen (SRS 17): wallet display (HUB-001), hero selection (HUB-003), the permanent
    /// upgrade shop (META-001 to META-005) and Start Run (HUB-002).
    /// Talks to meta progression over the EventBus, never by referencing ChibiRift.Meta.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HubUiController : MonoBehaviour
    {
        [Header("Wallet (HUB-001)")]
        [SerializeField] private Text _goldLabel;
        [SerializeField] private Text _gemLabel;

        [Header("Actions")]
        [Tooltip("Start Run (HUB-002).")]
        [SerializeField] private Button _startRunButton;

        [Tooltip("Opens hero selection (HUB-003).")]
        [SerializeField] private Button _heroSelectButton;

        [Header("Meta upgrades (META-001..META-005)")]
        [Tooltip("Rows for the permanent HP, Attack and Crit upgrades.")]
        [SerializeField] private Transform _upgradeListRoot;

        private void Awake()
        {
            if (_startRunButton != null) _startRunButton.onClick.AddListener(OnStartRun);
        }

        private void OnEnable()
        {
            // TODO(HUB-001): subscribe to CurrencyChangedEvent and show the balances.
            // TODO(META-005): disable a purchase button when the balance is short.
        }

        private void OnDisable()
        {
            // TODO(HUB-001): unsubscribe from CurrencyChangedEvent.
        }

        /// <summary>Start Run (HUB-002).</summary>
        public void OnStartRun()
        {
            if (ServiceLocator.Current == null) return;
            ServiceLocator.Current.Get<ISceneFlowService>().LoadScene(SceneNames.Run01);
        }
    }
}
