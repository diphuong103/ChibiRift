using UnityEngine;

namespace ChibiRift.Data
{
    /// <summary>
    /// Cinemachine tuning for the gameplay camera (CAM-001, CAM-002).
    /// Kept out of <see cref="HeroData"/> because framing is a property of the stage and the
    /// screen, not of whichever hero is being played.
    /// </summary>
    [CreateAssetMenu(fileName = "CameraConfig", menuName = "ChibiRift/Camera Config", order = 5)]
    public sealed class CameraConfig : GameDataAsset
    {
        [Header("Follow damping (CAM-001)")]
        [Tooltip("Seconds the camera takes to catch up horizontally. Higher is smoother but laggier.")]
        [Min(0f)]
        [SerializeField] private float _dampingX = 0.3f;

        [Tooltip("Seconds the camera takes to catch up vertically. Higher than X so jumps do not jolt the view.")]
        [Min(0f)]
        [SerializeField] private float _dampingY = 0.5f;

        [Tooltip("How far ahead of the hero's motion the camera leads, in seconds of travel.")]
        [Min(0f)]
        [SerializeField] private float _lookahead = 0.2f;

        /// <summary>Horizontal damping in seconds (CAM-001).</summary>
        public float DampingX => _dampingX;

        /// <summary>Vertical damping in seconds (CAM-001).</summary>
        public float DampingY => _dampingY;

        /// <summary>Lookahead in seconds of travel (CAM-001).</summary>
        public float Lookahead => _lookahead;
    }
}
