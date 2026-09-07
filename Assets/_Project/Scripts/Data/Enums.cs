namespace ChibiRift.Data
{
    /// <summary>Drives upgrade weighting and card presentation (UPG-002, UPG-004, RNG-001).</summary>
    public enum Rarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4
    }

    /// <summary>The four upgrade families of SRS 11.1.</summary>
    public enum UpgradeCategory
    {
        /// <summary>HP, Attack, Attack Speed, Move Speed, Crit Chance, Crit Damage.</summary>
        Stat = 0,

        /// <summary>Fireball, Dash, Spin, Projectile, AoE.</summary>
        ActiveSkill = 1,

        /// <summary>Burn, Bleed, Poison, Thorns, Lifesteal.</summary>
        Passive = 2,

        /// <summary>Extra projectiles, longer effects, explosion on death, chain hit.</summary>
        ModifierSynergy = 3
    }

    /// <summary>Which stat a Stat upgrade moves (SRS 11.1).</summary>
    public enum StatType
    {
        MaxHealth = 0,
        Attack = 1,
        AttackSpeed = 2,
        MoveSpeed = 3,
        CritChance = 4,
        CritMultiplier = 5,
        Defense = 6,
        DamageReduction = 7
    }

    /// <summary>Delivery shape of a skill (SRS 11.1, COM-007).</summary>
    public enum SkillType
    {
        Melee = 0,
        Projectile = 1,
        AreaOfEffect = 2,
        Dash = 3,
        Buff = 4
    }

    /// <summary>Enemy archetypes of SRS 13. Tank and Flying are marked Expansion there.</summary>
    public enum EnemyArchetype
    {
        Melee = 0,
        Ranged = 1,
        Charger = 2,
        Tank = 3,
        Flying = 4
    }

    /// <summary>The three MVP elite modifiers required by ELT-002.</summary>
    public enum EliteModifierType
    {
        /// <summary>Absorbs damage behind a shield pool.</summary>
        Shielded = 0,

        /// <summary>Gains speed and damage at or below 50% HP.</summary>
        Enraged = 1,

        /// <summary>Deals area damage on death.</summary>
        Explosive = 2
    }

    /// <summary>How a wave is judged complete (WAV-001, WAV-004).</summary>
    public enum WaveClearCondition
    {
        /// <summary>Every spawned enemy is dead.</summary>
        AllEnemiesDefeated = 0,

        /// <summary>The wave duration elapsed.</summary>
        DurationElapsed = 1,

        /// <summary>A kill quota was met.</summary>
        KillCountReached = 2
    }
}
