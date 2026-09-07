using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChibiRift.Core
{
    /// <summary>
    /// Wraps the Unity Input System (package) so gameplay never polls devices directly.
    /// The legacy Input Manager is not used anywhere in the project.
    /// Bindings encode the control map fixed in SRS 8.3; W/S/F stay unbound in MVP (SRS 43 Q2),
    /// and Mouse Right is declared but reserved.
    /// </summary>
    public sealed class InputManager : IInputService, IDisposable
    {
        private readonly InputAction _move;
        private readonly InputAction _jump;
        private readonly InputAction _dash;
        private readonly InputAction _attack;
        private readonly InputAction _skill1;
        private readonly InputAction _skill2;
        private readonly InputAction _ultimate;
        private readonly InputAction _point;
        private readonly InputAction _pause;

        /// <inheritdoc />
        public Camera AimCamera { get; set; }

        public InputManager()
        {
            // MOV-001: A/D move left/right.
            _move = new InputAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("1DAxis")
                 .With("Negative", "<Keyboard>/a")
                 .With("Positive", "<Keyboard>/d");

            // MOV-002, MOV-003: Space jump / double jump.
            _jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");

            // MOV-006: Left Shift dash with i-frames.
            _dash = new InputAction("Dash", InputActionType.Button, "<Keyboard>/leftShift");

            // COM-001, COM-002: Mouse Left basic attack and combo.
            _attack = new InputAction("Attack", InputActionType.Button, "<Mouse>/leftButton");

            // COM-007: Q/E/R special skills.
            _skill1 = new InputAction("Skill1", InputActionType.Button, "<Keyboard>/q");
            _skill2 = new InputAction("Skill2", InputActionType.Button, "<Keyboard>/e");
            _ultimate = new InputAction("Ultimate", InputActionType.Button, "<Keyboard>/r");

            // COM-009: mouse aim, no auto-target.
            _point = new InputAction("Point", InputActionType.Value, "<Mouse>/position");

            // PAU-001: ESC opens the Pause Menu during a Run.
            _pause = new InputAction("Pause", InputActionType.Button, "<Keyboard>/escape");

            SetGameplayInputEnabled(true);
            _pause.Enable();
            _point.Enable();
        }

        /// <inheritdoc />
        public float MoveAxis => _move.ReadValue<float>();

        /// <inheritdoc />
        public bool JumpPressed => _jump.WasPressedThisFrame();

        /// <inheritdoc />
        public bool DashPressed => _dash.WasPressedThisFrame();

        /// <inheritdoc />
        public bool AttackPressed => _attack.WasPressedThisFrame();

        /// <inheritdoc />
        public bool PausePressed => _pause.WasPressedThisFrame();

        /// <inheritdoc />
        public Vector2 AimScreenPosition => _point.ReadValue<Vector2>();

        /// <inheritdoc />
        public Vector2 AimWorldPosition
        {
            get
            {
                Camera camera = AimCamera != null ? AimCamera : Camera.main;
                if (camera == null) return Vector2.zero;
                return camera.ScreenToWorldPoint(AimScreenPosition);
            }
        }

        /// <inheritdoc />
        public bool WasSkillPressed(SkillSlot slot)
        {
            switch (slot)
            {
                case SkillSlot.Skill1: return _skill1.WasPressedThisFrame();
                case SkillSlot.Skill2: return _skill2.WasPressedThisFrame();
                case SkillSlot.Ultimate: return _ultimate.WasPressedThisFrame();
                default: return false;
            }
        }

        /// <inheritdoc />
        public void SetGameplayInputEnabled(bool enabled)
        {
            // ESC and the pointer stay live while paused so the Pause Menu remains usable.
            if (enabled)
            {
                _move.Enable();
                _jump.Enable();
                _dash.Enable();
                _attack.Enable();
                _skill1.Enable();
                _skill2.Enable();
                _ultimate.Enable();
            }
            else
            {
                _move.Disable();
                _jump.Disable();
                _dash.Disable();
                _attack.Disable();
                _skill1.Disable();
                _skill2.Disable();
                _ultimate.Disable();
            }
        }

        /// <summary>Releases every action. Called when the ServiceLocator is cleared.</summary>
        public void Dispose()
        {
            _move.Dispose();
            _jump.Dispose();
            _dash.Dispose();
            _attack.Dispose();
            _skill1.Dispose();
            _skill2.Dispose();
            _ultimate.Dispose();
            _point.Dispose();
            _pause.Dispose();
        }
    }
}
