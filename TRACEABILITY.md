# Traceability Matrix

Every ID-bearing requirement in `SRS_2D_Action_Roguelite_RPG_Unity_v1.1.docx` mapped to the file
that serves it. Nothing is left blank: a requirement with no home yet says **chưa triển khai**.

### Status legend

| Status | Meaning |
|---|---|
| **Done** | Implemented and working. Where a test exists it is named. |
| **Schema** | The data model exists and is complete; the behaviour that consumes it does not. |
| **Skeleton** | Class, interface and events exist with a `TODO(<id>)` marking the gap. |
| **Chưa triển khai** | No code yet. Reason given. |

Paths are relative to `Assets/_Project/`.

---

## 8.1 Player Movement

| ID | Requirement | Where | Status |
|---|---|---|---|
| MOV-001 | Move left/right with A/D | `Settings/ChibiRiftControls.inputactions` (1D-axis composite), `Scripts/Core/Services/InputReader.cs` (`MoveAxis`), `Scripts/Gameplay/Player/PlayerController.cs`, `PlayerMotor.ApplyHorizontal` (facing moved to `PlayerCombat` in slice 2, see COM-009 and OI-20), `Data/MovementConfig.cs` (`GroundAccel` / `GroundDecel` / `AirAccel` / `AirDecel`) | **Done** — `Test_MoveRight_VelocityConvergesToMoveSpeed`, `Test_ReleaseInput_StopsWithin150ms` |
| MOV-002 | Jump with Space | `InputReader.JumpPressed` / `JumpHeld`, `PlayerMotor.RequestJump` + `ApplyJumpAndGravity`, `MovementConfig.JumpVelocity` / `GravityUp` / `FallMultiplier` / `LowJumpMultiplier` / `CoyoteTime` / `JumpBuffer` | **Done** — `Test_Jump_PeakHeightInRange`, `Test_CoyoteTime_JumpAfterEdge`, `Test_JumpBuffer_LandAndJump` |
| MOV-003 | Double jump | `PlayerMotor.JumpCount` (reset on landing), `HeroData.MaxJumpCount` (2), `MovementConfig.DoubleJumpVelocity` (13, separate from the ground jump) | **Done** — `Test_DoubleJump_OnlyOnce` |
| MOV-004 | Bounded by collision / world boundary | `PlayerMotor.UpdateGrounded` (`Physics2D.OverlapBox`, not `OnCollisionStay`), `PlayerMotor.ApplyBoundary` + `CheckFallLimit`, `Gameplay/SceneContext.cs` (`WorldHalfWidth` 20, `FallLimitY` -10, `SpawnPoint`), `Core/Utilities/GameLayers.cs` (`SolidWorldMask`), `ProjectSettings/Physics2DSettings.asset` | **Done** — `Test_WallCollision_NoPassThrough`, `Test_FallThroughHole_Respawn` |
| MOV-005 | Input survives FPS variation | `InputReader.cs` (Input System, no legacy polling); `PlayerController.Update` samples input every frame so a tap between physics steps is not dropped, while `PlayerMotor.FixedUpdate` integrates on a fixed `dt` | **Done** |
| MOV-006 | Dash on Left Shift with i-frames and cooldown | `Gameplay/Player/PlayerDash.cs` + `Combat/DashState.cs`; direction from move input or, standing still, from the cursor facing; i-frames through the shared `HealthComponent.BeginInvulnerability`; press buffered by `Combat/InputBuffer.cs` so a press during hit stop survives. `DashConfig` supplies 5u / 0.25s / 0.25s i-frame / 1.5s cooldown | **Done** — `Test_Dash_MovesExactDistance`, `Test_Dash_GrantsInvulnerability`, `Test_Dash_DoesNotCutExistingHurtIFrame`, `Test_Dash_RespectsCooldown`, `Test_Dash_BufferedDuringHitStop` |
| MOV-007 | Dash cannot clip colliders or leave the arena | `PlayerMotor.FixedUpdate` ends the dash when the body stops moving against a collider (`DashConfig.WallStopFraction`); `ApplyBoundary` still clamps the arena edge | **Done** — `Test_Dash_StopsAtWall` |

## 8.2 Combat & Attack

