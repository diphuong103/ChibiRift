using System;
using System.Collections.Generic;
using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Runs one wave at a time from <see cref="WaveData"/> (SRS 14).
    /// Composition is data, never hard-coded, which is what NFR-007 asks for, and the spawn
    /// budget of WAV-003 keeps concurrency inside the 30 enemy figure NFR-001 is measured at.
    /// </summary>
    /// <remarks>
    /// <para><b>Polled, not coroutine-driven.</b> Every timer here — per-entry <c>StartDelay</c>,
    /// <c>SpawnInterval</c>, <c>TransitionDelay</c> — advances only inside <see cref="Tick"/>,
    /// called once a frame by whatever owns this instance (<see cref="StageRunner"/> in real play).
    /// A test can drive the whole state machine by calling <see cref="Tick"/> in a loop with a fixed
    /// <c>deltaTime</c>, with no coroutine scheduling to wait on.</para>
    ///
    /// <para><b>WAV-002 ("wave starts only when the stage allows") holds by construction, not by a
    /// runtime check</b>: <see cref="BeginWave"/> has exactly one caller, <see cref="StageManager"/>,
    /// which only ever calls it while its own state is <see cref="StageState.RunningWaves"/>. A
    /// defensive check here would guard a call path that does not exist.</para>
    ///
    /// <para><b>Entries spawn in parallel, not in sequence.</b> <see cref="WaveEntry.StartDelay"/> is
    /// documented as seconds from wave start, not from when an earlier entry finishes, so each
    /// entry tracks its own spawn progress and interval clock.</para>
    /// </remarks>
    public sealed class WaveManager : IGameService
    {
        private readonly EventBus _eventBus;
        private readonly EnemySpawner _spawner;

        /// <summary>Index of the wave in progress within the stage (STG-001).</summary>
        public int CurrentWaveIndex { get; private set; }

        /// <summary>Progress of the wave in progress (WAV-002, WAV-004).</summary>
        public WaveState State { get; private set; } = WaveState.Pending;

        /// <summary>
        /// Raised once the wave's clear condition is met and its transition delay has elapsed
        /// (WAV-004, WAV-005). <see cref="StageManager"/> subscribes to advance to the next wave.
        /// </summary>
        public event Action<int> WaveCleared;

        private WaveData _wave;
        private int _waveCount;
        private string _stageId;
        private float _healthMultiplier = 1f;
        private float _damageMultiplier = 1f;
        private Vector2 _spawnOrigin;
        private float _spawnRadius;

        private int[] _spawnedPerEntry = Array.Empty<int>();
        private float[] _intervalClockPerEntry = Array.Empty<float>();
        private float _waveElapsed;
        private int _killCount;
        private float _transitionRemaining = -1f;

        private readonly List<EnemyController> _alive = new List<EnemyController>();

        public WaveManager(EventBus eventBus, EnemySpawner spawner)
        {
            _eventBus = eventBus;
            _spawner = spawner;
        }

        /// <summary>
        /// Begins <paramref name="wave"/> at <paramref name="waveIndex"/> of <paramref name="waveCount"/>
        /// (WAV-001). <paramref name="healthMultiplier"/>/<paramref name="damageMultiplier"/> are the
        /// stage's scaling (<c>StageData.EnemyHealthMultiplier</c>/<c>EnemyDamageMultiplier</c>);
        /// the elite bonus on top of them is computed per spawn by <see cref="EnemySpawner"/>, not
        /// here. <paramref name="spawnOrigin"/>/<paramref name="spawnRadius"/> come from
        /// <c>StageData.EnemySpawnPoint</c> and <c>BalanceConfig.DebugSpawnRadius</c>.
        /// </summary>
        public void BeginWave(
            WaveData wave, int waveIndex, int waveCount, string stageId,
            float healthMultiplier, float damageMultiplier, Vector2 spawnOrigin, float spawnRadius)
        {
            _wave = wave;
            CurrentWaveIndex = waveIndex;
            _waveCount = waveCount;
            _stageId = stageId;
            _healthMultiplier = healthMultiplier;
            _damageMultiplier = damageMultiplier;
            _spawnOrigin = spawnOrigin;
            _spawnRadius = spawnRadius;

            int entryCount = wave.Entries.Length;
            _spawnedPerEntry = new int[entryCount];
            _intervalClockPerEntry = new float[entryCount];
            _waveElapsed = 0f;
            _killCount = 0;
            _transitionRemaining = -1f;

            for (int i = 0; i < _alive.Count; i++)
            {
                if (_alive[i] != null) _alive[i].Died -= OnEnemyDied;
            }
            _alive.Clear();

            State = WaveState.Spawning;
            PublishState();
        }

        /// <summary>Advances spawning and evaluates the clear condition (WAV-004).</summary>
        public void Tick(float deltaTime)
        {
            if (State == WaveState.Transitioning)
            {
                _transitionRemaining -= deltaTime;
                if (_transitionRemaining > 0f) return;

                State = WaveState.Cleared;
                PublishState();
                WaveCleared?.Invoke(CurrentWaveIndex);
                return;
            }

            if (State != WaveState.Spawning && State != WaveState.Active) return;

            _waveElapsed += deltaTime;

            AdvanceSpawning(deltaTime);
            EvaluateClearCondition();
        }

        private void AdvanceSpawning(float deltaTime)
        {
            WaveEntry[] entries = _wave.Entries;

            for (int i = 0; i < entries.Length; i++)
            {
                WaveEntry entry = entries[i];
                if (_spawnedPerEntry[i] >= entry.Count) continue;
                if (_waveElapsed < entry.StartDelay) continue;

                _intervalClockPerEntry[i] -= deltaTime;

                while (_spawnedPerEntry[i] < entry.Count && _intervalClockPerEntry[i] <= 0f)
                {
                    // WAV-003: never exceed the wave's concurrent-enemy budget. Retried next Tick.
                    if (_spawner.ActiveEnemyCount >= _wave.SpawnBudget) break;

                    EnemyController enemy = _spawner.Spawn(
                        entry.Enemy, SpawnPosition(), entry.SpawnAsElite, _healthMultiplier, _damageMultiplier);

                    // TODO(SRS-30): the pool being exhausted here means a spawn never lands; retried
                    // next Tick rather than treated as an error, since the pool recovers on its own
                    // as enemies die.
                    if (enemy == null) break;

                    _alive.Add(enemy);
                    enemy.Died += OnEnemyDied;
                    _spawnedPerEntry[i]++;
                    _intervalClockPerEntry[i] = _wave.SpawnInterval;
                }
            }

            if (State != WaveState.Spawning) return;

            for (int i = 0; i < entries.Length; i++)
            {
                if (_spawnedPerEntry[i] < entries[i].Count) return;
            }

            State = WaveState.Active;
            PublishState();
        }

        private void EvaluateClearCondition()
        {
            // AllEnemiesDefeated must wait for every entry to finish spawning: _alive can pass
            // through zero between spawns while a later entry's StartDelay is still counting down.
            if (State != WaveState.Active) return;

            bool cleared = _wave.ClearCondition switch
            {
                WaveClearCondition.AllEnemiesDefeated => _alive.Count == 0,
                WaveClearCondition.DurationElapsed => _waveElapsed >= _wave.Duration,
                WaveClearCondition.KillCountReached => _killCount >= _wave.KillTarget,
                _ => false
            };

            if (!cleared) return;

            // WAV-005: the gap before the next wave starts.
            State = WaveState.Transitioning;
            _transitionRemaining = _wave.TransitionDelay;
            PublishState();
        }

        private void OnEnemyDied(EnemyController enemy)
        {
            enemy.Died -= OnEnemyDied;
            _alive.Remove(enemy);
            _killCount++;
            PublishState();
        }

        private Vector2 SpawnPosition()
            => _spawnOrigin + new Vector2(UnityEngine.Random.Range(-_spawnRadius, _spawnRadius), 0f);

        private void PublishState()
            => _eventBus.Publish(new WaveStateChangedEvent(_stageId, CurrentWaveIndex, _waveCount, State, _alive.Count));
    }
}
