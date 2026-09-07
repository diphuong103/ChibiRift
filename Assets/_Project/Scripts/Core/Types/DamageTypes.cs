namespace ChibiRift.Core
{
    /// <summary>Origin of a damage instance. Required for the per-source damage log (TEL-003).</summary>
    public enum DamageSource
    {
        Unknown = 0,
        BasicAttack = 1,
        Skill = 2,
        Passive = 3,
        Environment = 4,
        EliteExplosion = 5
    }

    /// <summary>
    /// Every input to the damage pipeline (HPS-003), gathered by the caller so that
    /// <c>DamageCalculator</c> stays pure and unit-testable (NFR-008).
    /// No value here may be a literal in gameplay code: all of them originate from
    /// <c>BalanceConfig</c>, <c>HeroData</c>, <c>EnemyData</c> or the live build state (SRS 35).
    /// </summary>
    public readonly struct DamageRequest
    {
        /// <summary>Attacker's base damage before modifiers.</summary>
        public readonly float BaseDamage;

        /// <summary>Product of all additive/multiplicative attack modifiers from the current build.</summary>
        public readonly float AttackModifiers;

        /// <summary>Whether the crit roll succeeded. Rolling is the caller's job, keeping this struct deterministic (RNG-004).</summary>
        public readonly bool IsCritical;

        /// <summary>Crit damage multiplier, from config (SRS 35: baseline 1.5x).</summary>
        public readonly float CriticalMultiplier;

        /// <summary>Target's flat armor (HPS-009).</summary>
        public readonly float TargetDefense;

        /// <summary>Target's proportional damage reduction, clamped to [0, MaxDamageReduction] (HPS-009).</summary>
        public readonly float TargetDamageReduction;

        /// <summary>Damage floor applied after armor (HPS-010; SRS 35 baseline 1).</summary>
        public readonly float MinDamage;

        /// <summary>Upper clamp for damage reduction (SRS 35: 0.8).</summary>
        public readonly float MaxDamageReduction;

        /// <summary>Where this hit came from (TEL-003).</summary>
        public readonly DamageSource Source;

        public DamageRequest(
            float baseDamage,
            float attackModifiers,
            bool isCritical,
            float criticalMultiplier,
            float targetDefense,
            float targetDamageReduction,
            float minDamage,
            float maxDamageReduction,
            DamageSource source = DamageSource.Unknown)
        {
            BaseDamage = baseDamage;
            AttackModifiers = attackModifiers;
            IsCritical = isCritical;
            CriticalMultiplier = criticalMultiplier;
            TargetDefense = targetDefense;
            TargetDamageReduction = targetDamageReduction;
            MinDamage = minDamage;
            MaxDamageReduction = maxDamageReduction;
            Source = source;
        }
    }

    /// <summary>
    /// Output of the damage pipeline. The intermediate values of SRS section 9 are exposed so
    /// each of the four mandatory steps can be asserted independently in unit tests.
    /// </summary>
    public readonly struct DamageResult
    {
        /// <summary>Step 1 and 2: BaseDamage * AttackModifiers, then * CriticalMultiplier when crit.</summary>
        public readonly float Raw;

        /// <summary>Step 3: (Raw - Defense) * (1 - DamageReduction). May be negative.</summary>
        public readonly float AfterArmor;

        /// <summary>Step 4: max(AfterArmor, MinDamage). The value actually applied to HP.</summary>
        public readonly float FinalDamage;

        /// <summary>Whether the crit multiplier was applied (COM-006).</summary>
        public readonly bool WasCritical;

        /// <summary>True when step 4 raised the result, i.e. armor fully absorbed the hit (HPS-010).</summary>
        public readonly bool WasClampedToMinimum;

        /// <summary>The effective damage reduction after clamping to [0, MaxDamageReduction].</summary>
        public readonly float ClampedDamageReduction;

        /// <summary>Where this hit came from (TEL-003).</summary>
        public readonly DamageSource Source;

        public DamageResult(
            float raw,
            float afterArmor,
            float finalDamage,
            bool wasCritical,
            bool wasClampedToMinimum,
            float clampedDamageReduction,
            DamageSource source)
        {
            Raw = raw;
            AfterArmor = afterArmor;
            FinalDamage = finalDamage;
            WasCritical = wasCritical;
            WasClampedToMinimum = wasClampedToMinimum;
            ClampedDamageReduction = clampedDamageReduction;
            Source = source;
        }
    }
}
