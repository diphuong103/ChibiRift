using ChibiRift.Core;

namespace ChibiRift.Telemetry
{
    /// <summary>
    /// Registers telemetry on the composition root. Runs before the save installer, because
    /// <c>SettingsManager</c> pushes the TEL-004 toggle into this service as it starts.
    /// </summary>
    public sealed class TelemetryServiceInstaller : ServiceInstaller
    {
        /// <inheritdoc />
        public override int Order => 10;

        /// <inheritdoc />
        public override void Install(ServiceLocator locator, EventBus eventBus)
        {
            var telemetry = new TelemetryService();
            locator.Register<ITelemetryService>(telemetry);
            locator.Register(telemetry);
        }
    }
}
