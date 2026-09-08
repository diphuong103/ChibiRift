using UnityEngine;

namespace ChibiRift.Data
{
    /// <summary>
    /// One enemy archetype (SRS 13, SRS 23). Defense and DamageReduction are mandatory here
    /// exactly as on the hero (HPS-009); DamageReduction is capped at 0.8 by BalanceConfig.
    /// </summary>
    [CreateAssetMenu(fileName = "ENM_", menuName = "ChibiRift/Enemy Data", order = 40)]
    public sealed class EnemyData : GameDataAsset
    {
        [Header("Stats (HPS-002, HPS-009)")]
        [Tooltip("Includes Defense (flat armor) and DamageReduction, both mandatory (HPS-009).")]
        [SerializeField] private StatBlock _baseStats = StatBlock.PlayerBaseline;

        [Header("Behaviour (AI-001, AI-002, AI-004)")]
        [Tooltip("Archetype driving the state machine (SRS 13).")]
        [SerializeField] private EnemyArchetype _archetype = EnemyArchetype.Melee;

        [Tooltip("Distance at which the enemy notices the hero (AI-002).")]
        [Min(0f)]
        [SerializeField] private float _detectionRange = 10f;

        [Tooltip("Distance at which the enemy commits to an attack.")]
        [Min(0f)]
        [SerializeField] private float _attackRange = 1.5f;

        [Tooltip("Seconds between attacks (AI-004).")]
        [Min(0f)]
        [SerializeField] private float _attackCooldown = 1.5f;

        [Tooltip("Seconds the hitbox stays active during an attack (COM-004).")]
        [Min(0f)]
        [SerializeField] private float _attackWindup = 0.3f;

        [Tooltip("Resistance to knockback. Higher means less displacement (SRS 13 Tank).")]
        [Min(0f)]
        [SerializeField] private float _knockbackResistance = 0f;

        [Tooltip("Seconds a corpse stays in the scene before returning to the pool (HPS-007). Long enough for the death feedback to read, short enough not to clutter a wave.")]
        [Min(0f)]
        [SerializeField] private float _corpseLingerSeconds = 0.5f;

        [Header("Rewards (EXP-001, SRS 16)")]
        [Tooltip("XP granted once on death (EXP-001, HPS-007). Granted exactly once (SRS 34).")]
        [Min(0f)]
        [SerializeField] private float _experienceReward = 10f;

        [Tooltip("Gold dropped on death (SRS 16).")]
        [Min(0)]
        [SerializeField] private int _goldReward = 1;

        [Tooltip("Gems dropped on death. Normally 0; gems come from bosses and rare rewards (SRS 16).")]
        [Min(0)]
        [SerializeField] private int _gemReward = 0;

        [Header("Elite (ELT-001, ELT-004)")]
        [Tooltip("Modifiers this enemy may receive when promoted to elite (ELT-001).")]
        [SerializeField] private EliteModifierData[] _eliteModifiers = new EliteModifierData[0];

        [Tooltip("Reward multiplier when this enemy spawns as an elite (ELT-004).")]
        [Min(1f)]
        [SerializeField] private float _eliteRewardMultiplier = 3f;

        [Header("Presentation")]
        [Tooltip("Prefab pooled by the spawner (SRS 29, AI-006).")]
        [SerializeField] private GameObject _prefab;

        /// <summary>Stats including Defense and DamageReduction (HPS-009).</summary>
        public StatBlock BaseStats => _baseStats;

        /// <summary>Archetype (SRS 13).</summary>
        public EnemyArchetype Archetype => _archetype;

        /// <summary>Detection range (AI-002).</summary>
        public float DetectionRange => _detectionRange;

        /// <summary>Attack commit range.</summary>
        public float AttackRange => _attackRange;

        /// <summary>Seconds between attacks (AI-004).</summary>
        public float AttackCooldown => _attackCooldown;

        /// <summary>Telegraph window before the hitbox activates (COM-004).</summary>
        public float AttackWindup => _attackWindup;

        /// <summary>Knockback resistance.</summary>
        public float KnockbackResistance => _knockbackResistance;

        /// <summary>Seconds between death and the return to the pool (HPS-007, SRS 29).</summary>
        public float CorpseLingerSeconds => _corpseLingerSeconds;

        /// <summary>XP granted on death (EXP-001).</summary>
        public float ExperienceReward => _experienceReward;

        /// <summary>Gold dropped (SRS 16).</summary>
        public int GoldReward => _goldReward;

        /// <summary>Gems dropped (SRS 16).</summary>
        public int GemReward => _gemReward;

        /// <summary>Elite modifier candidates (ELT-001).</summary>
        public EliteModifierData[] EliteModifiers => _eliteModifiers;

        /// <summary>Elite reward multiplier (ELT-004).</summary>
        public float EliteRewardMultiplier => _eliteRewardMultiplier;

        /// <summary>Pooled prefab (AI-006, SRS 29).</summary>
        public GameObject Prefab => _prefab;
    }
}