| ID | Requirement | Where | Status |
|---|---|---|---|
| COM-001 | Mouse Left basic attack | `InputReader.AttackPressed`, `PlayerController.Update`, `PlayerCombat.RequestAttack`; the swing slows the hero via `AttackData.MoveSpeedMultiplierWhileAttacking` and `PlayerMotor.SetSpeedMultiplier` | **Done** — `Test_Attack_SingleTargetHitOnce` |
| COM-002 | 3 hit combo | `PlayerCombat.RequestAttack` / `BeginStep` / `EndStep`, `Data/AttackData.cs` (three `AttackStep`, multipliers 1.0 / 1.1 / 1.6), `HeroData.ComboLength` (validated == 3) | **Done** — `Test_Combo_ThreeHitsInWindow`, `Test_Attack3_DealsHighestDamage` |
| COM-003 | Combo resets on timeout | `PlayerCombat.Update` + `ResetCombo`, `HeroData.ComboWindow` (0.5); also resets on leaving the ground and on taking a hit. Publishes `ComboChangedEvent` | **Done** — `Test_Combo_ResetsAfterWindowExpires`, `Test_Combo_ResetsWhenLeavingGround`, `Test_Hero_HitResetsCombo` |
| COM-004 | Clear hitbox / hurtbox, active frames only | `PlayerCombat.SweepHitbox` (`Physics2D.OverlapBox` inside `[ActiveStartTime, ActiveEndTime]`), per-swing `HashSet` for one hit per target, collision matrix. Frame data is in seconds, not Animation Events — see OI-18 | **Done** — `Test_Attack_HitboxOnlyActiveInWindow` |
| COM-005 | Damage, knockback, hit feedback | `DamageAppliedEvent` carries the attacker position and the force; the target applies it through `IKnockbackReceiver` (`PlayerMotor`, `EnemyMotor`) using the shared `Combat/KnockbackState.cs`. `CombatSystem` never pushes anyone: hit-stop and shake will subscribe to the same event. Horizontal only in P1 | **Done** — `Test_Knockback_MovesTargetAwayFromAttacker`, `Test_Knockback_ReturnsControlAfterDuration` |
| COM-006 | Critical hit | `DamageCalculator` step 2 + `RollCritical`, rolled inside `CombatSystem.DealDamage` with the seeded RNG (RNG-004). `PlayerCombat` passes `PlayerStats.CritChance` (0.05); multiplier 2.0 from `BalanceConfig`. `DamageAppliedEvent.Result.WasCritical` drives the colour, the longer hit stop and the harder shake | **Done** — `Test_Crit_AppliesMultiplier`, `Test_Crit_MarksDamageEventAsCrit`, `DamageCalculatorTests.Step2_AppliesCriticalMultiplierOnlyOnCrit` |
| COM-007 | Q/E/R special skills | `InputReader.WasSkillPressed` -> `PlayerController` -> `Skills/SkillSystem.cs` (a component on the hero, not a service). Two behaviours serve three skills: `ProjectileSkill` for Q, `AoeSkill` for E and R with different assets. Aim from `PlayerCombat`, damage through `CombatSystem`, feedback from `DamageAppliedEvent`. Press buffered like jump, dash and attack | **Done** — `Test_SkillQ_FiresProjectileTowardCursor`, `Test_SkillQ_ProjectileDealsDamageThroughCombatSystem`, `Test_SkillE_HitsAllEnemiesInRadius`, `Test_Skill_CannotFireWhileDashing`, `Test_Skill_BufferedDuringHitStop`, `Test_SkillCooldownEvent_FiresWithCorrectSlotIndex` |
| COM-008 | Cooldown only, no mana pool | `SkillSystem.CanCast` gates on the cooldown alone; `SkillData.Cooldown` supplies it. No energy or mana field exists anywhere in the project, by construction rather than omission | **Done** — `Test_Skill_RespectsCooldown` |
| COM-009 | Mouse aim, no auto-target | `InputReader.AimWorldPosition` -> `PlayerCombat.SetAimTarget`: sets `AimDirection`, places and rotates the hitbox at `HitboxOffsetDistance`, and is the single writer of `SpriteRenderer.flipX` (OI-20) | **Done** — `Test_MouseAim_HitboxFollowsCursorDirection`, `Test_MouseAim_SpriteFlipsCorrectly` |

## 9 Health, Damage and Death

| ID | Requirement | Where | Status |
|---|---|---|---|
| HPS-001 | Hero Current/Max HP | `Gameplay/Combat/HealthComponent.cs`, seeded by `PlayerStats.ApplyStats` from `HeroData.BaseStats`. Publishes `HealthChangedEvent` | **Done** |
| HPS-002 | Enemy HP/Max HP | The same `HealthComponent`, seeded by `HealthComponent.SeedFrom(EnemyData)` or by `EnemyController.Distribute`. Shown by `UI/EnemyHealthBar.cs`, which identifies its owner by its parent's instance id and so needs no reference into gameplay | **Done** |
| HPS-003 | One unified damage pipeline | `CombatSystem.DealDamage` is the only entry; it calls `DamageCalculator.Calculate` and is the only caller of `IDamageable.ApplyDamage` | **Done** — `DamagePipelineSourceTests` (text scan, OI-19) |
| HPS-004 | No damage to a dead target | Guarded twice: `CombatSystem.DealDamage` refuses the hit (so no damage number appears) and `HealthComponent.ApplyDamage` refuses it again | **Done** — `Test_Damage_DeadTargetTakesNoMoreDamage` |
| HPS-005 | I-frames after a hit and during dash | `HealthComponent.IsInvulnerable` / `BeginInvulnerability`; a surviving hit opens the window automatically for `HealthComponent.HurtIFrameDuration` (hero 0.8, enemies 0). `Gameplay/Feel/SpriteFeedback.cs` shows it. A longer window never shortens a running one, which is what lets the dash open its own 0.25s without truncating the 0.8s a hit just granted | **Done** — `Test_Hero_IFrameBlocksSecondHit`, `Test_Hero_IFrameExpiresAfterDuration`, `Test_Dash_GrantsInvulnerability`, `Test_Dash_DoesNotCutExistingHurtIFrame` |
| HPS-006 | Hero HP ≤ 0 enters Death | `HealthComponent.EnterDeathState`, guarded so `Died` and `EntityDiedEvent` fire once | **Done** — `Test_Death_FiresOnEntityDiedOnce` |
| HPS-007 | Enemy death triggers reward/XP | The same guarded `HealthComponent.EnterDeathState`; `EnemyController.OnHealthDied` is where XP and gold hang. Corpse disables its collider and retires after `EnemyData.CorpseLingerSeconds` | Death **Done** — `Test_Death_FiresOnEntityDiedOnce`; reward grant Skeleton |
| HPS-008 | Damage number on target | `UI/DamageNumberSpawner.cs` + `UI/DamageNumber.cs`, pooled with `ObjectPool<T>`, driven only by `DamageAppliedEvent`. Crit colour is set up although crits arrive in slice 4 | **Done** |
| HPS-009 | Defense (flat) and DamageReduction on hero and enemy | `Data/StatBlock.cs`, `HeroData`, `EnemyData`, `Core/Interfaces/IDamageable` | **Done** — `Step3_SubtractsFlatDefenseBeforeApplyingDamageReduction`, `DamageReductionAboveCapIsClampedToPointEight` |
| HPS-010 | FinalDamage ≥ MinDamage | `DamageCalculator.cs` step 4, `BalanceConfig.MinDamage` = 1 | **Done** — `Step4_NegativeDamageIsClampedToMinDamage`, `Step4_ZeroDamageIsClampedToMinDamage` |
| SRS 9 formula | Mandatory 4-step order | `DamageCalculator.Calculate`; `DamageResult` exposes every intermediate | **Done** — `OrderIsMandatory_CritAppliesBeforeArmorSubtraction` |

