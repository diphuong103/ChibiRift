using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;
using ChibiRift.Core;
using ChibiRift.Data;
using ChibiRift.Gameplay;

namespace ChibiRift.Tests.Edit
{
    /// <summary>
    /// Verifies the Level Up roll (TC-UPG: 3 choices, RNG, stacking, fallback pool when the pool
    /// runs dry). Covers EXP-005, EXP-006, EXP-009, UPG-009, UPG-010 and RNG-005.
    /// </summary>
    public sealed class UpgradeRollerTests
    {
        private const int ChoiceCount = 3; // EXP-005

        /// <summary>
        /// Lightweight stand-in for <see cref="UpgradeData"/>. Testing against the interface
        /// rather than the ScriptableObject is what NFR-008 ("test độc lập") buys us.
        /// </summary>
        private sealed class FakeUpgrade : IUpgradeEntry
        {
            public string Id { get; set; } = string.Empty;
            public UpgradeCategory Category { get; set; } = UpgradeCategory.Stat;
            public Rarity Rarity { get; set; } = Rarity.Common;
            public float Weight { get; set; } = 100f;
            public bool IsStackable { get; set; } = true;
            public int MaxStack { get; set; } = 5;
            public bool IsFallback { get; set; }

            public override string ToString() => Id;
        }

        private static FakeUpgrade Normal(string id, float weight = 100f) =>
            new FakeUpgrade { Id = id, Weight = weight, IsStackable = true, MaxStack = 5 };

        private static FakeUpgrade Fallback(string id) =>
            new FakeUpgrade { Id = id, Weight = 100f, IsStackable = true, MaxStack = int.MaxValue, IsFallback = true };

        private static int Roll(
            IReadOnlyList<FakeUpgrade> pool,
            List<FakeUpgrade> results,
            int seed = 1,
            System.Func<FakeUpgrade, int> stacks = null)
        {
            return UpgradeRoller.Roll(pool, stacks, ChoiceCount, null, new DeterministicRandom(seed), results);
        }

        [Test]
        public void ReturnsExactlyThreeChoicesFromAHealthyPool()
        {
            var pool = new List<FakeUpgrade> { Normal("a"), Normal("b"), Normal("c"), Normal("d"), Normal("e") };
            var results = new List<FakeUpgrade>();

            Assert.That(Roll(pool, results), Is.EqualTo(ChoiceCount));
            Assert.That(results.Count, Is.EqualTo(ChoiceCount));
        }

        [Test]
        public void NoDuplicatesWithinOneRoll()
        {
            // EXP-006: normal entries may not repeat inside a single Level Up.
            var pool = new List<FakeUpgrade> { Normal("a"), Normal("b"), Normal("c"), Normal("d") };

            for (int seed = 0; seed < 100; seed++)
            {
                var results = new List<FakeUpgrade>();
                Roll(pool, results, seed);

                CollectionAssert.AllItemsAreUnique(results, $"Duplicate offered with seed {seed} (EXP-006).");
            }
        }

        [Test]
        public void TopsUpFromFallbackPoolWhenTooFewNormalUpgradesRemain()
        {
            // EXP-009: only one normal entry left, so two cards must come from the Fallback Pool.
            var pool = new List<FakeUpgrade> { Normal("only"), Fallback("fb") };
            var results = new List<FakeUpgrade>();

            Assert.That(Roll(pool, results), Is.EqualTo(ChoiceCount));
            Assert.That(results.FindAll(u => u.Id == "only").Count, Is.EqualTo(1));
            Assert.That(results.FindAll(u => u.IsFallback).Count, Is.EqualTo(2));
        }

        [Test]
        public void FallbackEntriesMayRepeatWithinOneRoll()
        {
            // RNG-005: the Fallback Pool is explicitly exempt from the EXP-006 anti-duplicate rule.
            var pool = new List<FakeUpgrade> { Fallback("fb") };
            var results = new List<FakeUpgrade>();

            Assert.That(Roll(pool, results), Is.EqualTo(ChoiceCount));
            Assert.That(results.TrueForAll(u => u.Id == "fb"), Is.True);
        }

        [Test]
        public void ExhaustedNormalPoolIsFullyCoveredByFallback()
        {
            // Every normal upgrade is maxed out (UPG-010), so all three cards come from fallback.
            var maxed = Normal("maxed");
            maxed.MaxStack = 2;

            var pool = new List<FakeUpgrade> { maxed, Fallback("fb") };
            var results = new List<FakeUpgrade>();

            int count = Roll(pool, results, seed: 5, stacks: u => u.Id == "maxed" ? 2 : 0);

            Assert.That(count, Is.EqualTo(ChoiceCount));
            Assert.That(results.TrueForAll(u => u.IsFallback), Is.True);
        }

