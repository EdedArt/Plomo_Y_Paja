using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Importación de Assets/_Game/Art/Viewmodel/ (manos en primera persona y efectos):
/// Sprite (2D and UI), Single, sin compresión, mipmaps (Kaiser) con filtro Trilinear, máximo 2048 y Alpha Is Transparency.
/// Sin compresión ni bordes duros: la calidad del contorno es lo que más se nota en estas imágenes.
/// Contrato del arte: Assets/_Game/Art/Viewmodel/README_ART.md
///
/// Se aplica al importar y además se COMPRUEBA al abrir el Editor y cada vez que llegan PNG nuevos: si Unity ya
/// estaba abierto y los importó antes de que existiera esta clase (o con otros ajustes), se reimportan solos.
/// </summary>
public class ViewmodelArtImporter : AssetPostprocessor
{
    public const string ViewmodelFolder = "Assets/_Game/Art/Viewmodel";

    // Subir la versión reimporta todo el arte con los ajustes nuevos
    public override uint GetVersion() => 2;

    private static readonly HashSet<string> _triedFix = new HashSet<string>();

    private void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        if (!IsViewmodelTexture(path)) return;
        Apply((TextureImporter)assetImporter);
    }

    private static bool IsViewmodelTexture(string path)
    {
        return path.StartsWith(ViewmodelFolder + "/") && path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase);
    }

    private static void Apply(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = true;
        importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
        importer.mipMapsPreserveCoverage = false;
        importer.streamingMipmaps = false;
        importer.filterMode = FilterMode.Trilinear;
        importer.maxTextureSize = 2048;
        importer.alphaIsTransparency = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.sRGBTexture = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.isReadable = false;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMode = (int)SpriteImportMode.Single;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePixelsPerUnit = 100f;
        importer.SetTextureSettings(settings);
    }

    private static bool NeedsFix(TextureImporter importer)
    {
        return importer.textureType != TextureImporterType.Sprite
               || importer.spriteImportMode != SpriteImportMode.Single
               || importer.textureCompression != TextureImporterCompression.Uncompressed
               || !importer.mipmapEnabled
               || importer.filterMode != FilterMode.Trilinear
               || importer.maxTextureSize != 2048
               || !importer.alphaIsTransparency;
    }

    /// <summary>Reimporta (una sola vez por sesión) los PNG del viewmodel que no tengan los ajustes correctos.</summary>
    private static void FixImporters(IEnumerable<string> paths)
    {
        int fixedCount = 0;
        foreach (string raw in paths)
        {
            string path = raw.Replace('\\', '/');
            if (!IsViewmodelTexture(path) || _triedFix.Contains(path)) continue;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || !NeedsFix(importer)) continue;
            _triedFix.Add(path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); // OnPreprocessTexture aplica los ajustes
            fixedCount++;
        }
        if (fixedCount > 0) Debug.Log("[Viewmodel] " + fixedCount + " PNG reimportados con los ajustes de calidad del viewmodel.");
    }

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        FixImporters(imported);
    }

    [InitializeOnLoadMethod]
    private static void CheckOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (!AssetDatabase.IsValidFolder(ViewmodelFolder)) return;
            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ViewmodelFolder }))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            FixImporters(paths);
        };
    }
}

/// <summary>
/// MENÚ: Plomo y Paja → Crear Perfil Viewmodel Escopeta
/// Crea o actualiza Assets/_Game/Art/Viewmodel/VM_Shotgun.asset con los PNG de Viewmodel/Shotgun
/// (wilbur_vm_shotgun_&lt;clip&gt;_&lt;nn&gt;.png) y el fogonazo de Viewmodel/FX (fx_fogonazo_&lt;nn&gt;.png).
/// Al actualizar SOLO cambia los dibujos: anclas, tamaños y movimiento ajustados a mano se conservan.
/// El revólver usa la misma función (GetOrCreateRevolverProfile / menú "Crear Perfil Viewmodel Revólver").
/// </summary>
public static class ViewmodelProfileBuilder
{
    public const string ShotgunProfilePath = ViewmodelArtImporter.ViewmodelFolder + "/VM_Shotgun.asset";

    [MenuItem("Plomo y Paja/Crear Perfil Viewmodel Escopeta")]
    public static void BuildShotgunMenu()
    {
        if (EditorMenuGuard.BlockedInPlayMode()) return;
        ViewmodelProfile profile = GetOrCreateShotgunProfile();
        Selection.activeObject = profile;
    }

    public static ViewmodelProfile GetOrCreateShotgunProfile()
    {
        return BuildProfile("shotgun", "Shotgun", "VM_Shotgun", "fx_fogonazo");
    }

    public const string RevolverProfilePath = ViewmodelArtImporter.ViewmodelFolder + "/VM_Revolver.asset";

