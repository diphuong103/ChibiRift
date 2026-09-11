using System.Collections.Generic;
using System.IO;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ChibiRift.Core;
using ChibiRift.Data;
using ChibiRift.Gameplay;
using ChibiRift.UI;

namespace ChibiRift.Tests.Play
{
    /// <summary>
    /// Plays the real game: the real Boot scene builds the real services, the real
    /// <c>InputReader</c> reads simulated devices, and the real Run_01 prefabs act on them.
    /// </summary>
    /// <remarks>
    /// <para><b>Why this fixture is different from every other one.</b> The others build actors in
    /// code and drive them through <see cref="FakeInputService"/>. That keeps them fast and
    /// independent of scene layout, but it means the chain
    /// <c>device -> InputReader -> PlayerController -> PlayerCombat</c> and the chain
    /// <c>GameBootstrap -> ServiceLocator -> everything</c> had never executed in a test. Three
    /// separate playtest faults lived in exactly that gap.</para>
    ///
    /// <para>Nothing here is faked except the input devices themselves, which
    /// <see cref="InputTestFixture"/> replaces with a deterministic backend.</para>
    ///
    /// <para><b>Timeout.</b> Three times the slowest test in this fixture today. Boot plus a scene load per test makes this the slowest fixture; its longest test measured 6.40s. A
    /// test that hangs — waiting on a physics step while time is frozen is how it happens here —
    /// fails with a message rather than running forever. A run that never finishes reports nothing
    /// at all, which is why this is a guard and not a convenience (OI-28).</para>
    /// </remarks>
    [Timeout(20000)]
    public sealed class Run01SceneTests : InputTestFixture
    {
        private const string BootScene = "Boot";
        private const string RunScene = "Run_01";

        /// <summary>Long enough for gravity to settle an actor and for a fall to be obvious.</summary>
        private const float SettleSeconds = 0.5f;

        private Mouse _mouse;
        private Keyboard _keyboard;

        /// <summary>
        /// Boots the real game and lands in Run_01.
        /// </summary>
        /// <remarks>
        /// Called from the body of each test rather than from <c>[UnitySetUp]</c>, because
        /// <see cref="InputTestFixture"/> resets the input system in its own <c>[SetUp]</c>, which
        /// the framework runs <b>after</b> UnitySetUp. Devices created any earlier survive as
        /// objects but lose their state, and every read then throws "does not have any associated
        /// state".
        /// </remarks>
        private IEnumerator BootIntoRun01()
        {
            // Scene navigation buttons and the camera rig log about services this headless run does
            // not exercise. Their complaints are not what is under test.
            LogAssert.ignoreFailingMessages = true;

            // A hit stop left running by an earlier test would still hold the scale at zero, and
            // FixedUpdate does not run at zero — every WaitForFixedUpdate below would wait forever.
            Time.timeScale = 1f;

            // GameBootstrap survives scene loads and refuses to build a second root, so one left
            // over from an earlier test would keep its old InputReader registered, bound to devices
            // this test no longer has.
            DestroyExistingBootstrap();

            _mouse = InputSystem.AddDevice<Mouse>();
            _keyboard = InputSystem.AddDevice<Keyboard>();

            // Boot first: it is the composition root, and skipping it is what let a fake stand in
            // for the real InputReader in every other fixture.
            yield return SceneManager.LoadSceneAsync(BootScene, LoadSceneMode.Single);
            for (int i = 0; i < 5; i++) yield return null;

            Assert.That(ServiceLocator.Current, Is.Not.Null, "Boot did not build a ServiceLocator.");

            yield return SceneManager.LoadSceneAsync(RunScene, LoadSceneMode.Single);
            yield return Steps(StepsFor(SettleSeconds));
        }

        [UnityTearDown]
        public IEnumerator UnloadRealGame()
        {
            // Destroyed before the fixture disposes the input devices, so nothing is left polling a
            // device with no state.
            // Before the bootstrap goes: it hosts the hit-stop coroutine, so destroying it mid
            // freeze would strand timeScale at zero with nothing alive left to restore it.
            Time.timeScale = 1f;

            DestroyExistingBootstrap();
            yield return null;

            LogAssert.ignoreFailingMessages = false;
        }

        /// <summary>
        /// Removes the persistent composition root, if one is still alive from an earlier test.
        /// Its OnDestroy clears the ServiceLocator, so the next Boot builds a fresh one.
        /// </summary>
        private static void DestroyExistingBootstrap()
        {
            var bootstrap = Object.FindFirstObjectByType<GameBootstrap>();
            if (bootstrap != null) Object.DestroyImmediate(bootstrap.gameObject);

            if (ServiceLocator.Current != null) ServiceLocator.ClearCurrent();
        }

        // ----- the composition root -------------------------------------------------------

        [UnityTest]
        public IEnumerator Test_RealInputReader_HasHandlerForEveryWiredAction()
        {
            yield return BootIntoRun01();

            Assert.That(ServiceLocator.Current.TryGet(out IInputService input), Is.True,
                "No IInputService registered by Boot.");

            // A fake registered by accident would make every other assertion here meaningless.
            Assert.That(input.GetType().Name, Is.EqualTo("InputReader"),
                $"Boot registered {input.GetType().Name}, not the real InputReader.");

            // Move: a binding alone is not enough, the value has to arrive.
            Press(_keyboard.dKey);
            yield return null;
            Assert.That(input.MoveAxis, Is.GreaterThan(0.5f), "D produced no positive MoveAxis (MOV-001).");
            Release(_keyboard.dKey);
            yield return null;

            Press(_keyboard.aKey);
            yield return null;
            Assert.That(input.MoveAxis, Is.LessThan(-0.5f), "A produced no negative MoveAxis (MOV-001).");
            Release(_keyboard.aKey);
            yield return null;

            // Jump: both the press edge and the held state are consumed by the motor.
            Press(_keyboard.spaceKey);
            yield return null;
            Assert.That(input.JumpPressed, Is.True, "Space produced no JumpPressed (MOV-002).");
            Assert.That(input.JumpHeld, Is.True, "Space produced no JumpHeld (MOV-002).");
            Release(_keyboard.spaceKey);
            yield return null;

            // Attack: this is the one that was reported dead in playtest.
            Press(_mouse.leftButton);
            yield return null;
            Assert.That(input.AttackPressed, Is.True, "Mouse Left produced no AttackPressed (COM-001).");
            Release(_mouse.leftButton);
            yield return null;

            // Aim: a value, not an edge. It has to reach world space, which needs a live camera.
            Assert.That(Camera.main, Is.Not.Null, "No camera tagged MainCamera; aim cannot leave screen space.");

            Set(_mouse.position, new Vector2(50f, 50f));
            yield return null;
            Vector2 lowLeft = input.AimWorldPosition;

            Set(_mouse.position, new Vector2(Screen.width - 50f, Screen.height - 50f));
            yield return null;
            Vector2 highRight = input.AimWorldPosition;

            Assert.That(highRight.x, Is.GreaterThan(lowLeft.x),
                "Moving the cursor right did not move AimWorldPosition right (COM-009).");
            Assert.That(highRight.y, Is.GreaterThan(lowLeft.y),
                "Moving the cursor up did not move AimWorldPosition up (COM-009).");
        }

