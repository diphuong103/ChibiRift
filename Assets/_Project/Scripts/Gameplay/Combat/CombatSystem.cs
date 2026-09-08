using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// The one route damage may take from an attacker to a target (HPS-003).
    /// Wraps <see cref="DamageCalculator"/>, enforces the "no damage to the dead" rule (HPS-004)
    /// and the invulnerability rule (HPS-005), then publishes the feedback events the UI needs.
    /// </summary>
    /// <remarks>
    /// No other type calls <see cref="IDamageable.ApplyDamage"/>; <c>DamagePipelineSourceTests</c>
    /// asserts that, though only textually (OI-19). Keeping the single entry point matters for
    /// more than tidiness: the death guard, the i-frame guard, the crit roll and the telemetry
    /// event all live here, so a second path would silently skip all four.
    /// </remarks>
    public sealed class CombatSystem : IGameService
    {
        private readonly EventBus _eventBus;
        private readonly BalanceConfig _balance;
        private readonly DeterministicRandom _random;

        public CombatSystem(EventBus eventBus, BalanceConfig balance, DeterministicRandom random)
        {
            _eventBus = eventBus;
            _balance = balance;
            _random = random;
        }

        /// <summary>Balance asset this instance resolves MinDamage and the reduction cap from.</summary>
        public BalanceConfig Balance => _balance;

        /// <summary>
        /// Applies one hit end to end: guards, crit roll, the SRS 9 formula, then events.
        /// </summary>
        /// <param name="target">Receiver. Its own Defense and DamageReduction are read here, so a
        /// caller cannot pass stats that disagree with the thing being hit (HPS-009).</param>
        /// <param name="baseDamage">Attacker's Attack stat before modifiers.</param>
        /// <param name="attackModifiers">Product of build modifiers and the combo step multiplier.</param>
        /// <param name="critChance">0..1. Pass 0 to skip the roll.</param>
        /// <param name="source">Attribution for the per-source damage log (TEL-003).</param>
        /// <param name="worldPosition">Where the floating number appears (HPS-008).</param>
        /// <returns>The full result, or <c>default</c> when the hit was refused by a guard.</returns>
        public DamageResult DealDamage(
            IDamageable target,
            float baseDamage,
            float attackModifiers,
            float critChance,
            DamageSource source,
            Vector2 worldPosition)
        {
            if (target == null) return default;

            // HPS-004 and HPS-005. Checked here as well as in HealthComponent so that a refused
            // hit publishes no DamageAppliedEvent and therefore spawns no damage number.
            if (target.IsDead) return default;
            if (target.IsInvulnerable) return default;

            if (_balance == null)
            {
                GameLog.Error("Combat", "CombatSystem has no BalanceConfig; MinDamage is unavailable (SRS 30).");
                return default;
            }

            // COM-006: rolled here, outside the formula, so Calculate stays deterministic (RNG-004).
            // TODO(COM-006): slice 4 (P1-13) passes the hero's real crit chance; until then callers
            // pass 0 and every hit resolves as a normal hit.
            bool isCritical = DamageCalculator.RollCritical(critChance, _random);

            var request = new DamageRequest(
                baseDamage,
                attackModifiers,
                isCritical,
                _balance.DefaultCritMultiplier,
                target.Defense,
                target.DamageReduction,
                _balance.MinDamage,
                _balance.MaxDamageReduction,
                source);

            DamageResult result = DamageCalculator.Calculate(request);

            target.ApplyDamage(result);

            // HPS-008: the floating number. EntityDiedEvent is published by HealthComponent, which
            // is the only place that knows the hit was actually lethal.
            _eventBus?.Publish(new DamageAppliedEvent(
                EntityIdOf(target), result, worldPosition, TargetIsPlayer(target)));

            return result;
        }

        private static int EntityIdOf(IDamageable target)
            => target is HealthComponent health ? health.EntityId : 0;

        private static bool TargetIsPlayer(IDamageable target)
            => target is HealthComponent health && health.IsPlayer;
    }
}
