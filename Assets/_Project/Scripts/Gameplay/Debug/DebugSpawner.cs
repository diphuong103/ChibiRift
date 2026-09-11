using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// F2 spawns one enemy, F3 spawns ten, F4 clears the field. For stress testing by hand.
    /// </summary>
    /// <remarks>
    /// <para><b>This reads the keyboard directly, which nothing else in the project is allowed to
    /// do.</b> Gameplay takes input through <c>IInputService</c> and the bound action map, so
    /// rebinding is a data change and the control scheme is testable. Development keys have no
    /// business in a shipping control scheme — a released build would carry bindings for keys that
    /// do nothing — so this is the exception, and it is bounded by the guard below: the code cannot
    /// exist in a player build at all. <c>DeviceInputSourceTests</c> fails the build if any other
    /// file does the same thing, or if this guard is removed (OI-29).</para>
    ///
    /// <para>The guard is on the method bodies rather than the class, so the component still exists
    /// in a build and a scene reference to it does not break.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class DebugSpawner : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("Supplies the scatter radius for the spawn keys.")]
        [SerializeField] private BalanceConfig _balanceConfig;

        [Header("Wiring")]
        [Tooltip("Pool the enemies come from and go back to.")]
        [SerializeField] private EnemySpawner _spawner;

        [Tooltip("Spawns are scattered around this. Defaults to the hero.")]
        [SerializeField] private Transform _origin;

        /// <summary>Enemies one press of the ten-at-once key spawns.</summary>
        private const int BatchSize = 10;

        private void Awake()
        {
            if (_origin != null) return;

            var hero = GameObject.FindGameObjectWithTag("Player");
            if (hero != null) _origin = hero.transform;
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || _spawner == null) return;

            if (keyboard.f2Key.wasPressedThisFrame) SpawnMany(1);
            if (keyboard.f3Key.wasPressedThisFrame) SpawnMany(BatchSize);
            if (keyboard.f4Key.wasPressedThisFrame) _spawner.DespawnAll();
#endif
        }

        /// <summary>Spawns <paramref name="count"/> enemies scattered around the origin.</summary>
        public void SpawnMany(int count)
        {
            if (_spawner == null) return;

            float radius = _balanceConfig != null ? _balanceConfig.DebugSpawnRadius : 0f;
            Vector2 centre = _origin != null ? (Vector2)_origin.position : Vector2.zero;

            for (int i = 0; i < count; i++)
            {
                // Horizontal scatter only, and above the centre, so a spawn lands on the arena
                // floor rather than inside it or under the level.
                var offset = new Vector2(Random.Range(-radius, radius), 1f);
                _spawner.Spawn(centre + offset);
            }
        }
    }
}
