using System.Collections.Generic;
using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// The basic attack and its three hit chain (COM-001, COM-002, COM-003, COM-004), aimed with
    /// the mouse and with no auto-target (COM-009).
    /// </summary>
    /// <remarks>
    /// <para><b>The hitbox toggles from Animation Events when the clip carries them, a manual
    /// OverlapBox timer otherwise (COM-004, P2 slice 1).</b> P1 shipped with no animation clips,
    /// so each step's active window lived only in seconds
    /// (<see cref="AttackStep.ActiveStartTime"/>/<see cref="AttackStep.ActiveEndTime"/>) and this
    /// component swept a box during that window from <c>FixedUpdate</c> — see OI-18. Real Hero
    /// clips (built by <c>HeroAnimatorBuilder</c>) now carry <see cref="OnAttackActiveStart"/>/
    /// <see cref="OnAttackActiveEnd"/> events at exactly those same seconds, so the seconds stay
    /// the one source of truth either way. <see cref="DetectEventDrivenSteps"/> checks once, at
    /// <see cref="Awake"/>, which of the three attack clips actually carry both events; a step
    /// whose clip is missing one (or has no clip at all — the placeholder hero, or a spritesheet
    /// swapped in without re-running the animator builder) keeps using the timer, logging a warning
    /// once so a forgotten event does not fail silently.</para>
    ///
    /// <para>Damage never leaves this class as arithmetic: it always goes through
    /// <see cref="CombatSystem"/>, which is the single pipeline HPS-003 requires.</para>
    /// </remarks>
    [RequireComponent(typeof(PlayerStats))]
    [DisallowMultipleComponent]
    public sealed class PlayerCombat : MonoBehaviour
    {
        /// <summary>Where the swing is in its lifetime. Recovery is folded into Attacking.</summary>
        public enum CombatState
        {
            Idle = 0,
            Attacking = 1
        }

        [Header("Data")]
        [Tooltip("Supplies combo length, combo window and the basic-attack chain.")]
        [SerializeField] private HeroData _heroData;

        [Tooltip("Supplies the sweep buffer size, which is a performance budget tied to NFR-001.")]
        [SerializeField] private BalanceConfig _balanceConfig;

        [Header("Presentation")]
        [Tooltip("Flipped to face the cursor (COM-009). This component is its only writer.")]
        [SerializeField] private SpriteRenderer _spriteRenderer;

        [Tooltip("Attack1/2/3 clips, index 0..2 — checked only for OnAttackActiveStart/End Animation Events (COM-004), never played from here. Empty until HeroAnimatorBuilder wires a real Animator; the timer fallback covers that gap.")]
        [SerializeField] private AnimationClip[] _attackClips = System.Array.Empty<AnimationClip>();

        /// <summary>Current step of the chain: 0 when idle, 1..3 while attacking (COM-002).</summary>
        public int ComboStep { get; private set; }

        /// <summary>Seconds left to chain the next hit before the combo resets (COM-003).</summary>
        public float ComboWindowRemaining { get; private set; }

        /// <summary>Whether a swing is running.</summary>
        public CombatState State { get; private set; } = CombatState.Idle;

        /// <summary>True only while the current step's active window is open (COM-004).</summary>
        public bool IsHitboxActive { get; private set; }

        /// <summary>Centre of the hitbox this frame, in world space. Exposed for tests and gizmos.</summary>
        public Vector2 HitboxCenter { get; private set; }

        /// <summary>Aim direction, normalised. Drives facing and the hitbox offset (COM-009).</summary>
        public Vector2 AimDirection { get; private set; } = Vector2.right;

        /// <summary>Horizontal speed scale to apply while attacking; 1 when idle (COM-001).</summary>
        public float MoveSpeedMultiplier =>
            State == CombatState.Attacking && Attack != null
                ? Attack.MoveSpeedMultiplierWhileAttacking
                : 1f;

        private AttackData Attack => _heroData != null ? _heroData.BasicAttack : null;

        private PlayerStats _stats;
        private PlayerMotor _motor;
        private HealthComponent _health;
        private EventBus _eventBus;
        private CombatSystem _combat;

        private readonly HashSet<int> _hitThisSwing = new HashSet<int>();
        private Collider2D[] _overlapBuffer;

        private float _stepElapsed;
        private ContactFilter2D _enemyFilter;
        private bool _wasGrounded = true;

        /// <summary>
        /// Index <c>N</c> is true when <c>_attackClips[N - 1]</c> carries both hitbox events
        /// (COM-004). Index 0 is always false — <see cref="ComboStep"/> is never 0 while attacking.
        /// </summary>
        private bool[] _stepEventDriven = System.Array.Empty<bool>();

        private bool _hasAnimatorController;
        private bool _warnedMissingEvents;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _motor = GetComponent<PlayerMotor>();
            _health = GetComponent<HealthComponent>();

            // Allocated once from the configured budget. A fixed array is the point: the sweep runs
            // every fixed step of every swing and must not allocate (NFR-002).
            int capacity = _balanceConfig != null ? _balanceConfig.MaxTargetsPerSweep : 1;
            _overlapBuffer = new Collider2D[capacity];
            // Unity 6 deprecated OverlapBoxNonAlloc in favour of the ContactFilter2D overload.
            // Triggers are included: an enemy hurtbox may legitimately be a trigger and must
            // still be hittable.
            _enemyFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = LayerMask.GetMask(GameLayers.Enemy),
                useTriggers = true
            };

            if (_heroData == null)
                GameLog.Error("Combat", $"{name} has no HeroData; the attack chain is unavailable (SRS 30).");
            else if (Attack == null)
                GameLog.Error("Combat", $"{_heroData.name} has no AttackData; the attack chain is unavailable (SRS 30).");

            DetectEventDrivenSteps();
        }

        /// <summary>
        /// Checked once, not every swing: <see cref="_attackClips"/> is wired at edit time by
        /// <c>HeroAnimatorBuilder</c> and does not change at runtime, so which steps are
        /// event-driven cannot change either.
        /// </summary>
        private void DetectEventDrivenSteps()
        {
            int maxSteps = Attack != null ? Attack.StepCount : 0;
            _stepEventDriven = new bool[maxSteps + 1];
            _hasAnimatorController = _attackClips != null && _attackClips.Length > 0;

            if (_attackClips == null) return;

            for (int step = 1; step <= maxSteps && step - 1 < _attackClips.Length; step++)
            {
                AnimationClip clip = _attackClips[step - 1];
                if (clip != null) _stepEventDriven[step] = ClipHasBothHitboxEvents(clip);
            }
        }

        private static bool ClipHasBothHitboxEvents(AnimationClip clip)
        {
            bool hasStart = false;
            bool hasEnd = false;

            foreach (AnimationEvent evt in clip.events)
            {
                if (evt.functionName == nameof(OnAttackActiveStart)) hasStart = true;
                else if (evt.functionName == nameof(OnAttackActiveEnd)) hasEnd = true;
            }

            return hasStart && hasEnd;
        }

        private void Start()
        {
            if (ServiceLocator.Current == null) return;

            ServiceLocator.Current.TryGet(out _eventBus);
            if (!ServiceLocator.Current.TryGet(out _combat))
            {
                GameLog.Error("Combat",
                    "No CombatSystem registered. Enter play from the Boot scene so the composition root exists.");
            }
        }

        /// <summary>
        /// Points the hero and the hitbox at a world position (COM-009).
        /// Called every frame by <see cref="PlayerController"/> with the cursor position.
        /// </summary>
        public void SetAimTarget(Vector2 worldPosition)
        {
            Vector2 toTarget = worldPosition - (Vector2)transform.position;

            // A cursor exactly on the hero gives no direction; keep the last one rather than
            // snapping to an arbitrary axis.
            if (toTarget.sqrMagnitude > Mathf.Epsilon) AimDirection = toTarget.normalized;

            // COM-009: the single writer of flipX. See PlayerMotor.UpdateFacing and OI-20.
            if (_spriteRenderer != null) _spriteRenderer.flipX = AimDirection.x < 0f;
        }

        /// <summary>
        /// Requests an attack (COM-001). Starts the chain when idle, or queues the next step when
        /// the previous swing has finished and the combo window is still open (COM-002).
        /// </summary>
        public void RequestAttack()
        {
            if (Attack == null) return;
            if (State == CombatState.Attacking) return;

            int maxSteps = Mathf.Min(Attack.StepCount, _heroData.ComboLength);
            if (maxSteps <= 0) return;

            // COM-002: chain on while the window is open, otherwise start a fresh chain.
            int next = ComboWindowRemaining > 0f ? ComboStep + 1 : 1;

            // COM-002: the third hit ends the chain; a fourth press starts over at hit one.
            if (next > maxSteps) next = 1;

            BeginStep(next);
        }

        private void OnEnable()
        {
            // COM-003: taking a hit drops the chain. The rule was written in slice 2 but had
            // nothing that could hit the hero until enemies existed.
            if (_health != null) _health.Damaged += OnHeroDamaged;
        }

        private void OnDisable()
        {
            if (_health != null) _health.Damaged -= OnHeroDamaged;
        }

        private void OnHeroDamaged(HealthComponent health) => ResetCombo();

        /// <summary>Labels this instance's callbacks report under for NFR-002 profiling.</summary>
        private const string UpdateAllocationLabel = "PlayerCombat.Update";
        private const string FixedUpdateAllocationLabel = "PlayerCombat.FixedUpdate";

        private void Update()
        {
            // NFR-002 profiling (OI-32). No early return here, so Begin/End bracket the body directly.
            AllocationProfiler.BeginSample(UpdateAllocationLabel);

            // COM-003: the window only runs between swings. During a swing the next input is
            // accepted the moment the swing ends, so the window must not expire underneath it.
            if (State == CombatState.Idle && ComboWindowRemaining > 0f)
            {
                ComboWindowRemaining -= Time.deltaTime;
                if (ComboWindowRemaining <= 0f) ResetCombo();
            }

            // COM-003: leaving the ground drops the chain.
            bool grounded = _motor == null || _motor.IsGrounded;
            if (_wasGrounded && !grounded) ResetCombo();
            _wasGrounded = grounded;

            AllocationProfiler.EndSample(UpdateAllocationLabel);
        }

        private void FixedUpdate()
        {
            // NFR-002 profiling (OI-32). try/finally: idle (not attacking) is the common case and
            // returns early.
            AllocationProfiler.BeginSample(FixedUpdateAllocationLabel);
            try
            {
                if (State != CombatState.Attacking) return;

                AttackStep step = Attack.GetStep(ComboStep - 1);
                _stepElapsed += Time.fixedDeltaTime;

                // COM-004: an event-driven step has IsHitboxActive toggled by OnAttackActiveStart/
                // End below, called by the clip itself; this still re-sweeps every fixed step while
                // it is open, matching the timer path's behaviour of catching a target that walks
                // into the box mid-window rather than only checking once. Everything else uses the
                // ActiveStartTime/ActiveEndTime timer directly (OI-18).
                bool eventDriven = ComboStep < _stepEventDriven.Length && _stepEventDriven[ComboStep];
                if (eventDriven)
                {
                    if (IsHitboxActive) SweepHitbox(step);
                }
                else
                {
                    bool active = _stepElapsed >= step.ActiveStartTime && _stepElapsed <= step.ActiveEndTime;
                    IsHitboxActive = active;
                    if (active) SweepHitbox(step);

                    WarnOnceIfMissingEvents();
                }

                if (_stepElapsed < step.TotalDuration) return;

                EndStep();
            }
            finally
            {
                AllocationProfiler.EndSample(FixedUpdateAllocationLabel);
            }
        }

        private void BeginStep(int step)
        {
            ComboStep = step;
            State = CombatState.Attacking;
            _stepElapsed = 0f;
            IsHitboxActive = false;

            // COM-004: one swing may damage a given target only once, so the set is cleared per
            // swing rather than per frame. The active window spans several fixed steps, so without
            // this every step inside the window would land another hit.
            _hitThisSwing.Clear();

            PublishCombo();
        }

        private void EndStep()
        {
            State = CombatState.Idle;
            IsHitboxActive = false;

            int maxSteps = Mathf.Min(Attack.StepCount, _heroData.ComboLength);

            // COM-002: after the final hit the chain is over; no window, back to Idle.
            if (ComboStep >= maxSteps)
            {
                ResetCombo();
                return;
            }

            // COM-003: the window to chain the next hit starts when this one finishes.
            ComboWindowRemaining = _heroData.ComboWindow;
            PublishCombo();
        }

        /// <summary>Drops the chain back to zero (COM-003). Safe to call when already idle.</summary>
        public void ResetCombo()
        {
            if (ComboStep == 0 && ComboWindowRemaining <= 0f) return;

            ComboStep = 0;
            ComboWindowRemaining = 0f;
            PublishCombo();
        }

        /// <summary>
        /// Animation Event receiver, called by the current attack clip at its step's
        /// ActiveStartTime (COM-004). Opens the hitbox for the same current step
        /// <see cref="FixedUpdate"/> would otherwise be timing with the seconds-based fallback.
        /// </summary>
        public void OnAttackActiveStart()
        {
            if (State != CombatState.Attacking) return;

            IsHitboxActive = true;
            SweepHitbox(Attack.GetStep(ComboStep - 1));
        }

        /// <summary>Animation Event receiver, called at the step's ActiveEndTime (COM-004).</summary>
        public void OnAttackActiveEnd()
        {
            if (State != CombatState.Attacking) return;

            IsHitboxActive = false;
        }

        private void WarnOnceIfMissingEvents()
        {
            if (!_hasAnimatorController || _warnedMissingEvents) return;
            _warnedMissingEvents = true;

            GameLog.Warn("Combat",
                $"Attack{ComboStep} has no OnAttackActiveStart/OnAttackActiveEnd Animation Event; " +
                "falling back to the ActiveStartTime/ActiveEndTime timer (COM-004). Re-run the Hero " +
                "Animator builder if a spritesheet was swapped in without one.");
        }

        private void SweepHitbox(in AttackStep step)
        {
            // COM-009: the box sits along the aim vector, so the hero hits where the cursor is.
            HitboxCenter = (Vector2)transform.position + AimDirection * Attack.HitboxOffsetDistance;

            // Rotating the box to the aim angle keeps a diagonal swing the same shape as a
            // horizontal one, which a non-rotated box would not.
            float angle = Mathf.Atan2(AimDirection.y, AimDirection.x) * Mathf.Rad2Deg;

            int count = Physics2D.OverlapBox(
                HitboxCenter, Attack.HitboxSize, angle, _enemyFilter, _overlapBuffer);

            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _overlapBuffer[i];
                if (hit == null) continue;

                var target = hit.GetComponentInParent<HealthComponent>();
                if (target == null || target.IsDead) continue;

                // COM-004: one hit per target per swing.
                if (!_hitThisSwing.Add(target.EntityId)) continue;

                ResolveHit(target, step.DamageMultiplier);
            }
        }

        /// <summary>
        /// Resolves one hit through the shared pipeline (HPS-003). Damage is never computed here:
        /// <see cref="CombatSystem"/> owns the formula, the guards and the events.
        /// </summary>
        private void ResolveHit(HealthComponent target, float damageMultiplier)
        {
            if (_combat == null) return;

            // COM-006: the hero's own crit chance. Rolled inside CombatSystem with the seeded RNG,
            // so a hit is reproducible for a given seed (RNG-004).
            _combat.DealDamage(
                target,
                _stats.Attack,
                damageMultiplier,
                _stats.CritChance,
                DamageSource.BasicAttack,
                target.transform.position,
                transform.position);
        }

        private void PublishCombo()
        {
            int maxSteps = Attack != null && _heroData != null
                ? Mathf.Min(Attack.StepCount, _heroData.ComboLength)
                : 0;

            _eventBus?.Publish(new ComboChangedEvent(ComboStep, maxSteps, ComboWindowRemaining));
        }

        private void LateUpdate()
        {
            // A per-frame snapshot for the development overlay. Separate from ComboChangedEvent,
            // which fires only on transitions: the overlay needs the aim vector and the live
            // hitbox flag every frame, and neither of those is a transition.
            _eventBus?.Publish(new PlayerCombatStateEvent(
                State == CombatState.Attacking,
                IsHitboxActive,
                ComboStep,
                ComboWindowRemaining,
                AimDirection));
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (Attack == null) return;

            Vector2 center = (Vector2)transform.position + AimDirection * Attack.HitboxOffsetDistance;
            Gizmos.color = IsHitboxActive ? Color.red : Color.grey;

            Matrix4x4 previous = Gizmos.matrix;
            float angle = Mathf.Atan2(AimDirection.y, AimDirection.x) * Mathf.Rad2Deg;
            Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.Euler(0f, 0f, angle), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, Attack.HitboxSize);
            Gizmos.matrix = previous;
        }
#endif
    }
}
