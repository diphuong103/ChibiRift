using UnityEngine;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// The dash window: a fixed-velocity burst during which the owner's own movement is suspended
    /// and gravity does not apply (MOV-006).
    /// </summary>
    /// <remarks>
    /// <para><b>Why this is not <see cref="KnockbackState"/>.</b> The two look alike — both are a
    /// timer during which a motor stops driving itself — but they suspend different things.
    /// Knockback owns the horizontal axis only: the hero still falls while being shoved. A dash
    /// owns <b>both</b> axes, because a dash that arced downward would be unusable as a defensive
    /// move and would not cover the configured distance. Folding them together needs a
    /// "does this one also cancel gravity" flag, which makes both harder to read than two small
    /// structs that each say one thing.</para>
    ///
    /// <para>Velocity is constant rather than eased. <c>Distance / Duration</c> is what makes the
    /// dash cover exactly the configured distance, which is what <c>Test_Dash_MovesExactDistance</c>
    /// asserts and what lets the player learn its reach.</para>
    /// </remarks>
    public struct DashState
    {
        /// <summary>Seconds left in the dash. Zero when not dashing.</summary>
        public float Remaining;

        /// <summary>Velocity held for the whole dash, in units per second.</summary>
        public Vector2 Velocity;

        /// <summary>True while the motor must not drive itself or apply gravity.</summary>
        public bool IsActive => Remaining > 0f;

        /// <summary>
        /// Starts a dash along <paramref name="direction"/>. A dash already running is replaced,
        /// not extended: the cooldown is what limits how often one may start.
        /// </summary>
        public void Begin(Vector2 direction, float speed, float durationSeconds)
        {
            if (durationSeconds <= 0f || speed <= 0f) return;

            // Horizontal only in P1, matching knockback. A vertical component would let the hero
            // dash up ledges, which is a traversal decision this slice is not making.
            float sign = direction.x >= 0f ? 1f : -1f;

            Remaining = durationSeconds;
            Velocity = new Vector2(sign * speed, 0f);
        }

        /// <summary>Advances the timer. Returns true while the dash still owns the motor.</summary>
        public bool Tick(float deltaSeconds)
        {
            if (Remaining <= 0f) return false;

            Remaining -= deltaSeconds;
            if (Remaining < 0f) Remaining = 0f;
            return true;
        }

        /// <summary>
        /// Ends the dash immediately: hit a wall (MOV-007), died, or respawned.
        /// </summary>
        public void Clear()
        {
            Remaining = 0f;
            Velocity = Vector2.zero;
        }
    }
}
