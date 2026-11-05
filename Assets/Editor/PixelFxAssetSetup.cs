using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    internal sealed class PixelFxAssetSetup : AssetPostprocessor
    {
        public override uint GetVersion() => 3;
        private void OnPreprocessTexture()
        {
            if (assetPath != "Assets/Resources/ProjectDM/Skills/PixelFX/skill-effects.png" &&
                assetPath != "Assets/Resources/ProjectDM/UI/RelicForge/forge-effects.png" &&
                assetPath != "Assets/Resources/ProjectDM/UI/RelicForge/forge-coin-v1.png") return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048; importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
