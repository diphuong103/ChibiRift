using System;
using NUnit.Framework;
using UnityEngine;
using ChibiRift.Data;
using ChibiRift.Gameplay;

namespace ChibiRift.Tests.Edit
{
    /// <summary>
    /// Verifies the XP model of SRS section 10 (TC-UPG: XP threshold).
    /// XPRequired(level) = BaseXP * GrowthFactor^(level-1).
    /// </summary>
    public sealed class ExperienceCurveTests
    {
        private const float Tolerance = 0.001f;
        private const float BaseXp = 100f;
        private const float Growth = 1.15f;

        [Test]
        public void FirstLevelCostsExactlyBaseExperience()
        {
            // GrowthFactor^0 == 1, so level 1 must cost BaseXP with no scaling.
            Assert.That(
                ExperienceCurve.ExperienceRequired(1, BaseXp, Growth),
                Is.EqualTo(BaseXp).Within(Tolerance));
        }

        [Test]
        public void EachLevelScalesByTheGrowthFactor()
        {
            Assert.That(
                ExperienceCurve.ExperienceRequired(2, BaseXp, Growth),
                Is.EqualTo(BaseXp * Growth).Within(Tolerance));

            Assert.That(
                ExperienceCurve.ExperienceRequired(5, BaseXp, Growth),
                Is.EqualTo(BaseXp * Mathf.Pow(Growth, 4)).Within(Tolerance));
        }

        [Test]
        public void CurveIsMonotonicallyIncreasing()
        {
            float previous = 0f;
            for (int level = 1; level <= 30; level++)
            {
                float required = ExperienceCurve.ExperienceRequired(level, BaseXp, Growth);
                Assert.That(required, Is.GreaterThan(previous), $"Level {level} must cost more than level {level - 1}.");
                previous = required;
            }
        }

        [Test]
        public void GrowthFactorOfOneProducesAFlatCurve()
        {
            for (int level = 1; level <= 10; level++)
            {
                Assert.That(
                    ExperienceCurve.ExperienceRequired(level, BaseXp, 1f),
                    Is.EqualTo(BaseXp).Within(Tolerance));
            }
        }

        [Test]
        public void LevelBelowOneIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ExperienceCurve.ExperienceRequired(0, BaseXp, Growth));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ExperienceCurve.ExperienceRequired(-3, BaseXp, Growth));
        }

        [Test]
        public void TotalExperienceIsTheSumOfEveryPrecedingLevel()
        {
            float expected =
                ExperienceCurve.ExperienceRequired(1, BaseXp, Growth) +
                ExperienceCurve.ExperienceRequired(2, BaseXp, Growth);

            Assert.That(
                ExperienceCurve.TotalExperienceToReach(3, BaseXp, Growth),
                Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void TotalExperienceToReachLevelOneIsZero()
        {
            Assert.That(
                ExperienceCurve.TotalExperienceToReach(1, BaseXp, Growth),
                Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void LevelForTotalExperienceInvertsTheCurve()
        {
            Assert.That(ExperienceCurve.LevelForTotalExperience(0f, BaseXp, Growth, 50), Is.EqualTo(1));

            // Exactly enough for level 1 promotes to level 2.
            Assert.That(ExperienceCurve.LevelForTotalExperience(BaseXp, BaseXp, Growth, 50), Is.EqualTo(2));

            // One point short of the level 1 cost stays at level 1 (EXP-003).
            Assert.That(ExperienceCurve.LevelForTotalExperience(BaseXp - 1f, BaseXp, Growth, 50), Is.EqualTo(1));

            float toLevelFour = ExperienceCurve.TotalExperienceToReach(4, BaseXp, Growth);
            Assert.That(ExperienceCurve.LevelForTotalExperience(toLevelFour, BaseXp, Growth, 50), Is.EqualTo(4));
        }

        [Test]
        public void LevelForTotalExperienceRespectsTheLevelCap()
        {
            Assert.That(
                ExperienceCurve.LevelForTotalExperience(float.MaxValue / 2f, BaseXp, Growth, 10),
                Is.EqualTo(10));
        }

        [Test]
        public void NegativeExperienceIsTreatedAsZero()
        {
            Assert.That(ExperienceCurve.LevelForTotalExperience(-500f, BaseXp, Growth, 50), Is.EqualTo(1));
        }

        [Test]
        public void ParametersAreReadFromBalanceConfig()
        {
            // SRS 35: the curve terms are config, never literals in gameplay code.
            var config = ScriptableObject.CreateInstance<BalanceConfig>();

            Assert.That(
                ExperienceCurve.ExperienceRequired(3, config),
                Is.EqualTo(ExperienceCurve.ExperienceRequired(
                    3, config.BaseExperience, config.ExperienceGrowthFactor)).Within(Tolerance));

            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void NullBalanceConfigIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => ExperienceCurve.ExperienceRequired(1, null));
        }
    }
}
