using UnityEditor;

/// <summary>Imports every downloaded campaign portrait as a single, compressed sprite (max 512 px).</summary>
public class CampaignImagePostprocessor : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(CampaignGenerator.ImageFolder + "/")) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 512;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
    }
}
