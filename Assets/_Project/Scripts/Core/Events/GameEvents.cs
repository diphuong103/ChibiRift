using UnityEngine;

namespace ChibiRift.Core
{
    // Payloads carried by EventBus. They hold primitives and Core types only, so that
    // ChibiRift.UI can consume them without ever referencing ChibiRift.Gameplay.
    // Payloads that must reference a ScriptableObject live in ChibiRift.Data instead.

    /// <summary>HP changed on hero or enemy. Drives the HP bar and the boss/elite bars (SRS 19.2).</summary>
    public readonly struct HealthChangedEvent
    {
        public readonly int EntityId;
        public readonly float CurrentHealth;
        public readonly float MaxHealth;
        public readonly bool IsPlayer;

        public HealthChangedEvent(int entityId, float currentHealth, float maxHealth, bool isPlayer)
        {
            EntityId = entityId;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
            IsPlayer = isPlayer;
        }
    }

    /// <summary>
    /// A resolved hit. Spawns the floating damage number (HPS-008), drives knockback (COM-005) and
    /// feeds telemetry (TEL-003).
    /// </summary>
    /// <remarks>
    /// This is the one announcement of a landed hit, and it carries everything a reaction needs:
    /// who was hit, how hard, where, and from which direction. Knockback is applied by whoever was
    /// hit, subscribing through <see cref="IKnockbackReceiver"/>, rather than by the combat
    /// pipeline pushing them. Hit-stop and screen shake will subscribe here too, so adding them
    /// costs a new subscriber and no change to <c>CombatSystem</c>.
    ///
    /// <para>Delivery is synchronous: <c>EventBus.Publish</c> invokes handlers on the calling
    /// stack, so a subscriber reacts in the same frame the hit resolved, not the next one.</para>
    /// </remarks>
    public readonly struct DamageAppliedEvent
    {
        public readonly int TargetEntityId;
        public readonly DamageResult Result;
        public readonly Vector2 WorldPosition;
        public readonly bool TargetIsPlayer;

        /// <summary>Where the hit came from. Knockback direction is target minus this (COM-005).</summary>
        public readonly Vector2 AttackerPosition;

        /// <summary>Horizontal knockback speed for this hit, from config. Zero means no push.</summary>
        public readonly float KnockbackForce;

        /// <summary>Seconds the target's own movement yields to the knockback.</summary>
        public readonly float KnockbackDuration;

        /// <summary>
        /// True when this hit emptied the target's health. Carried here rather than read back off
        /// the target, because by the time a subscriber runs the target may already be leaving the
        /// scene. Hit stop uses it to give a kill the longest freeze (SRS 21).
        /// </summary>
        public readonly bool KilledTarget;

        public DamageAppliedEvent(
            int targetEntityId,
            in DamageResult result,
            Vector2 worldPosition,
            bool targetIsPlayer,
            Vector2 attackerPosition,
            float knockbackForce,
            float knockbackDuration,
            bool killedTarget)
        {
            KilledTarget = killedTarget;
            TargetEntityId = targetEntityId;
            Result = result;
            WorldPosition = worldPosition;
            TargetIsPlayer = targetIsPlayer;
            AttackerPosition = attackerPosition;
            KnockbackForce = knockbackForce;
            KnockbackDuration = knockbackDuration;
        }
    }

    /// <summary>An entity reached the death state (HPS-006, HPS-007).</summary>
    public readonly struct EntityDiedEvent
    {
        public readonly int EntityId;
        public readonly bool IsPlayer;
        public readonly Vector2 WorldPosition;
        public readonly DamageSource KillingBlowSource;

        public EntityDiedEvent(int entityId, bool isPlayer, Vector2 worldPosition, DamageSource killingBlowSource)
        {
            EntityId = entityId;
            IsPlayer = isPlayer;
            WorldPosition = worldPosition;
            KillingBlowSource = killingBlowSource;
        }
    }

    /// <summary>XP total moved. Drives the XP bar (SRS 19.2, EXP-002).</summary>
    public readonly struct ExperienceChangedEvent
    {
        public readonly int Level;
        public readonly float CurrentExperience;
        public readonly float ExperienceForNextLevel;

        public ExperienceChangedEvent(int level, float currentExperience, float experienceForNextLevel)
        {
            Level = level;
            CurrentExperience = currentExperience;
            ExperienceForNextLevel = experienceForNextLevel;
        }
    }

    /// <summary>Level threshold crossed (EXP-003). The upgrade panel opens and the game pauses (EXP-004).</summary>
    public readonly struct LevelUpEvent
    {
        public readonly int NewLevel;

        public LevelUpEvent(int newLevel) => NewLevel = newLevel;
    }

