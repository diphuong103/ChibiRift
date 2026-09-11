using System;
using UnityEngine;

namespace ChibiRift.Data
{
    /// <summary>
    /// How long the game freezes on a hit, per weight of hit (SRS 21 "Hit Stop").
    /// </summary>
    /// <remarks>
    /// Hit stop is the cheapest way to make three otherwise identical combo steps feel different,
    /// which is the whole point of this slice. The four figures must stay clearly apart or the
    /// distinction is not perceivable: crit at 0.10 is 2.5x the 0.04 of a light hit.
    ///
    /// <para>Measured in <b>unscaled</b> seconds, because the freeze works by setting
    /// <c>Time.timeScale</c> to zero and scaled time does not advance while it is held.</para>
    /// </remarks>
    [Serializable]
    public struct HitStopConfig
    {
        [Tooltip("Freeze on combo steps 1 and 2. Short enough to read as weight, not as a stutter.")]
        [Min(0f)]
        public float Light;

        [Tooltip("Freeze on the final combo step, which is also the one with the 1.6x multiplier.")]
        [Min(0f)]
        public float Heavy;

        [Tooltip("Freeze on a critical hit (COM-006). The longest of the non-lethal cases.")]
        [Min(0f)]
        public float Critical;

        [Tooltip("Freeze on the hit that kills. Longest of all, so a kill reads as an event.")]
        [Min(0f)]
        public float Kill;

        /// <summary>Baseline hit stop confirmed by the project owner for P1 slice 4A.</summary>
        public static HitStopConfig Baseline => new HitStopConfig
        {
            Light = 0.04f,
            Heavy = 0.08f,
            Critical = 0.1f,
            Kill = 0.14f
        };
    }

    /// <summary>One screen shake: how far and for how long (CAM-003).</summary>
    [Serializable]
    public struct ShakeStep
    {
        [Tooltip("Impulse amplitude. Larger throws the camera further.")]
        [Min(0f)]
        public float Amplitude;

        [Tooltip("Seconds the impulse takes to decay.")]
        [Min(0f)]
        public float Duration;
    }

    /// <summary>
    /// Screen shake per kind of hit (CAM-003).
    /// </summary>
    /// <remarks>
    /// Shake is the second half of the same job hit stop does: it separates a light hit from a
    /// heavy one without a number on screen. <see cref="HeroHurt"/> is deliberately strong — being
    /// hit should feel worse than landing a hit, and it is the only shake the player does not cause.
    /// </remarks>
    [Serializable]
    public struct ShakeConfig
    {
        [Tooltip("Hero lands a hit with combo step 1 or 2.")]
        public ShakeStep Light;

        [Tooltip("Hero lands the final combo step.")]
        public ShakeStep Heavy;

        [Tooltip("Hero lands a critical hit (COM-006).")]
        public ShakeStep Critical;

        [Tooltip("Hero takes a hit. Strong: the only shake the player did not ask for.")]
        public ShakeStep HeroHurt;

        /// <summary>Baseline shake confirmed by the project owner for P1 slice 4A.</summary>
        public static ShakeConfig Baseline => new ShakeConfig
        {
            Light = new ShakeStep { Amplitude = 0.12f, Duration = 0.1f },
            Heavy = new ShakeStep { Amplitude = 0.25f, Duration = 0.16f },
            Critical = new ShakeStep { Amplitude = 0.35f, Duration = 0.2f },
            HeroHurt = new ShakeStep { Amplitude = 0.3f, Duration = 0.18f }
        };
    }

    /// <summary>
    /// The rest of the hit feedback: the white flash, the impact burst and the dash trail
    /// (SRS 21, MOV-006).
    /// </summary>
    [Serializable]
    public struct ImpactConfig
    {
        [Tooltip("Seconds the struck sprite is tinted white. Long enough to see, short enough not to hide the sprite.")]
        [Min(0f)]
        public float FlashDuration;

        [Tooltip("Colour of that tint.")]
        public Color FlashColor;

        [Tooltip("Particles released at the point of impact. Not in the brief's list; a count was needed and this is it.")]
        [Min(0)]
        public int ParticleCount;

        [Tooltip("Seconds between dash afterimages. Smaller leaves a denser trail.")]
        [Min(0.001f)]
        public float DashGhostInterval;

        [Tooltip("Seconds one afterimage takes to fade out.")]
        [Min(0f)]
        public float DashGhostLifetime;

        [Tooltip("Starting alpha of an afterimage. Below 1 so the trail never reads as a second hero.")]
        [Range(0f, 1f)]
        public float DashGhostAlpha;

        /// <summary>Baseline impact feedback confirmed by the project owner for P1 slice 4A.</summary>
        public static ImpactConfig Baseline => new ImpactConfig
        {
            FlashDuration = 0.08f,
            FlashColor = Color.white,
            ParticleCount = 6,
            DashGhostInterval = 0.04f,
            DashGhostLifetime = 0.15f,
            DashGhostAlpha = 0.4f
        };
    }
}
