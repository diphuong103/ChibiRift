using System;
using System.Collections.Generic;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Builds the Level Up offer (EXP-005). Pure and static, driven by an injected
    /// <see cref="DeterministicRandom"/> so a seed reproduces a roll exactly (RNG-003, NFR-008).
    /// </summary>
    /// <remarks>
    /// Rules implemented, in the order they apply:
    /// <list type="bullet">
    ///   <item>UPG-010: an entry at its stack ceiling leaves the pool. Non-stackable entries have a ceiling of 1.</item>
    ///   <item>UPG-004 / RNG-001: draws are weighted by the entry's weight times its rarity multiplier.</item>
    ///   <item>EXP-006: no duplicates within one roll for normal entries.</item>
    ///   <item>RNG-005 / UPG-009: Fallback entries are exempt from that rule, are never capped, and may repeat.</item>
    ///   <item>EXP-009: when fewer than the requested number of normal entries remain, the roll is topped up from the Fallback Pool.</item>
    /// </list>
    /// SRS 30 forbids ever opening the panel with fewer than three cards, so a short result is
    /// logged as an error rather than silently accepted.
    /// </remarks>
    public static class UpgradeRoller
    {
        private const string LogCategory = "UpgradeRoller";

        /// <summary>
        /// Fills <paramref name="results"/> with the upgrades to offer.
        /// </summary>
        /// <param name="pool">Every upgrade in the game. Fallback membership is read from the entries themselves.</param>
        /// <param name="getStackCount">Current stacks already owned this Run. Null treats everything as unowned.</param>
        /// <param name="choiceCount">Cards to produce. Comes from <see cref="BalanceConfig.UpgradeChoiceCount"/> (EXP-005: 3).</param>
        /// <param name="rarityWeightMultiplier">Per-rarity multiplier. Null means no adjustment.</param>
        /// <param name="random">Seeded RNG. Required, so the roll is reproducible.</param>
        /// <param name="results">Cleared, then filled. Never null.</param>
        /// <returns>How many cards were produced. Fewer than <paramref name="choiceCount"/> means the pool and the Fallback Pool were both exhausted.</returns>
        public static int Roll<T>(
            IReadOnlyList<T> pool,
            Func<T, int> getStackCount,
            int choiceCount,
            Func<Rarity, float> rarityWeightMultiplier,
            DeterministicRandom random,
            List<T> results) where T : class, IUpgradeEntry
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (random == null) throw new ArgumentNullException(nameof(random));

            results.Clear();
            if (pool == null || pool.Count == 0 || choiceCount <= 0)
            {
                GameLog.Error(LogCategory, "Rolled against an empty pool; the Level Up panel must not open (SRS 30).");
                return 0;
            }

            var eligible = new List<T>(pool.Count);
            var fallback = new List<T>();

            for (int i = 0; i < pool.Count; i++)
            {
                T entry = pool[i];
                if (entry == null) continue;
                if (EffectiveWeight(entry, rarityWeightMultiplier) <= 0f) continue;

                // RNG-005 and UPG-009: fallback entries are uncapped and always available.
                if (entry.IsFallback)
                {
                    fallback.Add(entry);
                    continue;
                }

                if (IsAtStackCeiling(entry, getStackCount)) continue; // UPG-010
                eligible.Add(entry);
            }

            // EXP-006: draw normal entries without replacement so one roll has no duplicates.
            while (results.Count < choiceCount && eligible.Count > 0)
            {
                int index = PickWeightedIndex(eligible, rarityWeightMultiplier, random);
                if (index < 0) break;

                results.Add(eligible[index]);
                eligible.RemoveAt(index);
            }

            // EXP-009: top up from the Fallback Pool, which may repeat (RNG-005).
            while (results.Count < choiceCount && fallback.Count > 0)
            {
                int index = PickWeightedIndex(fallback, rarityWeightMultiplier, random);
                if (index < 0) break;

                results.Add(fallback[index]);
            }

            if (results.Count < choiceCount)
            {
                GameLog.Error(LogCategory,
                    $"Only {results.Count} of {choiceCount} cards available; add Fallback Pool entries (UPG-009, EXP-009).");
            }

            return results.Count;
        }

        /// <summary>Convenience overload taking the balance asset for choice count and rarity weights.</summary>
        public static int Roll<T>(
            IReadOnlyList<T> pool,
            Func<T, int> getStackCount,
            BalanceConfig config,
            DeterministicRandom random,
            List<T> results) where T : class, IUpgradeEntry
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            return Roll(
                pool,
                getStackCount,
                config.UpgradeChoiceCount,
                config.GetRarityWeightMultiplier,
                random,
                results);
        }

        /// <summary>
        /// UPG-010. A non-stackable upgrade is spent after one pick; a stackable one is spent at
        /// <see cref="IUpgradeEntry.MaxStack"/>.
        /// </summary>
        private static bool IsAtStackCeiling<T>(T entry, Func<T, int> getStackCount) where T : class, IUpgradeEntry
        {
            if (getStackCount == null) return false;

            int owned = getStackCount(entry);
            int ceiling = entry.IsStackable ? Math.Max(1, entry.MaxStack) : 1;
            return owned >= ceiling;
        }

        /// <summary>Entry weight combined with its rarity multiplier (UPG-004, RNG-001).</summary>
        private static float EffectiveWeight<T>(T entry, Func<Rarity, float> rarityWeightMultiplier)
            where T : class, IUpgradeEntry
        {
            float multiplier = rarityWeightMultiplier?.Invoke(entry.Rarity) ?? 1f;
            float weight = entry.Weight * multiplier;
            return weight > 0f ? weight : 0f;
        }

        /// <summary>Weighted index draw, or -1 when every candidate has zero weight.</summary>
        private static int PickWeightedIndex<T>(
            List<T> candidates,
            Func<Rarity, float> rarityWeightMultiplier,
            DeterministicRandom random) where T : class, IUpgradeEntry
        {
            float total = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                total += EffectiveWeight(candidates[i], rarityWeightMultiplier);
            }

            if (total <= 0f) return -1;

            float roll = random.NextFloat() * total;
            float cumulative = 0f;

            for (int i = 0; i < candidates.Count; i++)
            {
                cumulative += EffectiveWeight(candidates[i], rarityWeightMultiplier);
                if (roll < cumulative) return i;
            }

            // Floating point can leave roll a hair above the running total; take the last entry.
            return candidates.Count - 1;
        }
    }
}
