using UnityEngine;
using UnityEngine.InputSystem;
using ChibiRift.Core;

namespace ChibiRift.UI
{
    /// <summary>
    /// Development readout for the motor state, toggled with F1 (P1 slice 1).
    /// </summary>
    /// <remarks>
    /// Read-only by construction: it subscribes to <see cref="PlayerMotorStateEvent"/> on the
    /// <see cref="EventBus"/> and never touches gameplay. It could not do otherwise even by
    /// mistake, because ChibiRift.UI does not reference ChibiRift.Gameplay at all (SRS 26).
    /// Rendered with IMGUI so it needs no Canvas, no prefab and no art, and so it can be deleted
    /// in one file once a real HUD exists.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class DebugOverlay : MonoBehaviour
    {
        private const float PanelWidth = 230f;
        private const float PanelHeight = 130f;
        private const float PanelMargin = 10f;

        [Header("Visibility")]
        [Tooltip("Whether the overlay starts visible. F1 toggles it at runtime.")]
        [SerializeField] private bool _visibleOnStart = true;

        private EventBus _eventBus;
        private PlayerMotorStateEvent _state;
        private bool _visible;
        private GUIStyle _style;

        private void Awake() => _visible = _visibleOnStart;

        private void OnEnable()
        {
            if (ServiceLocator.Current == null) return;
            if (!ServiceLocator.Current.TryGet(out _eventBus)) return;

            _eventBus.Subscribe<PlayerMotorStateEvent>(OnMotorState);
        }

        private void OnDisable()
        {
            // Unsubscribing matters: the EventBus outlives the scene.
            _eventBus?.Unsubscribe<PlayerMotorStateEvent>(OnMotorState);
            _eventBus = null;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame) _visible = !_visible;
        }

        private void OnMotorState(PlayerMotorStateEvent state) => _state = state;

        private void OnGUI()
        {
            if (!_visible) return;

            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 12, richText = false };

            var area = new Rect(
                PanelMargin,
                Screen.height - PanelHeight - PanelMargin,
                PanelWidth,
                PanelHeight);

            GUI.Box(area, "Motor (F1)");
            GUILayout.BeginArea(new Rect(area.x + PanelMargin, area.y + PanelMargin * 2f, PanelWidth, PanelHeight));

            GUILayout.Label($"velocity.x   {_state.Velocity.x,8:F3}", _style);
            GUILayout.Label($"velocity.y   {_state.Velocity.y,8:F3}", _style);
            GUILayout.Label($"isGrounded   {_state.IsGrounded,8}", _style);
            GUILayout.Label($"jumpCount    {_state.JumpCount,8}", _style);
            GUILayout.Label($"coyoteTimer  {_state.CoyoteTimer,8:F3}", _style);
            GUILayout.Label($"jumpBuffer   {_state.JumpBufferTimer,8:F3}", _style);

            GUILayout.EndArea();
        }
    }
}
