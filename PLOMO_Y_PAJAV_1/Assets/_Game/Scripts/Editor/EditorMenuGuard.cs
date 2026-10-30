using UnityEditor;
using UnityEngine;

/// <summary>
/// Los menús "Plomo y Paja/..." crean escenas, prefabs y assets: en Play fallan a medias
/// (p. ej. InvalidOperationException en SaveCurrentModifiedScenesIfUserWantsTo). Cada menú empieza con
/// <c>if (EditorMenuGuard.BlockedInPlayMode()) return;</c>.
/// </summary>
public static class EditorMenuGuard
{
    public const string Message = "Sal de Play antes de usar este menú";

    /// <summary>true (con aviso) si el Editor está en Play o a punto de entrar en Play.</summary>
    public static bool BlockedInPlayMode()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode) return false;
        Debug.LogWarning("[Plomo y Paja] " + Message + ".");
        EditorUtility.DisplayDialog("Plomo y Paja", Message + ".", "Entendido");
        return true;
    }
}
