using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// The dash: a short invulnerable burst on Left Shift (MOV-006, MOV-007).
    /// </summary>
    /// <remarks>
    /// <para><b>Direction.</b> The movement input if the player is holding one, otherwise the way
    /// the hero is already facing. Falling back to facing matters because the hero faces the cursor
    /// (COM-009), so a standing player can dash toward what they are aiming at without first having
    /// to start walking.</para>
    ///
    /// <para><b>Invulnerability.</b> Opened through the same
    /// <see cref="HealthComponent.BeginInvulnerability"/> that a hit uses, which takes the longer
    /// of the running and requested windows. A dash therefore cannot cut short the 0.8s window the
    /// player earned by being hit, and being hit cannot shorten a dash's own 0.25s.</para>
    /// </remarks>
    [RequireComponent(typeof(PlayerMotor))]
    [RequireComponent(typeof(HealthComponent))]
    [DisallowMultipleComponent]
    public sealed class PlayerDash : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("Supplies distance, duration, i-frames, cooldown and the buffer window (SRS 35).")]
        [SerializeField] private HeroData _heroData;

        /// <summary>Seconds until the dash is available again (MOV-006). Zero means ready.</summary>
        public float CooldownRemaining { get; private set; }

        /// <summary>True while the dash window is open.</summary>
        public bool IsDashing => _motor != null && _motor.IsDashing;

        /// <summary>True when a press would start a dash right now.</summary>
        public bool CanDash => CooldownRemaining <= 0f && !IsDashing && !_health.IsDead;

        /// <summary>Seconds left on a remembered press (P1-07). Exposed for the overlay and tests.</summary>
        public float BufferRemaining => _buffer.Remaining;

        private PlayerMotor _motor;
        private HealthComponent _health;
        private PlayerCombat _combat;
        private EventBus _eventBus;
        private InputBuffer _buffer;

        private bool _wasDashing;

        private DashConfig Config => _heroData != null ? _heroData.Dash : default;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _health = GetComponent<HealthComponent>();
            _combat = GetComponent<PlayerCombat>();

            if (_heroData == null)
                GameLog.Error("Dash", $"{name} has no HeroData; the dash has no tuning values (SRS 30).");
        }

        private void Start()
        {
            if (ServiceLocator.Current != null) ServiceLocator.Current.TryGet(out _eventBus);
        }

        private void OnEnable()
        {
            if (_health != null) _health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (_health != null) _health.Died -= OnDied;
        }

        /// <summary>
        /// Remembers a dash press (P1-07). Buffered rather than acted on at once so a press during
        /// hit stop, or a few frames before the cooldown expires, still counts.
        /// </summary>
        public void RequestDash() => _buffer.Press(Config.BufferSeconds);

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            if (CooldownRemaining > 0f)
            {
                CooldownRemaining = Mathf.Max(CooldownRemaining - dt, 0f);

                // MOV-006 and SRS 19.2 both ask for a visible dash cooldown. Published here rather
                // than polled by the HUD, which cannot see this assembly.
                _eventBus?.Publish(new DashCooldownChangedEvent(CooldownRemaining, Config.Cooldown));
            }
            _buffer.Tick(dt);

            // The dash ending is where the cooldown starts, so it is measured from the end of the
            // window rather than from the press: two dashes can never overlap.
            if (_wasDashing && !_motor.IsDashing)
            {
                CooldownRemaining = Config.Cooldown;
                _wasDashing = false;
            }

            if (!_buffer.HasPending) return;
            if (!CanDash) return;
            if (!_buffer.TryConsume()) return;

            StartDash();
        }

        private void StartDash()
        {
            DashConfig config = Config;

            _motor.BeginDash(Direction(), config.Speed, config.Duration);
            _wasDashing = true;
            _eventBus?.Publish(new DashCooldownChangedEvent(config.Cooldown, config.Cooldown));

            // HPS-005: the same window a hit opens, so there is only one notion of invulnerability.
            _health.BeginInvulnerability(config.IFrameDuration);

            // COM-003: dashing out of a chain ends it, the same as leaving the ground does.
            if (_combat != null) _combat.ResetCombo();

            _eventBus?.Publish(new DashStartedEvent(
                gameObject.GetInstanceID(), transform.position, config.Duration, config.IFrameDuration));
        }

        /// <summary>
        /// MOV-006: the held movement direction, or the facing when the player is standing still.
        /// </summary>
        private Vector2 Direction()
        {
            float intent = _motor.MoveIntent;
            if (!Mathf.Approximately(intent, 0f)) return new Vector2(Mathf.Sign(intent), 0f);

            // Facing follows the cursor from slice 2 on, so this aims the dash where the player
            // is already looking rather than where they last walked.
            if (_combat != null && !Mathf.Approximately(_combat.AimDirection.x, 0f))
                return new Vector2(Mathf.Sign(_combat.AimDirection.x), 0f);

            return new Vector2(_motor.Facing, 0f);
        }

        /// <summary>A dash must not outlive the hero (MOV-006).</summary>
        private void OnDied(HealthComponent health)
        {
            _motor.CancelDash();
            _buffer.Clear();
        }
    }
}
