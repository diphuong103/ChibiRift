using UnityEngine;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Per-scene facts the hero needs but that do not belong in hero or balance data: how wide the
    /// arena is, how far down counts as falling out, and where to reappear (MOV-004).
    /// Injected by reference rather than found by tag, so a test scene can supply its own.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneContext : MonoBehaviour
    {
        [Header("Play area (MOV-004)")]
        [Tooltip("Half the arena width in units. The hero is clamped to [-value, +value].")]
        [Min(1f)]
        [SerializeField] private float _worldHalfWidth = 20f;

        [Tooltip("Falling below this Y counts as falling out of the level and triggers a respawn.")]
        [SerializeField] private float _fallLimitY = -10f;

        [Tooltip("Where the hero starts and respawns. Placeholder until checkpoints exist in P2.")]
        [SerializeField] private Transform _spawnPoint;

        /// <summary>Half the arena width in units (MOV-004).</summary>
        public float WorldHalfWidth => _worldHalfWidth;

        /// <summary>Y below which the hero is considered to have fallen out.</summary>
        public float FallLimitY => _fallLimitY;

        /// <summary>Respawn position, falling back to the origin when no spawn point is set.</summary>
        public Vector2 SpawnPosition => _spawnPoint != null ? (Vector2)_spawnPoint.position : Vector2.zero;
    }
}
