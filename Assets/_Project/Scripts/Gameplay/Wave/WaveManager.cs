using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Runs one wave at a time from <see cref="WaveData"/> (SRS 14).
    /// Composition is data, never hard-coded, which is what NFR-007 asks for, and the spawn
    /// budget of WAV-003 keeps concurrency inside the 30 enemy figure NFR-001 is measured at.
    /// </summary>
    public sealed class WaveManager : IGameService
    {
        private readonly EventBus _eventBus;
        private readonly EnemySpawner _spawner;

        /// <summary>Index of the wave in progress within the stage (STG-001).</summary>
        public int CurrentWaveIndex { get; private set; }

        /// <summary>Progress of the wave in progress (WAV-002, WAV-004).</summary>
        public WaveState State { get; private set; } = WaveState.Pending;

        public WaveManager(EventBus eventBus, EnemySpawner spawner)
        {
            _eventBus = eventBus;
            _spawner = spawner;
        }

        /// <summary>Begins a wave, but only when the stage state permits it (WAV-002).</summary>
        public void BeginWave(WaveData wave, int waveIndex, float healthMultiplier, float damageMultiplier)
        {
            // TODO(WAV-002): refuse unless the stage is in RunningWaves.
            // TODO(WAV-001): spawn each WaveEntry honouring its StartDelay and SpawnInterval.
            // TODO(WAV-003): never exceed WaveData.SpawnBudget concurrent enemies.
            // TODO(WAV-002): publish WaveStateChangedEvent so the HUD shows wave progress.
        }

        /// <summary>Advances spawning and evaluates the clear condition (WAV-004).</summary>
        public void Tick(float deltaTime)
        {
            // TODO(WAV-004): evaluate AllEnemiesDefeated, DurationElapsed or KillCountReached.
            // TODO(WAV-005): hold for WaveData.TransitionDelay before reporting the wave cleared.
            // TODO(SRS-30): if a spawn never lands, time out, log, and continue rather than hanging.
        }
    }
}
