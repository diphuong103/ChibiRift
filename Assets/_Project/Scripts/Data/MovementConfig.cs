using System;
using UnityEngine;

namespace ChibiRift.Data
{
    /// <summary>
    /// Locomotion tuning for one hero (MOV-001 through MOV-004).
    /// Every number the motor needs lives here, so <c>ChibiRift.Gameplay</c> contains no movement
    /// literal at all (SRS 35). Gravity is simulated in code rather than by Rigidbody2D, which is
    /// why <see cref="GravityUp"/> and the two multipliers below are ours and not physics settings.
    /// </summary>
    [Serializable]
    public struct MovementConfig
    {
        [Header("Ground acceleration (MOV-001)")]
        [Tooltip("Units/s^2 gained while pushing a direction on the ground.")]
        [Min(0f)]
        public float GroundAccel;

        [Tooltip("Units/s^2 shed while releasing input on the ground. Higher than accel so stopping feels crisp.")]
        [Min(0f)]
        public float GroundDecel;

        [Header("Air acceleration (MOV-001, MOV-003)")]
        [Tooltip("Units/s^2 gained while airborne. Lower than ground so jumps commit.")]
        [Min(0f)]
        public float AirAccel;

        [Tooltip("Units/s^2 shed while airborne with no input.")]
        [Min(0f)]
        public float AirDecel;

        [Header("Gravity (MOV-002)")]
        [Tooltip("Downward acceleration while rising with jump held. Peak height = JumpVelocity^2 / (2 * GravityUp).")]
        [Min(0.01f)]
        public float GravityUp;

        [Tooltip("Gravity multiplier once falling, so the descent is snappier than the rise.")]
        [Min(1f)]
        public float FallMultiplier;

        [Tooltip("Gravity multiplier while still rising but jump was released. Must exceed 1 or releasing early would raise the jump instead of cutting it (OI-16).")]
        [Min(1f)]
        public float LowJumpMultiplier;

        [Tooltip("Terminal downward speed, so a long fall stays controllable and cannot tunnel through colliders.")]
        [Min(0f)]
        public float MaxFallSpeed;

        [Header("Jump (MOV-002, MOV-003)")]
        [Tooltip("Upward velocity of the first jump.")]
        [Min(0f)]
        public float JumpVelocity;

        [Tooltip("Upward velocity of the second jump (MOV-003). Deliberately separate from JumpVelocity.")]
        [Min(0f)]
        public float DoubleJumpVelocity;

        [Tooltip("Seconds after leaving a ledge during which a ground jump is still accepted.")]
        [Min(0f)]
        public float CoyoteTime;

        [Tooltip("Seconds a jump press is remembered, so pressing just before landing still jumps.")]
        [Min(0f)]
        public float JumpBuffer;

        [Header("Ground probe (MOV-004)")]
        [Tooltip("Width of the OverlapBox probe. Slightly narrower than the collider so a wall does not read as ground.")]
        [Min(0.01f)]
        public float GroundCheckWidth;

        [Tooltip("Height of the OverlapBox probe. Thin, to avoid catching ground the hero has not reached.")]
        [Min(0.01f)]
        public float GroundCheckHeight;

        [Tooltip("Vertical offset of the probe from the hero origin. Negative places it at the feet.")]
        public float GroundCheckOffsetY;

        /// <summary>Baseline locomotion, matching the P1 slice 1 technical constants.</summary>
        public static MovementConfig Baseline => new MovementConfig
        {
            GroundAccel = 60f,
            GroundDecel = 80f,
            AirAccel = 35f,
            AirDecel = 20f,
            GravityUp = 40f,
            FallMultiplier = 1.6f,
            LowJumpMultiplier = 2f,
            MaxFallSpeed = 25f,
            JumpVelocity = 15.5f,
            DoubleJumpVelocity = 13f,
            CoyoteTime = 0.1f,
            JumpBuffer = 0.12f,
            GroundCheckWidth = 0.7f,
            GroundCheckHeight = 0.1f,
            GroundCheckOffsetY = -0.9f
        };
    }
}