    /// <summary>Run-local currency total changed (SRS 16). Committed to the wallet at Post-Run (RUN-006).</summary>
    public readonly struct CurrencyChangedEvent
    {
        public readonly CurrencyType Currency;
        public readonly int Delta;
        public readonly int NewTotal;
        public readonly bool IsMetaWallet;

        public CurrencyChangedEvent(CurrencyType currency, int delta, int newTotal, bool isMetaWallet)
        {
            Currency = currency;
            Delta = delta;
            NewTotal = newTotal;
            IsMetaWallet = isMetaWallet;
        }
    }

    /// <summary>Wave progression changed (WAV-001..WAV-005).</summary>
    public readonly struct WaveStateChangedEvent
    {
        public readonly string StageId;
        public readonly int WaveIndex;
        public readonly int WaveCount;
        public readonly WaveState State;

        public WaveStateChangedEvent(string stageId, int waveIndex, int waveCount, WaveState state)
        {
            StageId = stageId;
            WaveIndex = waveIndex;
            WaveCount = waveCount;
            State = state;
        }
    }

    /// <summary>Boss became active. Shows the boss HP bar (SRS 19.2).</summary>
    public readonly struct BossActivatedEvent
    {
        public readonly string BossId;
        public readonly string DisplayName;
        public readonly float MaxHealth;

        public BossActivatedEvent(string bossId, string displayName, float maxHealth)
        {
            BossId = bossId;
            DisplayName = displayName;
            MaxHealth = maxHealth;
        }
    }

    /// <summary>Boss crossed a phase threshold (BOS-002, BOS-003).</summary>
    public readonly struct BossPhaseChangedEvent
    {
        public readonly string BossId;
        public readonly int PhaseIndex;
        public readonly float HealthNormalized;

        public BossPhaseChangedEvent(string bossId, int phaseIndex, float healthNormalized)
        {
            BossId = bossId;
            PhaseIndex = phaseIndex;
            HealthNormalized = healthNormalized;
        }
    }

    /// <summary>An elite spawned. Shows the elite HP bar and its visual tell (ELT-003).</summary>
    public readonly struct EliteActivatedEvent
    {
        public readonly int EntityId;
        public readonly string EnemyId;
        public readonly float MaxHealth;

        public EliteActivatedEvent(int entityId, string enemyId, float maxHealth)
        {
            EntityId = entityId;
            EnemyId = enemyId;
            MaxHealth = maxHealth;
        }
    }

    /// <summary>Run lifecycle transition (RUN-001, RUN-002, RUN-003, RUN-008).</summary>
    public readonly struct RunStateChangedEvent
    {
        public readonly RunLifecycleState State;

        public RunStateChangedEvent(RunLifecycleState state) => State = state;
    }

    /// <summary>Run finished. Post-Run reads this to build the summary (RUN-004) and commit once (RUN-005).</summary>
    public readonly struct RunEndedEvent
    {
        public readonly RunLifecycleState Outcome;
        public readonly float DurationSeconds;
        public readonly int ReachedStageIndex;
        public readonly int ReachedWaveIndex;
        public readonly int Level;
        public readonly int GoldEarned;
        public readonly int GemsEarned;

        public RunEndedEvent(
            RunLifecycleState outcome,
            float durationSeconds,
            int reachedStageIndex,
            int reachedWaveIndex,
            int level,
            int goldEarned,
            int gemsEarned)
        {
            Outcome = outcome;
            DurationSeconds = durationSeconds;
            ReachedStageIndex = reachedStageIndex;
            ReachedWaveIndex = reachedWaveIndex;
            Level = level;
            GoldEarned = goldEarned;
            GemsEarned = gemsEarned;
        }
    }

    /// <summary>Pause flipped (PAU-001).</summary>
    public readonly struct PauseStateChangedEvent
    {
        public readonly bool IsPaused;
        public readonly PauseReason Reason;

        public PauseStateChangedEvent(bool isPaused, PauseReason reason)
        {
            IsPaused = isPaused;
            Reason = reason;
        }
    }

    /// <summary>Skill cooldown ticked. Drives the Q/E/R cooldown display (SRS 19.2, COM-008).</summary>
    public readonly struct SkillCooldownChangedEvent
    {
        public readonly SkillSlot Slot;
        public readonly float RemainingSeconds;
        public readonly float TotalSeconds;

        public SkillCooldownChangedEvent(SkillSlot slot, float remainingSeconds, float totalSeconds)
        {
            Slot = slot;
            RemainingSeconds = remainingSeconds;
            TotalSeconds = totalSeconds;
        }
    }

