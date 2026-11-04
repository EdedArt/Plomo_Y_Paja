using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Tarjetas ilustradas del tutorial (N1.3b). Recorta el fondo magenta de _entrante_ui/tutorial/tut_*.(png|jpg) con
/// Tools/character_key.py --single --crop 2 --defringe y deja los PNG con alfa en Assets/_Game/Art/UI/Tutorial/.
/// La importación (Sprite de UI, sin mipmaps, 2048, compresión HQ, Clamp) la fija CharacterSpriteImporter por carpeta.
/// MENÚ: Plomo y Paja → Nivel 1 → Importar tarjetas de tutorial (idempotente; también lo corre Colocar tutorial / Generar Nivel Porky,
/// y TutorialBuilder asigna los sprites al TutorialDirector). Un archivo que falte es un aviso, nunca un bloqueo: ese paso conserva
/// las etiquetas de texto de antes. Si el PNG ya está recortado y más nuevo que el original, no se vuelve a recortar.
/// </summary>
public static class TutorialCardImporter
{
    public const string DestFolder = CharacterSpriteImporter.TutorialFolder;
    private const string IncomingFolder = "_entrante_ui/tutorial";
    private static readonly string[] Extensions = { ".png", ".jpg", ".jpeg" };

    public const string Pensamientos = "tut_pensamientos";
    public const string Sello = "tut_sello_listo";
    public static readonly string[] MoveCards = { "tut_caminar", "tut_correr", "tut_examinar" };
    public static readonly string[] WeaponCards = { "tut_apuntar", "tut_disparar", "tut_recargar" };
    public static readonly string[] RevolverCards = { "tut_cambiar_arma" };

    [MenuItem("Plomo y Paja/Nivel 1/Importar tarjetas de tutorial")]
    public static void ImportMenu()
    {
        if (EditorMenuGuard.BlockedInPlayMode()) return;
        var report = new StringBuilder();
        ImportAll(report);
        if (File.Exists(LevelBuilder_NivelPorky.ScenePath))
        {
            TutorialBuilder.BuildMenu(); // reconstruye el tutorial de la escena: ahí se asignan los sprites al TutorialDirector
            Debug.Log("[Nivel 1] Tarjetas de tutorial:\n" + report);
        }
        else
        {
            Debug.Log("[Nivel 1] Tarjetas de tutorial:\n" + report);
            EditorUtility.DisplayDialog("Importar tarjetas de tutorial", report + "\nNo existe la escena de Nivel Porky: los sprites se asignan al correr Generar Nivel Porky.", "Entendido");
        }
    }

    /// <summary>Recorta e importa todo lo que haya. Nunca lanza ni bloquea.</summary>
    public static void ImportAll(StringBuilder report)
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        Directory.CreateDirectory(Path.Combine(root, DestFolder));
        int ok = 0, missing = 0;
        foreach (string name in AllNames())
        {
            string dest = DestFolder + "/" + name + ".png";
            string destFull = Path.Combine(root, dest);
            string src = FindSource(root, name);
            if (src != null && (!File.Exists(destFull) || File.GetLastWriteTimeUtc(src) > File.GetLastWriteTimeUtc(destFull)))
            {
                if (!Key(root, src, destFull, report)) { if (!File.Exists(destFull)) { missing++; continue; } }
                AssetDatabase.ImportAsset(dest, ImportAssetOptions.ForceUpdate);
            }
            if (File.Exists(destFull))
            {
                ok++;
                if (AssetDatabase.LoadAssetAtPath<Sprite>(dest) == null) AssetDatabase.ImportAsset(dest, ImportAssetOptions.ForceUpdate);
            }
            else
            {
                missing++;
                string optional = name == Sello ? " (opcional: se dibuja un sello rojo)" : " (ese paso conserva las etiquetas de texto)";
                report.AppendLine($"⚠ Falta {name} en {IncomingFolder}/{optional}");
                Debug.LogWarning($"[Tutorial] Falta {name}{optional}");
            }
        }
        report.AppendLine($"✓ Tarjetas de tutorial: {ok} listas, {missing} faltan");
    }

    public static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(DestFolder + "/" + name + ".png");

    public static Sprite[] LoadAll(string[] names)
    {
        var result = new Sprite[names.Length];
        for (int i = 0; i < names.Length; i++) result[i] = Load(names[i]);
        return result;
    }

    private static string[] AllNames()
    {
        var all = new System.Collections.Generic.List<string> { Pensamientos };
        all.AddRange(MoveCards);
        all.AddRange(WeaponCards);
        all.AddRange(RevolverCards);
        all.Add(Sello);
        return all.ToArray();
    }

    private static string FindSource(string root, string name)
    {
        foreach (string ext in Extensions)
        {
            string path = Path.Combine(root, IncomingFolder, name + ext);
            if (File.Exists(path)) return path;
        }
        return null;
    }

    private static bool Key(string root, string src, string dest, StringBuilder report)
    {
        string args = $"Tools/character_key.py --single \"{src}\" --out \"{dest}\" --crop 2 --defringe";
        foreach (string exe in new[] { "python", "py" })
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                };
                using (Process p = Process.Start(psi))
                {
                    p.StandardOutput.ReadToEnd();
                    string err = p.StandardError.ReadToEnd();
                    p.WaitForExit(120000);
                    if (p.ExitCode == 0) return true;
                    report.AppendLine($"⚠ El recorte de {Path.GetFileName(src)} falló: {err}");
                    Debug.LogWarning($"[Tutorial] Recorte de {Path.GetFileName(src)} falló: {err}");
                    return false;
                }
            }
            catch (System.ComponentModel.Win32Exception) { /* este ejecutable no existe: prueba el siguiente */ }
        }
        report.AppendLine("⚠ No hay Python en el PATH: no se pueden recortar las tarjetas nuevas (corre Tools/character_key.py a mano).");
        Debug.LogWarning("[Tutorial] No hay Python en el PATH.");
        return false;
    }
}
