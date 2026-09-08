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
    /// <para><b>Why the hitbox is a manual OverlapBox.</b> SRS 21 describes hitboxes toggled by
    /// Animation Events on the attack clips. P1 has no clips, so there is nothing to hang an event
    /// on. Instead each step carries an active window in seconds and this component sweeps a box
    /// during that window from <c>FixedUpdate</c>. The observable behaviour is the same and it is
    /// testable without an Animator. TODO(COM-004): move the toggle to Animation Events in P2.</para>
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

        [Header("Presentation")]
        [Tooltip("Flipped to face the cursor (COM-009). This component is its only writer.")]
        [SerializeField] private SpriteRenderer _spriteRenderer;

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
        private EventBus _eventBus;
        private CombatSystem _combat;

        private readonly HashSet<int> _hitThisSwing = new HashSet<int>();
        private readonly Collider2D[] _overlapBuffer = new Collider2D[MaxTargetsPerSweep];

        private float _stepElapsed;
        private ContactFilter2D _enemyFilter;
        private bool _wasGrounded = true;

        /// <summary>
        /// Upper bound on colliders one sweep reports. Sized well above the 30 concurrent enemies
        /// of NFR-001 that could plausibly overlap a 1.2 x 1.0 box at once, and fixed so the sweep
        /// allocates nothing per frame.
        /// </summary>
        private const int MaxTargetsPerSweep = 16;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _motor = GetComponent<PlayerMotor>();
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

        private void Update()
        {
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
        }

        private void FixedUpdate()
        {
            if (State != CombatState.Attacking) return;

            AttackStep step = Attack.GetStep(ComboStep - 1);
            _stepElapsed += Time.fixedDeltaTime;

            // COM-004: the hitbox exists only inside the active window of the current step.
            bool active = _stepElapsed >= step.ActiveStartTime && _stepElapsed <= step.ActiveEndTime;
            IsHitboxActive = active;

            if (active) SweepHitbox(step);

            if (_stepElapsed < step.TotalDuration) return;

            EndStep();
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

            // TODO(COM-006): pass _stats.CritChance once P1-13 wires crits in slice 4. Until then
            // every hit resolves as a normal hit, which is what the crit-free slice expects.
            _combat.DealDamage(
                target,
                _stats.Attack,
                damageMultiplier,
                0f,
                DamageSource.BasicAttack,
                target.transform.position);
        }

        private void PublishCombo()
        {
            int maxSteps = Attack != null && _heroData != null
                ? Mathf.Min(Attack.StepCount, _heroData.ComboLength)
                : 0;

            _eventBus?.Publish(new ComboChangedEvent(ComboStep, maxSteps, ComboWindowRemaining));
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (Attack == null) return;

            Vector2 center = (Vector2)transform.position + AimDirection * Attack.HitboxOffsetDistance;
            Gizmos.color = IsHitboxActive ? Color.red : new Color(1f, 1f, 1f, 0.25f);

            Matrix4x4 previous = Gizmos.matrix;
            float angle = Mathf.Atan2(AimDirection.y, AimDirection.x) * Mathf.Rad2Deg;
            Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.Euler(0f, 0f, angle), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, Attack.HitboxSize);
            Gizmos.matrix = previous;
        }
#endif
    }
}
