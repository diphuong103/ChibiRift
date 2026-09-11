using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Counts live enemies and reports what the closest one is doing, for the F1 overlay.
    /// </summary>
    /// <remarks>
    /// <para>Development only, and deliberately the dumbest possible implementation: it re-scans
    /// with <c>FindObjectsByType</c> on a slow timer. That is far too expensive for gameplay, which
    /// is why nothing in gameplay uses it — but it needs no registry, so it cannot itself be the
    /// reason a diagnosis comes out wrong. Delete this together with the overlay once the wave
    /// system owns the live enemy list (AI-006).</para>
    ///
    /// <para>Guarded like <c>DebugSpawner</c> (OI-29): this component's own doc comment already
    /// called it development-only, but nothing enforced that until a profiling pass found it
    /// scanning and allocating on every scene load, including a Release build nobody had toggled it
    /// in (OI-32). The scan itself is also gated out of the frame-time harness's own measurement —
    /// see <c>FrameTimeHarness</c> — because a diagnostic overlay skews the very number it exists to
    /// help read.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class EnemyDebugCensus : MonoBehaviour
    {
        /// <summary>Label this component's scan reports under for NFR-002 profiling.</summary>
        private const string AllocationLabel = "EnemyDebugCensus.Update";

        [Tooltip("Supplies the scan interval. The diagnostic samples at the AI's own decision rate, so what it reports is what the AI last decided.")]
        [SerializeField] private BalanceConfig _balanceConfig;

        /// <summary>
        /// Seconds between scans, taken from the AI think interval rather than a constant here.
        /// Sampling faster than the AI decides would only report the same answer repeatedly; this
        /// also keeps the project free of tuning literals outside ChibiRift.Data (SRS 35).
        /// </summary>
        private float ScanInterval => _balanceConfig != null ? _balanceConfig.EnemyThinkInterval : 0f;

        private EventBus _eventBus;
        private Transform _hero;
        private float _timer;

        private void Start()
        {
            if (ServiceLocator.Current != null) ServiceLocator.Current.TryGet(out _eventBus);

            var hero = GameObject.FindGameObjectWithTag("Player");
            if (hero != null) _hero = hero.transform;
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // NFR-002 profiling (OI-32). try/finally: the two early returns below (no bus yet,
            // still within the scan interval) are the common case every other frame and must still
            // close the sample.
            AllocationProfiler.BeginSample(AllocationLabel);
            try
            {
                if (_eventBus == null) return;

                _timer -= Time.deltaTime;
                if (_timer > 0f) return;
                _timer = ScanInterval;

                EnemyAI[] enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);

                int alive = 0;
                float nearestDistance = float.MaxValue;
                EnemyLifecycleState nearestState = EnemyLifecycleState.Idle;

                for (int i = 0; i < enemies.Length; i++)
                {
                    EnemyAI enemy = enemies[i];
                    if (enemy.State == EnemyLifecycleState.Death) continue;

                    alive++;

                    if (_hero == null) continue;

                    float distance = Vector2.Distance(enemy.transform.position, _hero.position);
                    if (distance >= nearestDistance) continue;

                    nearestDistance = distance;
                    nearestState = enemy.State;
                }

                _eventBus.Publish(new EnemyCensusEvent(
                    alive, nearestState, nearestDistance == float.MaxValue ? -1f : nearestDistance));
            }
            finally
            {
                AllocationProfiler.EndSample(AllocationLabel);
            }
#endif
        }
    }
}
