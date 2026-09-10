using System;
using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// One enemy instance (SRS 13). Implements <see cref="IPoolable"/> so the spawner recycles it
    /// instead of destroying it (SRS 29).
    /// </summary>
    /// <remarks>
    /// Health is not here. This component used to implement <see cref="IDamageable"/> with its own
    /// copy of hit points and its own death transition, duplicating what the hero did separately.
    /// <see cref="HealthComponent"/> now owns both for every entity; this component configures it
    /// from <see cref="EnemyData"/> and reacts to its death (HPS-007).
    /// </remarks>
    [RequireComponent(typeof(HealthComponent))]
    [DisallowMultipleComponent]
    public sealed class EnemyController : MonoBehaviour, IPoolable
    {
        [Header("Data")]
        [Tooltip("Archetype definition. Every stat comes from here (NFR-007).")]
        [SerializeField] private EnemyData _enemyData;

        /// <summary>Raised once on death, so XP and reward grant exactly once (HPS-007, SRS 34).</summary>
        public event Action<EnemyController> Died;

        /// <summary>True when spawned as an elite (ELT-001).</summary>
        public bool IsElite { get; private set; }

        /// <summary>Archetype definition.</summary>
        public EnemyData Data => _enemyData;

        /// <summary>Health for this instance. The one place its hit points live.</summary>
        public HealthComponent Health => _health;

        private HealthComponent _health;
        private EnemyMotor _motor;
        private EnemyAttack _attack;
        private EnemyAI _ai;

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();
            _motor = GetComponent<EnemyMotor>();
            _attack = GetComponent<EnemyAttack>();
            _ai = GetComponent<EnemyAI>();

            // NFR-007: one asset drives every component, so changing a number in the asset changes
            // behaviour with no script edit. Done in Awake as well as in Configure so an enemy
            // placed straight into a scene behaves the same as a spawned one.
            if (_enemyData != null) Distribute(_enemyData);
        }

        private void OnEnable()
        {
            if (_health != null) _health.Died += OnHealthDied;
        }

        private void OnDisable()
        {
            if (_health != null) _health.Died -= OnHealthDied;
        }

        /// <summary>Configures the instance for a wave, applying stage and elite scaling.</summary>
        public void Configure(EnemyData data, bool asElite, float healthMultiplier, float damageMultiplier)
        {
            // TODO(ELT-001): apply BalanceConfig.EliteHealthMultiplier and EliteDamageMultiplier
            //   on top of the stage multipliers when asElite is set.
            // TODO(ELT-005): clamp the combined multipliers to the configured caps.
            _enemyData = data;
            IsElite = asElite;

            Distribute(data);
        }

        /// <summary>
        /// Pushes the archetype to every component that reads numbers from it (NFR-007).
        /// One place does this, so a new component reading <see cref="EnemyData"/> is wired by
        /// adding a line here rather than by remembering to set a field in the inspector.
        /// </summary>
        private void Distribute(EnemyData data)
        {
            if (data == null) return;

            _enemyData = data;

            if (_motor != null) _motor.Data = data;
            if (_attack != null) _attack.Data = data;
            if (_ai != null) _ai.Data = data;
            if (_health != null) _health.SeedFrom(data);
        }

        /// <summary>
        /// Death reached through the shared pipeline (HPS-007). <see cref="HealthComponent"/>
        /// already guarantees this fires once, so XP and reward grant once too (SRS 34).
        /// </summary>
        private void OnHealthDied(HealthComponent health)
        {
            // TODO(EXP-001): grant EnemyData.ExperienceReward here.
            // TODO(SRS-16): drop GoldReward and GemReward here.
            Died?.Invoke(this);
        }

        /// <inheritdoc />
        public void OnSpawnedFromPool()
        {
            if (_health != null && _enemyData != null) _health.SeedFrom(_enemyData);

            // TODO(SRS-29): reset the state machine and VFX so a recycled enemy behaves like new.
        }

        /// <inheritdoc />
        public void OnReturnedToPool()
        {
            // TODO(SRS-29): clear timers so the instance holds no stale references.
        }
    }
}
