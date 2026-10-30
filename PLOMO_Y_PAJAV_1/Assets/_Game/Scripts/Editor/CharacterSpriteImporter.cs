using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ajustes de importación para los PNG de Assets/_Game/Art/Characters/&lt;characterId&gt;/:
/// Sprite Single, pivote abajo al centro, 250 px por metro (450 px ≈ 1.8 m), Bilinear, sin compresión.
/// Excepciones: wilbur_hands → pivote abajo a la derecha; intro → pivote al centro y 100 px por metro;
/// guardia → 236 px por metro (el primer dibujo mide 449 px de alto en un lienzo de 512: 449 / 236 ≈ 1.9 m).
/// </summary>
public class CharacterSpriteImporter : AssetPostprocessor
{
    public const string CharactersFolder = "Assets/_Game/Art/Characters";

    // Subir la versión obliga a Unity a reimportar los PNG con los ajustes nuevos
    public override uint GetVersion() => 2;

    private void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.StartsWith(CharactersFolder + "/")) return;

        string characterId = CharacterSpriteSetBuilder.CharacterIdFromPath(path);
        var importer = (TextureImporter)assetImporter;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.maxTextureSize = 2048;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMode = (int)SpriteImportMode.Single;
        settings.spriteMeshType = SpriteMeshType.FullRect;

        switch (characterId)
        {
            case "wilbur_hands":
                settings.spriteAlignment = (int)SpriteAlignment.BottomRight;
                settings.spritePixelsPerUnit = 250f;
                break;
            case "intro":
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.spritePixelsPerUnit = 100f;
                break;
            case "guardia":
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                settings.spritePixelsPerUnit = 236f;
                break;
            default:
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                settings.spritePixelsPerUnit = 250f;
                break;
        }
        importer.SetTextureSettings(settings);
    }
}

/// <summary>
/// MENÚ: Plomo y Paja → Reconstruir Sets de Sprites
/// Una carpeta por personaje en Art/Characters → un CharacterSpriteSet en Art/SpriteSets.
/// Archivos: &lt;anim&gt;_&lt;NN&gt;.png (también se acepta &lt;characterId&gt;_&lt;anim&gt;_&lt;NN&gt;.png).
/// Al actualizar se conserva el fps que se haya cambiado a mano en el asset.
/// </summary>
public static class CharacterSpriteSetBuilder
{
    public const string SetsFolder = "Assets/_Game/Art/SpriteSets";

    public static readonly string[] CharacterIds =
        { "guardia", "informante", "donlobo", "porky", "max", "wilbur_hands", "fx", "intro" };

    private static readonly Dictionary<string, float> DefaultFps = new Dictionary<string, float>
    {
        { "idle", 4f }, { "walk", 8f }, { "shoot", 10f }, { "hit", 1f }, { "surrender", 4f },
        { "death", 8f }, { "talk", 6f }, { "fire", 12f }, { "reload", 8f },
    };

    private static readonly Regex FrameName = new Regex(@"^([a-z]+)_(\d+)$");

    [MenuItem("Plomo y Paja/Reconstruir Sets de Sprites")]
    public static void RebuildAll()
    {
        if (EditorMenuGuard.BlockedInPlayMode()) return;
        int built = 0;
        if (AssetDatabase.IsValidFolder(CharacterSpriteImporter.CharactersFolder))
        {
            foreach (string folder in AssetDatabase.GetSubFolders(CharacterSpriteImporter.CharactersFolder))
            {
                string characterId = Path.GetFileName(folder).ToLowerInvariant(); // "Guardia" y "guardia" son el mismo personaje
                Rebuild(characterId, folder);
                built++;
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[SpriteSets] {built} set(s) actualizados en {SetsFolder}");
    }

    /// <summary>Carga el set de un personaje o lo crea vacío (los prefabs lo referencian desde el principio).</summary>
    public static CharacterSpriteSet GetOrCreateSet(string characterId)
    {
        string path = $"{SetsFolder}/{characterId}.asset";
        var set = AssetDatabase.LoadAssetAtPath<CharacterSpriteSet>(path);
        if (set != null) return set;

        LevelBuilderUtils.EnsureFolder(SetsFolder);
        set = ScriptableObject.CreateInstance<CharacterSpriteSet>();
        set.characterId = characterId;
        AssetDatabase.CreateAsset(set, path);
        return set;
    }

    /// <summary>"Assets/_Game/Art/Characters/guardia/idle_01.png" → "guardia".</summary>
    public static string CharacterIdFromPath(string assetPath)
    {
        string rest = assetPath.Substring(CharacterSpriteImporter.CharactersFolder.Length + 1);
        int slash = rest.IndexOf('/');
        return slash > 0 ? rest.Substring(0, slash).ToLowerInvariant() : "";
    }

    private static void Rebuild(string characterId, string folder)
    {
        CharacterSpriteSet set = GetOrCreateSet(characterId);

        // anim → (número de frame → sprite)
        var anims = new SortedDictionary<string, SortedDictionary<int, Sprite>>(System.StringComparer.Ordinal);
        string prefix = characterId.ToLowerInvariant() + "_";
        foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) continue;

            string file = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (file.StartsWith(prefix)) file = file.Substring(prefix.Length);
            Match m = FrameName.Match(file);
            if (!m.Success)
            {
                Debug.LogWarning($"[SpriteSets] Nombre no válido (se espera <anim>_<NN>.png): {path}");
                continue;
            }

            string anim = m.Groups[1].Value;
            int frame = int.Parse(m.Groups[2].Value);
            if (!anims.TryGetValue(anim, out var frames))
                anims[anim] = frames = new SortedDictionary<int, Sprite>();
            frames[frame] = sprite;
        }

        var clips = new List<CharacterSpriteSet.AnimClip>();
        foreach (var pair in anims)
        {
            float fps = DefaultFps.TryGetValue(pair.Key, out float f) ? f : 8f;
            if (set.clips != null)
                foreach (var old in set.clips)
                    if (old != null && old.name == pair.Key && old.fps > 0f) fps = old.fps;

            clips.Add(new CharacterSpriteSet.AnimClip
            {
                name = pair.Key,
                frames = new List<Sprite>(pair.Value.Values).ToArray(),
                fps = fps,
            });
        }

        set.characterId = characterId;
        set.clips = clips.ToArray();
        EditorUtility.SetDirty(set);
    }
}
