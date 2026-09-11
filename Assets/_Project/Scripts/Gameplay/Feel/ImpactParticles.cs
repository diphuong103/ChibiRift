using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// A short burst of square particles at the point of impact (SRS 21).
    /// </summary>
    /// <remarks>
    /// One pooled <see cref="ParticleSystem"/> emitting at a moved position, rather than a pooled
    /// prefab per hit: a burst has no state to reset, so a single system is both simpler and
    /// cheaper than recycling objects. The particles fly along the hit direction so the burst reads
    /// as coming from the attacker rather than appearing on the spot.
    ///
    /// <para>SRS 21's hard rule applies: this must never cover a hitbox or a telegraph. The burst
    /// is small, brief and drawn behind nothing important.</para>
    /// </remarks>
    [RequireComponent(typeof(ParticleSystem))]
    [DisallowMultipleComponent]
    public sealed class ImpactParticles : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("Supplies how many particles one impact releases (SRS 35).")]
        [SerializeField] private BalanceConfig _balanceConfig;

        private EventBus _eventBus;
        private ParticleSystem _particles;

        private void Awake() => _particles = GetComponent<ParticleSystem>();

        private void Start()
        {
            if (ServiceLocator.Current == null) return;
            if (!ServiceLocator.Current.TryGet(out _eventBus)) return;

            _eventBus.Subscribe<DamageAppliedEvent>(OnDamageApplied);
        }

        private void OnDisable() => _eventBus?.Unsubscribe<DamageAppliedEvent>(OnDamageApplied);

        private void OnDamageApplied(DamageAppliedEvent evt)
        {
            if (_particles == null || _balanceConfig == null) return;

            int count = _balanceConfig.Impact.ParticleCount;
            if (count <= 0) return;

            transform.position = evt.WorldPosition;

            // Aim the burst away from the attacker. A hit with no direction (a test calling the
            // pipeline directly) still emits, just without a bias.
            Vector2 away = evt.WorldPosition - evt.AttackerPosition;
            if (away.sqrMagnitude > Mathf.Epsilon)
            {
                float angle = Mathf.Atan2(away.y, away.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
            }

            _particles.Emit(count);
        }
    }
}
