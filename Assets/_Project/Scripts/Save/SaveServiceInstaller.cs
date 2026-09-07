using ChibiRift.Core;

namespace ChibiRift.Save
{
    /// <summary>
    /// Registers the save services on the composition root. Placed on a child of the
    /// GameBootstrap object in the Boot scene. Runs after telemetry so
    /// <see cref="SettingsManager"/> can push the TEL-004 toggle straight into it.
    /// </summary>
    public sealed class SaveServiceInstaller : ServiceInstaller
    {
        /// <inheritdoc />
        public override int Order => 20;

        /// <inheritdoc />
        public override void Install(ServiceLocator locator, EventBus eventBus)
        {
            var saveManager = new SaveManager();
            saveManager.Load(); // SAVE-001: progression is available before MainMenu appears.

            locator.Register<ISaveService>(saveManager);
            locator.Register(saveManager);

            var settings = new SettingsManager(
                saveManager,
                locator.Get<IAudioService>(),
                locator.Get<ITelemetryService>());

            settings.Apply();
            locator.Register(settings);
        }
    }
}
