using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Turns gameplay events into sound cues (SRS 22, P1-31).
    /// </summary>
    /// <remarks>
    /// <para><b>Silent by design, for now.</b> No audio ships in P1. Every clip field on
    /// <see cref="SfxLibrary"/> may be empty and the game must play exactly the same, so a missing
    /// clip is dropped without an error and is reported once per session, not once per hit. Adding
    /// sound later is dropping nine files into the asset — no code changes.</para>
    ///
    /// <para><b>Why this lives in Gameplay.</b> <see cref="SfxLibrary"/> is a ScriptableObject, so
    /// it belongs to ChibiRift.Data; the audio service lives in ChibiRift.Core, which cannot
    /// reference Data. This component is the join: it holds the asset, listens to events, and hands
    /// plain <c>AudioClip</c>s to <see cref="IAudioService.PlayOneShot"/>.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SfxPlayer : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("The nine cues. Fields may be empty until the audio files exist; see README.")]
        [SerializeField] private SfxLibrary _library;

        private EventBus _eventBus;
        private IAudioService _audio;
        private PlayerCombat _heroCombat;
        private HealthComponent _heroHealth;
        private PlayerMotor _heroMotor;

        private void Awake()
        {
            _heroCombat = GetComponent<PlayerCombat>();
            _heroHealth = GetComponent<HealthComponent>();
            _heroMotor = GetComponent<PlayerMotor>();
        }

        private void Start()
        {
            if (ServiceLocator.Current == null) return;

            ServiceLocator.Current.TryGet(out _eventBus);
            ServiceLocator.Current.TryGet(out _audio);

            if (_eventBus == null) return;

            _eventBus.Subscribe<DamageAppliedEvent>(OnDamageApplied);
            _eventBus.Subscribe<EntityDiedEvent>(OnEntityDied);
            _eventBus.Subscribe<DashStartedEvent>(OnDashStarted);
            _eventBus.Subscribe<ComboChangedEvent>(OnComboChanged);

            if (_heroHealth != null) _heroHealth.Damaged += OnHeroDamaged;

            // A local event rather than one on the bus: nothing outside this object cares that the
            // hero jumped, and a bus event would have to be filtered by id on arrival.
            if (_heroMotor != null) _heroMotor.Jumped += PlayJump;
        }

        private void OnDisable()
        {
            if (_eventBus != null)
            {
                _eventBus.Unsubscribe<DamageAppliedEvent>(OnDamageApplied);
                _eventBus.Unsubscribe<EntityDiedEvent>(OnEntityDied);
                _eventBus.Unsubscribe<DashStartedEvent>(OnDashStarted);
                _eventBus.Unsubscribe<ComboChangedEvent>(OnComboChanged);
            }

            if (_heroHealth != null) _heroHealth.Damaged -= OnHeroDamaged;
            if (_heroMotor != null) _heroMotor.Jumped -= PlayJump;
        }

        /// <summary>Plays the jump cue. Called by the motor rather than driven by an event.</summary>
        public void PlayJump() => Play(_library != null ? _library.Jump : null);

        private void OnDamageApplied(DamageAppliedEvent evt)
        {
            if (_library == null) return;

            // A crit gets its own cue, so the ear separates it from an ordinary hit the same way
            // the colour and the freeze separate it for the eye and the hands.
            Play(evt.Result.WasCritical ? _library.Crit : _library.Hit);
        }

        private void OnEntityDied(EntityDiedEvent evt)
        {
            if (_library == null || evt.IsPlayer) return;
            Play(_library.EnemyDeath);
        }

        private void OnDashStarted(DashStartedEvent evt) => Play(_library != null ? _library.Dash : null);

        private void OnComboChanged(ComboChangedEvent evt)
        {
            if (_library == null || evt.Step <= 0) return;

            // Fires as the swing starts, not as it connects: a whiff should still make a sound.
            Play(_library.AttackStep(evt.Step));
        }

        private void OnHeroDamaged(HealthComponent health) => Play(_library != null ? _library.HurtHero : null);

        private void Play(AudioClip clip) => _audio?.PlayOneShot(clip);
    }
}