    /// <summary>Dash cooldown ticked. Drives the dash indicator required by MOV-006 and SRS 19.2.</summary>
    public readonly struct DashCooldownChangedEvent
    {
        public readonly float RemainingSeconds;
        public readonly float TotalSeconds;

        public DashCooldownChangedEvent(float remainingSeconds, float totalSeconds)
        {
            RemainingSeconds = remainingSeconds;
            TotalSeconds = totalSeconds;
        }
    }

    /// <summary>
    /// Per-frame motor snapshot for the development overlay (P1 slice 1).
    /// Published by the player motor and read only by <c>ChibiRift.UI</c>, so the overlay never
    /// holds a reference into ChibiRift.Gameplay.
    /// </summary>
    public readonly struct PlayerMotorStateEvent
    {
        public readonly Vector2 Velocity;
        public readonly bool IsGrounded;
        public readonly int JumpCount;
        public readonly float CoyoteTimer;
        public readonly float JumpBufferTimer;

        public PlayerMotorStateEvent(
            Vector2 velocity, bool isGrounded, int jumpCount, float coyoteTimer, float jumpBufferTimer)
        {
            Velocity = velocity;
            IsGrounded = isGrounded;
            JumpCount = jumpCount;
            CoyoteTimer = coyoteTimer;
            JumpBufferTimer = jumpBufferTimer;
        }
    }

    /// <summary>
    /// The basic-attack chain advanced or reset (COM-002, COM-003).
    /// <paramref name="Step"/> is 0 when idle, 1..3 while a chain is running.
    /// </summary>
    public readonly struct ComboChangedEvent
    {
        public readonly int Step;
        public readonly int MaxStep;
        public readonly float WindowRemaining;

        public ComboChangedEvent(int step, int maxStep, float windowRemaining)
        {
            Step = step;
            MaxStep = maxStep;
            WindowRemaining = windowRemaining;
        }
    }

    /// <summary>
    /// An enemy changed state (AI-001). Read by the development overlay; no gameplay depends on it.
    /// </summary>
    public readonly struct EnemyStateChangedEvent
    {
        public readonly int EntityId;
        public readonly EnemyLifecycleState State;

        public EnemyStateChangedEvent(int entityId, EnemyLifecycleState state)
        {
            EntityId = entityId;
            State = state;
        }
    }

    /// <summary>
    /// Per-frame combat snapshot for the development overlay (P1 slice 3).
    /// Published by the hero's combat component and read only by <c>ChibiRift.UI</c>.
    /// </summary>
    public readonly struct PlayerCombatStateEvent
    {
        public readonly bool IsAttacking;
        public readonly bool IsHitboxActive;
        public readonly int ComboStep;
        public readonly float ComboWindowRemaining;
        public readonly Vector2 AimDirection;

        public PlayerCombatStateEvent(
            bool isAttacking, bool isHitboxActive, int comboStep, float comboWindowRemaining, Vector2 aimDirection)
        {
            IsAttacking = isAttacking;
            IsHitboxActive = isHitboxActive;
            ComboStep = comboStep;
            ComboWindowRemaining = comboWindowRemaining;
            AimDirection = aimDirection;
        }
    }

    /// <summary>
    /// How many enemies are alive and what the closest one is doing (P1 slice 3, development only).
    /// </summary>
    /// <remarks>
    /// Exists so the overlay can answer "why is nothing attacking me" without the UI assembly
    /// needing to see an enemy, which the assembly graph forbids.
    /// </remarks>
    public readonly struct EnemyCensusEvent
    {
        public readonly int AliveCount;
        public readonly EnemyLifecycleState NearestState;
        public readonly float NearestDistance;

        public EnemyCensusEvent(int aliveCount, EnemyLifecycleState nearestState, float nearestDistance)
        {
            AliveCount = aliveCount;
            NearestState = nearestState;
            NearestDistance = nearestDistance;
        }
    }

    /// <summary>
    /// A dash started (MOV-006). Drives the afterimage trail and the dash cue; the dash itself is
    /// already under way by the time this is published.
    /// </summary>
    public readonly struct DashStartedEvent
    {
        public readonly int EntityId;
        public readonly Vector2 WorldPosition;
        public readonly float Duration;
        public readonly float InvulnerableSeconds;

        public DashStartedEvent(int entityId, Vector2 worldPosition, float duration, float invulnerableSeconds)
        {
            EntityId = entityId;
            WorldPosition = worldPosition;
            Duration = duration;
            InvulnerableSeconds = invulnerableSeconds;
        }
    }

    /// <summary>Camera shake request (CAM-003). VFX and combat publish it; the camera rig consumes it.</summary>
    public readonly struct ScreenShakeRequestedEvent
    {
        public readonly float Amplitude;
        public readonly float Duration;

        public ScreenShakeRequestedEvent(float amplitude, float duration)
        {
            Amplitude = amplitude;
            Duration = duration;
        }
    }
}
