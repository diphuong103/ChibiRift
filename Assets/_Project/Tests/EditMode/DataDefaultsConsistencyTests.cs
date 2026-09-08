using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using ChibiRift.Data;
using ChibiRift.EditorTools;

namespace ChibiRift.Tests.Edit
{
    /// <summary>
    /// Guards the balance values the project owner has confirmed against silent reversion.
    ///
    /// <para><b>The failure this exists to prevent.</b> <see cref="SampleDataGenerator"/> deletes and
    /// recreates every data asset, so each generated value comes from a field initialiser in
    /// <c>ChibiRift.Data</c>. Confirming a number by editing only the <c>.asset</c> file therefore
    /// looks correct until someone re-runs <b>ChibiRift → Setup</b>, at which point the initialiser
    /// wins and the confirmed value is gone with no error and no diff anyone is watching. That
    /// already happened once with <c>hurtIFrameDuration</c> (asset 0.8, initialiser 0.5, OI-05).</para>
    ///
    /// <para><b>How it works.</b> The generator is run into a scratch folder under <c>Assets/</c> —
    /// <see cref="AssetDatabase"/> refuses paths outside the asset tree, so a real temp directory is
    /// not an option — and every row below is compared against what actually came out. The shipped
    /// assets are never touched.</para>
    ///
    /// <para><b>Maintaining it.</b> Every time a number is confirmed, add one row to
    /// <see cref="ConfirmedValues"/>. That list is the only barrier between a confirmed value and a
    /// silent revert.</para>
    /// </summary>
    [TestFixture]
    public sealed class DataDefaultsConsistencyTests
    {
        /// <summary>Scratch generation target. Under Assets/ because AssetDatabase requires it.</summary>
        private const string ScratchRoot = "Assets/_DataDefaultsScratch";

        /// <summary>Float tolerance. Wide enough for serialisation, far tighter than any real drift.</summary>
        private const float Tolerance = 0.0001f;

        private static BalanceConfig s_balance;
        private static HeroData s_hero;

        [OneTimeSetUp]
        public void GenerateIntoScratchFolder()
        {
            DeleteScratchFolder();

            SampleDataGenerator.Generate(ScratchRoot);
            AssetDatabase.Refresh();

            s_balance = AssetDatabase.LoadAssetAtPath<BalanceConfig>($"{ScratchRoot}/BalanceConfig.asset");
            s_hero = AssetDatabase.LoadAssetAtPath<HeroData>($"{ScratchRoot}/HERO_Knight.asset");

            Assert.That(s_balance, Is.Not.Null, $"The generator produced no BalanceConfig in {ScratchRoot}.");
            Assert.That(s_hero, Is.Not.Null, $"The generator produced no HERO_Knight in {ScratchRoot}.");
        }

        [OneTimeTearDown]
        public void RemoveScratchFolder()
        {
            s_balance = null;
            s_hero = null;
            DeleteScratchFolder();
        }

        /// <summary>
        /// One row per confirmed value: display name, the number the owner signed off, and how to
        /// read it back out of the generated assets. Add a row whenever a value is confirmed.
        /// </summary>
        public static IEnumerable<TestCaseData> ConfirmedValues()
        {
            // Combat and progression (SRS 35, OI-01, OI-02).
            yield return Row("critMultiplier (BalanceConfig)", 2.0f, (b, h) => b.DefaultCritMultiplier);
            yield return Row("critMultiplier (HERO_Knight)", 2.0f, (b, h) => h.BaseStats.CritMultiplier);
            yield return Row("baseExperience", 100f, (b, h) => b.BaseExperience);
            yield return Row("xpGrowthFactor", 1.4f, (b, h) => b.ExperienceGrowthFactor);
            yield return Row("eliteDamageMultiplier", 1.5f, (b, h) => b.EliteDamageMultiplier);

            // Dash (OI-03). Held in two places, so both are checked.
            yield return Row("dashDistance", 5.0f, (b, h) => h.Dash.Distance);
            yield return Row("dashDuration", 0.25f, (b, h) => h.Dash.Duration);
            yield return Row("dashIFrameDuration (BalanceConfig)", 0.25f, (b, h) => b.DashIFrameDuration);
            yield return Row("dashIFrameDuration (HERO_Knight)", 0.25f, (b, h) => h.Dash.IFrameDuration);
            yield return Row("dashCooldown (BalanceConfig)", 1.5f, (b, h) => b.DashCooldown);
            yield return Row("dashCooldown (HERO_Knight)", 1.5f, (b, h) => h.Dash.Cooldown);

            // Combo and i-frames (OI-04, OI-05).
            yield return Row("comboWindow (BalanceConfig)", 0.5f, (b, h) => b.DefaultComboWindow);
            yield return Row("comboWindow (HERO_Knight)", 0.5f, (b, h) => h.ComboWindow);
            yield return Row("hurtIFrameDuration", 0.8f, (b, h) => h.HurtIFrameDuration);

            // Locomotion (P1 slice 1).
            yield return Row("moveSpeed", 7f, (b, h) => h.BaseStats.MoveSpeed);
            yield return Row("jumpVelocity", 15.5f, (b, h) => h.Movement.JumpVelocity);
            yield return Row("doubleJumpVelocity", 13f, (b, h) => h.Movement.DoubleJumpVelocity);
            yield return Row("gravityUp", 40f, (b, h) => h.Movement.GravityUp);
            yield return Row("fallMultiplier", 1.6f, (b, h) => h.Movement.FallMultiplier);
            yield return Row("lowJumpMultiplier", 2.0f, (b, h) => h.Movement.LowJumpMultiplier);
            yield return Row("maxFallSpeed", 25f, (b, h) => h.Movement.MaxFallSpeed);
            yield return Row("coyoteTime", 0.1f, (b, h) => h.Movement.CoyoteTime);
            yield return Row("jumpBuffer", 0.12f, (b, h) => h.Movement.JumpBuffer);
        }

        [TestCaseSource(nameof(ConfirmedValues))]
        public void GeneratedAssetKeepsConfirmedValue(
            string name, float expected, Func<BalanceConfig, HeroData, float> read)
        {
            float actual = read(s_balance, s_hero);

            Assert.That(
                actual,
                Is.EqualTo(expected).Within(Tolerance),
                $"{name} came out of SampleDataGenerator as {actual}, but {expected} was confirmed. " +
                "Fix the field initialiser in ChibiRift.Data — editing the .asset alone will be " +
                "overwritten the next time the generator runs.");
        }

        private static TestCaseData Row(
            string name, float expected, Func<BalanceConfig, HeroData, float> read)
        {
            return new TestCaseData(name, expected, read).SetName($"Confirmed: {name} == {expected}");
        }

        /// <summary>Removes the scratch folder and the .meta Unity leaves behind next to it.</summary>
        private static void DeleteScratchFolder()
        {
            if (AssetDatabase.IsValidFolder(ScratchRoot))
                AssetDatabase.DeleteAsset(ScratchRoot);

            if (Directory.Exists(ScratchRoot))
                Directory.Delete(ScratchRoot, true);

            string meta = $"{ScratchRoot}.meta";
            if (File.Exists(meta))
                File.Delete(meta);

            AssetDatabase.Refresh();
        }
    }
}
