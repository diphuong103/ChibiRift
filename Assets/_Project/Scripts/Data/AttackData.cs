using System;
using UnityEngine;

namespace ChibiRift.Data
{
    /// <summary>
    /// One step of a basic-attack chain (COM-002, COM-004).
    /// </summary>
    /// <remarks>
    /// The active window is expressed in <b>seconds from the start of the swing</b> rather than in
    /// animation frames. SRS 21 describes hitboxes toggled by Animation Events, but P1 has no
    /// animation clips at all, so there is nothing to hang an event on. Time-based frame data
    /// produces the same observable behaviour and lets the combo be tested without an Animator.
    /// See OI-18. TODO(COM-004): move the toggle to Animation Events once real clips exist in P2.
    /// </remarks>
    [Serializable]
    public struct AttackStep
    {
        [Tooltip("Seconds the whole swing takes, start to recovery end. Input is locked until it elapses.")]
        [Min(0.01f)]
        public float TotalDuration;

        [Tooltip("Seconds from swing start until the hitbox opens (COM-004).")]
        [Min(0f)]
        public float ActiveStartTime;

        [Tooltip("Seconds from swing start until the hitbox closes (COM-004). Must exceed ActiveStartTime.")]
        [Min(0f)]
        public float ActiveEndTime;

        [Tooltip("Damage scale for this step, applied to the attacker's Attack stat (COM-002).")]
        [Min(0f)]
        public float DamageMultiplier;

        /// <summary>Seconds the hitbox is open. Zero or less means the step can never connect.</summary>
        public float ActiveDuration => ActiveEndTime - ActiveStartTime;
    }

    /// <summary>
    /// The basic-attack chain of one hero: frame data per step plus the shared hitbox geometry
    /// (COM-001, COM-002, COM-004, COM-009).
    /// </summary>
    /// <remarks>
    /// Held as an asset rather than as fields on <see cref="HeroData"/> because a hero may later
    /// swap chains per weapon, and because it keeps every combat number in one place that
    /// <c>DataDefaultsConsistencyTests</c> can diff after a generator run.
    /// </remarks>
    [CreateAssetMenu(fileName = "ATK_", menuName = "ChibiRift/Attack Data", order = 15)]
    public sealed class AttackData : GameDataAsset
    {
        [Header("Chain (COM-002)")]
        [Tooltip("One entry per combo step, in order. COM-002 requires three.")]
        [SerializeField] private AttackStep[] _steps = BaselineSteps();

        [Header("Hitbox geometry (COM-004, COM-009)")]
        [Tooltip("Width of the OverlapBox swept during the active window.")]
        [Min(0.01f)]
        [SerializeField] private float _hitboxWidth = 1.2f;

        [Tooltip("Height of the OverlapBox swept during the active window.")]
        [Min(0.01f)]
        [SerializeField] private float _hitboxHeight = 1f;

        [Tooltip("Distance from the hero to the hitbox centre, along the aim vector (COM-009).")]
        [Min(0f)]
        [SerializeField] private float _hitboxOffsetDistance = 0.8f;

        [Header("Input buffering (P1-07)")]
        [Tooltip("Seconds an attack press is remembered. Its own field rather than the jump buffer, so retuning attack rhythm cannot silently change how jumps feel.")]
        [Min(0f)]
        [SerializeField] private float _attackBufferSeconds = 0.12f;

        [Header("Movement while attacking (COM-001)")]
        [Tooltip("Horizontal speed scale during a swing. Below 1 commits the attack without freezing the hero. Not in SRS 35; set by the project owner.")]
        [Range(0f, 1f)]
        [SerializeField] private float _moveSpeedMultiplierWhileAttacking = 0.3f;

        /// <summary>Chain steps in order (COM-002).</summary>
        public AttackStep[] Steps => _steps;

        /// <summary>Number of hits in the chain (COM-002 requires 3).</summary>
        public int StepCount => _steps != null ? _steps.Length : 0;

        /// <summary>Hitbox width in world units (COM-004).</summary>
        public float HitboxWidth => _hitboxWidth;

        /// <summary>Hitbox height in world units (COM-004).</summary>
        public float HitboxHeight => _hitboxHeight;

        /// <summary>Hitbox size as one vector, the shape <c>Physics2D.OverlapBox</c> wants.</summary>
        public Vector2 HitboxSize => new Vector2(_hitboxWidth, _hitboxHeight);

        /// <summary>Distance from hero to hitbox centre along the aim vector (COM-009).</summary>
        public float HitboxOffsetDistance => _hitboxOffsetDistance;

        /// <summary>Horizontal speed scale while a swing is running (COM-001).</summary>
        public float MoveSpeedMultiplierWhileAttacking => _moveSpeedMultiplierWhileAttacking;

        /// <summary>
        /// Seconds an attack press is remembered (P1-07). Needed because hit stop stops time: a
        /// press during the freeze would otherwise be dropped, which reads as the game ignoring it.
        /// </summary>
        public float AttackBufferSeconds => _attackBufferSeconds;

        /// <summary>The step at <paramref name="index"/>, or a harmless empty step when out of range.</summary>
        public AttackStep GetStep(int index)
            => _steps != null && index >= 0 && index < _steps.Length ? _steps[index] : default;

        /// <summary>The three-hit baseline chain confirmed by the project owner for P1 slice 2.</summary>
        public static AttackStep[] BaselineSteps() => new[]
        {
            new AttackStep { TotalDuration = 0.3f, ActiveStartTime = 0.1f, ActiveEndTime = 0.16f, DamageMultiplier = 1f },
            new AttackStep { TotalDuration = 0.3f, ActiveStartTime = 0.1f, ActiveEndTime = 0.16f, DamageMultiplier = 1.1f },
            new AttackStep { TotalDuration = 0.45f, ActiveStartTime = 0.15f, ActiveEndTime = 0.24f, DamageMultiplier = 1.6f }
        };

        /// <inheritdoc />
        protected override void OnValidate()
        {
            base.OnValidate();

            if (_steps == null) return;

            for (int i = 0; i < _steps.Length; i++)
            {
                AttackStep step = _steps[i];

                // A closed or inverted window means the swing can never land, which is silent at
                // runtime and very hard to spot in the inspector (SRS 34 asks for validation).
                if (step.ActiveEndTime <= step.ActiveStartTime)
                {
                    GameLogWarn($"Step {i} of '{name}' has ActiveEndTime <= ActiveStartTime, so it can never hit (COM-004).");
                }
                else if (step.ActiveEndTime > step.TotalDuration)
                {
                    GameLogWarn($"Step {i} of '{name}' stays active past TotalDuration, so the window is cut short (COM-004).");
                }
            }
        }

        private static void GameLogWarn(string message)
        {
#if UNITY_EDITOR
            Core.GameLog.Warn(LogCategory, message);
#endif
        }
    }
}
