using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Drives one Run from start to Post-Run (SRS 18).
    /// All three endings, Victory, Death and Abandon, route through the same Post-Run path so the
    /// reward commit stays single and identical (RUN-002, RUN-003, RUN-008, PAU-004).
    /// </summary>
    public sealed class RunManager : IGameService
    {
        private readonly EventBus _eventBus;
        private readonly ISceneFlowService _sceneFlow;
        private readonly ITelemetryService _telemetry;

        /// <summary>State for the Run in progress (SRS 23).</summary>
        public RunState Current { get; } = new RunState();

        public RunManager(EventBus eventBus, ISceneFlowService sceneFlow, ITelemetryService telemetry)
        {
            _eventBus = eventBus;
            _sceneFlow = sceneFlow;
            _telemetry = telemetry;
        }

        /// <summary>Starts a Run from the Hub (HUB-002, RUN-001).</summary>
        public void StartRun(HeroData hero, int seed)
        {
            // TODO(RUN-001): reset Current, set Hero and Seed, move to Started then InProgress,
            // publish RunStateChangedEvent, and load Run_01.
        }

        /// <summary>Boss defeated (RUN-002).</summary>
        public void CompleteRun()
        {
            // TODO(RUN-002): set state Completed and route to Post-Run.
        }

        /// <summary>Hero died (RUN-003).</summary>
        public void FailRun()
        {
            // TODO(RUN-003): set state Failed and route to Post-Run.
        }

        /// <summary>Abandoned from the Pause Menu, after the confirm step (RUN-008, PAU-003, PAU-004).</summary>
        public void AbandonRun()
        {
            // TODO(RUN-008): set state Abandoned and route to Post-Run so the reward still commits.
        }

        private void EndRun(RunLifecycleState outcome)
        {
            // TODO(RUN-004): publish RunEndedEvent with the summary figures.
            // TODO(TEL-001): log hero, duration, stage/wave reached and the reason the Run ended.
            // TODO(TEL-005): flush telemetry here, at Run end, so no frame pays for the write.
            // TODO(RUN-004): load the PostRun scene.
        }
    }
}
