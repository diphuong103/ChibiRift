using System.Collections.Generic;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Everything that lives for exactly one Run (SRS 23).
    /// Explicitly runtime-only: MVP has no mid-run save, so this is never written to disk
    /// (SAVE-006, SAVE-007). Currency accumulates here and commits to the wallet once at
    /// Post-Run, whatever the outcome (RUN-006, SRS 16).
    /// </summary>
    public sealed class RunState
    {
        private readonly Dictionary<string, int> _upgradeStacks = new Dictionary<string, int>();

        /// <summary>Hero chosen in the Hub (HUB-003).</summary>
        public HeroData Hero { get; set; }

        /// <summary>Lifecycle position (RUN-001).</summary>
        public RunLifecycleState State { get; set; } = RunLifecycleState.None;

        /// <summary>Seed for every RNG draw this Run, letting a run be replayed (RNG-003).</summary>
        public int Seed { get; set; }

        /// <summary>Current level (EXP-003).</summary>
        public int Level { get; set; } = ExperienceCurve.FirstLevel;

        /// <summary>XP banked toward the next level.</summary>
        public float Experience { get; set; }

        /// <summary>Stage index reached, for the Post-Run summary (RUN-004).</summary>
        public int StageIndex { get; set; }

        /// <summary>Wave index reached, for the Post-Run summary (RUN-004).</summary>
        public int WaveIndex { get; set; }

        /// <summary>Seconds elapsed, logged per Run (TEL-001).</summary>
        public float ElapsedSeconds { get; set; }

        /// <summary>Gold collected this Run, committed at Post-Run (RUN-006).</summary>
        public int GoldCollected { get; set; }

        /// <summary>Gems collected this Run, committed at Post-Run (RUN-006).</summary>
        public int GemsCollected { get; set; }

        /// <summary>Guard making the reward commit happen exactly once (RUN-005, SRS 34).</summary>
        public bool RewardCommitted { get; set; }

        /// <summary>Stacks owned per upgrade id. This is the build (UPG-006).</summary>
        public IReadOnlyDictionary<string, int> UpgradeStacks => _upgradeStacks;

        /// <summary>Stacks owned of <paramref name="upgradeId"/>. Feeds the UPG-010 pool filter.</summary>
        public int GetUpgradeStack(string upgradeId)
            => _upgradeStacks.TryGetValue(upgradeId, out int count) ? count : 0;

        /// <summary>Records one more stack of an upgrade after the player picks it (UPG-006).</summary>
        public void AddUpgradeStack(string upgradeId)
        {
            if (string.IsNullOrWhiteSpace(upgradeId)) return;
            _upgradeStacks[upgradeId] = GetUpgradeStack(upgradeId) + 1;
        }

        /// <summary>Clears the state for a fresh Run (RUN-001).</summary>
        public void Reset()
        {
            _upgradeStacks.Clear();
            State = RunLifecycleState.None;
            Level = ExperienceCurve.FirstLevel;
            Experience = 0f;
            StageIndex = 0;
            WaveIndex = 0;
            ElapsedSeconds = 0f;
            GoldCollected = 0;
            GemsCollected = 0;
            RewardCommitted = false;
        }
    }
}
