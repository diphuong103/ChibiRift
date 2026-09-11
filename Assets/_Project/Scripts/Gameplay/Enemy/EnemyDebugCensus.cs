using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Counts live enemies and reports what the closest one is doing, for the F1 overlay.
    /// </summary>
    /// <remarks>
    /// Development only, and deliberately the dumbest possible implementation: it re-scans with
    /// <c>FindObjectsByType</c> on a slow timer. That is far too expensive for gameplay, which is
    /// why nothing in gameplay uses it — but it needs no registry, so it cannot itself be the
    /// reason a diagnosis comes out wrong. Delete this together with the overlay once the wave
    /// system owns the live enemy list (AI-006).
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class EnemyDebugCensus : MonoBehaviour
    {
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
    }
}
