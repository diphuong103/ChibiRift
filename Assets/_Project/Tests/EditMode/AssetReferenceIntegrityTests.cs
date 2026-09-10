using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ChibiRift.Data;

namespace ChibiRift.Tests.Edit
{
    /// <summary>
    /// Catches object references that were left dangling by an asset being renamed, moved or
    /// regenerated.
    /// </summary>
    /// <remarks>
    /// <para><b>Why this exists.</b> <c>ENM_Melee</c> was renamed to <c>ENM_MeleeGrunt</c> in P1
    /// slice 3. Renaming an asset that others point at is the classic way to leave a reference
    /// reading <c>None (EnemyData)</c> in the inspector, and Unity does not complain: the wave
    /// simply spawns nothing, at runtime, in a build, with no error. A missing reference is a
    /// content bug that only content can catch, so it is checked here rather than trusted.</para>
    ///
    /// <para>The generator deletes and recreates every asset on each run, so GUIDs churn by
    /// design. That makes broken cross-references a standing risk rather than a one-off.</para>
    /// </remarks>
    [TestFixture]
    public sealed class AssetReferenceIntegrityTests
    {
        [Test]
        public void EveryWaveEntryPointsAtAnExistingEnemy()
        {
            var offenders = new List<string>();

            foreach (WaveData wave in LoadAll<WaveData>())
            {
                WaveEntry[] entries = wave.Entries;

                Assert.That(entries, Is.Not.Null.And.Not.Empty,
                    $"'{wave.name}' has no spawn entries, so the wave can never be cleared (WAV-002).");

                for (int i = 0; i < entries.Length; i++)
                {
                    if (entries[i].Enemy != null) continue;
                    offenders.Add($"{wave.name} entry {i}");
                }
            }

            Assert.That(offenders, Is.Empty,
                "Wave entries point at a missing EnemyData:\n  " + string.Join("\n  ", offenders) +
                "\nThis is what an asset rename leaves behind. Re-run ChibiRift > Setup so the " +
                "generator re-links them.");
        }

        [Test]
        public void EveryStagePointsAtAnExistingWaveAndBoss()
        {
            var offenders = new List<string>();

            foreach (StageData stage in LoadAll<StageData>())
            {
                WaveData[] waves = stage.Waves;

                Assert.That(waves, Is.Not.Null.And.Not.Empty, $"'{stage.name}' has no waves (STG-003).");

                for (int i = 0; i < waves.Length; i++)
                {
                    if (waves[i] == null) offenders.Add($"{stage.name} wave {i}");
                }

                if (stage.Boss == null) offenders.Add($"{stage.name} boss");
            }

            Assert.That(offenders, Is.Empty,
                "Stage references are missing:\n  " + string.Join("\n  ", offenders));
        }

        [Test]
        public void HeroPointsAtAnExistingAttackChain()
        {
            foreach (HeroData hero in LoadAll<HeroData>())
            {
                Assert.That(hero.BasicAttack, Is.Not.Null,
                    $"'{hero.name}' has no AttackData, so the basic attack does nothing (COM-001).");

                Assert.That(hero.BasicAttack.StepCount, Is.EqualTo(hero.ComboLength),
                    $"'{hero.name}' declares a {hero.ComboLength} hit combo but its chain has " +
                    $"{hero.BasicAttack.StepCount} steps (COM-002).");
            }
        }

        [Test]
        public void EveryDataAssetHasAnId()
        {
            var offenders = new List<string>();

            foreach (GameDataAsset asset in LoadAll<GameDataAsset>())
            {
                if (!asset.HasValidId) offenders.Add(asset.name);
            }

            Assert.That(offenders, Is.Empty,
                "Assets with an empty Id, which save files and telemetry key on (UPG-001):\n  " +
                string.Join("\n  ", offenders));
        }

        [Test]
        public void NoTwoDataAssetsShareAnId()
        {
            var seen = new Dictionary<string, string>();
            var offenders = new List<string>();

            foreach (GameDataAsset asset in LoadAll<GameDataAsset>())
            {
                if (!asset.HasValidId) continue;

                // Keyed by type as well as id: an EnemyData and an UpgradeData may legitimately
                // share a name, and the uniqueness rule of UPG-001 is per type.
                string key = $"{asset.GetType().Name}:{asset.Id}";

                if (seen.TryGetValue(key, out string previous))
                {
                    offenders.Add($"{key} on both '{previous}' and '{asset.name}'");
                    continue;
                }

                seen[key] = asset.name;
            }

            Assert.That(offenders, Is.Empty,
                "Duplicate ids (UPG-001):\n  " + string.Join("\n  ", offenders));
        }

        private static IEnumerable<T> LoadAll<T>() where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { "Assets/_Project/Data" });
            var results = new List<T>(guids.Length);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) results.Add(asset);
            }

            Assert.That(results, Is.Not.Empty,
                $"No {typeof(T).Name} found under Assets/_Project/Data, so this test proved nothing.");

            return results;
        }
    }
}
