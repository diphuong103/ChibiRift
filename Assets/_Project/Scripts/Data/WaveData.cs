using System;
using UnityEngine;

namespace ChibiRift.Data
{
    /// <summary>
    /// One enemy group inside a wave: which enemy, how many, and whether it spawns as an elite.
    /// </summary>
    [Serializable]
    public struct WaveEntry
    {
        [Tooltip("Enemy archetype asset to spawn.")]
        public EnemyData Enemy;

        [Tooltip("How many of this enemy the wave spawns.")]
        [Min(0)]
        public int Count;

        [Tooltip("Spawn these as elites: base enemy plus modifier plus stat multipliers (ELT-001).")]
        public bool SpawnAsElite;

        [Tooltip("Seconds after wave start before this group begins spawning.")]
        [Min(0f)]
        public float StartDelay;
    }

    /// <summary>
    /// A single wave (WAV-001). Composition is data, never hard-coded (SRS 14, NFR-007).
    /// The spawn budget of WAV-003 caps concurrent enemies so NFR-001's 30 enemy ceiling holds.
    /// </summary>
    [CreateAssetMenu(fileName = "WAV_", menuName = "ChibiRift/Wave Data", order = 60)]
    public sealed class WaveData : GameDataAsset
    {
        [Header("Composition (WAV-001)")]
        [Tooltip("Enemy groups making up this wave.")]
        [SerializeField] private WaveEntry[] _entries = new WaveEntry[0];

        [Header("Clear condition (WAV-001, WAV-004)")]
        [Tooltip("How the wave is judged complete.")]
        [SerializeField] private WaveClearCondition _clearCondition = WaveClearCondition.AllEnemiesDefeated;

        [Tooltip("Seconds before the wave ends when the condition is DurationElapsed.")]
        [Min(0f)]
        [SerializeField] private float _duration = 0f;

        [Tooltip("Kills required when the condition is KillCountReached.")]
        [Min(0)]
        [SerializeField] private int _killTarget = 0;

        [Header("Spawn control (WAV-003, WAV-005)")]
        [Tooltip("Maximum enemies alive at once (WAV-003). NFR-001 measures performance at 30.")]
        [Min(1)]
        [SerializeField] private int _spawnBudget = 15;

        [Tooltip("Seconds between individual spawns.")]
        [Min(0f)]
        [SerializeField] private float _spawnInterval = 0.5f;

        [Tooltip("Seconds of breathing room before the next wave starts (WAV-005).")]
        [Min(0f)]
        [SerializeField] private float _transitionDelay = 2f;

        /// <summary>Enemy groups (WAV-001).</summary>
        public WaveEntry[] Entries => _entries;

        /// <summary>Clear condition (WAV-004).</summary>
        public WaveClearCondition ClearCondition => _clearCondition;

        /// <summary>Duration for time-based waves.</summary>
        public float Duration => _duration;

        /// <summary>Kill quota for quota-based waves.</summary>
        public int KillTarget => _killTarget;

        /// <summary>Concurrent enemy cap (WAV-003).</summary>
        public int SpawnBudget => _spawnBudget;

        /// <summary>Seconds between spawns.</summary>
        public float SpawnInterval => _spawnInterval;

        /// <summary>Gap before the following wave (WAV-005).</summary>
        public float TransitionDelay => _transitionDelay;

        /// <summary>Total enemies this wave will spawn. Used by the HUD and by wave tests.</summary>
        public int TotalEnemyCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _entries.Length; i++) total += _entries[i].Count;
                return total;
            }
        }
    }
}
