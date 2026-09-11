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

        /// <summary>
        /// Voices available at once. Above the concurrent-enemy figure of NFR-001 so a busy wave
        /// never silences a cue, and fixed so playing a sound allocates nothing.
        /// </summary>
        private const int VoiceCount = 16;

        private AudioSource[] _voices;
        private GameObject _voiceHolder;
        private int _nextVoice;
        private bool _warnedAboutNullClip;

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
        /// <remarks>
        /// A null clip is normal, not an error: no audio ships in P1 and the wiring is meant to be
        /// complete and silent until clips are dropped into <c>SfxLibrary</c>. It is worth saying
        /// once, so a genuinely missing assignment is still discoverable, but not every frame.
        /// </remarks>
        public void PlayOneShot(AudioClip clip, float volume = 1f)
        {
            if (clip == null)
            {
                if (_warnedAboutNullClip) return;

                _warnedAboutNullClip = true;
                GameLog.Warn(LogCategory,
                    "A sound cue has no AudioClip assigned. See README section on SfxLibrary for " +
                    "where the files go. This is reported once per session.");
                return;
            }

            AudioSource voice = NextVoice();
            if (voice == null) return;

            voice.PlayOneShot(clip, Mathf.Clamp01(volume) * _sfxVolume * _masterVolume);
        }

        /// <summary>Releases the pooled voices. Called when the composition root tears down.</summary>
        public void Dispose()
        {
            if (_voiceHolder == null) return;

            Object.Destroy(_voiceHolder);
            _voiceHolder = null;
            _voices = null;
        }

        /// <summary>
        /// Round-robins through the pooled sources. Oldest voice is reused when all are busy, which
        /// is preferable to dropping the newest cue: the newest is the one the player just caused.
        /// </summary>
        private AudioSource NextVoice()
        {
            if (_voices == null) CreateVoices();
            if (_voices == null) return null;

            AudioSource voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            return voice;
        }

        private void CreateVoices()
        {
            _voiceHolder = new GameObject("SfxVoices") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(_voiceHolder);

            _voices = new AudioSource[VoiceCount];
            for (int i = 0; i < VoiceCount; i++)
            {
                var voice = _voiceHolder.AddComponent<AudioSource>();
                voice.playOnAwake = false;

                // 2D: this is a side-on arena a few units wide, and panning cues by world position
                // would put half of them outside the speakers for no gain.
                voice.spatialBlend = 0f;

                _voices[i] = voice;
            }
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
