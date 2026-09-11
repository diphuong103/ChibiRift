using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ChibiRift.Core
{
    /// <summary>
    /// The only writer of <see cref="Time.timeScale"/> in the project (PAU-001, EXP-004).
    /// Pause reasons stack, so the Level Up panel opening on top of a paused game cannot be
    /// dismissed early by the Pause Menu resuming (PAU-005).
    /// </summary>
    /// <remarks>
    /// Hit stop (SRS 21) also freezes time, and also lives here for the same reason. Two writers
    /// would collide in a way that is easy to miss and hard to reproduce: a hit stop finishing
    /// during a pause would set the scale back to 1 and un-pause the game underneath the player.
    /// The rule is that pause outranks hit stop in both directions — a request made while paused is
    /// dropped, and a pause taken during a freeze cancels it outright.
    /// </remarks>
    public sealed class PauseManager : IPauseService
    {
        private const float RunningTimeScale = 1f;
        private const float PausedTimeScale = 0f;

        private readonly HashSet<PauseReason> _reasons = new HashSet<PauseReason>();
        private readonly EventBus _eventBus;
        private readonly IInputService _input;
        private readonly ICoroutineRunner _coroutines;

        private Coroutine _hitStopRoutine;
        private float _hitStopRemaining;

        /// <inheritdoc />
        public bool IsPaused => _reasons.Count > 0;

        /// <inheritdoc />
        public bool IsHitStopped => _hitStopRoutine != null;

        /// <inheritdoc />
        public event Action<bool> PauseStateChanged;

        public PauseManager(EventBus eventBus, IInputService input, ICoroutineRunner coroutines)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _coroutines = coroutines ?? throw new ArgumentNullException(nameof(coroutines));
        }

        /// <inheritdoc />
        public void Pause(PauseReason reason)
        {
            bool wasPaused = IsPaused;
            if (!_reasons.Add(reason)) return;
            if (wasPaused) return;

            // A running hit stop is abandoned rather than resumed afterwards. It is feedback for a
            // hit that happened before the player paused; replaying it on resume would freeze the
            // game for no visible reason.
            CancelHitStop();

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
            CancelHitStop();
            Time.timeScale = RunningTimeScale;
            _input.SetGameplayInputEnabled(true);
            Notify(false, PauseReason.SceneTransition);
        }

        /// <inheritdoc />
        public void RequestHitStop(float unscaledSeconds)
        {
            if (unscaledSeconds <= 0f) return;

            // Pause outranks hit stop. Without this the freeze would expire under a paused game and
            // hand the scale back to 1.
            if (IsPaused) return;

            // Longer wins, never the sum: several enemies struck on one frame would otherwise add
            // their freezes together into a stall the player reads as a hitch.
            _hitStopRemaining = Mathf.Max(_hitStopRemaining, unscaledSeconds);

            if (_hitStopRoutine != null) return;
            _hitStopRoutine = _coroutines.Run(RunHitStop());
        }

        private IEnumerator RunHitStop()
        {
            Time.timeScale = PausedTimeScale;

            while (_hitStopRemaining > 0f)
            {
                yield return null;

                // Pause took over mid-freeze. Leave the scale where pause wants it.
                if (IsPaused)
                {
                    _hitStopRemaining = 0f;
                    _hitStopRoutine = null;
                    yield break;
                }

                _hitStopRemaining -= Time.unscaledDeltaTime;
            }

            _hitStopRemaining = 0f;
            _hitStopRoutine = null;
            Time.timeScale = RunningTimeScale;
        }

        private void CancelHitStop()
        {
            if (_hitStopRoutine == null) return;

            _coroutines.Stop(_hitStopRoutine);
            _hitStopRoutine = null;
            _hitStopRemaining = 0f;
        }

        private void Notify(bool isPaused, PauseReason reason)
        {
            PauseStateChanged?.Invoke(isPaused);
            _eventBus.Publish(new PauseStateChangedEvent(isPaused, reason));
        }
    }
}
