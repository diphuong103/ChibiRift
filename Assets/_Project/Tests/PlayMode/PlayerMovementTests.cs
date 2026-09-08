using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChibiRift.Core;
using ChibiRift.Data;
using ChibiRift.Gameplay;

namespace ChibiRift.Tests.Play
{
    /// <summary>
    /// TC-MOV: the movement half of P1-32 (MOV-001 through MOV-004).
    /// </summary>
    /// <remarks>
    /// The arena is built in code rather than by loading Run_01, so a change to that scene's
    /// layout or placeholder art cannot turn these red. Private serialized fields are set by
    /// reflection, not <c>SerializedObject</c>, so this assembly stays free of UnityEditor and can
    /// also run in a player build.
    /// </remarks>
    public sealed class PlayerMovementTests
    {
        private const float SpawnY = 1f;
        private const float FallLimitY = -10f;
        private const float ArenaHalfWidth = 20f;
        private const float GroundSurfaceY = 0f;

        /// <summary>Hero centre height when standing: ground surface plus half the capsule.</summary>
        private const float RestingY = 0.9f;

        /// <summary>How long before touchdown the buffered jump is pressed. Spec says 0.10s.</summary>
        private const float PressLeadTime = 0.10f;

        private readonly List<GameObject> _spawned = new List<GameObject>();

        private ServiceLocator _locator;
        private FakeInputService _input;
        private HeroData _hero;
        private PlayerMotor _motor;
        private GameObject _heroObject;
        private GameObject _ground;

        private MovementConfig Config => _hero.Movement;
        private float MoveSpeed => _hero.BaseStats.MoveSpeed;
        private float HeroY => _heroObject.transform.position.y;

        [SetUp]
        public void SetUp()
        {
            _locator = new ServiceLocator();
            _input = new FakeInputService();
            _locator.Register(new EventBus());
            _locator.Register<IInputService>(_input);
            ServiceLocator.SetCurrent(_locator);

            _hero = ScriptableObject.CreateInstance<HeroData>();

            _ground = Track(CreateSolid("Ground", new Vector2(0f, -0.5f), new Vector2(40f, 1f), GameLayers.Ground));
            Track(CreateSolid("Wall_Right", new Vector2(ArenaHalfWidth, 0f), new Vector2(1f, 20f), GameLayers.Boundary));

            GameObject spawn = Track(new GameObject("SpawnPoint"));
            spawn.transform.position = new Vector3(0f, SpawnY, 0f);

            GameObject contextObject = Track(new GameObject("SceneContext"));
            var context = contextObject.AddComponent<SceneContext>();
            SetField(context, "_worldHalfWidth", ArenaHalfWidth);
            SetField(context, "_fallLimitY", FallLimitY);
            SetField(context, "_spawnPoint", spawn.transform);

            _heroObject = Track(new GameObject("Hero") { layer = LayerMask.NameToLayer(GameLayers.Player) });
            _heroObject.transform.position = new Vector3(0f, SpawnY, 0f);

            // Awake fires the moment a component is added to an ACTIVE object, which would run
            // PlayerMotor before its HeroData is injected and make it log a missing-data error.
            // Building the hero inactive defers every Awake to the SetActive below.
            _heroObject.SetActive(false);

            var capsule = _heroObject.AddComponent<CapsuleCollider2D>();
            capsule.direction = CapsuleDirection2D.Vertical;
            capsule.size = new Vector2(0.8f, 1.8f);

            _motor = _heroObject.AddComponent<PlayerMotor>();
            SetField(_motor, "_heroData", _hero);
            SetField(_motor, "_sceneContext", context);
            _heroObject.AddComponent<PlayerController>();

            _heroObject.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            // Only what this fixture created; never a blanket scene wipe.
            foreach (GameObject spawned in _spawned)
            {
                if (spawned != null) Object.DestroyImmediate(spawned);
            }
            _spawned.Clear();

            if (_hero != null) Object.DestroyImmediate(_hero);

            _locator.Clear();
            ServiceLocator.ClearCurrent();
        }

