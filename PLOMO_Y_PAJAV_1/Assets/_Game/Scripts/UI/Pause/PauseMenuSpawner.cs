using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instancia Resources/PauseMenu_Canvas en cada escena de juego.
/// No hace nada en MenuPrincipal ni en Intro, ni si ya hay un PauseMenu.
/// Si el prefab no existe (no se corrio el menu del editor), solo avisa una vez.
/// </summary>
public static class PauseMenuSpawner
{
    private const string RutaPrefab = "PauseMenu_Canvas";
    private static bool avisado;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        avisado = false;
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;
        Intentar();
    }

    private static void AlCargarEscena(Scene escena, LoadSceneMode modo)
    {
        Intentar();
    }

    private static void Intentar()
    {
        string nombre = SceneManager.GetActiveScene().name;
        if (nombre == "MenuPrincipal" || nombre == "Intro") return;
        if (Object.FindObjectOfType<PauseMenu>(true) != null) return;

        var prefab = Resources.Load<GameObject>(RutaPrefab);
        if (prefab == null)
        {
            if (!avisado)
            {
                Debug.LogWarning("[PauseMenuSpawner] Falta Resources/" + RutaPrefab +
                                 ". Corre 'Plomo y Paja/Crear menu de pausa'.");
                avisado = true;
            }
            return;
        }

        var go = Object.Instantiate(prefab);
        go.name = RutaPrefab;
    }
}
