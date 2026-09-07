using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Orders the waves of a stage and hands over to the boss (SRS 14, STG-001 to STG-003).
    /// </summary>
    public sealed class StageManager : IGameService
    {
        private readonly EventBus _eventBus;
        private readonly WaveManager _waveManager;

        /// <summary>Stage being played.</summary>
        public StageData Current { get; private set; }

        /// <summary>Stage progress (STG-003).</summary>
        public StageState State { get; private set; } = StageState.NotStarted;

        public StageManager(EventBus eventBus, WaveManager waveManager)
        {
            _eventBus = eventBus;
            _waveManager = waveManager;
        }

        /// <summary>Starts the stage at wave 0 (STG-001).</summary>
        public void BeginStage(StageData stage)
        {
            // TODO(STG-001): store the stage, move to RunningWaves, begin wave 0 with the
            //   stage's enemy health and damage multipliers.
        }

        /// <summary>Called when a wave clears; advances or moves to the boss (STG-002, STG-003).</summary>
        public void OnWaveCleared()
        {
            // TODO(STG-001): begin the next wave when one remains.
            // TODO(STG-003): otherwise move to BossFight, or Cleared when the stage has no boss.
        }
    }
}
