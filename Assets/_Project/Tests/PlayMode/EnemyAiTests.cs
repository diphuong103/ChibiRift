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
    /// TC-AI: the enemy half of P1 slice 3 (AI-001 to AI-005, COM-005, HPS-005).
    /// </summary>
    /// <remarks>
    /// Every wait is <c>WaitForFixedUpdate</c>. In batch mode a frame is far shorter than the fixed
    /// timestep, so waiting on physics steps also guarantees several <c>Update</c> calls have run —
    /// whereas waiting on frames guarantees no physics step at all, which is exactly what made a
    /// slice 2 test pass for the wrong reason.
    ///
    /// <para>The arena is built in code, private serialized fields are set by reflection so this
    /// assembly needs no UnityEditor reference, and only objects this fixture created are
    /// destroyed.</para>
    ///
    /// <para><b>Timeout.</b> Three times the slowest test in this fixture today. Its longest test measured 5.20s. A
    /// test that hangs — waiting on a physics step while time is frozen is how it happens here —
    /// fails with a message rather than running forever. A run that never finishes reports nothing
    /// at all, which is why this is a guard and not a convenience (OI-28).</para>
    /// </remarks>
    [Timeout(20000)]
    public sealed class EnemyAiTests
    {
        private const float GroundSurfaceY = 0f;
        private const float StandY = 1f;

        private const float EnemyHealth = 40f;
        private const float EnemyDamage = 8f;
        private const float EnemyMoveSpeed = 3f;
        private const float DetectionRange = 8f;
        private const float LoseAggroRange = 12f;
        private const float AttackRange = 1.2f;
        private const float HurtStun = 0.2f;

        private const float HeroHealth = 100f;
        private const float HeroIFrames = 0.8f;

        private readonly List<GameObject> _spawned = new List<GameObject>();

        private ServiceLocator _locator;
        private EventBus _eventBus;
        private BalanceConfig _balance;
        private EnemyData _enemyData;
        private HeroData _heroData;

        private GameObject _heroObject;
        private HealthComponent _heroHealth;

        private GameObject _enemyObject;
        private EnemyAI _ai;
        private EnemyMotor _motor;
        private EnemyAttack _attack;
        private HealthComponent _enemyHealth;

        private CombatSystem Combat => ServiceLocator.Current.Get<CombatSystem>();
        private float EnemyX => _enemyObject.transform.position.x;

        [SetUp]
        public void SetUp()
        {
            _locator = new ServiceLocator();
            _eventBus = new EventBus();

            _balance = ScriptableObject.CreateInstance<BalanceConfig>();
            _enemyData = BuildEnemyData();
            _heroData = ScriptableObject.CreateInstance<HeroData>();

            _locator.Register(_eventBus);
            _locator.Register<IInputService>(new FakeInputService());
            _locator.Register(new CombatSystem(_eventBus, _balance, new DeterministicRandom(1)));
            ServiceLocator.SetCurrent(_locator);

            // Wide and flat: these tests are about decisions, not terrain.
            Track(CreateSolid("Ground", new Vector2(0f, GroundSurfaceY - 0.5f), new Vector2(120f, 1f), GameLayers.Ground));

            BuildHero(new Vector2(0f, StandY));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject spawned in _spawned)
            {
                if (spawned != null) Object.DestroyImmediate(spawned);
            }
            _spawned.Clear();

            if (_balance != null) Object.DestroyImmediate(_balance);
            if (_enemyData != null) Object.DestroyImmediate(_enemyData);
            if (_heroData != null) Object.DestroyImmediate(_heroData);

            _locator.Clear();
            ServiceLocator.ClearCurrent();
        }

        // ----- AI-002: detection and aggro ------------------------------------------------

        [UnityTest]
        public IEnumerator Test_Enemy_StaysIdleWhenPlayerFar()
        {
            BuildEnemy(new Vector2(DetectionRange + 3f, StandY));
            yield return Steps(20);

            Assert.That(_ai.State, Is.EqualTo(EnemyLifecycleState.Idle),
                $"Hero is {DetectionRange + 3f}u away, beyond the {DetectionRange}u detection range.");
            Assert.That(Mathf.Abs(_motor.Velocity.x), Is.LessThan(0.01f), "An idle enemy should not drift.");
        }

        [UnityTest]
        public IEnumerator Test_Enemy_ChasesWhenPlayerInDetectionRange()
        {
            BuildEnemy(new Vector2(DetectionRange - 2f, StandY));
            float startX = EnemyX;

            yield return Steps(30);

            Assert.That(_ai.State, Is.EqualTo(EnemyLifecycleState.Chase),
                "Hero is inside detection range, so the enemy should be chasing (AI-002).");
            Assert.That(EnemyX, Is.LessThan(startX),
                "The hero is to the left, so the enemy should have closed the distance.");
        }

        [UnityTest]
        public IEnumerator Test_Enemy_LosesAggroBeyondLoseRange()
        {
            BuildEnemy(new Vector2(DetectionRange - 1f, StandY));
            yield return Steps(20);
            Assert.That(_ai.State, Is.EqualTo(EnemyLifecycleState.Chase), "Setup failed: never aggroed.");

            // Teleporting the hero rather than walking: the point is the threshold, not the travel.
            _heroObject.transform.position = new Vector3(-(LoseAggroRange + 5f), StandY, 0f);
            yield return Steps(20);

            Assert.That(_ai.State, Is.EqualTo(EnemyLifecycleState.ReturnToSpawn),
                $"Hero is past the {LoseAggroRange}u give-up range, so aggro should have dropped (AI-002).");
        }

        [UnityTest]
        public IEnumerator Test_Enemy_ReturnsToSpawnAfterLosingAggro()
        {
            var spawn = new Vector2(DetectionRange - 1f, StandY);
            BuildEnemy(spawn);

            yield return Steps(20);
            Assert.That(_ai.State, Is.EqualTo(EnemyLifecycleState.Chase), "Setup failed: never aggroed.");

            float chasedX = EnemyX;
            Assert.That(chasedX, Is.LessThan(spawn.x), "Setup failed: the enemy never left its spawn.");

            _heroObject.transform.position = new Vector3(-(LoseAggroRange + 20f), StandY, 0f);

            // Long enough to walk home at 3 u/s from wherever the chase reached.
            yield return Steps(240);

            Assert.That(EnemyX, Is.EqualTo(spawn.x).Within(0.3f),
                $"Should have walked home to x={spawn.x}, ended at {EnemyX} (AI-002).");
            Assert.That(_ai.State, Is.EqualTo(EnemyLifecycleState.Idle),
                "Once home, the enemy should settle back to Idle.");
        }

        // ----- AI-004: attack window and cooldown -----------------------------------------

        [UnityTest]
        public IEnumerator Test_Enemy_AttacksWhenInRange()
        {
            BuildEnemy(new Vector2(AttackRange - 0.2f, StandY));
            yield return Steps(30);

            Assert.That(
                _ai.State,
                Is.EqualTo(EnemyLifecycleState.Attack).Or.EqualTo(EnemyLifecycleState.Recovery),
                "Inside attack range with the cooldown clear, the enemy should have committed (AI-004).");
        }

        [UnityTest]
        public IEnumerator Test_Enemy_AttackRespectsCooldown()
        {
            // This test measures one thing: the gap between two swings. Two other systems would
            // otherwise decide the outcome instead.
            //
            // Enemy pinned: left mobile it walks straight through the hero (Player and Enemy do not
            // collide, by design) and oscillates in and out of range.
            StatBlock stats = _enemyData.BaseStats;
            stats.MoveSpeed = 0f;
            SetField(_enemyData, "_baseStats", stats);

            // Hero knockback disabled: the first hit shoves the hero 0.48u, which alone is enough
            // to push them past the 1.2u attack range. A pinned enemy then cannot close again and
            // the missing second swing would look like a cooldown failure when it is a positioning
            // one. Knockback has its own tests.
            SetField(_balance, "_heroKnockbackForce", 0f);

            BuildEnemy(new Vector2(AttackRange - 0.2f, StandY));

            yield return WaitUntilAttacking();
            yield return Steps(StepsFor(_enemyData.Attack.TotalDuration + 0.05f));

            Assert.That(_attack.IsAttacking, Is.False, "The first swing should have finished.");
            Assert.That(_attack.CooldownRemaining, Is.GreaterThan(0f),
                "The cooldown should start when recovery ends (AI-004).");

            // Most of the way through the cooldown: still nothing, even though range allows it.
            yield return Steps(StepsFor(_enemyData.Attack.Cooldown * 0.5f));

            Assert.That(_attack.IsAttacking, Is.False,
                "A second swing started while the cooldown was still running (AI-004).");

            // Past it: the enemy swings again, which is what proves the cooldown released rather
            // than the enemy simply having stopped attacking.
            yield return Steps(StepsFor(_enemyData.Attack.Cooldown * 0.5f + 0.2f));

            Assert.That(_attack.IsAttacking, Is.True,
                $"After {_enemyData.Attack.Cooldown}s the enemy should have swung again (AI-004).");
        }

        [UnityTest]
        public IEnumerator Test_Enemy_HitboxOnlyActiveInWindow()
        {
            BuildEnemy(new Vector2(AttackRange - 0.2f, StandY));

            EnemyAttackConfig config = _enemyData.Attack;
            _attack.BeginAttack();

            var samples = new List<(float Elapsed, bool Active)>();
            int steps = StepsFor(config.TotalDuration);
            for (int i = 0; i < steps; i++)
            {
                yield return TestTime.Steps(1);
                samples.Add((_attack.Elapsed, _attack.IsHitboxActive));
            }

            float slack = Time.fixedDeltaTime;
            foreach ((float elapsed, bool active) in samples)
            {
                if (elapsed < config.ActiveStartTime - slack)
                {
                    Assert.That(active, Is.False,
                        $"Hitbox was live at {elapsed:F3}s, before the window opens at {config.ActiveStartTime}s.");
                }
                else if (elapsed > config.ActiveEndTime + slack)
                {
                    Assert.That(active, Is.False,
                        $"Hitbox was still live at {elapsed:F3}s, after it closes at {config.ActiveEndTime}s.");
                }
            }

            Assert.That(samples.Exists(s => s.Active), Is.True,
                "The hitbox never opened, so the window assertions proved nothing.");
        }

        [UnityTest]
        public IEnumerator Test_Enemy_DamageGoesThroughCombatSystem()
        {
            BuildEnemy(new Vector2(AttackRange - 0.2f, StandY));

            var sources = new List<DamageSource>();
            void OnDamage(DamageAppliedEvent evt)
            {
                if (evt.TargetIsPlayer) sources.Add(evt.Result.Source);
            }

            _eventBus.Subscribe<DamageAppliedEvent>(OnDamage);
            try
            {
                float before = _heroHealth.CurrentHealth;

                yield return WaitUntilAttacking();
                yield return Steps(StepsFor(_enemyData.Attack.TotalDuration + 0.05f));

                Assert.That(_heroHealth.CurrentHealth, Is.LessThan(before), "The hero was never hit.");

                // The event only exists because CombatSystem published it. Health dropping without
                // one would mean a second damage path had been added around the pipeline (HPS-003).
                Assert.That(sources, Is.Not.Empty,
                    "The hero lost health but no DamageAppliedEvent was published, so the damage " +
                    "bypassed CombatSystem (HPS-003).");

                float expected = before - EnemyDamage;
                Assert.That(_heroHealth.CurrentHealth, Is.EqualTo(expected).Within(0.001f),
                    $"Expected {EnemyDamage} damage from the enemy's Attack stat.");
            }
            finally
            {
                _eventBus.Unsubscribe<DamageAppliedEvent>(OnDamage);
            }
        }

        // ----- HPS-005: hero i-frames ------------------------------------------------------

        [UnityTest]
        public IEnumerator Test_Hero_IFrameBlocksSecondHit()
        {
            BuildEnemy(new Vector2(20f, StandY));

            Combat.DealDamage(_heroHealth, EnemyDamage, 1f, 0f, DamageSource.BasicAttack,
                _heroObject.transform.position, new Vector2(5f, StandY));
            yield return TestTime.Steps(1);

            float afterFirst = _heroHealth.CurrentHealth;
            Assert.That(afterFirst, Is.EqualTo(HeroHealth - EnemyDamage).Within(0.001f),
                "The first hit should have landed in full.");
            Assert.That(_heroHealth.IsInvulnerable, Is.True,
                "A surviving hit should open the post-hit window (HPS-005).");

            Combat.DealDamage(_heroHealth, EnemyDamage, 1f, 0f, DamageSource.BasicAttack,
                _heroObject.transform.position, new Vector2(5f, StandY));
            yield return TestTime.Steps(1);

            Assert.That(_heroHealth.CurrentHealth, Is.EqualTo(afterFirst).Within(0.001f),
                "The second hit landed during i-frames and should have been refused (HPS-005).");
        }

        [UnityTest]
        public IEnumerator Test_Hero_IFrameExpiresAfterDuration()
        {
            BuildEnemy(new Vector2(20f, StandY));

            Combat.DealDamage(_heroHealth, EnemyDamage, 1f, 0f, DamageSource.BasicAttack,
                _heroObject.transform.position, new Vector2(5f, StandY));
            yield return TestTime.Steps(1);

            float afterFirst = _heroHealth.CurrentHealth;

            yield return Steps(StepsFor(HeroIFrames + 0.1f));

            Assert.That(_heroHealth.IsInvulnerable, Is.False,
                $"The {HeroIFrames}s window should have closed by now (HPS-005).");

            Combat.DealDamage(_heroHealth, EnemyDamage, 1f, 0f, DamageSource.BasicAttack,
                _heroObject.transform.position, new Vector2(5f, StandY));
            yield return TestTime.Steps(1);

            Assert.That(_heroHealth.CurrentHealth, Is.EqualTo(afterFirst - EnemyDamage).Within(0.001f),
                "Once the window closed the next hit should land in full.");
        }

        [UnityTest]
        public IEnumerator Test_Hero_HitResetsCombo()
        {
            BuildEnemy(new Vector2(20f, StandY));

            var combat = _heroObject.GetComponent<PlayerCombat>();
            combat.RequestAttack();
            yield return Steps(StepsFor(0.35f));

            Assert.That(combat.ComboStep, Is.EqualTo(1), "Setup failed: the first swing never ran.");

            Combat.DealDamage(_heroHealth, EnemyDamage, 1f, 0f, DamageSource.BasicAttack,
                _heroObject.transform.position, new Vector2(5f, StandY));
            yield return TestTime.Steps(1);

            // COM-003 was written in slice 2 but could not be tested until something could hit back.
            Assert.That(combat.ComboStep, Is.EqualTo(0),
                "Taking a hit should drop the chain (COM-003).");
        }

        // ----- COM-005: knockback ----------------------------------------------------------

        [UnityTest]
        public IEnumerator Test_Knockback_MovesTargetAwayFromAttacker()
        {
            BuildEnemy(new Vector2(6f, StandY));
            yield return TestTime.Steps(1);

            float before = EnemyX;

            // Attacker to the LEFT of the enemy, so the push must be to the right.
            Combat.DealDamage(_enemyHealth, 1f, 1f, 0f, DamageSource.BasicAttack,
                _enemyObject.transform.position, new Vector2(before - 2f, StandY));

            Assert.That(_motor.IsKnockedBack, Is.True,
                "The hit should have opened a knockback window synchronously (COM-005).");

            yield return Steps(StepsFor(_balance.EnemyKnockbackDuration));

            Assert.That(EnemyX, Is.GreaterThan(before + 0.1f),
                $"Enemy should have been pushed right, away from the attacker; moved from {before} to {EnemyX}.");
        }

        [UnityTest]
        public IEnumerator Test_Knockback_ReturnsControlAfterDuration()
        {
            BuildEnemy(new Vector2(6f, StandY));
            yield return TestTime.Steps(1);

            Combat.DealDamage(_enemyHealth, 1f, 1f, 0f, DamageSource.BasicAttack,
                _enemyObject.transform.position, new Vector2(EnemyX - 2f, StandY));

            Assert.That(_motor.IsKnockedBack, Is.True, "Setup failed: no knockback started.");

            yield return Steps(StepsFor(_balance.EnemyKnockbackDuration + 0.05f));

            Assert.That(_motor.IsKnockedBack, Is.False,
                $"Control should return after {_balance.EnemyKnockbackDuration}s (COM-005).");

            // And the enemy must actually drive itself again, not stay frozen.
            float resumedFrom = EnemyX;
            yield return Steps(StepsFor(_enemyData.HurtStunDuration + 0.4f));

            Assert.That(EnemyX, Is.LessThan(resumedFrom - 0.1f),
                "After the knockback the enemy should resume chasing the hero on its left.");
        }

        // ----- AI-001 and AI-003: interrupts and death -------------------------------------

        [UnityTest]
        public IEnumerator Test_Enemy_HurtStunInterruptsWindup()
        {
            BuildEnemy(new Vector2(AttackRange - 0.2f, StandY));

            _attack.BeginAttack();

            // Land the hit inside the windup, before the hitbox would have opened.
            yield return Steps(StepsFor(_enemyData.Attack.Windup * 0.5f));
            Assert.That(_attack.IsAttacking, Is.True, "Setup failed: the swing never started.");
            Assert.That(_attack.IsHitboxActive, Is.False, "Setup failed: already past windup.");

            Combat.DealDamage(_enemyHealth, 1f, 1f, 0f, DamageSource.BasicAttack,
                _enemyObject.transform.position, new Vector2(EnemyX - 2f, StandY));

            Assert.That(_ai.State, Is.EqualTo(EnemyLifecycleState.Hurt),
                "A landed hit interrupts every state but Death (AI-001).");
            Assert.That(_attack.IsAttacking, Is.False, "The interrupted swing should have been cancelled.");

            yield return Steps(StepsFor(HurtStun + 0.1f));

            Assert.That(_ai.State, Is.Not.EqualTo(EnemyLifecycleState.Hurt),
                $"Stun should end after {HurtStun}s.");
        }

        [UnityTest]
        public IEnumerator Test_Enemy_DeathStopsAllAI()
        {
            BuildEnemy(new Vector2(AttackRange - 0.2f, StandY));
            yield return TestTime.Steps(1);

            // One overwhelming hit, so death cannot be confused with a stun.
            Combat.DealDamage(_enemyHealth, EnemyHealth * 10f, 1f, 0f, DamageSource.BasicAttack,
                _enemyObject.transform.position, new Vector2(EnemyX - 2f, StandY));
            yield return TestTime.Steps(1);

            Assert.That(_enemyHealth.IsDead, Is.True, "Setup failed: the enemy survived.");
            Assert.That(_ai.State, Is.EqualTo(EnemyLifecycleState.Death), "Death should be entered at once.");

            float restingX = EnemyX;
            yield return Steps(60);

            // AI-003: Death is terminal. No chase, no attack, no drift, whatever the hero does.
            Assert.That(_ai.State, Is.EqualTo(EnemyLifecycleState.Death), "Death must be terminal (AI-003).");
            Assert.That(_attack.IsAttacking, Is.False, "A corpse must not swing.");
            Assert.That(EnemyX, Is.EqualTo(restingX).Within(0.05f), "A corpse must not walk.");
        }

        // ----- AI-001 and NFR-007: data drives behaviour -----------------------------------

        [UnityTest]
        public IEnumerator Test_Enemy_StatsReadFromEnemyData()
        {
            // Halving the detection range in the ASSET must change behaviour with no script edit.
            // The hero sits at a distance that the default range reaches and the halved one does not.
            float halved = DetectionRange / 2f;
            SetField(_enemyData, "_detectionRange", halved);

            BuildEnemy(new Vector2(halved + 2f, StandY));
            yield return Steps(30);

            Assert.That(_ai.State, Is.EqualTo(EnemyLifecycleState.Idle),
                $"With detection cut to {halved}u the hero at {halved + 2f}u is out of range, " +
                "so behaviour did not follow the asset (NFR-007).");

            // And health seeded from the asset, not from a constant in the component.
            Assert.That(_enemyHealth.MaxHealth, Is.EqualTo(EnemyHealth).Within(0.001f),
                "MaxHealth should come from EnemyData.BaseStats.");
            Assert.That(_enemyData.BaseStats.Attack, Is.EqualTo(EnemyDamage).Within(0.001f),
                "The damage figure should come from EnemyData.BaseStats.");
        }

        // ----- helpers ---------------------------------------------------------------------

        private EnemyData BuildEnemyData()
        {
            var data = ScriptableObject.CreateInstance<EnemyData>();

            StatBlock stats = data.BaseStats;
            stats.MaxHealth = EnemyHealth;
            stats.Attack = EnemyDamage;
            stats.MoveSpeed = EnemyMoveSpeed;
            stats.Defense = 0f;
            stats.DamageReduction = 0f;

            SetField(data, "_baseStats", stats);
            SetField(data, "_detectionRange", DetectionRange);
            SetField(data, "_loseAggroRange", LoseAggroRange);
            SetField(data, "_attackRange", AttackRange);
            SetField(data, "_hurtStunDuration", HurtStun);
            SetField(data, "_attack", EnemyAttackConfig.MeleeBaseline);

            return data;
        }

        private void BuildHero(Vector2 position)
        {
            _heroObject = Track(new GameObject("Hero")
            {
                layer = LayerMask.NameToLayer(GameLayers.Player),
                tag = "Player"
            });
            _heroObject.transform.position = position;
            _heroObject.SetActive(false);

            _heroObject.AddComponent<SpriteRenderer>();

            var capsule = _heroObject.AddComponent<CapsuleCollider2D>();
            capsule.direction = CapsuleDirection2D.Vertical;
            capsule.size = new Vector2(0.8f, 1.8f);

            var motor = _heroObject.AddComponent<PlayerMotor>();
            SetField(motor, "_heroData", _heroData);

            _heroHealth = _heroObject.AddComponent<HealthComponent>();
            SetField(_heroHealth, "_isPlayer", true);
            SetField(_heroHealth, "_bodyCollider", capsule);
            SetField(_heroHealth, "_hurtIFrameDuration", HeroIFrames);

            var stats = _heroObject.AddComponent<PlayerStats>();
            SetField(stats, "_heroData", _heroData);
            SetField(stats, "_balanceConfig", _balance);

            var attack = ScriptableObject.CreateInstance<AttackData>();
            SetField(attack, "_steps", AttackData.BaselineSteps());
            SetField(_heroData, "_basicAttack", attack);

            StatBlock heroStats = _heroData.BaseStats;
            heroStats.MaxHealth = HeroHealth;
            SetField(_heroData, "_baseStats", heroStats);

            var combat = _heroObject.AddComponent<PlayerCombat>();
            SetField(combat, "_heroData", _heroData);

            _heroObject.SetActive(true);
        }

        private void BuildEnemy(Vector2 position)
        {
            _enemyObject = Track(new GameObject("Enemy") { layer = LayerMask.NameToLayer(GameLayers.Enemy) });
            _enemyObject.transform.position = position;
            _enemyObject.SetActive(false);

            _enemyObject.AddComponent<SpriteRenderer>();

            var body = _enemyObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.freezeRotation = true;

            var box = _enemyObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.8f, 1.8f);

            _enemyHealth = _enemyObject.AddComponent<HealthComponent>();
            SetField(_enemyHealth, "_sourceData", _enemyData);
            SetField(_enemyHealth, "_bodyCollider", box);

            _motor = _enemyObject.AddComponent<EnemyMotor>();
            SetField(_motor, "_enemyData", _enemyData);

            _attack = _enemyObject.AddComponent<EnemyAttack>();
            SetField(_attack, "_enemyData", _enemyData);

            _ai = _enemyObject.AddComponent<EnemyAI>();
            SetField(_ai, "_enemyData", _enemyData);
            SetField(_ai, "_balanceConfig", _balance);

            var controller = _enemyObject.GetComponent<EnemyController>();
            if (controller == null) controller = _enemyObject.AddComponent<EnemyController>();
            SetField(controller, "_enemyData", _enemyData);

            _enemyObject.SetActive(true);

            // Set explicitly rather than relying on the tag lookup in Start, so the test does not
            // depend on scene search order.
            _ai.Target = _heroObject.transform;
        }

        /// <summary>
        /// Runs until the enemy commits to a swing. The state machine evaluates on its own clock,
        /// so a test that assumed the swing began immediately would be asserting on a swing that
        /// had not started.
        /// </summary>
        private IEnumerator WaitUntilAttacking()
        {
            int budget = StepsFor(1f);
            for (int i = 0; i < budget && !_attack.IsAttacking; i++) yield return TestTime.Steps(1);

            Assert.That(_attack.IsAttacking, Is.True,
                "The enemy never started a swing, so nothing downstream was tested.");
        }

        private static IEnumerator Steps(int count) => TestTime.Steps(count);

        private static int StepsFor(float seconds) => TestTime.StepsFor(seconds);

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

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"{target.GetType().Name} has no field '{fieldName}'.");
            field.SetValue(target, value);
        }
    }
}
