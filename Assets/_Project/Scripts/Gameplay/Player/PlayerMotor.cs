using System;
using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// All hero locomotion physics (MOV-001 through MOV-005). Deliberately knows nothing about
    /// input: <see cref="PlayerController"/> reads <see cref="IInputService"/> and pushes intent
    /// in here. That split is what lets the PlayMode tests drive the motor deterministically.
    /// </summary>
    /// <remarks>
    /// Gravity is simulated here rather than by Rigidbody2D (<c>gravityScale = 0</c>) so the rise,
    /// the fall and the released-early rise can each use their own multiplier from
    /// <see cref="MovementConfig"/>. Movement is applied through
    /// <see cref="Rigidbody2D.linearVelocity"/>; nothing writes <c>transform.position</c> except
    /// the respawn, which is a teleport by definition.
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CapsuleCollider2D))]
    [DisallowMultipleComponent]
    public sealed class PlayerMotor : MonoBehaviour, IKnockbackReceiver
    {
        [Header("Data (SRS 35: no movement literal lives in this script)")]
        [Tooltip("Supplies move speed, acceleration, gravity, jump and ground-probe values.")]
        [SerializeField] private HeroData _heroData;

        [Header("Scene")]
        [Tooltip("Supplies the world bounds and the respawn point (MOV-004).")]
        [SerializeField] private SceneContext _sceneContext;

        /// <summary>Raised whenever the hero touches or leaves the ground.</summary>
        public event Action<bool> GroundedChanged;

        /// <summary>Raised the moment a jump actually fires, for the jump cue (SRS 22).</summary>
        public event Action Jumped;

        /// <summary>Raised after a respawn, so the camera can snap instead of pan (MOV-004).</summary>
        public event Action Respawned;

        /// <summary>Current velocity, in units per second.</summary>
        public Vector2 Velocity => _body != null ? _body.linearVelocity : Vector2.zero;

        /// <summary>True while the ground probe overlaps the Ground layer (MOV-004).</summary>
        public bool IsGrounded { get; private set; }

        /// <summary>Jumps used since last touching ground. Reset to 0 on landing (MOV-003).</summary>
        public int JumpCount { get; private set; }

        /// <summary>Seconds of coyote time left. Above zero, a ground jump is still allowed.</summary>
        public float CoyoteTimer { get; private set; }

        /// <summary>Seconds a buffered jump press stays valid.</summary>
        public float JumpBufferTimer { get; private set; }

        /// <summary>Facing sign: +1 right, -1 left (COM-009 will drive this from the cursor later).</summary>
        public int Facing { get; private set; } = 1;

        private Rigidbody2D _body;
        private CapsuleCollider2D _collider;
        private EventBus _eventBus;

        private float _moveIntent;
        private float _speedMultiplier = 1f;
        private bool _jumpHeld;
        private KnockbackState _knockback;
        private DashState _dash;
        private float _wallStopFraction;
        private int _groundMask;

        private MovementConfig Config => _heroData.Movement;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<CapsuleCollider2D>();

            // Gravity is ours, not the physics engine's; see the class remarks.
            _body.gravityScale = 0f;
            _body.freezeRotation = true;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            _groundMask = LayerMask.GetMask(GameLayers.Ground);

            // The motor writes linearVelocity directly, but Unity still applies contact friction
            // afterwards inside the same step, which bleeds off speed and would leave the hero
            // moving slower than the configured MoveSpeed. A frictionless character collider is
            // the standard fix and also stops the hero sticking to walls. A material assigned in
            // the inspector wins, so this only supplies a sane default.
            if (_collider.sharedMaterial == null)
            {
                _collider.sharedMaterial = new PhysicsMaterial2D("HeroFrictionless")
                {
                    friction = 0f,
                    bounciness = 0f
                };
            }

            if (_heroData == null)
                GameLog.Error("Motor", $"{name} has no HeroData; movement values are unavailable (SRS 30).");
        }

        private void Start()
        {
            if (ServiceLocator.Current != null) ServiceLocator.Current.TryGet(out _eventBus);
            if (_sceneContext != null) Teleport(_sceneContext.SpawnPosition);

            // Subscribed in Start rather than OnEnable because the bus is resolved here; the
            // matching Unsubscribe in OnDisable is safe either way.
            _eventBus?.Subscribe<DamageAppliedEvent>(OnDamageApplied);
        }

        private void OnDisable()
        {
            // The EventBus outlives the scene, so a handler left behind would fire into a
            // destroyed component on the next Run.
            _eventBus?.Unsubscribe<DamageAppliedEvent>(OnDamageApplied);
        }

        private void OnDamageApplied(DamageAppliedEvent evt)
        {
            if (evt.TargetEntityId != gameObject.GetInstanceID()) return;

            Vector2 away = (Vector2)transform.position - evt.AttackerPosition;
            ApplyKnockback(away, evt.KnockbackForce, evt.KnockbackDuration);
        }

        /// <summary>Horizontal intent for this step, -1..1. Called by <see cref="PlayerController"/>.</summary>
        public void SetMoveIntent(float axis) => _moveIntent = Mathf.Clamp(axis, -1f, 1f);

        /// <summary>Horizontal intent as last set, -1..1. Read by the dash to pick its direction.</summary>
        public float MoveIntent => _moveIntent;

        /// <summary>
        /// Scales top speed for this step, 0..1 (COM-001). An attack commits the hero by slowing
        /// them rather than freezing them, so a swing never feels like a stutter.
        /// </summary>
        public void SetSpeedMultiplier(float multiplier) => _speedMultiplier = Mathf.Clamp01(multiplier);

        /// <inheritdoc />
        public bool IsKnockedBack => _knockback.IsActive;

        /// <summary>True while a dash owns both axes (MOV-006).</summary>
        public bool IsDashing => _dash.IsActive;

        /// <summary>
        /// Starts a dash along <paramref name="direction"/> (MOV-006). The motor stops driving
        /// itself and stops applying gravity until the window ends or a wall stops it (MOV-007).
        /// </summary>
        public void BeginDash(Vector2 direction, float speed, float durationSeconds)
        {
            _wallStopFraction = _heroData != null ? _heroData.Dash.WallStopFraction : 0f;
            _dash.Begin(direction, speed, durationSeconds);
            if (!_dash.IsActive) return;

            // A dash replaces momentum outright rather than adding to it: otherwise dashing
            // mid-fall would carry the fall speed through and undershoot the distance.
            _body.linearVelocity = _dash.Velocity;
            _knockback.Clear();
        }

        /// <summary>Ends a dash early. Used on death and on respawn.</summary>
        public void CancelDash() => _dash.Clear();

        /// <inheritdoc />
        /// <remarks>
        /// Subscribed to <c>DamageAppliedEvent</c> in <see cref="OnEnable"/>: the hit announces
        /// itself and the hero reacts, rather than the combat pipeline reaching in to push them.
        /// </remarks>
        public void ApplyKnockback(Vector2 direction, float force, float durationSeconds)
        {
            float velocityX = _knockback.Begin(direction, force, durationSeconds);
            if (!_knockback.IsActive) return;

            Vector2 velocity = _body.linearVelocity;
            velocity.x = velocityX;
            _body.linearVelocity = velocity;
        }

        /// <summary>Whether the jump key is currently held, for the variable-height cut (MOV-002).</summary>
        public void SetJumpHeld(bool held) => _jumpHeld = held;

        /// <summary>
        /// Records a jump press. Buffered for <see cref="MovementConfig.JumpBuffer"/> seconds so a
        /// press landing just before touchdown still fires.
        /// </summary>
        public void RequestJump() => JumpBufferTimer = Config.JumpBuffer;

        private void FixedUpdate()
        {
            if (_heroData == null) return;

            float dt = Time.fixedDeltaTime;

            UpdateGrounded();
            TickTimers(dt);

            Vector2 velocity = _body.linearVelocity;

            // MOV-006: a dash owns both axes. Checked first because it also outranks knockback —
            // being shoved mid-dash must not bend the dash off its line.
            if (_dash.Tick(dt))
            {
                velocity = _dash.Velocity;

                // MOV-007: a wall ends the dash rather than being passed through. The collider has
                // already stopped the body, so near-zero travel is the signal that it hit something.
                if (Mathf.Abs(_body.linearVelocity.x) < Mathf.Abs(_dash.Velocity.x) * _wallStopFraction)
                {
                    _dash.Clear();
                    velocity = _body.linearVelocity;
                }
            }
            else
            {
                // COM-005: while a knockback runs the hero has no horizontal say. Without this the
                // line below would overwrite the pushed velocity on the very next step and the
                // knockback would never be visible.
                if (!_knockback.Tick(dt)) velocity.x = ApplyHorizontal(velocity.x, dt);

                velocity.y = ApplyJumpAndGravity(velocity.y, dt);
            }

            _body.linearVelocity = velocity;

            ApplyBoundary();
            CheckFallLimit();
            UpdateFacing(velocity.x);
            PublishState(velocity);
        }

        /// <summary>MOV-004: an OverlapBox at the feet, never OnCollisionStay.</summary>
        private void UpdateGrounded()
        {
            MovementConfig config = Config;

            // Scaled by lossyScale, not used raw. The probe offset is measured in the same units as
            // the collider, so a scaled Transform moves the collider's feet without moving the
            // probe: a hero prefab left at scale (1, 2, 1) put the probe 0.9u inside its own body
            // and IsGrounded was false while standing still. The scale is 1 today; this keeps the
            // two in step if anyone scales an actor again.
            Vector3 scale = transform.lossyScale;
            var probeCentre = new Vector2(
                transform.position.x,
                transform.position.y + config.GroundCheckOffsetY * scale.y);
            var probeSize = new Vector2(
                config.GroundCheckWidth * Mathf.Abs(scale.x),
                config.GroundCheckHeight * Mathf.Abs(scale.y));

            bool grounded = Physics2D.OverlapBox(probeCentre, probeSize, 0f, _groundMask) != null;

            if (grounded != IsGrounded)
            {
                IsGrounded = grounded;
                GroundedChanged?.Invoke(grounded);
            }

            if (!grounded) return;

            // MOV-003: landing restores both jumps.
            JumpCount = 0;
            CoyoteTimer = config.CoyoteTime;
        }

        private void TickTimers(float dt)
        {
            if (!IsGrounded && CoyoteTimer > 0f) CoyoteTimer = Mathf.Max(0f, CoyoteTimer - dt);
            if (JumpBufferTimer > 0f) JumpBufferTimer = Mathf.Max(0f, JumpBufferTimer - dt);
        }

        /// <summary>MOV-001: accelerate toward the target, with separate ground and air rates.</summary>
        private float ApplyHorizontal(float velocityX, float dt)
        {
            MovementConfig config = Config;
            float target = _moveIntent * _heroData.BaseStats.MoveSpeed * _speedMultiplier;

            bool accelerating = !Mathf.Approximately(_moveIntent, 0f);
            float rate = IsGrounded
                ? (accelerating ? config.GroundAccel : config.GroundDecel)
                : (accelerating ? config.AirAccel : config.AirDecel);

            return Mathf.MoveTowards(velocityX, target, rate * dt);
        }

        /// <summary>MOV-002 and MOV-003: consume a buffered jump, then integrate our own gravity.</summary>
        private float ApplyJumpAndGravity(float velocityY, float dt)
        {
            MovementConfig config = Config;

            if (JumpBufferTimer > 0f && TryConsumeJump(out float jumpVelocity))
            {
                JumpBufferTimer = 0f;
                CoyoteTimer = 0f;
                Jumped?.Invoke();

                // The launch happens partway through the step, not at its start. Charging the
                // full step of gravity undershoots the arc and charging none overshoots it; half
                // a step is the semi-implicit Euler correction and lands the peak on the
                // analytic JumpVelocity^2 / (2 * GravityUp). The fraction lives in the asset only
                // so no tuning number sits in this assembly; it is not something to tune.
                return jumpVelocity - config.GravityUp * dt * config.LaunchGravityFraction;
            }

            float gravity = config.GravityUp;

            if (velocityY < 0f)
            {
                gravity *= config.FallMultiplier;
            }
            else if (velocityY > 0f && !_jumpHeld)
            {
                // Releasing early must cut the jump short, so this multiplier is above 1.
                gravity *= config.LowJumpMultiplier;
            }

            return Mathf.Max(velocityY - gravity * dt, -config.MaxFallSpeed);
        }

        /// <summary>
        /// Ground jump while grounded or inside coyote time; otherwise the single air jump of
        /// MOV-003, which uses its own velocity rather than reusing the ground value.
        /// </summary>
        private bool TryConsumeJump(out float jumpVelocity)
        {
            MovementConfig config = Config;

            if (IsGrounded || CoyoteTimer > 0f)
            {
                JumpCount = 1;
                jumpVelocity = config.JumpVelocity;
                return true;
            }

            if (JumpCount == 1 && _heroData.MaxJumpCount > 1)
            {
                JumpCount = 2;
                jumpVelocity = config.DoubleJumpVelocity;
                return true;
            }

            jumpVelocity = 0f;
            return false;
        }

        /// <summary>MOV-004: never leave the play area, even if a collider is missing.</summary>
        private void ApplyBoundary()
        {
            if (_sceneContext == null) return;

            float halfWidth = _sceneContext.WorldHalfWidth;
            Vector3 position = transform.position;
            float clampedX = Mathf.Clamp(position.x, -halfWidth, halfWidth);
            if (Mathf.Approximately(clampedX, position.x)) return;

            position.x = clampedX;
            transform.position = position;

            Vector2 velocity = _body.linearVelocity;
            velocity.x = 0f;
            _body.linearVelocity = velocity;
        }

        private void CheckFallLimit()
        {
            if (_sceneContext == null) return;
            if (transform.position.y >= _sceneContext.FallLimitY) return;

            Teleport(_sceneContext.SpawnPosition);
            Respawned?.Invoke();
        }

        /// <summary>Places the hero and clears momentum. The only writer of transform.position.</summary>
        public void Teleport(Vector2 position)
        {
            transform.position = position;
            if (_body != null) _body.linearVelocity = Vector2.zero;

            JumpCount = 0;
            CoyoteTimer = 0f;
            JumpBufferTimer = 0f;
            _knockback.Clear();
            _dash.Clear();
        }

        /// <summary>
        /// Tracks which way the hero is travelling (MOV-001).
        /// </summary>
        /// <remarks>
        /// This deliberately does <b>not</b> write <c>SpriteRenderer.flipX</c>. From P1 slice 2 the
        /// sprite faces the mouse cursor (COM-009) and <see cref="PlayerCombat"/> is the single
        /// writer; two writers produced a visible flicker whenever the cursor and the movement
        /// direction disagreed. Running left with the cursor to the right therefore moonwalks,
        /// which is the correct behaviour for a mouse-aimed game — see OI-20.
        /// </remarks>
        private void UpdateFacing(float velocityX)
        {
            if (Mathf.Approximately(_moveIntent, 0f)) return;

            Facing = _moveIntent > 0f ? 1 : -1;
        }

        private void PublishState(Vector2 velocity)
        {
            _eventBus?.Publish(new PlayerMotorStateEvent(
                velocity, IsGrounded, JumpCount, CoyoteTimer, JumpBufferTimer));
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_heroData == null) return;

            MovementConfig config = Config;
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(
                new Vector3(transform.position.x, transform.position.y + config.GroundCheckOffsetY, 0f),
                new Vector3(config.GroundCheckWidth, config.GroundCheckHeight, 0f));
        }
#endif
    }
}
