using System;
using UnityEngine;

namespace ChibiRift.Data
{
    /// <summary>
    /// Locomotion physics for one enemy archetype, mirroring <see cref="MovementConfig"/> on the
    /// hero side (AI-001, SRS 35).
    /// </summary>
    /// <remarks>
    /// Gravity is simulated in code rather than by <c>Rigidbody2D</c>, exactly as the hero does, so
    /// the two never fall under different rules. That makes gravity a tuning value rather than a
    /// physics setting, which is why it lives here and not as a constant in <c>EnemyMotor</c>.
    /// </remarks>
    [Serializable]
    public struct EnemyPhysicsConfig
    {
        [Tooltip("Downward acceleration. Enemies do not jump, so one constant covers the whole fall.")]
        [Min(0.01f)]
        public float Gravity;

        [Tooltip("Terminal downward speed, so a long fall stays controllable and cannot tunnel through a collider.")]
        [Min(0f)]
        public float MaxFallSpeed;

        [Tooltip("Width of the OverlapBox ground probe. Narrower than the body so a wall does not read as ground.")]
        [Min(0.01f)]
        public float GroundCheckWidth;

        [Tooltip("Height of the ground probe. Thin, to avoid catching ground the enemy has not reached.")]
        [Min(0.01f)]
        public float GroundCheckHeight;

        [Tooltip("Vertical offset of the probe from the enemy origin. Negative places it at the feet.")]
        public float GroundCheckOffsetY;

        /// <summary>Probe size as one vector, the shape <c>Physics2D.OverlapBox</c> wants.</summary>
        public Vector2 GroundCheckSize => new Vector2(GroundCheckWidth, GroundCheckHeight);

        /// <summary>Baseline melee physics. Matches the hero's figures so both fall identically.</summary>
        public static EnemyPhysicsConfig MeleeBaseline => new EnemyPhysicsConfig
        {
            Gravity = 40f,
            MaxFallSpeed = 25f,
            GroundCheckWidth = 0.7f,
            GroundCheckHeight = 0.1f,
            GroundCheckOffsetY = -0.9f
        };
    }
}
