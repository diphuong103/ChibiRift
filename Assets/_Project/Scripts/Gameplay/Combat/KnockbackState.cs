using UnityEngine;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// The knockback window shared by the hero and enemy motors (COM-005).
    /// </summary>
    /// <remarks>
    /// <para><b>The problem this solves.</b> Both motors write <c>Rigidbody2D.linearVelocity</c>
    /// outright every <c>FixedUpdate</c> rather than adding forces. A knockback that simply set the
    /// velocity would therefore be erased on the very next physics step and never be seen. The fix
    /// is a timer the motor asks about: while it runs, the motor skips its own horizontal drive and
    /// lets the pushed velocity decay on its own.</para>
    ///
    /// <para>A struct, not a base class. The two motors have almost nothing else in common — the
    /// hero has jump buffering, coyote time, double jump and respawn, none of which an enemy wants —
    /// so sharing through inheritance would drag all of that across. This shares exactly the one
    /// mechanism that is genuinely identical.</para>
    /// </remarks>
    public struct KnockbackState
    {
        /// <summary>Seconds left before the owner regains control. Zero means it has control.</summary>
        public float Remaining;

        /// <summary>True while the owner's own horizontal drive must be suppressed.</summary>
        public bool IsActive => Remaining > 0f;

        /// <summary>
        /// Starts a push and returns the horizontal velocity the motor should adopt.
        /// A new knockback replaces a running one instead of adding to it, so being hit twice in
        /// quick succession cannot accumulate into a launch.
        /// </summary>
        public float Begin(Vector2 direction, float force, float durationSeconds)
        {
            if (durationSeconds <= 0f || force <= 0f) return 0f;

            Remaining = durationSeconds;

            // Horizontal only in P1: a vertical component would fight the hero's own gravity
            // integration and could carry an enemy off a ledge it was never meant to leave.
            float sign = direction.x >= 0f ? 1f : -1f;
            return sign * force;
        }

        /// <summary>Advances the timer. Returns true while the owner is still not in control.</summary>
        public bool Tick(float deltaSeconds)
        {
            if (Remaining <= 0f) return false;

            Remaining -= deltaSeconds;
            if (Remaining < 0f) Remaining = 0f;
            return true;
        }

        /// <summary>Ends the window immediately, e.g. on respawn or when returning to a pool.</summary>
        public void Clear() => Remaining = 0f;
    }
}