        [UnityTest]
        public IEnumerator Test_MoveRight_VelocityConvergesToMoveSpeed()
        {
            _input.MoveAxis = 1f;
            yield return RunSeconds(1f);

            Assert.That(Mathf.Abs(_motor.Velocity.x - MoveSpeed), Is.LessThan(0.15f),
                $"Expected about {MoveSpeed}, measured {_motor.Velocity.x:F3}.");
        }

        [UnityTest]
        public IEnumerator Test_ReleaseInput_StopsWithin150ms()
        {
            _input.MoveAxis = 1f;
            yield return RunSeconds(1f);

            _input.MoveAxis = 0f;
            yield return RunSeconds(0.15f);

            Assert.That(Mathf.Abs(_motor.Velocity.x), Is.LessThan(0.1f),
                $"Still drifting at {_motor.Velocity.x:F3} after 150 ms.");
        }

        [UnityTest]
        public IEnumerator Test_Jump_PeakHeightInRange()
        {
            yield return SettleOnGround();

            float startY = HeroY;
            _input.PressJump();          // jump stays held, so no low-jump cut applies
            yield return new WaitForFixedUpdate();

            float peak = startY;
            while (_motor.Velocity.y > 0f)
            {
                yield return new WaitForFixedUpdate();
                peak = Mathf.Max(peak, HeroY);
            }

            float height = peak - startY;
            float analytic = Config.JumpVelocity * Config.JumpVelocity / (2f * Config.GravityUp);

            Assert.That(height, Is.InRange(2.9f, 3.1f),
                $"Peak {height:F3}u; JumpVelocity^2 / (2*GravityUp) = {analytic:F3}u.");
        }

        [UnityTest]
        public IEnumerator Test_DoubleJump_OnlyOnce()
        {
            yield return SettleOnGround();

            _input.PressJump();
            yield return RunSeconds(0.2f);
            Assert.That(_motor.JumpCount, Is.EqualTo(1), "First jump should set jumpCount to 1.");

            _input.PressJump();
            yield return RunSeconds(0.2f);
            Assert.That(_motor.JumpCount, Is.EqualTo(2), "Second jump should set jumpCount to 2.");

            float before = _motor.Velocity.y;
            _input.PressJump();
            yield return new WaitForFixedUpdate();

            Assert.That(_motor.JumpCount, Is.EqualTo(2), "A third jump must be refused (MOV-003).");
            Assert.That(_motor.Velocity.y, Is.LessThan(before + 0.01f),
                "A third jump must not add upward velocity.");
        }

        [UnityTest]
        public IEnumerator Test_CoyoteTime_JumpAfterEdge()
        {
            yield return SettleOnGround();

            // Removing the floor is the deterministic equivalent of stepping off its edge.
            Object.DestroyImmediate(_ground);
            _spawned.Remove(_ground);

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(_motor.IsGrounded, Is.False, "Should be airborne once the floor is gone.");
            Assert.That(_motor.CoyoteTimer, Is.GreaterThan(0f),
                $"Coyote window should still be open within {Config.CoyoteTime}s.");

            _input.PressJump();
            yield return new WaitForFixedUpdate();

            Assert.That(_motor.Velocity.y, Is.GreaterThan(0f),
                "A jump inside the coyote window must still launch.");
            Assert.That(_motor.JumpCount, Is.EqualTo(1), "It counts as the ground jump, not the air jump.");
        }

