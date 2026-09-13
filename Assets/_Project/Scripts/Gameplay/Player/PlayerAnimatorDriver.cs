using UnityEngine;
using ChibiRift.Core;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Pushes gameplay state onto the hero's <see cref="Animator"/> parameters (P2 slice 1: art
    /// pipeline, A3). One direction only: this reads events and writes Animator parameters, and
    /// writes nothing back into <see cref="PlayerMotor"/>, <see cref="PlayerCombat"/> or
    /// <see cref="HealthComponent"/> — gameplay decides state, the Animator only displays it.
    /// <c>Test_Animator_DoesNotMutateGameplayState</c> holds that boundary by scanning this file.
    /// </summary>
    /// <remarks>
    /// Reads only <see cref="EventBus"/> events, the same channel the development overlay and the
    /// HUD use, rather than holding direct references to the hero's other components. That keeps
    /// this component swappable — deleting it changes nothing about how the hero plays, only how it
    /// looks — and keeps the "does not drive gameplay" boundary a matter of what this file imports,
    /// not of remembering not to call a setter.
    /// </remarks>
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    public sealed class PlayerAnimatorDriver : MonoBehaviour
    {
        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int IsGroundedParam = Animator.StringToHash("IsGrounded");
        private static readonly int IsDashingParam = Animator.StringToHash("IsDashing");
        private static readonly int ComboStepParam = Animator.StringToHash("ComboStep");
        private static readonly int AttackTrigger = Animator.StringToHash("Attack");
        private static readonly int HurtTrigger = Animator.StringToHash("Hurt");
        private static readonly int DeathTrigger = Animator.StringToHash("Death");
        private static readonly int JumpTrigger = Animator.StringToHash("Jump");

        private Animator _animator;
        private EventBus _eventBus;

        private int _lastJumpCount;
        private int _lastComboStep;

        private void Awake() => _animator = GetComponent<Animator>();

        private void OnEnable()
        {
            if (ServiceLocator.Current == null) return;
            if (!ServiceLocator.Current.TryGet(out _eventBus)) return;

            _eventBus.Subscribe<PlayerMotorStateEvent>(OnMotorState);
            _eventBus.Subscribe<ComboChangedEvent>(OnComboChanged);
            _eventBus.Subscribe<DamageAppliedEvent>(OnDamageApplied);
            _eventBus.Subscribe<EntityDiedEvent>(OnEntityDied);
        }

        private void OnDisable()
        {
            if (_eventBus == null) return;

            _eventBus.Unsubscribe<PlayerMotorStateEvent>(OnMotorState);
            _eventBus.Unsubscribe<ComboChangedEvent>(OnComboChanged);
            _eventBus.Unsubscribe<DamageAppliedEvent>(OnDamageApplied);
            _eventBus.Unsubscribe<EntityDiedEvent>(OnEntityDied);
            _eventBus = null;
        }

        /// <summary>Speed, grounded and dashing every frame; Jump the frame JumpCount rises.</summary>
        private void OnMotorState(PlayerMotorStateEvent evt)
        {
            _animator.SetFloat(SpeedParam, Mathf.Abs(evt.Velocity.x));
            _animator.SetBool(IsGroundedParam, evt.IsGrounded);
            _animator.SetBool(IsDashingParam, evt.IsDashing);

            if (evt.JumpCount > _lastJumpCount) _animator.SetTrigger(JumpTrigger);
            _lastJumpCount = evt.JumpCount;
        }

        /// <summary>ComboStep every change; Attack only on a rising step, never on the reset to 0.</summary>
        private void OnComboChanged(ComboChangedEvent evt)
        {
            _animator.SetInteger(ComboStepParam, evt.Step);

            if (evt.Step > 0 && evt.Step != _lastComboStep) _animator.SetTrigger(AttackTrigger);
            _lastComboStep = evt.Step;
        }

        /// <summary>Hurt trigger, filtered to the hero: every entity's hits publish on this channel.</summary>
        private void OnDamageApplied(DamageAppliedEvent evt)
        {
            if (!evt.TargetIsPlayer) return;
            _animator.SetTrigger(HurtTrigger);
        }

        /// <summary>Death trigger, filtered to the hero for the same reason as above.</summary>
        private void OnEntityDied(EntityDiedEvent evt)
        {
            if (!evt.IsPlayer) return;
            _animator.SetTrigger(DeathTrigger);
        }
    }
}
