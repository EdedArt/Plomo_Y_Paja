using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ajustes de importación para todo lo que se suelte en Audio/Voices y Audio/SFX:
/// - Siempre mono (los sonidos 3D no necesitan estéreo)
/// - Voces: Decompress On Load
/// - Nombres que empiezan por "lluvia_" o "musica_": Streaming (son largos)
/// </summary>
public class GameAudioImporter : AssetPostprocessor
{
    public const string AudioRoot = "Assets/_Game/Audio";
    public const string VoicesFolder = AudioRoot + "/Voices";
    public const string SfxFolder = AudioRoot + "/SFX";

    private void OnPreprocessAudio()
    {
        string path = assetPath.Replace('\\', '/');
        bool isVoice = path.StartsWith(VoicesFolder + "/");
        bool isSfx = path.StartsWith(SfxFolder + "/");
        if (!isVoice && !isSfx) return;

        var importer = (UnityEditor.AudioImporter)assetImporter;
        importer.forceToMono = true;

        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        string file = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        if (file.StartsWith("lluvia_") || file.StartsWith("musica_"))
            settings.loadType = AudioClipLoadType.Streaming;
        else if (isVoice)
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
        importer.defaultSampleSettings = settings;
    }
}

/// <summary>
/// MENÚ: Plomo y Paja → Reconstruir Librería de Audio
/// Lee Audio/Voices y Audio/SFX y llena Assets/_Game/Audio/AudioLibrary.asset (lo crea si falta).
/// </summary>
public static class AudioLibraryBuilder
{
    public const string LibraryPath = GameAudioImporter.AudioRoot + "/AudioLibrary.asset";

    private static readonly string[] AudioExtensions = { ".wav", ".ogg", ".mp3", ".aif", ".aiff" };
    private static readonly Regex VariantSuffix = new Regex(@"^(.+)_(\d+)$");

    [MenuItem("Plomo y Paja/Reconstruir Librería de Audio")]
    public static void Rebuild()
    {
        if (EditorMenuGuard.BlockedInPlayMode()) return;
        AudioLibrary library = GetOrCreateLibrary();

        // Voces: lineId = nombre del archivo
        var voices = new List<AudioLibrary.VoiceEntry>();
        foreach (string path in FindAudio(GameAudioImporter.VoicesFolder))
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) continue;
            voices.Add(new AudioLibrary.VoiceEntry { lineId = Path.GetFileNameWithoutExtension(path), clip = clip });
        }
        voices.Sort((a, b) => string.CompareOrdinal(a.lineId, b.lineId));

        // SFX: "<nombre>_<NN>" se agrupa por <nombre>; sin sufijo, el archivo es su propio grupo
        var groups = new SortedDictionary<string, SortedDictionary<int, AudioClip>>(System.StringComparer.Ordinal);
        foreach (string path in FindAudio(GameAudioImporter.SfxFolder))
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) continue;

            string file = Path.GetFileNameWithoutExtension(path);
            string name = file;
            int order = 0;
            Match m = VariantSuffix.Match(file);
            if (m.Success)
            {
                name = m.Groups[1].Value;
                order = int.Parse(m.Groups[2].Value);
            }

            if (!groups.TryGetValue(name, out var variants))
                groups[name] = variants = new SortedDictionary<int, AudioClip>();
            while (variants.ContainsKey(order)) order++;
            variants[order] = clip;
        }

        var sfx = new List<AudioLibrary.SfxEntry>();
        foreach (var pair in groups)
            sfx.Add(new AudioLibrary.SfxEntry { name = pair.Key, variants = new List<AudioClip>(pair.Value.Values).ToArray() });

        library.EditorSetContent(voices.ToArray(), sfx.ToArray());
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();

        Debug.Log($"[AudioLibrary] {voices.Count} voces y {sfx.Count} efectos en {LibraryPath}");
    }

    /// <summary>Carga AudioLibrary.asset o lo crea vacío (lo usan los prefabs).</summary>
    public static AudioLibrary GetOrCreateLibrary()
    {
        var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
        if (library != null) return library;

        LevelBuilderUtils.EnsureFolder(GameAudioImporter.AudioRoot);
        library = ScriptableObject.CreateInstance<AudioLibrary>();
        AssetDatabase.CreateAsset(library, LibraryPath);
        AssetDatabase.SaveAssets();
        return library;
    }

    private static IEnumerable<string> FindAudio(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder)) yield break;
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (System.Array.IndexOf(AudioExtensions, ext) >= 0) yield return path;
        }
    }
}
