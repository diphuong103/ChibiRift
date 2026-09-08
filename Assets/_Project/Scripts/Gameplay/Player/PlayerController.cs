using UnityEngine;
using ChibiRift.Core;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Turns input into motor intent (MOV-001 through MOV-003). Deliberately thin: it owns no
    /// physics and no timers, so the split against <see cref="PlayerMotor"/> stays clean and the
    /// motor can be tested without any input at all.
    /// </summary>
    /// <remarks>
    /// Input is sampled in <c>Update</c>, not <c>FixedUpdate</c>, because a key tapped between two
    /// physics steps would otherwise be dropped. The press is handed to the motor's jump buffer,
    /// which is what makes it survive to the next step (MOV-005).
    /// </remarks>
    [RequireComponent(typeof(PlayerMotor))]
    [DisallowMultipleComponent]
    public sealed class PlayerController : MonoBehaviour
    {
        private PlayerMotor _motor;
        private IInputService _input;

        private void Awake() => _motor = GetComponent<PlayerMotor>();

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

            // TODO(MOV-006): dash on _input.DashPressed once the dash system exists.
            // TODO(COM-001): basic attack on _input.AttackPressed.
            // TODO(COM-007): Q/E/R via _input.WasSkillPressed.
            // TODO(COM-009): face the cursor using _input.AimWorldPosition instead of velocity.
        }
    }
}
