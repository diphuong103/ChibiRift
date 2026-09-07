using System;
using UnityEngine;

namespace ChibiRift.Data
{
    /// <summary>Per-rarity draw weight multiplier, feeding the weighted roll of UPG-004 / RNG-001.</summary>
    [Serializable]
    public struct RarityWeight
    {
        [Tooltip("Rarity tier.")]
        public Rarity Rarity;

        [Tooltip("Multiplier applied on top of each upgrade's own weight.")]
        [Min(0f)]
        public float WeightMultiplier;
    }

    /// <summary>
    /// The single home for every tunable number (SRS 35: "Balance không hard-code trong script").
    /// Gameplay code reads values from here; a balance literal appearing in ChibiRift.Gameplay is
    /// a defect. Values carrying an explicit SRS 35 baseline are copied verbatim and must not be
    /// edited without updating the SRS; values SRS 35 only marks "Configurable" are provisional
    /// and listed in OPEN_ISSUES.md.
    /// </summary>
    [CreateAssetMenu(fileName = "BalanceConfig", menuName = "ChibiRift/Balance Config", order = 0)]
    public sealed class BalanceConfig : GameDataAsset
    {
        [Header("Damage pipeline (SRS 9, HPS-009, HPS-010)")]
        [Tooltip("SRS 35 baseline: 1. FinalDamage can never fall below this (HPS-010).")]
        [Min(0f)]
        [SerializeField] private float _minDamage = 1f;

        [Tooltip("SRS 35: DamageReduction is clamped to at most 0.8 (HPS-009).")]
        [Range(0f, 1f)]
        [SerializeField] private float _maxDamageReduction = 0.8f;

        [Tooltip("SRS 35 range is 1.5x-2.0x; 1.5 taken as the baseline. See OPEN_ISSUES.md.")]
        [Min(1f)]
        [SerializeField] private float _defaultCritMultiplier = 2f;

        [Header("Player baseline (SRS 35)")]
        [Tooltip("SRS 35 baseline: 100.")]
        [Min(1f)]
        [SerializeField] private float _playerBaseHealth = 100f;

        [Tooltip("SRS 35 baseline: 10.")]
        [Min(0f)]
        [SerializeField] private float _playerBaseAttack = 10f;

        [Tooltip("SRS 35 baseline: 5%.")]
        [Range(0f, 1f)]
        [SerializeField] private float _playerBaseCritChance = 0.05f;

        [Tooltip("SRS 35 baseline: 0 flat armor.")]
        [Min(0f)]
        [SerializeField] private float _playerBaseDefense = 0f;

        [Tooltip("SRS 35 baseline: 0% damage reduction.")]
        [Range(0f, 0.8f)]
        [SerializeField] private float _playerBaseDamageReduction = 0f;

        [Header("Dash (SRS 35, MOV-006)")]
        [Tooltip("SRS 35 baseline: 1.5s.")]
        [Min(0f)]
        [SerializeField] private float _dashCooldown = 1.5f;

        [Tooltip("SRS 35 baseline: 0.25s.")]
        [Min(0f)]
        [SerializeField] private float _dashIFrameDuration = 0.25f;

        [Header("Experience (SRS 10)")]
        [Tooltip("XPRequired(level) = BaseXP * GrowthFactor^(level-1). SRS 35 says Configurable only; provisional.")]
        [Min(1f)]
        [SerializeField] private float _baseExperience = 100f;

        [Tooltip("Growth factor of the XP curve. SRS 35 says Configurable only; provisional.")]
        [Min(1f)]
        [SerializeField] private float _experienceGrowthFactor = 1.4f;

        [Tooltip("Highest level the curve is defined for. Guards against runaway loops.")]
        [Min(1)]
        [SerializeField] private int _maxHeroLevel = 50;

        [Header("Upgrade roll (EXP-005, EXP-009, UPG-009)")]
        [Tooltip("Cards shown per Level Up. EXP-005 requires exactly 3.")]
        [Min(1)]
        [SerializeField] private int _upgradeChoiceCount = 3;

        [Tooltip("Per-rarity multiplier on top of each upgrade's weight (UPG-004, RNG-001).")]
        [SerializeField] private RarityWeight[] _rarityWeights = new RarityWeight[0];

        [Header("Elite (SRS 35, ELT-001)")]
        [Tooltip("SRS 35 baseline: HP x3.")]
        [Min(1f)]
        [SerializeField] private float _eliteHealthMultiplier = 3f;

        [Tooltip("SRS 35 baseline: Damage x1.5.")]
        [Min(1f)]
        [SerializeField] private float _eliteDamageMultiplier = 1.5f;

        [Header("Combat feel (SRS 21, SRS 35)")]
        [Tooltip("Combo input window in seconds. SRS 35 says Configurable only; provisional.")]
        [Min(0.01f)]
        [SerializeField] private float _defaultComboWindow = 0.5f;