## 10 Experience & Level Up

| ID | Requirement | Where | Status |
|---|---|---|---|
| EXP-001 | Enemy grants XP | `Data/EnemyData.ExperienceReward`, `BossData.ExperienceReward` | Schema Done |
| EXP-002 | Hero gains XP in a Run | `Gameplay/Progression/ExperienceSystem.cs`, `ExperienceChangedEvent` | Skeleton |
| EXP-003 | Enough XP triggers Level Up | `Progression/ExperienceCurve.cs` | **Done** — `ExperienceCurveTests` (12 cases) |
| EXP-004 | Game pauses while choosing | `Core/Services/PauseManager.cs`, `PauseReason.LevelUpSelection` | **Done** |
| EXP-005 | Exactly 3 valid choices | `Progression/UpgradeRoller.cs`, `BalanceConfig.UpgradeChoiceCount` = 3 | **Done** — `ReturnsExactlyThreeChoicesFromAHealthyPool`, `ChoiceCountComesFromBalanceConfig` |
| EXP-006 | No duplicates in one roll | `UpgradeRoller.cs` (draw without replacement) | **Done** — `NoDuplicatesWithinOneRoll` (100 seeds) |
| EXP-007 | Pick one, close the panel | `Scripts/UI/LevelUpPanel.OnCardSelected` | Skeleton |
| EXP-008 | Upgrade applies immediately | `Progression/UpgradeSystem.SelectUpgrade`, `PlayerStats.RecalculateFromBuild` | Skeleton |
| EXP-009 | Fallback Pool tops up to 3 | `UpgradeRoller.cs` second loop | **Done** — `TopsUpFromFallbackPoolWhenTooFewNormalUpgradesRemain`, `ExhaustedNormalPoolIsFullyCoveredByFallback` |
| SRS 10 model | `BaseXP × Growth^(level-1)` | `ExperienceCurve.ExperienceRequired`, `BalanceConfig` | **Done** — see OI-02 for the confirmed values |

## 11 Roguelite Upgrade System

| ID | Requirement | Where | Status |
|---|---|---|---|
| UPG-001 | Unique upgrade ID | `Data/GameDataAsset.cs` `OnValidate` (empty + duplicate warnings) | **Done** |
| UPG-002 | Name, icon, description, rarity, effect | `Data/UpgradeData.cs` | Schema Done |
| UPG-003 | Unlock / availability condition | `UpgradeData.MinimumHeroLevel` | Schema Done, filter Skeleton |
| UPG-004 | RNG weighted by rarity/weight | `UpgradeRoller.EffectiveWeight`, `BalanceConfig.RarityWeights` | **Done** — `HeavierWeightsAreDrawnMoreOften`, `RarityMultiplierCanSuppressATier` |
| UPG-005 | Stacking when configured | `UpgradeData.IsStackable` / `MaxStack` | **Done** — `StackableUpgradeStaysUntilItReachesMaxStack` |
| UPG-006 | Build stored in Run state | `Gameplay/Run/RunState.UpgradeStacks` / `AddUpgradeStack` | **Done** |
| UPG-007 | UI shows the impact clearly | `Scripts/UI/UpgradeCardView.cs` | Skeleton |
| UPG-008 | ≥ 12 upgrades in the MVP pool | 1 sample asset only (`Data/UPG_AttackUp.asset`) | **Chưa triển khai** — content work, P2. See OI-09 |
| UPG-009 | Fallback Pool, repeatable, uncapped | `UpgradeData.IsFallback` (+ `OnValidate` forces stackable), `UpgradeRoller` | Mechanism **Done** — `FallbackEntriesMayRepeatWithinOneRoll`; pool content chưa triển khai |
| UPG-010 | Maxed non-stackables leave the pool | `UpgradeRoller.IsAtStackCeiling` | **Done** — `NonStackableUpgradeLeavesThePoolOnceTaken` |

## 12 Hero System

| ID | Requirement | Where | Status |
|---|---|---|---|
| HER-001 | Base HP, Attack, Speed, Crit | `Data/HeroData.BaseStats`, `Data/StatBlock.cs` | **Done** |
| HER-002 | Hero passive | `HeroData.Passive` | Schema Done |
| HER-003 | Configurable skill set | `HeroData.Skills`, `Data/SkillData.cs` | Schema Done |
| HER-004 | Animation set Idle/Run/Jump/Attack1-3/Hurt/Death | `HeroData.AnimatorController` | Schema Done; **art chưa triển khai** (out of skeleton scope) |
| HER-005 | Hero unlocked via meta progression | `HeroData.UnlockedByDefault` / `GemUnlockCost`, `MetaSave.UnlockedHeroIds` | Schema Done, logic Skeleton |
| HER-006 | Defense/DamageReduction + dash config | `HeroData.BaseStats`, `HeroData.Dash`, `Data/DashConfig.cs` | **Done** |

