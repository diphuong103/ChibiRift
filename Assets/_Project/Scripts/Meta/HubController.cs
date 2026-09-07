using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Meta
{
    /// <summary>
    /// Hub scene logic (SRS 17): shows the wallet, lets the player pick a hero, and starts a Run.
    /// Publishes to the EventBus rather than touching Hub UI directly, keeping the gameplay to UI
    /// direction one-way (SRS 26).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HubController : MonoBehaviour
    {
        [Header("Heroes (HUB-003)")]
        [Tooltip("Heroes selectable in the Hub. MVP ships one but the list supports more (HER-005).")]
        [SerializeField] private HeroData[] _availableHeroes;

        /// <summary>Hero currently selected (HUB-003).</summary>
        public HeroData SelectedHero { get; private set; }

        private void Start()
        {
            // TODO(HUB-001): publish the current Gold and Gem balances so the Hub UI can show them.
            // TODO(HUB-003): select the first unlocked hero by default.
        }

        /// <summary>Selects a hero for the next Run (HUB-003).</summary>
        public void SelectHero(HeroData hero)
        {
            // TODO(HER-005): refuse a hero that meta progression has not unlocked.
            SelectedHero = hero;
        }

        /// <summary>Start Run (HUB-002). Begins the Run lifecycle at RUN-001.</summary>
        public void StartRun()
        {
            // TODO(HUB-002): ask RunManager to start with SelectedHero and a fresh seed (RNG-003).
        }
    }
}
