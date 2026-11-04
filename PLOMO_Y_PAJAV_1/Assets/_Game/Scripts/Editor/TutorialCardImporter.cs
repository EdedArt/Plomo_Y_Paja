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
/// las etiquetas de texto de antes. Si el PNG ya existe en Art/UI/Tutorial/ no se llama a Python.
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

    /// <summary>
    /// Importa lo que haya y deja un resumen. Nunca lanza ni bloquea. Python solo se llama para los PNG que NO existen ya en
    /// Art/UI/Tutorial/ (para volver a recortar uno, bórralo y corre el menú). Sin Python o si falla: UN error con el comando a mano.
    /// </summary>
    public static void ImportAll(StringBuilder report)
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        Directory.CreateDirectory(Path.Combine(root, DestFolder));
        bool pythonFailed = false;
        string manual = null;
        var absent = new System.Collections.Generic.List<string>();
        foreach (string name in AllNames())
        {
            string dest = DestFolder + "/" + name + ".png";
            string destFull = Path.Combine(root, dest);
            if (!File.Exists(destFull))
            {
                string src = FindSource(root, name);
                if (src != null && !pythonFailed)
                {
                    if (Key(root, src, destFull, out string error)) AssetDatabase.ImportAsset(dest, ImportAssetOptions.ForceUpdate);
                    else
                    {
                        pythonFailed = true;
                        manual = $"cd PLOMO_Y_PAJAV_1 && for %f in (_entrante_ui\\tutorial\\tut_*.png) do python Tools/character_key.py --single %f --out Assets/_Game/Art/UI/Tutorial/%~nxf --crop 2 --defringe";
                        string msg = $"[Tutorial] No se pudo recortar las tarjetas con Python ({error}). Córrelo a mano desde PLOMO_Y_PAJAV_1/: {manual}";
                        report.AppendLine("✗ " + msg);
                        Debug.LogError(msg);
                    }
                }
            }
            if (File.Exists(destFull) && AssetDatabase.LoadAssetAtPath<Sprite>(dest) == null)
                AssetDatabase.ImportAsset(dest, ImportAssetOptions.ForceUpdate);
            if (!File.Exists(destFull)) absent.Add(name);
        }

        var cards = new System.Collections.Generic.List<string>(AllNames());
        cards.Remove(Sello); // el sello es opcional: sin él se dibuja un visto rojo
        var missingCards = new System.Collections.Generic.List<string>();
        foreach (string name in cards) if (absent.Contains(name)) missingCards.Add(name);
        string summary = $"Tutorial: {cards.Count - missingCards.Count}/{cards.Count} tarjetas asignadas" +
                         (missingCards.Count > 0 ? $" (faltan: {string.Join(", ", missingCards)})" : "");
        if (absent.Contains(Sello)) summary += " · sello: dibujado (falta " + Sello + ")";
        report.AppendLine((missingCards.Count > 0 ? "⚠ " : "✓ ") + summary);
        if (missingCards.Count > 0) Debug.LogWarning("[Tutorial] " + summary + ": esos pasos conservan las etiquetas de texto.");
        else Debug.Log("[Tutorial] " + summary);
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

    private static bool Key(string root, string src, string dest, out string error)
    {
        error = "no hay Python en el PATH";
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
                    error = $"{Path.GetFileName(src)}: {err.Trim()}";
                    return false;
                }
            }
            catch (System.ComponentModel.Win32Exception) { /* este ejecutable no existe: prueba el siguiente */ }
        }
        return false;
    }
}
