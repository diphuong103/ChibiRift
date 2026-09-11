using System;
using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Hit points for anything that can be hurt: the hero, every enemy, the training dummies
    /// (HPS-001, HPS-002, HPS-004, HPS-006, HPS-007).
    /// </summary>
    /// <remarks>
    /// <para>This is the project's <b>only</b> implementation of <see cref="IDamageable"/> and the
    /// only place health is written. Before P1 slice 2 the hero and enemies each carried their own
    /// copy of health plus their own death transition, which meant two independent "am I dead"
    /// flags and, on the hero, two components answering <c>GetComponent&lt;IDamageable&gt;()</c>
    /// depending on component order. Consolidating here removes that ambiguity.</para>
    ///
    /// <para><see cref="ApplyDamage"/> is reached only through <see cref="CombatSystem"/>
    /// (HPS-003). Nothing else may subtract health; <c>DamagePipelineSourceTests</c> asserts it.
    /// The check is textual, not a compiler guarantee — see OI-19.</para>
    ///
    /// <para><see cref="IsInvulnerable"/> lives here rather than on the hero's stat component
    /// because this is the gate damage passes through, and because enemies have no stat
    /// component at all. Dash i-frames (slice 4) and post-hit i-frames (slice 3) both open the
    /// window through <see cref="BeginInvulnerability"/>.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class HealthComponent : MonoBehaviour, IDamageable
    {
        [Header("Seeding")]
        [Tooltip("Optional. When set, health and defences are seeded from this asset on Awake, so a prefab placed straight into a scene needs no code to configure it.")]
        [SerializeField] private EnemyData _sourceData;

        [Tooltip("Marks this as the hero, so UI can tell the player bar from an enemy bar.")]
        [SerializeField] private bool _isPlayer;

        [Tooltip("Disabled the moment health reaches zero, so a corpse stops blocking and stops taking hits.")]
        [SerializeField] private Collider2D _bodyCollider;

        [Tooltip("Seconds of invulnerability granted automatically after each hit that lands (HPS-005). Zero means none, which is what enemies use.")]
        [Min(0f)]
        [SerializeField] private float _hurtIFrameDuration;

        /// <summary>Raised on every health change. Local listeners; UI uses the EventBus instead.</summary>
        public event Action<float, float> HealthChanged;

        /// <summary>Raised exactly once, when health first reaches zero (HPS-006, HPS-007).</summary>
        public event Action<HealthComponent> Died;

        /// <summary>
        /// Raised once the corpse timer expires (HPS-007).
        /// </summary>
        /// <remarks>
        /// This component announces that the body is done; it does <b>not</b> deactivate the object.
        /// Doing both would give the GameObject two owners — this and the pool — and releasing an
        /// instance that is already inactive is how a pool ends up handing the same object to two
        /// callers. <see cref="EnemySpawner"/> owns the lifetime; see the ownership table in README.
        /// </remarks>
        public event Action<HealthComponent> CorpseExpired;

        /// <summary>
        /// Raised on every hit that actually landed, before the death check. The enemy state
        /// machine uses it to enter Hurt; the hero's combat component uses it to drop the combo.
        /// </summary>
        public event Action<HealthComponent> Damaged;

        /// <inheritdoc />
        public float CurrentHealth { get; private set; }

        /// <inheritdoc />
        public float MaxHealth { get; private set; }

        /// <inheritdoc />
        public bool IsDead { get; private set; }

        /// <inheritdoc />
        public bool IsInvulnerable => _invulnerableRemaining > 0f;

        /// <inheritdoc />
        public float Defense { get; private set; }

        /// <inheritdoc />
        public float DamageReduction { get; private set; }

        /// <summary>True for the hero, false for everything else. Tags the events UI reads.</summary>
        public bool IsPlayer => _isPlayer;

        /// <summary>Stable id for the events, so UI can key a health bar without a reference.</summary>
        public int EntityId => gameObject.GetInstanceID();

        /// <summary>Seconds until the corpse is retired; negative means no retirement is pending.</summary>
        public float CorpseTimeRemaining => _corpseRemaining;

        /// <summary>Automatic post-hit invulnerability, in seconds (HPS-005). Zero disables it.</summary>
        public float HurtIFrameDuration
        {
            get => _hurtIFrameDuration;
            set => _hurtIFrameDuration = Mathf.Max(value, 0f);
        }

        private EventBus _eventBus;
        private float _invulnerableRemaining;
        private float _corpseRemaining = -1f;
        private bool _seeded;

        private void Awake()
        {
            if (_bodyCollider == null) _bodyCollider = GetComponent<Collider2D>();
            if (_sourceData != null) SeedFrom(_sourceData);
        }

        private void Start()
        {
            if (ServiceLocator.Current != null) ServiceLocator.Current.TryGet(out _eventBus);

            // Publishing here rather than in Initialize: a component seeded from the inspector has
            // no other moment to announce itself, and the bus may not exist yet during Awake.
            PublishHealth();
        }

        private void OnEnable()
        {
            // A pooled instance comes back with the corpse timer still counting down.
            _corpseRemaining = -1f;
        }

        /// <summary>Label every instance's Update reports under for NFR-002 profiling.</summary>
        private const string AllocationLabel = "HealthComponent.Update";

        private void Update()
        {
            // NFR-002 profiling (OI-32). try/finally: the guard below returns early for anything
            // that is not currently a corpse, which is most instances on most frames.
            AllocationProfiler.BeginSample(AllocationLabel);
            try
            {
                float dt = Time.deltaTime;

                if (_invulnerableRemaining > 0f) _invulnerableRemaining -= dt;

                if (_corpseRemaining < 0f) return;

                _corpseRemaining -= dt;
                if (_corpseRemaining > 0f) return;

                // HPS-007: announce, do not deactivate. Whoever owns this instance decides what
                // happens to it — the pool for an enemy, nothing at all for a scene-placed dummy.
                _corpseRemaining = -1f;
                CorpseExpired?.Invoke(this);
            }
            finally
            {
                AllocationProfiler.EndSample(AllocationLabel);
            }
        }

        /// <summary>
        /// Seeds health and defences from an enemy archetype (HPS-002).
        /// Primitives rather than a <c>StatBlock</c> would work equally well; this overload just
        /// saves every caller from unpacking the asset.
        /// </summary>
        public void SeedFrom(EnemyData data)
        {
            if (data == null) return;

            StatBlock stats = data.BaseStats;
            Initialize(stats.MaxHealth, stats.Defense, stats.DamageReduction);
        }

        /// <summary>
        /// Seeds health and defences (HPS-001, HPS-002). Safe to call again on respawn or when a
        /// pooled instance is reused: it clears death, i-frames and the corpse timer.
        /// </summary>
        public void Initialize(float maxHealth, float defense, float damageReduction)
        {
            MaxHealth = Mathf.Max(maxHealth, 0f);
            CurrentHealth = MaxHealth;
            Defense = defense;
            DamageReduction = damageReduction;

            IsDead = false;
            _invulnerableRemaining = 0f;
            _corpseRemaining = -1f;
            _seeded = true;

            if (_bodyCollider != null) _bodyCollider.enabled = true;

            PublishHealth();
        }

        /// <summary>
        /// Updates the defences after an upgrade changes the build (EXP-008, HPS-009).
        /// Health itself is untouched, so a mid-run stat change cannot heal or kill.
        /// </summary>
        public void SetDefenses(float defense, float damageReduction)
        {
            Defense = defense;
            DamageReduction = damageReduction;
        }

        /// <summary>
        /// Opens an invulnerability window (HPS-005). A longer window never shortens a running
        /// one, so a dash cannot accidentally cut post-hit i-frames short.
        /// </summary>
        public void BeginInvulnerability(float durationSeconds)
        {
            if (durationSeconds <= 0f) return;
            _invulnerableRemaining = Mathf.Max(_invulnerableRemaining, durationSeconds);
        }

        /// <summary>Ends the invulnerability window early.</summary>
        public void ClearInvulnerability() => _invulnerableRemaining = 0f;

        /// <inheritdoc />
        /// <remarks>
        /// Call this only from <see cref="CombatSystem"/> (HPS-003). It applies an
        /// already-calculated result and deliberately runs no formula of its own, so the SRS 9
        /// order of operations exists in exactly one place.
        /// </remarks>
        public void ApplyDamage(in DamageResult result)
        {
            // HPS-004: a corpse takes no further damage. Without this the same swing landing on
            // the frame of death would fire Died twice and grant rewards twice (SRS 34).
            if (IsDead) return;

            // HPS-005: i-frames from a dash or a previous hit.
            if (IsInvulnerable) return;

            if (!_seeded)
            {
                GameLog.Error("Health",
                    $"{name} took damage before Initialize; MaxHealth is 0 so it would die instantly (SRS 30).");
                return;
            }

            CurrentHealth = Mathf.Max(CurrentHealth - result.FinalDamage, 0f);
            PublishHealth();

            if (CurrentHealth <= 0f)
            {
                EnterDeathState(result.Source);
                return;
            }

            // HPS-005: the post-hit window opens automatically, using the same mechanism a dash
            // will use in slice 4, so there is only ever one notion of invulnerability. Opened
            // only on a surviving hit: a corpse has nothing to be invulnerable for.
            BeginInvulnerability(_hurtIFrameDuration);

            Damaged?.Invoke(this);
        }

        /// <summary>
        /// The single death transition (HPS-006, HPS-007). Guarded so <see cref="Died"/> is raised
        /// exactly once however many hits land on the same frame, which is what SRS 34 requires of
        /// XP and reward grants.
        /// </summary>
        private void EnterDeathState(DamageSource killingBlow)
        {
            if (IsDead) return;

            IsDead = true;
            CurrentHealth = 0f;

            // A corpse must stop blocking movement and stop absorbing hits meant for the living.
            if (_bodyCollider != null) _bodyCollider.enabled = false;

            _corpseRemaining = _sourceData != null ? _sourceData.CorpseLingerSeconds : 0f;

            _eventBus?.Publish(new EntityDiedEvent(EntityId, _isPlayer, transform.position, killingBlow));
            Died?.Invoke(this);
        }

        private void PublishHealth()
        {
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
            _eventBus?.Publish(new HealthChangedEvent(EntityId, CurrentHealth, MaxHealth, _isPlayer));
        }
    }
}
