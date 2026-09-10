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

        /// <summary>Pixels per unit, matching the project constant (README section 2).</summary>
        private const int PixelsPerUnit = 32;

        /// <summary>
        /// Returns the white placeholder sprite, writing it to disk on first use.
        /// </summary>
        [MenuItem("ChibiRift/Setup/6. Generate Placeholder Art")]
        public static void Generate() => LoadOrCreateWhiteSprite();

        /// <summary>Returns the shared white sprite asset, creating it if it is not there yet.</summary>
        public static Sprite LoadOrCreateWhiteSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
            if (existing != null) return existing;

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
            var importer = AssetImporter.GetAtPath(WhiteSpritePath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;

            importer.SaveAndReimport();
        }
    }
}
