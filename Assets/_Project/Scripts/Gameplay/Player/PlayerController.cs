using UnityEngine;
using ChibiRift.Core;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Turns input into requests on <see cref="PlayerMotor"/> and <see cref="PlayerCombat"/>.
    /// Holds no physics and no combat rules of its own.
    /// </summary>
    /// <remarks>
    /// Input is sampled in <c>Update</c>, not <c>FixedUpdate</c>: a tap that begins and ends
    /// between two physics steps would otherwise be dropped entirely (MOV-005).
    /// </remarks>
    [RequireComponent(typeof(PlayerMotor))]
    [DisallowMultipleComponent]
    public sealed class PlayerController : MonoBehaviour
    {
        private IInputService _input;
        private PlayerMotor _motor;
        private PlayerCombat _combat;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _combat = GetComponent<PlayerCombat>();
        }

        private void Start()
        {
            if (ServiceLocator.Current == null || !ServiceLocator.Current.TryGet(out _input))
            {
                GameLog.Error("Player",
                    "No IInputService registered. Enter play from the Boot scene so the composition root exists.");
            }
        }

        private void Update()
        {
            if (_input == null) return;

            _motor.SetMoveIntent(_input.MoveAxis);   // MOV-001
            _motor.SetJumpHeld(_input.JumpHeld);     // MOV-002 variable height

            // MOV-002, MOV-003: the motor buffers the press and decides ground vs air jump.
            if (_input.JumpPressed) _motor.RequestJump();

            if (_combat != null)
            {
                // COM-009: facing and hitbox placement follow the cursor, with no auto-target.
                _combat.SetAimTarget(_input.AimWorldPosition);

                // COM-001: one press starts one swing; the chain is PlayerCombat's decision.
                if (_input.AttackPressed) _combat.RequestAttack();

                // COM-001: an attack commits the hero by slowing them, not by freezing them.
                _motor.SetSpeedMultiplier(_combat.MoveSpeedMultiplier);
            }

            // TODO(MOV-006): dash on _input.DashPressed once the dash system exists.
            // TODO(COM-007): Q/E/R via _input.WasSkillPressed.
        }
    }
}
