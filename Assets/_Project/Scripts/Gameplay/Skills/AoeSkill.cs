using UnityEngine;
using ChibiRift.Core;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Damages everything on the Enemy layer inside a radius around the caster (COM-007).
    /// Used by both E and R; they differ only in the asset they are cast with.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AoeSkill : MonoBehaviour, ISkillBehaviour
    {
        /// <summary>
        /// Cap on targets one blast reports. Sized from the performance budget so the sweep never
        /// allocates; a crowd larger than this simply loses the overflow, which is preferable to a
        /// per-cast allocation in the middle of the NFR-001 case.
        /// </summary>
        private Collider2D[] _overlapBuffer;

        private ContactFilter2D _enemyFilter;
        private CombatSystem _combat;
        private EventBus _eventBus;

        /// <summary>Radius of the last blast, in world units. For gizmos and tests.</summary>
        public float LastRadius { get; private set; }

        /// <summary>Targets the last blast actually hit.</summary>
        public int LastHitCount { get; private set; }

        /// <summary>Sets the sweep capacity. Called by the caster, which can read BalanceConfig.</summary>
        public void ConfigureCapacity(int capacity) => _overlapBuffer = new Collider2D[Mathf.Max(capacity, 1)];

        private void Awake()
        {
            _enemyFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = LayerMask.GetMask(GameLayers.Enemy),
                useTriggers = true
            };

            if (_overlapBuffer == null) ConfigureCapacity(1);
        }

        private void Start()
        {
            if (ServiceLocator.Current == null) return;

            ServiceLocator.Current.TryGet(out _combat);
            ServiceLocator.Current.TryGet(out _eventBus);
        }

        /// <inheritdoc />
        public void Cast(in SkillCastContext context)
        {
            if (_combat == null || context.Data == null) return;

            LastRadius = context.Data.Radius;
            LastHitCount = 0;

            int count = Physics2D.OverlapCircle(
                context.Origin, LastRadius, _enemyFilter, _overlapBuffer);

            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _overlapBuffer[i];
                if (hit == null) continue;

                var target = hit.GetComponentInParent<HealthComponent>();
                if (target == null || target.IsDead) continue;

                // Through the pipeline, never inline: the death guard, the crit roll and every
                // piece of hit feedback live there (HPS-003).
                _combat.DealDamage(
                    target,
                    context.BaseDamage,
                    context.DamageMultiplier,
                    context.CritChance,
                    DamageSource.Skill,
                    target.transform.position,
                    context.Origin);

                LastHitCount++;
            }
        }
    }
}
