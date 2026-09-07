namespace ChibiRift.Core
{
    /// <summary>Lifecycle of a single Run (RUN-001). Abandoned added in SRS v1.1 (RUN-008, PAU-004).</summary>
    public enum RunLifecycleState
    {
        None = 0,
        Started = 1,
        InProgress = 2,
        Completed = 3,
        Failed = 4,
        Abandoned = 5
    }

    /// <summary>The two MVP currencies (SRS 16). Neither is lost on death; both commit at Post-Run (RUN-006).</summary>
    public enum CurrencyType
    {
        Gold = 0,
        Gem = 1
    }

    /// <summary>Skill slots bound to Q/E/R (COM-007). Cooldown-only in MVP, no resource pool (COM-008).</summary>
    public enum SkillSlot
    {
        Skill1 = 0,
        Skill2 = 1,
        Ultimate = 2
    }

    /// <summary>Wave progression states driven by WaveManager (WAV-001..WAV-005).</summary>
    public enum WaveState
    {
        Pending = 0,
        Spawning = 1,
        Active = 2,
        Cleared = 3,
        Transitioning = 4
    }

    /// <summary>Stage progression states (STG-001..STG-003).</summary>
    public enum StageState
    {
        NotStarted = 0,
        RunningWaves = 1,
        BossFight = 2,
        Cleared = 3
    }
}