        [UnityTest]
        public IEnumerator Test_JumpBuffer_LandAndJump()
        {
            yield return SettleOnGround();

            // Spend both jumps so a buffered press cannot be consumed in mid-air.
            _input.PressJump();
            yield return RunSeconds(0.2f);
            _input.PressJump();
            yield return RunSeconds(0.2f);
            Assert.That(_motor.JumpCount, Is.EqualTo(2), "Both jumps should be spent.");

            // Fall until touchdown is roughly PressLeadTime away, matching "press just before
            // landing". Pressing on a fixed height instead would be fragile: the fall speed at
            // that height depends on the two jumps above it.
            while (true)
            {
                yield return new WaitForFixedUpdate();
                if (_motor.IsGrounded) Assert.Fail("Landed before the buffered press was issued.");

                float fallSpeed = -_motor.Velocity.y;
                if (fallSpeed <= 0f) continue;

                float timeToLand = (HeroY - RestingY) / fallSpeed;
                if (timeToLand <= PressLeadTime) break;
            }

            _input.PressJump();

            // PlayerController samples input in Update, so the press only reaches the motor's
            // buffer on the next frame; asserting immediately would read a stale zero.
            yield return null;

            Assert.That(_motor.JumpBufferTimer, Is.GreaterThan(0f), "Press should be buffered, not dropped.");
            Assert.That(_motor.JumpCount, Is.EqualTo(2), "It must not fire while still airborne.");

            bool jumpedOnLanding = false;
            for (float t = 0f; t < 0.5f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                if (_motor.JumpCount == 1 && _motor.Velocity.y > 0f)
                {
                    jumpedOnLanding = true;
                    break;
                }
            }

            Assert.That(jumpedOnLanding, Is.True,
                $"The buffered press should fire on touchdown within {Config.JumpBuffer}s.");
        }

        [UnityTest]
        public IEnumerator Test_WallCollision_NoPassThrough()
        {
            yield return SettleOnGround();

            _input.MoveAxis = 1f;
            yield return RunSeconds(5f);

            Assert.That(Mathf.Abs(_motor.Velocity.x), Is.LessThan(0.5f),
                $"Should be stopped against the wall, still moving at {_motor.Velocity.x:F3}.");
            Assert.That(_heroObject.transform.position.x, Is.LessThanOrEqualTo(ArenaHalfWidth),
                "Hero must never cross the boundary (MOV-004).");
        }

        [UnityTest]
        public IEnumerator Test_FallThroughHole_Respawn()
        {
            yield return SettleOnGround();

            // Removing the floor stands in for walking into the 3u hole in Run_01.
            Object.DestroyImmediate(_ground);
            _spawned.Remove(_ground);

            bool fellPastLimit = false;
            bool respawned = false;

            for (float t = 0f; t < 5f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();

                if (HeroY < FallLimitY) fellPastLimit = true;
                if (fellPastLimit && Mathf.Abs(HeroY - SpawnY) < 0.5f)
                {
                    respawned = true;
                    break;
                }
            }

            Assert.That(fellPastLimit, Is.True, $"Hero should have fallen past y={FallLimitY}.");
            Assert.That(respawned, Is.True,
                $"Should respawn at y={SpawnY}; ended at {_heroObject.transform.position}.");
        }

        private IEnumerator SettleOnGround()
        {
            for (int i = 0; i < 30 && !_motor.IsGrounded; i++) yield return new WaitForFixedUpdate();
            Assert.That(_motor.IsGrounded, Is.True, "Hero failed to settle on the ground.");
        }

        private static IEnumerator RunSeconds(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.fixedDeltaTime) yield return new WaitForFixedUpdate();
        }

        private GameObject Track(GameObject spawned)
        {
            _spawned.Add(spawned);
            return spawned;
        }

        private static GameObject CreateSolid(string name, Vector2 centre, Vector2 size, string layer)
        {
            var solid = new GameObject(name) { layer = LayerMask.NameToLayer(layer) };
            solid.transform.position = centre;
            solid.AddComponent<BoxCollider2D>().size = size;
            return solid;
        }

        /// <summary>
        /// Assigns a private serialized field. Reflection rather than SerializedObject, so this
        /// assembly needs no UnityEditor reference.
        /// </summary>
        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"{target.GetType().Name} has no field '{fieldName}'.");
            field.SetValue(target, value);
        }
    }
}
