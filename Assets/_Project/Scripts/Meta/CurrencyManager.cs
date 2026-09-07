using ChibiRift.Core;
using ChibiRift.Save;

namespace ChibiRift.Meta
{
    /// <summary>
    /// The meta wallet (SRS 16). Gold and Gems both persist across Runs and are never lost on
    /// death; the entire Run haul arrives here once, at Post-Run (RUN-006, SRS 34).
    /// </summary>
    public sealed class CurrencyManager : IGameService
    {
        private readonly SaveManager _save;
        private readonly EventBus _eventBus;

        public CurrencyManager(SaveManager save, EventBus eventBus)
        {
            _save = save;
            _eventBus = eventBus;
        }

        /// <summary>Gold held (HUB-001).</summary>
        public int Gold => _save.Data.Gold;

        /// <summary>Gems held (HUB-001).</summary>
        public int Gems => _save.Data.Gems;

        /// <summary>Balance of one currency.</summary>
        public int GetBalance(CurrencyType currency)
            => currency == CurrencyType.Gold ? Gold : Gems;

        /// <summary>Credits currency and notifies the HUD and Hub (SRS 16, RUN-006).</summary>
        public void Add(CurrencyType currency, int amount)
        {
            if (amount <= 0) return;

            if (currency == CurrencyType.Gold) _save.Data.Gold += amount;
            else _save.Data.Gems += amount;

            _eventBus.Publish(new CurrencyChangedEvent(currency, amount, GetBalance(currency), true));
        }

        /// <summary>
        /// Spends currency on a permanent upgrade. Refuses when the balance is short, which is
        /// exactly what META-005 and SRS 34 require.
        /// </summary>
        public bool TrySpend(CurrencyType currency, int amount)
        {
            if (amount <= 0) return false;
            if (GetBalance(currency) < amount) return false; // META-005

            if (currency == CurrencyType.Gold) _save.Data.Gold -= amount;
            else _save.Data.Gems -= amount;

            _eventBus.Publish(new CurrencyChangedEvent(currency, -amount, GetBalance(currency), true));
            return true;
        }
    }
}
