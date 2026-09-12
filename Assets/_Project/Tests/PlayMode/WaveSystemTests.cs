using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;
using ChibiRift.Gameplay;

namespace ChibiRift.Tests.Play
{
    /// <summary>
    /// WAV-001 to WAV-005 and STG-001 to STG-003: <see cref="WaveManager"/> and
    /// <see cref="StageManager"/> spawning, clearing and advancing (P2 slice 1).
    /// </summary>
    /// <remarks>
    /// Both classes are plain C# (<see cref="IGameService"/>), ticked by a <c>float deltaTime</c>
    /// rather than driven by a coroutine, so every test here calls <c>Tick</c> directly in a loop —
    /// no scene load, no <c>yield return</c>, no frame to wait for. Component lifecycle
    /// (<c>Awake</c>/<c>OnEnable</c>) still needs a live Play session, which is why this is a
    /// PlayMode fixture despite every test method being a plain synchronous <c>[Test]</c>: nothing
    /// here waits across a frame, so nothing here can hang the way a coroutine-driven test can
    /// (OI-28 does not apply).
    ///
    /// <para>Enemies are a minimal <see cref="EnemyController"/> + <see cref="HealthComponent"/>
    /// only — no motor, attack or AI component. Nothing under test reads them (spawning, dying and
    /// wave/stage progression do not depend on an enemy's motion or aggro), and every field access
    /// on those three components is null-checked in <see cref="EnemyController.Distribute"/>.</para>
    /// </remarks>
    public sealed class WaveSystemTests
    {
        private const float SpawnInterval = 0.1f;
        private const float TransitionDelay = 0.2f;
        private const float TickStep = 0.05f;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<ScriptableObject> _assets = new List<ScriptableObject>();

        private ServiceLocator _locator;
        private EventBus _eventBus;
        private CombatSystem _combat;
        private BalanceConfig _balance;
        private EnemyData _enemyData;
        private EnemySpawner _spawner;

        [SetUp]
        public void SetUp()
        {
            _locator = new ServiceLocator();
            _eventBus = new EventBus();
            _balance = Create<BalanceConfig>();
            _combat = new CombatSystem(_eventBus, _balance, new DeterministicRandom(1));

            _locator.Register(_eventBus);
            ServiceLocator.SetCurrent(_locator);

            _enemyData = Create<EnemyData>();
            StatBlock stats = _enemyData.BaseStats;
            stats.MaxHealth = 20f;
            stats.Attack = 5f;
            SetField(_enemyData, "_baseStats", stats);

            _spawner = BuildSpawner(BuildEnemyTemplate(_enemyData));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject spawned in _spawned)
            {
                if (spawned != null) Object.DestroyImmediate(spawned);
            }
            _spawned.Clear();

            foreach (ScriptableObject asset in _assets)
            {
                if (asset != null) Object.DestroyImmediate(asset);
            }
            _assets.Clear();

            _locator.Clear();
            ServiceLocator.ClearCurrent();
        }

        // ----- WAV-001, WAV-003, WAV-004 -----------------------------------------------------

        [Test]
        public void Test_Wave_SpawnsCorrectEnemyCount()
        {
            var wave = BuildWave(3);
            var waveManager = new WaveManager(_eventBus, _spawner);

            waveManager.BeginWave(wave, 0, 1, "stage.test", 1f, 1f, Vector2.zero, 0f);
            TickUntil(waveManager, () => waveManager.State == WaveState.Active, maxSeconds: 2f);

            Assert.That(_spawner.ActiveEnemyCount, Is.EqualTo(3),
                "The wave should have spawned exactly its entry's Count.");
        }

