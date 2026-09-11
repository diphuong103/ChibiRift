using UnityEngine;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Remembers a button press for a short window so it still counts when the game was not ready
    /// for it (P1-07).
    /// </summary>
    /// <remarks>
    /// <para>Three actions need this and each keeps its own instance with its own duration, so
    /// retuning attack rhythm cannot silently change how jumping feels.</para>
    ///
    /// <para><b>Hit stop is why the window widened beyond jump.</b> Impact freezes
    /// <c>Time.timeScale</c> for up to 0.14s. A press during that freeze reaches input polling but
    /// would find the systems mid-freeze and be dropped — which the player experiences as the game
    /// ignoring a button they definitely pressed. Buffering runs on scaled time deliberately: while
    /// time is frozen the window does not tick down, so a press held through a freeze survives it
    /// intact.</para>
    /// </remarks>
    public struct InputBuffer
    {
        /// <summary>Seconds left before the remembered press expires. Zero means nothing pending.</summary>
        public float Remaining;

        /// <summary>True while a press is remembered and still unconsumed.</summary>
        public bool HasPending => Remaining > 0f;

        /// <summary>Remembers a press for <paramref name="windowSeconds"/>.</summary>
        public void Press(float windowSeconds)
        {
            if (windowSeconds <= 0f) return;
            Remaining = windowSeconds;
        }

        /// <summary>Counts the window down. Call once per fixed step.</summary>
        public void Tick(float deltaSeconds)
        {
            if (Remaining <= 0f) return;

            Remaining -= deltaSeconds;
            if (Remaining < 0f) Remaining = 0f;
        }

        /// <summary>
        /// Takes the pending press if there is one, clearing it. Returns false when nothing is
        /// buffered. Consuming rather than peeking is what stops one press firing twice.
        /// </summary>
        public bool TryConsume()
        {
            if (Remaining <= 0f) return false;

            Remaining = 0f;
            return true;
        }

        /// <summary>Forgets any pending press, e.g. on death or respawn.</summary>
        public void Clear() => Remaining = 0f;
    }
}
