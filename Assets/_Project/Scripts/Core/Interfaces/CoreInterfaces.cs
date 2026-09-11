using System;
using System.Collections;

namespace ChibiRift.Core
{
    /// <summary>Marker for anything registered in <see cref="ServiceLocator"/>.</summary>
    public interface IGameService
    {
    }

    /// <summary>
    /// Anything that can receive damage through the single combat pipeline (HPS-003).
    /// Implementations must ignore damage once dead (HPS-004).
    /// </summary>
    public interface IDamageable
    {
        /// <summary>Current hit points (HPS-001, HPS-002).</summary>
        float CurrentHealth { get; }

        /// <summary>Maximum hit points (HPS-001, HPS-002).</summary>
        float MaxHealth { get; }

        /// <summary>True once health reached zero and the death transition ran (HPS-006, HPS-007).</summary>
        bool IsDead { get; }

        /// <summary>True while invulnerable: post-hit i-frames or an active dash window (HPS-005).</summary>
        bool IsInvulnerable { get; }

        /// <summary>Flat armor subtracted before proportional reduction (HPS-009).</summary>
        float Defense { get; }

        /// <summary>Proportional reduction, clamped to the configured maximum (HPS-009).</summary>
        float DamageReduction { get; }

        /// <summary>Applies an already-calculated result. Callers must not bypass this (HPS-003).</summary>
        void ApplyDamage(in DamageResult result);
    }

    /// <summary>
    /// Anything that can be pushed by a hit (COM-005).
    /// </summary>
    /// <remarks>
    /// Implemented by the movement components, not by <see cref="IDamageable"/>, because being
    /// damaged and being movable are separate: a training dummy takes damage but is bolted down.
    /// The receiver subscribes to <c>DamageAppliedEvent</c> itself rather than being pushed by the
    /// combat pipeline, which keeps that pipeline to the single job of resolving damage.
    /// </remarks>
    public interface IKnockbackReceiver
    {
        /// <summary>True while a knockback is running and self-driven movement is suspended.</summary>
        bool IsKnockedBack { get; }

        /// <summary>
        /// Pushes the receiver along <paramref name="direction"/> at <paramref name="force"/>
        /// units per second for <paramref name="durationSeconds"/>, during which its own movement
        /// yields. A later knockback replaces a running one rather than stacking.
        /// </summary>
        void ApplyKnockback(UnityEngine.Vector2 direction, float force, float durationSeconds);
    }

    /// <summary>Pooled objects get lifecycle callbacks instead of Instantiate/Destroy (SRS 29).</summary>
    public interface IPoolable
    {
        /// <summary>Called as the object leaves the pool. Reset all per-use state here.</summary>
        void OnSpawnedFromPool();

        /// <summary>Called as the object returns to the pool.</summary>
        void OnReturnedToPool();
    }

    /// <summary>Async scene orchestration across the five scenes of SRS 27.</summary>
    public interface ISceneFlowService : IGameService
    {
        /// <summary>Name of the scene currently loaded as the active gameplay/menu scene.</summary>
        string CurrentSceneName { get; }

        /// <summary>True while a transition is in flight; requests during this are ignored.</summary>
        bool IsTransitioning { get; }

        /// <summary>Raised with the progress of the running load, 0..1. UI binds a loading bar to this.</summary>
        event Action<float> LoadProgressChanged;

        /// <summary>Raised once the target scene is active.</summary>
        event Action<string> SceneLoaded;

        /// <summary>Starts an async transition. Scene load budget is 5s (NFR-004).</summary>
        void LoadScene(string sceneName, Action onComplete = null);
    }

    /// <summary>Global pause, owned by one service so time scale has a single writer (PAU-001).</summary>
    public interface IPauseService : IGameService
    {
        /// <summary>True while game time is stopped.</summary>
        bool IsPaused { get; }

        /// <summary>Raised whenever the pause state flips. HUD and Pause Menu subscribe.</summary>
        event Action<bool> PauseStateChanged;

        /// <summary>Stops game time for <paramref name="reason"/> (pause menu, level-up panel).</summary>
        void Pause(PauseReason reason);

        /// <summary>Releases the pause held by <paramref name="reason"/>. Time resumes when no reason is left.</summary>
        void Resume(PauseReason reason);

        /// <summary>True while a hit stop is holding time at zero (SRS 21).</summary>
        bool IsHitStopped { get; }

        /// <summary>
        /// Freezes time for <paramref name="unscaledSeconds"/> as impact feedback (SRS 21).
        /// </summary>
        /// <remarks>
        /// Routed through the pause service rather than written directly, because
        /// <see cref="UnityEngine.Time.timeScale"/> has exactly one owner. A second writer would
        /// end its freeze by setting the scale back to 1 and silently un-pause a game the player
        /// had paused. Pause outranks hit stop: a request made while paused is dropped, and a pause
        /// during a freeze cancels it rather than queueing behind it.
        ///
        /// <para>Overlapping requests take the longer duration; they never add up, or a crowd
        /// landing hits together would freeze the game for a noticeable stretch.</para>
        /// </remarks>
        void RequestHitStop(float unscaledSeconds);
    }

