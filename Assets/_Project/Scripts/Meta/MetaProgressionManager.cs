using ChibiRift.Core;
using ChibiRift.Save;

namespace ChibiRift.Meta
{
    /// <summary>
    /// Permanent upgrades bought in the Hub (SRS 17). MVP must offer HP, Attack and Crit
    /// (META-001 to META-003), each with an explicit level and cost (META-004), all persisting
    /// across Runs (META-006).
    /// </summary>
    public sealed class MetaProgressionManager : IGameService
    {
        private readonly SaveManager _save;
        private readonly CurrencyManager _currency;
        private readonly EventBus _eventBus;

        public MetaProgressionManager(SaveManager save, CurrencyManager currency, EventBus eventBus)
        {
            _save = save;
            _currency = currency;
            _eventBus = eventBus;
        }

        /// <summary>Level owned of a permanent upgrade (META-004, META-006).</summary>
        public int GetLevel(string upgradeId) => _save.Data.GetUpgradeLevel(upgradeId);

        /// <summary>Cost of the next level (META-004, SRS 43 Q8: cost curve plus a cap).</summary>
        public int GetNextLevelCost(string upgradeId)
        {
            // TODO(META-004): read base cost, growth and cap from a MetaUpgradeData asset. The
            // curve is balance data and must never be a literal here (SRS 35).
            return 0;
        }

        /// <summary>
        /// Buys one level. Fails when the currency is short (META-005) or the cap is reached.
        /// </summary>
        public bool TryPurchase(string upgradeId)
        {
            // TODO(META-005): refuse when _currency.TrySpend fails.
            // TODO(META-004): refuse when already at the level cap.
            // TODO(META-006): raise the level, save, and publish so the Hub UI refreshes.
            return false;
        }

        /// <summary>Applies purchased upgrades on top of the hero base stats at Run start.</summary>
        public void ApplyToHeroStats(ref Data.StatBlock stats)
        {
            // TODO(META-001): add the permanent HP bonus.
            // TODO(META-002): add the permanent Attack bonus.
            // TODO(META-003): add the permanent Crit bonus.
            // TODO(SRS-5): keep the totals inside the caps so meta growth does not break difficulty.
        }
    }
}
