using System;
using System.Collections;
using UnityEngine;

namespace ChibiRift.Tests.Play
{
    /// <summary>
    /// The waiting primitives every PlayMode fixture uses. One copy, so the hit-stop guard below
    /// cannot be present in some fixtures and missing from others.
    /// </summary>
    /// <remarks>
    /// <para><b>Never write <c>yield return new WaitForFixedUpdate()</c> directly in a test.</b>
    /// <c>FixedUpdate</c> does not run while <c>Time.timeScale</c> is zero, and from P1 slice 4A
    /// every landed hit freezes time for up to 0.14s — including a hit an enemy lands on the hero
    /// in the middle of a test about something else. A bare fixed-step wait during a freeze does
    /// not fail; it simply never returns, and the whole run hangs. That happened once already and
    /// cost a full test cycle to diagnose, because a hang looks identical to "still running"
    /// (OI-28).</para>
    ///
    /// <para><see cref="Steps"/> waits each freeze out on frames — frames advance at a zero time
    /// scale, physics steps do not — and gives up loudly if the freeze never ends.</para>
    /// </remarks>
    public static class TestTime
    {
        /// <summary>
        /// Real seconds a freeze may last before the wait is treated as a leak rather than as
        /// feedback. Far above the longest legitimate hit stop, which is 0.14s on a kill.
        /// </summary>
        private const float FrozenTimeLimitSeconds = 2f;

        private const string FrozenMessage =
            "Time is frozen (timeScale=0) — did a hit stop leak, or is a WaitForFixedUpdate " +
            "waiting on frozen physics?";

        /// <summary>Waits <paramref name="count"/> physics steps, surviving any hit stop.</summary>
        /// <exception cref="TimeoutException">
        /// Thrown when time stays frozen past <see cref="FrozenTimeLimitSeconds"/>. Failing here is
        /// the point: the alternative is a run that never finishes and reports nothing.
        /// </exception>
        public static IEnumerator Steps(int count)
        {
            for (int i = 0; i < count; i++)
            {
                float frozenUntil = Time.realtimeSinceStartup + FrozenTimeLimitSeconds;

                while (Time.timeScale <= 0f)
                {
                    if (Time.realtimeSinceStartup > frozenUntil)
                    {
                        throw new TimeoutException(
                            $"{FrozenMessage} Waited {FrozenTimeLimitSeconds}s of real time at " +
                            $"step {i + 1} of {count} and the scale never came back. " +
                            "Something requested a freeze and never released it, or a pause was " +
                            "left on. See OI-28.");
                    }

                    yield return null;
                }

                yield return new WaitForFixedUpdate();
            }
        }

        /// <summary>Waits roughly <paramref name="seconds"/> of scaled time, in whole physics steps.</summary>
        public static IEnumerator Seconds(float seconds) => Steps(StepsFor(seconds));

        /// <summary>
        /// Waits in real time, which keeps running while time is frozen. Use this, and only this,
        /// when the test itself is the thing holding the freeze.
        /// </summary>
        public static IEnumerator RealSeconds(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        /// <summary>Physics steps covering <paramref name="seconds"/>, rounded up with one to spare.</summary>
        public static int StepsFor(float seconds) => Mathf.CeilToInt(seconds / Time.fixedDeltaTime) + 1;
    }
}
