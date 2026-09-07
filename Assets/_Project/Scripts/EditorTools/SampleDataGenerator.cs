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
        private const string DataRoot = "Assets/_Project/Data";

        /// <summary>Generates the nine baseline assets. Safe to re-run: existing assets are replaced.</summary>
        [MenuItem("ChibiRift/Setup/3. Generate Baseline Data Assets")]
        public static void Generate()
        {
            Directory.CreateDirectory(DataRoot);

            // SRS 35 lives here. Every default already matches the table, so nothing is overridden.
            BalanceConfig balance = Create<BalanceConfig>(
                "BalanceConfig", "balance.default", "Default Balance",
                "Baseline balance from SRS section 35. Values marked Configurable there are provisional; see OPEN_ISSUES.md.",
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

            SkillData skill = Create<SkillData>(
                "SKL_Fireball", "skill.fireball", "Fireball",
                "Launches a fireball toward the cursor. Cooldown only, no resource cost (COM-008).",
                so =>
                {
                    so.FindProperty("_slot").enumValueIndex = (int)Core.SkillSlot.Skill1;
                    so.FindProperty("_type").enumValueIndex = (int)SkillType.Projectile;
                    so.FindProperty("_baseDamage").floatValue = 15f;
                    so.FindProperty("_cooldown").floatValue = 5f;
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

            EnemyData enemyData = Create<EnemyData>(
                "ENM_Melee", "enemy.melee_grunt", "Melee Grunt",
                "Closes on the hero and attacks in melee (SRS 13).",
                so =>
                {
                    so.FindProperty("_archetype").enumValueIndex = (int)EnemyArchetype.Melee;
                    SerializedProperty stats = so.FindProperty("_baseStats");
                    stats.FindPropertyRelative("MaxHealth").floatValue = 30f;
                    stats.FindPropertyRelative("Attack").floatValue = 5f;
                    stats.FindPropertyRelative("MoveSpeed").floatValue = 3f;
                    stats.FindPropertyRelative("Defense").floatValue = 0f;
                    stats.FindPropertyRelative("DamageReduction").floatValue = 0f;

                    SerializedProperty modifiers = so.FindProperty("_eliteModifiers");
                    modifiers.arraySize = 1;
                    modifiers.GetArrayElementAtIndex(0).objectReferenceValue = elite;

                    // SRS 35: elite reward multiplier follows the x3 HP figure.
                    so.FindProperty("_eliteRewardMultiplier").floatValue = 3f;
                });

            HeroData hero = Create<HeroData>(
                "HERO_Knight", "hero.knight", "Knight",
                "The MVP hero. Melee, 3 hit combo, dash with i-frames (SRS 12).",
                so =>
                {
                    // Stats and dash already default to the SRS 35 baseline; only the loadout is wired.
                    SerializedProperty skills = so.FindProperty("_skills");
                    skills.arraySize = 1;
                    skills.GetArrayElementAtIndex(0).objectReferenceValue = skill;
                });

            WaveData wave = Create<WaveData>(
                "WAV_Stage1_Wave1", "wave.stage1.wave1", "Stage 1 - Wave 1",
                "Five melee enemies, matching the SRS 14 example for Stage 1.",
                so =>
                {
                    SerializedProperty entries = so.FindProperty("_entries");
                    entries.arraySize = 1;

                    SerializedProperty entry = entries.GetArrayElementAtIndex(0);
                    entry.FindPropertyRelative("Enemy").objectReferenceValue = enemyData;
                    entry.FindPropertyRelative("Count").intValue = 5; // SRS 14: "Wave 1: 5 Melee"
                    entry.FindPropertyRelative("SpawnAsElite").boolValue = false;
                    entry.FindPropertyRelative("StartDelay").floatValue = 0f;

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

            Create<StageData>(
                "STG_Stage1", "stage.01", "Stage 1",
                "The MVP stage: waves then a two phase boss (SRS 14, STG-003).",
                so =>
                {
                    SerializedProperty waves = so.FindProperty("_waves");
                    waves.arraySize = 1;
                    waves.GetArrayElementAtIndex(0).objectReferenceValue = wave;
                    so.FindProperty("_boss").objectReferenceValue = boss;
                });

            // Silences the unused-local warnings for assets referenced only by others.
            if (balance == null || hero == null || upgrade == null)
                Debug.LogError("[Setup] Asset creation returned null.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Setup] Baseline data assets generated in {DataRoot}.");
        }

        private static void SetRarityWeight(SerializedProperty array, int index, Rarity rarity, float multiplier)
        {
            SerializedProperty element = array.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("Rarity").enumValueIndex = (int)rarity;
            element.FindPropertyRelative("WeightMultiplier").floatValue = multiplier;
        }

        private static T Create<T>(
            string fileName,
            string id,
            string displayName,
            string description,
            Action<SerializedObject> configure = null) where T : GameDataAsset
        {
            string path = $"{DataRoot}/{fileName}.asset";
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
}
