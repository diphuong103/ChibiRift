using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Cinemachine rig following the hero (SRS 20). Confiner keeps the view inside the level
    /// (CAM-002) and an impulse source provides the shake of CAM-003, driven by
    /// <see cref="ScreenShakeRequestedEvent"/> so combat code never touches the camera directly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraRig : MonoBehaviour
    {
        [Header("Targets")]
        [Tooltip("Transform the virtual camera follows (CAM-001).")]
        [SerializeField] private Transform _followTarget;

        [Header("Shake (CAM-003)")]
        [Tooltip("Amplitude and duration defaults come from here, never from a literal (SRS 35).")]
        [SerializeField] private BalanceConfig _balanceConfig;

        private void OnEnable()
        {
            // TODO(CAM-003): subscribe to ScreenShakeRequestedEvent on the EventBus.
        }

        private void OnDisable()
        {
            // TODO(CAM-003): unsubscribe, so a scene change leaves no dangling handler.
        }

        /// <summary>Points the rig at the hero once the Run scene has spawned them (CAM-001).</summary>
        public void SetFollowTarget(Transform target)
        {
            // TODO(CAM-001): assign the CinemachineCamera Follow target.
            // TODO(CAM-002): assign the CinemachineConfiner2D bounding shape from the level boundary.
            _followTarget = target;
        }

        /// <summary>Zoom for a large event such as the boss entrance (CAM-004).</summary>
        public void SetZoom(float orthographicSize, float durationSeconds)
        {
            // TODO(CAM-004): tween the CinemachineCamera lens size.
        }
    }
}
