using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Orquesta el flujo general del juego: inicio, derrota, finales, reinicio.
/// DÓNDE VA: lo crea GameManagersBootstrap al arrancar el juego (N2.1.1). La copia que traiga una escena se destruye sola.
///
/// ESCENAS NECESARIAS (configurar en File → Build Settings):
/// Index 0: "MainMenu"
/// Index 1: "NivelPorky"   — Casa del hermano menor
/// Index 2: "NivelLobo"    — Ciudad/Campamento de Don Lobo
/// Index 3: "Final_A"      — La bala fácil
/// Index 4: "Final_B"      — Confianza equivocada
/// Index 5: "Final_C"      — La verdad pesa más
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Estado del juego")]
    [SerializeField] private bool gameIsOver = false;
    [SerializeField] private bool gameIsPaused = false;

    [Header("Delay antes de pantallas de game over / final")]
    [SerializeField] private float deathScreenDelay = 2f;
    [SerializeField] private float endingScreenDelay = 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // Pausa con Escape (desactivada durante el interrogatorio: la escena ya congela el tiempo, fase 3C.3)
        // ni durante una cinemática (INTRO.2): ahí Esc mantenido es "saltar"
        // ni en el menú ni en el libro (MENU.1), las mismas escenas sin pausa que PauseMenuSpawner: este objeto sobrevive
        // al volver del nivel, y ahí Esc es "volver" (menú) o "saltar" (libro)
        if (Input.GetKeyDown(KeyCode.Escape) && !gameIsOver && !InterrogationDirector.IsActive && !CinematicState.IsPlaying
            && !IsSceneWithoutPause())
        {
            TogglePause();
        }
    }

    // ============ FLUJO DEL JUEGO ============

    /// <summary>
    /// Llamar cuando el jugador muere (conectar desde PlayerHealth.onPlayerDied).
    /// </summary>
    public void PlayerDied()
    {
        if (gameIsOver) return;
        gameIsOver = true;
        StartCoroutine(ShowDeathScreen());
    }

    private IEnumerator ShowDeathScreen()
    {
        yield return new WaitForSeconds(deathScreenDelay);
        if (UIManager.Instance != null) UIManager.Instance.ShowDeathScreen();
    }

    /// <summary>
    /// El jugador murió y LevelDeathRestart va a recargar (N2.1.1): sin pausa ni control hasta entonces.
    /// No hay pantalla de muerte en el juego, así que no pasa por PlayerDied.
    /// </summary>
    public void MarkGameOverForRestart()
    {
        gameIsOver = true;
    }

    /// <summary>
    /// Reiniciar este nivel (muerte o Pausa → Reiniciar): vuelve al progreso con que se entró (N2.1.1) y recarga la escena.
    /// Las banderas de una vez por sesión siguen: sin taxi ni llegada otra vez. Por nombre, como las demás cargas por escena.
    /// </summary>
    public void RetryCurrentLevel()
    {
        gameIsOver = false;
        if (gameIsPaused) TogglePause(); // este objeto sobrevive a la recarga: no debe entrar pausado a la escena nueva
        if (GameStateTracker.Instance != null) GameStateTracker.Instance.RestoreLevelSnapshot();
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>"Jugar" en el menú (N2.1.1): partida desde cero, banderas de sesión incluidas.</summary>
    public void StartNewGame()
    {
        gameIsOver = false;
        if (gameIsPaused) TogglePause();
        Time.timeScale = 1f;
        if (GameStateTracker.Instance != null) GameStateTracker.Instance.ResetForNewGame();
    }

    /// <summary>
    /// Ir al menú principal.
    /// </summary>
    public void GoToMainMenu()
    {
        gameIsOver = false;
        if (GameStateTracker.Instance != null) GameStateTracker.Instance.ResetState();
        Time.timeScale = 1f;
        // Por nombre (no por índice). Si MenuPrincipal aún no está en Build Settings, cae al índice 0.
        if (Application.CanStreamedLevelBeLoaded("MenuPrincipal")) SceneManager.LoadScene("MenuPrincipal");
        else SceneManager.LoadScene(0);
    }

    /// <summary>
    /// Cargar el siguiente nivel.
    /// </summary>
    public void LoadNextLevel()
    {
        int currentIndex = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(currentIndex + 1);
    }

    /// <summary>¿Esta escena se puede cargar? (está en Build Settings)</summary>
    public static bool CanLoadLevel(string sceneName) => Application.CanStreamedLevelBeLoaded(sceneName);

    /// <summary>
    /// Pasar a otro nivel por nombre, en segundo plano (N1.4, salida del bosque). Por nombre porque el orden de Build Settings no es
    /// de quien llama. Es carga Single: GameStateTracker saca la foto de entrada en sceneLoaded. Sin esa escena devuelve null y no cambia nada.
    /// activateWhenReady = false: quien llama activa con allowSceneActivation al terminar su fundido.
    /// </summary>
    public AsyncOperation LoadLevelAsync(string sceneName, bool activateWhenReady = true)
    {
        if (!CanLoadLevel(sceneName)) return null;
        gameIsOver = false;
        if (gameIsPaused) TogglePause(); // este objeto sobrevive a la carga: no debe entrar pausado a la escena nueva
        Time.timeScale = 1f;
        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (load != null) load.allowSceneActivation = activateWhenReady;
        return load;
    }

    /// <summary>
    /// Triggerear el final del juego basado en las decisiones del jugador.
    /// Llamar desde FinaleTrigger cuando el jugador completa el juego.
    /// </summary>
    public void TriggerEnding()
    {
        if (gameIsOver) return;
        gameIsOver = true;
        StartCoroutine(LoadEndingScene());
    }

    private IEnumerator LoadEndingScene()
    {
        yield return new WaitForSeconds(endingScreenDelay);

        GameEnding ending = GameStateTracker.Instance != null
            ? GameStateTracker.Instance.DetermineEnding()
            : GameEnding.BlindJustice;

        Debug.Log($"[GameManager] Final determinado: {ending}");

        switch (ending)
        {
            case GameEnding.BlindJustice:
                SceneManager.LoadScene("Final_A");
                break;
            case GameEnding.PartialTruth:
                SceneManager.LoadScene("Final_B");
                break;
            case GameEnding.TruthAndForgiveness:
                SceneManager.LoadScene("Final_C");
                break;
        }
    }

    // ============ PAUSA ============

    /// <summary>
    /// Se dispara en cada TogglePause con el nuevo estado (true = pausado).
    /// Lo escucha PauseMenu (UI/Pause). Se limpia al cargar el subsistema para
    /// no arrastrar suscriptores muertos (GameManager es DontDestroyOnLoad).
    /// </summary>
    public static event System.Action<bool> OnPauseChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPauseEvent()
    {
        OnPauseChanged = null;
    }

    private static bool IsSceneWithoutPause()
    {
        string scene = SceneManager.GetActiveScene().name;
        return scene == "MenuPrincipal" || scene == "Intro";
    }

    public void TogglePause()
    {
        gameIsPaused = !gameIsPaused;
        Time.timeScale = gameIsPaused ? 0f : 1f;
        OnPauseChanged?.Invoke(gameIsPaused);

        Cursor.lockState = gameIsPaused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = gameIsPaused;
    }

    public bool IsGameOver => gameIsOver;
    public bool IsGamePaused => gameIsPaused;
}
