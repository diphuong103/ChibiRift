using UnityEngine;
using ChibiRift.Core;

namespace ChibiRift.UI
{
    /// <summary>
    /// Panel stack for one scene (SRS 26). Owns which overlay is on top so ESC has a single,
    /// predictable meaning: close the top panel, except while the Level Up panel is awaiting a
    /// choice, which cannot be dismissed (PAU-005).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiManager : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject[] _panels;

        /// <summary>True when any modal overlay is showing.</summary>
        public bool HasOpenPanel { get; private set; }

        /// <summary>Opens a panel and puts it on top of the stack.</summary>
        public void OpenPanel(GameObject panel)
        {
            // TODO(SRS-19.5): push onto the stack and mark it modal.
        }

        /// <summary>Closes the top panel (SRS 19.5).</summary>
        public void CloseTopPanel()
        {
            // TODO(PAU-005): refuse while the Level Up panel is awaiting a choice.
        }
    }
}
