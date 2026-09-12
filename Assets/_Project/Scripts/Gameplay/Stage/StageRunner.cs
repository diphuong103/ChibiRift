using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Starts a stage when Run_01 loads and ticks it every frame (WAV-001..005, STG-001..003).
    /// </summary>
    /// <remarks>
    /// <see cref="WaveManager"/> and <see cref="StageManager"/> are plain C# so a test can build and
    /// tick them directly with no scene at all. This is the one place that wires them to Unity's
    /// frame loop for real play — deliberately a scene-local <c>new</c>, not a
    /// <see cref="ServiceLocator"/> registration: their lifetime is exactly one Run_01 load, and
    /// nothing outside this scene needs to resolve them by service lookup (the HUD only ever
    /// listens on <see cref="EventBus"/>).
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class StageRunner : MonoBehaviour
    {
        [Tooltip("Stage to run. MVP has one fixed stage (SRS 43 Q4).")]
        [SerializeField] private StageData _stage;

        [Tooltip("Supplies the enemy spawn scatter radius (DebugSpawnRadius, reused for wave spawns).")]
        [SerializeField] private BalanceConfig _balanceConfig;

        [Tooltip("Pool the stage's enemies come from.")]
        [SerializeField] private EnemySpawner _spawner;

        /// <summary>The running stage/wave state machine, or null before <see cref="Start"/> runs.</summary>
        public StageManager Stage { get; private set; }

        private void Start()
        {
            if (_stage == null || _balanceConfig == null || _spawner == null)
            {
                GameLog.Error("Stage", $"{name} is missing its stage, balance config or spawner (SRS 30).");
                return;
            }

            if (ServiceLocator.Current == null || !ServiceLocator.Current.TryGet(out EventBus eventBus))
            {
                GameLog.Error("Stage", "No EventBus registered. Enter play from the Boot scene.");
                return;
            }

            var waveManager = new WaveManager(eventBus, _spawner);
            Stage = new StageManager(eventBus, waveManager);
            Stage.BeginStage(_stage, _balanceConfig.DebugSpawnRadius);
        }

        private void Update() => Stage?.Tick(Time.deltaTime);
    }
}
