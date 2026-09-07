using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Basic attack, the three hit combo and the Q/E/R skills (SRS 8.2).
    /// Aim is the mouse vector with no auto-target (COM-009), and skills are gated by cooldown
    /// alone because MVP has no energy or mana pool (COM-008).
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    [DisallowMultipleComponent]
    public sealed class PlayerCombat : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("Supplies combo length, combo window and the skill list.")]
        [SerializeField] private HeroData _heroData;

        [Tooltip("Supplies crit multiplier, MinDamage and the DamageReduction cap.")]
        [SerializeField] private BalanceConfig _balanceConfig;

        [Header("Hitboxes (COM-004)")]
        [Tooltip("One hitbox per combo step, active only during that step's active frames.")]
        [SerializeField] private Hitbox[] _comboHitboxes;

        /// <summary>Current index in the chain, 0 based. Resets on timeout (COM-003).</summary>
        public int ComboIndex { get; private set; }

        /// <summary>Seconds left to chain the next hit before the combo resets (COM-003).</summary>
        public float ComboWindowRemaining { get; private set; }

        private void Update()
        {
            // TODO(COM-001): start an attack on IInputService.AttackPressed.
            // TODO(COM-002): advance Attack1 to Attack2 to Attack3 while within the combo window.
            // TODO(COM-003): reset ComboIndex when ComboWindowRemaining hits 0 or state is invalid.
            // TODO(COM-007): fire the Q/E/R skill whose cooldown has expired.
            // TODO(COM-009): orient the hitbox and any projectile along the mouse aim vector.
        }

        /// <summary>
        /// Resolves one hit through the shared pipeline. Damage is never computed inline:
        /// it always goes through <see cref="DamageCalculator"/> (HPS-003).
        /// </summary>
        private void ResolveHit(IDamageable target, float baseDamage, DamageSource source)
        {
            // TODO(COM-006): roll crit with DamageCalculator.RollCritical using the run's seeded RNG.
            // TODO(HPS-003): build the request with DamageCalculator.CreateRequest and apply the result.
            // TODO(COM-005): apply knockback and publish DamageAppliedEvent for hit feedback and HPS-008.
        }
    }
}
