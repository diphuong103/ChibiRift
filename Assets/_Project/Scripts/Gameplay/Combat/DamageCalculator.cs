using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// The single damage pipeline required by HPS-003. Pure and static: it touches no
    /// MonoBehaviour, no scene state and no global RNG, so NFR-008 ("Damage/XP/RNG có thể test
    /// độc lập") holds and the results are reproducible.
    /// </summary>
    /// <remarks>
    /// SRS section 9 fixes the order of operations, and that order is mandatory:
    /// <list type="number">
    ///   <item>Raw = BaseDamage * AttackModifiers</item>
    ///   <item>if crit: Raw = Raw * CriticalMultiplier</item>
    ///   <item>AfterArmor = (Raw - Target.Defense) * (1 - Target.DamageReduction)</item>
    ///   <item>FinalDamage = max(AfterArmor, MinDamage)</item>
    /// </list>
    /// DamageReduction is clamped to [0, MaxDamageReduction] (SRS 35: 0.8) and MinDamage
    /// guarantees a hit always lands for something (HPS-010).
    /// </remarks>
    public static class DamageCalculator
    {
        /// <summary>Runs the four mandatory steps of SRS section 9 in order.</summary>
        /// <param name="request">All inputs, gathered by the caller from config and stats.</param>
        /// <returns>Both the final damage and every intermediate value, for tests and telemetry.</returns>
        public static DamageResult Calculate(in DamageRequest request)
        {
            // Step 1: base damage scaled by the build's attack modifiers.
            float raw = request.BaseDamage * request.AttackModifiers;

            // Step 2: critical multiplier, applied only on a crit (COM-006).
            if (request.IsCritical) raw *= request.CriticalMultiplier;

            // Step 3: flat armor first, then proportional reduction (HPS-009).
            // The clamp ceiling itself comes from config so 0.8 is never a literal here.
            float reductionCeiling = Mathf.Clamp01(request.MaxDamageReduction);
            float clampedReduction = Mathf.Clamp(request.TargetDamageReduction, 0f, reductionCeiling);
            float afterArmor = (raw - request.TargetDefense) * (1f - clampedReduction);

            // Step 4: the damage floor. Armor can zero out or invert step 3, and HPS-010
            // requires the result to still land for MinDamage.
            bool clampedToMinimum = afterArmor < request.MinDamage;
            float finalDamage = clampedToMinimum ? request.MinDamage : afterArmor;

            return new DamageResult(
                raw,
                afterArmor,
                finalDamage,
                request.IsCritical,
                clampedToMinimum,
                clampedReduction,
                request.Source);
        }

        /// <summary>
        /// Assembles a <see cref="DamageRequest"/> from an attacker's numbers, a target's stats
        /// and the balance asset, so callers never spell out MinDamage or the reduction cap.
        /// </summary>
        public static DamageRequest CreateRequest(
            float baseDamage,
            float attackModifiers,
            bool isCritical,
            float criticalMultiplier,
            in StatBlock targetStats,
            BalanceConfig config,
            DamageSource source)
        {
            return new DamageRequest(
                baseDamage,
                attackModifiers,
                isCritical,
                criticalMultiplier,
                targetStats.Defense,
                targetStats.DamageReduction,
                config.MinDamage,
                config.MaxDamageReduction,
                source);
        }

        /// <summary>
        /// Rolls the critical chance (COM-006). Kept separate from <see cref="Calculate"/> so the
        /// formula itself stays deterministic, which is what RNG-004 asks for.
        /// </summary>
        public static bool RollCritical(float critChance, DeterministicRandom random)
            => random != null && random.NextBool(Mathf.Clamp01(critChance));
    }
}
