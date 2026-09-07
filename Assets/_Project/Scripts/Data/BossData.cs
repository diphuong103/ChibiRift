using System;
using UnityEngine;

namespace ChibiRift.Data
{
    /// <summary>
    /// One boss phase (BOS-002, BOS-003). SRS 15 gives Phase 1 above 70% HP and Phase 2 at or
    /// below 70%, with an optional Phase 3 at or below 40%.
    /// </summary>
    [Serializable]
    public struct BossPhase
    {
        [Tooltip("Phase begins when HP falls to or below this fraction of maximum (BOS-003).")]
        [Range(0f, 1f)]
        public float HealthThreshold;

        [Tooltip("Damage multiplier applied to the boss during this phase.")]
        [Min(0f)]
        public float DamageMultiplier;

        [Tooltip("Move speed multiplier during this phase.")]
        [Min(0f)]
        public float SpeedMultiplier;

        [Tooltip("Seconds of telegraph before each pattern in this phase (BOS-004).")]
        [Min(0f)]
        public float TelegraphDuration;
    }

    /// <summary>
    /// The stage boss (SRS 15). MVP requires at least two phases (BOS-002) and a death sequence
    /// that triggers the reward exactly once (BOS-005, SRS 34).
    /// </summary>
    [CreateAssetMenu(fileName = "BOSS_", menuName = "ChibiRift/Boss Data", order = 80)]
    public sealed class BossData : GameDataAsset
    {
        [Header("Stats (BOS-001)")]
        [Tooltip("Includes Defense and DamageReduction, same pipeline as any other target (HPS-009).")]
        [SerializeField] private StatBlock _baseStats = StatBlock.PlayerBaseline;

        [Header("Phases (BOS-002, BOS-003)")]
        [Tooltip("Ordered high to low threshold. BOS-002 requires at least 2 in MVP.")]
        [SerializeField] private BossPhase[] _phases = new BossPhase[0];

        [Header("Rewards (BOS-005, SRS 16)")]
        [Tooltip("Gold granted on death, committed once (RUN-005).")]
        [Min(0)]
        [SerializeField] private int _goldReward = 100;

        [Tooltip("Gems granted on death. SRS 16 makes bosses the main gem source.")]
        [Min(0)]
        [SerializeField] private int _gemReward = 1;

        [Tooltip("XP granted on death (EXP-001).")]
        [Min(0f)]
        [SerializeField] private float _experienceReward = 100f;

        [Header("Presentation")]
        [Tooltip("Boss prefab.")]
        [SerializeField] private GameObject _prefab;

        [Tooltip("Seconds of death sequence before the reward triggers (BOS-005).")]
        [Min(0f)]
        [SerializeField] private float _deathSequenceDuration = 2f;

        /// <summary>Boss stats (BOS-001).</summary>
        public StatBlock BaseStats => _baseStats;

        /// <summary>Ordered phases (BOS-002).</summary>
        public BossPhase[] Phases => _phases;

        /// <summary>Gold on death (BOS-005).</summary>
        public int GoldReward => _goldReward;

        /// <summary>Gems on death (SRS 16).</summary>
        public int GemReward => _gemReward;

        /// <summary>XP on death (EXP-001).</summary>
        public float ExperienceReward => _experienceReward;

        /// <summary>Boss prefab.</summary>
        public GameObject Prefab => _prefab;

        /// <summary>Death sequence length before reward commit (BOS-005).</summary>
        public float DeathSequenceDuration => _deathSequenceDuration;

        protected override void OnValidate()
        {
            base.OnValidate();

#if UNITY_EDITOR
            if (_phases.Length < 2)
            {
                Core.GameLog.Warn(LogCategory,
                    $"'{name}' declares {_phases.Length} phase(s); BOS-002 requires at least 2 in MVP.");
            }
#endif
        }
    }
}
