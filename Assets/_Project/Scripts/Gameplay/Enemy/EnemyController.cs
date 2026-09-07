using System;
using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// One enemy instance (SRS 13). Implements <see cref="IDamageable"/> so it flows through the
    /// same pipeline as the hero (HPS-003), and <see cref="IPoolable"/> so the spawner recycles
    /// it instead of destroying it (SRS 29).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyController : MonoBehaviour, IDamageable, IPoolable
    {
        [Header("Data")]
        [Tooltip("Archetype definition. Every stat comes from here (NFR-007).")]
        [SerializeField] private EnemyData _enemyData;

        /// <summary>Raised once on death, so XP and reward grant exactly once (HPS-007, SRS 34).</summary>
        public event Action<EnemyController> Died;

        /// <inheritdoc />
        public float CurrentHealth { get; private set; }

        /// <inheritdoc />
        public float MaxHealth { get; private set; }

        /// <inheritdoc />
        public bool IsDead { get; private set; }

        /// <inheritdoc />
        public bool IsInvulnerable => false;

        /// <inheritdoc />
        public float Defense => _enemyData != null ? _enemyData.BaseStats.Defense : 0f;

        /// <inheritdoc />
        public float DamageReduction => _enemyData != null ? _enemyData.BaseStats.DamageReduction : 0f;

        /// <summary>True when spawned as an elite (ELT-001).</summary>
        public bool IsElite { get; private set; }

        /// <summary>Archetype definition.</summary>
        public EnemyData Data => _enemyData;

        /// <summary>Configures the instance for a wave, applying stage and elite scaling.</summary>
        public void Configure(EnemyData data, bool asElite, float healthMultiplier, float damageMultiplier)
        {
            // TODO(ELT-001): apply BalanceConfig.EliteHealthMultiplier and EliteDamageMultiplier
            //   on top of the stage multipliers when asElite is set.
            // TODO(ELT-005): clamp the combined multipliers to the configured caps.
            _enemyData = data;
            IsElite = asElite;
        }

        /// <inheritdoc />
        public void ApplyDamage(in DamageResult result)
        {
            // TODO(HPS-004): ignore the hit when IsDead.
            // TODO(HPS-007): subtract, and on reaching 0 enter Death and raise Died exactly once.
        }

        /// <summary>
        /// Single death transition (HPS-007). The guard means XP and reward are granted exactly
        /// once no matter how many hits land on the same frame (SRS 34).
        /// </summary>
        private void EnterDeathState()
        {
            if (IsDead) return;

            IsDead = true;
            CurrentHealth = 0f;
            Died?.Invoke(this);
        }

        /// <inheritdoc />
        public void OnSpawnedFromPool()
        {
            // TODO(SRS-29): reset HP, state machine and VFX so a recycled enemy behaves like a new one.
        }

        /// <inheritdoc />
        public void OnReturnedToPool()
        {
            // TODO(SRS-29): clear timers and subscriptions so the instance holds no stale references.
        }
    }
}
