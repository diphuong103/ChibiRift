using UnityEngine;

namespace ChibiRift.Core
{
    /// <summary>
    /// Owns the three volume buses of SRS 19.4 and the cue categories of SRS 22
    /// (BGM, SFX Player, SFX Enemy, UI). Volumes are persisted by the settings save (SAVE-002).
    /// </summary>
    public sealed class AudioManager : IAudioService
    {
        private const string LogCategory = "Audio";

        private float _masterVolume = 1f;
        private float _musicVolume = 1f;
        private float _sfxVolume = 1f;

        /// <inheritdoc />
        public float MasterVolume
        {
            get => _masterVolume;
            set => _masterVolume = Mathf.Clamp01(value);
        }

        /// <inheritdoc />
        public float MusicVolume
        {
            get => _musicVolume;
            set => _musicVolume = Mathf.Clamp01(value);
        }

        /// <inheritdoc />
        public float SfxVolume
        {
            get => _sfxVolume;
            set => _sfxVolume = Mathf.Clamp01(value);
        }

        /// <inheritdoc />
        public void PlaySfx(string sfxId)
        {
            // TODO(SRS-22): resolve sfxId against the audio library asset and play it on a
            // pooled AudioSource at MasterVolume * SfxVolume. Cues: jump, attack1-3, skill,
            // hurt, death, enemy spawn/attack/hit/death, UI hover/click/confirm/error.
            GameLog.Info(LogCategory, $"PlaySfx not implemented yet: {sfxId}");
        }

        /// <inheritdoc />
        public void PlayMusic(string musicId)
        {
            // TODO(SRS-22): crossfade the BGM track for MainMenu / Hub / Run / Boss /
            // Victory / Defeat at MasterVolume * MusicVolume.
            GameLog.Info(LogCategory, $"PlayMusic not implemented yet: {musicId}");
        }
    }
}
