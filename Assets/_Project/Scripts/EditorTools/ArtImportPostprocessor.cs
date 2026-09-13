using UnityEditor;
using UnityEngine;

namespace ChibiRift.EditorTools
{
    /// <summary>
    /// Forces correct sprite import settings on the three known spritesheets, so dropping a real
    /// PNG into the right folder never needs a manual Inspector pass (P2 slice 1: art pipeline).
    /// </summary>
    /// <remarks>
    /// Scoped to exactly the three folders <see cref="SpritesheetSlicer.TryGetRule"/> knows about.
    /// Everything else under <c>Art/</c> — the placeholder sprite <see cref="PlaceholderArt"/>
    /// already configures, and any stray asset — is left untouched: this postprocessor is not a
    /// general "every PNG under Art/" rule, only the pipeline for these three folders.
    ///
    /// <para>Slicing happens here, in <see cref="OnPreprocessTexture"/>, rather than after import:
    /// <c>TextureImporter.spritesheet</c> only takes effect when set during the texture's own
    /// import pass. <see cref="TextureImporter.GetSourceTextureWidthAndHeight"/> gives the source
    /// pixel size before the texture is fully decoded, which is what the grid slice needs.</para>
    /// </remarks>
    public sealed class ArtImportPostprocessor : AssetPostprocessor
    {
        private const int PixelsPerUnit = 32;

        private void OnPreprocessTexture()
        {
            if (!SpritesheetSlicer.TryGetRule(assetPath, out SpritesheetSlicer.SliceRule rule)) return;

            var importer = (TextureImporter)assetImporter;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;

            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            importer.spritesheet = SpritesheetSlicer.BuildSpritesheetMeta(width, height, rule);
        }
    }
}
