using System;
using UnityEngine;

namespace ChibiRift.Data
{
    /// <summary>
    /// Timing of one enemy attack (AI-004, COM-004).
    /// </summary>
    /// <remarks>
    /// Three phases in seconds, mirroring <see cref="AttackStep"/> on the hero side: the hitbox
    /// exists only during <see cref="Active"/>. Time-based rather than Animation Events for the
    /// same reason as the hero's chain — P1 has no clips to hang an event on (OI-18).
    ///
    /// <para>This replaces the single <c>AttackWindup</c> field that used to sit on
    /// <see cref="EnemyData"/>, whose name said "windup" while its tooltip described the active
    /// window. One number could not express both, and the ambiguity would have surfaced as an
    /// attack that either never connected or never ended.</para>
    /// </remarks>
    [Serializable]
    public struct EnemyAttackConfig
    {
        [Tooltip("Seconds of telegraph before the hitbox opens. The player's window to read the attack and get out (AI-004).")]
        [Min(0f)]
        public float Windup;

        [Tooltip("Seconds the hitbox is open (COM-004).")]
        [Min(0.01f)]
        public float Active;

        [Tooltip("Seconds of recovery after the hitbox closes, during which the enemy cannot act.")]
        [Min(0f)]
        public float Recovery;

        [Tooltip("Seconds before the next attack may start, counted from the end of recovery (AI-004).")]
        [Min(0f)]
        public float Cooldown;

        [Tooltip("Width of the swept hitbox (COM-004).")]
        [Min(0.01f)]
        public float HitboxWidth;

        [Tooltip("Height of the swept hitbox (COM-004).")]
        [Min(0.01f)]
        public float HitboxHeight;

        [Tooltip("Distance from the enemy to the hitbox centre, along its facing.")]
        [Min(0f)]
        public float HitboxOffsetDistance;

        /// <summary>Seconds from swing start until the hitbox opens.</summary>
        public float ActiveStartTime => Windup;

        /// <summary>Seconds from swing start until the hitbox closes.</summary>
        public float ActiveEndTime => Windup + Active;

        /// <summary>Seconds the whole swing takes, telegraph through recovery.</summary>
        public float TotalDuration => Windup + Active + Recovery;

        /// <summary>Hitbox size as one vector, the shape <c>Physics2D.OverlapBox</c> wants.</summary>
        public Vector2 HitboxSize => new Vector2(HitboxWidth, HitboxHeight);

        /// <summary>Baseline melee attack confirmed by the project owner for P1 slice 3.</summary>
        public static EnemyAttackConfig MeleeBaseline => new EnemyAttackConfig
        {
            Windup = 0.35f,
            Active = 0.1f,
            Recovery = 0.45f,
            Cooldown = 1.2f,

            // Same reach as the hero's chain, so trading blows at the edge of range is symmetrical.
            HitboxWidth = 1.2f,
            HitboxHeight = 1f,
            HitboxOffsetDistance = 0.8f
        };
    }
}