        [UnityTest]
        public IEnumerator Test_RealClick_SwingsAndDamages()
        {
            yield return BootIntoRun01();

            PlayerMotor motor = FindHeroMotor();
            var combat = motor.GetComponent<PlayerCombat>();

            GameObject dummy = GameObject.Find("Dummy_A");
            Assert.That(dummy, Is.Not.Null, "Run_01 has no Dummy_A to hit.");

            var target = dummy.GetComponent<HealthComponent>();
            Assert.That(target, Is.Not.Null, "Dummy_A has no HealthComponent.");

            // Stand next to it and aim at it. The hitbox reaches roughly 1.4u, so a click from
            // across the arena is a miss, not a fault.
            motor.Teleport(new Vector2(dummy.transform.position.x - 1f, 1f));
            Set(_mouse.position, new Vector2(Screen.width - 1f, Screen.height / 2f));
            yield return Steps(10);

            Assert.That(combat.AimDirection.x, Is.GreaterThan(0f),
                "Cursor is to the right but the hero is not aiming right (COM-009).");

            float before = target.CurrentHealth;

            Press(_mouse.leftButton);
            yield return null;
            Release(_mouse.leftButton);

            yield return Steps(StepsFor(0.5f));

            Assert.That(target.CurrentHealth, Is.LessThan(before),
                $"A real click next to Dummy_A dealt no damage. Health stayed at {before}. " +
                "This is the whole chain: mouse, InputReader, PlayerController, PlayerCombat, " +
                "CombatSystem, HealthComponent.");
        }

        [UnityTest]
        public IEnumerator Test_RealHit_ShowsFeedbackThePlayerCanSee()
        {
            yield return BootIntoRun01();

            PlayerMotor motor = FindHeroMotor();

            EnemyAI enemy = NearestEnemy(motor.transform.position);
            var enemyHealth = enemy.GetComponent<HealthComponent>();
            var bar = enemy.GetComponentInChildren<EnemyHealthBar>(true);

            Assert.That(bar, Is.Not.Null, "The enemy prefab carries no EnemyHealthBar.");
            Assert.That(bar.IsVisible, Is.False, "An untouched enemy should not show a health bar.");

            var spawner = Object.FindFirstObjectByType<DamageNumberSpawner>();
            Assert.That(spawner, Is.Not.Null, "Run_01 has no DamageNumberSpawner, so hits show no number.");

            // Stand next to the enemy and swing at it for real.
            motor.Teleport(new Vector2(enemy.transform.position.x - 1f, 1f));
            Set(_mouse.position, new Vector2(Screen.width - 1f, Screen.height / 2f));
            yield return Steps(10);

            float before = enemyHealth.CurrentHealth;

            Press(_mouse.leftButton);
            yield return null;
            Release(_mouse.leftButton);
            yield return Steps(StepsFor(0.5f));

            Assert.That(enemyHealth.CurrentHealth, Is.LessThan(before), "The swing never landed.");

            // Everything below is feedback the player reads the fight by. Each one has failed
            // silently at least once while the underlying system worked perfectly.
            Assert.That(bar.IsVisible, Is.True,
                "The enemy took damage but its health bar stayed hidden (SRS 19.2).");

            Assert.That(bar.Fraction, Is.LessThan(1f),
                $"The health bar is showing but still reads full at {bar.Fraction}.");

            DamageNumber[] numbers = Object.FindObjectsByType<DamageNumber>(FindObjectsSortMode.None);
            bool anyLive = false;
            foreach (DamageNumber number in numbers)
            {
                if (!number.IsFinished) anyLive = true;
            }

            Assert.That(anyLive, Is.True,
                $"A hit landed but none of the {numbers.Length} pooled damage numbers is running, " +
                "so nothing appeared on screen (HPS-008).");
        }

        // ----- the scene's own actors -----------------------------------------------------

        [UnityTest]
        public IEnumerator Test_Hero_IsGroundedAfterSpawning()
        {
            yield return BootIntoRun01();

            PlayerMotor motor = FindHeroMotor();

            Assert.That(motor.IsGrounded, Is.True,
                $"Hero is at {motor.transform.position} with velocity {motor.Velocity} and reports " +
                "IsGrounded false. Gravity is being cancelled by a collider, so it is standing on " +
                "the floor while the ground probe cannot see it.");

            Assert.That(motor.Velocity.y, Is.EqualTo(0f).Within(0.01f),
                $"Vertical velocity settled at {motor.Velocity.y}, not 0. One step of gravity being " +
                "re-applied every step means the motor does not know it is grounded.");
        }

        [UnityTest]
        public IEnumerator Test_RealSpaceKey_Jumps()
        {
            yield return BootIntoRun01();

            PlayerMotor motor = FindHeroMotor();
            Assert.That(motor.IsGrounded, Is.True, "Setup failed: hero never landed.");

            float restingY = motor.transform.position.y;

            Press(_keyboard.spaceKey);
            yield return Steps(StepsFor(0.2f));
            Release(_keyboard.spaceKey);

            Assert.That(motor.transform.position.y, Is.GreaterThan(restingY + 0.5f),
                "Pressing the real Space key did not lift the hero. This is what the player sees " +
                "as Space doing nothing.");
        }

        [UnityTest]
        public IEnumerator Test_Enemies_AreGroundedAfterSpawning()
        {
            yield return BootIntoRun01();

            EnemyMotor[] enemies = Object.FindObjectsByType<EnemyMotor>(FindObjectsSortMode.None);
            Assert.That(enemies.Length, Is.EqualTo(3), $"Expected 3 enemies in {RunScene}.");

            foreach (EnemyMotor enemy in enemies)
            {
                Assert.That(enemy.IsGrounded, Is.True,
                    $"{enemy.name} at {enemy.transform.position} is not grounded.");
            }
        }

