using UnityEngine;

namespace ChibiRift.Core
{
    /// <summary>
    /// Spawns the juice effects of SRS 21 from pooled prefabs (SRS 29), and relays screen shake
    /// requests to the camera rig (CAM-003).
    /// Hard rule from SRS 21: VFX must never obscure a hitbox, a telegraph or critical UI.
    /// </summary>
    public sealed class VfxManager : IGameService
    {
        private const string LogCategory = "VFX";

        private readonly EventBus _eventBus;

        public VfxManager(EventBus eventBus) => _eventBus = eventBus;

        /// <summary>Impact particles at a hit location (SRS 21).</summary>
        public void PlayHitImpact(Vector2 worldPosition)
        {
            // TODO(SRS-21): take an impact VFX from ObjectPool and place it at worldPosition.
            GameLog.Info(LogCategory, $"PlayHitImpact not implemented yet at {worldPosition}.");
        }

        /// <summary>Death burst for hero or enemy (SRS 21).</summary>
        public void PlayDeathEffect(Vector2 worldPosition)
        {
            // TODO(SRS-21): pooled death VFX, must not cover the remaining enemies.
            GameLog.Info(LogCategory, $"PlayDeathEffect not implemented yet at {worldPosition}.");
        }

        /// <summary>
        /// Brief global freeze on a heavy hit (SRS 21 "Hit Stop"). Must be bounded so it cannot
        /// push a frame past the 100 ms ceiling of NFR-002.
        /// </summary>
        public void RequestHitStop(float durationSeconds)
        {
            // TODO(SRS-21): scale Time.timeScale to 0 for durationSeconds using unscaled time,
            // coordinating with PauseManager so the two never fight over timeScale.
            GameLog.Info(LogCategory, $"RequestHitStop not implemented yet: {durationSeconds}s.");
        }

        /// <summary>Publishes a shake request for the Cinemachine impulse source (CAM-003).</summary>
        public void RequestScreenShake(float amplitude, float duration)
            => _eventBus.Publish(new ScreenShakeRequestedEvent(amplitude, duration));
    }
}
