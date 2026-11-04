using UnityEngine;

/// <summary>
/// Crea los managers del juego al arrancar, en cualquier escena (N2.1.1): GameManager, GameStateTracker y LevelDeathRestart, en
/// un solo objeto "GameManagers" con DontDestroyOnLoad. Así ninguna escena tiene que traerlos, y abrir NivelPorky o Campamento
/// directo en el Editor funciona igual. Corre antes de cargar la primera escena: los Awake de la escena ya los encuentran, y la
/// copia que trae Campamento se destruye sola (es su patrón Singleton).
/// </summary>
public static class GameManagersBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (GameManager.Instance != null && GameStateTracker.Instance != null) return;

        var go = new GameObject("GameManagers");
        if (GameManager.Instance == null) go.AddComponent<GameManager>();
        if (GameStateTracker.Instance == null) go.AddComponent<GameStateTracker>();
        go.AddComponent<LevelDeathRestart>();
    }
}
