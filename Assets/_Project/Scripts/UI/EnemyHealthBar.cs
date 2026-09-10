using UnityEngine;
using UnityEngine.UI;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.UI
{
    /// <summary>
    /// A world-space health bar floating above one enemy (SRS 19.2).
    /// </summary>
    /// <remarks>
    /// <para><b>How it knows which enemy it belongs to, without referencing gameplay.</b>
    /// ChibiRift.UI cannot reference ChibiRift.Gameplay, so this component cannot hold a reference
    /// to the health it displays, and <c>HealthChangedEvent</c> carries an id but no position. The
    /// answer is that this component lives as a <b>child of the enemy</b>: it reads its parent's
    /// <c>GetInstanceID()</c>, which is exactly the id the health component publishes, and it
    /// follows the enemy for free because it is parented to it. No lookup, no registry, and no
    /// reference across the assembly boundary.</para>
    ///
    /// <para>Hidden at full health so an untouched crowd stays readable (SRS 5), shown on damage,
    /// hidden again after <see cref="BalanceConfig.EnemyHealthBarHideDelay"/> seconds without a hit,
    /// and hidden immediately on death.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class EnemyHealthBar : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("Supplies how long the bar lingers after the last hit.")]
        [SerializeField] private BalanceConfig _balanceConfig;

        [Header("View")]
        [Tooltip("World-space canvas holding the bar. Disabled to hide, rather than deactivating the object.")]
        [SerializeField] private Canvas _canvas;

        [Tooltip("Fill image whose fillAmount tracks the health fraction.")]
        [SerializeField] private Image _fill;

        /// <summary>True while the bar is on screen.</summary>
        public bool IsVisible { get; private set; }

        /// <summary>Health fraction last received, 0..1.</summary>
        public float Fraction { get; private set; } = 1f;

        private EventBus _eventBus;
        private int _ownerId;
        private float _hideTimer;

        private float HideDelay => _balanceConfig != null ? _balanceConfig.EnemyHealthBarHideDelay : 0f;

        private void Awake()
        {
            if (_canvas == null) _canvas = GetComponent<Canvas>();

            // The parent is the enemy; its instance id is what HealthChangedEvent carries.
            _ownerId = transform.parent != null
                ? transform.parent.gameObject.GetInstanceID()
                : gameObject.GetInstanceID();

            SetVisible(false);
        }

        private void OnEnable()
        {
            if (ServiceLocator.Current == null) return;
            if (!ServiceLocator.Current.TryGet(out _eventBus)) return;

            _eventBus.Subscribe<HealthChangedEvent>(OnHealthChanged);
            _eventBus.Subscribe<EntityDiedEvent>(OnEntityDied);
        }

        private void OnDisable()
        {
            // The bus outlives the scene; a handler left behind would fire into a destroyed object.
            _eventBus?.Unsubscribe<HealthChangedEvent>(OnHealthChanged);
            _eventBus?.Unsubscribe<EntityDiedEvent>(OnEntityDied);
            _eventBus = null;
        }

        private void Update()
        {
            if (!IsVisible || _hideTimer <= 0f) return;

            _hideTimer -= Time.deltaTime;
            if (_hideTimer <= 0f) SetVisible(false);
        }

        private void OnHealthChanged(HealthChangedEvent evt)
        {
            if (evt.EntityId != _ownerId) return;

            Fraction = evt.MaxHealth > 0f ? Mathf.Clamp01(evt.CurrentHealth / evt.MaxHealth) : 0f;

            if (_fill != null) _fill.fillAmount = Fraction;

            // Full health means either untouched or fully healed; either way there is nothing worth
            // showing, and a crowd of full bars is exactly the clutter SRS 5 warns about.
            if (Fraction >= 1f)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            _hideTimer = HideDelay;
        }

        private void OnEntityDied(EntityDiedEvent evt)
        {
            if (evt.EntityId != _ownerId) return;
            SetVisible(false);
        }

        /// <summary>
        /// Hides by disabling the canvas, never by deactivating the GameObject. Deactivating would
        /// stop <c>Update</c> and run <c>OnDisable</c>, dropping the very subscriptions that would
        /// have to bring the bar back, so the bar could be hidden exactly once and never again.
        /// </summary>
        private void SetVisible(bool visible)
        {
            IsVisible = visible;
            if (_canvas != null) _canvas.enabled = visible;
        }
    }
}
