using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Orden canónico de Build Settings, compartido por todos los generadores de escenas:
/// 0 MenuPrincipal, 1 Intro, 2 NivelPorky, 3 Campamento, luego el resto de escenas de juego.
/// Idempotente. Nunca quita una escena que otra persona haya añadido. SampleScene queda fuera.
/// </summary>
public static class BuildSceneOrder
{
    public const string MenuPath = "Assets/_Game/Scenes/MenuPrincipal.unity";
    public const string IntroPath = "Assets/Scenes/Intro.unity";
    public const string NivelPorkyPath = "Assets/Scenes/NivelPorky.unity";
    public const string CampamentoPath = "Assets/Scenes/Campamento.unity";
    public const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    /// <summary>Escenas con posición fija. Una canónica que aún no existe en disco se omite con aviso.</summary>
    private static readonly string[] Canonical =
    {
        MenuPath,
        IntroPath,
        NivelPorkyPath,
        CampamentoPath,
    };

    /// <summary>Aplica el orden. Devuelve la lista final; si <paramref name="report"/> no es null añade una línea.</summary>
    public static List<EditorBuildSettingsScene> Ensure(StringBuilder report = null)
    {
        var current = EditorBuildSettings.scenes;
        var list = new List<EditorBuildSettingsScene>();
        var used = new HashSet<string>();

        // 1) Las canónicas, en su orden, si existen en disco (si no, aviso y se omiten).
        foreach (string path in Canonical)
        {
            if (!File.Exists(path))
            {
                string msg = "[BuildSceneOrder] No existe " + path + ": se omite en Build Settings.";
                Debug.LogWarning(msg);
                if (report != null) report.AppendLine("⚠ " + msg);
                continue;
            }
            list.Add(new EditorBuildSettingsScene(path, true));
            used.Add(path);
        }

        // 2) Lo que ya estaba (de otros) se conserva detrás, con su orden y su estado. SampleScene nunca.
        foreach (EditorBuildSettingsScene s in current)
        {
            if (used.Contains(s.path) || s.path == SampleScenePath) continue;
            if (!File.Exists(s.path))
                Debug.LogWarning("[BuildSceneOrder] " + s.path + " no existe en disco; se deja en Build Settings sin tocar.");
            list.Add(new EditorBuildSettingsScene(s.path, s.enabled));
            used.Add(s.path);
        }

        EditorBuildSettings.scenes = list.ToArray();

        if (report != null)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
                sb.Append(i > 0 ? ", " : "").Append(i).Append('=').Append(Path.GetFileNameWithoutExtension(list[i].path));
            report.AppendLine("✓ Build Settings: " + sb + " (SampleScene fuera)");
        }
        return list;
    }
}
