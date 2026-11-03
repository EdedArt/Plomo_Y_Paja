#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Configura solo las imagenes de Assets/_Game/Art/UI/Menu/ al importarlas.
// Independiente del CharacterSpriteImporter de David: solo toca esa carpeta
// y corre al final (order alto) para que su configuracion mande.
public class MenuSpriteImporter : AssetPostprocessor
{
    private const string Carpeta = "Assets/_Game/Art/UI/Menu/";

    public override int GetPostprocessOrder() { return 1000; }

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Carpeta)) return;

        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = false;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.maxTextureSize = 2048;
        ti.filterMode = FilterMode.Bilinear;

        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteAlignment = (int)SpriteAlignment.Center;
        s.spriteMeshType = SpriteMeshType.FullRect;
        ti.SetTextureSettings(s);
    }
}
#endif
