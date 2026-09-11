using System.Collections.Generic;
using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Creates and recycles enemies (AI-006, SRS 29). The single owner of a pooled enemy's
    /// lifetime: nothing else activates or deactivates one.
    /// </summary>
    /// <remarks>
    /// <para>Instances are made once, at scene load. SRS 29 forbids the Instantiate and Destroy
    /// churn of spawning during a wave, and <c>Test_Pool_NoInstantiateAfterPrewarm</c> holds it by
    /// watching the pool's own creation count.</para>
    ///
    /// <para><b>Reuse is the risky part, not creation.</b> A recycled enemy that keeps anything
    /// from its previous life — health, state, velocity, sprite tint, i-frames, a disabled collider
    /// — behaves differently from a fresh one, and only once the pool starts reusing, which is when
    /// the scene is busiest. <c>Test_Pool_EnemyFullyResetOnReuse</c> checks each of those.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class EnemySpawner : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("Supplies the pool sizes, which are performance budgets (SRS 29, NFR-001).")]
        [SerializeField] private BalanceConfig _balanceConfig;

        [Tooltip("Archetype the pool is built from. P1 ships one.")]
        [SerializeField] private EnemyData _enemyData;

        [Header("Pooling (SRS 29)")]
        [Tooltip("Enemy prefab. Pooled, never instantiated during play.")]
        [SerializeField] private EnemyController _prefab;

        [Tooltip("Parent for pooled instances. Defaults to this transform.")]
        [SerializeField] private Transform _container;

        /// <summary>Enemies currently alive. Checked against the wave budget (WAV-003).</summary>
        public int ActiveEnemyCount => _pool != null ? _pool.ActiveCount : 0;

        /// <summary>Instances the pool has ever made. Must stop rising once prewarm covers demand.</summary>
        public int TotalCreated => _pool != null ? _pool.TotalCreated : 0;

        /// <summary>Instances made up front (SRS 29).</summary>
        public int PrewarmCount => _balanceConfig != null ? _balanceConfig.EnemyPoolPrewarm : 0;

        private ObjectPool<EnemyController> _pool;

        /// <summary>
        /// Enemies this spawner currently owns. A release is refused unless the instance is in
        /// here, which is what makes a double Despawn a no-op instead of a double hand-out.
        /// </summary>
        private readonly HashSet<int> _owned = new HashSet<int>();

        private void Awake()
        {
            if (_container == null) _container = transform;

            if (_prefab == null || _balanceConfig == null)
            {
                GameLog.Error("Spawner", $"{name} has no enemy prefab or balance asset (SRS 30).");
                return;
            }

            _pool = new ObjectPool<EnemyController>(
                _prefab, _container, _balanceConfig.EnemyPoolPrewarm, _balanceConfig.EnemyPoolMax);
        }

        private void OnDestroy() => _pool?.Dispose();

        /// <summary>Takes an enemy from the pool and places it at <paramref name="position"/> (AI-006).</summary>
        public EnemyController Spawn(Vector2 position) => Spawn(_enemyData, position);

        /// <summary>Takes an enemy of <paramref name="data"/> and places it (AI-006).</summary>
        public EnemyController Spawn(EnemyData data, Vector2 position)
        {
            if (_pool == null || data == null) return null;

            EnemyController enemy = _pool.Get(position, Quaternion.identity);
            if (enemy == null) return null;

            _owned.Add(enemy.GetInstanceID());

            enemy.Configure(data, false, 1f, 1f);
            enemy.SetSpawner(this);
            enemy.SetSpawnPosition(position);

            // TODO(ELT-003): slice P2 enables the aura, tint and scale on an elite, plus its bar.
            return enemy;
        }

        /// <summary>
        /// Returns a dead enemy to the pool (SRS 29). Calling it twice on the same instance is a
        /// no-op rather than an error: the corpse timer and an explicit clear can both reach here,
        /// and releasing twice would hand the same object to two callers.
        /// </summary>
        public bool Despawn(EnemyController enemy)
        {
            if (_pool == null || enemy == null) return false;
            if (!_owned.Remove(enemy.GetInstanceID())) return false;

            _pool.Release(enemy);
            return true;
        }

        /// <summary>Clears the field, e.g. when a Run is abandoned (RUN-008).</summary>
        public void DespawnAll()
        {
            if (_pool == null) return;

            _owned.Clear();
            _pool.ReleaseAll();
        }
    }
}
