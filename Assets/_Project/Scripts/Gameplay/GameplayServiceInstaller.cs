using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Registers the gameplay services on the composition root (SRS 26).
    /// </summary>
    /// <remarks>
    /// <see cref="GameBootstrap"/> lives in ChibiRift.Core, which references neither
    /// ChibiRift.Gameplay nor ChibiRift.Data, so it cannot construct <see cref="CombatSystem"/>
    /// or hold a <see cref="BalanceConfig"/>. That is exactly what <see cref="ServiceInstaller"/>
    /// exists for: the Boot scene carries this component and Core discovers it without ever naming
    /// this assembly.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class GameplayServiceInstaller : ServiceInstaller
    {
        [Tooltip("Supplies MinDamage, the DamageReduction cap and the crit multiplier (SRS 35).")]
        [SerializeField] private BalanceConfig _balanceConfig;

        [Tooltip("Seed for combat RNG. 0 asks for a time-based seed, so a Run varies (RNG-003).")]
        [SerializeField] private int _combatSeed;

        /// <summary>Runs after Save (20) and Meta (30) so it can read persisted settings later.</summary>
        public override int Order => 40;

        /// <inheritdoc />
        public override void Install(ServiceLocator locator, EventBus eventBus)
        {
            if (_balanceConfig == null)
            {
                GameLog.Error("Gameplay",
                    "GameplayServiceInstaller has no BalanceConfig; CombatSystem cannot resolve MinDamage (SRS 30).");
                return;
            }

            var random = new DeterministicRandom(_combatSeed != 0 ? _combatSeed : NewSeed());
            locator.Register(new CombatSystem(eventBus, _balanceConfig, random));
        }

        private static int NewSeed() => unchecked((int)System.DateTime.UtcNow.Ticks);
    }
}
