using UnityEngine;
using ChibiRift.Core;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// An attack volume that only deals damage during its active frames (COM-004).
    /// Sits on the PlayerHitbox or EnemyHitbox layer, so the collision matrix alone decides
    /// what it can reach and no filtering logic is needed here.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public sealed class Hitbox : MonoBehaviour
    {
        [Tooltip("Where damage from this hitbox is attributed, for the per-source log (TEL-003).")]
        [SerializeField] private DamageSource _source = DamageSource.BasicAttack;

        [Tooltip("Damage multiplier for this combo step, relative to the attacker's Attack stat.")]
        [Min(0f)]
        [SerializeField] private float _damageMultiplier = 1f;

        /// <summary>True only during the active frames of the attack (COM-004).</summary>
        public bool IsActive { get; private set; }

        /// <summary>Attribution for telemetry (TEL-003).</summary>
        public DamageSource Source => _source;

        /// <summary>Damage scale for this combo step.</summary>
        public float DamageMultiplier => _damageMultiplier;

        /// <summary>Opens the hitbox for the active frames of the current attack (COM-004).</summary>
        public void Activate()
        {
            // TODO(COM-004): enable the collider and clear the per-swing hit set so one swing
            // cannot damage the same target twice.
        }

        /// <summary>Closes the hitbox at the end of the active frames (COM-004).</summary>
        public void Deactivate()
        {
            // TODO(COM-004): disable the collider.
        }
    }
}
