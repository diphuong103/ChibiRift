using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Chooses how long to freeze the game for each hit and asks the pause service to do it
    /// (SRS 21 "Hit Stop").
    /// </summary>
    /// <remarks>
    /// <para><b>Deciding here, writing there.</b> The durations come from
    /// <see cref="BalanceConfig"/>, which ChibiRift.Core cannot reference, so the choice is made in
    /// this assembly. The freeze itself is applied by <see cref="IPauseService"/>, which is the one
    /// owner of <c>Time.timeScale</c>. A second writer would end its freeze by setting the scale
    /// back to 1 and un-pause a game the player had paused.</para>
    ///
    /// <para><b>Why hit stop carries the slice.</b> Three combo steps that differ only by a damage
    /// number feel identical. A freeze of 0.04s against 0.08s against 0.10s is the cheapest signal
    /// that separates them, because it lands on the player's hands rather than their reading.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class HitStopService : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("Supplies the four freeze durations (SRS 35).")]
        [SerializeField] private BalanceConfig _balanceConfig;

        [Tooltip("Combo step that counts as heavy. Steps below it use the light freeze.")]
        [Min(1)]
        [SerializeField] private int _heavyComboStep = 3;

        private EventBus _eventBus;
        private IPauseService _pause;
        private PlayerCombat _heroCombat;

        private void Start()
        {
            if (ServiceLocator.Current == null) return;

            ServiceLocator.Current.TryGet(out _eventBus);
            ServiceLocator.Current.TryGet(out _pause);

            _heroCombat = GetComponent<PlayerCombat>();

            _eventBus?.Subscribe<DamageAppliedEvent>(OnDamageApplied);
        }

        private void OnDisable()
        {
            // The bus outlives the scene; a handler left behind would freeze the next Run.
            _eventBus?.Unsubscribe<DamageAppliedEvent>(OnDamageApplied);
        }

        private void OnDamageApplied(DamageAppliedEvent evt)
        {
            if (_pause == null || _balanceConfig == null) return;

            _pause.RequestHitStop(DurationFor(evt));
        }

        /// <summary>
        /// Picks the freeze for one hit. Order matters: a lethal critical on the final combo step
        /// should read as a kill, not as a crit, so the lethal case is tested first.
        /// </summary>
        private float DurationFor(in DamageAppliedEvent evt)
        {
            HitStopConfig config = _balanceConfig.HitStop;

            // The hit that kills is the one worth the longest pause, whatever landed it.
            if (evt.Result.FinalDamage > 0f && IsLethal(evt)) return config.Kill;

            if (evt.Result.WasCritical) return config.Critical;

            // Steps 1 and 2 are light; the final step shares the 1.6x multiplier and the weight.
            bool heavy = _heroCombat != null && _heroCombat.ComboStep >= _heavyComboStep;
            return heavy ? config.Heavy : config.Light;
        }

        /// <summary>
        /// Whether this hit emptied the target. Read from the event rather than from the target,
        /// because the target may already be on its way out of the scene by now.
        /// </summary>
        private static bool IsLethal(in DamageAppliedEvent evt) => evt.KilledTarget;
    }
}
