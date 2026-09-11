using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Chooses how hard to shake the camera for each hit and publishes the request (CAM-003).
    /// </summary>
    /// <remarks>
    /// Same split as <see cref="HitStopService"/>: the weights live in <see cref="BalanceConfig"/>,
    /// so the decision is made in this assembly, and the camera applies it. The request travels as
    /// <c>ScreenShakeRequestedEvent</c>, which has existed since the foundation slice and until now
    /// had no subscriber at all — the shake was a wire connected at one end only.
    ///
    /// <para>Taking a hit shakes harder than landing one. It is the only shake the player did not
    /// ask for, and it has to cut through whatever they were concentrating on.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ScreenShakeService : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("Supplies the four shake weights (SRS 35).")]
        [SerializeField] private BalanceConfig _balanceConfig;

        [Tooltip("Combo step that counts as heavy. Steps below it use the light shake.")]
        [Min(1)]
        [SerializeField] private int _heavyComboStep = 3;

        private EventBus _eventBus;
        private PlayerCombat _heroCombat;

        private void Awake() => _heroCombat = GetComponent<PlayerCombat>();

        private void Start()
        {
            if (ServiceLocator.Current == null) return;
            if (!ServiceLocator.Current.TryGet(out _eventBus)) return;

            _eventBus.Subscribe<DamageAppliedEvent>(OnDamageApplied);
        }

        private void OnDisable() => _eventBus?.Unsubscribe<DamageAppliedEvent>(OnDamageApplied);

        private void OnDamageApplied(DamageAppliedEvent evt)
        {
            if (_balanceConfig == null || _eventBus == null) return;

            ShakeStep step = StepFor(evt);
            if (step.Amplitude <= 0f || step.Duration <= 0f) return;

            _eventBus.Publish(new ScreenShakeRequestedEvent(step.Amplitude, step.Duration));
        }

        private ShakeStep StepFor(in DamageAppliedEvent evt)
        {
            ShakeConfig config = _balanceConfig.Shake;

            if (evt.TargetIsPlayer) return config.HeroHurt;
            if (evt.Result.WasCritical) return config.Critical;

            bool heavy = _heroCombat != null && _heroCombat.ComboStep >= _heavyComboStep;
            return heavy ? config.Heavy : config.Light;
        }
    }
}