        [Tooltip("Hit stop length in seconds. Bounded so no frame breaks the 100ms ceiling of NFR-002.")]
        [Range(0f, 0.1f)]
        [SerializeField] private float _hitStopDuration = 0.05f;

        [Tooltip("Default screen shake amplitude (CAM-003).")]
        [Min(0f)]
        [SerializeField] private float _screenShakeAmplitude = 1f;

        [Tooltip("Default screen shake duration in seconds (CAM-003).")]
        [Min(0f)]
        [SerializeField] private float _screenShakeDuration = 0.15f;

        // Damage pipeline
        /// <summary>Damage floor (HPS-010, SRS 35 baseline 1).</summary>
        public float MinDamage => _minDamage;

        /// <summary>Upper clamp on DamageReduction (HPS-009, SRS 35 value 0.8).</summary>
        public float MaxDamageReduction => _maxDamageReduction;

        /// <summary>Default crit multiplier (COM-006, SRS 35 baseline 1.5).</summary>
        public float DefaultCritMultiplier => _defaultCritMultiplier;

        // Player baseline
        /// <summary>SRS 35: Player Base HP 100.</summary>
        public float PlayerBaseHealth => _playerBaseHealth;

        /// <summary>SRS 35: Player Base Attack 10.</summary>
        public float PlayerBaseAttack => _playerBaseAttack;

        /// <summary>SRS 35: Crit Chance 5%.</summary>
        public float PlayerBaseCritChance => _playerBaseCritChance;

        /// <summary>SRS 35: Player Base Defense 0 flat armor.</summary>
        public float PlayerBaseDefense => _playerBaseDefense;

        /// <summary>SRS 35: Player Base 0% damage reduction.</summary>
        public float PlayerBaseDamageReduction => _playerBaseDamageReduction;

        // Dash
        /// <summary>SRS 35: Dash Cooldown 1.5s.</summary>
        public float DashCooldown => _dashCooldown;

        /// <summary>SRS 35: Dash I-Frame Duration 0.25s.</summary>
        public float DashIFrameDuration => _dashIFrameDuration;

        // Experience
        /// <summary>BaseXP term of the SRS 10 curve.</summary>
        public float BaseExperience => _baseExperience;

        /// <summary>GrowthFactor term of the SRS 10 curve.</summary>
        public float ExperienceGrowthFactor => _experienceGrowthFactor;

        /// <summary>Level ceiling the curve is defined for.</summary>
        public int MaxHeroLevel => _maxHeroLevel;

        // Upgrade roll
        /// <summary>Cards per Level Up (EXP-005: 3).</summary>
        public int UpgradeChoiceCount => _upgradeChoiceCount;

        /// <summary>Per-rarity weight multipliers (UPG-004, RNG-001).</summary>
        public RarityWeight[] RarityWeights => _rarityWeights;

        // Elite
        /// <summary>SRS 35: Elite HP multiplier x3.</summary>
        public float EliteHealthMultiplier => _eliteHealthMultiplier;

        /// <summary>SRS 35: Elite Damage multiplier x1.5.</summary>
        public float EliteDamageMultiplier => _eliteDamageMultiplier;

        // Combat feel
        /// <summary>Combo input window (COM-003).</summary>
        public float DefaultComboWindow => _defaultComboWindow;

        /// <summary>Hit stop length (SRS 21).</summary>
        public float HitStopDuration => _hitStopDuration;

        /// <summary>Screen shake amplitude (CAM-003).</summary>
        public float ScreenShakeAmplitude => _screenShakeAmplitude;

        /// <summary>Screen shake duration (CAM-003).</summary>
        public float ScreenShakeDuration => _screenShakeDuration;

        /// <summary>
        /// Weight multiplier for <paramref name="rarity"/>, or 1 when unconfigured, so a missing
        /// row degrades to "no adjustment" instead of removing the upgrade from the pool.
        /// </summary>
        public float GetRarityWeightMultiplier(Rarity rarity)
        {
            if (_rarityWeights == null) return 1f;

            for (int i = 0; i < _rarityWeights.Length; i++)
            {
                if (_rarityWeights[i].Rarity == rarity) return _rarityWeights[i].WeightMultiplier;
            }
            return 1f;
        }

        protected override void OnValidate()
        {
            base.OnValidate();

#if UNITY_EDITOR
            if (!Mathf.Approximately(_maxDamageReduction, 0.8f))
            {
                Core.GameLog.Warn(LogCategory,
                    $"MaxDamageReduction is {_maxDamageReduction}; SRS 35 and HPS-009 specify 0.8.");
            }

            if (_upgradeChoiceCount != 3)
            {
                Core.GameLog.Warn(LogCategory,
                    $"UpgradeChoiceCount is {_upgradeChoiceCount}; EXP-005 requires exactly 3.");
            }
#endif
        }
    }
}
