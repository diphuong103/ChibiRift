using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ChibiRift.Core;
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
    /// </remarks>
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

        // ----- helpers ---------------------------------------------------------------------

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

        private static IEnumerator Steps(int count)
        {
            for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate();
        }

        private static int StepsFor(float seconds) => Mathf.CeilToInt(seconds / Time.fixedDeltaTime) + 1;
    }
}
