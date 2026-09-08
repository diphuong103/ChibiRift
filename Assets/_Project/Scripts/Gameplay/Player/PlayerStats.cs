using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Live stat block for the hero during a Run (HER-001): Attack, MoveSpeed, crit, Defense and
    /// DamageReduction, after meta upgrades and in-run picks are folded in.
    /// </summary>
    /// <remarks>
    /// Health is deliberately <b>not</b> here. Until P1 slice 2 this component owned hit points
    /// and implemented <see cref="IDamageable"/>, which meant the hero had a second, independent
    /// notion of "dead" from the one every enemy used. <see cref="HealthComponent"/> now owns
    /// health for every entity including the hero; this component owns the numbers that feed it
    /// (HPS-009) and pushes them across whenever the build changes.
    /// </remarks>
    [RequireComponent(typeof(HealthComponent))]
    [DisallowMultipleComponent]
    public sealed class PlayerStats : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("Hero definition. Every base number comes from here, never from this script (SRS 35).")]
        [SerializeField] private HeroData _heroData;

        [Tooltip("Global balance asset supplying MinDamage and the DamageReduction cap.")]
        [SerializeField] private BalanceConfig _balanceConfig;

        /// <summary>Full live stat block, including upgrade contributions applied this Run (UPG-008).</summary>
        public StatBlock Current => _current;

        /// <summary>Attack stat, the base damage of every basic attack (COM-001).</summary>
        public float Attack => _current.Attack;

        /// <summary>Top horizontal speed (MOV-001).</summary>
        public float MoveSpeed => _current.MoveSpeed;

        /// <summary>Chance for a hit to crit, 0..1 (COM-006).</summary>
        public float CritChance => _current.CritChance;

        /// <summary>Hero definition backing this instance.</summary>
        public HeroData Hero => _heroData;

        /// <summary>Balance asset backing this instance.</summary>
        public BalanceConfig Balance => _balanceConfig;

        private StatBlock _current;
        private HealthComponent _health;

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();

            // A hero placed straight into a scene has no one to call Initialize, so seed from the
            // serialized asset. Initialize overwrites this when a Run starts.
            if (_heroData != null) ApplyStats(_heroData.BaseStats);
        }

        /// <summary>
        /// Seeds the live block from HeroData plus purchased meta upgrades (META-001..003), then
        /// hands health and defences to <see cref="HealthComponent"/>.
        /// </summary>
        public void Initialize(HeroData hero, BalanceConfig balance)
        {
            _heroData = hero;
            _balanceConfig = balance;

            // TODO(META-001, META-002, META-003): fold the purchased permanent upgrades into
            // _current here, before health is seeded from it.
            ApplyStats(hero != null ? hero.BaseStats : default);
        }

        /// <summary>Recomputes the block after an upgrade is picked (EXP-008).</summary>
        public void RecalculateFromBuild()
        {
            // TODO(EXP-008): fold the RunState upgrade stacks into _current so the pick takes
            // effect immediately. Defences are pushed across below; MaxHealth changes are a
            // separate decision (heal on increase, or raise the ceiling only).
            if (_health != null) _health.SetDefenses(_current.Defense, _current.DamageReduction);
        }

        private void ApplyStats(in StatBlock stats)
        {
            _current = stats;

            if (_health == null) _health = GetComponent<HealthComponent>();
            if (_health == null) return;

            _health.Initialize(_current.MaxHealth, _current.Defense, _current.DamageReduction);
        }
    }
}
