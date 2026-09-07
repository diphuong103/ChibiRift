using System;
using UnityEngine;

namespace ChibiRift.Data
{
    /// <summary>
    /// Dash tuning required on every hero by HER-006 and MOV-006.
    /// Cooldown and i-frame duration have SRS 35 baselines; distance and duration do not and are
    /// were fixed by the project owner and recorded in OPEN_ISSUES.md (OI-03).
    /// </summary>
    [Serializable]
    public struct DashConfig
    {
        [Tooltip("World units travelled by one dash. Not in SRS 35; set by the project owner (OI-03).")]
        [Min(0f)]
        public float Distance;

        [Tooltip("Seconds the dash movement takes. Not in SRS 35; set by the project owner (OI-03).")]
        [Min(0.01f)]
        public float Duration;

        [Tooltip("Seconds of invulnerability during the dash (HPS-005). SRS 35 baseline: 0.25.")]
        [Min(0f)]
        public float IFrameDuration;

        [Tooltip("Seconds before the dash is available again (MOV-006). SRS 35 baseline: 1.5.")]
        [Min(0f)]
        public float Cooldown;

        /// <summary>Baseline dash: SRS 35 values where given, owner-confirmed values elsewhere (OI-03).</summary>
        public static DashConfig Baseline => new DashConfig
        {
            Distance = 5f,
            Duration = 0.25f,
            IFrameDuration = 0.25f,
            Cooldown = 1.5f
        };
    }
}
