using UnityEngine;

namespace ChibiRift.Data
{
    /// <summary>
    /// One stage: an ordered wave list ending in a boss (SRS 14, STG-001, STG-003).
    /// SRS 14 sketches Stage 1 as waves of Melee, Melee plus Ranged, mixed, Elite, then Boss.
    /// </summary>
    [CreateAssetMenu(fileName = "STG_", menuName = "ChibiRift/Stage Data", order = 70)]
    public sealed class StageData : GameDataAsset
    {
        [Header("Waves (STG-001)")]
        [Tooltip("Waves in play order.")]
        [SerializeField] private WaveData[] _waves = new WaveData[0];

        [Header("Boss (STG-003)")]
        [Tooltip("Boss fought after the final wave. Null means the stage ends on wave clear.")]
        [SerializeField] private BossData _boss;

        [Header("Scaling (SRS 35: Enemy HP/Damage scaling per stage)")]
        [Tooltip("Enemy max HP multiplier for this stage.")]
        [Min(0.1f)]
        [SerializeField] private float _enemyHealthMultiplier = 1f;

        [Tooltip("Enemy damage multiplier for this stage.")]
        [Min(0.1f)]
        [SerializeField] private float _enemyDamageMultiplier = 1f;

        [Header("Rewards (STG-002)")]
        [Tooltip("Gold granted for clearing the stage.")]
        [Min(0)]
        [SerializeField] private int _clearGoldReward = 50;

        [Tooltip("Gems granted for clearing the stage.")]
        [Min(0)]
        [SerializeField] private int _clearGemReward = 0;

        [Header("Arena")]
        [Tooltip("Scene holding this stage's arena. MVP uses the fixed Run_01 (SRS 43 Q4).")]
        [SerializeField] private string _sceneName = Core.SceneNames.Run01;

        /// <summary>Waves in order (STG-001).</summary>
        public WaveData[] Waves => _waves;

        /// <summary>Stage boss (STG-003).</summary>
        public BossData Boss => _boss;

        /// <summary>Enemy HP scaling (SRS 35).</summary>
        public float EnemyHealthMultiplier => _enemyHealthMultiplier;

        /// <summary>Enemy damage scaling (SRS 35).</summary>
        public float EnemyDamageMultiplier => _enemyDamageMultiplier;

        /// <summary>Gold for clearing (STG-002).</summary>
        public int ClearGoldReward => _clearGoldReward;

        /// <summary>Gems for clearing (STG-002).</summary>
        public int ClearGemReward => _clearGemReward;

        /// <summary>Arena scene name.</summary>
        public string SceneName => _sceneName;

        /// <summary>Wave count, used by the HUD progress readout.</summary>
        public int WaveCount => _waves.Length;
    }
}
