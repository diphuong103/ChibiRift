using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChibiRift.Core
{
    /// <summary>
    /// The project's only bridge to hardware input (P1-01). Wraps the "Gameplay" action map of
    /// <c>ChibiRiftControls.inputactions</c>, whose bindings are the control map fixed in SRS 8.3.
    /// The legacy Input Manager is not used anywhere.
    /// </summary>
    /// <remarks>
    /// Not a singleton and not a MonoBehaviour: <see cref="GameBootstrap"/> constructs one and
    /// registers it on the <see cref="ServiceLocator"/>, so tests can substitute a fake
    /// <see cref="IInputService"/> without touching real devices.
    /// P1 slice 1 wires Move and Jump only. Every other action has its binding, its event and its
    /// handler already written, but the handler is deliberately not subscribed yet.
    /// </remarks>
    public sealed class InputReader : IInputService, IDisposable
    {
        private const string LogCategory = "Input";
        private const string GameplayMapName = "Gameplay";

        private readonly InputActionAsset _asset;
        private readonly InputActionMap _gameplay;

        private readonly InputAction _move;
        private readonly InputAction _jump;
        private readonly InputAction _dash;
        private readonly InputAction _attack;
        private readonly InputAction _skill1;
        private readonly InputAction _skill2;
        private readonly InputAction _skill3;
        private readonly InputAction _aim;
        private readonly InputAction _pause;

        // ---- Wired in this slice ----

        /// <summary>Horizontal axis changed, -1..1 (MOV-001).</summary>
        public event Action<float> MoveChanged;

        /// <summary>Space went down (MOV-002, MOV-003).</summary>
        public event Action JumpStarted;

        /// <summary>Space came up. Ends the variable-height window (MOV-002).</summary>
        public event Action JumpReleased;

        // ---- Declared now, subscribed in a later slice ----

        /// <summary>Left Shift went down. Consumer arrives with dash (MOV-006).</summary>
        public event Action DashStarted;

        /// <summary>Mouse Left went down. Consumer arrives with the basic attack (COM-001).</summary>
        public event Action AttackStarted;

        /// <summary>Q, E or R went down. Consumer arrives with the skill system (COM-007).</summary>
        public event Action<SkillSlot> SkillStarted;

        /// <summary>ESC went down. Consumer arrives with the Pause Menu (PAU-001).</summary>
        public event Action PauseStarted;

        /// <inheritdoc />
        public Camera AimCamera { get; set; }

        /// <param name="asset">The ChibiRiftControls asset, supplied by the Boot scene.</param>
        public InputReader(InputActionAsset asset)
        {
            _asset = asset != null ? asset : throw new ArgumentNullException(nameof(asset));
            _gameplay = _asset.FindActionMap(GameplayMapName, throwIfNotFound: true);

            _move = _gameplay.FindAction("Move", throwIfNotFound: true);
            _jump = _gameplay.FindAction("Jump", throwIfNotFound: true);
            _dash = _gameplay.FindAction("Dash", throwIfNotFound: true);
            _attack = _gameplay.FindAction("Attack", throwIfNotFound: true);
            _skill1 = _gameplay.FindAction("Skill1", throwIfNotFound: true);
            _skill2 = _gameplay.FindAction("Skill2", throwIfNotFound: true);
            _skill3 = _gameplay.FindAction("Skill3", throwIfNotFound: true);
            _aim = _gameplay.FindAction("Aim", throwIfNotFound: true);
            _pause = _gameplay.FindAction("Pause", throwIfNotFound: true);

            // Wired: an event is raised for anything a system already listens for.
            //
            // Note that the polling properties below (MoveAxis, AttackPressed, AimScreenPosition
            // and the rest) work whether or not an event is wired here, because they read the
            // action directly and the whole map is enabled at the end of this constructor.
            // PlayerController polls; these events exist for systems that want an edge rather than
            // a per-frame check. Aim has no event by design — a pointer position is a value that is
            // read, not an edge that fires.
            _move.performed += OnMove;
            _move.canceled += OnMove;
            _jump.started += OnJumpStarted;
            _jump.canceled += OnJumpReleased;
            _attack.started += OnAttack;

            // Not wired because nothing consumes them yet; the actions are still bound and
            // pollable, so wiring is a one-line change when the system arrives.
            // TODO(MOV-006): _dash.started += OnDash;
            // TODO(COM-007): _skill1/_skill2/_skill3.started += OnSkill1/2/3;
            // TODO(PAU-001): _pause.started += OnPause;

            _gameplay.Enable();
        }

        /// <inheritdoc />
        public float MoveAxis => _move.ReadValue<float>();

        /// <inheritdoc />
        public bool JumpPressed => _jump.WasPressedThisFrame();

        /// <inheritdoc />
        public bool JumpHeld => _jump.IsPressed();

        /// <inheritdoc />
        public bool DashPressed => _dash.WasPressedThisFrame();

        /// <inheritdoc />
        public bool AttackPressed => _attack.WasPressedThisFrame();

        /// <inheritdoc />
        public bool PausePressed => _pause.WasPressedThisFrame();

        /// <inheritdoc />
        public Vector2 AimScreenPosition => _aim.ReadValue<Vector2>();

        /// <inheritdoc />
        public Vector2 AimWorldPosition
        {
            get
            {
                Camera camera = AimCamera != null ? AimCamera : Camera.main;
                return camera == null ? Vector2.zero : (Vector2)camera.ScreenToWorldPoint(AimScreenPosition);
            }
        }

        /// <inheritdoc />
        public bool WasSkillPressed(SkillSlot slot)
        {
            switch (slot)
            {
                case SkillSlot.Skill1: return _skill1.WasPressedThisFrame();
                case SkillSlot.Skill2: return _skill2.WasPressedThisFrame();
                case SkillSlot.Ultimate: return _skill3.WasPressedThisFrame();
                default: return false;
            }
        }

        /// <inheritdoc />
        public void SetGameplayInputEnabled(bool enabled)
        {
            if (enabled) _gameplay.Enable();
            else _gameplay.Disable();
        }

        /// <summary>Unsubscribes and disposes the asset's actions.</summary>
        public void Dispose()
        {
            _move.performed -= OnMove;
            _move.canceled -= OnMove;
            _jump.started -= OnJumpStarted;
            _jump.canceled -= OnJumpReleased;
            _attack.started -= OnAttack;

            _gameplay.Disable();
        }

        private void OnMove(InputAction.CallbackContext context) => MoveChanged?.Invoke(context.ReadValue<float>());

        private void OnJumpStarted(InputAction.CallbackContext context) => JumpStarted?.Invoke();

        private void OnJumpReleased(InputAction.CallbackContext context) => JumpReleased?.Invoke();

        // Handlers below are written but not subscribed. Each slice that adds the consuming
        // system also adds its "+=" line in the constructor.

        private void OnDash(InputAction.CallbackContext context)
        {
            // TODO(MOV-006): dash with i-frames and cooldown consumes this.
            DashStarted?.Invoke();
        }

        private void OnAttack(InputAction.CallbackContext context)
        {
            // TODO(COM-001): basic attack and the 3 hit combo consume this.
            AttackStarted?.Invoke();
        }

        private void OnSkill1(InputAction.CallbackContext context)
        {
            // TODO(COM-007): Q skill.
            SkillStarted?.Invoke(SkillSlot.Skill1);
        }

        private void OnSkill2(InputAction.CallbackContext context)
        {
            // TODO(COM-007): E skill.
            SkillStarted?.Invoke(SkillSlot.Skill2);
        }

        private void OnSkill3(InputAction.CallbackContext context)
        {
            // TODO(COM-007): R ultimate.
            SkillStarted?.Invoke(SkillSlot.Ultimate);
        }

        private void OnPause(InputAction.CallbackContext context)
        {
            // TODO(PAU-001): Pause Menu consumes this.
            PauseStarted?.Invoke();
        }
    }
}
