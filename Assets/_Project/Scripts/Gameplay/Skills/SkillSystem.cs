using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Owns the Q/E/R cooldowns (COM-007). MVP has no second resource: a skill is available
    /// when and only when its cooldown reaches zero (COM-008), so no energy or mana bar exists.
    /// </summary>
    public sealed class SkillSystem : IGameService
    {
        private readonly EventBus _eventBus;

        public SkillSystem(EventBus eventBus) => _eventBus = eventBus;

        /// <summary>Seconds left on a slot's cooldown. Zero means ready (COM-008).</summary>
        public float GetCooldownRemaining(SkillSlot slot)
        {
            // TODO(COM-008): return the remaining timer for the slot.
            return 0f;
        }

        /// <summary>Casts the skill in <paramref name="slot"/> when its cooldown allows.</summary>
        public bool TryCast(SkillSlot slot, UnityEngine.Vector2 aimDirection)
        {
            // TODO(COM-008): refuse the cast unless the cooldown is 0. There is no resource check.
            // TODO(COM-009): emit the hitbox or projectile along aimDirection.
            // TODO(COM-007): start SkillData.Cooldown and publish SkillCooldownChangedEvent.
            return false;
        }

        /// <summary>Advances every cooldown and notifies the HUD (SRS 19.2).</summary>
        public void Tick(float deltaTime)
        {
            // TODO(COM-008): decrement timers and publish SkillCooldownChangedEvent per slot.
        }
    }
}
