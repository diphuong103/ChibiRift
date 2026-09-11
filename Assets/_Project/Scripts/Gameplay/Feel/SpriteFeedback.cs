using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// The single owner of a sprite's colour and alpha: the white flash on being hit (SRS 21) and
    /// the pulse while invulnerable (HPS-005).
    /// </summary>
    /// <remarks>
    /// <para><b>Why one component owns both.</b> They collide by definition — being hit is what
    /// opens the i-frame window, so the flash and the pulse always fire together on the hero. Two
    /// components writing <c>SpriteRenderer.color</c> produce whichever value ran last that frame,
    /// which is the same failure that made the sprite flicker when <c>PlayerMotor</c> and
    /// <c>PlayerCombat</c> both wrote <c>flipX</c> in slice 2 (OI-20). Flash outranks pulse: it is
    /// shorter and it is the one that tells the player the hit landed.</para>
    ///
    /// <para><b>MaterialPropertyBlock, not a new material.</b> Assigning to
    /// <c>Renderer.material</c> clones the material on every access and leaks one per instance per
    /// flash. A property block changes the draw without touching the shared material, so a wave of
    /// enemies flashing costs nothing. <c>Test_Flash_DoesNotLeakMaterials</c> holds this.</para>
    /// </remarks>
    [RequireComponent(typeof(HealthComponent))]
    [DisallowMultipleComponent]
    public sealed class SpriteFeedback : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("Supplies the flash duration and colour, and the invulnerability pulse (SRS 35).")]
        [SerializeField] private BalanceConfig _balanceConfig;

        [Tooltip("Sprite to drive. Defaults to the first one on this object or its children.")]
        [SerializeField] private SpriteRenderer _spriteRenderer;

        /// <summary>True while the white hit flash is showing.</summary>
        public bool IsFlashing => _flashRemaining > 0f;

        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        private HealthComponent _health;
        private MaterialPropertyBlock _block;
        private Color _baseColor = Color.white;
        private float _flashRemaining;

        private ImpactConfig Impact => _balanceConfig != null ? _balanceConfig.Impact : default;

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();
            if (_spriteRenderer == null) _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (_spriteRenderer != null) _baseColor = _spriteRenderer.color;

            _block = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (_health != null) _health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (_health != null) _health.Damaged -= OnDamaged;
            _flashRemaining = 0f;
            Apply(_baseColor);
        }

        private void Update()
        {
            if (_spriteRenderer == null || _balanceConfig == null) return;

            // Unscaled: a flash whose whole duration is shorter than a hit stop would otherwise be
            // frozen solid and never be seen.
            if (_flashRemaining > 0f)
            {
                _flashRemaining -= Time.unscaledDeltaTime;
                if (_flashRemaining > 0f)
                {
                    Apply(Impact.FlashColor);
                    return;
                }
            }

            if (_health != null && _health.IsInvulnerable)
            {
                float wave = Mathf.PingPong(Time.time * _balanceConfig.HurtFlashesPerSecond, 1f);
                Color pulsed = _baseColor;
                pulsed.a = Mathf.Lerp(_balanceConfig.HurtFlashMinAlpha, _baseColor.a, wave);
                Apply(pulsed);
                return;
            }

            Apply(_baseColor);
        }

        private void OnDamaged(HealthComponent health) => _flashRemaining = Impact.FlashDuration;

        private void Apply(Color color)
        {
            _spriteRenderer.GetPropertyBlock(_block);
            _block.SetColor(ColorProperty, color);
            _spriteRenderer.SetPropertyBlock(_block);
        }
    }
}
