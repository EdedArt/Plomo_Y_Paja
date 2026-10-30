using UnityEditor;
using UnityEngine;

/// <summary>
/// Crea los materiales de personaje sin iluminación en Assets/_Game/Resources/CharacterLook/.
/// Están en Resources para que el build incluya los shaders y CharacterLook los encuentre en runtime.
/// </summary>
public static class CharacterLookBuilder
{
    private const string Folder = "Assets/_Game/Resources/" + CharacterLook.ResourceFolder;

    [MenuItem("Plomo y Paja/Crear Materiales de Personajes")]
    public static void CreateAll()
    {
        LevelBuilderUtils.EnsureFolder(Folder);
        Material sprite = GetOrCreate(CharacterLook.SpriteMaterialName, CharacterLook.SpriteShaderName);
        if (sprite != null) sprite.SetFloat("_Cutoff", 0.5f);

        Material placeholder = GetOrCreate(CharacterLook.PlaceholderMaterialName, CharacterLook.PlaceholderShaderName);
        if (placeholder != null)
        {
            placeholder.SetColor("_OutlineColor", Color.black);
            placeholder.SetFloat("_OutlineWidth", 0.025f);
        }

        Material blob = GetOrCreate(CharacterLook.BlobMaterialName, CharacterLook.BlobShaderName);
        if (blob != null)
        {
            blob.SetColor("_ShadowColor", new Color32(0x40, 0x2E, 0x59, 0xFF));
            blob.SetFloat("_Strength", 0.75f);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[CharacterLook] Materiales de personaje listos en " + Folder);
    }

    private static Material GetOrCreate(string name, string shaderName)
    {
        string path = Folder + "/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogError("[CharacterLook] No se encontró el shader '" + shaderName + "'. ¿Se importó Assets/_Game/Shaders?");
            return mat;
        }
        if (mat == null)
        {
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
