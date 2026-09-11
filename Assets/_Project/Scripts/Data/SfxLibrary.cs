using UnityEngine;

namespace ChibiRift.Data
{
    /// <summary>
    /// The nine sound cues P1 needs, one field each (SRS 22, P1-31).
    /// </summary>
    /// <remarks>
    /// <para><b>Every field may be empty.</b> No audio ships yet; the wiring is complete and silent
    /// until clips are dropped in. A missing clip is not an error and must never throw — it logs
    /// once and stays quiet, so a half-filled library is a usable state rather than a broken one.</para>
    ///
    /// <para>This lives in ChibiRift.Data rather than beside the audio service, because the service
    /// is in ChibiRift.Core and Core cannot reference Data. <c>SfxPlayer</c> in ChibiRift.Gameplay
    /// holds this asset and hands clips to <c>IAudioService.PlayOneShot</c>.</para>
    /// </remarks>
    [CreateAssetMenu(fileName = "SFX_", menuName = "ChibiRift/SFX Library", order = 60)]
    public sealed class SfxLibrary : GameDataAsset
    {
        [Header("Hero attack (COM-001, COM-002)")]
        [Tooltip("First combo step.")]
        [SerializeField] private AudioClip _attack1;

        [Tooltip("Second combo step.")]
        [SerializeField] private AudioClip _attack2;

        [Tooltip("Third combo step. The heaviest of the three.")]
        [SerializeField] private AudioClip _attack3;

        [Header("Impact (HPS-003, COM-006)")]
        [Tooltip("An ordinary hit landing on anything.")]
        [SerializeField] private AudioClip _hit;

        [Tooltip("A critical hit. Layered over, or instead of, the ordinary hit.")]
        [SerializeField] private AudioClip _crit;

        [Header("Movement (MOV-002, MOV-006)")]
        [Tooltip("Jump and double jump.")]
        [SerializeField] private AudioClip _jump;

        [Tooltip("Dash.")]
        [SerializeField] private AudioClip _dash;

        [Header("Damage taken (HPS-005, HPS-007)")]
        [Tooltip("The hero being hit.")]
        [SerializeField] private AudioClip _hurtHero;

        [Tooltip("An enemy dying.")]
        [SerializeField] private AudioClip _enemyDeath;

        /// <summary>First combo step (COM-002).</summary>
        public AudioClip Attack1 => _attack1;

        /// <summary>Second combo step (COM-002).</summary>
        public AudioClip Attack2 => _attack2;

        /// <summary>Third combo step (COM-002).</summary>
        public AudioClip Attack3 => _attack3;

        /// <summary>An ordinary hit landing (HPS-003).</summary>
        public AudioClip Hit => _hit;

        /// <summary>A critical hit (COM-006).</summary>
        public AudioClip Crit => _crit;

        /// <summary>Jump and double jump (MOV-002, MOV-003).</summary>
        public AudioClip Jump => _jump;

        /// <summary>Dash (MOV-006).</summary>
        public AudioClip Dash => _dash;

        /// <summary>The hero being hit (HPS-005).</summary>
        public AudioClip HurtHero => _hurtHero;

        /// <summary>An enemy dying (HPS-007).</summary>
        public AudioClip EnemyDeath => _enemyDeath;

        /// <summary>Clip for one combo step, 1-based. Null when that step has no clip yet.</summary>
        public AudioClip AttackStep(int step)
        {
            switch (step)
            {
                case 1: return _attack1;
                case 2: return _attack2;
                case 3: return _attack3;
                default: return null;
            }
        }
    }
}
