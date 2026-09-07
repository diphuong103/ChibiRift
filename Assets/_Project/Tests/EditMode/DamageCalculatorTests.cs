using NUnit.Framework;
using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;
using ChibiRift.Gameplay;

namespace ChibiRift.Tests.Edit
{
    /// <summary>
    /// Verifies the damage pipeline of SRS section 9 (TC-COM).
    /// The order of the four steps is mandatory, so several tests exist purely to pin it down:
    /// a formula that produced the right number by a different route would still be a defect.
    /// </summary>
    public sealed class DamageCalculatorTests
    {
        private const float Tolerance = 0.0001f;
        private const float MinDamage = 1f;          // SRS 35 baseline
        private const float MaxDamageReduction = 0.8f; // SRS 35 / HPS-009 cap

        private static DamageRequest Request(
            float baseDamage = 10f,
            float attackModifiers = 1f,
            bool isCritical = false,
            float criticalMultiplier = 1.5f,
            float targetDefense = 0f,
            float targetDamageReduction = 0f,
            float minDamage = MinDamage,
            float maxDamageReduction = MaxDamageReduction)
        {
            return new DamageRequest(
                baseDamage, attackModifiers, isCritical, criticalMultiplier,
                targetDefense, targetDamageReduction, minDamage, maxDamageReduction,
                DamageSource.BasicAttack);
        }

        [Test]
        public void Step1_MultipliesBaseDamageByAttackModifiers()
        {
            DamageResult result = DamageCalculator.Calculate(Request(baseDamage: 10f, attackModifiers: 2f));

            Assert.That(result.Raw, Is.EqualTo(20f).Within(Tolerance));
            Assert.That(result.FinalDamage, Is.EqualTo(20f).Within(Tolerance));
        }

        [Test]
        public void Step2_AppliesCriticalMultiplierOnlyOnCrit()
        {
            DamageResult normal = DamageCalculator.Calculate(
                Request(baseDamage: 10f, attackModifiers: 2f, isCritical: false, criticalMultiplier: 1.5f));
            DamageResult critical = DamageCalculator.Calculate(
                Request(baseDamage: 10f, attackModifiers: 2f, isCritical: true, criticalMultiplier: 1.5f));

            Assert.That(normal.Raw, Is.EqualTo(20f).Within(Tolerance));
            Assert.That(critical.Raw, Is.EqualTo(30f).Within(Tolerance));
            Assert.That(critical.WasCritical, Is.True);
            Assert.That(normal.WasCritical, Is.False);
        }

        [Test]
        public void Step3_SubtractsFlatDefenseBeforeApplyingDamageReduction()
        {
            // (100 - 20) * (1 - 0.5) = 40. Applying the reduction first would give 30.
            DamageResult result = DamageCalculator.Calculate(
                Request(baseDamage: 100f, targetDefense: 20f, targetDamageReduction: 0.5f));

            Assert.That(result.AfterArmor, Is.EqualTo(40f).Within(Tolerance));
            Assert.That(result.FinalDamage, Is.EqualTo(40f).Within(Tolerance));
        }

        [Test]
        public void OrderIsMandatory_CritAppliesBeforeArmorSubtraction()
        {
            // Correct order: raw = 10 * 1 * 2 = 20, then (20 - 10) = 10.
            // Wrong order (armor first, crit after) would give (10 - 10) * 2 = 0, clamped to MinDamage.
            DamageResult result = DamageCalculator.Calculate(
                Request(baseDamage: 10f, isCritical: true, criticalMultiplier: 2f, targetDefense: 10f));

            Assert.That(result.FinalDamage, Is.EqualTo(10f).Within(Tolerance));
            Assert.That(result.WasClampedToMinimum, Is.False);
        }

        [Test]
        public void Step4_NegativeDamageIsClampedToMinDamage()
        {
            // HPS-010: armor may exceed the hit, but the result can never fall below MinDamage.
            DamageResult result = DamageCalculator.Calculate(
                Request(baseDamage: 5f, targetDefense: 100f));

            Assert.That(result.AfterArmor, Is.LessThan(0f), "Step 3 is expected to go negative here.");
            Assert.That(result.FinalDamage, Is.EqualTo(MinDamage).Within(Tolerance));
            Assert.That(result.WasClampedToMinimum, Is.True);
        }

