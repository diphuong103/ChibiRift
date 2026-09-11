using UnityEngine;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// What a skill actually does when it fires (COM-007).
    /// </summary>
    /// <remarks>
    /// P1 ships three skills and two behaviours: Q is a projectile, E and R are the same area
    /// behaviour with different radius, damage and cooldown. Writing one class per skill would make
    /// the fourth skill a new class instead of a new asset, which is the opposite of what SRS 23
    /// asks of content.
    ///
    /// <para>A behaviour never computes damage. It finds targets and hands them to
    /// <c>CombatSystem.DealDamage</c>, the single pipeline (HPS-003).</para>
    /// </remarks>
    public interface ISkillBehaviour
    {
        /// <summary>Fires the skill described by <paramref name="context"/>.</summary>
        void Cast(in SkillCastContext context);
    }

    /// <summary>Everything a behaviour needs to fire one cast, gathered by the caster.</summary>
    public readonly struct SkillCastContext
    {
        /// <summary>The skill asset. Every number comes from here (NFR-007).</summary>
        public readonly SkillData Data;

        /// <summary>Where the cast starts, normally the caster's position.</summary>
        public readonly Vector2 Origin;

        /// <summary>Normalised aim direction, from the cursor (COM-009).</summary>
        public readonly Vector2 Direction;

        /// <summary>Caster's Attack stat; the multiplier in <see cref="Data"/> scales it.</summary>
        public readonly float AttackStat;

        /// <summary>Caster's crit chance, 0..1 (COM-006).</summary>
        public readonly float CritChance;

        public SkillCastContext(
            SkillData data, Vector2 origin, Vector2 direction, float attackStat, float critChance)
        {
            Data = data;
            Origin = origin;
            Direction = direction;
            AttackStat = attackStat;
            CritChance = critChance;
        }

        /// <summary>
        /// Damage before the target's armour. Flat damage wins when an asset sets it; otherwise the
        /// hit scales off Attack like the combo does. P1's three skills all use the multiplier and
        /// leave flat damage at zero (see <see cref="SkillData.BaseDamage"/>).
        /// </summary>
        public float BaseDamage => Data.BaseDamage > 0f ? Data.BaseDamage : AttackStat;

        /// <summary>Multiplier applied to <see cref="BaseDamage"/>. One when flat damage is used.</summary>
        public float DamageMultiplier => Data.BaseDamage > 0f ? 1f : Data.DamageMultiplier;
    }
}
