using UnityEngine;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>States every enemy implements (AI-001).</summary>
    public enum EnemyState
    {
        Idle = 0,
        Chase = 1,
        Attack = 2,
        Hurt = 3,
        Death = 4
    }

    /// <summary>
    /// Enemy state machine (AI-001 through AI-005). Behaviour differences between Melee, Ranged
    /// and Charger come from <see cref="EnemyData.Archetype"/>, not from separate hard-coded classes.
    /// </summary>
    [RequireComponent(typeof(EnemyController))]
    [DisallowMultipleComponent]
    public sealed class EnemyAI : MonoBehaviour
    {
        [Header("Throttling (SRS 29)")]
        [Tooltip("Seconds between AI evaluations. Keeps 30 concurrent enemies inside the NFR-001 budget.")]
        [Min(0f)]
        [SerializeField] private float _thinkInterval = 0.1f;

        /// <summary>Current state (AI-001).</summary>
        public EnemyState State { get; private set; } = EnemyState.Idle;

        /// <summary>Seconds between AI evaluations. Throttling keeps 30 enemies inside NFR-001.</summary>
        public float ThinkInterval => _thinkInterval;

        private void Update()
        {
            // TODO(SRS-29): only evaluate every _thinkInterval seconds, not every frame.
            // TODO(AI-002): switch to Chase when the hero is inside EnemyData.DetectionRange.
            // TODO(AI-004): attack only when the cooldown has expired and the range allows.
            // TODO(AI-003): refuse every transition once State is Death.
            // TODO(AI-005): cap re-path attempts so no pathfinding loop can spin forever.
        }
    }
}
