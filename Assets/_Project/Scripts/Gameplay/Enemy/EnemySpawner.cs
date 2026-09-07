using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Instantiates and recycles enemies for the wave system (AI-006).
    /// Backed by <see cref="ObjectPool{T}"/> so a dense wave never triggers the Instantiate and
    /// Destroy churn SRS 29 warns about.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemySpawner : MonoBehaviour
    {
        [Header("Spawn points")]
        [Tooltip("Positions the wave system may spawn at.")]
        [SerializeField] private Transform[] _spawnPoints;

        [Tooltip("Instances created up front per archetype so the first wave does not hitch (NFR-002).")]
        [Min(0)]
        [SerializeField] private int _prewarmPerArchetype = 8;

        /// <summary>Enemies currently alive. Checked against the wave budget (WAV-003).</summary>
        public int ActiveEnemyCount { get; private set; }

        /// <summary>Instances pre-created per archetype so the first wave does not hitch (NFR-002).</summary>
        public int PrewarmPerArchetype => _prewarmPerArchetype;

        /// <summary>Takes an enemy from the pool and places it at a spawn point (AI-006).</summary>
        public EnemyController Spawn(EnemyData data, bool asElite, float healthMultiplier, float damageMultiplier)
        {
            // TODO(AI-006): fetch from the per-archetype ObjectPool, Configure it, place it.
            // TODO(ELT-003): enable the aura, tint and scale on an elite, plus its HP bar.
            // TODO(SRS-30): on failure, log and let WaveManager run its timeout fallback.
            return null;
        }

        /// <summary>Returns a dead enemy to its pool (SRS 29).</summary>
        public void Despawn(EnemyController enemy)
        {
            // TODO(AI-006): release back to the pool rather than destroying.
        }

        /// <summary>Clears the field, e.g. when a Run is abandoned (RUN-008).</summary>
        public void DespawnAll()
        {
            // TODO(SRS-29): ReleaseAll on every pool.
        }
    }
}