    /// <summary>Why the game is paused. The level-up panel cannot be dismissed with ESC (PAU-005).</summary>
    public enum PauseReason
    {
        PauseMenu = 0,
        LevelUpSelection = 1,
        SceneTransition = 2
    }

    /// <summary>Persistence of MetaSave and settings (SAVE-001, SAVE-002).</summary>
    public interface ISaveService : IGameService
    {
        /// <summary>True when the save file loaded cleanly; false when the fallback default was used (SAVE-004).</summary>
        bool LoadedCleanly { get; }

        /// <summary>Reads the save file, falling back to backup then to defaults on corruption (SAVE-004).</summary>
        void Load();

        /// <summary>Writes the save atomically: temp file, then replace, keeping a backup (SAVE-003).</summary>
        void Save();

        /// <summary>Wipes progression back to defaults. Callers must confirm with the player first (SAVE-005).</summary>
        void ResetToDefaults();
    }

    /// <summary>Local-only telemetry. Never sends over the network (TEL-004).</summary>
    public interface ITelemetryService : IGameService
    {
        /// <summary>Player-facing toggle exposed in Settings (TEL-004).</summary>
        bool IsEnabled { get; set; }

        /// <summary>Buffers one record. Must not touch the disk on the calling frame (TEL-005).</summary>
        void Record(string eventType, string jsonPayload);

        /// <summary>Writes the buffer to disk as JSON Lines. Called at Run end (TEL-005).</summary>
        void Flush();
    }

    /// <summary>Audio buses of SRS 22, with the volume settings of SRS 19.4.</summary>
    public interface IAudioService : IGameService
    {
        /// <summary>Master bus volume, 0..1 (SRS 19.4).</summary>
        float MasterVolume { get; set; }

        /// <summary>Music bus volume, 0..1 (SRS 19.4).</summary>
        float MusicVolume { get; set; }

        /// <summary>SFX bus volume, 0..1 (SRS 19.4).</summary>
        float SfxVolume { get; set; }

        /// <summary>Plays a one-shot sound effect by id.</summary>
        void PlaySfx(string sfxId);

        /// <summary>
        /// Plays <paramref name="clip"/> once at <paramref name="volume"/>, scaled by the SFX and
        /// master buses. A null clip is silently ignored: no audio ships yet and a half-filled
        /// library must stay playable.
        /// </summary>
        void PlayOneShot(UnityEngine.AudioClip clip, float volume = 1f);

        /// <summary>Switches the background music track (SRS 22).</summary>
        void PlayMusic(string musicId);
    }

    /// <summary>
    /// Wraps the Input System actions so gameplay never reads devices directly.
    /// Bindings are fixed by SRS 8.3; W/S/F are deliberately unbound in MVP (SRS 43 Q2).
    /// </summary>
    public interface IInputService : IGameService
    {
        /// <summary>-1..1 from A/D (MOV-001).</summary>
        float MoveAxis { get; }

        /// <summary>True on the frame Space was pressed (MOV-002, MOV-003).</summary>
        bool JumpPressed { get; }

        /// <summary>
        /// True while Space is held. Releasing mid-rise cuts the jump short, so the motor needs
        /// the held state and not just the press edge (MOV-002).
        /// </summary>
        bool JumpHeld { get; }

        /// <summary>True on the frame Left Shift was pressed (MOV-006).</summary>
        bool DashPressed { get; }

        /// <summary>True on the frame Mouse Left was pressed (COM-001).</summary>
        bool AttackPressed { get; }

        /// <summary>True on the frame ESC was pressed (PAU-001).</summary>
        bool PausePressed { get; }

        /// <summary>Raw pointer position in screen space.</summary>
        UnityEngine.Vector2 AimScreenPosition { get; }

        /// <summary>Pointer position in world space; drives facing and aim (COM-009).</summary>
        UnityEngine.Vector2 AimWorldPosition { get; }

        /// <summary>Camera used to project <see cref="AimScreenPosition"/> into the world.</summary>
        UnityEngine.Camera AimCamera { get; set; }

        /// <summary>True on the frame the given skill key (Q/E/R) was pressed (COM-007).</summary>
        bool WasSkillPressed(SkillSlot slot);

        /// <summary>Enables or disables the gameplay action map, e.g. while paused.</summary>
        void SetGameplayInputEnabled(bool enabled);
    }

    /// <summary>A coroutine host that survives scene loads, for services that are not MonoBehaviours.</summary>
    public interface ICoroutineRunner : IGameService
    {
        /// <summary>Starts <paramref name="routine"/> on the persistent bootstrap object.</summary>
        UnityEngine.Coroutine Run(IEnumerator routine);

        /// <summary>Stops a coroutine previously started by <see cref="Run"/>.</summary>
        void Stop(UnityEngine.Coroutine coroutine);
    }
}
