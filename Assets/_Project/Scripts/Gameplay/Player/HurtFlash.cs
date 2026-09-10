using UnityEngine;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Pulses the sprite's alpha while invulnerable (HPS-005), so the player can see that hits are
    /// not landing on them right now.
    /// </summary>
    /// <remarks>
    /// Reads <see cref="HealthComponent.IsInvulnerable"/> rather than tracking its own timer. The
    /// i-frame window has exactly one owner and this is a view of it; a second timer here would
    /// drift out of step with the real one and show the wrong thing at the edges.
    /// </remarks>
    [RequireComponent(typeof(HealthComponent))]
    [DisallowMultipleComponent]
    public sealed class HurtFlash : MonoBehaviour
    {
        [Tooltip("Supplies the flash rate and its dimmest alpha. Both are juice values and belong in the asset (SRS 35).")]
        [SerializeField] private BalanceConfig _balanceConfig;

        [Tooltip("Sprite to pulse. Defaults to the first one found on this object or its children.")]
        [SerializeField] private SpriteRenderer _spriteRenderer;

        private HealthComponent _health;
        private float _restoreAlpha = 1f;
        private bool _wasInvulnerable;

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();
            if (_spriteRenderer == null) _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (_spriteRenderer != null) _restoreAlpha = _spriteRenderer.color.a;
        }

        private void Update()
        {
            if (_spriteRenderer == null || _health == null) return;

            bool invulnerable = _health.IsInvulnerable;

            if (!invulnerable)
            {
                // Restore once on the trailing edge rather than every frame, so nothing else that
                // tints the sprite is fighting this component for the alpha channel.
                if (_wasInvulnerable) SetAlpha(_restoreAlpha);
                _wasInvulnerable = false;
                return;
            }

            if (!_wasInvulnerable)
            {
                _restoreAlpha = _spriteRenderer.color.a;
                _wasInvulnerable = true;
            }

            if (_balanceConfig == null) return;

            float wave = Mathf.PingPong(Time.time * _balanceConfig.HurtFlashesPerSecond, 1f);
            SetAlpha(Mathf.Lerp(_balanceConfig.HurtFlashMinAlpha, _restoreAlpha, wave));
        }

        private void SetAlpha(float alpha)
        {
            Color color = _spriteRenderer.color;
            color.a = alpha;
            _spriteRenderer.color = color;
        }
    }
}