        [UnityTest]
        public IEnumerator Test_Enemy_ChasesHeroInRealScene()
        {
            yield return BootIntoRun01();

            PlayerMotor motor = FindHeroMotor();

            EnemyAI nearest = NearestEnemy(motor.transform.position);
            float startDistance = Vector2.Distance(nearest.transform.position, motor.transform.position);

            Assert.That(startDistance, Is.LessThan(nearest.Data.DetectionRange),
                $"{nearest.name} starts {startDistance:F2}u away, outside its " +
                $"{nearest.Data.DetectionRange}u detection range, so this test would prove nothing.");

            float startX = nearest.transform.position.x;

            yield return Steps(StepsFor(2f));

            Assert.That(nearest.State, Is.EqualTo(EnemyLifecycleState.Chase)
                .Or.EqualTo(EnemyLifecycleState.Attack)
                .Or.EqualTo(EnemyLifecycleState.Recovery)
                .Or.EqualTo(EnemyLifecycleState.Hurt),
                $"{nearest.name} is still {nearest.State} with the hero {startDistance:F2}u away (AI-002).");

            // Two seconds at 3 u/s covers 6u. Requiring only 2u leaves room for the think throttle
            // and for the enemy stopping once it is in attack range, while still failing the case
            // that shipped: an enemy wedged behind a solid training dummy crawling 0.6u in six
            // seconds.
            float travelled = Mathf.Abs(nearest.transform.position.x - startX);

            Assert.That(travelled, Is.GreaterThan(2f),
                $"{nearest.name} moved only {travelled:F2}u in 2s while chasing. It is blocked by " +
                "something on the Enemy layer, or its move intent never reaches the motor (AI-005).");
        }

        // ----- MOV-006, MOV-007: dash ------------------------------------------------------

        [UnityTest]
        public IEnumerator Test_Dash_MovesExactDistance()
        {
            yield return BootIntoRun01();

            PlayerMotor motor = FindHeroMotor();
            PlayerDash dash = motor.GetComponent<PlayerDash>();
            DashConfig config = HeroDataOf(motor).Dash;

            // Aimed right: away from the hole at x = -8 and well short of the wall at x = 20.
            yield return AimAtScreenSide(0.9f);

            float startX = motor.transform.position.x;

            dash.RequestDash();
            yield return Steps(2);
            yield return WaitForDashToEnd(motor);

            float travelled = motor.transform.position.x - startX;

            // Constant velocity over a whole number of fixed steps, so the distance is exact to
            // within one step of travel. A dash that undershoots is a dash whose reach cannot be
            // learned, which is the entire point of a fixed distance.
            float tolerance = config.Speed * Time.fixedDeltaTime * 2f;
            Assert.That(travelled, Is.EqualTo(config.Distance).Within(tolerance),
                $"Dash covered {travelled:F3}u, expected {config.Distance}u.");
        }

        [UnityTest]
        public IEnumerator Test_Dash_GrantsInvulnerability()
        {
            yield return BootIntoRun01();

            PlayerMotor motor = FindHeroMotor();
            var health = motor.GetComponent<HealthComponent>();
            PlayerDash dash = motor.GetComponent<PlayerDash>();

            Assert.That(health.IsInvulnerable, Is.False, "Setup failed: hero started invulnerable.");

            dash.RequestDash();
            yield return Steps(2);

            Assert.That(health.IsInvulnerable, Is.True,
                "A dash must open the i-frame window (MOV-006, HPS-005).");
        }

        [UnityTest]
        public IEnumerator Test_Dash_DoesNotCutExistingHurtIFrame()
        {
            yield return BootIntoRun01();

            PlayerMotor motor = FindHeroMotor();
            var health = motor.GetComponent<HealthComponent>();
            PlayerDash dash = motor.GetComponent<PlayerDash>();

            float hurtWindow = HeroDataOf(motor).HurtIFrameDuration;
            float dashWindow = HeroDataOf(motor).Dash.IFrameDuration;

            Assert.That(hurtWindow, Is.GreaterThan(dashWindow),
                "This test only means something while the hurt window is the longer of the two.");

            health.BeginInvulnerability(hurtWindow);
            dash.RequestDash();
            yield return Steps(2);

            // Wait past the dash window but well short of the hurt window.
            yield return Steps(StepsFor(dashWindow + 0.05f));

            Assert.That(health.IsInvulnerable, Is.True,
                $"The {dashWindow}s dash window replaced the longer {hurtWindow}s hurt window " +
                "instead of being absorbed by it (HPS-005).");
        }

        [UnityTest]
        public IEnumerator Test_Dash_StopsAtWall()
        {
            yield return BootIntoRun01();

            PlayerMotor motor = FindHeroMotor();
            PlayerDash dash = motor.GetComponent<PlayerDash>();
            DashConfig config = HeroDataOf(motor).Dash;

            // Right up against the wall at x = +20, with less room than the dash distance.
            motor.Teleport(new Vector2(ArenaHalfWidth - 2f, motor.transform.position.y));
            yield return AimAtScreenSide(0.9f);
            yield return Steps(4);

            dash.RequestDash();
            yield return Steps(StepsFor(config.Duration) + 4);

            // MOV-007: the wall stops it. Passing through would put the hero outside the arena.
            float endX = motor.transform.position.x;

            Assert.That(endX, Is.GreaterThan(ArenaHalfWidth - 2f),
                "The hero dashed away from the wall, so nothing about MOV-007 was tested.");
            Assert.That(endX, Is.LessThan(ArenaHalfWidth),
                $"Hero dashed to x={endX:F2}, through the wall at {ArenaHalfWidth}.");
            Assert.That(motor.IsDashing, Is.False, "The dash should have ended at the wall.");
        }

        [UnityTest]
        public IEnumerator Test_Dash_RespectsCooldown()
        {
            yield return BootIntoRun01();

            PlayerMotor motor = FindHeroMotor();
            PlayerDash dash = motor.GetComponent<PlayerDash>();
            DashConfig config = HeroDataOf(motor).Dash;

            yield return AimAtScreenSide(0.9f);

            dash.RequestDash();
            yield return Steps(2);
            yield return WaitForDashToEnd(motor);
            yield return Steps(2);

            Assert.That(dash.CooldownRemaining, Is.GreaterThan(0f),
                "The cooldown should start when the dash window closes (MOV-006).");

            // Let the post-dash glide finish before taking the reference position, or the glide
            // would be mistaken for a second dash.
            yield return Steps(StepsFor(0.4f));
            float afterFirst = motor.transform.position.x;

            // Half way through the cooldown: a press must do nothing at all.
            dash.RequestDash();
            yield return Steps(StepsFor(config.Cooldown * 0.5f));

            Assert.That(motor.transform.position.x, Is.EqualTo(afterFirst).Within(1f),
                "A second dash ran while the cooldown was still going (MOV-006).");

            yield return Steps(StepsFor(config.Cooldown * 0.5f + 0.1f));

            Assert.That(dash.CanDash, Is.True,
                $"After {config.Cooldown}s the dash should be available again.");
        }

