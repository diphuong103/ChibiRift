using UnityEngine;

namespace ChibiRift.Data
{
    /// <summary>
    /// One elite modifier (ELT-001, ELT-002). MVP ships Shielded, Enraged and Explosive.
    /// Stat multipliers here must respect the caps of ELT-005.
    /// </summary>
    [CreateAssetMenu(fileName = "ELT_", menuName = "ChibiRift/Elite Modifier Data", order = 50)]
    public sealed class EliteModifierData : GameDataAsset
    {
        [Header("Behaviour (ELT-002)")]
        [Tooltip("Which of the three MVP modifiers this asset configures.")]
        [SerializeField] private EliteModifierType _modifier = EliteModifierType.Shielded;

        [Header("Shielded")]
        [Tooltip("Shield pool absorbed before HP is touched.")]
        [Min(0f)]
        [SerializeField] private float _shieldAmount = 0f;

        [Header("Enraged (ELT-002: triggers at HP <= 50%)")]
        [Tooltip("HP fraction at which enrage triggers. ELT-002 specifies 0.5.")]
        [Range(0f, 1f)]
        [SerializeField] private float _enrageHealthThreshold = 0.5f;

        [Tooltip("Move speed multiplier once enraged.")]
        [Min(1f)]
        [SerializeField] private float _enrageSpeedMultiplier = 1f;

        [Tooltip("Damage multiplier once enraged.")]
        [Min(1f)]
        [SerializeField] private float _enrageDamageMultiplier = 1f;

        [Header("Explosive")]
        [Tooltip("Area damage dealt on death.")]
        [Min(0f)]
        [SerializeField] private float _explosionDamage = 0f;

        [Tooltip("Explosion radius in world units.")]
        [Min(0f)]
        [SerializeField] private float _explosionRadius = 0f;

        [Header("Visual identity (ELT-003)")]
        [Tooltip("Aura tint applied so an elite is recognisable at gameplay distance.")]
        [SerializeField] private Color _auraTint = Color.white;

        [Tooltip("Scale multiplier reinforcing the elite silhouette.")]
        [Min(0.1f)]
        [SerializeField] private float _scaleMultiplier = 1.15f;

        /// <summary>Which modifier this is (ELT-002).</summary>
        public EliteModifierType Modifier => _modifier;

        /// <summary>Shield pool for Shielded elites.</summary>
        public float ShieldAmount => _shieldAmount;

        /// <summary>HP fraction that triggers enrage (ELT-002).</summary>
        public float EnrageHealthThreshold => _enrageHealthThreshold;

        /// <summary>Speed multiplier once enraged.</summary>
        public float EnrageSpeedMultiplier => _enrageSpeedMultiplier;

        /// <summary>Damage multiplier once enraged.</summary>
        public float EnrageDamageMultiplier => _enrageDamageMultiplier;

        /// <summary>Area damage on death for Explosive elites.</summary>
        public float ExplosionDamage => _explosionDamage;

        /// <summary>Explosion radius for Explosive elites.</summary>
        public float ExplosionRadius => _explosionRadius;

        /// <summary>Aura tint (ELT-003).</summary>
        public Color AuraTint => _auraTint;

        /// <summary>Scale multiplier (ELT-003).</summary>
        public float ScaleMultiplier => _scaleMultiplier;
    }
}