        [Test]
        public void Test_Wave_UsesPoolNotInstantiate()
        {
            int createdBeforeWave = _spawner.TotalCreated;

            var wave = BuildWave(3);
            var waveManager = new WaveManager(_eventBus, _spawner);

            waveManager.BeginWave(wave, 0, 1, "stage.test", 1f, 1f, Vector2.zero, 0f);
            TickUntil(waveManager, () => waveManager.State == WaveState.Active, maxSeconds: 2f);

            Assert.That(_spawner.TotalCreated, Is.EqualTo(createdBeforeWave),
                "A wave must spawn from the prewarmed pool. Any rise here is an Instantiate during " +
                "play, which is exactly what SRS 29 forbids.");
        }

        [Test]
        public void Test_Wave_CompletesWhenAllEnemiesDead()
        {
            var wave = BuildWave(2);
            var waveManager = new WaveManager(_eventBus, _spawner);

            waveManager.BeginWave(wave, 0, 1, "stage.test", 1f, 1f, Vector2.zero, 0f);
            TickUntil(waveManager, () => waveManager.State == WaveState.Active, maxSeconds: 2f);

            KillEveryActiveEnemy();
            waveManager.Tick(TickStep);

            Assert.That(waveManager.State, Is.EqualTo(WaveState.Transitioning),
                "AllEnemiesDefeated should trip the moment the last spawned enemy dies (WAV-004).");
        }

        [Test]
        public void Test_Wave_AdvancesAfterDelay()
        {
            var wave = BuildWave(1);
            var waveManager = new WaveManager(_eventBus, _spawner);

            int clearedWaveIndex = -1;
            waveManager.WaveCleared += index => clearedWaveIndex = index;

            waveManager.BeginWave(wave, 0, 1, "stage.test", 1f, 1f, Vector2.zero, 0f);
            TickUntil(waveManager, () => waveManager.State == WaveState.Active, maxSeconds: 2f);

            KillEveryActiveEnemy();
            waveManager.Tick(TickStep);
            Assert.That(waveManager.State, Is.EqualTo(WaveState.Transitioning),
                "Setup: the wave should be counting down its transition delay by now.");

            // Just short of the delay: still transitioning.
            waveManager.Tick(TransitionDelay - TickStep * 0.5f);
            Assert.That(waveManager.State, Is.EqualTo(WaveState.Transitioning),
                "The transition delay (WAV-005) should not have elapsed yet.");
            Assert.That(clearedWaveIndex, Is.EqualTo(-1), "WaveCleared fired before the delay elapsed.");

            waveManager.Tick(TickStep);
            Assert.That(waveManager.State, Is.EqualTo(WaveState.Cleared),
                "The transition delay has now elapsed; the wave should report Cleared.");
            Assert.That(clearedWaveIndex, Is.EqualTo(0), "WaveCleared should name the wave that cleared.");
        }

        // ----- STG-001, STG-002, STG-003 ------------------------------------------------------

        [Test]
        public void Test_Stage_FiresStageClearedAfterLastWave()
        {
            var stage = BuildStage(BuildWave(1), BuildWave(1));
            var waveManager = new WaveManager(_eventBus, _spawner);
            var stageManager = new StageManager(_eventBus, waveManager);

            var stageStates = new List<StageState>();
            _eventBus.Subscribe<StageStateChangedEvent>(evt => stageStates.Add(evt.State));

            stageManager.BeginStage(stage, spawnRadius: 0f);
            Assert.That(stageManager.State, Is.EqualTo(StageState.RunningWaves));

            ClearCurrentWave(waveManager, stageManager);
            Assert.That(waveManager.CurrentWaveIndex, Is.EqualTo(1),
                "Clearing wave 0 of a two-wave stage should have started wave 1 (STG-001).");
            Assert.That(stageManager.State, Is.EqualTo(StageState.RunningWaves),
                "A stage with a wave still to go must not report Cleared.");

            ClearCurrentWave(waveManager, stageManager);
            Assert.That(stageManager.State, Is.EqualTo(StageState.Cleared),
                "A boss-less stage should finish the moment its last wave clears (STG-002, STG-003).");
            Assert.That(stageStates, Does.Contain(StageState.Cleared),
                "StageStateChangedEvent must carry the Cleared state so the HUD/next system can react.");
        }

