using System;
using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Live stat block for the hero during a Run (HER-001, HPS-001). Owns the invulnerability
    /// window shared by the post-hit i-frames and the dash window (HPS-005), and exposes the
    /// Defense / DamageReduction the pipeline needs (HPS-009).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerStats : MonoBehaviour, IDamageable
    {
        [Header("Data")]
        [Tooltip("Hero definition. Every base number comes from here, never from this script (SRS 35).")]
        [SerializeField] private HeroData _heroData;

        [Tooltip("Global balance asset supplying MinDamage and the DamageReduction cap.")]
        [SerializeField] private BalanceConfig _balanceConfig;

        /// <summary>Raised on every HP change so the HUD bar can follow (SRS 19.2).</summary>
        public event Action<float, float> HealthChanged;

        /// <summary>Raised when HP reaches zero and the hero enters Death state (HPS-006).</summary>
        public event Action Died;

        /// <inheritdoc />
        public float CurrentHealth { get; private set; }

        /// <inheritdoc />
        public float MaxHealth { get; private set; }

        /// <inheritdoc />
        public bool IsDead { get; private set; }

        /// <inheritdoc />
        public bool IsInvulnerable { get; private set; }

        /// <inheritdoc />
        public float Defense => _current.Defense;

        /// <inheritdoc />
        public float DamageReduction => _current.DamageReduction;

        /// <summary>Full live stat block, including upgrade contributions applied this Run (UPG-008).</summary>
        public StatBlock Current => _current;

        /// <summary>Hero definition backing this instance.</summary>
        public HeroData Hero => _heroData;

        /// <summary>Balance asset backing this instance.</summary>
        public BalanceConfig Balance => _balanceConfig;

        private StatBlock _current;

        /// <summary>
        /// Seeds the live block from HeroData plus purchased meta upgrades (META-001..003).
        /// </summary>
        public void Initialize(HeroData hero, BalanceConfig balance)
        {
            _heroData = hero;
            _balanceConfig = balance;
            _current = hero != null ? hero.BaseStats : default;

            // TODO(META-001, META-002, META-003): fold the purchased permanent upgrades into
            // _current here, before health is seeded from it.

            MaxHealth = _current.MaxHealth;
            CurrentHealth = MaxHealth;
            IsDead = false;
            IsInvulnerable = false;

            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        /// <summary>
        /// Single death transition (HPS-006). Guarded so Died is raised exactly once however
        /// many code paths reach zero health, which is what SRS 34 requires of reward grants.
        /// </summary>
        private void EnterDeathState()
        {
            if (IsDead) return;

            IsDead = true;
            CurrentHealth = 0f;

            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
            Died?.Invoke();
        }

        /// <inheritdoc />
        public void ApplyDamage(in DamageResult result)
        {
            // TODO(HPS-004): return immediately when IsDead so a corpse cannot be damaged again.
            // TODO(HPS-005): return immediately when IsInvulnerable.
            // TODO(HPS-006): subtract result.FinalDamage, raise HealthChanged, and enter Death at <= 0.
        }

        /// <summary>Opens an invulnerability window (HPS-005): post-hit or dash i-frames.</summary>
        public void BeginInvulnerability(float durationSeconds)
        {
            // TODO(HPS-005): run a timer on unscaled-independent game time and clear IsInvulnerable after it.
        }

        /// <summary>Recomputes the block after an upgrade is picked (EXP-008).</summary>
        public void RecalculateFromBuild()
        {
            // TODO(EXP-008): fold the RunState upgrade stacks into _current so the pick takes
            // effect immediately, then raise HealthChanged if MaxHealth moved.
        }
    }
}
