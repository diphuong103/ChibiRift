using UnityEngine;

namespace ChibiRift.UI
{
    /// <summary>
    /// Onboarding panel (SRS 36, NFR-005). Explains the control map of SRS 8.3 and the core
    /// loop, without needing a separate tutorial map.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HowToPlayPanel : MonoBehaviour
    {
        [Header("Content (SRS 36)")]
        [Tooltip("Pages covering: A/D move, Space jump and double jump, Left Shift dash with i-frames, Mouse Left attack and combo, Q/E/R skills, XP and Level Up, the 3-card pick, and Run to Reward to Hub.")]
        [SerializeField] private GameObject[] _pages;

        /// <summary>Page currently shown.</summary>
        public int PageIndex { get; private set; }

        /// <summary>Advances one page (SRS 36).</summary>
        public void NextPage()
        {
            // TODO(SRS-36): show the next page, wrapping or stopping at the last.
        }
    }
}