        // ----- helpers -------------------------------------------------------------------------

        /// <summary>Ticks until <paramref name="done"/> is true or <paramref name="maxSeconds"/> is spent.</summary>
        private static void TickUntil(WaveManager waveManager, System.Func<bool> done, float maxSeconds)
        {
            float elapsed = 0f;
            while (!done() && elapsed < maxSeconds)
            {
                waveManager.Tick(TickStep);
                elapsed += TickStep;
            }

            Assert.That(done(), Is.True, $"Condition was not met within {maxSeconds}s of simulated ticks.");
        }

        /// <summary>Runs one wave from Active to Cleared: kills its enemies, then ticks past the delay.</summary>
        private void ClearCurrentWave(WaveManager waveManager, StageManager stageManager)
        {
            TickUntil(waveManager, () => waveManager.State == WaveState.Active, maxSeconds: 2f);
            KillEveryActiveEnemy();
            stageManager.Tick(TickStep);
            stageManager.Tick(TransitionDelay + TickStep);
        }

        /// <summary>
        /// Kills every currently-active spawned enemy. <c>GetComponentsInChildren</c> defaults to
        /// active-only, which is exactly "alive": a despawned corpse is inactive again by the time
        /// it goes back to the pool.
        /// </summary>
        private void KillEveryActiveEnemy()
        {
            foreach (EnemyController enemy in _spawner.GetComponentsInChildren<EnemyController>())
            {
                if (enemy.Health == null || enemy.Health.IsDead) continue;

                _combat.DealDamage(
                    enemy.Health, 9999f, 1f, 0f, DamageSource.BasicAttack,
                    enemy.transform.position, Vector2.zero);
            }
        }

        private WaveData BuildWave(int count)
        {
            var wave = Create<WaveData>();
            var entries = new[]
            {
                new WaveEntry { Enemy = _enemyData, Count = count, SpawnAsElite = false, StartDelay = 0f }
            };

            SetField(wave, "_entries", entries);
            SetField(wave, "_clearCondition", WaveClearCondition.AllEnemiesDefeated);
            SetField(wave, "_spawnBudget", 15);
            SetField(wave, "_spawnInterval", SpawnInterval);
            SetField(wave, "_transitionDelay", TransitionDelay);
            return wave;
        }

        private StageData BuildStage(params WaveData[] waves)
        {
            var stage = Create<StageData>();
            SetField(stage, "_waves", waves);
            SetField(stage, "_enemyHealthMultiplier", 1f);
            SetField(stage, "_enemyDamageMultiplier", 1f);
            SetField(stage, "_enemySpawnPoint", Vector2.zero);
            return stage;
        }

        private EnemyController BuildEnemyTemplate(EnemyData data)
        {
            var template = Track(new GameObject("EnemyTemplate")
            {
                layer = LayerMask.NameToLayer(GameLayers.Enemy)
            });
            template.SetActive(false);

            var box = template.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.8f, 1.8f);

            var health = template.AddComponent<HealthComponent>();
            SetField(health, "_bodyCollider", box);

            var controller = template.AddComponent<EnemyController>();
            SetField(controller, "_enemyData", data);

            return controller;
        }

        private EnemySpawner BuildSpawner(EnemyController template)
        {
            var spawnerObject = Track(new GameObject("EnemySpawner"));
            spawnerObject.SetActive(false);

            var spawner = spawnerObject.AddComponent<EnemySpawner>();
            SetField(spawner, "_balanceConfig", _balance);
            SetField(spawner, "_enemyData", _enemyData);
            SetField(spawner, "_prefab", template);

            spawnerObject.SetActive(true);
            return spawner;
        }

        private GameObject Track(GameObject spawned)
        {
            _spawned.Add(spawned);
            return spawned;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
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