    [MenuItem("Plomo y Paja/Crear Perfil Viewmodel Revólver")]
    public static void BuildRevolverMenu()
    {
        if (EditorMenuGuard.BlockedInPlayMode()) return;
        Selection.activeObject = GetOrCreateRevolverProfile();
    }

    /// <summary>
    /// VM_Revolver: idle, fire, aimidle y reload (abrir, meter, cerrar = 3 cuadros). Sin cycle: el revólver no bombea.
    /// Los valores propios del revólver (anclas, fogonazo más chico, culatazo más suave) solo se ponen al CREAR el asset.
    /// </summary>
    public static ViewmodelProfile GetOrCreateRevolverProfile()
    {
        return BuildProfile("revolver", "Revolver", "VM_Revolver", "fx_fogonazo", p =>
        {
            p.hipMuzzle = new Vector2(1185f, 690f);   // boca del cañón en el cuadro "fire" (estimada)
            p.aimMuzzle = new Vector2(960f, 545f);    // boca del cañón apuntando (estimada)
            p.flashSize = 280f;
            p.kickOffset = new Vector2(30f, -45f);
            p.kickRotation = 6f;
            p.fire.minDuration = 0.12f;
            p.aimFire.minDuration = 0.12f;
        });
    }

    public static ViewmodelProfile BuildProfile(string weaponId, string weaponFolder, string assetName, string flashPrefix,
                                                System.Action<ViewmodelProfile> onCreate = null)
    {
        string folder = ViewmodelArtImporter.ViewmodelFolder;
        LevelBuilderUtils.EnsureFolder(folder);
        string assetPath = $"{folder}/{assetName}.asset";

        var profile = AssetDatabase.LoadAssetAtPath<ViewmodelProfile>(assetPath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<ViewmodelProfile>();
            profile.weaponId = weaponId;
            if (onCreate != null) onCreate(profile);
            AssetDatabase.CreateAsset(profile, assetPath);
        }

        // clip → (número → sprite)
        Dictionary<string, SortedDictionary<int, Sprite>> clips = LoadNumbered(
            $"{folder}/{weaponFolder}", new Regex($@"^wilbur_vm_{Regex.Escape(weaponId)}_([a-z]+)_(\d+)$"));

        profile.idle.frames = Frames(clips, "idle");
        profile.fire.frames = Frames(clips, "fire");
        profile.cycle.frames = Frames(clips, "cycle");
        profile.reload.frames = Frames(clips, "reload");
        profile.aimIdle.frames = Frames(clips, "aimidle");
        profile.aimFire.frames = Frames(clips, "aimfire");
        profile.aimCycle.frames = Frames(clips, "aimcycle");

        Dictionary<string, SortedDictionary<int, Sprite>> fx = LoadNumbered(
            $"{folder}/FX", new Regex(@"^(fx_[a-z]+)_(\d+)$"));
        profile.flashSprites = Frames(fx, flashPrefix);

        foreach (string clip in clips.Keys)
            if (System.Array.IndexOf(KnownClips, clip) < 0)
                Debug.LogWarning($"[Viewmodel] Clip desconocido '{clip}' en {weaponFolder} (válidos: {string.Join(", ", KnownClips)})");

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Viewmodel] {assetName}: idle {profile.idle.frames.Length}, fire {profile.fire.frames.Length}, " +
                  $"cycle {profile.cycle.frames.Length}, reload {profile.reload.frames.Length}, aimIdle {profile.aimIdle.frames.Length}, " +
                  $"aimFire {profile.aimFire.frames.Length}, aimCycle {profile.aimCycle.frames.Length}, fogonazos {profile.flashSprites.Length}");
        return profile;
    }

    private static readonly string[] KnownClips = { "idle", "fire", "cycle", "reload", "aimidle", "aimfire", "aimcycle" };

    private static Dictionary<string, SortedDictionary<int, Sprite>> LoadNumbered(string folder, Regex pattern)
    {
        var result = new Dictionary<string, SortedDictionary<int, Sprite>>();
        if (!AssetDatabase.IsValidFolder(folder)) return result;

        foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetDirectoryName(path).Replace('\\', '/') != folder) continue; // sin subcarpetas
            Match m = pattern.Match(Path.GetFileNameWithoutExtension(path).ToLowerInvariant());
            if (!m.Success) continue;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) continue;

            string key = m.Groups[1].Value;
            if (!result.TryGetValue(key, out var frames)) result[key] = frames = new SortedDictionary<int, Sprite>();
            frames[int.Parse(m.Groups[2].Value)] = sprite;
        }
        return result;
    }

    private static Sprite[] Frames(Dictionary<string, SortedDictionary<int, Sprite>> clips, string key)
    {
        return clips.TryGetValue(key, out var frames) ? new List<Sprite>(frames.Values).ToArray() : new Sprite[0];
    }
}
