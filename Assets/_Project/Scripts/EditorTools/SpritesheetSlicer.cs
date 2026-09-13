using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ChibiRift.EditorTools
{
    /// <summary>
    /// Slices the three known spritesheets into named, pivoted sprites (P2 slice 1: art pipeline).
    /// The grid cell size and pivot come from which of the three folders a file sits under, not
    /// from the file itself — the brief fixes the mapping (Hero 64px, Enemy_Melee 48px, Tileset
    /// 16px), so it lives here as a small lookup table rather than anywhere configurable.
    /// </summary>
    /// <remarks>
    /// The actual slicing runs from <see cref="ArtImportPostprocessor.OnPreprocessTexture"/> — it
    /// has to, since <c>TextureImporter.spritesheet</c> only takes effect when set during import,
    /// not after. What lives here is <see cref="TryGetRule"/> and <see cref="BuildSpritesheetMeta"/>,
    /// shared by that postprocessor and by <see cref="ReimportKnownSpritesheets"/> below, which is
    /// the "menu tool" the brief asks for: it does not slice anything itself, it just forces Unity
    /// to reimport the three known paths, which runs the same postprocessor.
    /// </remarks>
    public static class SpritesheetSlicer
    {
        private const string HeroFolder = "Assets/_Project/Art/Characters/Hero";
        private const string EnemyMeleeFolder = "Assets/_Project/Art/Characters/Enemy_Melee";
        private const string TilesetGroundFolder = "Assets/_Project/Art/Tileset/Ground";

        private const string HeroSheetPath = HeroFolder + "/Hero_Spritesheet.png";
        private const string EnemyMeleeSheetPath = EnemyMeleeFolder + "/Enemy_Melee_Spritesheet.png";

        private static readonly HashSet<string> s_warned = new HashSet<string>();

        /// <summary>One folder's slicing rule: grid cell size, sprite pivot, and name prefix.</summary>
        public readonly struct SliceRule
        {
            public readonly int CellSize;
            public readonly SpriteAlignment Pivot;
            public readonly string NamePrefix;

            public SliceRule(int cellSize, SpriteAlignment pivot, string namePrefix)
            {
                CellSize = cellSize;
                Pivot = pivot;
                NamePrefix = namePrefix;
            }
        }

        /// <summary>
        /// Resolves the slicing rule for <paramref name="assetPath"/> from which of the three known
        /// folders it sits under. False for anything else under <c>Art/</c> — a PNG outside these
        /// three folders (the placeholder sprite, a stray asset) is not this pipeline's business.
        /// </summary>
        public static bool TryGetRule(string assetPath, out SliceRule rule)
        {
            if (assetPath.StartsWith(HeroFolder + "/"))
            {
                rule = new SliceRule(64, SpriteAlignment.BottomCenter, "Hero");
                return true;
            }

            if (assetPath.StartsWith(EnemyMeleeFolder + "/"))
            {
                rule = new SliceRule(48, SpriteAlignment.BottomCenter, "Enemy_Melee");
                return true;
            }

            if (assetPath.StartsWith(TilesetGroundFolder + "/"))
            {
                rule = new SliceRule(16, SpriteAlignment.Center, "Ground");
                return true;
            }

            rule = default;
            return false;
        }

        /// <summary>
        /// Builds one <see cref="SpriteMetaData"/> per grid cell, named <c>{prefix}_0</c>,
        /// <c>{prefix}_1</c>... in sheet order: left to right, top to bottom. Texture space has
        /// y = 0 at the bottom, so the sheet's top row is read last in texture coordinates.
        /// </summary>
        public static SpriteMetaData[] BuildSpritesheetMeta(int width, int height, SliceRule rule)
        {
            int columns = Mathf.Max(1, width / rule.CellSize);
            int rows = Mathf.Max(1, height / rule.CellSize);
            Vector2 pivot = PivotFor(rule.Pivot);

            var metas = new SpriteMetaData[columns * rows];
            int index = 0;

            for (int row = 0; row < rows; row++)
            {
                float y = height - (row + 1) * rule.CellSize;
                for (int col = 0; col < columns; col++)
                {
                    metas[index] = new SpriteMetaData
                    {
                        name = $"{rule.NamePrefix}_{index}",
                        rect = new Rect(col * rule.CellSize, y, rule.CellSize, rule.CellSize),
                        alignment = (int)rule.Pivot,
                        pivot = pivot
                    };
                    index++;
                }
            }

            return metas;
        }

        /// <summary>
        /// Forces Unity to reimport the known spritesheets, which runs
        /// <see cref="ArtImportPostprocessor.OnPreprocessTexture"/> on each. Safe to run with none
        /// of the files present yet: each missing one logs a warning exactly once and is otherwise
        /// a no-op, leaving whatever placeholder is already wired.
        /// </summary>
        [MenuItem("ChibiRift/Setup/7. Slice Character & Tile Spritesheets")]
        public static void ReimportKnownSpritesheets()
        {
            ReimportIfPresent(HeroSheetPath);
            ReimportIfPresent(EnemyMeleeSheetPath);

            if (!AssetDatabase.IsValidFolder(TilesetGroundFolder))
            {
                WarnOnceMissing(TilesetGroundFolder);
                return;
            }

            // Unlike Hero/Enemy_Melee, the brief names no fixed file for the tileset — every PNG
            // found under the folder is sliced.
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { TilesetGroundFolder });
            foreach (string guid in guids)
            {
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            }
        }

        private static void ReimportIfPresent(string path)
        {
            if (!File.Exists(path))
            {
                WarnOnceMissing(path);
                return;
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static void WarnOnceMissing(string path)
        {
            if (!s_warned.Add(path)) return;
            Debug.LogWarning($"[Setup] {path} not found; keeping the existing placeholder art.");
        }

        private static Vector2 PivotFor(SpriteAlignment alignment) => alignment switch
        {
            SpriteAlignment.BottomCenter => new Vector2(0.5f, 0f),
            SpriteAlignment.Center => new Vector2(0.5f, 0.5f),
            _ => new Vector2(0.5f, 0.5f)
        };
    }
}
