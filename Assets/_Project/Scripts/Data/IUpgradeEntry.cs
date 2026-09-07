namespace ChibiRift.Data
{
    /// <summary>
    /// The subset of upgrade data the roller needs. Declaring it as an interface lets
    /// <c>UpgradeRoller</c> be unit-tested against lightweight fakes rather than against
    /// ScriptableObject instances, satisfying NFR-008.
    /// </summary>
    public interface IUpgradeEntry
    {
        /// <summary>Unique id (UPG-001). Also the de-duplication key within one roll (EXP-006).</summary>
        string Id { get; }

        /// <summary>Family the upgrade belongs to (SRS 11.1).</summary>
        UpgradeCategory Category { get; }

        /// <summary>Rarity tier, shown on the card (UPG-002).</summary>
        Rarity Rarity { get; }

        /// <summary>Relative draw weight; higher is more likely (UPG-004, RNG-001).</summary>
        float Weight { get; }

        /// <summary>Whether repeated picks stack (UPG-005).</summary>
        bool IsStackable { get; }

        /// <summary>Stack ceiling. Once reached, the entry leaves the pool (UPG-010).</summary>
        int MaxStack { get; }

        /// <summary>
        /// Member of the Fallback Pool (UPG-009). Fallback entries are repeatable, ignore the
        /// stack cap, and are exempt from the anti-duplicate rule of EXP-006 (RNG-005).
        /// </summary>
        bool IsFallback { get; }
    }
}
