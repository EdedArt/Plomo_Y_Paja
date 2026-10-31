using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Importación de Assets/_Game/Art/Viewmodel/ (manos en primera persona y efectos):
/// Sprite (2D and UI), Single, Bilinear, sin mipmaps, máximo 2048, compresión de alta calidad.
/// Contrato del arte: Assets/_Game/Art/Viewmodel/README_ART.md
/// </summary>
public class ViewmodelArtImporter : AssetPostprocessor
{
    public const string ViewmodelFolder = "Assets/_Game/Art/Viewmodel";

    public override uint GetVersion() => 1;

    private void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.StartsWith(ViewmodelFolder + "/")) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.alphaIsTransparency = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.wrapMode = TextureWrapMode.Clamp;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMode = (int)SpriteImportMode.Single;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePixelsPerUnit = 100f;
        importer.SetTextureSettings(settings);
    }
}

/// <summary>
/// MENÚ: Plomo y Paja → Crear Perfil Viewmodel Escopeta
/// Crea o actualiza Assets/_Game/Art/Viewmodel/VM_Shotgun.asset con los PNG de Viewmodel/Shotgun
/// (wilbur_vm_shotgun_&lt;clip&gt;_&lt;nn&gt;.png) y el fogonazo de Viewmodel/FX (fx_fogonazo_&lt;nn&gt;.png).
/// Al actualizar SOLO cambia los dibujos: anclas, tamaños y movimiento ajustados a mano se conservan.
/// Fase 2: BuildProfile("revolver", "Revolver", "VM_Revolver", "fx_fogonazo") para el revólver.
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

    public static ViewmodelProfile BuildProfile(string weaponId, string weaponFolder, string assetName, string flashPrefix)
    {
        string folder = ViewmodelArtImporter.ViewmodelFolder;
        LevelBuilderUtils.EnsureFolder(folder);
        string assetPath = $"{folder}/{assetName}.asset";

        var profile = AssetDatabase.LoadAssetAtPath<ViewmodelProfile>(assetPath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<ViewmodelProfile>();
            profile.weaponId = weaponId;
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