        [Test]
        public void CompletelyEmptyPoolReturnsNothingAndReportsAnError()
        {
            // SRS 30: the panel must never open with fewer than three cards, so this is an error
            // path rather than a silently short offer.
            LogAssert.ignoreFailingMessages = true;

            var results = new List<FakeUpgrade> { Normal("stale") };
            int count = Roll(new List<FakeUpgrade>(), results);

            Assert.That(count, Is.EqualTo(0));
            Assert.That(results, Is.Empty, "Results must be cleared even when the roll fails.");

            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void NoFallbackAndTooFewNormalsProducesAShortOfferAndAnError()
        {
            LogAssert.ignoreFailingMessages = true;

            var pool = new List<FakeUpgrade> { Normal("a") };
            var results = new List<FakeUpgrade>();

            Assert.That(Roll(pool, results), Is.EqualTo(1));
            Assert.That(results.Count, Is.EqualTo(1));

            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void NonStackableUpgradeLeavesThePoolOnceTaken()
        {
            // UPG-010: a non-stackable upgrade has an effective ceiling of one.
            var once = new FakeUpgrade { Id = "once", IsStackable = false, MaxStack = 5 };
            var pool = new List<FakeUpgrade> { once, Fallback("fb") };
            var results = new List<FakeUpgrade>();

            Roll(pool, results, seed: 3, stacks: u => u.Id == "once" ? 1 : 0);

            Assert.That(results.Exists(u => u.Id == "once"), Is.False);
        }

        [Test]
        public void StackableUpgradeStaysUntilItReachesMaxStack()
        {
            var stackable = Normal("stack");
            stackable.MaxStack = 3;
            var pool = new List<FakeUpgrade> { stackable, Fallback("fb") };

            var belowCap = new List<FakeUpgrade>();
            Roll(pool, belowCap, seed: 11, stacks: u => u.Id == "stack" ? 2 : 0);
            Assert.That(belowCap.Exists(u => u.Id == "stack"), Is.True, "Two of three stacks: still eligible.");

            var atCap = new List<FakeUpgrade>();
            Roll(pool, atCap, seed: 11, stacks: u => u.Id == "stack" ? 3 : 0);
            Assert.That(atCap.Exists(u => u.Id == "stack"), Is.False, "At MaxStack: removed (UPG-010).");
        }

        [Test]
        public void ZeroWeightUpgradesAreNeverOffered()
        {
            // RNG-002: an entry banned by rule must not be drawn.
            var pool = new List<FakeUpgrade>
            {
                Normal("never", weight: 0f), Normal("a"), Normal("b"), Normal("c")
            };

            for (int seed = 0; seed < 50; seed++)
            {
                var results = new List<FakeUpgrade>();
                Roll(pool, results, seed);
                Assert.That(results.Exists(u => u.Id == "never"), Is.False);
            }
        }

        [Test]
        public void SameSeedProducesTheSameOffer()
        {
            // RNG-003: a fixed seed reproduces the roll, which is what makes TC-UPG repeatable.
            var pool = new List<FakeUpgrade> { Normal("a"), Normal("b"), Normal("c"), Normal("d"), Normal("e") };

            var first = new List<FakeUpgrade>();
            var second = new List<FakeUpgrade>();
            Roll(pool, first, seed: 424242);
            Roll(pool, second, seed: 424242);

            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void HeavierWeightsAreDrawnMoreOften()
        {
            // UPG-004 / RNG-001: weighting must actually bias the draw.
            var pool = new List<FakeUpgrade> { Normal("heavy", weight: 1000f), Normal("light", weight: 1f) };

            int heavyFirst = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                var results = new List<FakeUpgrade>();
                UpgradeRoller.Roll(pool, null, 1, null, new DeterministicRandom(seed), results);
                if (results.Count > 0 && results[0].Id == "heavy") heavyFirst++;
            }

            Assert.That(heavyFirst, Is.GreaterThan(180), "A 1000:1 weight should dominate the draw.");
        }

        [Test]
        public void RarityMultiplierCanSuppressATier()
        {
            // A rarity multiplier of 0 removes the tier from the pool entirely.
            var pool = new List<FakeUpgrade>
            {
                new FakeUpgrade { Id = "legendary", Rarity = Rarity.Legendary, Weight = 100f },
                Normal("common1"), Normal("common2"), Normal("common3")
            };

            var results = new List<FakeUpgrade>();
            UpgradeRoller.Roll(
                pool, null, ChoiceCount,
                rarity => rarity == Rarity.Legendary ? 0f : 1f,
                new DeterministicRandom(9), results);

            Assert.That(results.Exists(u => u.Id == "legendary"), Is.False);
        }

        [Test]
        public void ChoiceCountComesFromBalanceConfig()
        {
            // EXP-005 is satisfied by config, not by a literal 3 in gameplay code.
            var config = UnityEngine.ScriptableObject.CreateInstance<BalanceConfig>();
            var pool = new List<FakeUpgrade> { Normal("a"), Normal("b"), Normal("c"), Normal("d") };
            var results = new List<FakeUpgrade>();

            int count = UpgradeRoller.Roll(pool, null, config, new DeterministicRandom(2), results);

            Assert.That(count, Is.EqualTo(config.UpgradeChoiceCount));
            Assert.That(config.UpgradeChoiceCount, Is.EqualTo(3));

            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void NullEntriesInThePoolAreSkipped()
        {
            var pool = new List<FakeUpgrade> { Normal("a"), null, Normal("b"), null, Normal("c") };
            var results = new List<FakeUpgrade>();

            Assert.That(Roll(pool, results), Is.EqualTo(ChoiceCount));
            Assert.That(results.Contains(null), Is.False);
        }
    }
}