        [Test]
        public void Step4_ZeroDamageIsClampedToMinDamage()
        {
            DamageResult result = DamageCalculator.Calculate(
                Request(baseDamage: 10f, targetDefense: 10f));

            Assert.That(result.AfterArmor, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(result.FinalDamage, Is.EqualTo(MinDamage).Within(Tolerance));
            Assert.That(result.WasClampedToMinimum, Is.True);
        }

        [Test]
        public void DamageReductionAboveCapIsClampedToPointEight()
        {
            // HPS-009: DamageReduction is clamped to [0, 0.8], so 0.95 behaves as 0.8.
            DamageResult result = DamageCalculator.Calculate(
                Request(baseDamage: 100f, targetDamageReduction: 0.95f));

            Assert.That(result.ClampedDamageReduction, Is.EqualTo(0.8f).Within(Tolerance));
            Assert.That(result.AfterArmor, Is.EqualTo(20f).Within(Tolerance));
        }

        [Test]
        public void DamageReductionOfExactlyOneStillLeavesTwentyPercent()
        {
            // Without the cap this would zero the hit entirely; with it, 20% always lands.
            DamageResult result = DamageCalculator.Calculate(
                Request(baseDamage: 100f, targetDamageReduction: 1f));

            Assert.That(result.FinalDamage, Is.EqualTo(20f).Within(Tolerance));
        }

        [Test]
        public void NegativeDamageReductionIsClampedToZero()
        {
            // A negative reduction must not become a damage amplifier.
            DamageResult result = DamageCalculator.Calculate(
                Request(baseDamage: 100f, targetDamageReduction: -0.5f));

            Assert.That(result.ClampedDamageReduction, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(result.AfterArmor, Is.EqualTo(100f).Within(Tolerance));
        }

        [Test]
        public void SourceIsCarriedThroughForTelemetry()
        {
            // TEL-003 needs damage attributed per source.
            var request = new DamageRequest(
                10f, 1f, false, 1.5f, 0f, 0f, MinDamage, MaxDamageReduction, DamageSource.Skill);

            Assert.That(DamageCalculator.Calculate(request).Source, Is.EqualTo(DamageSource.Skill));
        }

        [Test]
        public void CreateRequest_TakesMinDamageAndCapFromBalanceConfig()
        {
            // Proves gameplay never has to spell out 1 or 0.8 itself (SRS 35).
            var config = ScriptableObject.CreateInstance<BalanceConfig>();
            var targetStats = new StatBlock { Defense = 3f, DamageReduction = 0.25f };

            DamageRequest request = DamageCalculator.CreateRequest(
                10f, 1f, false, 1.5f, targetStats, config, DamageSource.BasicAttack);

            Assert.That(request.MinDamage, Is.EqualTo(config.MinDamage).Within(Tolerance));
            Assert.That(request.MaxDamageReduction, Is.EqualTo(config.MaxDamageReduction).Within(Tolerance));
            Assert.That(request.TargetDefense, Is.EqualTo(3f).Within(Tolerance));

            Object.DestroyImmediate(config);
        }

        [Test]
        public void BalanceConfigDefaultsMatchTheSrsBaseline()
        {
            var config = ScriptableObject.CreateInstance<BalanceConfig>();

            Assert.That(config.MinDamage, Is.EqualTo(1f).Within(Tolerance), "SRS 35: MinDamage 1");
            Assert.That(config.MaxDamageReduction, Is.EqualTo(0.8f).Within(Tolerance), "SRS 35: cap 0.8");
            Assert.That(config.PlayerBaseHealth, Is.EqualTo(100f).Within(Tolerance), "SRS 35: HP 100");
            Assert.That(config.PlayerBaseAttack, Is.EqualTo(10f).Within(Tolerance), "SRS 35: Attack 10");
            Assert.That(config.PlayerBaseCritChance, Is.EqualTo(0.05f).Within(Tolerance), "SRS 35: 5% crit");
            Assert.That(config.DashCooldown, Is.EqualTo(1.5f).Within(Tolerance), "SRS 35: dash cooldown 1.5s");
            Assert.That(config.DashIFrameDuration, Is.EqualTo(0.25f).Within(Tolerance), "SRS 35: i-frame 0.25s");
            Assert.That(config.EliteHealthMultiplier, Is.EqualTo(3f).Within(Tolerance), "SRS 35: elite HP x3");
            Assert.That(config.EliteDamageMultiplier, Is.EqualTo(1.5f).Within(Tolerance), "SRS 35: elite dmg x1.5");
            Assert.That(config.UpgradeChoiceCount, Is.EqualTo(3), "EXP-005: exactly 3 cards");

            Object.DestroyImmediate(config);
        }

        [Test]
        public void RollCritical_IsDeterministicForAGivenSeed()
        {
            // RNG-004: randomness must not make damage untestable.
            var first = new DeterministicRandom(12345);
            var second = new DeterministicRandom(12345);

            for (int i = 0; i < 20; i++)
            {
                Assert.That(
                    DamageCalculator.RollCritical(0.5f, first),
                    Is.EqualTo(DamageCalculator.RollCritical(0.5f, second)));
            }
        }

        [Test]
        public void RollCritical_NeverCritsAtZeroChanceAndAlwaysCritsAtOne()
        {
            var random = new DeterministicRandom(7);

            for (int i = 0; i < 50; i++)
            {
                Assert.That(DamageCalculator.RollCritical(0f, random), Is.False);
                Assert.That(DamageCalculator.RollCritical(1f, random), Is.True);
            }
        }
    }
}
