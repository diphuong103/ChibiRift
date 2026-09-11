using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// One pooled projectile: travels, hits the first enemy it overlaps, then retires (COM-007).
    /// </summary>
    /// <remarks>
    /// <see cref="IPoolable"/>, and the reset in <see cref="OnSpawnedFromPool"/> is the part that
    /// matters. A recycled instance that keeps any state from its previous life — remaining
    /// lifetime, velocity, the target it already hit — behaves differently from a fresh one, and
    /// that difference only shows up once the pool starts reusing, which is exactly when a scene is
    /// busiest. <c>Test_Pool_ProjectileFullyResetOnReuse</c> checks each field.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class Projectile : MonoBehaviour, IPoolable
    {
        /// <summary>True while the projectile is flying. False once spent or expired.</summary>
        public bool IsAlive { get; private set; }

        /// <summary>Seconds left before it retires on its own.</summary>
        public float LifetimeRemaining { get; private set; }

        /// <summary>Units per second along <see cref="Direction"/>.</summary>
        public float Speed { get; private set; }

        /// <summary>Normalised travel direction.</summary>
        public Vector2 Direction { get; private set; }

        /// <summary>Distance covered since launch, for tests.</summary>
        public float DistanceTravelled { get; private set; }

        /// <summary>Raised when it should go back to its pool: spent, expired, or cancelled.</summary>
        public event System.Action<Projectile> Finished;

        private CombatSystem _combat;
        private SkillData _data;
        private float _baseDamage;
        private float _damageMultiplier;
        private float _critChance;
        private Vector2 _origin;
        private ContactFilter2D _enemyFilter;
        private readonly Collider2D[] _overlapBuffer = new Collider2D[SingleTarget];

        /// <summary>A projectile stops on its first target, so one slot is all the sweep needs.</summary>
        private const int SingleTarget = 1;

        private void Awake()
        {
            _enemyFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = LayerMask.GetMask(GameLayers.Enemy),
                useTriggers = true
            };
        }

        private void Start()
        {
            if (ServiceLocator.Current != null) ServiceLocator.Current.TryGet(out _combat);
        }

        /// <summary>Launches it. Everything that varies per cast arrives here.</summary>
        public void Launch(in SkillCastContext context)
        {
            _data = context.Data;
            _baseDamage = context.BaseDamage;
            _damageMultiplier = context.DamageMultiplier;
            _critChance = context.CritChance;
            _origin = context.Origin;

            Direction = context.Direction.sqrMagnitude > Mathf.Epsilon
                ? context.Direction.normalized
                : Vector2.right;

            Speed = context.Data.ProjectileSpeed;
            LifetimeRemaining = context.Data.ProjectileLifetime;
            DistanceTravelled = 0f;
            IsAlive = true;

            transform.position = context.Origin;
        }

        private void FixedUpdate()
        {
            if (!IsAlive) return;

            float dt = Time.fixedDeltaTime;

            LifetimeRemaining -= dt;
            if (LifetimeRemaining <= 0f)
            {
                Retire();
                return;
            }

            float step = Speed * dt;
            transform.position = (Vector2)transform.position + Direction * step;
            DistanceTravelled += step;

            TrySweep();
        }

        private void TrySweep()
        {
            // Resolved lazily, not in Start. A prewarmed instance is created inactive, so its Start
            // never ran; and once activated, FixedUpdate can precede Start. Either way the first
            // shot of the game would silently pass through its target with no damage.
            if (_combat == null && ServiceLocator.Current != null)
                ServiceLocator.Current.TryGet(out _combat);

            if (_combat == null || _data == null) return;

            int count = Physics2D.OverlapCircle(
                transform.position, _data.ProjectileRadius, _enemyFilter, _overlapBuffer);

            if (count <= 0) return;

            var target = _overlapBuffer[0] != null
                ? _overlapBuffer[0].GetComponentInParent<HealthComponent>()
                : null;

            if (target == null || target.IsDead) return;

            _combat.DealDamage(
                target,
                _baseDamage,
                _damageMultiplier,
                _critChance,
                DamageSource.Skill,
                target.transform.position,
                _origin);

            Retire();
        }

        private void Retire()
        {
            if (!IsAlive) return;

            IsAlive = false;
            Finished?.Invoke(this);
        }

        /// <inheritdoc />
        public void OnSpawnedFromPool()
        {
            // Everything the previous flight could have left behind. Launch overwrites most of it,
            // but a projectile fetched and not launched must still be inert rather than carrying
            // the last one's velocity.
            IsAlive = false;
            LifetimeRemaining = 0f;
            Speed = 0f;
            Direction = Vector2.right;
            DistanceTravelled = 0f;
            _data = null;
            _baseDamage = 0f;
            _damageMultiplier = 0f;
            _critChance = 0f;
        }

        /// <inheritdoc />
        public void OnReturnedToPool()
        {
            IsAlive = false;
            LifetimeRemaining = 0f;
            Speed = 0f;
        }
    }
}
