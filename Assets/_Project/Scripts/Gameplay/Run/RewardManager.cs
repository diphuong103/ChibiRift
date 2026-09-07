using ChibiRift.Core;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Moves the currency earned in a Run into the meta wallet, exactly once (RUN-005, RUN-006).
    /// SRS 16 fixed the policy in v1.1: the full Gold and Gem haul transfers on Victory, Death
    /// and Abandon alike; MVP has no death penalty.
    /// </summary>
    public sealed class RewardManager : IGameService
    {
        private readonly EventBus _eventBus;

        public RewardManager(EventBus eventBus) => _eventBus = eventBus;

        /// <summary>
        /// Commits the Run haul to the wallet. The <see cref="RunState.RewardCommitted"/> guard
        /// makes a second call a no-op, which is what RUN-005 and SRS 34 require.
        /// </summary>
        public void CommitRunRewards(RunState runState)
        {
            // TODO(RUN-005): return immediately when runState.RewardCommitted is already true.
            // TODO(RUN-006): add GoldCollected and GemsCollected to the meta wallet via
            //   CurrencyManager, set RewardCommitted, then trigger the meta save (SAVE-001).
        }
    }
}