        [UnityTest]
        public IEnumerator Test_Dash_BufferedDuringHitStop()
        {
            yield return BootIntoRun01();

            PlayerMotor motor = FindHeroMotor();
            PlayerDash dash = motor.GetComponent<PlayerDash>();
            var pause = ServiceLocator.Current.Get<IPauseService>();

            yield return AimAtScreenSide(0.9f);

            // Freeze time, then press. Without buffering the press lands on a frozen game and is
            // simply gone, which the player reads as the button not working.
            pause.RequestHitStop(HeroDataOf(motor).Dash.BufferSeconds * 0.5f);
            yield return null;

            Assert.That(Time.timeScale, Is.EqualTo(0f).Within(0.001f), "Setup failed: time is not frozen.");

            dash.RequestDash();
            Assert.That(dash.BufferRemaining, Is.GreaterThan(0f), "The press was not buffered (P1-07).");

            yield return WaitForRealSeconds(0.2f);
            yield return Steps(StepsFor(HeroDataOf(motor).Dash.Duration) + 4);

            Assert.That(dash.CooldownRemaining, Is.GreaterThan(0f),
                "The buffered press never became a dash once time resumed (P1-07).");
        }

        // ----- COM-006: crit ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator Test_Crit_AppliesMultiplier()
        {
            yield return BootIntoRun01();

            var combat = ServiceLocator.Current.Get<CombatSystem>();
            var balance = BalanceOf(FindHeroMotor());
            HealthComponent target = FindDummy();

            // Chance 1 rather than hunting for a seed that crits: the multiplier is what is under
            // test, not the roll.
            DamageResult normal = combat.DealDamage(
                target, 10f, 1f, 0f, DamageSource.BasicAttack, target.transform.position, Vector2.zero);

            // Real time, not fixed steps: that hit froze the game, and FixedUpdate does not run
            // while it is frozen, so a WaitForFixedUpdate here would never return.
            yield return WaitForRealSeconds(HitStopSettle);

            DamageResult crit = combat.DealDamage(
                target, 10f, 1f, 1f, DamageSource.BasicAttack, target.transform.position, Vector2.zero);

            Assert.That(normal.WasCritical, Is.False, "A 0 chance hit should never crit.");
            Assert.That(crit.WasCritical, Is.True, "A chance of 1 must always crit (COM-006).");
            Assert.That(crit.FinalDamage,
                Is.EqualTo(normal.FinalDamage * balance.DefaultCritMultiplier).Within(0.001f),
                $"Crit should deal {balance.DefaultCritMultiplier}x the normal hit.");
        }

        [UnityTest]
        public IEnumerator Test_Crit_MarksDamageEventAsCrit()
        {
            yield return BootIntoRun01();

            var bus = ServiceLocator.Current.Get<EventBus>();
            var combat = ServiceLocator.Current.Get<CombatSystem>();
            HealthComponent target = FindDummy();

            bool sawCrit = false;
            void OnDamage(DamageAppliedEvent evt)
            {
                if (evt.Result.WasCritical) sawCrit = true;
            }

            bus.Subscribe<DamageAppliedEvent>(OnDamage);
            try
            {
                combat.DealDamage(
                    target, 10f, 1f, 1f, DamageSource.BasicAttack, target.transform.position, Vector2.zero);
                yield return WaitForRealSeconds(HitStopSettle);

                // The damage number colours itself off this flag, so it is the flag the UI needs
                // rather than the result the caller happens to hold.
                Assert.That(sawCrit, Is.True,
                    "DamageAppliedEvent did not report the crit, so the UI cannot colour it (HPS-008).");
            }
            finally
            {
                bus.Unsubscribe<DamageAppliedEvent>(OnDamage);
            }
        }

        // ----- SRS 21: hit stop ------------------------------------------------------------

