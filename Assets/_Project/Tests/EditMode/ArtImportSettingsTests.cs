using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ChibiRift.EditorTools;

namespace ChibiRift.Tests.Edit
{
    /// <summary>
    /// A1/A2 (P2 slice 1): every PNG under the three known art folders gets the correct import
    /// settings automatically, and the pipeline runs to completion with none of the real art files
    /// present — which is this project's actual state for the whole of this slice.
    /// </summary>
    [TestFixture]
    public sealed class ArtImportSettingsTests
    {
        private const string ArtRoot = "Assets/_Project/Art";
        private const string PlaceholderRoot = ArtRoot + "/Placeholder";

        private static readonly string[] KnownArtFolders =
        {
            ArtRoot + "/Characters/Hero",
            ArtRoot + "/Characters/Enemy_Melee",
            ArtRoot + "/Tileset/Ground"
        };

        /// <summary>
        /// Scans whatever PNGs actually exist. Passes trivially when none do under the three known
        /// folders — the expected state for this whole slice — but still holds the Placeholder
        /// sprites (which do exist) to their own P1 rule, and would catch a wrong setting the
        /// moment real art lands.
        /// </summary>
        [Test]
        public void Test_Sprite_ImportSettingsAreCorrect()
        {
            var offenders = new List<string>();

            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                bool isPlaceholder = path.StartsWith(PlaceholderRoot + "/");
                bool isKnownArt = IsUnderKnownArtFolder(path);
                if (!isPlaceholder && !isKnownArt) continue; // not this pipeline's concern

                string why = CheckCommonSettings(importer)
                    ?? CheckSpriteMode(importer, expectMultiple: isKnownArt);

                if (why != null) offenders.Add($"{path}: {why}");
            }

            Assert.That(offenders, Is.Empty, "Bad sprite import settings:\n  " + string.Join("\n  ", offenders));
        }

        private static bool IsUnderKnownArtFolder(string path)
        {
            foreach (string folder in KnownArtFolders)
            {
                if (path.StartsWith(folder + "/")) return true;
            }

            return false;
        }

        private static string CheckCommonSettings(TextureImporter importer)
        {
            if (importer.textureType != TextureImporterType.Sprite) return "textureType must be Sprite";
            if (importer.spritePixelsPerUnit != 32) return "spritePixelsPerUnit must be 32";
            if (importer.filterMode != FilterMode.Point) return "filterMode must be Point (no filter)";
            if (importer.mipmapEnabled) return "mipmapEnabled must be off";
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                return "textureCompression must be Uncompressed";
            return null;
        }

        private static string CheckSpriteMode(TextureImporter importer, bool expectMultiple)
        {
            SpriteImportMode expected = expectMultiple ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            return importer.spriteImportMode == expected
                ? null
                : $"expected spriteImportMode {expected}, found {importer.spriteImportMode}";
        }

        /// <summary>
        /// A real, if synthetic, end-to-end exercise of the slicer: a 2-frame 128x64 sheet at the
        /// Hero folder's real path (deleted afterwards) proves the grid math, the frame naming, and
        /// the bottom-centre character pivot all actually work — not just "doesn't throw when
        /// nothing is there", which the other two tests here already cover.
        /// </summary>
        [Test]
        public void Test_SpritesheetSlicer_SlicesAKnownFolderCorrectly()
        {
            const string path = ArtRoot + "/Characters/Hero/Hero_Spritesheet.png";

            Assert.That(File.Exists(path), Is.False,
                $"{path} already exists — refusing to overwrite it with a synthetic test sheet.");

            try
            {
                WritePlaceholderPng(path, width: 128, height: 64);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, "The synthetic sheet did not import as a TextureImporter.");
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple));

                SpriteMetaData[] sheet = importer.spritesheet;
                Assert.That(sheet, Has.Length.EqualTo(2),
                    "A 128x64 sheet at a 64px cell size should slice into exactly 2 sprites.");
                Assert.That(sheet[0].name, Is.EqualTo("Hero_0"));
                Assert.That(sheet[1].name, Is.EqualTo("Hero_1"));

                // Bottom-centre pivot for a character (A1): a 64x64 rect pivots at (0.5, 0).
                Assert.That(sheet[0].pivot, Is.EqualTo(new Vector2(0.5f, 0f)));
                Assert.That(sheet[1].pivot, Is.EqualTo(new Vector2(0.5f, 0f)));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".meta")) File.Delete(path + ".meta");

                // Leaves no trace: WritePlaceholderPng creates Art/Characters/Hero/ if it did not
                // already exist, and an empty folder this test created is not this test's to keep.
                DeleteIfEmpty(ArtRoot + "/Characters/Hero");
                DeleteIfEmpty(ArtRoot + "/Characters");

                AssetDatabase.Refresh();
            }
        }

        private static void DeleteIfEmpty(string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath)) return;
            if (Directory.GetFileSystemEntries(folderPath).Length > 0) return;

            AssetDatabase.DeleteAsset(folderPath);
        }

        /// <summary>
        /// The whole art pipeline, run with none of the three real files present — this project's
        /// actual state throughout this slice. Must complete with no exception and no error-level
        /// log (a warning is fine and expected; see the tools' own doc comments).
        /// </summary>
        [Test]
        public void Test_ArtPipeline_ToolsRunWithoutArtFiles()
        {
            Assert.DoesNotThrow(() => SpritesheetSlicer.ReimportKnownSpritesheets());
            Assert.DoesNotThrow(() => HeroAnimatorBuilder.Build());
        }

        private static void WritePlaceholderPng(string path, int width, int height)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply();

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
    }
}
