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
    /// <para><b>Naming.</b> <c>CombatSystem.DealDamage</c> is the single entry point; a caller
    /// wanting to hurt something calls this. <c>IDamageable.ApplyDamage</c> is the receiving end,
    /// which only this class calls. The two are easy to confuse and must not be swapped.</para>
    ///
    /// <para>No other type calls <see cref="IDamageable.ApplyDamage"/>;
    /// <c>DamagePipelineSourceTests</c> asserts that, though only textually (OI-19). Keeping the
    /// single entry point matters for more than tidiness: the death guard, the i-frame guard, the
    /// crit roll and the telemetry event all live here, so a second path would silently skip all
    /// four.</para>
    ///
    /// <para><b>What this class deliberately does not do.</b> It resolves damage and announces the
    /// hit. Knockback (COM-005) is applied by the target, which subscribes to
    /// <c>DamageAppliedEvent</c> through <see cref="IKnockbackReceiver"/>. Hit-stop and screen
    /// shake will subscribe to the same event. Every one of those is a reaction to a hit, not part
    /// of resolving it, and putting them here would mean editing this class once per reaction.</para>
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
        /// <param name="attackerPosition">Origin of the hit. Sets the knockback direction (COM-005).</param>
        /// <returns>The full result, or <c>default</c> when the hit was refused by a guard.</returns>
        public DamageResult DealDamage(
            IDamageable target,
            float baseDamage,
            float attackModifiers,
            float critChance,
            DamageSource source,
            Vector2 worldPosition,
            Vector2 attackerPosition)
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

            // HPS-008 and COM-005: the one announcement of a landed hit. EntityDiedEvent is
            // published by HealthComponent, which is the only place that knows the hit was lethal.
            bool targetIsPlayer = TargetIsPlayer(target);

            _eventBus?.Publish(new DamageAppliedEvent(
                EntityIdOf(target),
                result,
                worldPosition,
                targetIsPlayer,
                attackerPosition,
                targetIsPlayer ? _balance.HeroKnockbackForce : _balance.EnemyKnockbackForce,
                targetIsPlayer ? _balance.HeroKnockbackDuration : _balance.EnemyKnockbackDuration));

            return result;
        }

        private static int EntityIdOf(IDamageable target)
            => target is HealthComponent health ? health.EntityId : 0;

        private static bool TargetIsPlayer(IDamageable target)
            => target is HealthComponent health && health.IsPlayer;
    }
}
