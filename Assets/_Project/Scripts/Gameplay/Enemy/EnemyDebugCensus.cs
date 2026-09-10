using UnityEngine;
using ChibiRift.Core;

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
        [Tooltip("Seconds between scans. Slow on purpose: this is a diagnostic, not a system.")]
        [Min(0.05f)]
        [SerializeField] private float _scanInterval = 0.25f;

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
            _timer = _scanInterval;

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
