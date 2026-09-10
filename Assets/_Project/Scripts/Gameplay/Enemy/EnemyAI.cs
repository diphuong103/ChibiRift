using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Enemy state machine (AI-001 through AI-005).
    /// </summary>
    /// <remarks>
    /// <para><b>Shape.</b> One method per state, dispatched from a single switch. Slice 4 adds
    /// elite modifiers, which change what a state does rather than which states exist; keeping each
    /// state as its own method means that lands as an edit inside one method instead of a rewrite
    /// of one long <c>Update</c>.</para>
    ///
    /// <para><b>Two clocks, deliberately.</b> Deciding — measuring distance to the hero and
    /// choosing a state — is throttled to <see cref="BalanceConfig.EnemyThinkInterval"/> so thirty
    /// enemies stay inside NFR-001. Counting — attack phases, stun, cooldown — runs every
    /// <c>FixedUpdate</c>. Throttling the timers too would quantise every attack to the think
    /// interval, which is visible as a stutter at 0.1s.</para>
    ///
    /// <para><b>Hysteresis.</b> Aggro is gained at <see cref="EnemyData.DetectionRange"/> and lost
    /// at the larger <see cref="EnemyData.LoseAggroRange"/>. A single threshold would flip state
    /// every frame for a hero standing exactly on it (AI-002).</para>
    /// </remarks>
    [RequireComponent(typeof(EnemyController))]
    [RequireComponent(typeof(EnemyMotor))]
    [RequireComponent(typeof(HealthComponent))]
    [DisallowMultipleComponent]
    public sealed class EnemyAI : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("Supplies ranges, speed and timings. Every number comes from here, never from this script (NFR-007).")]
        [SerializeField] private EnemyData _enemyData;

        [Tooltip("Supplies the AI evaluation interval. The number is balance data and lives in the asset, never here (SRS 35).")]
        [SerializeField] private BalanceConfig _balanceConfig;

        /// <summary>Current state (AI-001).</summary>
        public EnemyLifecycleState State { get; private set; } = EnemyLifecycleState.Idle;

        /// <summary>Seconds between AI evaluations. Throttling keeps 30 enemies inside NFR-001.</summary>
        public float ThinkInterval => _balanceConfig != null ? _balanceConfig.EnemyThinkInterval : 0f;

        /// <summary>Where this enemy started, and where it walks back to on losing aggro (AI-002).</summary>
        public Vector2 SpawnPosition { get; private set; }

        /// <summary>Seconds left of hurt stun. Zero when not stunned.</summary>
        public float StunRemaining { get; private set; }

        /// <summary>The hero this enemy is tracking, or null when none has been found.</summary>
        public Transform Target { get; set; }

        /// <summary>Archetype supplying every number.</summary>
        public EnemyData Data
        {
            get => _enemyData;
            set => _enemyData = value;
        }

        private EnemyMotor _motor;
        private EnemyAttack _attack;
        private HealthComponent _health;
        private EventBus _eventBus;

        private float _thinkTimer;
        private float _stuckTimer;
        private float _stuckAnchorX;
        private int _stuckEvadeDirection;
        private float _evadeRemaining;


        private void Awake()
        {
            _motor = GetComponent<EnemyMotor>();
            _attack = GetComponent<EnemyAttack>();
            _health = GetComponent<HealthComponent>();

            SpawnPosition = transform.position;

            if (_enemyData == null)
                GameLog.Error("Enemy", $"{name} has no EnemyData; the state machine has no ranges (SRS 30).");
        }

        private void OnEnable()
        {
            if (_health == null) return;
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (_health == null) return;
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
        }

        private void Start()
        {
            if (ServiceLocator.Current != null) ServiceLocator.Current.TryGet(out _eventBus);

            // No service registry for the hero yet, and adding one for a single lookup would be
            // more machinery than it saves. TODO(AI-002): take the target from RunManager once it
            // owns the hero instance.
            if (Target == null)
            {
                var hero = GameObject.FindGameObjectWithTag("Player");
                if (hero != null) Target = hero.transform;
            }
        }

        /// <summary>Places this enemy's home position, used when aggro is lost (AI-002).</summary>
        public void SetSpawnPosition(Vector2 position) => SpawnPosition = position;

        private void FixedUpdate()
        {
            if (_enemyData == null) return;

            float dt = Time.fixedDeltaTime;

            // Counting runs every step; see the class remarks on the two clocks.
            TickTimers(dt);

            // AI-003: Death is terminal. Nothing below may run, and no transition leaves it.
            if (State == EnemyLifecycleState.Death) return;

            if (State == EnemyLifecycleState.Hurt)
            {
                TickHurt();
                return;
            }

            _thinkTimer -= dt;
            if (_thinkTimer > 0f) return;
            _thinkTimer = ThinkInterval;

            switch (State)
            {
                case EnemyLifecycleState.Idle: TickIdle(); break;
                case EnemyLifecycleState.Chase: TickChase(); break;
                case EnemyLifecycleState.Attack: TickAttack(); break;
                case EnemyLifecycleState.Recovery: TickRecovery(); break;
                case EnemyLifecycleState.ReturnToSpawn: TickReturnToSpawn(); break;
            }
        }

        private void TickTimers(float dt)
        {
            if (StunRemaining > 0f) StunRemaining = Mathf.Max(StunRemaining - dt, 0f);
            if (_evadeRemaining > 0f) _evadeRemaining = Mathf.Max(_evadeRemaining - dt, 0f);

            // Counted here, on the fixed clock, and not inside TickChase. TickChase runs at the
            // think rate, so accumulating there advanced this timer at a fifth of real time and
            // the configured 0.5s window silently became 2.5s.
            if (State == EnemyLifecycleState.Chase) _stuckTimer += dt;
        }

        // ----- states ---------------------------------------------------------------------

        /// <summary>AI-002: waits at home until the hero comes inside detection range.</summary>
        private void TickIdle()
        {
            _motor.Halt();

            if (DistanceToTarget() <= _enemyData.DetectionRange) Transition(EnemyLifecycleState.Chase);
        }

        /// <summary>AI-002 and AI-004: closes on the hero, attacks in range, gives up beyond lose range.</summary>
        private void TickChase()
        {
            float distance = DistanceToTarget();

            // AI-002: the wider give-up range is the hysteresis; see the class remarks.
            if (distance > _enemyData.LoseAggroRange)
            {
                Transition(EnemyLifecycleState.ReturnToSpawn);
                return;
            }

            if (distance <= _enemyData.AttackRange && _attack != null && _attack.CanAttack)
            {
                // Face the hero before committing. Facing is otherwise a by-product of velocity,
                // so an enemy that was already in range when it noticed the hero has never moved
                // and would swing in whatever direction it was left pointing.
                _motor.SetFacing(DirectionToTarget());

                Transition(EnemyLifecycleState.Attack);
                _attack.BeginAttack();
                _motor.Halt();
                return;
            }

            UpdateStuckDetection();
            _motor.SetMoveIntent(_evadeRemaining > 0f ? _stuckEvadeDirection : DirectionToTarget());
        }

        /// <summary>AI-004: holds still while the committed swing plays out.</summary>
        private void TickAttack()
        {
            _motor.Halt();

            if (_attack == null || !_attack.IsAttacking) Transition(EnemyLifecycleState.Recovery);
        }

        /// <summary>Returns to chasing once the swing is fully done.</summary>
        private void TickRecovery()
        {
            _motor.Halt();
            Transition(EnemyLifecycleState.Chase);
        }

        /// <summary>AI-002: walks home, and re-aggros if the hero comes back within detection.</summary>
        private void TickReturnToSpawn()
        {
            if (DistanceToTarget() <= _enemyData.DetectionRange)
            {
                Transition(EnemyLifecycleState.Chase);
                return;
            }

            float delta = SpawnPosition.x - transform.position.x;
            if (Mathf.Abs(delta) <= _enemyData.SpawnArrivalTolerance)
            {
                _motor.Halt();
                Transition(EnemyLifecycleState.Idle);
                return;
            }

            _motor.SetMoveIntent(Mathf.Sign(delta));
        }

        /// <summary>Stun runs on the fixed clock, not the think clock, so it ends on time.</summary>
        private void TickHurt()
        {
            _motor.Halt();
            if (StunRemaining > 0f) return;

            Transition(EnemyLifecycleState.Chase);
        }

        // ----- reactions ------------------------------------------------------------------

        /// <summary>
        /// AI-001: a landed hit interrupts everything except Death, cancelling a swing in progress.
        /// </summary>
        private void OnDamaged(HealthComponent health)
        {
            if (State == EnemyLifecycleState.Death) return;

            _attack?.CancelAttack();
            StunRemaining = _enemyData != null ? _enemyData.HurtStunDuration : 0f;
            Transition(EnemyLifecycleState.Hurt);
        }

        /// <summary>AI-003: Death stops the machine for good. The corpse is retired by health.</summary>
        private void OnDied(HealthComponent health)
        {
            _attack?.CancelAttack();
            _motor.Halt();
            _motor.ResetMotion();
            Transition(EnemyLifecycleState.Death);

            // TODO(AI-006): slice 4 returns the instance to its pool here instead of leaving
            // HealthComponent to deactivate it after the corpse timer.
        }

        // ----- helpers --------------------------------------------------------------------

        /// <summary>
        /// AI-005: an enemy that has chased for a whole window without covering the minimum
        /// distance is wedged, so it sidesteps rather than grinding into whatever blocks it.
        /// </summary>
        private void UpdateStuckDetection()
        {
            if (_evadeRemaining > 0f) return;
            if (_stuckTimer < _enemyData.StuckCheckWindow) return;

            float travelled = Mathf.Abs(transform.position.x - _stuckAnchorX);
            _stuckTimer = 0f;
            _stuckAnchorX = transform.position.x;

            if (travelled >= _enemyData.StuckMinDisplacement) return;

            _stuckEvadeDirection = -DirectionToTarget();
            if (_stuckEvadeDirection == 0) _stuckEvadeDirection = 1;
            _evadeRemaining = _enemyData.EvadeDuration;
        }

        private float DistanceToTarget()
            => Target == null ? float.MaxValue : Vector2.Distance(transform.position, Target.position);

        private int DirectionToTarget()
        {
            if (Target == null) return 0;

            float delta = Target.position.x - transform.position.x;
            if (Mathf.Approximately(delta, 0f)) return 0;
            return delta > 0f ? 1 : -1;
        }

        private void Transition(EnemyLifecycleState next)
        {
            if (State == next) return;

            State = next;

            if (next == EnemyLifecycleState.Chase)
            {
                _stuckTimer = 0f;
                _stuckAnchorX = transform.position.x;
            }

            _eventBus?.Publish(new EnemyStateChangedEvent(gameObject.GetInstanceID(), next));
        }
    }
}
