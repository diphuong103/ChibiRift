using UnityEngine;

namespace ChibiRift.Data
{
    /// <summary>
    /// One roguelite upgrade offered during a Run (SRS 11).
    /// MVP needs at least 12 valid upgrades plus the Fallback Pool (UPG-008, UPG-009).
    /// </summary>
    [CreateAssetMenu(fileName = "UPG_", menuName = "ChibiRift/Upgrade Data", order = 30)]
    public sealed class UpgradeData : GameDataAsset, IUpgradeEntry
    {
        [Header("Classification (UPG-002, UPG-004)")]
        [Tooltip("Upgrade family (SRS 11.1).")]
        [SerializeField] private UpgradeCategory _category = UpgradeCategory.Stat;

        [Tooltip("Rarity tier shown on the card.")]
        [SerializeField] private Rarity _rarity = Rarity.Common;

        [Tooltip("Relative draw weight. Higher is more likely (RNG-001).")]
        [Min(0f)]
        [SerializeField] private float _weight = 100f;

        [Tooltip("Icon on the level-up card (UPG-002).")]
        [SerializeField] private Sprite _icon;

        [Header("Stacking (UPG-005, UPG-010)")]
        [Tooltip("Whether picking this more than once is allowed.")]
        [SerializeField] private bool _isStackable = true;

        [Tooltip("Stack ceiling. At this many picks the upgrade leaves the pool (UPG-010).")]
        [Min(1)]
        [SerializeField] private int _maxStack = 5;

        [Header("Fallback Pool (UPG-009, RNG-005)")]
        [Tooltip("Fallback entries are repeatable, uncapped, and exempt from the anti-duplicate rule of EXP-006.")]
        [SerializeField] private bool _isFallback = false;

        [Header("Effect")]
        [Tooltip("Stat moved when Category is Stat.")]
        [SerializeField] private StatType _targetStat = StatType.Attack;

        [Tooltip("Additive amount applied per stack. Balance value, never a literal in code (SRS 35).")]
        [SerializeField] private float _flatAmount = 0f;

        [Tooltip("Multiplicative amount applied per stack, e.g. 0.1 for +10%.")]
        [SerializeField] private float _percentAmount = 0f;

        [Tooltip("Skill granted or upgraded when Category is ActiveSkill.")]
        [SerializeField] private SkillData _grantedSkill;

        [Header("Availability (UPG-003)")]
        [Tooltip("Hero level required before this can appear. 1 means always available.")]
        [Min(1)]
        [SerializeField] private int _minimumHeroLevel = 1;

        /// <inheritdoc />
        public UpgradeCategory Category => _category;

        /// <inheritdoc />
        public Rarity Rarity => _rarity;

        /// <inheritdoc />
        public float Weight => _weight;

        /// <inheritdoc />
        public bool IsStackable => _isStackable;

        /// <inheritdoc />
        public int MaxStack => _maxStack;

        /// <inheritdoc />
        public bool IsFallback => _isFallback;

        /// <summary>Card icon (UPG-002).</summary>
        public Sprite Icon => _icon;

        /// <summary>Stat moved by a Stat upgrade (SRS 11.1).</summary>
        public StatType TargetStat => _targetStat;

        /// <summary>Additive amount per stack.</summary>
        public float FlatAmount => _flatAmount;

        /// <summary>Multiplicative amount per stack.</summary>
        public float PercentAmount => _percentAmount;

        /// <summary>Skill granted when this is an ActiveSkill upgrade.</summary>
        public SkillData GrantedSkill => _grantedSkill;

        /// <summary>Level gate (UPG-003).</summary>
        public int MinimumHeroLevel => _minimumHeroLevel;

        protected override void OnValidate()
        {
            base.OnValidate();

#if UNITY_EDITOR
            // UPG-009: a Fallback entry must be repeatable and uncapped, otherwise it cannot
            // guarantee three cards when the main pool runs dry (EXP-009).
            if (_isFallback && !_isStackable)
            {
                _isStackable = true;
                Core.GameLog.Warn(LogCategory,
                    $"'{name}' is in the Fallback Pool, so IsStackable was forced on (UPG-009).");
            }

            if (_weight <= 0f)
            {
                Core.GameLog.Warn(LogCategory,
                    $"'{name}' has weight {_weight}; it can never be drawn (RNG-001).");
            }
#endif
        }
    }
}
