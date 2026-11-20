using UnityEditor;

namespace ChickenShooter.EditorTools
{
    /// <summary>
    /// Import rules for third-party art: everything becomes a single Sprite without mipmaps.
    /// Chicken frames are huge (1100px) so they get a high pixels-per-unit; pixel-art food uses point filtering.
    /// Bump GetVersion() after changing rules to force a reimport.
    /// </summary>
    public class TextureImportRules : AssetPostprocessor
    {
        public override uint GetVersion() => 7;

        private static readonly string[] PixelArtFolders =
        {
            "/Food/"
        };

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/ThirdParty/") && !assetPath.StartsWith("Assets/_Project/Art/")) return;

            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.spritePixelsPerUnit = 100f;

            if (assetPath.Contains("/Bevouliin_"))
            {
                ti.spritePixelsPerUnit = 1000f;
            }
            else if (assetPath.Contains("/Backgrounds/"))
            {
                ti.maxTextureSize = 4096;
                ti.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            }
            else if (assetPath.Contains("/Kenney_ParticlePack/"))
            {
                // Kenney particles are white shapes on black (made for additive blending): derive alpha from luminance.
                ti.alphaSource = TextureImporterAlphaSource.FromGrayScale;
            }

            foreach (string folder in PixelArtFolders)
            {
                if (!assetPath.Contains(folder)) continue;
                ti.filterMode = UnityEngine.FilterMode.Point;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                break;
            }
        }
    }
}