## 13 Enemy & AI

| ID | Requirement | Where | Status |
|---|---|---|---|
| AI-001 | State machine Idle/Chase/Attack/Hurt/Death | `Gameplay/Enemy/EnemyAI.cs`, one method per state dispatched from a single switch; `EnemyLifecycleState` in Core (the state-change event carries it, and Core cannot see Gameplay). Hurt interrupts everything but Death | **Done** — `Test_Enemy_HurtStunInterruptsWindup` |
| AI-002 | Detection range | `EnemyAI.TickIdle` / `TickChase` / `TickReturnToSpawn`, `EnemyData.DetectionRange` (8) and `LoseAggroRange` (12). The gap between the two is hysteresis: one threshold would flip state every evaluation for a hero standing on it | **Done** — `Test_Enemy_StaysIdleWhenPlayerFar`, `Test_Enemy_ChasesWhenPlayerInDetectionRange`, `Test_Enemy_LosesAggroBeyondLoseRange`, `Test_Enemy_ReturnsToSpawnAfterLosingAggro` |
| AI-003 | No attack while dead | `EnemyAI.FixedUpdate` returns on `Death` before anything else; `EnemyMotor` also refuses the killing blow's own knockback so the corpse cannot slide (OI-21) | **Done** — `Test_Enemy_DeathStopsAllAI` |
| AI-004 | Attack cooldown / window | `Gameplay/Enemy/EnemyAttack.cs`, three phases from `EnemyAttackConfig` (windup 0.35 / active 0.10 / recovery 0.45, cooldown 1.2 counted from the end of recovery). Hitbox open only inside the active window; the swing is committed at windup and plays out even if the hero leaves | **Done** — `Test_Enemy_AttacksWhenInRange`, `Test_Enemy_AttackRespectsCooldown`, `Test_Enemy_HitboxOnlyActiveInWindow` |
| AI-005 | No infinite pathfinding loop | Two mechanisms: `EnemyAI.UpdateStuckDetection` sidesteps when a chase covers less than `StuckMinDisplacement` in `StuckCheckWindow`, and `EnemyMotor.SeparationPush` keeps a crowd from collapsing into one silhouette | **Done** |
| AI-006 | Spawn/despawn owned by Wave System | `Gameplay/Enemy/EnemySpawner.cs` over `ObjectPool<T>`, prewarmed at scene load. The single owner of a pooled enemy's lifetime: `HealthComponent` announces `CorpseExpired` and `EnemyController` returns the instance. A second release is refused rather than handing one object to two callers | Spawner **Done** — `Test_Pool_NoInstantiateAfterPrewarm`, `Test_Pool_EnemyFullyResetOnReuse`, `Test_Pool_DoubleDespawnIsSafe`; driven by `WaveManager` from P2 slice 1 — `Test_Wave_UsesPoolNotInstantiate` |
| ELT-001 | Elite = base + modifier + ×3 HP / ×1.5 dmg | `BalanceConfig.EliteHealthMultiplier` / `EliteDamageMultiplier` combined with the stage multiplier in `EnemySpawner.Spawn(EnemyData, Vector2, bool, float, float)`, applied via `HealthComponent.SeedFrom(EnemyData, float)` and `EnemyAttack.DamageMultiplier` | **Done** — stat scaling only, exercised by Stage 1's wave 5 elite placeholder (P2 slice 1); no visual tell (see ELT-003) |
| ELT-002 | ≥ 3 modifiers: Shielded, Enraged, Explosive | `EliteModifierType` enum + `EliteModifierData` fields for all three; `ELT_Enraged.asset` sample | Schema Done; 2 remaining assets chưa triển khai (content) |
| ELT-003 | Distinct visuals + HP bar | `EliteModifierData.AuraTint` / `ScaleMultiplier`, `EliteActivatedEvent`, `HudController` | Schema Done, visuals Skeleton |
| ELT-004 | Higher XP and reward | `EnemyData.EliteRewardMultiplier` | Schema Done |
| ELT-005 | Modifier combo cannot exceed stat caps | `EnemyController.Configure` TODO | Skeleton |

## 14 Wave & Stage

**Event contract (P2 slice 1).** Wave and stage progress each have exactly one event —
`WaveStateChangedEvent(StageId, WaveIndex, WaveCount, WaveState, EnemiesRemaining)` and
`StageStateChangedEvent(StageId, StageState)`, both in `Core/Events/GameEvents.cs`. Check there
before adding a new one (OI-33): a brief once asked for three separately-named events
(`OnWaveStarted`/`OnWaveCleared`/`OnStageCleared`) without checking that `WaveStateChangedEvent` was
already reserved and referenced from this table.

