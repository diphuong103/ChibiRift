using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// The one route damage may take from an attacker to a target (HPS-003).
    /// Wraps <see cref="DamageCalculator"/>, enforces the "no damage to the dead" rule (HPS-004)
    /// and the invulnerability rule (HPS-005), then publishes the feedback events the UI needs.
    /// </summary>
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

        /// <summary>
        /// Applies one hit end to end: crit roll, the SRS 9 formula, the death guard, then events.
        /// </summary>
        public DamageResult DealDamage(
            IDamageable target,
            in StatBlock targetStats,
            float baseDamage,
            float attackModifiers,
            float critChance,
            float critMultiplier,
            DamageSource source)
        {
            // TODO(HPS-004): drop the hit when target.IsDead.
            // TODO(HPS-005): drop the hit when target.IsInvulnerable.
            // TODO(COM-006): roll crit via DamageCalculator.RollCritical(critChance, _random).
            // TODO(HPS-003): DamageCalculator.Calculate on the request, then target.ApplyDamage.
            // TODO(HPS-008): publish DamageAppliedEvent so the floating number appears.
            // TODO(HPS-007): publish EntityDiedEvent once, so XP and reward grant exactly once (SRS 34).
            return default;
        }
    }
}
