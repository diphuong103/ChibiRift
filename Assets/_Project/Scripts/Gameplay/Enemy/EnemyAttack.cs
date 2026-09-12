using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// One enemy melee swing in three phases: windup, active, recovery (AI-004, COM-004).
    /// </summary>
    /// <remarks>
    /// Same mechanism as the hero's chain: the hitbox is a manual <c>Physics2D.OverlapBox</c> swept
    /// only inside the active window, with the window expressed in seconds rather than driven by
    /// Animation Events, because P1 has no clips (OI-18).
    ///
    /// <para><b>The swing is committed at windup.</b> Once started it plays out even if the hero
    /// walks away. That is deliberate, not an oversight: an attack that could be cancelled mid
    /// telegraph would be unreadable, and the fixed rhythm is what makes the dash of slice 4 a
    /// skill rather than a coin flip.</para>
    /// </remarks>
    [RequireComponent(typeof(EnemyMotor))]
    [DisallowMultipleComponent]
    public sealed class EnemyAttack : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("Supplies phase timing, cooldown, reach and the Attack stat (NFR-007).")]
        [SerializeField] private EnemyData _enemyData;


        /// <summary>True while a swing is running, from windup through recovery.</summary>
        public bool IsAttacking { get; private set; }

        /// <summary>True only inside the active window (COM-004).</summary>
        public bool IsHitboxActive { get; private set; }

        /// <summary>Seconds until the next swing may start (AI-004). Zero means ready.</summary>
        public float CooldownRemaining { get; private set; }

        /// <summary>Seconds elapsed in the current swing.</summary>
        public float Elapsed => _elapsed;

        /// <summary>
        /// Scales <see cref="StatBlock.Attack"/> before it reaches <see cref="CombatSystem"/>
        /// (ELT-001). Set by <see cref="EnemyController.Configure"/>; 1 for a non-elite or a
        /// scene-placed enemy that was never configured.
        /// </summary>
        public float DamageMultiplier { get; set; } = 1f;

        /// <summary>Archetype supplying the numbers.</summary>
        public EnemyData Data
        {
            get => _enemyData;
            set => _enemyData = value;
        }

        /// <summary>True when range and cooldown both allow a swing to start (AI-004).</summary>
        public bool CanAttack => !IsAttacking && CooldownRemaining <= 0f;

        private EnemyMotor _motor;
        private CombatSystem _combat;
        private ContactFilter2D _playerFilter;
        private readonly Collider2D[] _overlapBuffer = new Collider2D[MaxTargetsPerSweep];

        private float _elapsed;
        private bool _hitLandedThisSwing;

        /// <summary>One hero, but sized above one so an overlapping trigger cannot mask them.</summary>
        private const int MaxTargetsPerSweep = 4;

        private EnemyAttackConfig Config => _enemyData != null ? _enemyData.Attack : default;

        private void Awake()
        {
            _motor = GetComponent<EnemyMotor>();
            _playerFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = LayerMask.GetMask(GameLayers.Player),
                useTriggers = true
            };
        }

        private void Start()
        {
            if (ServiceLocator.Current == null) return;
            if (!ServiceLocator.Current.TryGet(out _combat))
            {
                GameLog.Error("Enemy",
                    "No CombatSystem registered. Enter play from the Boot scene so the composition root exists.");
            }
        }

        /// <summary>Starts a swing (AI-004). Ignored when one is running or the cooldown is live.</summary>
        public void BeginAttack()
        {
            if (!CanAttack || _enemyData == null) return;

            IsAttacking = true;
            IsHitboxActive = false;
            _elapsed = 0f;
            _hitLandedThisSwing = false;
        }

        /// <summary>Cancels a swing without starting the cooldown. Used by Hurt and Death.</summary>
        public void CancelAttack()
        {
            IsAttacking = false;
            IsHitboxActive = false;
            _elapsed = 0f;
        }

        /// <summary>Label every instance's FixedUpdate reports under for NFR-002 profiling.</summary>
        private const string AllocationLabel = "EnemyAttack.FixedUpdate";

        private void FixedUpdate()
        {
            // NFR-002 profiling (OI-32). try/finally: the idle (not attacking) path returns early
            // and must still close the sample.
            AllocationProfiler.BeginSample(AllocationLabel);
            try
            {
                float dt = Time.fixedDeltaTime;

                if (!IsAttacking)
                {
                    // AI-004: the cooldown runs from the end of recovery, so it is ticked here and
                    // not during the swing.
                    if (CooldownRemaining > 0f) CooldownRemaining = Mathf.Max(CooldownRemaining - dt, 0f);
                    return;
                }

                EnemyAttackConfig config = Config;
                _elapsed += dt;

                IsHitboxActive = _elapsed >= config.ActiveStartTime && _elapsed <= config.ActiveEndTime;
                if (IsHitboxActive) SweepHitbox();

                if (_elapsed < config.TotalDuration) return;

                IsAttacking = false;
                IsHitboxActive = false;
                CooldownRemaining = config.Cooldown;
            }
            finally
            {
                AllocationProfiler.EndSample(AllocationLabel);
            }
        }

        private void SweepHitbox()
        {
            // One hit per swing. The active window spans several fixed steps, so without this the
            // hero would take a hit on every step inside it.
            if (_hitLandedThisSwing) return;

            Vector2 centre = HitboxCentre();
            int count = Physics2D.OverlapBox(centre, Config.HitboxSize, 0f, _playerFilter, _overlapBuffer);

            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _overlapBuffer[i];
                if (hit == null) continue;

                var target = hit.GetComponentInParent<HealthComponent>();
                if (target == null || target.IsDead) continue;

                ResolveHit(target);
                _hitLandedThisSwing = true;
                return;
            }
        }

        /// <summary>
        /// Routes the hit through the shared pipeline (HPS-003). No arithmetic here: the formula,
        /// the guards and the events all belong to <see cref="CombatSystem"/>.
        /// </summary>
        private void ResolveHit(HealthComponent target)
        {
            if (_combat == null) return;

            _combat.DealDamage(
                target,
                _enemyData.BaseStats.Attack * DamageMultiplier,
                1f,
                0f,
                DamageSource.BasicAttack,
                target.transform.position,
                transform.position);
        }

        private Vector2 HitboxCentre()
        {
            float direction = _motor != null ? _motor.Facing : 1f;
            return (Vector2)transform.position + new Vector2(direction * Config.HitboxOffsetDistance, 0f);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsHitboxActive ? Color.red : Color.grey;
            Gizmos.DrawWireCube(HitboxCentre(), Config.HitboxSize);
        }
#endif
    }
}
