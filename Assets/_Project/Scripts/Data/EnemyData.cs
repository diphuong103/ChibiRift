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

        [Tooltip("Distance at which the enemy notices the hero and starts chasing (AI-002).")]
        [Min(0f)]
        [SerializeField] private float _detectionRange = 8f;

        [Tooltip("Distance at which the enemy gives up and walks home. Deliberately larger than DetectionRange: the gap is hysteresis, so a hero standing on the boundary does not make the state flicker (AI-002).")]
        [Min(0f)]
        [SerializeField] private float _loseAggroRange = 12f;

        [Tooltip("Distance at which the enemy commits to an attack (AI-004).")]
        [Min(0f)]
        [SerializeField] private float _attackRange = 1.2f;

        [Tooltip("Windup, active and recovery phases plus the cooldown between attacks (AI-004).")]
        [SerializeField] private EnemyAttackConfig _attack = EnemyAttackConfig.MeleeBaseline;

        [Tooltip("Seconds the enemy is stunned after taking a hit. Long enough to read, short enough not to make it a punching bag.")]
        [Min(0f)]
        [SerializeField] private float _hurtStunDuration = 0.2f;

        [Tooltip("Gravity, fall speed and ground probe. Mirrors the hero's MovementConfig so both fall under the same rules.")]
        [SerializeField] private EnemyPhysicsConfig _physics = EnemyPhysicsConfig.MeleeBaseline;

        [Tooltip("How close to its spawn point counts as home when walking back after losing aggro (AI-002).")]
        [Min(0.01f)]
        [SerializeField] private float _spawnArrivalTolerance = 0.15f;

        [Tooltip("Seconds an anti-stuck sidestep runs before normal chasing resumes (AI-005).")]
        [Min(0.01f)]
        [SerializeField] private float _evadeDuration = 0.35f;

        [Header("Anti-stuck (AI-005)")]
        [Tooltip("Seconds of chasing over which displacement is measured. Moving less than the minimum below means the enemy is wedged.")]
        [Min(0.01f)]
        [SerializeField] private float _stuckCheckWindow = 0.5f;

        [Tooltip("World units the enemy must cover within the check window to count as making progress (AI-005).")]
        [Min(0f)]
        [SerializeField] private float _stuckMinDisplacement = 0.1f;

        [Tooltip("Enemies closer than this push each other apart, so a group reads as a crowd instead of one silhouette (SRS 5).")]
        [Min(0f)]
        [SerializeField] private float _separationRadius = 0.6f;

        [Tooltip("Strength of that push, in units per second added to horizontal velocity.")]
        [Min(0f)]
        [SerializeField] private float _separationForce = 2f;

        [Tooltip("Resistance to knockback. Higher means less displacement (SRS 13 Tank).")]
        [Min(0f)]
        [SerializeField] private float _knockbackResistance = 0f;

        [Tooltip("Seconds a corpse stays in the scene before it is retired (HPS-007). Long enough for the death feedback to read, short enough not to clutter a wave.")]
        [Min(0f)]
        [SerializeField] private float _corpseLingerSeconds = 1f;

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

        /// <summary>Distance at which the enemy starts chasing (AI-002).</summary>
        public float DetectionRange => _detectionRange;

        /// <summary>Distance at which the enemy gives up and returns to spawn (AI-002).</summary>
        public float LoseAggroRange => _loseAggroRange;

        /// <summary>Attack commit range (AI-004).</summary>
        public float AttackRange => _attackRange;

        /// <summary>Attack phase timing and cooldown (AI-004, COM-004).</summary>
        public EnemyAttackConfig Attack => _attack;

        /// <summary>Seconds of stun after taking a hit.</summary>
        public float HurtStunDuration => _hurtStunDuration;

        /// <summary>Gravity, fall speed and ground probe (AI-001).</summary>
        public EnemyPhysicsConfig Physics => _physics;

        /// <summary>Distance from spawn that counts as having arrived home (AI-002).</summary>
        public float SpawnArrivalTolerance => _spawnArrivalTolerance;

        /// <summary>Seconds an anti-stuck sidestep lasts (AI-005).</summary>
        public float EvadeDuration => _evadeDuration;

        /// <summary>Seconds of chasing over which stuck detection measures displacement (AI-005).</summary>
        public float StuckCheckWindow => _stuckCheckWindow;

        /// <summary>Minimum distance covered in that window before the enemy counts as stuck (AI-005).</summary>
        public float StuckMinDisplacement => _stuckMinDisplacement;

        /// <summary>Distance within which enemies push each other apart (AI-005).</summary>
        public float SeparationRadius => _separationRadius;

        /// <summary>Strength of the separation push (AI-005).</summary>
        public float SeparationForce => _separationForce;

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
