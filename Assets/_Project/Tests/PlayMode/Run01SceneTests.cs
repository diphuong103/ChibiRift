using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ChibiRift.Core;
using ChibiRift.Data;
using ChibiRift.Gameplay;

namespace ChibiRift.Tests.Play
{
    /// <summary>
    /// Plays the real Run_01 scene, with the prefabs that actually ship in it.
    /// </summary>
    /// <remarks>
    /// <para><b>Why this is different from every other PlayMode fixture.</b> The others build their
    /// actors in code: a fresh GameObject, a default Transform, components wired by reflection.
    /// That is deliberate — it keeps them independent of scene layout. But it also means they never
    /// touch <c>Hero.prefab</c>, so a fault that lives in the prefab rather than in the code is
    /// invisible to all of them. The hero was permanently airborne in Run_01 while 162 tests were
    /// green, because the prefab carried Transform scale (1, 2, 1) and the ground probe offset is
    /// measured in unscaled units.</para>
    ///
    /// <para>This fixture exists to cover exactly that gap and nothing else. Keep it small: the
    /// scene is slow to load and brittle to assert against in detail.</para>
    /// </remarks>
    public sealed class Run01SceneTests
    {
        private const string SceneName = "Run_01";

        /// <summary>Long enough for gravity to settle the hero and for a fall to be obvious.</summary>
        private const float SettleSeconds = 0.5f;

        private ServiceLocator _locator;
        private BalanceConfig _balance;
        private Scene _scene;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Run_01 is not the composition root, so stand in for Boot. Registered before the load
            // so the scene's own Start methods find the services they expect.
            _locator = new ServiceLocator();
            _balance = ScriptableObject.CreateInstance<BalanceConfig>();

            var bus = new EventBus();
            _locator.Register(bus);
            _locator.Register<IInputService>(new FakeInputService());
            _locator.Register(new CombatSystem(bus, _balance, new DeterministicRandom(1)));
            ServiceLocator.SetCurrent(_locator);

            // The scene carries navigation and camera components that expect services this stand-in
            // root does not provide, and they log about it. Their complaints are not what is under
            // test here.
            LogAssert.ignoreFailingMessages = true;

            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            _scene = SceneManager.GetActiveScene();

            Assert.That(_scene.name, Is.EqualTo(SceneName), $"Failed to load {SceneName}.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = false;

            if (_balance != null) Object.DestroyImmediate(_balance);

            _locator.Clear();
            ServiceLocator.ClearCurrent();

            yield return null;
        }

        [UnityTest]
        public IEnumerator Test_Hero_IsGroundedAfterSpawning()
        {
            PlayerMotor motor = FindHeroMotor();

            yield return Steps(StepsFor(SettleSeconds));

            Assert.That(motor.IsGrounded, Is.True,
                $"Hero is at {motor.transform.position} with velocity {motor.Velocity} and reports " +
                "IsGrounded false. Gravity is being cancelled by a collider, so it is standing on " +
                "the floor while the ground probe cannot see it.");

            // The give-away symptom: gravity added every step and zeroed by the collider every
            // step, leaving exactly one step of fall speed on the clock.
            Assert.That(motor.Velocity.y, Is.EqualTo(0f).Within(0.01f),
                $"Vertical velocity settled at {motor.Velocity.y}, not 0. A value of " +
                $"-{40f * Time.fixedDeltaTime:F3} means one step of gravity is being re-applied " +
                "every step because the motor does not know it is grounded.");
        }

        [UnityTest]
        public IEnumerator Test_Hero_CanJumpFromTheGround()
        {
            PlayerMotor motor = FindHeroMotor();

            yield return Steps(StepsFor(SettleSeconds));
            Assert.That(motor.IsGrounded, Is.True, "Setup failed: hero never landed.");

            float restingY = motor.transform.position.y;
            motor.RequestJump();

            yield return Steps(StepsFor(0.2f));

            Assert.That(motor.transform.position.y, Is.GreaterThan(restingY + 0.5f),
                "Hero did not leave the ground after a jump request. This is what the player sees " +
                "as Space doing nothing.");
        }

        [UnityTest]
        public IEnumerator Test_Enemies_AreGroundedAfterSpawning()
        {
            yield return Steps(StepsFor(SettleSeconds));

            EnemyMotor[] enemies = Object.FindObjectsByType<EnemyMotor>(FindObjectsSortMode.None);

            Assert.That(enemies.Length, Is.EqualTo(3), $"Expected 3 enemies in {SceneName}.");

            foreach (EnemyMotor enemy in enemies)
            {
                Assert.That(enemy.IsGrounded, Is.True,
                    $"{enemy.name} at {enemy.transform.position} is not grounded. Enemies share the " +
                    "hero's ground-probe arrangement, so they share its failure modes.");
            }
        }

        private static PlayerMotor FindHeroMotor()
        {
            var motor = Object.FindFirstObjectByType<PlayerMotor>();
            Assert.That(motor, Is.Not.Null, $"{SceneName} contains no PlayerMotor.");
            return motor;
        }

        private static IEnumerator Steps(int count)
        {
            for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate();
        }

        private static int StepsFor(float seconds) => Mathf.CeilToInt(seconds / Time.fixedDeltaTime) + 1;
    }
}