| ID | Requirement | Where | Status |
|---|---|---|---|
| WAV-001 | ID, duration/kill condition, composition | `Data/WaveData.cs`, `WaveEntry`; five waves authored for Stage 1 in `SampleDataGenerator.cs` | **Done** — `Test_Wave_SpawnsCorrectEnemyCount` |
| WAV-002 | Wave starts only when stage allows | `WaveManager.BeginWave` has exactly one caller, `StageManager`, which only calls it while its own state is `RunningWaves` — holds by construction, not a runtime check | **Done** |
| WAV-003 | Spawn budget respected | `WaveManager.AdvanceSpawning` checks `EnemySpawner.ActiveEnemyCount` against `WaveData.SpawnBudget` before every spawn, retrying next `Tick` | **Done** |
| WAV-004 | Wave clear detected precisely | `WaveClearCondition` enum; `WaveManager.EvaluateClearCondition`, gated to only run once every entry has finished spawning | **Done** — `Test_Wave_CompletesWhenAllEnemiesDead` (Stage 1 uses `AllEnemiesDefeated`; `DurationElapsed`/`KillCountReached` implemented but unexercised by a test) |
| WAV-005 | Transition gap between waves | `WaveData.TransitionDelay` (3s baseline), counted down in `WaveManager.Tick` | **Done** — `Test_Wave_AdvancesAfterDelay` |
| STG-001 | Stage orders its waves | `Data/StageData.Waves`, `StageManager.BeginStage`/`HandleWaveCleared` | **Done** — `Test_Stage_FiresStageClearedAfterLastWave` |
| STG-002 | Stage clear condition | `StageManager.HandleWaveCleared`, `StageData.ClearGoldReward` (reward *granting* is a later slice — RunManager/RUN-002 own that, not this one) | Progression **Done**, reward grant Skeleton |
| STG-003 | Transition to Boss / next stage | `StageState` enum; `StageManager.HandleWaveCleared` moves to `BossFight` when `StageData.Boss` is set, else `Cleared` | **Done** — the `BossFight` branch itself is P2's boss slice |

## 15 Boss

| ID | Requirement | Where | Status |
|---|---|---|---|
| BOS-001 | HP, damage, pattern, reward | `Data/BossData.cs` | Schema **Done** |
| BOS-002 | ≥ 2 phases in MVP | `BossData.Phases` + `OnValidate` warning; `BOSS_Stage1.asset` has 2 | Schema **Done** |
| BOS-003 | Phase by HP threshold or event | `BossPhase.HealthThreshold` (0.7 per SRS 15), `BossManager.EvaluatePhase` | Skeleton — Schema Done |
| BOS-004 | Clear telegraph per phase | `BossPhase.TelegraphDuration` | Schema Done, VFX chưa triển khai |
| BOS-005 | Death sequence then reward | `BossData.DeathSequenceDuration`, `BossManager.OnBossDefeated` | Skeleton |

## 16–17 Economy, Hub, Meta

| ID | Requirement | Where | Status |
|---|---|---|---|
| HUB-001 | Hub shows Gold/Gem | `Meta/CurrencyManager.cs`, `Scripts/UI/HubUiController.cs`, `CurrencyChangedEvent` | Currency **Done**, display Skeleton |
| HUB-002 | Player can Start Run | `HubUiController.OnStartRun` → `SceneFlowManager` | **Done** (navigates); Run start Skeleton |
| HUB-003 | View / select hero | `Meta/HubController.SelectHero` | Skeleton |
| META-001 | Permanent HP upgrade | `Meta/MetaProgressionManager.ApplyToHeroStats` TODO, `MetaSave.MetaUpgrades` | Persistence **Done**, logic Skeleton |
| META-002 | Permanent Attack upgrade | as above | Persistence Done, logic Skeleton |
| META-003 | Permanent Crit upgrade | as above | Persistence Done, logic Skeleton |
| META-004 | Explicit level and cost | `MetaUpgradeEntry.Level`, `MetaProgressionManager.GetNextLevelCost` TODO | Skeleton — a `MetaUpgradeData` asset type is **chưa triển khai** |
| META-005 | Cannot buy without currency | `CurrencyManager.TrySpend` (refuses when short) | **Done** |
| META-006 | Meta upgrades persist across Runs | `Save/MetaSave.cs`, `Save/SaveManager.cs` | **Done** |
| SRS 16 policy | Gold/Gem never lost on death | `RunState.GoldCollected` / `GemsCollected` + `RewardCommitted`; no deduction path exists | **Done** by construction |

## 18 Run / Post-Run

| ID | Requirement | Where | Status |
|---|---|---|---|
| RUN-001 | States Started/InProgress/Completed/Failed/Abandoned | `Core/Types/GameTypes.cs` `RunLifecycleState`, `Run/RunState.cs` | **Done** |
| RUN-002 | Victory ends the Run | `Run/RunManager.CompleteRun` | Skeleton |
| RUN-003 | Death ends the Run | `RunManager.FailRun` | Skeleton |
| RUN-004 | Post-Run shows the result | `RunEndedEvent`, `Scripts/UI/PostRunPanel.cs` | Event **Done**, display Skeleton |
| RUN-005 | Reward computed exactly once | `RunState.RewardCommitted`, `Run/RewardManager.CommitRunRewards` | Guard **Done**, commit Skeleton |
| RUN-006 | All Gold/Gem move to the wallet once | `RewardManager.cs` + `CurrencyManager.Add` | Skeleton |
| RUN-007 | Player can return to Hub | `PostRunPanel.OnReturnToHub` | **Done** |
| RUN-008 | Abandon ends as Abandoned, still via Post-Run | `RunManager.AbandonRun`, `RunLifecycleState.Abandoned` | Skeleton — state Done |

## 19 UI/UX

