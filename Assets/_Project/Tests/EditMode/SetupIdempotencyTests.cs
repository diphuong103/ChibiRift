using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Tilemaps;
using UnityEngine.TestTools;
using ChibiRift.Data;
using ChibiRift.EditorTools;

namespace ChibiRift.Tests.Edit
{
    /// <summary>
    /// <c>ProjectSetup.RunAll()</c> must be idempotent (OI-35): running it, hand-editing a few of
    /// its own generated assets, then running it again must land on exactly the same result as the
    /// first run. This project has hit the opposite of that three times — an editor tool with an
    /// "already exists, return the old one" path that let a stale value survive a re-run: OI-05
    /// (<c>hurtIFrameDuration</c> reverted silently), a wave-data near miss caught in review before
    /// it shipped, and <c>RunSceneBuilder.LoadOrCreateGroundTile</c> (a <c>colliderType</c> fix that
    /// never reached the asset already on disk — the Tilemap floor had no collision for exactly
    /// this reason).
    /// </summary>
    /// <remarks>
    /// <para><b>Scope: plain ScriptableObject <c>.asset</c> files only.</b> Every asset under
    /// <c>Assets/_Project/Data</c> plus the ground tile round-trips through
    /// <c>AssetDatabase.CreateAsset</c> with a fixed main-object fileID (<c>11400000</c>), so its
    /// serialized YAML is byte-identical across two regenerations when nothing meaningful changed.
    /// Prefabs and scenes are deliberately excluded: their GameObjects get a fresh random fileID on
    /// every <c>SaveAsPrefabAsset</c>/scene save regardless of any real property drift — confirmed
    /// empirically, <c>Projectile.prefab</c>'s root fileID differed between two back-to-back
    /// <c>RunAll()</c> calls with an otherwise identical prefab — so a byte-for-byte compare there
    /// would fail for a reason that has nothing to do with the bug this guards against.</para>
    ///
    /// <para>Calling <c>RunAll()</c> replaces the currently open scene
    /// (<c>EditorSceneManager.NewScene</c>), same as running it from the menu. Harmless in the
    /// batchmode runs this project's test suite actually runs under; running this test interactively
    /// in the Editor discards whatever scene was open, same as the menu command already does.</para>
    /// </remarks>
    [TestFixture]
    public sealed class SetupIdempotencyTests
    {
        private const string DataRoot = "Assets/_Project/Data";
        private const string TilePath = "Assets/_Project/Art/Tileset/Ground/Tile_Placeholder.asset";

        [Test]
        public void Test_Setup_IsIdempotent()
        {
            // RunAll() rebuilds the HUD, which looks up a builtin font that this Editor-test
            // context (unlike a plain -executeMethod invocation) logs an assertion for failing to
            // find — harmless noise unrelated to asset idempotency, the thing under test here.
            LogAssert.ignoreFailingMessages = true;
            try
            {
                ProjectSetup.RunAll();
                Dictionary<string, string> baseline = SnapshotGeneratedAssets();

                // Simulate a future "early return on already exists" bug by hand-corrupting a
                // handful of representative fields the exact same way OI-35's three real incidents
                // did.
                PerturbTileColliderType();
                PerturbBalanceConfigField();
                PerturbWaveDataCount();

                ProjectSetup.RunAll();
                Dictionary<string, string> afterSecondRun = SnapshotGeneratedAssets();

                AssertNoDrift(baseline, afterSecondRun);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        private static void AssertNoDrift(
            Dictionary<string, string> baseline, Dictionary<string, string> afterSecondRun)
        {
            var offenders = new List<string>();

            foreach (KeyValuePair<string, string> entry in baseline)
            {
                if (!afterSecondRun.TryGetValue(entry.Key, out string after))
                {
                    offenders.Add($"{entry.Key}: present after run 1, missing after run 2");
                }
                else if (after != entry.Value)
                {
                    offenders.Add($"{entry.Key}: content differs after the second run");
                }
            }

            foreach (string path in afterSecondRun.Keys)
            {
                if (!baseline.ContainsKey(path)) offenders.Add($"{path}: appeared only after the second run");
            }

            Assert.That(offenders, Is.Empty,
                "ProjectSetup.RunAll() is not idempotent — a hand-edit made before the second run " +
                "should have been overwritten, not kept:\n  " + string.Join("\n  ", offenders));
        }

        private static Dictionary<string, string> SnapshotGeneratedAssets()
        {
            var snapshot = new Dictionary<string, string>();

            foreach (string path in Directory.GetFiles(DataRoot, "*.asset", SearchOption.TopDirectoryOnly))
            {
                snapshot[path.Replace('\\', '/')] = File.ReadAllText(path);
            }

            if (File.Exists(TilePath)) snapshot[TilePath] = File.ReadAllText(TilePath);

            Assert.That(snapshot, Is.Not.Empty, $"No .asset files found under {DataRoot} — RunAll() did not run.");
            return snapshot;
        }

        private static void PerturbTileColliderType()
        {
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(TilePath);
            Assert.That(tile, Is.Not.Null, $"{TilePath} was not generated by the first RunAll().");

            tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
            AssetDatabase.SaveAssets();
        }

        private static void PerturbBalanceConfigField()
        {
            var balance = AssetDatabase.LoadAssetAtPath<BalanceConfig>($"{DataRoot}/BalanceConfig.asset");
            Assert.That(balance, Is.Not.Null, "BalanceConfig.asset was not generated by the first RunAll().");

            var so = new SerializedObject(balance);
            so.FindProperty("_stressAllocationBudgetKilobytes").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        private static void PerturbWaveDataCount()
        {
            var wave = AssetDatabase.LoadAssetAtPath<WaveData>($"{DataRoot}/WAV_Stage1_Wave1.asset");
            Assert.That(wave, Is.Not.Null, "WAV_Stage1_Wave1.asset was not generated by the first RunAll().");

            var so = new SerializedObject(wave);
            SerializedProperty entries = so.FindProperty("_entries");
            entries.GetArrayElementAtIndex(0).FindPropertyRelative("Count").intValue = 999;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }
    }
}
