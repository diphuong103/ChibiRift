using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using ChibiRift.Data;

namespace ChibiRift.EditorTools
{
    /// <summary>
    /// Creates one baseline asset per content type (SRS 23), with the values of SRS 35 copied
    /// verbatim. Most numbers already come from the field initialisers in ChibiRift.Data, so
    /// this only fills in identity and the cross-references between assets.
    /// </summary>
    public static class SampleDataGenerator
    {
        /// <summary>Where the shipped assets live; the target of the parameterless <see cref="Generate()"/>.</summary>
        public const string DefaultDataRoot = "Assets/_Project/Data";

        /// <summary>Generates the baseline assets. Safe to re-run: existing assets are replaced.</summary>
        [MenuItem("ChibiRift/Setup/3. Generate Baseline Data Assets")]
        public static void Generate() => Generate(DefaultDataRoot);

        /// <summary>
        /// Generates the baseline assets into <paramref name="dataRoot"/>.
        /// The parameter exists so <c>DataDefaultsConsistencyTests</c> can generate into a scratch
        /// folder and diff the result against the confirmed values without disturbing the shipped
        /// assets. It must stay under <c>Assets/</c>: <c>AssetDatabase.CreateAsset</c> refuses any
        /// path outside the project's asset tree.
        /// </summary>
        public static void Generate(string dataRoot)
        {
            if (string.IsNullOrEmpty(dataRoot))
                throw new ArgumentException("dataRoot must be a path under Assets/.", nameof(dataRoot));

            Directory.CreateDirectory(dataRoot);

            // SRS 35 lives here. Every default already matches the table, so nothing is overridden.
            BalanceConfig balance = Create<BalanceConfig>(
                "BalanceConfig", "balance.default", "Default Balance",
                "Baseline balance from SRS section 35. Values SRS marks Configurable were fixed by the project owner; see OPEN_ISSUES.md.",
                so =>
                {
                    // UPG-004 / RNG-001: rarer upgrades are drawn less often.
                    SerializedProperty weights = so.FindProperty("_rarityWeights");
                    weights.arraySize = 5;
                    SetRarityWeight(weights, 0, Rarity.Common, 100f);
                    SetRarityWeight(weights, 1, Rarity.Uncommon, 60f);
                    SetRarityWeight(weights, 2, Rarity.Rare, 30f);
                    SetRarityWeight(weights, 3, Rarity.Epic, 12f);
                    SetRarityWeight(weights, 4, Rarity.Legendary, 4f);
                });

            // CAM-001 / CAM-002: framing values, deliberately not on HeroData.
            Create<CameraConfig>(
                "CameraConfig", "camera.default", "Default Camera",
                "Cinemachine damping and lookahead for the gameplay camera (CAM-001).");

            // COM-007: three skills, two behaviours. Q is a projectile; E and R are the same area
            // behaviour with different radius, damage and cooldown. All three scale off the hero's
            // Attack through DamageMultiplier, so BaseDamage stays 0 (see SkillData).
            SkillData skill = Create<SkillData>(
                "SKL_Q_Fireball", "skill.fireball", "Fireball",
                "Launches a fireball toward the cursor. Cooldown only, no resource cost (COM-008).",
                so =>
                {
                    so.FindProperty("_slot").enumValueIndex = (int)Core.SkillSlot.Skill1;
                    so.FindProperty("_type").enumValueIndex = (int)SkillType.Projectile;
                    so.FindProperty("_baseDamage").floatValue = 0f;
                    so.FindProperty("_damageMultiplier").floatValue = 1.5f;
                    so.FindProperty("_cooldown").floatValue = 3f;
                    so.FindProperty("_projectileSpeed").floatValue = 12f;
                    so.FindProperty("_projectileLifetime").floatValue = 2f;
                    so.FindProperty("_projectileRadius").floatValue = 0.25f;
                });

            SkillData skillE = Create<SkillData>(
                "SKL_E_Shockwave", "skill.shockwave", "Shockwave",
                "Damages everything close to the hero. Cooldown only, no resource cost (COM-008).",
                so =>
                {
                    so.FindProperty("_slot").enumValueIndex = (int)Core.SkillSlot.Skill2;
                    so.FindProperty("_type").enumValueIndex = (int)SkillType.AreaOfEffect;
                    so.FindProperty("_baseDamage").floatValue = 0f;
                    so.FindProperty("_damageMultiplier").floatValue = 2f;
                    so.FindProperty("_cooldown").floatValue = 6f;
                    so.FindProperty("_radius").floatValue = 2.5f;
                    so.FindProperty("_windup").floatValue = 0.2f;
                });

            SkillData skillR = Create<SkillData>(
                "SKL_R_Cataclysm", "skill.cataclysm", "Cataclysm",
                "The ultimate: a wide blast. One cast kills a full-health melee grunt (COM-007).",
                so =>
                {
                    so.FindProperty("_slot").enumValueIndex = (int)Core.SkillSlot.Ultimate;
                    so.FindProperty("_type").enumValueIndex = (int)SkillType.AreaOfEffect;
                    so.FindProperty("_baseDamage").floatValue = 0f;
                    so.FindProperty("_damageMultiplier").floatValue = 4f;
                    so.FindProperty("_cooldown").floatValue = 15f;
                    so.FindProperty("_radius").floatValue = 3.5f;
                    so.FindProperty("_windup").floatValue = 0.35f;
                });

            UpgradeData upgrade = Create<UpgradeData>(
                "UPG_AttackUp", "upgrade.attack_up", "Sharpened Claws",
                "Increases Attack. Stackable up to 5 times (UPG-005, UPG-010).",
                so =>
                {
                    so.FindProperty("_category").enumValueIndex = (int)UpgradeCategory.Stat;
                    so.FindProperty("_rarity").enumValueIndex = (int)Rarity.Common;
                    so.FindProperty("_targetStat").enumValueIndex = (int)StatType.Attack;
                    so.FindProperty("_flatAmount").floatValue = 3f;
                });

            EliteModifierData elite = Create<EliteModifierData>(
                "ELT_Enraged", "elite.enraged", "Enraged",
                "Gains speed and damage at or below 50% HP (ELT-002).",
                so =>
                {
                    so.FindProperty("_modifier").enumValueIndex = (int)EliteModifierType.Enraged;
                    so.FindProperty("_enrageHealthThreshold").floatValue = 0.5f; // ELT-002
                    so.FindProperty("_enrageSpeedMultiplier").floatValue = 1.5f;
                    so.FindProperty("_enrageDamageMultiplier").floatValue = 1.5f;
                    so.FindProperty("_auraTint").colorValue = new Color(1f, 0.35f, 0.25f, 1f);
                });

            // Renamed from ENM_Melee in slice 3 so the asset and ENM_MeleeGrunt.prefab match.
            // The id is unchanged, so save files and telemetry are unaffected.
            EnemyData enemyData = Create<EnemyData>(
                "ENM_MeleeGrunt", "enemy.melee_grunt", "Melee Grunt",
                "Closes on the hero and attacks in melee (SRS 13). The only archetype in P1.",
                so =>
                {
                    so.FindProperty("_archetype").enumValueIndex = (int)EnemyArchetype.Melee;
                    SerializedProperty stats = so.FindProperty("_baseStats");
                    stats.FindPropertyRelative("MaxHealth").floatValue = 40f;
                    stats.FindPropertyRelative("Attack").floatValue = 8f;
                    stats.FindPropertyRelative("MoveSpeed").floatValue = 3f;
                    stats.FindPropertyRelative("Defense").floatValue = 0f;
                    stats.FindPropertyRelative("DamageReduction").floatValue = 0f;

                    SerializedProperty modifiers = so.FindProperty("_eliteModifiers");
                    modifiers.arraySize = 1;
                    modifiers.GetArrayElementAtIndex(0).objectReferenceValue = elite;

                    // SRS 35: elite reward multiplier follows the x3 HP figure.
                    so.FindProperty("_eliteRewardMultiplier").floatValue = 3f;

                    // AI-002: gain aggro at 8, lose it at 12. The gap is hysteresis, so a hero
                    // standing on the boundary does not flip the state every evaluation.
                    so.FindProperty("_detectionRange").floatValue = 8f;
                    so.FindProperty("_loseAggroRange").floatValue = 12f;
                    so.FindProperty("_attackRange").floatValue = 1.2f;
                    so.FindProperty("_hurtStunDuration").floatValue = 0.2f;
                    so.FindProperty("_corpseLingerSeconds").floatValue = 1f;

                    // AI-004: three phases in seconds; see OI-18 on why not Animation Events.
                    SerializedProperty attack = so.FindProperty("_attack");
                    EnemyAttackConfig melee = EnemyAttackConfig.MeleeBaseline;
                    attack.FindPropertyRelative("Windup").floatValue = melee.Windup;
                    attack.FindPropertyRelative("Active").floatValue = melee.Active;
                    attack.FindPropertyRelative("Recovery").floatValue = melee.Recovery;
                    attack.FindPropertyRelative("Cooldown").floatValue = melee.Cooldown;
                    attack.FindPropertyRelative("HitboxWidth").floatValue = melee.HitboxWidth;
                    attack.FindPropertyRelative("HitboxHeight").floatValue = melee.HitboxHeight;
                    attack.FindPropertyRelative("HitboxOffsetDistance").floatValue = melee.HitboxOffsetDistance;

                    // Gravity and the ground probe, mirroring the hero so both fall alike.
                    SerializedProperty physics = so.FindProperty("_physics");
                    EnemyPhysicsConfig body = EnemyPhysicsConfig.MeleeBaseline;
                    physics.FindPropertyRelative("Gravity").floatValue = body.Gravity;
                    physics.FindPropertyRelative("MaxFallSpeed").floatValue = body.MaxFallSpeed;
                    physics.FindPropertyRelative("GroundCheckWidth").floatValue = body.GroundCheckWidth;
                    physics.FindPropertyRelative("GroundCheckHeight").floatValue = body.GroundCheckHeight;
                    physics.FindPropertyRelative("GroundCheckOffsetY").floatValue = body.GroundCheckOffsetY;

                    so.FindProperty("_spawnArrivalTolerance").floatValue = 0.15f;
                    so.FindProperty("_evadeDuration").floatValue = 0.35f;

                    // AI-005.
                    so.FindProperty("_stuckCheckWindow").floatValue = 0.5f;
                    so.FindProperty("_stuckMinDisplacement").floatValue = 0.1f;
                    so.FindProperty("_separationRadius").floatValue = 0.6f;
                    so.FindProperty("_separationForce").floatValue = 2f;
                });

            // COM-001..COM-004: frame data in seconds because P1 has no animation clips to hang
            // Animation Events on (OI-18). Values confirmed by the project owner for slice 2.
            AttackData basicAttack = Create<AttackData>(
                "ATK_KnightBasic", "attack.knight.basic", "Knight Basic Chain",
                "Three hit basic chain. Active windows are in seconds from swing start (OI-18).",
                so =>
                {
                    SerializedProperty steps = so.FindProperty("_steps");
                    AttackStep[] baseline = AttackData.BaselineSteps();
                    steps.arraySize = baseline.Length;

                    for (int i = 0; i < baseline.Length; i++)
                    {
                        SerializedProperty entry = steps.GetArrayElementAtIndex(i);
                        entry.FindPropertyRelative("TotalDuration").floatValue = baseline[i].TotalDuration;
                        entry.FindPropertyRelative("ActiveStartTime").floatValue = baseline[i].ActiveStartTime;
                        entry.FindPropertyRelative("ActiveEndTime").floatValue = baseline[i].ActiveEndTime;
                        entry.FindPropertyRelative("DamageMultiplier").floatValue = baseline[i].DamageMultiplier;
                    }
                });

            HeroData hero = Create<HeroData>(
                "HERO_Knight", "hero.knight", "Knight",
                "The MVP hero. Melee, 3 hit combo, dash with i-frames (SRS 12).",
                so =>
                {
                    // Stats and dash already default to the SRS 35 baseline; only the loadout is wired.
                    SerializedProperty skills = so.FindProperty("_skills");
                    skills.arraySize = 3;
                    skills.GetArrayElementAtIndex(0).objectReferenceValue = skill;
                    skills.GetArrayElementAtIndex(1).objectReferenceValue = skillE;
                    skills.GetArrayElementAtIndex(2).objectReferenceValue = skillR;

                    so.FindProperty("_basicAttack").objectReferenceValue = basicAttack;
                });

            // P1 slice 2 needs something to hit before enemy AI exists in slice 3. Two variants:
            // one that survives any amount of testing, one that dies quickly so death, the corpse
            // timer and the once-only EntityDiedEvent can be exercised.
            Create<EnemyData>(
                "ENM_TrainingDummy", "enemy.training_dummy", "Training Dummy",
                "Inert target for combat testing. No AI, no movement, no retaliation.",
                so =>
                {
                    SerializedProperty stats = so.FindProperty("_baseStats");
                    stats.FindPropertyRelative("MaxHealth").floatValue = 9999f;
                    stats.FindPropertyRelative("Attack").floatValue = 0f;
                    stats.FindPropertyRelative("MoveSpeed").floatValue = 0f;
                    stats.FindPropertyRelative("Defense").floatValue = 0f;
                    stats.FindPropertyRelative("DamageReduction").floatValue = 0f;
                    so.FindProperty("_experienceReward").floatValue = 0f;
                    so.FindProperty("_goldReward").intValue = 0;
                });

            Create<EnemyData>(
                "ENM_TrainingDummyFragile", "enemy.training_dummy_fragile", "Fragile Training Dummy",
                "Low HP target so death, the corpse timer and EntityDiedEvent can be exercised.",
                so =>
                {
                    SerializedProperty stats = so.FindProperty("_baseStats");
                    stats.FindPropertyRelative("MaxHealth").floatValue = 30f;
                    stats.FindPropertyRelative("Attack").floatValue = 0f;
                    stats.FindPropertyRelative("MoveSpeed").floatValue = 0f;
                    stats.FindPropertyRelative("Defense").floatValue = 0f;
                    stats.FindPropertyRelative("DamageReduction").floatValue = 0f;
                    so.FindProperty("_experienceReward").floatValue = 0f;
                    so.FindProperty("_goldReward").intValue = 0;
                });

            // Stage 1's five waves (P2 slice 1): 3, 5, 7, 10 melee, then wave 5's single elite
            // placeholder alongside 4 more melee. All AllEnemiesDefeated — Stage 1 has no
            // duration- or kill-quota wave. One local helper rather than five near-identical
            // Create<WaveData> calls: the only thing that varies between waves is composition.
            WaveData BuildMeleeWave(string fileName, string id, string displayName, int count)
                => Create<WaveData>(fileName, id, displayName,
                    $"{count} melee enemies (SRS 14, Stage 1).",
                    so =>
                    {
                        SerializedProperty entries = so.FindProperty("_entries");
                        entries.arraySize = 1;

                        SerializedProperty entry = entries.GetArrayElementAtIndex(0);
                        entry.FindPropertyRelative("Enemy").objectReferenceValue = enemyData;
                        entry.FindPropertyRelative("Count").intValue = count;
                        entry.FindPropertyRelative("SpawnAsElite").boolValue = false;
                        entry.FindPropertyRelative("StartDelay").floatValue = 0f;

                        so.FindProperty("_clearCondition").enumValueIndex = (int)WaveClearCondition.AllEnemiesDefeated;
                    });

            WaveData wave1 = BuildMeleeWave("WAV_Stage1_Wave1", "wave.stage1.wave1", "Stage 1 - Wave 1", 3);
            WaveData wave2 = BuildMeleeWave("WAV_Stage1_Wave2", "wave.stage1.wave2", "Stage 1 - Wave 2", 5);
            WaveData wave3 = BuildMeleeWave("WAV_Stage1_Wave3", "wave.stage1.wave3", "Stage 1 - Wave 3", 7);
            WaveData wave4 = BuildMeleeWave("WAV_Stage1_Wave4", "wave.stage1.wave4", "Stage 1 - Wave 4", 10);

            // Wave 5: one elite placeholder (ELT-001 stat scaling only, no visual tell yet — a real
            // boss is a later slice) plus 4 more melee.
            WaveData wave5 = Create<WaveData>(
                "WAV_Stage1_Wave5", "wave.stage1.wave5", "Stage 1 - Wave 5",
                "One elite melee plus four regular melee (SRS 14, Stage 1, ELT-001 placeholder).",
                so =>
                {
                    SerializedProperty entries = so.FindProperty("_entries");
                    entries.arraySize = 2;

                    SerializedProperty elite = entries.GetArrayElementAtIndex(0);
                    elite.FindPropertyRelative("Enemy").objectReferenceValue = enemyData;
                    elite.FindPropertyRelative("Count").intValue = 1;
                    elite.FindPropertyRelative("SpawnAsElite").boolValue = true;
                    elite.FindPropertyRelative("StartDelay").floatValue = 0f;

                    SerializedProperty regular = entries.GetArrayElementAtIndex(1);
                    regular.FindPropertyRelative("Enemy").objectReferenceValue = enemyData;
                    regular.FindPropertyRelative("Count").intValue = 4;
                    regular.FindPropertyRelative("SpawnAsElite").boolValue = false;
                    regular.FindPropertyRelative("StartDelay").floatValue = 0f;

                    so.FindProperty("_clearCondition").enumValueIndex = (int)WaveClearCondition.AllEnemiesDefeated;
                });

            BossData boss = Create<BossData>(
                "BOSS_Stage1", "boss.stage1", "Stage 1 Boss",
                "Two phase boss. Phase 2 begins at or below 70% HP (SRS 15, BOS-002).",
                so =>
                {
                    SerializedProperty phases = so.FindProperty("_phases");
                    phases.arraySize = 2; // BOS-002: at least 2 phases in MVP

                    SerializedProperty phaseOne = phases.GetArrayElementAtIndex(0);
                    phaseOne.FindPropertyRelative("HealthThreshold").floatValue = 1f;
                    phaseOne.FindPropertyRelative("DamageMultiplier").floatValue = 1f;
                    phaseOne.FindPropertyRelative("SpeedMultiplier").floatValue = 1f;
                    phaseOne.FindPropertyRelative("TelegraphDuration").floatValue = 0.8f;

                    SerializedProperty phaseTwo = phases.GetArrayElementAtIndex(1);
                    phaseTwo.FindPropertyRelative("HealthThreshold").floatValue = 0.7f; // SRS 15
                    phaseTwo.FindPropertyRelative("DamageMultiplier").floatValue = 1.3f;
                    phaseTwo.FindPropertyRelative("SpeedMultiplier").floatValue = 1.2f;
                    phaseTwo.FindPropertyRelative("TelegraphDuration").floatValue = 0.6f;

                    SerializedProperty stats = so.FindProperty("_baseStats");
                    stats.FindPropertyRelative("MaxHealth").floatValue = 800f;
                    stats.FindPropertyRelative("Attack").floatValue = 12f;
                });

            // P1-31: the cue list. Every clip field is empty on purpose — no audio ships yet and
            // the wiring is complete and silent until files are dropped in (see README).
            Create<SfxLibrary>(
                "SFX_Default", "sfx.default", "Default SFX",
                "Nine cue slots for P1. Empty until the audio files exist; a missing clip is silent, not an error.");

            Create<StageData>(
                "STG_Stage1", "stage.01", "Stage 1",
                "The MVP stage: five waves then a two phase boss (SRS 14, STG-003).",
                so =>
                {
                    SerializedProperty waves = so.FindProperty("_waves");
                    waves.arraySize = 5;
                    waves.GetArrayElementAtIndex(0).objectReferenceValue = wave1;
                    waves.GetArrayElementAtIndex(1).objectReferenceValue = wave2;
                    waves.GetArrayElementAtIndex(2).objectReferenceValue = wave3;
                    waves.GetArrayElementAtIndex(3).objectReferenceValue = wave4;
                    waves.GetArrayElementAtIndex(4).objectReferenceValue = wave5;
                    so.FindProperty("_boss").objectReferenceValue = boss;

                    // WAV-001: enemies spawn away from the hero's own spawn point and clear of the
                    // hole at x=-8 (RunSceneBuilder.HoleCentreX) — not hard-coded in WaveManager.
                    so.FindProperty("_enemySpawnPoint").vector2Value = new Vector2(14f, 1f);
                });

            // Silences the unused-local warnings for assets referenced only by others.
            if (balance == null || hero == null || upgrade == null || basicAttack == null)
                Debug.LogError("[Setup] Asset creation returned null.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Setup] Baseline data assets generated in {dataRoot}.");

            // Local rather than static: it needs dataRoot, and nothing outside Generate creates assets.
            T Create<T>(
                string fileName,
                string id,
                string displayName,
                string description,
                Action<SerializedObject> configure = null) where T : GameDataAsset
            {
                string path = $"{dataRoot}/{fileName}.asset";
                AssetDatabase.DeleteAsset(path);

                var asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);

                var so = new SerializedObject(asset);
                so.FindProperty("_id").stringValue = id;
                so.FindProperty("_displayName").stringValue = displayName;
                so.FindProperty("_description").stringValue = description;
                configure?.Invoke(so);
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(asset);
                return asset;
            }
        }

        private static void SetRarityWeight(SerializedProperty array, int index, Rarity rarity, float multiplier)
        {
            SerializedProperty element = array.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("Rarity").enumValueIndex = (int)rarity;
            element.FindPropertyRelative("WeightMultiplier").floatValue = multiplier;
        }

    }
}
