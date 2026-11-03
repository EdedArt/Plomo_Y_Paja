using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ajustes de importación para los PNG de Assets/_Game/Art/Characters/&lt;characterId&gt;/:
/// Sprite Single, pivote abajo al centro, 250 px por metro (450 px ≈ 1.8 m), Bilinear, sin compresión.
/// Excepciones: wilbur_hands → pivote abajo a la derecha; intro → pivote al centro y 100 px por metro;
/// Pivote: el X va al centro del lienzo y el Y a la fila más baja con alpha > 0.1 (los pies), como pivote
/// personalizado, para que cualquier frame quede pegado al suelo aunque el dibujo tenga margen debajo.
/// contador → 500 px por metro y pivote Bottom Center (lienzo 1024, fase 3C.2).
/// guardia → 236 px por metro (el primer dibujo mide 449 px de alto en un lienzo de 512: 449 / 236 ≈ 1.9 m).
/// La muerte (dibujo tumbado) usa la misma regla: su fila más baja es el cuerpo en el piso, sin pivote al centro.
/// </summary>
public class CharacterSpriteImporter : AssetPostprocessor
{
    public const string CharactersFolder = "Assets/_Game/Art/Characters";

    // Subir la versión obliga a Unity a reimportar los PNG con los ajustes nuevos.
    // 4: los .meta que llegan con PPU/pivote puestos a mano (p. ej. los del guardia de Edgar) se pisan aquí.
    // 6: contador a 500 px por metro (fase 3C.2, D12).
    // 7: también Art/Pickups/ (fase 3C.5.2: el revólver en el piso).
    // 8: también Art/UI/HUD/ (fase HUD.1: caras e íconos del HUD).
    // 9: también Art/FX/ (fase HUD.2: polvo de impacto y agujero de bala).
    // 10: Art/Intro/ (fase INTRO.0: texturas del libro, no son sprites de personaje).
    // 11: taxista a 500 px por metro, pivote abajo al centro, mipmaps (fase INTRO.2).
    // 12: HUD.1.1: el arte del HUD que cae en Art/Characters/hud/ (así llegó el de Edgar desde dev) se importa con la regla
    //     del HUD (sprite de UI: pivote al centro, sin mipmaps, sin compresión), nunca con la de personaje (PPU 250 y pivote
    //     en los pies). Los PNG de Edgar ya se movieron a Art/UI/HUD/ con los ids del guion.
    public override uint GetVersion() => 12;

    /// <summary>Texturas del libro de la intro (fase INTRO): Art/Intro/ilus_intro_NN, libro_portada, libro_guardas, libro_papel.</summary>
    public const string IntroFolder = "Assets/_Game/Art/Intro";

    /// <summary>Efectos de disparo (fase HUD.2): Art/FX/fx_impacto_01/02, fx_agujero_bala (character_key.py --center FX --dst ...).</summary>
    public const string FxFolder = "Assets/_Game/Art/FX";

    /// <summary>Caras e íconos del HUD (fase HUD.1): Art/UI/HUD/&lt;id&gt;.png, recortados con character_key.py --center.</summary>
    public const string HudFolder = "Assets/_Game/Art/UI/HUD";

    /// <summary>
    /// HUD.1.1: arte del HUD que llega dentro de Art/Characters (carpeta "hud"). NO es un personaje: regla del HUD y fuera
    /// de "Reconstruir Sets de Sprites". Lo correcto es moverlo a Art/UI/HUD/ con el id del guion (HudBuilder lo busca ahí).
    /// </summary>
    public const string HudInCharactersFolder = CharactersFolder + "/hud";

    /// <summary>¿Esta ruta es arte del HUD (Art/UI/HUD o Art/Characters/hud, sin importar mayúsculas)?</summary>
    public static bool IsHudArt(string assetPath)
    {
        string path = assetPath.Replace('\\', '/');
        return path.StartsWith(HudFolder + "/") ||
               path.StartsWith(HudInCharactersFolder + "/", System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Objetos recogibles dibujados (fase 3C.5.2): Art/Pickups/&lt;nombre&gt;.png, recortados con character_key.py --single.</summary>
    public const string PickupsFolder = "Assets/_Game/Art/Pickups";

    /// <summary>Alpha mínimo (0..255) para considerar que un píxel es parte del personaje.</summary>
    private const byte FeetAlpha = 26; // 0.1

    private void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        if (path.StartsWith(PickupsFolder + "/"))
        {
            ApplyPickup((TextureImporter)assetImporter);
            return;
        }
        if (path.StartsWith(FxFolder + "/"))
        {
            ApplyPickup((TextureImporter)assetImporter); // mismo trato: Sprite con alpha, pivote al centro, mipmaps
            return;
        }
        if (IsHudArt(path)) // HUD.1.1: también Art/Characters/hud/ (regla explícita, antes caía en la de personaje)
        {
            ApplyHud((TextureImporter)assetImporter);
            return;
        }
        if (path.StartsWith(IntroFolder + "/"))
        {
            ApplyBook((TextureImporter)assetImporter);
            return;
        }
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
            case "contador":
                // lienzo 1024x1024 con los pies pegados al borde inferior: pivote Bottom Center, mipmaps activos.
                // 500 px por metro (D12): el lienzo mide 2.05 m, igual que el lienzo de 512 del guardia a 250.
                // Los cuadros sentados llenan el lienzo: CentavoController los achica con seatedScale.
                importer.mipmapEnabled = true;
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                settings.spritePixelsPerUnit = 500f;
                break;
            case "taxista":
                // INTRO.2: lienzo 1024x1024 (conduce_01, habla_01/02) como el contador: 500 px por metro, pivote abajo al
                // centro y mipmaps (se ve de cerca dentro del taxi)
                importer.mipmapEnabled = true;
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                settings.spritePixelsPerUnit = 500f;
                break;
            case "guardia":
                SetFeetPivot(settings, path);
                settings.spritePixelsPerUnit = 236f;
                break;
            default:
                SetFeetPivot(settings, path);
                settings.spritePixelsPerUnit = 250f;
                break;
        }
        importer.SetTextureSettings(settings);
    }

