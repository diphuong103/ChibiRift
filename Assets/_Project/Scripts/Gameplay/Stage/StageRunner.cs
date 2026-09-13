using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Wires <see cref="WaveManager"/>/<see cref="StageManager"/> to Unity's frame loop and exposes
    /// <see cref="BeginStage"/>, the one entry point that actually starts a stage (WAV-001..005,
    /// STG-001..003).
    /// </summary>
    /// <remarks>
    /// <para><b>Does not start a stage on its own by default (OI-34).</b> An earlier version called
    /// <c>BeginStage</c> from <c>Start()</c> unconditionally, which meant simply opening Run_01 —
    /// including every PlayMode test that boots it for reasons that have nothing to do with waves —
    /// spawned enemies into the shared pool in the background. That broke
    /// <c>Test_Pool_NoInstantiateAfterPrewarm</c> (the wave's spawns pushed the pool over its
    /// prewarm count) and silently changed enemy counts for any test enumerating them. In P3,
    /// <c>RunManager</c> calls <see cref="BeginStage"/> when a Run actually begins; until then,
    /// <see cref="_autoStartOnPlay"/> (off by default) or the F5 debug key start it by hand.</para>
    ///
    /// <para><see cref="WaveManager"/> and <see cref="StageManager"/> are plain C# so a test can
    /// build and tick them directly with no scene at all. This is the one place that wires them to
    /// Unity's frame loop for real play — deliberately a scene-local <c>new</c>, not a
    /// <see cref="ServiceLocator"/> registration: their lifetime is exactly one Run_01 load, and
    /// nothing outside this scene needs to resolve them by service lookup (the HUD only ever
    /// listens on <see cref="EventBus"/>).</para>
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

        [Tooltip("Starts the stage the instant the scene loads. Off by default (OI-34): P3's RunManager calls BeginStage() when a Run actually starts, not when the scene merely opens.")]
        [SerializeField] private bool _autoStartOnPlay;

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

            if (_autoStartOnPlay) BeginStage();
        }

        /// <summary>
        /// Starts the configured stage (STG-001). The one entry point that actually begins
        /// spawning: called by <c>RunManager</c> once a Run begins in P3, by the F5 debug key
        /// below, or by <see cref="_autoStartOnPlay"/> — nothing else triggers it.
        /// </summary>
        public void BeginStage()
        {
            if (Stage == null)
            {
                GameLog.Error("Stage",
                    $"{name} has not initialized (missing stage, balance config, spawner or EventBus); cannot begin.");
                return;
            }

            Stage.BeginStage(_stage, _balanceConfig.DebugSpawnRadius);
        }

        private void Update()
        {
            Stage?.Tick(Time.deltaTime);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Manual start for hand-testing before RunManager drives this in P3 (OI-29 pattern:
            // gameplay never reads a device directly outside this guard — DeviceInputSourceTests).
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f5Key.wasPressedThisFrame) BeginStage();
#endif
        }
    }
}
