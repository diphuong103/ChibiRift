using UnityEngine;

namespace ChibiRift.Core
{
    /// <summary>
    /// Extension point that lets assemblies outside Core contribute services to the composition
    /// root without Core ever referencing them. ChibiRift.Save, ChibiRift.Telemetry and
    /// ChibiRift.Meta each ship one installer; <see cref="GameBootstrap"/> discovers them on its
    /// own hierarchy in the Boot scene and runs them in <see cref="Order"/>.
    /// This is what keeps ChibiRift.Core dependency-free while still having a single root.
    /// </summary>
    public abstract class ServiceInstaller : MonoBehaviour
    {
        /// <summary>Lower runs first. Installers that depend on another must sort after it.</summary>
        public abstract int Order { get; }

        /// <summary>Registers this module's services on the shared locator.</summary>
        public abstract void Install(ServiceLocator locator, EventBus eventBus);
    }
}
