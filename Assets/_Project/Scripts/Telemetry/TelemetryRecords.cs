using System;

namespace ChibiRift.Telemetry
{
    /// <summary>
    /// One completed Run (TEL-001): hero, duration, progress reached and why it ended.
    /// Serialized with <c>JsonUtility</c>, so fields are public and concrete.
    /// </summary>
    [Serializable]
    public sealed class RunTelemetryRecord
    {
        /// <summary>Hero id played.</summary>
        public string HeroId = string.Empty;

        /// <summary>Seconds the Run lasted.</summary>
        public float DurationSeconds;

        /// <summary>Stage index reached.</summary>
        public int StageIndex;

        /// <summary>Wave index reached.</summary>
        public int WaveIndex;

        /// <summary>Hero level at the end.</summary>
        public int Level;

        /// <summary>Completed, Failed or Abandoned (RUN-001).</summary>
        public string Outcome = string.Empty;

        /// <summary>RNG seed, so a logged Run can be replayed (RNG-003).</summary>
        public int Seed;
    }

    /// <summary>
    /// One Level Up (TEL-002): the three cards shown and the one taken.
    /// This is the record that makes the RNG balance risk in SRS 39 measurable.
    /// </summary>
    [Serializable]
    public sealed class LevelUpTelemetryRecord
    {
        /// <summary>Level reached.</summary>
        public int Level;

        /// <summary>Ids of the cards offered, in display order (EXP-005).</summary>
        public string[] OfferedUpgradeIds = new string[0];

        /// <summary>Id of the card the player took (EXP-007).</summary>
        public string ChosenUpgradeId = string.Empty;

        /// <summary>Whether any offered card came from the Fallback Pool (EXP-009, UPG-009).</summary>
        public bool UsedFallbackPool;
    }

    /// <summary>
    /// Damage grouped by source, plus what killed the hero (TEL-003).
    /// Written at Run end rather than per hit, so no frame pays for it (TEL-005).
    /// </summary>
    [Serializable]
    public sealed class DamageTelemetryRecord
    {
        /// <summary>Total damage dealt by basic attacks.</summary>
        public float DamageFromBasicAttack;

        /// <summary>Total damage dealt by skills.</summary>
        public float DamageFromSkill;

        /// <summary>Total damage dealt by passives.</summary>
        public float DamageFromPassive;

        /// <summary>Total damage the hero took.</summary>
        public float DamageTaken;

        /// <summary>Source of the killing blow, blank when the hero survived.</summary>
        public string KillingBlowSource = string.Empty;
    }
}
