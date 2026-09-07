using System;
using UnityEngine;

namespace ChibiRift.Data
{
    /// <summary>
    /// Shared stat block for hero and enemy (HER-001, HPS-001, HPS-002).
    /// Defense and DamageReduction are mandatory on both sides (HPS-009) and feed
    /// steps 3 and 4 of the damage formula in SRS section 9.
    /// </summary>
    [Serializable]
    public struct StatBlock
    {
        [Tooltip("Maximum hit points. SRS 35 baseline for the player hero: 100.")]
        [Min(1f)]
        public float MaxHealth;

        [Tooltip("Base attack damage. SRS 35 baseline for the player hero: 10.")]
        [Min(0f)]
        public float Attack;

        [Tooltip("Attacks per second multiplier applied to the combo timing.")]
        [Min(0.01f)]
        public float AttackSpeed;

        [Tooltip("Horizontal movement speed in units per second (MOV-001).")]
        [Min(0f)]
        public float MoveSpeed;

        [Tooltip("Probability of a critical hit, 0..1. SRS 35 baseline: 0.05 (5%).")]
        [Range(0f, 1f)]
        public float CritChance;

        [Tooltip("Critical damage multiplier. SRS 35 gives a 1.5x-2.0x range; 1.5 taken as baseline.")]
        [Min(1f)]
        public float CritMultiplier;

        [Tooltip("Flat armor subtracted before proportional reduction (HPS-009). SRS 35 player baseline: 0.")]
        [Min(0f)]
        public float Defense;

        [Tooltip("Proportional reduction, 0..0.8 (HPS-009). SRS 35 player baseline: 0. Hard cap lives in BalanceConfig.")]
        [Range(0f, 0.8f)]
        public float DamageReduction;

        /// <summary>Baseline block matching the player values of SRS 35, used for new assets.</summary>
        public static StatBlock PlayerBaseline => new StatBlock
        {
            MaxHealth = 100f,
            Attack = 10f,
            AttackSpeed = 1f,
            MoveSpeed = 6f,
            CritChance = 0.05f,
            CritMultiplier = 1.5f,
            Defense = 0f,
            DamageReduction = 0f
        };
    }
}
