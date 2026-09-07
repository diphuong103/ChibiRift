using System;
using UnityEngine;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// The XP model of SRS section 10: XPRequired(level) = BaseXP * GrowthFactor^(level-1).
    /// Pure and static so the thresholds of EXP-003 are unit-testable (NFR-008).
    /// Both parameters come from <see cref="BalanceConfig"/>; neither is a literal here (SRS 35).
    /// </summary>
    public static class ExperienceCurve
    {
        /// <summary>The first level a hero occupies. Level 1 needs BaseXP to reach level 2.</summary>
        public const int FirstLevel = 1;

        /// <summary>XP needed to advance from <paramref name="level"/> to the next one.</summary>
        /// <param name="level">Current level, 1-based.</param>
        /// <param name="baseExperience">BaseXP term, from <see cref="BalanceConfig.BaseExperience"/>.</param>
        /// <param name="growthFactor">GrowthFactor term, from <see cref="BalanceConfig.ExperienceGrowthFactor"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">Level is below <see cref="FirstLevel"/>.</exception>
        public static float ExperienceRequired(int level, float baseExperience, float growthFactor)
        {
            if (level < FirstLevel)
                throw new ArgumentOutOfRangeException(nameof(level), level, $"Level starts at {FirstLevel}.");

            return baseExperience * Mathf.Pow(growthFactor, level - FirstLevel);
        }

        /// <summary>Convenience overload reading both terms from the balance asset.</summary>
        public static float ExperienceRequired(int level, BalanceConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            return ExperienceRequired(level, config.BaseExperience, config.ExperienceGrowthFactor);
        }

        /// <summary>Cumulative XP earned across every level from 1 up to <paramref name="level"/>.</summary>
        public static float TotalExperienceToReach(int level, float baseExperience, float growthFactor)
        {
            if (level < FirstLevel)
                throw new ArgumentOutOfRangeException(nameof(level), level, $"Level starts at {FirstLevel}.");

            float total = 0f;
            for (int i = FirstLevel; i < level; i++)
            {
                total += ExperienceRequired(i, baseExperience, growthFactor);
            }
            return total;
        }

        /// <summary>
        /// Level reached with <paramref name="totalExperience"/> banked, capped at
        /// <paramref name="maxLevel"/>. Lets a Run recompute level from a single running total
        /// instead of tracking per-level remainders.
        /// </summary>
        public static int LevelForTotalExperience(
            float totalExperience,
            float baseExperience,
            float growthFactor,
            int maxLevel)
        {
            int level = FirstLevel;
            float remaining = Mathf.Max(0f, totalExperience);

            while (level < maxLevel)
            {
                float needed = ExperienceRequired(level, baseExperience, growthFactor);
                if (remaining < needed) break;

                remaining -= needed;
                level++;
            }

            return level;
        }
    }
}
