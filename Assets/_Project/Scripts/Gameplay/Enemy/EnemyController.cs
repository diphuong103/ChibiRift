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
        private EnemySpawner _spawner;
        private EnemyMotor _motor;
        private EnemyAttack _attack;
        private EnemyAI _ai;

        /// <summary>Combined stage x elite HP scale (ELT-001). 1 until <see cref="Configure"/> runs.</summary>
        private float _healthMultiplier = 1f;

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
            if (_health == null) return;

            _health.Died += OnHealthDied;
            _health.CorpseExpired += OnCorpseExpired;
        }

        private void OnDisable()
        {
            if (_health == null) return;

            _health.Died -= OnHealthDied;
            _health.CorpseExpired -= OnCorpseExpired;
        }

        /// <summary>Records which spawner owns this instance, so the corpse goes back to it.</summary>
        public void SetSpawner(EnemySpawner spawner) => _spawner = spawner;

        /// <summary>Places the home position the AI walks back to on losing aggro (AI-002).</summary>
        public void SetSpawnPosition(Vector2 position)
        {
            if (_ai != null) _ai.SetSpawnPosition(position);
        }

        /// <summary>
        /// AI-006: the corpse goes back to the pool. A scene-placed enemy has no spawner, so it
        /// simply deactivates — the two cases are separated here rather than inside HealthComponent,
        /// which has no business knowing whether it is pooled.
        /// </summary>
        private void OnCorpseExpired(HealthComponent health)
        {
            if (_spawner != null && _spawner.Despawn(this)) return;

            gameObject.SetActive(false);
        }

        /// <summary>
        /// Configures the instance for a wave, applying stage and elite scaling (ELT-001).
        /// <paramref name="healthMultiplier"/>/<paramref name="damageMultiplier"/> are already the
        /// stage multiplier combined with <c>BalanceConfig.EliteHealthMultiplier</c>/
        /// <c>EliteDamageMultiplier</c> when <paramref name="asElite"/> is set — see
        /// <see cref="EnemySpawner.Spawn(EnemyData, Vector2, bool, float, float)"/>, which owns that
        /// arithmetic because it already holds the <c>BalanceConfig</c> reference this component
        /// does not need otherwise.
        /// </summary>
        /// <remarks>
        /// P2 slice 1 stops at the numbers: an elite here is tankier and hits harder with no visual
        /// tell. TODO(ELT-003): aura, tint, scale and its own HP bar. TODO(ELT-005): clamp the
        /// combined multipliers to a configured cap once a P2 system can push them arbitrarily high.
        /// </remarks>
        public void Configure(EnemyData data, bool asElite, float healthMultiplier, float damageMultiplier)
        {
            _enemyData = data;
            IsElite = asElite;
            _healthMultiplier = healthMultiplier;

            Distribute(data);

            if (_attack != null) _attack.DamageMultiplier = damageMultiplier;
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
            if (_health != null) _health.SeedFrom(data, _healthMultiplier);
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
        /// <remarks>
        /// Every piece of per-life state, in one place. A field missed here does not fail on the
        /// first reuse — it fails on a busy wave, as an enemy that spawns already hurt, already
        /// invulnerable, still sliding, or with no collider.
        /// </remarks>
        public void OnSpawnedFromPool()
        {
            if (_health != null && _enemyData != null)
            {
                _health.SeedFrom(_enemyData);       // health, i-frames, collider, corpse timer
                _health.ClearInvulnerability();
            }

            if (_motor != null) _motor.ResetMotion();   // velocity and any knockback window
            if (_attack != null) _attack.CancelAttack();
            if (_ai != null) _ai.ResetToIdle();         // state machine, stun, think timer
        }

        /// <inheritdoc />
        public void OnReturnedToPool()
        {
            if (_motor != null) _motor.ResetMotion();
            if (_attack != null) _attack.CancelAttack();
        }
    }
}
