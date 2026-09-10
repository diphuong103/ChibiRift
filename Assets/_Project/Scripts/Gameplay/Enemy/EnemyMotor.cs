using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Movement for one enemy: gravity, ground contact, horizontal drive, knockback (COM-005) and
    /// crowd separation (AI-005). Holds no decisions — <see cref="EnemyAI"/> tells it where to go.
    /// </summary>
    /// <remarks>
    /// <para>Separate from <see cref="PlayerMotor"/> rather than shared through a base class. The
    /// hero motor is most of the way made of things an enemy has no use for: jump buffering, coyote
    /// time, double jump, world clamping and fall respawn. Inheriting would carry all of it across
    /// for the sake of the roughly twenty lines the two genuinely share, which is
    /// <see cref="KnockbackState"/> and is shared directly.</para>
    ///
    /// <para>Gravity is simulated here rather than by <c>Rigidbody2D</c>, matching the hero. Two
    /// bodies in one scene falling under two different rules would make every knockback and every
    /// ledge behave differently depending on who was involved.</para>
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public sealed class EnemyMotor : MonoBehaviour, IKnockbackReceiver
    {
        [Header("Data")]
        [Tooltip("Supplies move speed, separation radius and force. Every number comes from here (NFR-007).")]
        [SerializeField] private EnemyData _enemyData;


        /// <summary>True while the ground probe overlaps the Ground layer.</summary>
        public bool IsGrounded { get; private set; }

        /// <summary>Current velocity, for tests and diagnostics.</summary>
        public Vector2 Velocity => _body != null ? _body.linearVelocity : Vector2.zero;

        /// <inheritdoc />
        public bool IsKnockedBack => _knockback.IsActive;

        /// <summary>Which way the enemy is travelling: 1 right, -1 left.</summary>
        public int Facing { get; private set; } = 1;

        /// <summary>Archetype supplying the movement numbers.</summary>
        public EnemyData Data
        {
            get => _enemyData;
            set => _enemyData = value;
        }

        private Rigidbody2D _body;
        private Collider2D _collider;
        private HealthComponent _health;
        private SpriteRenderer _spriteRenderer;
        private EventBus _eventBus;
        private KnockbackState _knockback;

        private float _moveIntent;
        private int _groundMask;
        private ContactFilter2D _enemyFilter;
        private readonly Collider2D[] _neighbourBuffer = new Collider2D[MaxNeighbours];

        /// <summary>Cap on neighbours considered for separation, so the sweep never allocates.</summary>
        private const int MaxNeighbours = 8;

        private float MoveSpeed => _enemyData != null ? _enemyData.BaseStats.MoveSpeed : 0f;

        private EnemyPhysicsConfig Physics2DConfig =>
            _enemyData != null ? _enemyData.Physics : EnemyPhysicsConfig.MeleeBaseline;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            _health = GetComponent<HealthComponent>();
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            _body.gravityScale = 0f;
            _body.freezeRotation = true;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;

            _groundMask = LayerMask.GetMask(GameLayers.Ground);
            _enemyFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = LayerMask.GetMask(GameLayers.Enemy),
                useTriggers = false
            };

            // The same contact-friction problem the hero hit (OI-17): this motor writes
            // linearVelocity outright, and Unity then bleeds speed off it inside the same step, so
            // the enemy would crawl below its configured MoveSpeed. A material set in the
            // inspector wins; this only supplies a sane default.
            if (_collider.sharedMaterial == null)
            {
                _collider.sharedMaterial = new PhysicsMaterial2D("EnemyFrictionless")
                {
                    friction = 0f,
                    bounciness = 0f
                };
            }
        }

        private void Start()
        {
            if (ServiceLocator.Current != null) ServiceLocator.Current.TryGet(out _eventBus);
            _eventBus?.Subscribe<DamageAppliedEvent>(OnDamageApplied);
        }

        private void OnDisable() => _eventBus?.Unsubscribe<DamageAppliedEvent>(OnDamageApplied);

        /// <summary>Horizontal intent for this step, -1..1. Set by <see cref="EnemyAI"/>.</summary>
        public void SetMoveIntent(float axis) => _moveIntent = Mathf.Clamp(axis, -1f, 1f);

        /// <summary>Stops horizontal movement immediately, e.g. on entering Attack or Death.</summary>
        public void Halt() => _moveIntent = 0f;

        /// <summary>
        /// Points the enemy at <paramref name="direction"/> without moving it.
        /// </summary>
        /// <remarks>
        /// Needed because facing is otherwise derived from velocity, and an enemy that is already
        /// within attack range when it notices the hero never moves — so it would keep whatever
        /// facing it happened to have and swing at empty air behind itself.
        /// </remarks>
        public void SetFacing(int direction)
        {
            if (direction == 0) return;

            Facing = direction > 0 ? 1 : -1;
            if (_spriteRenderer != null) _spriteRenderer.flipX = Facing < 0;
        }

        /// <inheritdoc />
        public void ApplyKnockback(Vector2 direction, float force, float durationSeconds)
        {
            float velocityX = _knockback.Begin(direction, force, durationSeconds);
            if (!_knockback.IsActive) return;

            Vector2 velocity = _body.linearVelocity;
            velocity.x = velocityX;
            _body.linearVelocity = velocity;
        }

        /// <summary>Clears momentum and the knockback window. Used when an instance is reused.</summary>
        public void ResetMotion()
        {
            _moveIntent = 0f;
            _knockback.Clear();
            if (_body != null) _body.linearVelocity = Vector2.zero;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            UpdateGrounded();

            Vector2 velocity = _body.linearVelocity;

            // COM-005: a knockback owns the horizontal axis until it expires. Without this the
            // drive below would overwrite the push on the next step and nothing would be seen.
            if (!_knockback.Tick(dt))
            {
                velocity.x = _moveIntent * MoveSpeed;
                velocity.x += SeparationPush() * dt;
            }

            velocity.y = ApplyGravity(velocity.y, dt);

            _body.linearVelocity = velocity;
            UpdateFacing(velocity.x);
        }

        private float ApplyGravity(float velocityY, float dt)
        {
            // Resting on the ground rather than accumulating downward speed into it, which would
            // otherwise build up and punch through on the frame the collider is re-enabled.
            if (IsGrounded && velocityY <= 0f) return 0f;

            EnemyPhysicsConfig config = Physics2DConfig;
            return Mathf.Max(velocityY - config.Gravity * dt, -config.MaxFallSpeed);
        }

        private void UpdateGrounded()
        {
            EnemyPhysicsConfig config = Physics2DConfig;
            var probeCentre = new Vector2(
                transform.position.x, transform.position.y + config.GroundCheckOffsetY);

            IsGrounded = Physics2D.OverlapBox(probeCentre, config.GroundCheckSize, 0f, _groundMask) != null;
        }

        /// <summary>
        /// AI-005: nudges apart from neighbours so a group reads as a crowd rather than collapsing
        /// into one silhouette (SRS 5). Horizontal only, and weak enough that it never overrides
        /// the chase direction outright.
        /// </summary>
        private float SeparationPush()
        {
            if (_enemyData == null || _enemyData.SeparationRadius <= 0f) return 0f;

            int count = Physics2D.OverlapCircle(
                transform.position, _enemyData.SeparationRadius, _enemyFilter, _neighbourBuffer);

            float push = 0f;
            for (int i = 0; i < count; i++)
            {
                Collider2D other = _neighbourBuffer[i];
                if (other == null || other.gameObject == gameObject) continue;

                float delta = transform.position.x - other.transform.position.x;

                // Exactly overlapping neighbours have no direction to separate along; break the
                // tie by instance id so the two pick opposite ways instead of both standing still.
                if (Mathf.Approximately(delta, 0f))
                {
                    delta = gameObject.GetInstanceID() < other.gameObject.GetInstanceID() ? -1f : 1f;
                }

                push += Mathf.Sign(delta) * _enemyData.SeparationForce;
            }

            return push;
        }

        private void UpdateFacing(float velocityX)
        {
            if (Mathf.Abs(velocityX) < Mathf.Epsilon) return;

            Facing = velocityX > 0f ? 1 : -1;
            if (_spriteRenderer != null) _spriteRenderer.flipX = Facing < 0;
        }

        private void OnDamageApplied(DamageAppliedEvent evt)
        {
            if (evt.TargetEntityId != gameObject.GetInstanceID()) return;

            // AI-003: Death is terminal, and that has to include the killing blow's own knockback.
            // The death transition runs inside ApplyDamage and halts this motor; this event is
            // published immediately afterwards, so without this guard the push would undo the halt
            // and the corpse would slide away.
            if (_health != null && _health.IsDead) return;

            Vector2 away = (Vector2)transform.position - evt.AttackerPosition;
            ApplyKnockback(away, evt.KnockbackForce, evt.KnockbackDuration);
        }
    }
}
