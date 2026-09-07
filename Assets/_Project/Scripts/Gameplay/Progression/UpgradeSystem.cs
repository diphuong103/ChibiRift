using System.Collections.Generic;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Presents the three Level Up cards and applies the pick (SRS 11).
    /// The roll itself is delegated to <see cref="UpgradeRoller"/>, which is unit-tested and
    /// seeded, so the guarantees of EXP-006, EXP-009, UPG-009 and RNG-005 are verifiable.
    /// </summary>
    public sealed class UpgradeSystem : IGameService
    {
        private readonly EventBus _eventBus;
        private readonly BalanceConfig _balance;
        private readonly DeterministicRandom _random;
        private readonly List<UpgradeData> _pool = new List<UpgradeData>();
        private readonly List<UpgradeData> _currentOffer = new List<UpgradeData>();

        public UpgradeSystem(EventBus eventBus, BalanceConfig balance, DeterministicRandom random)
        {
            _eventBus = eventBus;
            _balance = balance;
            _random = random;
        }

        /// <summary>The three cards currently on screen (EXP-005).</summary>
        public IReadOnlyList<UpgradeData> CurrentOffer => _currentOffer;

        /// <summary>Loads the run pool. MVP needs 12 or more plus the Fallback Pool (UPG-008, UPG-009).</summary>
        public void Initialize(IReadOnlyList<UpgradeData> allUpgrades)
        {
            _pool.Clear();
            if (allUpgrades == null) return;

            for (int i = 0; i < allUpgrades.Count; i++)
            {
                if (allUpgrades[i] != null) _pool.Add(allUpgrades[i]);
            }
        }

        /// <summary>
        /// Builds the offer for a Level Up. Always yields exactly three cards while the Fallback
        /// Pool has entries (EXP-005, EXP-009); a short offer must block the panel (SRS 30).
        /// </summary>
        public int RollOffer(RunState runState)
        {
            // TODO(EXP-005): the count comes from _balance.UpgradeChoiceCount, never a literal.
            return UpgradeRoller.Roll(
                _pool,
                upgrade => runState != null ? runState.GetUpgradeStack(upgrade.Id) : 0,
                _balance,
                _random,
                _currentOffer);
        }

        /// <summary>Applies the chosen card immediately (EXP-007, EXP-008) and records it (UPG-006).</summary>
        public void SelectUpgrade(UpgradeData chosen, RunState runState)
        {
            // TODO(EXP-008): apply the effect to PlayerStats and call RecalculateFromBuild.
            // TODO(UPG-006): increment the stack count in RunState so the build is saved in run state.
            // TODO(EXP-007): clear the offer and resume from PauseReason.LevelUpSelection.
            // TODO(TEL-002): log the three cards shown and the one chosen.
        }
    }
}
