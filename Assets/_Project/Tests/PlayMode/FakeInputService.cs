using UnityEngine;
using ChibiRift.Core;

namespace ChibiRift.Tests.Play
{
    /// <summary>
    /// Scriptable stand-in for <see cref="IInputService"/>.
    /// </summary>
    /// <remarks>
    /// Used instead of the Input System's device simulation because the movement tests are about
    /// the motor, not about bindings: driving intent directly keeps them deterministic and free of
    /// global device state. The bindings themselves are covered separately by
    /// <c>InputActionsAssetTests</c> in EditMode.
    /// </remarks>
    public sealed class FakeInputService : IInputService
    {
        /// <summary>Set by the test to steer the hero, -1..1.</summary>
        public float MoveAxis { get; set; }

        /// <summary>True for exactly one frame per press; the reader clears it on read.</summary>
        public bool JumpPressed
        {
            get
            {
                if (!_jumpPressed) return false;
                _jumpPressed = false;
                return true;
            }
        }

        /// <summary>Whether the jump key is held, driving the variable jump height.</summary>
        public bool JumpHeld { get; set; }

        public bool DashPressed => false;

        /// <summary>True for exactly one frame per press, mirroring WasPressedThisFrame.</summary>
        public bool AttackPressed
        {
            get
            {
                if (!_attackPressed) return false;
                _attackPressed = false;
                return true;
            }
        }

        public bool PausePressed => false;
        public Vector2 AimScreenPosition => Vector2.zero;

        /// <summary>Set by the test to place the cursor. Drives facing and hitbox offset (COM-009).</summary>
        public Vector2 AimWorldPosition { get; set; } = Vector2.right;

        public Camera AimCamera { get; set; }

        private bool _jumpPressed;
        private bool _attackPressed;

        /// <summary>Queues one jump press, exactly as tapping Space would.</summary>
        public void PressJump()
        {
            _jumpPressed = true;
            JumpHeld = true;
        }

        /// <summary>Releases the jump key, ending the variable-height window.</summary>
        public void ReleaseJump() => JumpHeld = false;

        /// <summary>Queues one attack press, exactly as clicking Mouse Left would (COM-001).</summary>
        public void PressAttack() => _attackPressed = true;

        /// <summary>Points the cursor at a world position (COM-009).</summary>
        public void AimAt(Vector2 worldPosition) => AimWorldPosition = worldPosition;

        public bool WasSkillPressed(SkillSlot slot) => false;

        public void SetGameplayInputEnabled(bool enabled) { }
    }
}
