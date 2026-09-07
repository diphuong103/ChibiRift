using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChibiRift.Core
{
    /// <summary>
    /// The only writer of <see cref="Time.timeScale"/> in the project (PAU-001, EXP-004).
    /// Pause reasons stack, so the Level Up panel opening on top of a paused game cannot be
    /// dismissed early by the Pause Menu resuming (PAU-005).
    /// </summary>
    public sealed class PauseManager : IPauseService
    {
        private const float RunningTimeScale = 1f;
        private const float PausedTimeScale = 0f;

        private readonly HashSet<PauseReason> _reasons = new HashSet<PauseReason>();
        private readonly EventBus _eventBus;
        private readonly IInputService _input;

        /// <inheritdoc />
        public bool IsPaused => _reasons.Count > 0;

        /// <inheritdoc />
        public event Action<bool> PauseStateChanged;

        public PauseManager(EventBus eventBus, IInputService input)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _input = input ?? throw new ArgumentNullException(nameof(input));
        }

        /// <inheritdoc />
        public void Pause(PauseReason reason)
        {
            bool wasPaused = IsPaused;
            if (!_reasons.Add(reason)) return;
            if (wasPaused) return;

            Time.timeScale = PausedTimeScale;
            _input.SetGameplayInputEnabled(false);
            Notify(true, reason);
        }

        /// <inheritdoc />
        public void Resume(PauseReason reason)
        {
            if (!_reasons.Remove(reason)) return;
            if (IsPaused) return;

            Time.timeScale = RunningTimeScale;
            _input.SetGameplayInputEnabled(true);
            Notify(false, reason);
        }

        /// <summary>Clears every pause reason. Used when a Run tears down or a scene changes.</summary>
        public void ForceResume()
        {
            if (_reasons.Count == 0) return;

            _reasons.Clear();
            Time.timeScale = RunningTimeScale;
            _input.SetGameplayInputEnabled(true);
            Notify(false, PauseReason.SceneTransition);
        }

        private void Notify(bool isPaused, PauseReason reason)
        {
            PauseStateChanged?.Invoke(isPaused);
            _eventBus.Publish(new PauseStateChangedEvent(isPaused, reason));
        }
    }
}
