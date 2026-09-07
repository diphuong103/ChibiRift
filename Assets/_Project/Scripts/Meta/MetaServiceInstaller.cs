using ChibiRift.Core;
using ChibiRift.Save;

namespace ChibiRift.Meta
{
    /// <summary>
    /// Registers the wallet and permanent progression. Runs last, because both depend on the
    /// <see cref="SaveManager"/> the save installer put in place.
    /// </summary>
    public sealed class MetaServiceInstaller : ServiceInstaller
    {
        /// <inheritdoc />
        public override int Order => 30;

        /// <inheritdoc />
        public override void Install(ServiceLocator locator, EventBus eventBus)
        {
            var save = locator.Get<SaveManager>();

            var currency = new CurrencyManager(save, eventBus);
            locator.Register(currency);

            locator.Register(new MetaProgressionManager(save, currency, eventBus));
        }
    }
}
