using UnityEngine;

namespace ChibiRift.Core
{
    /// <summary>
    /// Relays screen shake requests to the camera rig (CAM-003).
    /// </summary>
    /// <remarks>
    /// Deliberately thin. This class used to also declare hit stop and the impact and death
    /// effects, but it lives in ChibiRift.Core and every one of those needs tuning values from
    /// ChibiRift.Data, which Core cannot reference. They now live in ChibiRift.Gameplay:
    /// <c>HitStopService</c> decides freeze length and asks <see cref="IPauseService"/> (the single
    /// owner of <see cref="UnityEngine.Time.timeScale"/>) to apply it, and <c>ImpactParticles</c>
    /// owns the burst. What is left here is the one thing Core can honestly do: publish the event.
    ///
    /// <para>Hard rule from SRS 21: VFX must never obscure a hitbox, a telegraph or critical UI.</para>
    /// </remarks>
    public sealed class VfxManager : IGameService
    {
        private const string LogCategory = "VFX";

        private readonly EventBus _eventBus;

        public VfxManager(EventBus eventBus) => _eventBus = eventBus;

        /// <summary>Death burst for hero or enemy (SRS 21).</summary>
        public void PlayDeathEffect(Vector2 worldPosition)
        {
            // TODO(SRS-21): pooled death VFX, must not cover the remaining enemies. Will move to
            // ChibiRift.Gameplay alongside ImpactParticles when it is built.
            GameLog.Info(LogCategory, $"PlayDeathEffect not implemented yet at {worldPosition}.");
        }

        /// <summary>Publishes a shake request for the Cinemachine impulse source (CAM-003).</summary>
        public void RequestScreenShake(float amplitude, float duration)
            => _eventBus.Publish(new ScreenShakeRequestedEvent(amplitude, duration));
    }
}
