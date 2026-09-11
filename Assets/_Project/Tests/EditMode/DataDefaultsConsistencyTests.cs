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
        private static AttackData s_attack;
        private static EnemyData s_enemy;
        private static SkillData s_skillQ;
        private static SkillData s_skillE;
        private static SkillData s_skillR;

        [OneTimeSetUp]
        public void GenerateIntoScratchFolder()
        {
            DeleteScratchFolder();

            SampleDataGenerator.Generate(ScratchRoot);
            AssetDatabase.Refresh();

            s_balance = AssetDatabase.LoadAssetAtPath<BalanceConfig>($"{ScratchRoot}/BalanceConfig.asset");
            s_hero = AssetDatabase.LoadAssetAtPath<HeroData>($"{ScratchRoot}/HERO_Knight.asset");
            s_attack = AssetDatabase.LoadAssetAtPath<AttackData>($"{ScratchRoot}/ATK_KnightBasic.asset");
            s_enemy = AssetDatabase.LoadAssetAtPath<EnemyData>($"{ScratchRoot}/ENM_MeleeGrunt.asset");
            s_skillQ = AssetDatabase.LoadAssetAtPath<SkillData>($"{ScratchRoot}/SKL_Q_Fireball.asset");
            s_skillE = AssetDatabase.LoadAssetAtPath<SkillData>($"{ScratchRoot}/SKL_E_Shockwave.asset");
            s_skillR = AssetDatabase.LoadAssetAtPath<SkillData>($"{ScratchRoot}/SKL_R_Cataclysm.asset");

            Assert.That(s_balance, Is.Not.Null, $"The generator produced no BalanceConfig in {ScratchRoot}.");
            Assert.That(s_hero, Is.Not.Null, $"The generator produced no HERO_Knight in {ScratchRoot}.");
            Assert.That(s_attack, Is.Not.Null, $"The generator produced no ATK_KnightBasic in {ScratchRoot}.");
            Assert.That(s_enemy, Is.Not.Null, $"The generator produced no ENM_MeleeGrunt in {ScratchRoot}.");
            Assert.That(s_skillQ, Is.Not.Null, $"The generator produced no SKL_Q_Fireball in {ScratchRoot}.");
            Assert.That(s_skillE, Is.Not.Null, $"The generator produced no SKL_E_Shockwave in {ScratchRoot}.");
            Assert.That(s_skillR, Is.Not.Null, $"The generator produced no SKL_R_Cataclysm in {ScratchRoot}.");
        }

        [OneTimeTearDown]
        public void RemoveScratchFolder()
        {
            s_balance = null;
            s_hero = null;
            s_attack = null;
            s_enemy = null;
            s_skillQ = null;
            s_skillE = null;
            s_skillR = null;
            DeleteScratchFolder();
        }

        /// <summary>
        /// The generated assets, handed to each row's reader. A struct rather than more parameters
        /// so adding a fourth asset later does not touch every existing row.
        /// </summary>
        public readonly struct Generated
        {
            public readonly BalanceConfig Balance;
            public readonly HeroData Hero;
            public readonly AttackData Attack;
            public readonly EnemyData Enemy;
            public readonly SkillData SkillQ;
            public readonly SkillData SkillE;
            public readonly SkillData SkillR;

            public Generated(
                BalanceConfig balance,
                HeroData hero,
                AttackData attack,
                EnemyData enemy,
                SkillData skillQ,
                SkillData skillE,
                SkillData skillR)
            {
                Balance = balance;
                Hero = hero;
                Attack = attack;
                Enemy = enemy;
                SkillQ = skillQ;
                SkillE = skillE;
                SkillR = skillR;
            }

            /// <summary>Step <paramref name="index"/> of the basic chain, 0-based (COM-002).</summary>
            public AttackStep Step(int index) => Attack.GetStep(index);
        }

        /// <summary>
        /// One row per confirmed value: display name, the number the owner signed off, and how to
        /// read it back out of the generated assets. Add a row whenever a value is confirmed.
        /// </summary>
        public static IEnumerable<TestCaseData> ConfirmedValues()
        {
            // Combat and progression (SRS 35, OI-01, OI-02).
            yield return Row("critMultiplier (BalanceConfig)", 2.0f, g => g.Balance.DefaultCritMultiplier);
            yield return Row("critMultiplier (HERO_Knight)", 2.0f, g => g.Hero.BaseStats.CritMultiplier);
            yield return Row("baseExperience", 100f, g => g.Balance.BaseExperience);
            yield return Row("xpGrowthFactor", 1.4f, g => g.Balance.ExperienceGrowthFactor);
            yield return Row("eliteDamageMultiplier", 1.5f, g => g.Balance.EliteDamageMultiplier);

            // Dash (OI-03). Held in two places, so both are checked.
            yield return Row("dashDistance", 5.0f, g => g.Hero.Dash.Distance);
            yield return Row("dashDuration", 0.25f, g => g.Hero.Dash.Duration);
            yield return Row("dashIFrameDuration (BalanceConfig)", 0.25f, g => g.Balance.DashIFrameDuration);
            yield return Row("dashIFrameDuration (HERO_Knight)", 0.25f, g => g.Hero.Dash.IFrameDuration);
            yield return Row("dashCooldown (BalanceConfig)", 1.5f, g => g.Balance.DashCooldown);
            yield return Row("dashCooldown (HERO_Knight)", 1.5f, g => g.Hero.Dash.Cooldown);

            // Combo and i-frames (OI-04, OI-05).
            yield return Row("comboWindow (BalanceConfig)", 0.5f, g => g.Balance.DefaultComboWindow);
            yield return Row("comboWindow (HERO_Knight)", 0.5f, g => g.Hero.ComboWindow);
            yield return Row("hurtIFrameDuration", 0.8f, g => g.Hero.HurtIFrameDuration);

            // Locomotion (P1 slice 1).
            yield return Row("moveSpeed", 7f, g => g.Hero.BaseStats.MoveSpeed);
            yield return Row("jumpVelocity", 15.5f, g => g.Hero.Movement.JumpVelocity);
            yield return Row("doubleJumpVelocity", 13f, g => g.Hero.Movement.DoubleJumpVelocity);
            yield return Row("gravityUp", 40f, g => g.Hero.Movement.GravityUp);
            yield return Row("fallMultiplier", 1.6f, g => g.Hero.Movement.FallMultiplier);
            yield return Row("lowJumpMultiplier", 2.0f, g => g.Hero.Movement.LowJumpMultiplier);
            yield return Row("maxFallSpeed", 25f, g => g.Hero.Movement.MaxFallSpeed);
            yield return Row("coyoteTime", 0.1f, g => g.Hero.Movement.CoyoteTime);
            yield return Row("jumpBuffer", 0.12f, g => g.Hero.Movement.JumpBuffer);

            // Basic-attack chain (P1 slice 2). Frame data is in seconds because P1 has no
            // animation clips to hang Animation Events on (OI-18).
            yield return Row("attack1.totalDuration", 0.30f, g => g.Step(0).TotalDuration);
            yield return Row("attack1.activeStart", 0.10f, g => g.Step(0).ActiveStartTime);
            yield return Row("attack1.activeEnd", 0.16f, g => g.Step(0).ActiveEndTime);
            yield return Row("attack1.damageMult", 1.0f, g => g.Step(0).DamageMultiplier);

            yield return Row("attack2.totalDuration", 0.30f, g => g.Step(1).TotalDuration);
            yield return Row("attack2.activeStart", 0.10f, g => g.Step(1).ActiveStartTime);
            yield return Row("attack2.activeEnd", 0.16f, g => g.Step(1).ActiveEndTime);
            yield return Row("attack2.damageMult", 1.1f, g => g.Step(1).DamageMultiplier);

            yield return Row("attack3.totalDuration", 0.45f, g => g.Step(2).TotalDuration);
            yield return Row("attack3.activeStart", 0.15f, g => g.Step(2).ActiveStartTime);
            yield return Row("attack3.activeEnd", 0.24f, g => g.Step(2).ActiveEndTime);
            yield return Row("attack3.damageMult", 1.6f, g => g.Step(2).DamageMultiplier);

            yield return Row("hitboxWidth", 1.2f, g => g.Attack.HitboxWidth);
            yield return Row("hitboxHeight", 1.0f, g => g.Attack.HitboxHeight);
            yield return Row("hitboxOffsetDistance", 0.8f, g => g.Attack.HitboxOffsetDistance);

            // Not in the brief's list of 15: the brief named the 30% figure in prose only. Left
            // out of this list it would revert on the next generator run exactly like OI-05 did.
            yield return Row("moveSpeedMultiplierWhileAttacking", 0.3f,
                g => g.Attack.MoveSpeedMultiplierWhileAttacking);

            // Melee enemy (P1 slice 3).
            yield return Row("enemy.hp", 40f, g => g.Enemy.BaseStats.MaxHealth);
            yield return Row("enemy.damage", 8f, g => g.Enemy.BaseStats.Attack);
            yield return Row("enemy.defense", 0f, g => g.Enemy.BaseStats.Defense);
            yield return Row("enemy.damageReduction", 0f, g => g.Enemy.BaseStats.DamageReduction);
            yield return Row("enemy.moveSpeed", 3.0f, g => g.Enemy.BaseStats.MoveSpeed);

            // AI-002: the gap between these two is the hysteresis and must not be closed.
            yield return Row("enemy.detectionRange", 8.0f, g => g.Enemy.DetectionRange);
            yield return Row("enemy.loseAggroRange", 12.0f, g => g.Enemy.LoseAggroRange);
            yield return Row("enemy.attackRange", 1.2f, g => g.Enemy.AttackRange);

            yield return Row("enemy.attackWindup", 0.35f, g => g.Enemy.Attack.Windup);
            yield return Row("enemy.attackActive", 0.10f, g => g.Enemy.Attack.Active);
            yield return Row("enemy.attackRecovery", 0.45f, g => g.Enemy.Attack.Recovery);
            yield return Row("enemy.attackCooldown", 1.2f, g => g.Enemy.Attack.Cooldown);

            yield return Row("enemy.hurtStunDuration", 0.2f, g => g.Enemy.HurtStunDuration);
            yield return Row("enemy.corpseLingerSeconds", 1.0f, g => g.Enemy.CorpseLingerSeconds);

            // Knockback (COM-005).
            yield return Row("enemyKnockbackForce", 6.0f, g => g.Balance.EnemyKnockbackForce);
            yield return Row("enemyKnockbackDuration", 0.15f, g => g.Balance.EnemyKnockbackDuration);
            yield return Row("heroKnockbackForce", 4.0f, g => g.Balance.HeroKnockbackForce);
            yield return Row("heroKnockbackDuration", 0.12f, g => g.Balance.HeroKnockbackDuration);

            // Anti-stuck (AI-005).
            yield return Row("stuckCheckWindow", 0.5f, g => g.Enemy.StuckCheckWindow);
            yield return Row("stuckMinDisplacement", 0.1f, g => g.Enemy.StuckMinDisplacement);
            yield return Row("separationRadius", 0.6f, g => g.Enemy.SeparationRadius);
            yield return Row("separationForce", 2.0f, g => g.Enemy.SeparationForce);

            // Not in the brief's list: the brief asked for "a few seconds" in prose. Left out of
            // this list it would revert on the next generator run exactly like OI-05 did.
            yield return Row("enemyHealthBarHideDelay", 3.0f, g => g.Balance.EnemyHealthBarHideDelay);

            // Twelve more the brief did not list. They were about to be literals inside
            // ChibiRift.Gameplay, which the banned-literal audit correctly refuses: an enemy's
            // gravity and reach are balance numbers as much as its HP is. Defaults match what
            // would otherwise have been hard-coded, and mirror the hero where the two should agree.
            yield return Row("enemy.hitboxWidth", 1.2f, g => g.Enemy.Attack.HitboxWidth);
            yield return Row("enemy.hitboxHeight", 1.0f, g => g.Enemy.Attack.HitboxHeight);
            yield return Row("enemy.hitboxOffsetDistance", 0.8f, g => g.Enemy.Attack.HitboxOffsetDistance);

            yield return Row("enemy.gravity", 40f, g => g.Enemy.Physics.Gravity);
            yield return Row("enemy.maxFallSpeed", 25f, g => g.Enemy.Physics.MaxFallSpeed);
            yield return Row("enemy.groundCheckWidth", 0.7f, g => g.Enemy.Physics.GroundCheckWidth);
            yield return Row("enemy.groundCheckHeight", 0.1f, g => g.Enemy.Physics.GroundCheckHeight);
            yield return Row("enemy.groundCheckOffsetY", -0.9f, g => g.Enemy.Physics.GroundCheckOffsetY);

            yield return Row("enemy.spawnArrivalTolerance", 0.15f, g => g.Enemy.SpawnArrivalTolerance);
            yield return Row("enemy.evadeDuration", 0.35f, g => g.Enemy.EvadeDuration);

            yield return Row("hurtFlashesPerSecond", 8f, g => g.Balance.HurtFlashesPerSecond);
            yield return Row("hurtFlashMinAlpha", 0.25f, g => g.Balance.HurtFlashMinAlpha);

            // Hit stop (P1 slice 4A). The four must stay clearly apart or combo steps that differ
            // only by a damage number stay indistinguishable to the hands.
            yield return Row("hitStopLight", 0.04f, g => g.Balance.HitStop.Light);
            yield return Row("hitStopHeavy", 0.08f, g => g.Balance.HitStop.Heavy);
            yield return Row("hitStopCrit", 0.10f, g => g.Balance.HitStop.Critical);
            yield return Row("hitStopKill", 0.14f, g => g.Balance.HitStop.Kill);

            // Screen shake (CAM-003).
            yield return Row("shakeLight.amplitude", 0.12f, g => g.Balance.Shake.Light.Amplitude);
            yield return Row("shakeLight.duration", 0.10f, g => g.Balance.Shake.Light.Duration);
            yield return Row("shakeHeavy.amplitude", 0.25f, g => g.Balance.Shake.Heavy.Amplitude);
            yield return Row("shakeHeavy.duration", 0.16f, g => g.Balance.Shake.Heavy.Duration);
            yield return Row("shakeCrit.amplitude", 0.35f, g => g.Balance.Shake.Critical.Amplitude);
            yield return Row("shakeCrit.duration", 0.20f, g => g.Balance.Shake.Critical.Duration);
            yield return Row("shakeHeroHurt.amplitude", 0.30f, g => g.Balance.Shake.HeroHurt.Amplitude);
            yield return Row("shakeHeroHurt.duration", 0.18f, g => g.Balance.Shake.HeroHurt.Duration);

            // Flash and dash trail (SRS 21, MOV-006).
            yield return Row("flashDuration", 0.08f, g => g.Balance.Impact.FlashDuration);
            yield return Row("dashGhostInterval", 0.04f, g => g.Balance.Impact.DashGhostInterval);
            yield return Row("dashGhostLifetime", 0.15f, g => g.Balance.Impact.DashGhostLifetime);
            yield return Row("dashGhostAlpha", 0.4f, g => g.Balance.Impact.DashGhostAlpha);

            // Not in the brief's list: a particle count was needed and prose said "a few".
            yield return Row("impactParticleCount", 6f, g => g.Balance.Impact.ParticleCount);

            // Input buffering (P1-07). Separate fields from the jump buffer on purpose.
            yield return Row("attackBufferSeconds", 0.12f, g => g.Attack.AttackBufferSeconds);
            yield return Row("dashBufferSeconds", 0.12f, g => g.Hero.Dash.BufferSeconds);

            // Not in the brief's list: MOV-007 needs a threshold for "the wall stopped me".
            yield return Row("dashWallStopFraction", 0.1f, g => g.Hero.Dash.WallStopFraction);

            // Not in the brief's list: this is the coefficient of the jump's integration scheme,
            // which lived as a literal in ChibiRift.Gameplay until the audit list reached it. It is
            // in data so no tuning number sits in that assembly, not because it is tunable.
            yield return Row("launchGravityFraction", 0.5f, g => g.Hero.Movement.LaunchGravityFraction);

            // Skill Q, a projectile (COM-007).
            yield return Row("skillQ.damageMult", 1.5f, g => g.SkillQ.DamageMultiplier);
            yield return Row("skillQ.cooldown", 3.0f, g => g.SkillQ.Cooldown);
            yield return Row("skillQ.projectileSpeed", 12.0f, g => g.SkillQ.ProjectileSpeed);
            yield return Row("skillQ.projectileLifetime", 2.0f, g => g.SkillQ.ProjectileLifetime);
            yield return Row("skillQ.projectileRadius", 0.25f, g => g.SkillQ.ProjectileRadius);

            // Skill E, an area blast around the hero (COM-007).
            yield return Row("skillE.damageMult", 2.0f, g => g.SkillE.DamageMultiplier);
            yield return Row("skillE.cooldown", 6.0f, g => g.SkillE.Cooldown);
            yield return Row("skillE.radius", 2.5f, g => g.SkillE.Radius);
            yield return Row("skillE.windup", 0.20f, g => g.SkillE.Windup);

            // Skill R, the ultimate. One cast kills a full-health melee grunt: 10 x 4.0 = 40.
            yield return Row("skillR.damageMult", 4.0f, g => g.SkillR.DamageMultiplier);
            yield return Row("skillR.cooldown", 15.0f, g => g.SkillR.Cooldown);
            yield return Row("skillR.radius", 3.5f, g => g.SkillR.Radius);
            yield return Row("skillR.windup", 0.35f, g => g.SkillR.Windup);

            // Flat damage stays 0 on all three: P1 skills scale off Attack (see SkillData).
            yield return Row("skillQ.baseDamage", 0f, g => g.SkillQ.BaseDamage);
            yield return Row("skillE.baseDamage", 0f, g => g.SkillE.BaseDamage);
            yield return Row("skillR.baseDamage", 0f, g => g.SkillR.BaseDamage);

            // Pooling (SRS 29, AI-006).
            yield return Row("enemyPoolPrewarm", 16f, g => g.Balance.EnemyPoolPrewarm);
            yield return Row("projectilePoolPrewarm", 24f, g => g.Balance.ProjectilePoolPrewarm);

            // Not in the brief's list: ObjectPool needs a ceiling, or a bug that fires every frame
            // allocates until the process dies.
            yield return Row("enemyPoolMax", 48f, g => g.Balance.EnemyPoolMax);
            yield return Row("projectilePoolMax", 64f, g => g.Balance.ProjectilePoolMax);

            // Profiler harness (NFR-001, NFR-002).
            yield return Row("stressEnemyCount", 30f, g => g.Balance.StressEnemyCount);
            yield return Row("stressDurationSeconds", 10f, g => g.Balance.StressDurationSeconds);

            // Performance budgets that used to be literals in Core and Gameplay.
            yield return Row("sfxVoiceCount", 16f, g => g.Balance.SfxVoiceCount);
            yield return Row("maxTargetsPerSweep", 16f, g => g.Balance.MaxTargetsPerSweep);

            // Not in the brief's list: the F2/F3 keys need a scatter radius.
            yield return Row("debugSpawnRadius", 8f, g => g.Balance.DebugSpawnRadius);

            // Not in the brief's list: skills buffer their presses like everything else does.
            yield return Row("skillBufferSeconds", 0.12f, g => g.Balance.SkillBufferSeconds);
        }

        [TestCaseSource(nameof(ConfirmedValues))]
        public void GeneratedAssetKeepsConfirmedValue(
            string name, float expected, Func<Generated, float> read)
        {
            float actual = read(new Generated(s_balance, s_hero, s_attack, s_enemy, s_skillQ, s_skillE, s_skillR));

            Assert.That(
                actual,
                Is.EqualTo(expected).Within(Tolerance),
                $"{name} came out of SampleDataGenerator as {actual}, but {expected} was confirmed. " +
                "Fix the field initialiser in ChibiRift.Data — editing the .asset alone will be " +
                "overwritten the next time the generator runs.");
        }

        private static TestCaseData Row(
            string name, float expected, Func<Generated, float> read)
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
