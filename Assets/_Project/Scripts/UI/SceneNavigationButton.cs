using UnityEngine;
using UnityEngine.UI;
using ChibiRift.Core;

namespace ChibiRift.UI
{
    /// <summary>
    /// Sends the player to a named scene when its button is clicked, routing through
    /// <see cref="ISceneFlowService"/> so every transition still goes through the single
    /// async owner (SRS 27). Used for the plain links between the five scenes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneNavigationButton : MonoBehaviour
    {
        [Tooltip("Button that triggers the transition.")]
        [SerializeField] private Button _button;

        [Tooltip("Target scene name. Use the constants in SceneNames, never a typed literal.")]
        [SerializeField] private string _targetScene = string.Empty;

        private void Awake()
        {
            if (_button != null) _button.onClick.AddListener(Navigate);
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(Navigate);
        }

        /// <summary>Loads the configured scene (SRS 27, NFR-004).</summary>
        public void Navigate()
        {
            if (ServiceLocator.Current == null)
            {
                GameLog.Error("UI", "No ServiceLocator; the game must be entered from the Boot scene.");
                return;
            }

            ServiceLocator.Current.Get<ISceneFlowService>().LoadScene(_targetScene);
        }
    }
}