        [UnityTest]
        public IEnumerator Test_HitStop_RestoresTimeScale()
        {
            yield return BootIntoRun01();

            var pause = ServiceLocator.Current.Get<IPauseService>();

            pause.RequestHitStop(0.05f);
            yield return null;

            Assert.That(Time.timeScale, Is.EqualTo(0f).Within(0.001f), "Hit stop did not freeze time.");

            yield return WaitForRealSeconds(0.2f);

            Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.001f),
                "Time never resumed. A stuck timeScale is a hung game, not a slow one.");
            Assert.That(pause.IsHitStopped, Is.False, "The hit stop should have finished.");
        }

        [UnityTest]
        public IEnumerator Test_HitStop_TakesLongerDurationNotSum()
        {
            yield return BootIntoRun01();

            var pause = ServiceLocator.Current.Get<IPauseService>();

            // Two hits on one frame, as a crowd produces. Summing them would stall the game for
            // long enough to read as a hitch.
            pause.RequestHitStop(0.05f);
            pause.RequestHitStop(0.08f);
            yield return null;

            // Past the longer of the two, but well short of their sum.
            yield return WaitForRealSeconds(0.11f);

            Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.001f),
                "Overlapping hit stops added up instead of taking the longer one.");
        }

        [UnityTest]
        public IEnumerator Test_HitStop_DoesNotCancelPause()
        {
            yield return BootIntoRun01();

            var pause = ServiceLocator.Current.Get<IPauseService>();

            pause.Pause(PauseReason.PauseMenu);
            Assert.That(Time.timeScale, Is.EqualTo(0f).Within(0.001f), "Setup failed: pause did not freeze time.");

            // A hit resolving during a pause. With two writers of timeScale this is the moment the
            // freeze expires and hands the scale back to 1, un-pausing the game underneath the
            // player.
            pause.RequestHitStop(0.05f);
            yield return WaitForRealSeconds(0.2f);

            Assert.That(Time.timeScale, Is.EqualTo(0f).Within(0.001f),
                "A hit stop finished during a pause and resumed the game (PAU-001).");
            Assert.That(pause.IsPaused, Is.True, "The pause itself was lost.");

            pause.Resume(PauseReason.PauseMenu);
        }

        [UnityTest]
        public IEnumerator Test_Pause_DuringHitStop_RestoresToOneOnResume()
        {
            yield return BootIntoRun01();

            var pause = ServiceLocator.Current.Get<IPauseService>();

            pause.RequestHitStop(0.2f);
            yield return null;
            Assert.That(pause.IsHitStopped, Is.True, "Setup failed: no hit stop is running.");

            // Pause lands in the middle of the freeze and takes over.
            pause.Pause(PauseReason.PauseMenu);
            yield return WaitForRealSeconds(0.3f);

            pause.Resume(PauseReason.PauseMenu);
            yield return null;

            // The danger is the opposite of the previous test: the abandoned hit stop leaving the
            // scale at 0 with nothing left to restore it.
            Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.001f),
                "Time stayed frozen after resuming: the cancelled hit stop never released the scale.");
        }

        // ----- SRS 21: flash, and CAM-003 ---------------------------------------------------

        [UnityTest]
        public IEnumerator Test_Flash_DoesNotLeakMaterials()
        {
            yield return BootIntoRun01();

            var combat = ServiceLocator.Current.Get<CombatSystem>();
            HealthComponent target = FindDummy();

            yield return WaitForRealSeconds(HitStopSettle);
            int before = Resources.FindObjectsOfTypeAll<Material>().Length;

            // Twenty hits. Writing Renderer.material clones the shared material on every access,
            // so a leaking implementation grows the count once per flash.
            for (int i = 0; i < 20; i++)
            {
                combat.DealDamage(
                    target, 1f, 1f, 0f, DamageSource.BasicAttack, target.transform.position, Vector2.zero);
                yield return WaitForRealSeconds(HitStopSettle);
            }

            int after = Resources.FindObjectsOfTypeAll<Material>().Length;

            Assert.That(after - before, Is.LessThanOrEqualTo(2),
                $"Material count grew from {before} to {after} over 20 flashes. Use a " +
                "MaterialPropertyBlock rather than assigning to Renderer.material.");
        }

        [UnityTest]
        public IEnumerator Test_Shake_DoesNotEscapeConfiner()
        {
            yield return BootIntoRun01();

            var bus = ServiceLocator.Current.Get<EventBus>();
            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null, $"{RunScene} has no main camera.");

            // Half the visible width: the confiner keeps the camera edge inside the polygon, so the
            // centre may come no closer than this to the arena edge.
            float halfWidth = camera.orthographicSize * camera.aspect;

            // Far harder than anything the game asks for, so a confiner that is being bypassed
            // shows up rather than hiding inside the tolerance.
            for (int i = 0; i < 10; i++)
            {
                bus.Publish(new ScreenShakeRequestedEvent(5f, 0.2f));
                yield return WaitForRealSeconds(0.05f);

                float limit = ArenaHalfWidth - halfWidth;
                Assert.That(Mathf.Abs(camera.transform.position.x), Is.LessThanOrEqualTo(limit + 1f),
                    $"Camera reached x={camera.transform.position.x:F2}, outside the confiner. " +
                    "The impulse listener must run before CinemachineConfiner2D (CAM-002, CAM-003).");
            }
        }

        // ----- SRS 22: audio ----------------------------------------------------------------

        [UnityTest]
        public IEnumerator Test_Sfx_NullClipDoesNotThrow()
        {
            yield return BootIntoRun01();

            var audio = ServiceLocator.Current.Get<IAudioService>();

            // Every clip is empty in P1 and that is the shipping state. Playing one must be silent
            // and uneventful, not an exception in the middle of combat.
            Assert.DoesNotThrow(() => audio.PlayOneShot(null));
            Assert.DoesNotThrow(() => audio.PlayOneShot(null, 0.5f));

            // And the real path: land a hit with an empty library behind it.
            var combat = ServiceLocator.Current.Get<CombatSystem>();
            HealthComponent target = FindDummy();

            combat.DealDamage(
                target, 1f, 1f, 0f, DamageSource.BasicAttack, target.transform.position, Vector2.zero);

            yield return WaitForRealSeconds(HitStopSettle);
        }

        // ----- COM-007, COM-008: skills ----------------------------------------------------

        [UnityTest]
        public IEnumerator Test_SkillQ_FiresProjectileTowardCursor()
        {
            yield return BootIntoRun01();

            PlayerMotor motor = FindHeroMotor();
            var skills = motor.GetComponent<SkillSystem>();
            var projectiles = motor.GetComponent<ProjectileSkill>();

            yield return AimAtScreenSide(0.9f);
            float heroX = motor.transform.position.x;

            skills.RequestCast(SkillSlot.Skill1);
            yield return Steps(4);

            Projectile fired = projectiles.LastFired;
            Assert.That(fired, Is.Not.Null, "Q fired no projectile (COM-007).");
            Assert.That(fired.Direction.x, Is.GreaterThan(0f),
                "Cursor is to the right, so the projectile must travel right. Aim comes from " +
                "PlayerCombat, with no auto-target (COM-009).");

            yield return Steps(StepsFor(0.2f));
            Assert.That(fired.transform.position.x, Is.GreaterThan(heroX),
                "The projectile did not move away from the hero.");
        }

        [UnityTest]
        public IEnumerator Test_SkillQ_ProjectileDealsDamageThroughCombatSystem()
        {
            yield return BootIntoRun01();

            PlayerMotor motor = FindHeroMotor();
            var skills = motor.GetComponent<SkillSystem>();
            var bus = ServiceLocator.Current.Get<EventBus>();

            // A pooled target in the empty left half, rather than one of the scene dummies. Two
            // reasons: FindObjectsByType returns them in no particular order, and the three scene
            // enemies walk toward the hero, so either of those can put something unintended in the
            // line of fire and turn a pass into luck.
            yield return FireIntoEmptySpace(motor);

            var combat = motor.GetComponent<PlayerCombat>();

            // Parked well away first, then moved into the line of fire immediately before the cast.
            // Aim cannot be predicted ahead of time here: it is derived from the cursor through the
            // camera, and the camera is still settling against the confiner, so the world direction
            // under a fixed screen point keeps changing for the first few frames.
            EnemySpawner spawner = FindSpawner();
            EnemyController enemy = spawner.Spawn(new Vector2(-30f, 1f));
            Assert.That(enemy, Is.Not.Null, "The pool handed out no target.");

            yield return Steps(8);
            HealthComponent target = enemy.Health;

            var sources = new List<DamageSource>();
            void OnDamage(DamageAppliedEvent evt) => sources.Add(evt.Result.Source);

            bus.Subscribe<DamageAppliedEvent>(OnDamage);
            try
            {
                float before = target.CurrentHealth;

                // Two units along the aim the hero has right now: close enough that the projectile
                // arrives in about nine physics steps, before anything can drift.
                enemy.transform.position =
                    (Vector2)motor.transform.position + combat.AimDirection * 2f;

                yield return null;

                skills.RequestCast(SkillSlot.Skill1);
                yield return WaitForRealSeconds(0.6f);

                Assert.That(target.CurrentHealth, Is.LessThan(before),
                    $"The projectile never connected. Hero at {motor.transform.position.x:F1}, " +
                    $"target at {target.transform.position.x:F1}.");

                // Health dropping with no event would mean a second damage path had been added
                // around the pipeline (HPS-003).
                Assert.That(sources, Does.Contain(DamageSource.Skill),
                    "Damage landed but no DamageAppliedEvent carried DamageSource.Skill, so it " +
                    "bypassed CombatSystem (HPS-003).");
            }
            finally
            {
                bus.Unsubscribe<DamageAppliedEvent>(OnDamage);
                spawner.DespawnAll();
            }
        }

        [UnityTest]
        public IEnumerator Test_SkillE_HitsAllEnemiesInRadius()
        {
            yield return BootIntoRun01();

            PlayerMotor motor = FindHeroMotor();
            var skills = motor.GetComponent<SkillSystem>();
            var area = motor.GetComponent<AoeSkill>();

            SkillData data = skills.GetSkill(SkillSlot.Skill2);
            Assert.That(data, Is.Not.Null, "E has no skill asset.");

            // Three dummies stand at x = 3, 8 and 12. Put the hero on the first one so exactly one
            // is inside the 2.5u radius and the others are far outside it.
            HealthComponent near = FindDummy();
            motor.Teleport(new Vector2(near.transform.position.x, motor.transform.position.y));
            yield return Steps(4);

            float before = near.CurrentHealth;

            skills.RequestCast(SkillSlot.Skill2);
            yield return WaitForRealSeconds(0.3f);

            Assert.That(area.LastRadius, Is.EqualTo(data.Radius).Within(0.001f),
                "The blast used a different radius from the asset (NFR-007).");
            Assert.That(area.LastHitCount, Is.GreaterThan(0), "The blast hit nothing at all.");
            Assert.That(near.CurrentHealth, Is.LessThan(before), "The dummy inside the radius was not hit.");
        }

        [UnityTest]
        public IEnumerator Test_Skill_RespectsCooldown()
        {
            yield return BootIntoRun01();

            PlayerMotor motor = FindHeroMotor();
            var skills = motor.GetComponent<SkillSystem>();
            SkillData data = skills.GetSkill(SkillSlot.Skill1);

            yield return AimAtScreenSide(0.9f);

            skills.RequestCast(SkillSlot.Skill1);
            yield return Steps(4);

            Assert.That(skills.GetCooldownRemaining(SkillSlot.Skill1), Is.GreaterThan(0f),
                "Casting must start the cooldown (COM-008).");
            Assert.That(skills.CanCast(SkillSlot.Skill1), Is.False,
                "A live cooldown must refuse the next cast. There is no resource cost in MVP, so " +
                "the cooldown is the only gate (COM-008).");

            yield return Steps(StepsFor(data.Cooldown + 0.2f));

            Assert.That(skills.CanCast(SkillSlot.Skill1), Is.True,
                $"After {data.Cooldown}s the skill should be available again.");
        }

        [UnityTest]
        public IEnumerator Test_Skill_CannotFireWhileDashing()
        {
            yield return BootIntoRun01();

            PlayerMotor motor = FindHeroMotor();
            var skills = motor.GetComponent<SkillSystem>();
            var dash = motor.GetComponent<PlayerDash>();

            yield return AimAtScreenSide(0.9f);

            dash.RequestDash();
            yield return Steps(3);
            Assert.That(motor.IsDashing, Is.True, "Setup failed: the dash never started.");

            Assert.That(skills.CanCast(SkillSlot.Skill1), Is.False,
                "A dash is a commitment. Letting a skill interrupt it would remove the cost that " +
                "makes dashing a decision.");

            skills.RequestCast(SkillSlot.Skill1);
            yield return Steps(2);

            Assert.That(skills.GetCooldownRemaining(SkillSlot.Skill1), Is.EqualTo(0f).Within(0.001f),
                "A cast went through mid-dash.");
        }

        [UnityTest]
        public IEnumerator Test_Skill_BufferedDuringHitStop()
        {
            yield return BootIntoRun01();

            PlayerMotor motor = FindHeroMotor();
            var skills = motor.GetComponent<SkillSystem>();
            var pause = ServiceLocator.Current.Get<IPauseService>();

            yield return AimAtScreenSide(0.9f);

            pause.RequestHitStop(0.06f);
            yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(0f).Within(0.001f), "Setup failed: time is not frozen.");

            // Without buffering this press reaches a frozen game and is simply gone, which the
            // player experiences as the button not working (P1-07).
            skills.RequestCast(SkillSlot.Skill1);

            yield return WaitForRealSeconds(0.3f);
            yield return Steps(4);

            Assert.That(skills.GetCooldownRemaining(SkillSlot.Skill1), Is.GreaterThan(0f),
                "The buffered press never became a cast once time resumed (P1-07).");
        }

        [UnityTest]
        public IEnumerator Test_SkillCooldownEvent_FiresWithCorrectSlotIndex()
        {
            yield return BootIntoRun01();

            PlayerMotor motor = FindHeroMotor();
            var skills = motor.GetComponent<SkillSystem>();
            var bus = ServiceLocator.Current.Get<EventBus>();

            var seen = new List<SkillCooldownChangedEvent>();
            void OnCooldown(SkillCooldownChangedEvent evt) => seen.Add(evt);

            bus.Subscribe<SkillCooldownChangedEvent>(OnCooldown);
            try
            {
                yield return AimAtScreenSide(0.9f);

                // E, not Q: a wrong-slot bug that always reported slot 0 would pass unnoticed.
                skills.RequestCast(SkillSlot.Skill2);
                yield return Steps(4);

                SkillData data = skills.GetSkill(SkillSlot.Skill2);

                Assert.That(seen, Is.Not.Empty,
                    "No SkillCooldownChangedEvent was published, so the HUD has nothing to show. " +
                    "This event has been in the contract since the foundation slice.");

                bool matched = seen.Exists(e =>
                    e.Slot == SkillSlot.Skill2 && Mathf.Approximately(e.TotalSeconds, data.Cooldown));

                Assert.That(matched, Is.True,
                    "Casting E published no event for slot Skill2 with its own cooldown total. " +
                    $"Slots seen: {string.Join(", ", seen.ConvertAll(e => e.Slot.ToString()))}");
            }
            finally
            {
                bus.Unsubscribe<SkillCooldownChangedEvent>(OnCooldown);
            }
        }

        // ----- AI-006, SRS 29: pooling -------------------------------------------------------

        [UnityTest]
        public IEnumerator Test_Pool_NoInstantiateAfterPrewarm()
        {
            yield return BootIntoRun01();

            EnemySpawner spawner = FindSpawner();

            int prewarm = spawner.PrewarmCount;
            int createdAfterPrewarm = spawner.TotalCreated;

            Assert.That(createdAfterPrewarm, Is.EqualTo(prewarm),
                $"The pool made {createdAfterPrewarm} instances at load but was asked for {prewarm}.");

            // Spawn and retire in batches that stay inside the prewarmed set. SRS 29's whole point
            // is that a wave costs no Instantiate at all.
            for (int batch = 0; batch < 3; batch++)
            {
                for (int i = 0; i < prewarm; i++) spawner.Spawn(new Vector2(i, 2f));

                yield return Steps(2);
                spawner.DespawnAll();
                yield return Steps(2);
            }

            Assert.That(spawner.TotalCreated, Is.EqualTo(createdAfterPrewarm),
                $"The pool created {spawner.TotalCreated - createdAfterPrewarm} more instances " +
                "during play. Prewarm exists so a wave never instantiates (SRS 29).");
        }

        [UnityTest]
        public IEnumerator Test_Pool_EnemyFullyResetOnReuse()
        {
            yield return BootIntoRun01();

            EnemySpawner spawner = FindSpawner();
            var combat = ServiceLocator.Current.Get<CombatSystem>();

            EnemyController first = spawner.Spawn(new Vector2(30f, 2f));
            Assert.That(first, Is.Not.Null, "The pool handed out nothing.");

            yield return Steps(4);

            // Leave it in the worst state a reused instance could inherit: dead, invulnerable,
            // moving, mid-swing, collider off.
            HealthComponent health = first.Health;
            first.GetComponent<EnemyMotor>().ApplyKnockback(Vector2.right, 8f, 1f);

            // Kill first, dirty the invulnerability flag second. The other order cannot work:
            // CombatSystem refuses a hit on an invulnerable target, which is the rule under test
            // elsewhere (HPS-005).
            combat.DealDamage(health, 9999f, 1f, 0f, DamageSource.BasicAttack,
                first.transform.position, Vector2.zero);

            yield return WaitForRealSeconds(0.3f);
            Assert.That(health.IsDead, Is.True, "Setup failed: the enemy survived.");

            health.BeginInvulnerability(5f);

            spawner.Despawn(first);
            yield return Steps(2);

            EnemyController reused = spawner.Spawn(new Vector2(32f, 2f));
            yield return Steps(2);

            Assert.That(reused, Is.SameAs(first), "The pool did not reuse the released instance.");

            // Each of these is a separate way a recycled enemy can come back wrong, and each only
            // shows up once the pool starts reusing — which is when the scene is busiest.
            Assert.That(reused.Health.IsDead, Is.False, "Reused enemy is still dead.");
            Assert.That(reused.Health.CurrentHealth, Is.EqualTo(reused.Health.MaxHealth).Within(0.001f),
                "Reused enemy did not come back at full health.");
            Assert.That(reused.Health.IsInvulnerable, Is.False, "Reused enemy is still invulnerable.");
            // Horizontal only. The instance is spawned clear of the arena floor, so its vertical
            // velocity is fresh gravity rather than anything left over; knockback is horizontal and
            // is the momentum that would actually survive a bad reset.
            Assert.That(Mathf.Abs(reused.GetComponent<EnemyMotor>().Velocity.x), Is.LessThan(0.5f),
                "Reused enemy is still carrying the knockback from its previous life.");
            Assert.That(reused.GetComponent<EnemyAI>().State, Is.EqualTo(EnemyLifecycleState.Idle),
                "Reused enemy came back in its old state rather than Idle.");
            Assert.That(reused.GetComponent<EnemyAttack>().IsAttacking, Is.False,
                "Reused enemy came back mid-swing.");
            Assert.That(reused.GetComponent<Collider2D>().enabled, Is.True,
                "Reused enemy came back with the collider its corpse had switched off.");

            spawner.DespawnAll();
        }

        [UnityTest]
        public IEnumerator Test_Pool_DoubleDespawnIsSafe()
        {
            yield return BootIntoRun01();

            EnemySpawner spawner = FindSpawner();

            EnemyController enemy = spawner.Spawn(new Vector2(30f, 2f));
            yield return Steps(2);

            Assert.That(spawner.Despawn(enemy), Is.True, "The first release should succeed.");
            Assert.That(spawner.Despawn(enemy), Is.False,
                "A second release of the same instance must be refused. Both the corpse timer and " +
                "an explicit clear can reach Despawn, and releasing twice hands the same object to " +
                "two callers.");

            // The real damage a double release does: the same instance handed out twice at once.
            EnemyController a = spawner.Spawn(new Vector2(30f, 2f));
            EnemyController b = spawner.Spawn(new Vector2(32f, 2f));

            Assert.That(a, Is.Not.SameAs(b),
                "The pool handed the same instance to two callers, which is what a double release " +
                "causes.");

            spawner.DespawnAll();
            yield return Steps(2);
        }

        [UnityTest]
        public IEnumerator Test_Pool_ProjectileFullyResetOnReuse()
        {
            yield return BootIntoRun01();

            PlayerMotor motor = FindHeroMotor();
            var skills = motor.GetComponent<SkillSystem>();
            var projectiles = motor.GetComponent<ProjectileSkill>();
            SkillData data = skills.GetSkill(SkillSlot.Skill1);

            // Away from the three scene enemies and aimed at empty arena, so the shot expires on
            // its lifetime instead of retiring the instant it spawns inside whatever walked up to
            // the hero while the test was setting up.
            yield return FireIntoEmptySpace(motor);

            skills.RequestCast(SkillSlot.Skill1);
            yield return Steps(2);

            Projectile first = projectiles.LastFired;
            Assert.That(first, Is.Not.Null, "Nothing was fired.");

            yield return WaitForRealSeconds(data.ProjectileLifetime + 0.3f);
            Assert.That(first.IsAlive, Is.False, "The projectile never expired.");

            yield return Steps(StepsFor(data.Cooldown + 0.2f));
            yield return FireIntoEmptySpace(motor);

            skills.RequestCast(SkillSlot.Skill1);
            yield return Steps(2);

            Projectile reused = projectiles.LastFired;
            Assert.That(reused, Is.SameAs(first), "The pool did not reuse the expired projectile.");

            Assert.That(reused.IsAlive, Is.True, "The reused projectile was not relaunched.");
            Assert.That(reused.DistanceTravelled, Is.LessThan(data.ProjectileRange * 0.5f),
                "The reused projectile kept the distance it had already travelled.");
            Assert.That(reused.LifetimeRemaining, Is.GreaterThan(0f),
                "The reused projectile came back with its previous lifetime already spent.");
            Assert.That(reused.Speed, Is.EqualTo(data.ProjectileSpeed).Within(0.001f),
                "The reused projectile did not take its speed from the asset again.");
        }

        // ----- NFR-001, NFR-002: profiler harness --------------------------------------------

        [UnityTest]
        public IEnumerator Test_Profiler_HarnessProducesCompleteReport()
        {
            yield return BootIntoRun01();

            var harness = Object.FindFirstObjectByType<FrameTimeHarness>();
            Assert.That(harness, Is.Not.Null, $"{RunScene} has no FrameTimeHarness.");

            yield return harness.Run();

            FrameTimeReport report = harness.LastReport;
            Assert.That(report, Is.Not.Null, "The harness produced no report.");

            // Deliberately no threshold assertion. This is a headless run with no renderer, so its
            // frame times are not a player's frame times; asserting an FPS figure here would be a
            // number that means nothing. What is checked is that the harness measured something and
            // filled in every field a reader needs.
            Assert.That(report.FrameCount, Is.GreaterThan(0), "No frames were sampled.");
            Assert.That(report.EnemyCount, Is.GreaterThan(0), "No enemies were spawned to measure against.");
            Assert.That(report.DurationSeconds, Is.GreaterThan(0f), "The sample had no duration.");
            Assert.That(report.MeanMs, Is.GreaterThan(0f), "Mean frame time is zero.");
            Assert.That(report.MedianMs, Is.GreaterThan(0f), "Median is missing.");
            Assert.That(report.P95Ms, Is.GreaterThanOrEqualTo(report.MedianMs), "p95 is below the median.");
            Assert.That(report.P99Ms, Is.GreaterThanOrEqualTo(report.P95Ms), "p99 is below p95.");
            Assert.That(report.MaxMs, Is.GreaterThanOrEqualTo(report.P99Ms), "The maximum is below p99.");
            Assert.That(report.MeanFps, Is.GreaterThan(0f), "Mean FPS was not derived.");
            Assert.That(report.OnePercentLowFps, Is.GreaterThan(0f), "The 1% low was not derived.");

            Assert.That(report.Caveats, Is.Not.Null.And.Not.Empty,
                "The report must carry its own caveats, so nobody reads the numbers without " +
                "seeing what they exclude.");
            Assert.That(report.Caveats, Does.Contain("skill"),
                "The caveat must say that no skill is cast, since the ultimate's cooldown is " +
                "longer than the sample and its area sweep is therefore unmeasured.");

            Assert.That(File.Exists(harness.LastReportPath), Is.True,
                $"No report file at {harness.LastReportPath}.");
        }

        // ----- helpers ---------------------------------------------------------------------

        /// <summary>
        /// Puts the hero in the empty left half of the arena, aimed further left. Scene enemies
        /// spawn at x = 5, 10 and 15 and walk toward the hero, so a shot fired from where they are
        /// standing retires on its first step and tells you nothing about its flight.
        /// </summary>
        private IEnumerator FireIntoEmptySpace(PlayerMotor motor)
        {
            // x = -15 is solid floor: the gap in the ground is 3u wide at x = -8.
            motor.Teleport(new Vector2(-15f, motor.transform.position.y));
            yield return AimAtScreenSide(0.1f);
            yield return Steps(2);
        }

        private static EnemySpawner FindSpawner()
        {
            var spawner = Object.FindFirstObjectByType<EnemySpawner>();
            Assert.That(spawner, Is.Not.Null, $"{RunScene} has no EnemySpawner.");
            return spawner;
        }

        /// <summary>Half-width of the arena, matching the boundary walls RunSceneBuilder places.</summary>
        private const float ArenaHalfWidth = 20f;

        /// <summary>
        /// Real seconds to wait after landing a hit. Longer than the longest freeze (0.14s on a
        /// kill), so time has certainly resumed before the next assertion or fixed-step wait.
        /// </summary>
        private const float HitStopSettle = 0.25f;

        private static HeroData HeroDataOf(PlayerMotor motor)
        {
            var stats = motor.GetComponent<PlayerStats>();
            Assert.That(stats, Is.Not.Null, "Hero has no PlayerStats.");
            Assert.That(stats.Hero, Is.Not.Null, "PlayerStats has no HeroData.");
            return stats.Hero;
        }

        private static BalanceConfig BalanceOf(PlayerMotor motor)
        {
            var stats = motor.GetComponent<PlayerStats>();
            Assert.That(stats.Balance, Is.Not.Null, "PlayerStats has no BalanceConfig.");
            return stats.Balance;
        }

        /// <summary>
        /// Points the cursor at one side of the screen and lets it take effect.
        /// </summary>
        /// <remarks>
        /// It has to be the real mouse. Facing follows the cursor from slice 2 on and the dash
        /// falls back to facing, but <c>PlayerController.Update</c> re-applies the pointer position
        /// every frame — so setting the aim directly is overwritten before the next fixed step, and
        /// the dash goes wherever the untouched cursor happens to point. In this scene that is
        /// leftward, into the hole at x = -8.
        /// </remarks>
        private IEnumerator AimAtScreenSide(float normalisedX)
        {
            Set(_mouse.position, new Vector2(Screen.width * normalisedX, Screen.height * 0.5f));
            yield return null;
            yield return null;
        }

        /// <summary>
        /// Runs until the dash window closes, then returns. Distance has to be measured here:
        /// after the dash the motor decelerates from 20 u/s at 80 u/s^2 and keeps travelling, so a
        /// fixed number of extra steps would fold that glide into the measurement.
        /// </summary>
        private static IEnumerator WaitForDashToEnd(PlayerMotor motor)
        {
            int budget = StepsFor(2f);
            for (int i = 0; i < budget && motor.IsDashing; i++) yield return TestTime.Steps(1);

            Assert.That(motor.IsDashing, Is.False, "The dash never ended.");
        }

        /// <summary>A dummy to hit: it has plenty of health and never fights back.</summary>
        private static HealthComponent FindDummy()
        {
            HealthComponent[] all = Object.FindObjectsByType<HealthComponent>(FindObjectsSortMode.None);

            foreach (HealthComponent health in all)
            {
                if (health.IsPlayer || health.IsDead) continue;
                if (!health.name.StartsWith("Dummy")) continue;
                return health;
            }

            Assert.Fail($"{RunScene} contains no training dummy.");
            return null;
        }

        private static IEnumerator WaitForRealSeconds(float seconds) => TestTime.RealSeconds(seconds);

        private static PlayerMotor FindHeroMotor()
        {
            var motor = Object.FindFirstObjectByType<PlayerMotor>();
            Assert.That(motor, Is.Not.Null, $"{RunScene} contains no PlayerMotor.");
            return motor;
        }

        private static EnemyAI NearestEnemy(Vector2 to)
        {
            EnemyAI[] enemies = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
            Assert.That(enemies, Is.Not.Empty, $"{RunScene} contains no EnemyAI.");

            EnemyAI best = enemies[0];
            float bestDistance = float.MaxValue;

            foreach (EnemyAI enemy in enemies)
            {
                float distance = Vector2.Distance(enemy.transform.position, to);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = enemy;
            }

            return best;
        }

        private static IEnumerator Steps(int count) => TestTime.Steps(count);

        private static int StepsFor(float seconds) => TestTime.StepsFor(seconds);
    }
}
