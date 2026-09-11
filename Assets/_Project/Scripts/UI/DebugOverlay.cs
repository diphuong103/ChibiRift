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
        private const float PanelWidth = 250f;
        private const float PanelHeight = 220f;
        private const float PanelMargin = 10f;

        [Header("Visibility")]
        [Tooltip("Whether the overlay starts visible. F1 toggles it at runtime.")]
        [SerializeField] private bool _visibleOnStart = true;

        private EventBus _eventBus;
        private PlayerMotorStateEvent _state;
        private PlayerCombatStateEvent _combat;
        private EnemyCensusEvent _census;
        private bool _visible;
        private GUIStyle _style;

        private void Awake() => _visible = _visibleOnStart;

        private void OnEnable()
        {
            if (ServiceLocator.Current == null) return;
            if (!ServiceLocator.Current.TryGet(out _eventBus)) return;

            _eventBus.Subscribe<PlayerMotorStateEvent>(OnMotorState);
            _eventBus.Subscribe<PlayerCombatStateEvent>(OnCombatState);
            _eventBus.Subscribe<EnemyCensusEvent>(OnCensus);
        }

        private void OnDisable()
        {
            // Unsubscribing matters: the EventBus outlives the scene.
            _eventBus?.Unsubscribe<PlayerMotorStateEvent>(OnMotorState);
            _eventBus?.Unsubscribe<PlayerCombatStateEvent>(OnCombatState);
            _eventBus?.Unsubscribe<EnemyCensusEvent>(OnCensus);
            _eventBus = null;
        }

        private void Update()
        {
            // Guarded, not because the overlay is expensive, but because reading a device directly
            // is otherwise forbidden project-wide: input reaches gameplay through IInputService and
            // the bound action map, never through Keyboard.current. A development toggle is a
            // legitimate exception only while it cannot exist in a shipping build, which is what
            // the guard enforces and DeviceInputSourceTests checks (OI-29).
            //
            // The method body is guarded rather than the whole class, so the component still exists
            // in a player build and the scene reference to it does not break.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame) _visible = !_visible;
#endif
        }

        private void OnMotorState(PlayerMotorStateEvent state) => _state = state;

        private void OnCombatState(PlayerCombatStateEvent combat) => _combat = combat;

        private void OnCensus(EnemyCensusEvent census) => _census = census;

        /// <summary>Label this component's draw reports under for NFR-002 profiling.</summary>
        private const string AllocationLabel = "DebugOverlay.OnGUI";

        private void OnGUI()
        {
            // Gated for the same reason DebugSpawner's key reads are (OI-29): a development readout
            // has no business drawing, or costing anything, in a Release build. Unlike the F1 key
            // read this was NOT guarded before a profiling pass found it running from frame one —
            // _visibleOnStart defaults to true, so every session paid for this every frame whether
            // or not anyone was looking at it (OI-32).
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_visible) return;

            // NFR-002 profiling. try/finally: the line above is the path taken almost every frame
            // once F1 is off, and it still needs to close the sample.
            AllocationProfiler.BeginSample(AllocationLabel);
            try
            {
                _style ??= new GUIStyle(GUI.skin.label) { fontSize = 12, richText = false };

                var area = new Rect(
                    PanelMargin,
                    Screen.height - PanelHeight - PanelMargin,
                    PanelWidth,
                    PanelHeight);

                GUI.Box(area, "Debug (F1)");
                GUILayout.BeginArea(
                    new Rect(area.x + PanelMargin, area.y + PanelMargin * 2f, PanelWidth, PanelHeight));

                GUILayout.Label($"velocity.x   {_state.Velocity.x,8:F3}", _style);
                GUILayout.Label($"velocity.y   {_state.Velocity.y,8:F3}", _style);
                GUILayout.Label($"isGrounded   {_state.IsGrounded,8}", _style);
                GUILayout.Label($"jumpCount    {_state.JumpCount,8}", _style);
                GUILayout.Label($"coyoteTimer  {_state.CoyoteTimer,8:F3}", _style);
                GUILayout.Label($"jumpBuffer   {_state.JumpBufferTimer,8:F3}", _style);

                GUILayout.Space(6f);

                // Attack lands only within roughly 1.4u of a target and has no animation yet, so
                // without these lines a swing that simply missed is indistinguishable from broken
                // input.
                string attackState = _combat.IsAttacking
                    ? (_combat.IsHitboxActive ? "ACTIVE" : "swinging")
                    : "idle";

                GUILayout.Label($"attackState  {attackState,8}", _style);
                GUILayout.Label(
                    $"comboStep    {_combat.ComboStep,8}  (window {_combat.ComboWindowRemaining:F2})", _style);
                GUILayout.Label(
                    $"aimDir       {_combat.AimDirection.x,5:F2},{_combat.AimDirection.y,5:F2}", _style);
                GUILayout.Label($"enemyCount   {_census.AliveCount,8}", _style);
                GUILayout.Label(
                    _census.NearestDistance < 0f
                        ? "nearestEnemy      none"
                        : $"nearestEnemy {_census.NearestState,8}  ({_census.NearestDistance:F1}u)",
                    _style);

                GUILayout.EndArea();
            }
            finally
            {
                AllocationProfiler.EndSample(AllocationLabel);
            }
#endif
        }
    }
}