| ID | Requirement | Where | Status |
|---|---|---|---|
| SRS 19.1 | Main Menu: Play / Settings / How to Play / Quit | `Scripts/UI/MainMenuController.cs`, `Scenes/MainMenu.unity` | **Done** (4 buttons wired) |
| SRS 19.2 | HUD: HP, XP, level, skills, dash, buffs, boss/elite bars | `Scripts/UI/HudController.cs` + the events in `Core/Events/GameEvents.cs` | Events **Done**, display Skeleton |
| SRS 19.3 | Level Up overlay, 3 cards, hover, mouse select | `Scripts/UI/LevelUpPanel.cs`, `UpgradeCardView.cs` | Skeleton |
| SRS 19.4 | Settings: volumes, fullscreen, resolution, telemetry | `Scripts/UI/SettingsPanel.cs`, `Save/SettingsManager.cs`, `SettingsSave` | Backend **Done**, panel Skeleton |
| SRS 19.4 | Key Rebind (Should), Language (Could) | — | **Chưa triển khai** — out of MVP (SRS 43 Q10) |
| PAU-001 | ESC pauses and stops game time | `Core/Services/PauseManager.cs`, the only writer of `Time.timeScale`, which also applies hit stop so the two can never fight. Pause outranks hit stop in both directions | **Done** — `Test_HitStop_DoesNotCancelPause`, `Test_Pause_DuringHitStop_RestoresToOneOnResume` |
| PAU-002 | Resume / Settings / How to Play / Abandon / Quit | `Scripts/UI/PauseMenuController.cs` (5 button fields) | Skeleton |
| PAU-003 | Abandon and Quit need confirmation | `PauseMenuController._confirmationPanel` | Skeleton |
| PAU-004 | Abandon still passes through Post-Run | `RunManager.AbandonRun` TODO | Skeleton |
| PAU-005 | ESC cannot close the Level Up panel | `LevelUpPanel.IsAwaitingChoice`, `PauseReason.LevelUpSelection` (stacked reasons) | Mechanism **Done**, panel Skeleton |

## 20–22 Camera, Juice, Audio

| ID | Requirement | Where | Status |
|---|---|---|---|
| CAM-001 | Camera follows the hero stably | `Gameplay/Camera/CameraRig.SetFollowTarget` / `ApplyConfig` (Cinemachine 3: `CinemachineCamera.Target.TrackingTarget` + `CinemachinePositionComposer` damping and lookahead), `Data/CameraConfig.cs` (`DampingX` 0.3, `DampingY` 0.5, `Lookahead` 0.2), `CM_Follow` in `Scenes/Run_01.unity` | **Done** — smoothness accepted by playtest, not by a test |
| CAM-002 | Camera stays inside level bounds | `CameraRig` (`CinemachineConfiner2D` + `InvalidateConfinerCache`), `CameraConfiner` `PolygonCollider2D` (-20,0)-(20,12) in `Scenes/Run_01.unity` | **Done** |
| CAM-003 | Configurable screen shake | `Feel/ScreenShakeService.cs` picks the weight from `BalanceConfig.Shake` and publishes `ScreenShakeRequestedEvent`; `CameraRig` subscribes and fires a `CinemachineImpulseSource` on the hero. The `CinemachineImpulseListener` is ordered **before** `CinemachineConfiner2D`, or a heavy hit shakes the view past the arena edge | **Done** — `Test_Shake_DoesNotEscapeConfiner` |
| CAM-004 | Zoom for big events | `CameraRig.SetZoom` | Skeleton |
| SRS 21 | Hit stop, flash, damage numbers, particles, trails | `Feel/HitStopService.cs` (light / heavy / crit / kill, applied by `PauseManager` which is the one owner of `Time.timeScale`), `Feel/SpriteFeedback.cs` (white flash via `MaterialPropertyBlock`, no material clones), `Feel/ImpactParticles.cs`, `Feel/DashTrail.cs`, `UI/DamageNumber.cs` | **Done** — `Test_HitStop_RestoresTimeScale`, `Test_HitStop_TakesLongerDurationNotSum`, `Test_HitStop_DoesNotCancelPause`, `Test_Pause_DuringHitStop_RestoresToOneOnResume`, `Test_Flash_DoesNotLeakMaterials` |
| SRS 22 | BGM and SFX buses | `AudioManager.PlayOneShot` with a 16-voice pool, `Data/SfxLibrary.cs` (nine cues), `Feel/SfxPlayer.cs` joining the two because Core cannot reference Data. A missing clip is silent and warns once | Wiring **Done** — `Test_Sfx_NullClipDoesNotThrow`; **no audio files exist yet**, see README section 13 |

## 23 Data & Save

| ID | Requirement | Where | Status |
|---|---|---|---|
| SRS 23 | HeroData / SkillData / UpgradeData / EnemyData / WaveData / StageData | `Scripts/Data/*.cs` — 9 types, 9 baseline assets in `Data/` | **Done** |
| SRS 23 | MetaSave | `Save/MetaSave.cs` | **Done** |
| SRS 23 | RunState, runtime-only | `Gameplay/Run/RunState.cs` — never serialised. See OI-13 | **Done** |
| SAVE-001 | Save MetaProgression | `Save/SaveManager.Save` | **Done** |
| SAVE-002 | Save settings | `SettingsSave` inside `MetaSave`, `Save/SettingsManager.cs` | **Done** |
| SAVE-003 | Never overwrite with a bad state | `SaveManager.Save` validates then writes temp → `File.Replace` | **Done** |
| SAVE-004 | Fallback when the save is corrupt | `SaveManager.Load`: main → `meta_save.backup.json` → defaults | **Done** |
| SAVE-005 | Reset only on confirmation | `SaveManager.ResetToDefaults` (caller owns the prompt) | Backend **Done**, prompt Skeleton |
| SAVE-006 | No mid-run save, no Continue in Main Menu | `MainMenuController` has exactly 4 buttons, no Continue field exists | **Done** by construction |
| SAVE-007 | Mid-run save is post-MVP | — | **Chưa triển khai** — deliberately out of scope (Could) |
| TEL-001 | Log each Run | `Telemetry/TelemetryRecords.cs` `RunTelemetryRecord`, emitted from `RunManager.EndRun` TODO | Record **Done**, emit Skeleton |
| TEL-002 | Log the 3 cards offered and the pick | `LevelUpTelemetryRecord`, `UpgradeSystem.SelectUpgrade` TODO | Record Done, emit Skeleton |
| TEL-003 | Log damage per source and cause of death | `DamageTelemetryRecord`, `DamageSource` enum carried through `DamageResult` | Record + plumbing **Done**, aggregation Skeleton |
| TEL-004 | Local file, no network, toggleable | `Telemetry/TelemetryService.cs` — JSON Lines, `IsEnabled`, no network API referenced | **Done** |
| TEL-005 | Batched, no frame cost | `TelemetryService` buffers in memory, `Flush()` at Run end | **Done** |

