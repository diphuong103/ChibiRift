using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// The stage boss and its phases (SRS 15). MVP requires at least two phases (BOS-002),
    /// each with a readable telegraph (BOS-004), and a death sequence that grants the reward
    /// exactly once (BOS-005, SRS 34).
    /// </summary>
    public sealed class BossManager : IGameService
    {
        private readonly EventBus _eventBus;

        /// <summary>Boss being fought.</summary>
        public BossData Current { get; private set; }

        /// <summary>Active phase index (BOS-002).</summary>
        public int PhaseIndex { get; private set; }

        public BossManager(EventBus eventBus) => _eventBus = eventBus;

        /// <summary>Spawns the boss and shows its HP bar (BOS-001, SRS 19.2).</summary>
        public void BeginBossFight(BossData boss)
        {
            // TODO(BOS-001): spawn the prefab with the boss stat block.
            // TODO(SRS-19.2): publish BossActivatedEvent so the boss HP bar appears.
        }

        /// <summary>Re-evaluates the phase after damage (BOS-003).</summary>
        public void EvaluatePhase(float healthNormalized)
        {
            // TODO(BOS-003): advance when health drops to or past the next phase threshold.
            // TODO(BOS-004): play that phase's telegraph before its first pattern.
            // TODO(BOS-002): publish BossPhaseChangedEvent.
        }

        /// <summary>Runs the death sequence, then grants the reward once (BOS-005).</summary>
        public void OnBossDefeated()
        {
            // TODO(BOS-005): wait BossData.DeathSequenceDuration, then add the boss reward to
            //   RunState and tell RunManager the Run is complete (RUN-002).
        }
    }
}
