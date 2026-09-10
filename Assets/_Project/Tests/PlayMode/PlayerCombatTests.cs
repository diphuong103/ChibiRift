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
    /// TC-COM: the combat half of P1 slice 2 (COM-001 to COM-004, COM-009, HPS-003, HPS-004,
    /// HPS-006, HPS-007).
    /// </summary>
    /// <remarks>
    /// Same approach as <see cref="PlayerMovementTests"/>: the arena is built in code so a change
    /// to Run_01 cannot turn these red, private serialized fields are set by reflection so the
    /// assembly needs no UnityEditor reference, and every created object is tracked and destroyed
    /// rather than wiping the scene.
    /// </remarks>
    public sealed class PlayerCombatTests
    {
        private const float ArenaHalfWidth = 20f;
        private const float SpawnY = 1f;

        /// <summary>Hero attack stat. Chosen round so expected damage is obvious in a failure.</summary>
        private const float HeroAttack = 10f;

        private const float DummyHealth = 500f;

        /// <summary>Distance from hero to dummy. Inside the 0.8 offset plus half of the 1.2 box.</summary>
        private const float DummyDistance = 1f;

        private readonly List<GameObject> _spawned = new List<GameObject>();

        private ServiceLocator _locator;
        private EventBus _eventBus;
        private FakeInputService _input;
        private HeroData _hero;
        private AttackData _attack;
        private BalanceConfig _balance;

        private GameObject _heroObject;
        private PlayerCombat _combat;
        private PlayerMotor _motor;
        private HealthComponent _dummy;

        private AttackStep Step(int index) => _attack.GetStep(index);

        [SetUp]
        public void SetUp()
        {
            _locator = new ServiceLocator();
            _eventBus = new EventBus();
            _input = new FakeInputService();

            // Level with the hero, not at the world origin: the default (1,0) would aim diagonally
            // down from a hero standing at y=1 and swing the hitbox past the dummy.
            _input.AimAt(new Vector2(10f, SpawnY));

            _balance = ScriptableObject.CreateInstance<BalanceConfig>();
            _hero = ScriptableObject.CreateInstance<HeroData>();
            _attack = ScriptableObject.CreateInstance<AttackData>();
            SetField(_attack, "_steps", AttackData.BaselineSteps());
            SetField(_hero, "_basicAttack", _attack);

            StatBlock stats = _hero.BaseStats;
            stats.Attack = HeroAttack;
            SetField(_hero, "_baseStats", stats);

            _locator.Register(_eventBus);
            _locator.Register<IInputService>(_input);
            _locator.Register(new CombatSystem(_eventBus, _balance, new DeterministicRandom(1)));
            ServiceLocator.SetCurrent(_locator);

            Track(CreateSolid("Ground", new Vector2(0f, -0.5f), new Vector2(40f, 1f), GameLayers.Ground));

            GameObject spawn = Track(new GameObject("SpawnPoint"));
            spawn.transform.position = new Vector3(0f, SpawnY, 0f);

            GameObject contextObject = Track(new GameObject("SceneContext"));
            var context = contextObject.AddComponent<SceneContext>();
            SetField(context, "_worldHalfWidth", ArenaHalfWidth);
            SetField(context, "_fallLimitY", -10f);
            SetField(context, "_spawnPoint", spawn.transform);

            BuildHero(context);
            _dummy = BuildDummy("Dummy_Right", new Vector2(DummyDistance, SpawnY), DummyHealth);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject spawned in _spawned)
            {
                if (spawned != null) Object.DestroyImmediate(spawned);
            }
            _spawned.Clear();

            if (_hero != null) Object.DestroyImmediate(_hero);
            if (_attack != null) Object.DestroyImmediate(_attack);
            if (_balance != null) Object.DestroyImmediate(_balance);

            _locator.Clear();
            ServiceLocator.ClearCurrent();
        }

        // ----- COM-004: the active window -------------------------------------------------

        [UnityTest]
        public IEnumerator Test_Attack_HitboxOnlyActiveInWindow()
        {
            AttackStep step = Step(0);

            _combat.RequestAttack();

            var samples = new List<(float Elapsed, bool Active)>();
            for (float t = 0f; t <= step.TotalDuration; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                samples.Add((t + Time.fixedDeltaTime, _combat.IsHitboxActive));
            }

            // One fixed step of slack on each edge: the window is sampled at discrete times, so a
            // sample landing exactly on a boundary may fall either side of it.
            float slack = Time.fixedDeltaTime;

            foreach ((float elapsed, bool active) in samples)
            {
                if (elapsed < step.ActiveStartTime - slack)
                {
                    Assert.That(active, Is.False,
                        $"Hitbox was active at {elapsed:F3}s, before the window opens at {step.ActiveStartTime}s.");
                }
                else if (elapsed > step.ActiveEndTime + slack)
                {
                    Assert.That(active, Is.False,
                        $"Hitbox was still active at {elapsed:F3}s, after the window closes at {step.ActiveEndTime}s.");
                }
            }

            Assert.That(samples.Exists(s => s.Active), Is.True,
                "The hitbox never opened at all, so the window assertions proved nothing.");
        }

        [UnityTest]
        public IEnumerator Test_Attack_SingleTargetHitOnce()
        {
            AttackStep step = Step(0);
            float before = _dummy.CurrentHealth;

            _combat.RequestAttack();
            yield return RunSeconds(step.TotalDuration);

            float dealt = before - _dummy.CurrentHealth;

            // The window spans several fixed steps, so a per-swing hit set is the only thing that
            // can hold this to one hit. Expected: Attack 10 x multiplier 1.0, no armour.
            Assert.That(dealt, Is.EqualTo(HeroAttack * step.DamageMultiplier).Within(0.001f),
                $"One swing dealt {dealt}, which is not a single hit of {HeroAttack * step.DamageMultiplier}.");
        }

        // ----- COM-002 and COM-003: the chain ---------------------------------------------

        [UnityTest]
        public IEnumerator Test_Combo_ThreeHitsInWindow()
        {
            for (int i = 0; i < 3; i++)
            {
                _combat.RequestAttack();
                Assert.That(_combat.ComboStep, Is.EqualTo(i + 1),
                    $"Press {i + 1} should have advanced the chain to step {i + 1}.");

                yield return RunSeconds(Step(i).TotalDuration);
            }

            // COM-002: the third hit ends the chain, so the window must not be left open.
            Assert.That(_combat.ComboStep, Is.EqualTo(0),
                "The chain should return to Idle after the third hit.");
            Assert.That(_combat.ComboWindowRemaining, Is.EqualTo(0f).Within(0.0001f),
                "No combo window should remain after the final hit.");
        }

        [UnityTest]
        public IEnumerator Test_Combo_ResetsAfterWindowExpires()
        {
            _combat.RequestAttack();
            yield return RunSeconds(Step(0).TotalDuration);

            Assert.That(_combat.ComboStep, Is.EqualTo(1), "The first swing should have landed on step 1.");
            Assert.That(_combat.ComboWindowRemaining, Is.GreaterThan(0f), "A window should be open after step 1.");

            // Wait out the window, plus a margin so the Update that closes it has certainly run.
            yield return RunSeconds(_hero.ComboWindow + 0.1f);

            Assert.That(_combat.ComboStep, Is.EqualTo(0),
                $"The chain should reset {_hero.ComboWindow}s after the swing ends (COM-003).");

            _combat.RequestAttack();
            Assert.That(_combat.ComboStep, Is.EqualTo(1),
                "A press after the window expired should start a new chain at step 1, not step 2.");
        }

        [UnityTest]
        public IEnumerator Test_Combo_ResetsWhenLeavingGround()
        {
            yield return SettleOnGround();

            _combat.RequestAttack();
            yield return RunSeconds(Step(0).TotalDuration);
            Assert.That(_combat.ComboStep, Is.EqualTo(1), "The first swing should have landed on step 1.");

            _motor.RequestJump();

            // The jump is consumed in FixedUpdate, so wait on physics steps: in batch mode frames
            // are far shorter than the fixed timestep and 30 of them can pass without one.
            for (int i = 0; i < 30 && _motor.IsGrounded; i++) yield return new WaitForFixedUpdate();

            // The combo reset itself is detected in Update, so let one run after the transition.
            yield return null;

            Assert.That(_motor.IsGrounded, Is.False, "The hero never left the ground, so nothing was tested.");
            Assert.That(_combat.ComboStep, Is.EqualTo(0),
                "Leaving the ground should drop the chain (COM-003).");
        }

        // ----- COM-009: mouse aim ----------------------------------------------------------

        [UnityTest]
        public IEnumerator Test_MouseAim_HitboxFollowsCursorDirection()
        {
            HealthComponent left = BuildDummy("Dummy_Left", new Vector2(-DummyDistance, SpawnY), DummyHealth);

            // Cursor to the left: only the left dummy is inside the box.
            _input.AimAt(new Vector2(-10f, SpawnY));
            yield return null;

            float rightBefore = _dummy.CurrentHealth;
            float leftBefore = left.CurrentHealth;

            _combat.RequestAttack();
            yield return RunSeconds(Step(0).TotalDuration);

            Assert.That(left.CurrentHealth, Is.LessThan(leftBefore),
                "Aiming left should have hit the left dummy (COM-009).");
            Assert.That(_dummy.CurrentHealth, Is.EqualTo(rightBefore).Within(0.001f),
                "Aiming left must not hit the dummy on the right; that would be auto-targeting.");
        }

        [UnityTest]
        public IEnumerator Test_MouseAim_SpriteFlipsCorrectly()
        {
            var renderer = _heroObject.GetComponent<SpriteRenderer>();

            _input.AimAt(new Vector2(-10f, SpawnY));
            yield return null;
            Assert.That(renderer.flipX, Is.True, "Cursor to the left should flip the sprite (COM-009).");

            _input.AimAt(new Vector2(10f, SpawnY));
            yield return null;
            Assert.That(renderer.flipX, Is.False, "Cursor to the right should clear the flip (COM-009).");
        }

        // ----- HPS-003, HPS-004, HPS-006, HPS-007 ------------------------------------------

        [UnityTest]
        public IEnumerator Test_Damage_DeadTargetTakesNoMoreDamage()
        {
            HealthComponent fragile = BuildDummy("Dummy_Fragile", new Vector2(DummyDistance, SpawnY), 1f);
            Object.DestroyImmediate(_dummy.gameObject);

            var combatSystem = ServiceLocator.Current.Get<CombatSystem>();

            combatSystem.DealDamage(
                    fragile, HeroAttack, 1f, 0f, DamageSource.BasicAttack, Vector2.zero, Vector2.zero);
            yield return null;

            Assert.That(fragile.IsDead, Is.True, "A 1 HP dummy should have died to a 10 damage hit.");

            // HPS-004: a corpse takes nothing further, however the hit arrives.
            combatSystem.DealDamage(
                    fragile, HeroAttack, 1f, 0f, DamageSource.BasicAttack, Vector2.zero, Vector2.zero);
            Assert.That(fragile.CurrentHealth, Is.EqualTo(0f).Within(0.0001f),
                "Health moved below zero after death (HPS-004).");
        }

        [UnityTest]
        public IEnumerator Test_Death_FiresOnEntityDiedOnce()
        {
            HealthComponent fragile = BuildDummy("Dummy_Fragile", new Vector2(DummyDistance, SpawnY), 1f);
            Object.DestroyImmediate(_dummy.gameObject);

            // Start has not run on a component activated this frame, so it has no EventBus yet and
            // would publish nothing. One frame is enough.
            yield return null;

            int deaths = 0;
            void OnDied(EntityDiedEvent evt) => deaths++;
            _eventBus.Subscribe<EntityDiedEvent>(OnDied);

            try
            {
                var combatSystem = ServiceLocator.Current.Get<CombatSystem>();

                // Three lethal hits. SRS 34 needs the death to count once, or XP and rewards
                // would be granted once per hit that lands on the frame of death.
                for (int i = 0; i < 3; i++)
                {
                    combatSystem.DealDamage(
                    fragile, HeroAttack, 1f, 0f, DamageSource.BasicAttack, Vector2.zero, Vector2.zero);
                }

                yield return null;

                Assert.That(deaths, Is.EqualTo(1),
                    $"EntityDiedEvent fired {deaths} times for one death (HPS-007, SRS 34).");
            }
            finally
            {
                _eventBus.Unsubscribe<EntityDiedEvent>(OnDied);
            }
        }

        [UnityTest]
        public IEnumerator Test_Attack3_DealsHighestDamage()
        {
            var dealt = new float[3];

            for (int i = 0; i < 3; i++)
            {
                float before = _dummy.CurrentHealth;

                _combat.RequestAttack();
                Assert.That(_combat.ComboStep, Is.EqualTo(i + 1), $"Press {i + 1} should be step {i + 1}.");

                yield return RunSeconds(Step(i).TotalDuration);
                dealt[i] = before - _dummy.CurrentHealth;
            }

            // Multipliers 1.0, 1.1, 1.6 against Attack 10 and no armour.
            for (int i = 0; i < 3; i++)
            {
                Assert.That(dealt[i], Is.EqualTo(HeroAttack * Step(i).DamageMultiplier).Within(0.001f),
                    $"Step {i + 1} dealt {dealt[i]}, expected {HeroAttack * Step(i).DamageMultiplier}.");
            }

            Assert.That(dealt[2], Is.GreaterThan(dealt[1]), "Step 3 should out-damage step 2 (COM-002).");
            Assert.That(dealt[1], Is.GreaterThan(dealt[0]), "Step 2 should out-damage step 1 (COM-002).");
        }

        // ----- helpers ---------------------------------------------------------------------

        private void BuildHero(SceneContext context)
        {
            _heroObject = Track(new GameObject("Hero") { layer = LayerMask.NameToLayer(GameLayers.Player) });
            _heroObject.transform.position = new Vector3(0f, SpawnY, 0f);

            // Built inactive so every Awake runs after the data is injected; adding a component to
            // an active object fires Awake immediately.
            _heroObject.SetActive(false);

            var renderer = _heroObject.AddComponent<SpriteRenderer>();

            var capsule = _heroObject.AddComponent<CapsuleCollider2D>();
            capsule.direction = CapsuleDirection2D.Vertical;
            capsule.size = new Vector2(0.8f, 1.8f);

            _motor = _heroObject.AddComponent<PlayerMotor>();
            SetField(_motor, "_heroData", _hero);
            SetField(_motor, "_sceneContext", context);

            var health = _heroObject.AddComponent<HealthComponent>();
            SetField(health, "_isPlayer", true);
            SetField(health, "_bodyCollider", capsule);

            var stats = _heroObject.AddComponent<PlayerStats>();
            SetField(stats, "_heroData", _hero);
            SetField(stats, "_balanceConfig", _balance);

            _combat = _heroObject.AddComponent<PlayerCombat>();
            SetField(_combat, "_heroData", _hero);
            SetField(_combat, "_spriteRenderer", renderer);

            _heroObject.AddComponent<PlayerController>();

            _heroObject.SetActive(true);
        }

        private HealthComponent BuildDummy(string name, Vector2 position, float health)
        {
            GameObject dummy = Track(new GameObject(name) { layer = LayerMask.NameToLayer(GameLayers.Enemy) });
            dummy.transform.position = position;
            dummy.SetActive(false);

            var box = dummy.AddComponent<BoxCollider2D>();
            box.size = Vector2.one;

            var component = dummy.AddComponent<HealthComponent>();
            SetField(component, "_bodyCollider", box);

            dummy.SetActive(true);
            component.Initialize(health, 0f, 0f);

            return component;
        }

        private IEnumerator SettleOnGround()
        {
            for (int i = 0; i < 30 && !_motor.IsGrounded; i++) yield return new WaitForFixedUpdate();
            Assert.That(_motor.IsGrounded, Is.True, "Hero failed to settle on the ground.");
        }

        private static IEnumerator RunSeconds(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.fixedDeltaTime) yield return new WaitForFixedUpdate();
            yield return null;
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
