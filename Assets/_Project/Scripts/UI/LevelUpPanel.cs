using System.Collections.Generic;
using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.UI
{
    /// <summary>
    /// The three-card Level Up overlay (SRS 19.3).
    /// </summary>
    /// <remarks>
    /// Two rules are non-negotiable here. The panel opens only with exactly three cards
    /// (EXP-005, EXP-009, SRS 30), and it cannot be dismissed without a pick: ESC does nothing
    /// while it is open (PAU-005, SRS 34). Game time is stopped throughout (EXP-004).
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class LevelUpPanel : MonoBehaviour
    {
        [Header("Cards (SRS 19.3)")]
        [Tooltip("Exactly three card views: icon, name, rarity, description (EXP-005).")]
        [SerializeField] private UpgradeCardView[] _cards;

        [Header("Root")]
        [SerializeField] private GameObject _root;

        /// <summary>True while the player still owes a choice (PAU-005).</summary>
        public bool IsAwaitingChoice { get; private set; }

        /// <summary>
        /// Shows the offer. Refuses to open with fewer than the configured card count, because
        /// SRS 30 forbids a Level Up panel with fewer than three cards.
        /// </summary>
        public void Show(IReadOnlyList<UpgradeData> offer, int requiredCount)
        {
            // TODO(SRS-30): log an error and stay closed when offer.Count < requiredCount.
            // TODO(EXP-005): bind one card view per offered upgrade.
            // TODO(UPG-007): show enough of the effect for the player to decide.
            // TODO(EXP-004): the caller has already paused with PauseReason.LevelUpSelection.
        }

        /// <summary>Applies the pick and closes (EXP-007, EXP-008).</summary>
        public void OnCardSelected(int cardIndex)
        {
            // TODO(EXP-007): publish the choice, close the panel, and resume from
            //   PauseReason.LevelUpSelection.
            // TODO(TEL-002): log the three ids offered and the id chosen.
        }

        private void Update()
        {
            // TODO(PAU-005): explicitly swallow ESC while IsAwaitingChoice, so the panel cannot
            // be closed without a pick.
        }
    }
}
