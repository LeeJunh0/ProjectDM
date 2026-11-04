using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    internal sealed class PixelFxAssetSetup : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (assetPath != "Assets/Resources/ProjectDM/Skills/PixelFX/skill-effects.png") return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048; importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
