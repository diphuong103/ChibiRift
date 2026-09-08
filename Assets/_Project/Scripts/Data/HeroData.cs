using UnityEngine;

namespace ChibiRift.Data
{
    /// <summary>
    /// A playable hero (SRS 12). MVP ships one, but the schema supports many (HER-005),
    /// so nothing here may be hard-coded to the starting hero.
    /// </summary>
    [CreateAssetMenu(fileName = "HERO_", menuName = "ChibiRift/Hero Data", order = 10)]
    public sealed class HeroData : GameDataAsset
    {
        [Header("Base stats (HER-001, HER-006, HPS-009)")]
        [Tooltip("Includes Defense (flat armor) and DamageReduction, both mandatory (HPS-009).")]
        [SerializeField] private StatBlock _baseStats = StatBlock.PlayerBaseline;

        [Header("Dash (HER-006, MOV-006)")]
        [Tooltip("Distance, duration, i-frame window and cooldown. Cooldown 1.5s and i-frame 0.25s are SRS 35 baselines.")]
        [SerializeField] private DashConfig _dash = DashConfig.Baseline;

        [Header("Movement (MOV-001..MOV-004)")]
        [Tooltip("Acceleration, gravity, jump and ground-probe tuning. Every locomotion number lives here (SRS 35).")]
        [SerializeField] private MovementConfig _movement = MovementConfig.Baseline;

        [Tooltip("Jumps allowed before touching ground. 2 gives the double jump of MOV-003.")]
        [Min(1)]
        [SerializeField] private int _maxJumpCount = 2;

        [Header("Combat (COM-002, COM-003)")]
        [Tooltip("Hits in the basic attack chain. COM-002 requires 3.")]
        [Min(1)]
        [SerializeField] private int _comboLength = 3;

        [Tooltip("Seconds to input the next combo hit before the chain resets (COM-003). No SRS 35 baseline; set by the project owner (OI-04).")]
        [Min(0.01f)]
        [SerializeField] private float _comboWindow = 0.5f;

        [Tooltip("Seconds of invulnerability after taking a hit (HPS-005). No SRS 35 baseline; set by the project owner to 0.8 (OI-05).")]
        [Min(0f)]
        [SerializeField] private float _hurtIFrameDuration = 0.8f;

        [Header("Loadout (HER-002, HER-003)")]
        [Tooltip("Skills bound to Q / E / R (COM-007).")]
        [SerializeField] private SkillData[] _skills = new SkillData[0];

        [Tooltip("Hero-specific passive (HER-002).")]
        [SerializeField] private UpgradeData _passive;

        [Header("Presentation (HER-004)")]
        [Tooltip("Animator with Idle, Run, Jump, Attack1-3, Hurt, Death (HER-004).")]
        [SerializeField] private RuntimeAnimatorController _animatorController;

        [Tooltip("Portrait shown in Hub hero selection (HUB-003).")]
        [SerializeField] private Sprite _portrait;

        [Header("Unlock (HER-005)")]
        [Tooltip("Available without a meta purchase. MVP's single hero is unlocked by default.")]
        [SerializeField] private bool _unlockedByDefault = true;

        [Tooltip("Gem cost to unlock (SRS 16: Gem unlocks heroes).")]
        [Min(0)]
        [SerializeField] private int _gemUnlockCost = 0;

        /// <summary>Base stats including Defense and DamageReduction (HER-001, HPS-009).</summary>
        public StatBlock BaseStats => _baseStats;

        /// <summary>Dash tuning (HER-006, MOV-006).</summary>
        public DashConfig Dash => _dash;

        /// <summary>Locomotion tuning: acceleration, gravity, jump, ground probe (MOV-001..MOV-004).</summary>
        public MovementConfig Movement => _movement;

        /// <summary>Airborne jump allowance; 2 enables double jump (MOV-003).</summary>
        public int MaxJumpCount => _maxJumpCount;

        /// <summary>Hits in the basic chain (COM-002).</summary>
        public int ComboLength => _comboLength;

        /// <summary>Combo input window in seconds (COM-003).</summary>
        public float ComboWindow => _comboWindow;

        /// <summary>Post-hit invulnerability in seconds (HPS-005).</summary>
        public float HurtIFrameDuration => _hurtIFrameDuration;

        /// <summary>Skills for Q / E / R (HER-003).</summary>
        public SkillData[] Skills => _skills;

        /// <summary>Hero passive (HER-002).</summary>
        public UpgradeData Passive => _passive;

        /// <summary>Animator with the set required by HER-004.</summary>
        public RuntimeAnimatorController AnimatorController => _animatorController;

        /// <summary>Hub portrait (HUB-003).</summary>
        public Sprite Portrait => _portrait;

        /// <summary>Whether the hero needs unlocking (HER-005).</summary>
        public bool UnlockedByDefault => _unlockedByDefault;

        /// <summary>Gem cost when locked (SRS 16).</summary>
        public int GemUnlockCost => _gemUnlockCost;

        protected override void OnValidate()
        {
            base.OnValidate();

#if UNITY_EDITOR
            if (_comboLength != 3)
            {
                Core.GameLog.Warn(LogCategory,
                    $"'{name}' has ComboLength {_comboLength}; COM-002 specifies a 3 hit combo.");
            }
#endif
        }
    }
}
