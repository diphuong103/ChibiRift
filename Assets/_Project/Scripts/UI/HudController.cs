using UnityEngine;
using UnityEngine.UI;
using ChibiRift.Core;

namespace ChibiRift.UI
{
    /// <summary>
    /// In-run HUD (SRS 19.2): HP bar, XP bar and level, Q/E/R cooldowns, dash cooldown,
    /// buff icons, and the boss and elite HP bars.
    /// </summary>
    /// <remarks>
    /// Reads nothing from gameplay. Everything arrives through <see cref="EventBus"/>
    /// subscriptions, which is what SRS 26 means by "UI subscribe state/event". The assembly
    /// graph enforces it: ChibiRift.UI cannot reference ChibiRift.Gameplay at all.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class HudController : MonoBehaviour
    {
        [Header("Vitals (SRS 19.2)")]
        [SerializeField] private Slider _healthBar;
        [SerializeField] private Slider _experienceBar;
        [SerializeField] private Text _levelLabel;

        [Header("Cooldowns (SRS 19.2, MOV-006, COM-008)")]
        [Tooltip("Fill images for Q, E and R, in that order.")]
        [SerializeField] private Image[] _skillCooldownFills;

        [Tooltip("Dash cooldown indicator, required by MOV-006.")]
        [SerializeField] private Image _dashCooldownFill;

        [Header("Boss and elite (SRS 19.2, ELT-003)")]
        [SerializeField] private Slider _bossHealthBar;
        [SerializeField] private Slider _eliteHealthBar;

        [Header("Currency")]
        [SerializeField] private Text _goldLabel;
        [SerializeField] private Text _gemLabel;

        private EventBus _eventBus;

        private void OnEnable()
        {
            if (ServiceLocator.Current == null) return;
            _eventBus = ServiceLocator.Current.Get<EventBus>();

            // TODO(SRS-19.2): subscribe to HealthChangedEvent, ExperienceChangedEvent,
            //   LevelUpEvent, SkillCooldownChangedEvent, DashCooldownChangedEvent,
            //   CurrencyChangedEvent, BossActivatedEvent, BossPhaseChangedEvent,
            //   EliteActivatedEvent and DamageAppliedEvent.
        }

        private void OnDisable()
        {
            // TODO(SRS-19.2): unsubscribe from every event above so a scene change leaves no
            // dangling handler on the persistent EventBus.
        }

        /// <summary>Floating damage number over the target (HPS-008).</summary>
        private void SpawnDamageNumber(in DamageAppliedEvent evt)
        {
            // TODO(HPS-008): take a pooled damage label, scale and animate it, and mark crits.
            // TODO(SRS-21): keep it clear of hitboxes, telegraphs and critical UI.
        }
    }
}
