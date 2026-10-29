using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Orquesta el flujo general del juego: inicio, derrota, finales, reinicio.
/// DÓNDE VA: GameObject "GameManagers" (mismo que GameStateTracker y AICompanion).
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
        // Pausa con Escape
        if (Input.GetKeyDown(KeyCode.Escape) && !gameIsOver)
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
    /// Reintentar desde la pantalla de muerte — recarga la escena actual.
    /// </summary>
    public void RetryCurrentLevel()
    {
        gameIsOver = false;
        if (GameStateTracker.Instance != null) GameStateTracker.Instance.ResetState();
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Ir al menú principal.
    /// </summary>
    public void GoToMainMenu()
    {
        gameIsOver = false;
        if (GameStateTracker.Instance != null) GameStateTracker.Instance.ResetState();
        Time.timeScale = 1f;
        SceneManager.LoadScene(0); // MainMenu
    }

    /// <summary>
    /// Cargar el siguiente nivel.
    /// </summary>
    public void LoadNextLevel()
    {
        int currentIndex = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(currentIndex + 1);
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

    public void TogglePause()
    {
        gameIsPaused = !gameIsPaused;
        Time.timeScale = gameIsPaused ? 0f : 1f;
        if (UIManager.Instance != null) UIManager.Instance.ShowPauseMenu(gameIsPaused);

        Cursor.lockState = gameIsPaused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = gameIsPaused;
    }

    public bool IsGameOver => gameIsOver;
    public bool IsGamePaused => gameIsPaused;
}
