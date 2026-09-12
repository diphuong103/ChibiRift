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
    /// edited without updating the SRS; values SRS 35 only marks "Configurable" were fixed by
    /// the project owner and are recorded in OPEN_ISSUES.md.
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
        [Tooltip("XPRequired(level) = BaseXP * GrowthFactor^(level-1). SRS 35 says Configurable only; set by the project owner (OI-02).")]
        [Min(1f)]
        [SerializeField] private float _baseExperience = 100f;

        [Tooltip("Growth factor of the XP curve. SRS 35 says Configurable only; set by the project owner (OI-02).")]
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
        [Tooltip("Combo input window in seconds. SRS 35 says Configurable only; set by the project owner (OI-04).")]
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

        [Header("AI throttling (SRS 29)")]
        [Tooltip("Seconds between enemy AI evaluations. Throttling keeps 30 concurrent enemies inside the NFR-001 budget.")]
        [Min(0f)]
        [SerializeField] private float _enemyThinkInterval = 0.1f;

        [Header("Knockback (COM-005)")]
        [Tooltip("Horizontal speed given to an enemy when it is hit. Enemies are pushed harder than the hero so a hit reads clearly on a crowd.")]
        [Min(0f)]
        [SerializeField] private float _enemyKnockbackForce = 6f;

        [Tooltip("Seconds an enemy's own movement is suspended while knockback plays out.")]
        [Min(0f)]
        [SerializeField] private float _enemyKnockbackDuration = 0.15f;

        [Tooltip("Horizontal speed given to the hero when hit. Lower than the enemy figure: losing control of the hero feels far worse than seeing an enemy shoved.")]
        [Min(0f)]
        [SerializeField] private float _heroKnockbackForce = 4f;

        [Tooltip("Seconds the hero's input is ignored while knockback plays out. Deliberately the shortest of the four.")]
        [Min(0f)]
        [SerializeField] private float _heroKnockbackDuration = 0.12f;

        [Header("Enemy HUD")]
        [Tooltip("Seconds an enemy health bar stays visible after the last hit before it fades out again. Not in SRS 35; set by the project owner.")]
        [Min(0f)]
        [SerializeField] private float _enemyHealthBarHideDelay = 3f;

        [Header("Pooling (SRS 29, AI-006)")]
        [Tooltip("Enemies created before play starts. Nothing may be instantiated during a wave (SRS 29).")]
        [Min(0)]
        [SerializeField] private int _enemyPoolPrewarm = 16;

        [Tooltip("Hard ceiling on pooled enemies. NFR-001 caps 30 alive at once; the margin above that catches a leak instead of letting it grow unbounded.")]
        [Min(1)]
        [SerializeField] private int _enemyPoolMax = 48;

        [Tooltip("Projectiles created before play starts.")]
        [Min(0)]
        [SerializeField] private int _projectilePoolPrewarm = 24;

        [Tooltip("Hard ceiling on pooled projectiles. Without one, a bug that fires every frame would allocate until the process died.")]
        [Min(1)]
        [SerializeField] private int _projectilePoolMax = 64;

        [Header("Performance budget (NFR-001, NFR-002)")]
        [Tooltip("Concurrent SFX voices. Above the 30 concurrent enemies of NFR-001 so a busy wave never silences a cue, and fixed so playing a sound allocates nothing.")]
        [Min(1)]
        [SerializeField] private int _sfxVoiceCount = 16;

        [Tooltip("Upper bound on colliders one attack sweep reports. Sized for the NFR-001 crowd overlapping one hitbox, and fixed so the sweep allocates nothing per frame.")]
        [Min(1)]
        [SerializeField] private int _maxTargetsPerSweep = 16;

        [Header("Profiler harness (NFR-001, NFR-002)")]
        [Tooltip("Enemies the frame-time harness spawns. Matches the NFR-001 concurrency cap.")]
        [Min(1)]
        [SerializeField] private int _stressEnemyCount = 30;

        [Tooltip("Seconds the harness samples for. Long enough for a GC spike to show up in p99.")]
        [Min(1f)]
        [SerializeField] private float _stressDurationSeconds = 10f;

        [Tooltip("Managed heap growth budget for one harness run, in kilobytes. Measured 1700-3640 KB across repeated 30-enemy/10s runs (OI-32) — almost none of it attributable to ChibiRift's own scripts (every instrumented gameplay call site totalled under 60 KB), and an A/B experiment ruled out crowding/contact generation too (idle, non-overlapping enemies allocated just as much as ones chasing and clustering); the remainder is scoped to running 30 active enemy instances, not to what they do. Set to ~1.4x the top of that measured range (README section 8: raise this only in the commit that legitimately needs it, with the new range in the message) — wide enough to absorb run-to-run noise, narrow enough that a P2 system tripling the allocation rate still gets caught instead of hiding inside a budget nobody has looked at since.")]
        [Min(1f)]
        [SerializeField] private float _stressAllocationBudgetKilobytes = 5000f;

        [Tooltip("Seconds a skill press is remembered (P1-07). Its own field, like the jump, dash and attack buffers, so retuning one cannot silently change how the others feel.")]
        [Min(0f)]
        [SerializeField] private float _skillBufferSeconds = 0.12f;

        [Header("Development tools")]
        [Tooltip("Radius around the hero that the F2/F3 debug spawn keys scatter enemies into.")]
        [Min(0f)]
        [SerializeField] private float _debugSpawnRadius = 8f;

        [Header("Game feel (SRS 21, CAM-003)")]
        [Tooltip("How long the game freezes per weight of hit. The main thing separating combo step 1 from step 3.")]
        [SerializeField] private HitStopConfig _hitStop = HitStopConfig.Baseline;

        [Tooltip("Screen shake per weight of hit (CAM-003).")]
        [SerializeField] private ShakeConfig _shake = ShakeConfig.Baseline;

        [Tooltip("Flash, impact particles and the dash trail (SRS 21, MOV-006).")]
        [SerializeField] private ImpactConfig _impact = ImpactConfig.Baseline;

        [Header("Hit feedback (HPS-005)")]
        [Tooltip("Sprite flashes per second while the hero is invulnerable, so the state is visible without reading a number.")]
        [Min(0.1f)]
        [SerializeField] private float _hurtFlashesPerSecond = 8f;

        [Tooltip("Alpha at the dimmest point of that flash. Never fully transparent, so the hero stays trackable in a crowd.")]
        [Range(0f, 1f)]
        [SerializeField] private float _hurtFlashMinAlpha = 0.25f;

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

        /// <summary>Seconds between enemy AI evaluations (SRS 29).</summary>
        public float EnemyThinkInterval => _enemyThinkInterval;

        /// <summary>Horizontal knockback speed applied to a struck enemy (COM-005).</summary>
        public float EnemyKnockbackForce => _enemyKnockbackForce;

        /// <summary>Seconds an enemy's movement is suspended by knockback (COM-005).</summary>
        public float EnemyKnockbackDuration => _enemyKnockbackDuration;

        /// <summary>Horizontal knockback speed applied to the struck hero (COM-005).</summary>
        public float HeroKnockbackForce => _heroKnockbackForce;

        /// <summary>Seconds the hero's input is suspended by knockback (COM-005).</summary>
        public float HeroKnockbackDuration => _heroKnockbackDuration;

        /// <summary>Seconds an enemy health bar lingers after the last hit.</summary>
        public float EnemyHealthBarHideDelay => _enemyHealthBarHideDelay;

        /// <summary>Enemies created before play starts (SRS 29).</summary>
        public int EnemyPoolPrewarm => _enemyPoolPrewarm;

        /// <summary>Ceiling on pooled enemies (SRS 29).</summary>
        public int EnemyPoolMax => _enemyPoolMax;

        /// <summary>Projectiles created before play starts (SRS 29).</summary>
        public int ProjectilePoolPrewarm => _projectilePoolPrewarm;

        /// <summary>Ceiling on pooled projectiles (SRS 29).</summary>
        public int ProjectilePoolMax => _projectilePoolMax;

        /// <summary>Concurrent SFX voices (NFR-001).</summary>
        public int SfxVoiceCount => _sfxVoiceCount;

        /// <summary>Upper bound on colliders one attack sweep reports (NFR-001).</summary>
        public int MaxTargetsPerSweep => _maxTargetsPerSweep;

        /// <summary>Enemies the frame-time harness spawns (NFR-001).</summary>
        public int StressEnemyCount => _stressEnemyCount;

        /// <summary>Seconds the frame-time harness samples for (NFR-002).</summary>
        public float StressDurationSeconds => _stressDurationSeconds;

        /// <summary>Managed heap growth budget for one harness run, in kilobytes (NFR-002).</summary>
        public float StressAllocationBudgetKilobytes => _stressAllocationBudgetKilobytes;

        /// <summary>Seconds a skill press is remembered (P1-07).</summary>
        public float SkillBufferSeconds => _skillBufferSeconds;

        /// <summary>Scatter radius for the debug spawn keys.</summary>
        public float DebugSpawnRadius => _debugSpawnRadius;

        /// <summary>Freeze durations per weight of hit (SRS 21).</summary>
        public HitStopConfig HitStop => _hitStop;

        /// <summary>Screen shake per weight of hit (CAM-003).</summary>
        public ShakeConfig Shake => _shake;

        /// <summary>Flash, impact particles and dash trail (SRS 21, MOV-006).</summary>
        public ImpactConfig Impact => _impact;

        /// <summary>Invulnerability flashes per second (HPS-005).</summary>
        public float HurtFlashesPerSecond => _hurtFlashesPerSecond;

        /// <summary>Dimmest alpha of the invulnerability flash (HPS-005).</summary>
        public float HurtFlashMinAlpha => _hurtFlashMinAlpha;

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
