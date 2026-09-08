using UnityEngine;
using UnityEngine.UI;
using ChibiRift.Core;

namespace ChibiRift.UI
{
    /// <summary>
    /// One floating damage number (HPS-008). Rises and fades, then reports itself finished so the
    /// pool can take it back.
    /// </summary>
    /// <remarks>
    /// Pooled rather than instantiated (SRS 29): a busy wave produces one of these per hit, and
    /// NFR-002 leaves no room for per-hit allocation and garbage collection.
    /// </remarks>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public sealed class DamageNumber : MonoBehaviour, IPoolable
    {
        [Header("Presentation")]
        [Tooltip("Label showing the amount. Colour is set per hit, so do not tint it in the prefab.")]
        [SerializeField] private Text _label;

        [Tooltip("Seconds the number stays on screen before it is recycled.")]
        [Min(0.01f)]
        [SerializeField] private float _lifetime = 0.7f;

        [Tooltip("Screen pixels the number rises over its lifetime.")]
        [SerializeField] private float _riseDistance = 48f;

        [Tooltip("Colour for an ordinary hit.")]
        [SerializeField] private Color _normalColor = Color.white;

        [Tooltip("Colour for a critical hit (COM-006). Unused until crits are wired in slice 4, but set now so the two never look alike.")]
        [SerializeField] private Color _criticalColor = new Color(1f, 0.78f, 0.2f, 1f);

        /// <summary>True once the number has finished and is waiting to be recycled.</summary>
        public bool IsFinished { get; private set; } = true;

        private RectTransform _rect;
        private Vector2 _origin;
        private float _elapsed;

        private void Awake() => _rect = GetComponent<RectTransform>();

        /// <summary>Starts one number at <paramref name="screenPosition"/> (HPS-008).</summary>
        public void Play(float amount, bool isCritical, Vector2 screenPosition)
        {
            if (_rect == null) _rect = GetComponent<RectTransform>();

            _origin = screenPosition;
            _rect.position = screenPosition;
            _elapsed = 0f;
            IsFinished = false;

            if (_label == null) return;

            // Rounded for readability: HP is a float (OI-07) but a fractional damage number
            // is noise on screen.
            _label.text = Mathf.RoundToInt(amount).ToString();
            _label.color = isCritical ? _criticalColor : _normalColor;
        }

        private void Update()
        {
            if (IsFinished) return;

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _lifetime);

            _rect.position = _origin + Vector2.up * (_riseDistance * t);

            if (_label != null)
            {
                Color color = _label.color;
                color.a = 1f - t;
                _label.color = color;
            }

            if (t >= 1f) IsFinished = true;
        }

        /// <inheritdoc />
        public void OnSpawnedFromPool()
        {
            IsFinished = false;
            _elapsed = 0f;
        }

        /// <inheritdoc />
        public void OnReturnedToPool()
        {
            IsFinished = true;
            _elapsed = 0f;
        }
    }
}