## 24 RNG

| ID | Requirement | Where | Status |
|---|---|---|---|
| RNG-001 | Weight/rarity on the upgrade pool | `UpgradeData.Weight`, `BalanceConfig.RarityWeights` | **Done** |
| RNG-002 | Never draw a banned item | `UpgradeRoller` skips zero-weight and capped entries | **Done** — `ZeroWeightUpgradesAreNeverOffered` |
| RNG-003 | Seed fixable for debug | `Core/Utilities/DeterministicRandom.cs`, `RunState.Seed` | **Done** — `SameSeedProducesTheSameOffer` |
| RNG-004 | RNG must not make damage untestable | Crit rolled outside `DamageCalculator.Calculate`, which is pure | **Done** — `RollCritical_IsDeterministicForAGivenSeed` |
| RNG-005 | Fallback Pool exempt from the duplicate rule | `UpgradeRoller` second loop draws with replacement | **Done** — `FallbackEntriesMayRepeatWithinOneRoll` |

## 26–27 Architecture and Scenes

| Module (SRS 26) | Where | Status |
|---|---|---|
| GameBootstrap / GameManager | `Core/Bootstrap/GameBootstrap.cs`, `GameManager.cs` | **Done** / Skeleton |
| SceneFlowManager | `Core/Services/SceneFlowManager.cs` | **Done** |
| InputManager (SRS 26) | `Core/Services/InputReader.cs` + `Settings/ChibiRiftControls.inputactions` | **Done** — Move and Jump wired; the other seven actions are bound and exposed but not yet consumed |
| PlayerController / PlayerCombat / PlayerStats | `Gameplay/Player/` | `PlayerController`, `PlayerMotor`, `PlayerCombat` **Done**; `PlayerStats` **Done** (stats only, health moved to `HealthComponent`) |
| CombatSystem / DamageSystem / StatusEffectSystem | `Gameplay/Combat/` | `DamageCalculator`, `CombatSystem`, `HealthComponent` **Done**; `StatusEffectSystem` Skeleton |
| SkillSystem / UpgradeSystem | `Gameplay/Skills/`, `Gameplay/Progression/` | `SkillSystem` **Done** (COM-007, COM-008); `UpgradeRoller` **Done**; `UpgradeSystem` Skeleton (P2) |
| EnemyController / EnemyAI / EnemySpawner | `Gameplay/Enemy/` | `EnemyAI`, `EnemyMotor`, `EnemyAttack`, `EnemyController`, `EnemySpawner` **Done** (melee only) |
| WaveManager / StageManager / BossManager | `Gameplay/Wave|Stage|Boss/` | Skeleton |
| RunManager / RewardManager | `Gameplay/Run/` | Skeleton |
| MetaProgressionManager / CurrencyManager | `Meta/` | `CurrencyManager` **Done** |
| SaveManager / SettingsManager | `Save/` | **Done** |
| PauseManager | `Core/Services/PauseManager.cs` | **Done** |
| TelemetryManager | `Telemetry/TelemetryService.cs` | **Done** |
| UIManager / AudioManager / VFXManager | `UI/UiManager.cs`, `Core/Services/AudioManager.cs`, `VfxManager.cs` | Skeleton |
| **Gameplay must not reference UI** | Enforced by the assembly graph — `ChibiRift.UI` is referenced by nobody | **Done** |
| SRS 27 | Boot, MainMenu, Hub, Run_01, PostRun | `Scenes/*.unity`, registered in Build Settings, Boot at index 0 | **Done** |

## 28 Non-Functional

