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

        [Header("Wave (WAV-001..005)")]
        [SerializeField] private Text _waveLabel;

        private EventBus _eventBus;

        private void OnEnable()
        {
            if (ServiceLocator.Current == null) return;
            if (!ServiceLocator.Current.TryGet(out _eventBus)) return;

            // P1 wires the four readouts needed to accept slice 4B by eye. The rest of SRS 19.2 —
            // XP, level, currency, boss and elite bars — lands with the systems that feed them, and
            // their fields are left on this class rather than split into a second HUD that would
            // have to be merged back in P3.
            _eventBus.Subscribe<HealthChangedEvent>(OnHealthChanged);
            _eventBus.Subscribe<SkillCooldownChangedEvent>(OnSkillCooldownChanged);
            _eventBus.Subscribe<DashCooldownChangedEvent>(OnDashCooldownChanged);
            _eventBus.Subscribe<WaveStateChangedEvent>(OnWaveStateChanged);

            // TODO(SRS-19.2): ExperienceChangedEvent, LevelUpEvent, CurrencyChangedEvent,
            //   BossActivatedEvent, BossPhaseChangedEvent and EliteActivatedEvent.
        }

        private void OnDisable()
        {
            // The bus outlives the scene, so a handler left behind would fire into a destroyed
            // component on the next Run.
            if (_eventBus == null) return;

            _eventBus.Unsubscribe<HealthChangedEvent>(OnHealthChanged);
            _eventBus.Unsubscribe<SkillCooldownChangedEvent>(OnSkillCooldownChanged);
            _eventBus.Unsubscribe<DashCooldownChangedEvent>(OnDashCooldownChanged);
            _eventBus.Unsubscribe<WaveStateChangedEvent>(OnWaveStateChanged);
            _eventBus = null;
        }

        /// <summary>Hero health bar (SRS 19.2). Enemy bars are their own component.</summary>
        private void OnHealthChanged(HealthChangedEvent evt)
        {
            // Filtered on IsPlayer: every enemy publishes on this same channel, and without the
            // check the hero's bar would show whichever enemy was hit most recently.
            if (!evt.IsPlayer || _healthBar == null) return;

            _healthBar.value = evt.MaxHealth > 0f
                ? Mathf.Clamp01(evt.CurrentHealth / evt.MaxHealth)
                : 0f;
        }

        /// <summary>Q / E / R cooldowns (SRS 19.2, COM-008).</summary>
        private void OnSkillCooldownChanged(SkillCooldownChangedEvent evt)
        {
            int index = (int)evt.Slot;
            if (_skillCooldownFills == null || index < 0 || index >= _skillCooldownFills.Length) return;

            Image fill = _skillCooldownFills[index];
            if (fill == null) return;

            // Fill shows what is left to wait, so a ready skill is empty and a fresh cast is full.
            fill.fillAmount = evt.TotalSeconds > 0f
                ? Mathf.Clamp01(evt.RemainingSeconds / evt.TotalSeconds)
                : 0f;
        }

        /// <summary>Dash cooldown, required by MOV-006 and SRS 19.2.</summary>
        private void OnDashCooldownChanged(DashCooldownChangedEvent evt)
        {
            if (_dashCooldownFill == null) return;

            _dashCooldownFill.fillAmount = evt.TotalSeconds > 0f
                ? Mathf.Clamp01(evt.RemainingSeconds / evt.TotalSeconds)
                : 0f;
        }

        /// <summary>"Wave N/M · K left" readout (WAV-001..005).</summary>
        private void OnWaveStateChanged(WaveStateChangedEvent evt)
        {
            if (_waveLabel == null) return;

            _waveLabel.text = $"Wave {evt.WaveIndex + 1}/{evt.WaveCount} · {evt.EnemiesRemaining} left";
        }

        /// <summary>Floating damage number over the target (HPS-008).</summary>
        private void SpawnDamageNumber(in DamageAppliedEvent evt)
        {
            // TODO(HPS-008): take a pooled damage label, scale and animate it, and mark crits.
            // TODO(SRS-21): keep it clear of hitboxes, telegraphs and critical UI.
        }
    }
}
