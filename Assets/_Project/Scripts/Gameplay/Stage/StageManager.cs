using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Orders the waves of a stage and hands over to the boss (SRS 14, STG-001 to STG-003).
    /// </summary>
    /// <remarks>
    /// Reacts to <see cref="WaveManager.WaveCleared"/> rather than being polled or told externally
    /// when a wave ends — the same shape <see cref="ChibiRift.Gameplay.EnemyController"/> uses to
    /// bubble <c>HealthComponent.Died</c>. <see cref="Tick"/> exists only to give whatever owns this
    /// instance (<see cref="StageRunner"/>) one call to make each frame instead of two.
    /// </remarks>
    public sealed class StageManager : IGameService
    {
        private readonly EventBus _eventBus;
        private readonly WaveManager _waveManager;

        private float _spawnRadius;

        /// <summary>Stage being played.</summary>
        public StageData Current { get; private set; }

        /// <summary>Stage progress (STG-003).</summary>
        public StageState State { get; private set; } = StageState.NotStarted;

        public StageManager(EventBus eventBus, WaveManager waveManager)
        {
            _eventBus = eventBus;
            _waveManager = waveManager;
            _waveManager.WaveCleared += HandleWaveCleared;
        }

        /// <summary>
        /// Starts the stage at wave 0 (STG-001). <paramref name="spawnRadius"/> is
        /// <c>BalanceConfig.DebugSpawnRadius</c>, handed in here because <see cref="WaveManager"/>
        /// has no reason to hold a <c>BalanceConfig</c> reference of its own.
        /// </summary>
        public void BeginStage(StageData stage, float spawnRadius)
        {
            Current = stage;
            _spawnRadius = spawnRadius;

            State = StageState.RunningWaves;
            PublishState();

            BeginWaveAt(0);
        }

        /// <summary>Advances the wave in progress (WAV-004, WAV-005). Called once a frame.</summary>
        public void Tick(float deltaTime) => _waveManager.Tick(deltaTime);

        private void BeginWaveAt(int index)
        {
            WaveData wave = Current.Waves[index];
            _waveManager.BeginWave(
                wave, index, Current.WaveCount, Current.Id,
                Current.EnemyHealthMultiplier, Current.EnemyDamageMultiplier,
                Current.EnemySpawnPoint, _spawnRadius);
        }

        /// <summary>Advances to the next wave, or to the boss/cleared state (STG-002, STG-003).</summary>
        private void HandleWaveCleared(int waveIndex)
        {
            int next = waveIndex + 1;
            if (next < Current.WaveCount)
            {
                BeginWaveAt(next);
                return;
            }

            // STG-003: a stage with no boss ends the moment its last wave clears.
            State = Current.Boss != null ? StageState.BossFight : StageState.Cleared;
            PublishState();
        }

        private void PublishState() => _eventBus.Publish(new StageStateChangedEvent(Current.Id, State));
    }
}
