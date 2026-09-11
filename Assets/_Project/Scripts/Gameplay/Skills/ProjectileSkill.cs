using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Fires a pooled projectile along the aim vector (COM-007, COM-009). Used by Q.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProjectileSkill : MonoBehaviour, ISkillBehaviour
    {
        [Header("Data")]
        [Tooltip("Supplies the pool sizes, which are performance budgets (SRS 29).")]
        [SerializeField] private BalanceConfig _balanceConfig;

        [Header("Pooling (SRS 29)")]
        [Tooltip("Projectile prefab. Pooled, never instantiated during play.")]
        [SerializeField] private Projectile _prefab;

        [Tooltip("Scene-level parent for pooled instances. Must NOT be the caster: a projectile parented to a moving hero is dragged along by them mid-flight.")]
        [SerializeField] private Transform _container;

        /// <summary>Instances the pool has ever created. Stops rising once prewarm covers demand.</summary>
        public int TotalCreated => _pool != null ? _pool.TotalCreated : 0;

        /// <summary>Projectiles in flight.</summary>
        public int ActiveCount => _pool != null ? _pool.ActiveCount : 0;

        /// <summary>The most recent projectile fired. For tests.</summary>
        public Projectile LastFired { get; private set; }

        private ObjectPool<Projectile> _pool;

        private void Awake()
        {
            // Falling back to this transform would parent every projectile to the hero, and a
            // projectile advances by reading its own world position each step — so the hero's own
            // movement would be added to the projectile's flight. A scene-level container keeps
            // the two independent.
            if (_container == null) _container = new GameObject("Projectiles").transform;

            if (_prefab == null || _balanceConfig == null)
            {
                GameLog.Error("Skills", $"{name} has no projectile prefab or balance asset (SRS 30).");
                return;
            }

            // Prewarmed here, at scene load, which is the whole point: SRS 29 forbids the
            // Instantiate churn mid-wave, and Test_Pool_NoInstantiateAfterPrewarm holds it.
            _pool = new ObjectPool<Projectile>(
                _prefab,
                _container,
                _balanceConfig.ProjectilePoolPrewarm,
                _balanceConfig.ProjectilePoolMax);
        }

        private void OnDestroy() => _pool?.Dispose();

        /// <inheritdoc />
        public void Cast(in SkillCastContext context)
        {
            if (_pool == null || context.Data == null) return;

            Projectile projectile = _pool.Get(context.Origin, Quaternion.identity);
            if (projectile == null) return;

            projectile.Finished += OnProjectileFinished;
            projectile.Launch(context);

            LastFired = projectile;
        }

        private void OnProjectileFinished(Projectile projectile)
        {
            // Unsubscribed before release: the same instance is handed out again, and a handler
            // left attached would fire once per previous life on its next retirement.
            projectile.Finished -= OnProjectileFinished;
            _pool.Release(projectile);
        }
    }
}
