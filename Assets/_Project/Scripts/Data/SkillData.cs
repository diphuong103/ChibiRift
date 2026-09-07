using UnityEngine;
using ChibiRift.Core;

namespace ChibiRift.Data
{
    /// <summary>
    /// One Q/E/R skill (COM-007). MVP skills are gated by cooldown only: there is no energy or
    /// mana pool and no second resource bar (COM-008).
    /// </summary>
    [CreateAssetMenu(fileName = "SKL_", menuName = "ChibiRift/Skill Data", order = 20)]
    public sealed class SkillData : GameDataAsset
    {
        [Header("Binding (COM-007)")]
        [Tooltip("Which of Q / E / R triggers this skill.")]
        [SerializeField] private SkillSlot _slot = SkillSlot.Skill1;

        [Tooltip("Delivery shape (SRS 11.1).")]
        [SerializeField] private SkillType _type = SkillType.Melee;

        [Header("Balance (SRS 35: all values configurable)")]
        [Tooltip("Base damage before attack modifiers. Feeds step 1 of the SRS 9 formula.")]
        [Min(0f)]
        [SerializeField] private float _baseDamage = 0f;

        [Tooltip("Seconds before the skill is usable again. The only gate in MVP (COM-008).")]
        [Min(0f)]
        [SerializeField] private float _cooldown = 5f;

        [Tooltip("Seconds the effect persists, for lingering AoE or buffs.")]
        [Min(0f)]
        [SerializeField] private float _duration = 0f;

        [Tooltip("Effect radius in world units, for AoE skills.")]
        [Min(0f)]
        [SerializeField] private float _radius = 0f;

        [Tooltip("Projectiles emitted per cast. Raised by ModifierSynergy upgrades (SRS 11.1).")]
        [Min(1)]
        [SerializeField] private int _projectileCount = 1;

        [Header("Progression (SRS 43 Q7)")]
        [Tooltip("Stack ceiling. MVP keeps skill levelling to a simple stack count.")]
        [Min(1)]
        [SerializeField] private int _maxLevel = 5;

        [Tooltip("Rarity when this skill is offered as an upgrade card (UPG-002).")]
        [SerializeField] private Rarity _rarity = Rarity.Common;

        [Header("Presentation")]
        [Tooltip("HUD cooldown icon (SRS 19.2).")]
        [SerializeField] private Sprite _icon;

        /// <summary>Q, E or R (COM-007).</summary>
        public SkillSlot Slot => _slot;

        /// <summary>Delivery shape (SRS 11.1).</summary>
        public SkillType Type => _type;

        /// <summary>Base damage fed into the damage pipeline (HPS-003).</summary>
        public float BaseDamage => _baseDamage;

        /// <summary>Cooldown in seconds. The only activation gate in MVP (COM-008).</summary>
        public float Cooldown => _cooldown;

        /// <summary>Effect lifetime in seconds.</summary>
        public float Duration => _duration;

        /// <summary>Effect radius in world units.</summary>
        public float Radius => _radius;

        /// <summary>Projectiles per cast.</summary>
        public int ProjectileCount => _projectileCount;

        /// <summary>Stack ceiling (SRS 43 Q7).</summary>
        public int MaxLevel => _maxLevel;

        /// <summary>Rarity when offered as a card.</summary>
        public Rarity Rarity => _rarity;

        /// <summary>HUD icon.</summary>
        public Sprite Icon => _icon;
    }
}
