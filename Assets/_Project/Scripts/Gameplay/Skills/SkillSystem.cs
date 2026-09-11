using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Owns the Q/E/R cooldowns and casts them (COM-007, COM-008).
    /// </summary>
    /// <remarks>
    /// <para><b>A MonoBehaviour, not a service.</b> It was written as an <c>IGameService</c> in the
    /// foundation slice and never registered or called, because casting needs things only a scene
    /// has: the caster's position, the aim vector, a projectile pool. Splitting it into a service
    /// for the timers plus a component for the casting would leave two places holding cooldown, so
    /// it is one component on the hero. The name is kept because SRS 26 and TRACEABILITY use it.</para>
    ///
    /// <para><b>MVP has no second resource.</b> A skill is available when and only when its
    /// cooldown reaches zero (COM-008). There is no energy or mana anywhere in the project, by
    /// construction rather than by omission.</para>
    ///
    /// <para><b>Everything else is borrowed, not rebuilt.</b> Damage goes through
    /// <c>CombatSystem</c>; aim comes from <c>PlayerCombat</c>, which already owns the cursor
    /// vector (COM-009); hit stop, shake, flash and audio all follow from
    /// <c>DamageAppliedEvent</c> without this class knowing they exist.</para>
    /// </remarks>
    [RequireComponent(typeof(PlayerStats))]
    [DisallowMultipleComponent]
    public sealed class SkillSystem : MonoBehaviour
    {
        /// <summary>Q, E and R, in that order.</summary>
        private static readonly SkillSlot[] Slots =
        {
            SkillSlot.Skill1,
            SkillSlot.Skill2,
            SkillSlot.Ultimate
        };

        [Header("Data")]
        [Tooltip("One asset per slot, in Q / E / R order (COM-007).")]
        [SerializeField] private SkillData[] _skills = new SkillData[Slots.Length];

        [Tooltip("Supplies the input buffer window and the sweep budget.")]
        [SerializeField] private BalanceConfig _balanceConfig;

        [Header("Behaviours")]
        [Tooltip("Fires projectile skills. Q uses this.")]
        [SerializeField] private ProjectileSkill _projectileBehaviour;

        [Tooltip("Fires area skills. E and R both use this, with different assets.")]
        [SerializeField] private AoeSkill _areaBehaviour;

        /// <summary>Seconds left on a slot's cooldown. Zero means ready (COM-008).</summary>
        public float GetCooldownRemaining(SkillSlot slot) => _cooldowns[IndexOf(slot)];

        /// <summary>The asset bound to a slot, or null when the slot is empty.</summary>
        public SkillData GetSkill(SkillSlot slot) => _skills[IndexOf(slot)];

        /// <summary>True when a press would cast right now (COM-008).</summary>
        public bool CanCast(SkillSlot slot)
        {
            if (GetSkill(slot) == null) return false;
            if (_cooldowns[IndexOf(slot)] > 0f) return false;

            // A dash is a commitment; so is the tail of a swing. Letting a skill interrupt either
            // would make both cancellable, which removes the cost that makes them decisions.
            if (_motor != null && _motor.IsDashing) return false;
            if (_combat != null && _combat.State == PlayerCombat.CombatState.Attacking) return false;

            return !_health.IsDead;
        }

        private float BufferSeconds => _balanceConfig != null ? _balanceConfig.SkillBufferSeconds : 0f;

        private readonly float[] _cooldowns = new float[Slots.Length];
        private readonly InputBuffer[] _buffers = new InputBuffer[Slots.Length];

        private PlayerStats _stats;
        private PlayerMotor _motor;
        private PlayerCombat _combat;
        private HealthComponent _health;
        private EventBus _eventBus;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _motor = GetComponent<PlayerMotor>();
            _combat = GetComponent<PlayerCombat>();
            _health = GetComponent<HealthComponent>();

            if (_areaBehaviour != null && _balanceConfig != null)
                _areaBehaviour.ConfigureCapacity(_balanceConfig.MaxTargetsPerSweep);
        }

        private void Start()
        {
            if (ServiceLocator.Current != null) ServiceLocator.Current.TryGet(out _eventBus);

            // Publish the starting state so the HUD shows three ready slots rather than nothing
            // until the first cast.
            for (int i = 0; i < Slots.Length; i++) PublishCooldown(i);
        }

        /// <summary>
        /// Remembers a press (P1-07). Buffered rather than acted on at once, so a press during hit
        /// stop, or a few frames before the cooldown expires, still counts.
        /// </summary>
        public void RequestCast(SkillSlot slot) => _buffers[IndexOf(slot)].Press(BufferSeconds);

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            for (int i = 0; i < Slots.Length; i++)
            {
                if (_cooldowns[i] > 0f)
                {
                    _cooldowns[i] = Mathf.Max(_cooldowns[i] - dt, 0f);
                    PublishCooldown(i);
                }

                _buffers[i].Tick(dt);

                if (!_buffers[i].HasPending) continue;
                if (!CanCast(Slots[i])) continue;
                if (!_buffers[i].TryConsume()) continue;

                Cast(i);
            }
        }

        private void Cast(int index)
        {
            SkillData data = _skills[index];

            ISkillBehaviour behaviour = data.Type == SkillType.Projectile
                ? (ISkillBehaviour)_projectileBehaviour
                : _areaBehaviour;

            if (behaviour == null)
            {
                GameLog.Error("Skills", $"{data.name} needs a {data.Type} behaviour and none is assigned.");
                return;
            }

            Vector2 aim = _combat != null ? _combat.AimDirection : Vector2.right;

            behaviour.Cast(new SkillCastContext(
                data, transform.position, aim, _stats.Attack, _stats.CritChance));

            _cooldowns[index] = data.Cooldown;
            PublishCooldown(index);

            // TODO(COM-007): honour SkillData.Windup once the skills have telegraph animations.
            // The field is authored and covered by tests; nothing reads it yet, and adding a bare
            // delay without a visual would only make the skill feel unresponsive.
        }

        private void PublishCooldown(int index)
        {
            SkillData data = _skills[index];
            float total = data != null ? data.Cooldown : 0f;

            _eventBus?.Publish(new SkillCooldownChangedEvent(Slots[index], _cooldowns[index], total));
        }

        private static int IndexOf(SkillSlot slot)
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i] == slot) return i;
            }

            return 0;
        }
    }
}
