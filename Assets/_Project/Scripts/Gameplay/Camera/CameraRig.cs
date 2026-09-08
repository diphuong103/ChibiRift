using Unity.Cinemachine;
using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Drives the gameplay camera (CAM-001, CAM-002) through Cinemachine 3.
    /// </summary>
    /// <remarks>
    /// Follow damping and lookahead live on <see cref="CinemachinePositionComposer"/> and are read
    /// from <see cref="CameraConfig"/>, so no framing number is written here (SRS 35).
    /// <see cref="CinemachineConfiner2D"/> keeps the view inside the arena polygon, which is why
    /// the confiner shape sits on its own object and not on the hero or the ground.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CameraRig : MonoBehaviour
    {
        [Header("Cinemachine")]
        [Tooltip("The virtual camera that follows the hero (CAM-001).")]
        [SerializeField] private CinemachineCamera _camera;

        [Tooltip("Composer providing damping and lookahead. Values come from CameraConfig.")]
        [SerializeField] private CinemachinePositionComposer _composer;

        [Tooltip("Keeps the view inside the arena polygon (CAM-002).")]
        [SerializeField] private CinemachineConfiner2D _confiner;

        [Header("Data")]
        [Tooltip("Damping X/Y and lookahead. Never hard-coded in this script (SRS 35).")]
        [SerializeField] private CameraConfig _cameraConfig;

        private void Awake() => ApplyConfig();

        /// <summary>Pushes <see cref="CameraConfig"/> onto the composer (CAM-001).</summary>
        public void ApplyConfig()
        {
            if (_composer == null || _cameraConfig == null) return;

            _composer.Damping = new Vector3(_cameraConfig.DampingX, _cameraConfig.DampingY, 0f);

            LookaheadSettings lookahead = _composer.Lookahead;
            lookahead.Enabled = _cameraConfig.Lookahead > 0f;
            lookahead.Time = _cameraConfig.Lookahead;
            _composer.Lookahead = lookahead;
        }

        /// <summary>Points the rig at the hero once the Run scene has spawned them (CAM-001).</summary>
        public void SetFollowTarget(Transform target)
        {
            if (_camera == null) return;

            // Cinemachine 3 dropped the legacy Follow property; the target lives on CameraTarget.
            _camera.Target.TrackingTarget = target;
        }

        /// <summary>
        /// Cuts straight to the target instead of panning. Used after a respawn, where a smooth
        /// pan across the whole arena would read as a camera glitch (MOV-004).
        /// </summary>
        public void SnapToTarget()
        {
            if (_camera != null) _camera.PreviousStateIsValid = false;
        }

        /// <summary>Rebuilds the confiner cache after the bounding shape changes (CAM-002).</summary>
        public void InvalidateConfinerCache()
        {
            if (_confiner != null) _confiner.InvalidateBoundingShapeCache();
        }

        /// <summary>Zoom for a large event such as the boss entrance (CAM-004).</summary>
        public void SetZoom(float orthographicSize, float durationSeconds)
        {
            // TODO(CAM-004): tween _camera.Lens.OrthographicSize over durationSeconds.
            GameLog.Info("Camera", $"SetZoom not implemented yet: {orthographicSize} over {durationSeconds}s.");
        }
    }
}
