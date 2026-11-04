using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Los cuatro libros de final (decisión L8).</summary>
public enum FinalId
{
    A_BalaFacil,
    B_Muerto,
    B_Perdonado,
    C_Verdad
}

/// <summary>
/// Qué final toca (decisión L8). Resolve es una función pura; N2.3 le agrega el menú "Final → Probar resolución".
///   Don Lobo muerto:    menos de 2 pistas → A; 2 o más → B (versión muerto).
///   Don Lobo perdonado: 3 o más pistas y 2 o más interrogatorios → C; cualquier otro caso → B (versión perdonado).
/// GoToFinale la llaman N2.2 (rematado de rodillas) y N2.3 (después de la decisión). Mientras no exista la escena Final
/// (N2.4), solo registra el resultado en la consola.
/// </summary>
public static class FinaleResolver
{
    public const string FinalScene = "Final";

    /// <summary>El último final resuelto: lo lee la escena Final (N2.4).</summary>
    public static FinalId LastResolved { get; private set; }

    public static FinalId Resolve(LoboState lobo, int clues, int interrogations)
    {
        if (lobo == LoboState.Dead) return clues < 2 ? FinalId.A_BalaFacil : FinalId.B_Muerto;
        return clues >= 3 && interrogations >= 2 ? FinalId.C_Verdad : FinalId.B_Perdonado;
    }

    /// <summary>Con lo que hay ahora en GameStateTracker (pistas de los dos niveles, interrogatorios completados).</summary>
    public static FinalId ResolveCurrent()
    {
        GameStateTracker tracker = GameStateTracker.Instance;
        if (tracker == null)
        {
            Debug.LogWarning("[Final] Sin GameStateTracker: se resuelve como Don Lobo muerto, sin pistas.");
            return Resolve(LoboState.Dead, 0, 0);
        }
        return Resolve(tracker.Lobo, tracker.CluesFound, tracker.InterrogationsCompleted);
    }

    /// <summary>Resuelve y carga el libro del final. Sin la escena Final en Build Settings, se queda en el nivel y lo dice.</summary>
    public static void GoToFinale()
    {
        LastResolved = ResolveCurrent();
        GameStateTracker tracker = GameStateTracker.Instance;
        string state = tracker != null ? $"Lobo {tracker.Lobo}, {tracker.CluesFound} pistas, {tracker.InterrogationsCompleted} interrogatorios" : "sin tracker";
        Debug.Log($"[Final] Final resuelto: {LastResolved} ({state}).");

        if (Application.CanStreamedLevelBeLoaded(FinalScene)) SceneManager.LoadScene(FinalScene);
        else Debug.Log($"[Final] La escena {FinalScene} todavía no está en Build Settings (N2.4): el juego se queda en el nivel.");
    }
}
