using System.IO;
using UnityEditor;
using UnityEngine;

namespace ChibiRift.EditorTools
{
    /// <summary>
    /// Creates the one placeholder sprite every generated actor and platform uses.
    /// </summary>
    /// <remarks>
    /// <para><b>Why this exists.</b> The generators used to build their placeholder sprite with
    /// <c>new Texture2D</c> and <c>Sprite.Create</c> at edit time. Those are in-memory objects, not
    /// assets on disk. A scene can embed such an object in its own <c>.unity</c> file, so boxes and
    /// dummies created directly in the scene looked fine — but
    /// <see cref="PrefabUtility.SaveAsPrefabAsset"/> cannot serialise a reference to something that
    /// has no asset path, so every prefab came out with <c>Sprite = None</c> and rendered nothing.
    /// The hero and all three enemies were invisible in Run_01 for exactly this reason.</para>
    ///
    /// <para><b>One sprite, tinted.</b> A single white texel sheet is written once and every
    /// renderer tints it through <c>SpriteRenderer.color</c> and sizes it through
    /// <c>SpriteRenderer.size</c>. Nothing scales a Transform to change how big a sprite looks —
    /// that would scale the collider with it, which is the second half of the same bug.</para>
    ///
    /// <para>The import settings are the P1 technical constants from README section 2: 32 pixels
    /// per unit, Point filtering, no compression, no mipmaps.</para>
    /// </remarks>
    public static class PlaceholderArt
    {
        /// <summary>Where the generated placeholder art lives.</summary>
        public const string ArtRoot = "Assets/_Project/Art/Placeholder";

        /// <summary>The single white sprite every placeholder renderer uses.</summary>
        public const string WhiteSpritePath = ArtRoot + "/px_white.png";

        /// <summary>
        /// A second white sprite, sized to one Ground tileset cell (16px at 32 PPU = 0.5 world
        /// units) rather than one full unit. <see cref="WhiteSpritePath"/>'s sprite is the wrong
        /// size for a <c>Tile</c>: Tilemap rendering places a tile's sprite at its own pixel size,
        /// not stretched to fill the grid cell the way <c>SpriteRenderer.size</c> can (P2 slice 1,
        /// A5 — Run_01's ground moved from BoxCollider2D to Tilemap).
        /// </summary>
        public const string TileSpritePath = ArtRoot + "/px_white_tile16.png";

        /// <summary>Pixels per unit, matching the project constant (README section 2).</summary>
        private const int PixelsPerUnit = 32;

        /// <summary>Matches the Ground tileset's declared cell size (P2 slice 1, A1).</summary>
        private const int TilePixelSize = 16;

        /// <summary>
        /// Returns the white placeholder sprite, regenerating it on disk every run.
        /// </summary>
        [MenuItem("ChibiRift/Setup/6. Generate Placeholder Art")]
        public static void Generate() => LoadOrCreateWhiteSprite();

        /// <summary>
        /// Returns the shared white sprite asset, regenerating it every call. Setup tools in this
        /// project must be idempotent by fully reapplying their output, not by skipping work when
        /// something already exists (OI-35) — the file and its import settings are both cheap to
        /// rebuild, so there is no reason to special-case "already exists".
        /// </summary>
        public static Sprite LoadOrCreateWhiteSprite()
        {
            Directory.CreateDirectory(ArtRoot);

            var texture = new Texture2D(PixelsPerUnit, PixelsPerUnit, TextureFormat.RGBA32, false);
            var pixels = new Color32[PixelsPerUnit * PixelsPerUnit];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply();

            File.WriteAllBytes(WhiteSpritePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(WhiteSpritePath, ImportAssetOptions.ForceSynchronousImport);
            ApplyImportSettings();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
            if (sprite == null) Debug.LogError($"[Setup] Failed to import {WhiteSpritePath}.");

            return sprite;
        }

        /// <summary>
        /// Applies the P1 sprite import constants. Bilinear filtering or compression would blur a
        /// 32 PPU sprite, and mipmaps are pointless when the camera never scales it.
        /// </summary>
        private static void ApplyImportSettings()
        {
            ApplyImportSettings(WhiteSpritePath, SpriteImportMode.Single);
        }

        /// <summary>Returns the shared tile-sized white sprite, regenerating it every call (OI-35).</summary>
        public static Sprite LoadOrCreateTileSprite()
        {
            Directory.CreateDirectory(ArtRoot);

            var texture = new Texture2D(TilePixelSize, TilePixelSize, TextureFormat.RGBA32, false);
            var pixels = new Color32[TilePixelSize * TilePixelSize];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply();

            File.WriteAllBytes(TileSpritePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(TileSpritePath, ImportAssetOptions.ForceSynchronousImport);
            ApplyImportSettings(TileSpritePath, SpriteImportMode.Single);

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TileSpritePath);
            if (sprite == null) Debug.LogError($"[Setup] Failed to import {TileSpritePath}.");

            return sprite;
        }

        private static void ApplyImportSettings(string path, SpriteImportMode mode)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = mode;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;

            importer.SaveAndReimport();
        }
    }
}
