using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Hero locomotion (SRS 8.1). Reads only <see cref="IInputService"/>, never a device, so the
    /// legacy Input Manager stays unused. All tuning comes from <see cref="HeroData"/> (SRS 35).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("Supplies jump force, jump count and the dash config (HER-006).")]
        [SerializeField] private HeroData _heroData;

        [Header("Ground check (MOV-004)")]
        [Tooltip("Origin of the grounded probe.")]
        [SerializeField] private Transform _groundCheck;

        [Tooltip("Radius of the grounded probe.")]
        [Min(0.01f)]
        [SerializeField] private float _groundCheckRadius = 0.15f;

        /// <summary>True while standing on Ground. Resets the jump counter (MOV-003).</summary>
        public bool IsGrounded { get; private set; }

        /// <summary>True for the duration of a dash, during which i-frames apply (MOV-006, HPS-005).</summary>
        public bool IsDashing { get; private set; }

        /// <summary>Seconds until the dash is available again. Drives the HUD indicator (MOV-006).</summary>
        public float DashCooldownRemaining { get; private set; }

        /// <summary>Facing sign, +1 right and -1 left, set from the mouse aim vector (COM-009).</summary>
        public int FacingDirection { get; private set; } = 1;

        /// <summary>Radius of the grounded probe used for the MOV-004 ground check.</summary>
        public float GroundCheckRadius => _groundCheckRadius;

        private void FixedUpdate()
        {
            // TODO(MOV-001): drive horizontal velocity from IInputService.MoveAxis * HeroData move speed.
            // TODO(MOV-004): rely on Rigidbody2D and the collision matrix; never teleport past a collider.
            // TODO(MOV-005): read input in Update and consume it here so a frame spike cannot drop it.
        }

        private void Update()
        {
            // TODO(MOV-002): jump on IInputService.JumpPressed when the jump counter allows.
            // TODO(MOV-003): allow HeroData.MaxJumpCount jumps before touching ground again.
            // TODO(MOV-006): dash on IInputService.DashPressed when DashCooldownRemaining is 0.
            // TODO(COM-009): set FacingDirection from IInputService.AimWorldPosition, no auto-target.
        }

        /// <summary>
        /// Dash with i-frames and cooldown (MOV-006). Distance, duration, i-frame length and
        /// cooldown all come from <see cref="HeroData.Dash"/>; none may appear as a literal here.
        /// </summary>
        private void StartDash()
        {
            // TODO(MOV-006): sweep the dash distance over DashConfig.Duration, open the i-frame
            // window on PlayerStats for DashConfig.IFrameDuration, then start DashConfig.Cooldown.
            // TODO(MOV-007): sweep with Rigidbody2D.Cast against GameLayers.SolidWorldMask and stop
            // at the first hit, so a dash can never cross a collider or leave the boundary.
            // TODO(MOV-006): publish DashCooldownChangedEvent so the HUD shows the cooldown.
        }
    }
}