| ID | Requirement | Where | Status |
|---|---|---|---|
| NFR-001 | ≥ 60 FPS avg, ≥ 50 FPS 1% low @1080p | `Gameplay/Profiling/FrameTimeHarness.cs` spawns 30 enemies and reports mean FPS and the 1% low; protected by `ObjectPool<T>`, `EnemyAI.ThinkInterval`, the sweep and voice budgets in `BalanceConfig` | Harness **Done** — `Test_Profiler_HarnessProducesCompleteReport`; **the measurement itself needs an editor run on real hardware**, see README section 14 and OI-15 |
| NFR-002 | p99 frame time ≤ 33 ms, no frame > 100 ms | The same harness reports p95, p99, max, the count of frames over 33 ms and heap growth (`GC.GetTotalMemory`, paired with `GC.CollectionCount` so a mid-window collection is visible rather than silently undercounting); protected by batched telemetry, pool prewarm and bounded hit stop | Harness **Done**; frame-time measurement pending an editor run. Allocation is regression-guarded now: `BalanceConfig.StressAllocationBudgetKilobytes` + `Test_Profiler_AllocationStaysUnderBudget`. The p99 **excludes** area-skill cost, because a 15s ultimate cooldown does not fit a 10s sample — stated in the report file itself. See OI-32 for the investigation that found and fixed two harness bugs and a broken allocation API |
| NFR-003 | No crash in a happy-path Run | — | **Chưa đo được** — no Run yet |
| NFR-004 | Scene load ≤ 5 s, launch ≤ 8 s | `SceneFlowManager` async load + `LoadProgressChanged` | Infrastructure **Done**, chưa đo được |
| NFR-005 | New player understands the controls | `Scripts/UI/HowToPlayPanel.cs` | Skeleton |
| NFR-006 | Volume controls and readable UI | `IAudioService`, `SettingsSave` | Backend **Done** |
| NFR-007 | New content added via data/prefab | 9 ScriptableObject types; no content enumerated in code | **Done** |
| NFR-008 | Damage/XP/RNG independently testable | All three are `static` and pure; 183 EditMode + 68 PlayMode tests | **Done** |
| NFR-009 | Basic save validation | `MetaSave.IsValid()`, checked before every write and after every read | **Done** |

## 30 Error Handling

| Scenario | Where | Status |
|---|---|---|
| Save unreadable | `SaveManager.Load` → backup → defaults | **Done** |
| Missing ScriptableObject reference | `GameDataAsset.OnValidate`, `Core/Utilities/GameLog.cs` | **Done** |
| Invalid upgrade pool (< 3 valid) | `UpgradeRoller` tops up from fallback, else logs an error | **Done** |
| Enemy spawn failure | `EnemySpawner.Spawn` TODO, `WaveManager.Tick` timeout TODO | Skeleton |
| Scene transition failure | `SceneFlowManager.LoadRoutine` (null op check, re-entrancy guard) | **Done** |

## 33 User Stories

| ID | Covered by |
|---|---|
| US-001 | MOV-001 |
| US-002 | MOV-002, MOV-003 |
| US-003 | COM-002, COM-003 |
| US-004 | COM-007, COM-008 |
| US-005 | EXP-001, EXP-002, EXP-003 |
| US-006 | EXP-005, EXP-006, EXP-009 |
| US-007 | SRS 13 archetypes, `EnemyData.Archetype` |
| US-008 | BOS-002, BOS-003 |
| US-009 | RUN-006, SRS 16 |
| US-010 | META-001..006 |
| US-011 | SAVE-001, SAVE-002 |
| US-012 | MOV-006, HPS-005 |
| US-013 | PAU-004, RUN-008 |
| US-014 | TEL-002, TEL-003 |

## 35 Balance Framework

Every value below is copied verbatim from SRS 35 into `Data/BalanceConfig.asset`, and asserted by
`DamageCalculatorTests.BalanceConfigDefaultsMatchTheSrsBaseline`.

| SRS 35 parameter | Value | Field |
|---|---|---|
| Player Base HP | 100 | `PlayerBaseHealth` |
| Player Base Attack | 10 | `PlayerBaseAttack` |
| Crit Chance | 5% | `PlayerBaseCritChance` |
| Crit Multiplier | 2.0 | `DefaultCritMultiplier` |
| Combo Window | 0.5 s (**OI-04**) | `DefaultComboWindow` |
| Skill Cooldown | per-skill | `SkillData.Cooldown` |
| XP Growth | 100 / 1.4 | `BaseExperience`, `ExperienceGrowthFactor` |
| Enemy HP Scaling | per stage | `StageData.EnemyHealthMultiplier` |
| Enemy Damage Scaling | per stage | `StageData.EnemyDamageMultiplier` |
| Upgrade Weight | per rarity | `RarityWeights` |
| Dash Cooldown | 1.5 s | `DashCooldown` |
| Dash I-Frame Duration | 0.25 s | `DashIFrameDuration` |
| Player Base Defense | 0 / 0% | `PlayerBaseDefense`, `PlayerBaseDamageReduction` |
| Enemy Defense | per `EnemyData`, DR ≤ 0.8 | `EnemyData.BaseStats`, `MaxDamageReduction` |
| MinDamage | 1 | `MinDamage` |
| Elite HP / Damage | ×3 / ×1.5 | `EliteHealthMultiplier`, `EliteDamageMultiplier` |
| Upgrade Pool Size | ≥ 12 + Fallback | **Chưa triển khai**, see **OI-09** |

---

## Summary of what is deliberately not built

| Area | SRS reference | Reason |
|---|---|---|
| Animation, art, VFX, SFX | SRS 21, 22, HER-004 | Out of skeleton scope |
| Enemy AI behaviour, boss patterns | AI-001..006, BOS-003..005 | P1/P2 gameplay work |
| Level design for Run_01 | SRS 14 | P2 |
| 12+ upgrade pool, Fallback Pool content | UPG-008, UPG-009 | P2 content, OI-09 |
| Remaining 2 elite modifier assets | ELT-002 | P2 content |
| `MetaUpgradeData` asset type + cost curve | META-004 | Not in the 9 types the work order listed |
| Key rebind, localization, controller | SRS 19.4, SRS 43 Q9/Q10 | Explicitly out of MVP |
| Multiplayer, cloud save, mid-run save | SRS 3.2, SAVE-007 | Explicitly out of MVP |
