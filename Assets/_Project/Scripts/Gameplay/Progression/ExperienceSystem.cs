using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Accumulates XP during a Run and fires Level Up (SRS 10).
    /// Thresholds come from <see cref="ExperienceCurve"/>, which is unit-tested independently
    /// as NFR-008 requires.
    /// </summary>
    public sealed class ExperienceSystem : IGameService
    {
        private readonly EventBus _eventBus;
        private readonly BalanceConfig _balance;
        private readonly IPauseService _pause;

        /// <summary>Current hero level this Run.</summary>
        public int Level { get; private set; } = ExperienceCurve.FirstLevel;

        /// <summary>XP banked toward the next level.</summary>
        public float CurrentExperience { get; private set; }

        public ExperienceSystem(EventBus eventBus, BalanceConfig balance, IPauseService pause)
        {
            _eventBus = eventBus;
            _balance = balance;
            _pause = pause;
        }

        /// <summary>Grants XP from a defeated enemy (EXP-001, EXP-002). Granted once only (SRS 34).</summary>
        public void GrantExperience(float amount)
        {
            // TODO(EXP-002): add amount, publish ExperienceChangedEvent for the XP bar.
            // TODO(EXP-003): while CurrentExperience >= ExperienceCurve.ExperienceRequired(Level,
            //   _balance), subtract it, raise Level and publish LevelUpEvent.
            // TODO(EXP-004): pause with PauseReason.LevelUpSelection before the panel opens.
        }

        /// <summary>XP still needed for the next level. Drives the XP bar fill (SRS 19.2).</summary>
        public float ExperienceForNextLevel()
            => ExperienceCurve.ExperienceRequired(Level, _balance);
    }
}