    /// <summary>
    /// Recogibles: Sprite con alpha real, pivote al centro, mipmaps (se ven chicos y lejos), 1000 px por metro. El tamaño en
    /// el mundo lo fija WeaponPickup (visualLength), así que el PPU no importa.
    /// </summary>
    private static void ApplyPickup(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.textureShape = TextureImporterShape.Texture2D;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.maxTextureSize = 2048;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMode = (int)SpriteImportMode.Single;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePixelsPerUnit = 1000f;
        importer.SetTextureSettings(settings);
    }

    /// <summary>
    /// Libro de la intro (INTRO.0): textura normal (van en el material de un quad, no son Sprite), sRGB, mipmaps,
    /// Trilinear, máximo 2048, sin compresión (se ven de cerca, a ~85 % del cuadro).
    /// </summary>
    private static void ApplyBook(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Default;
        importer.textureShape = TextureImporterShape.Texture2D;
        importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.alphaIsTransparency = false;
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.isReadable = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
    }

    /// <summary>HUD (HUD.1, paso 0): Sprite Single, Full Rect, pivote al centro, SIN mipmaps, sin compresión, Bilinear,
    /// alpha = transparencia. El PPU (100) no importa en la UI: HudWilbur fija el tamaño de cada Image.</summary>
    private static void ApplyHud(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.textureShape = TextureImporterShape.Texture2D;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.maxTextureSize = 1024;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMode = (int)SpriteImportMode.Single;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePixelsPerUnit = 100f;
        importer.SetTextureSettings(settings);
    }

    /// <summary>Pivote personalizado: centro en X y la fila más baja visible en Y. Sin lectura posible = Bottom Center.</summary>
    private static void SetFeetPivot(TextureImporterSettings settings, string path)
    {
        float y = FeetPivotY(path);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = new Vector2(0.5f, y);
    }

    /// <summary>Fila más baja con alpha &gt; 0.1, normalizada 0..1 (0 = borde inferior). Lee el PNG del disco.</summary>
    public static float FeetPivotY(string assetPath)
    {
        try
        {
            string full = Path.GetFullPath(assetPath);
            if (!File.Exists(full)) return 0f;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(full), false)) return 0f;
                Color32[] px = tex.GetPixels32();
                int w = tex.width, h = tex.height;
                for (int row = 0; row < h; row++)          // fila 0 = abajo
                    for (int x = 0; x < w; x++)
                        if (px[row * w + x].a >= FeetAlpha) return row / (float)h;
                return 0f;
            }
            finally { Object.DestroyImmediate(tex); }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[SpriteImporter] No se pudo calcular el pivote de " + assetPath + ": " + e.Message);
            return 0f;
        }
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
        { "guardia", "informante", "donlobo", "porky", "max", "contador", "wilbur_hands", "fx", "intro" };

    private static readonly Dictionary<string, float> DefaultFps = new Dictionary<string, float>
    {
        { "idle", 4f }, { "walk", 8f }, { "shoot", 10f }, { "hit", 1f }, { "surrender", 4f },
        { "death", 8f }, { "talk", 6f }, { "fire", 12f }, { "reload", 8f }, { "limp", 4f },
    };

    private static readonly Regex FrameName = new Regex(@"^([a-z]+)_(\d+)$");

    /// <summary>Carpetas de Art/Characters que NO son personajes (HUD.1.1): no se escanean ni crean set.</summary>
    public static readonly string[] NonCharacterFolders = { "hud" };

    public static bool IsCharacterFolder(string folderName)
    {
        string id = folderName.ToLowerInvariant();
        foreach (string skip in NonCharacterFolders)
            if (id == skip) return false;
        return true;
    }

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
                if (!IsCharacterFolder(characterId)) continue; // HUD.1.1: el arte del HUD no es un personaje
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
