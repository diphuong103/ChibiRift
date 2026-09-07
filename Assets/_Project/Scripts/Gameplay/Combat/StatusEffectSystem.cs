using ChibiRift.Core;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Damage-over-time and reactive passives from SRS 11.1: Burn, Bleed, Poison, Thorns,
    /// Lifesteal. Ticks route through <see cref="CombatSystem"/> so HPS-003 is never bypassed.
    /// </summary>
    public sealed class StatusEffectSystem : IGameService
    {
        private readonly CombatSystem _combat;

        public StatusEffectSystem(CombatSystem combat) => _combat = combat;

        /// <summary>Attaches or refreshes a status effect on a target (SRS 11.1).</summary>
        public void ApplyEffect(IDamageable target, string effectId, float duration, float magnitude)
        {
            // TODO(SRS-11.1): stack or refresh by effectId; duration and magnitude come from
            // UpgradeData, never from literals here (SRS 35).
        }

        /// <summary>Advances every active effect. Throttled rather than run per enemy per frame (SRS 29).</summary>
        public void Tick(float deltaTime)
        {
            // TODO(SRS-11.1): tick timers and route damage through CombatSystem with
            // DamageSource.Passive so the per-source telemetry of TEL-003 stays accurate.
        }
    }
}
